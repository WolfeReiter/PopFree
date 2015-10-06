using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Text.RegularExpressions;
using PopFree.Mime.Interop;

namespace PopFree.Mime
{
    /// <summary>
    /// MimeStreamParser is an efficient MIME v1.0 parser. It works with streams in order to minimize memory allocation.
    /// MimeStreamParser generates ReceivedMessage objects which are derived from System.Net.Mail.MailMessage and 
    /// are generally compatible with System.Net.Mail APIs.
    /// </summary>
    public sealed class MimeStreamParser : IMimeParser
    {
        static MimeStreamParser()
        {
            Instance = new MimeStreamParser();
        }
        /// <summary>
        /// CTOR.
        /// </summary>
        private MimeStreamParser() { }

        /// <summary>
        /// Get singleton reference to a MimeStreamParser object.
        /// </summary>
        public static IMimeParser Instance { get; private set; }

        /// <summary>
        /// Create NameValueCollection from a MIME stream.
        /// </summary>
        /// <param name="stream">MIME stream</param>
        /// <returns>Collection of headers.</returns>
        public NameValueCollection ParseHeaders( Stream stream )
        {
            if( stream == null )
                return null;

            if( stream.CanSeek )
                stream.Seek( 0L, SeekOrigin.Begin );

            return ParseHeaders( new StreamReader( stream ) );
        }
        /// <summary>
        /// Create a NameValueCollection from a TextReader.
        /// </summary>
        /// <param name="reader">TextReader of a MIME Stream.</param>
        /// <returns></returns>
        public NameValueCollection ParseHeaders( TextReader reader )
        {
            if( reader == null )
                return null;

            NameValueCollection headers = new NameValueCollection();
            LoadHeaders( reader, headers );
            return headers;
        }
        /// <summary>
        /// Parse a Stream. The calling code is responsible for closing the reader and the stream.
        /// </summary>
        /// <param name="stream">MIME stream</param>
        public ReceivedMessage Parse( Stream stream )
        {
            if( stream == null )
                return null;

            if( stream.CanSeek )
                stream.Seek( 0L, SeekOrigin.Begin );

            return Parse( new StreamReader( stream ) );
        }

        /// <summary>
        /// Parse a TextReader. The calling code is responsible for closing the reader and the stream.
        /// </summary>
        /// <param name="reader"></param>
        /// <returns>Returns a ReceivedMessage object loaded from the TextReader. Return null if the TextReader is null.</returns>
        public ReceivedMessage Parse( TextReader reader )
        {
            if( reader == null )
                return null;

            ReceivedMessage message = new ReceivedMessage();
            if( !LoadHeaders( reader, message.ReceivedHeaders, message.RawReceivedHeaders ) )
                throw new InvalidDataException( "MIME Stream must contain headers." );

            LoadMailAddresses( message );
            Debug.WriteLine( message.ReceivedHeaders["subject"] );
            message.Subject = message.ReceivedHeaders["subject"];

            //Load message body and attachments  

            /*
             * http://tools.ietf.org/html/rfc2046
             * http://en.wikipedia.org/wiki/MIME#Content-Type
             * 
             * fully supported simple messages:
             * text/plain, text/html (text/html is not compliant, should be multipart/alternative)
             * text/(unrecognized subtype) treated as application/octet-stream
             * 
             * supported multipart messages:
             * multipart/mixed, multipart/alternative
             * multipart/digest
             * 
             * partially supported
             * multipart/related => treat as multipart/mixed. related parts are not linked.
             * multipart/signed -> treat as multipart/mixed
             * multipart/encrypted -> treat as multipart/mixed
             * mutipart/form-data -> treat as multipart/mixed
             * multipart/report -> treat as multipart/mixed
             * multipart/appledouble (RFC 1740) -> resource fork is discarded and the data fork becomes an attachment.
             */

            //Load text/plain and text/html as a simple body text. Otherwise, treat the message as multipart.
            //Anything that is text/plain or text/html should become an alternative view. Everything else becomes an attachment.
                        
            ContentType contentType = GetContentType( message.ReceivedHeaders );
            if( 0 == string.Compare( contentType.MediaType, MediaTypeNames.Text.Plain, true ) ||
                0 == string.Compare( contentType.MediaType, MediaTypeNames.Text.Html, true ) )
            {
                LoadBodyText( message, reader, contentType );
            }
            else
            {
                LoadBodyMultipart( message, reader, contentType );
            }

            return message;
        }

        /// <summary>
        /// Add alternate views and attachments to the existing message object.
        /// Each component is separated boundary text defined in the contentType.Boundary variable.
        /// Each component within a boundary has its own headers defining the payload.
        /// Media types text/plain and text/html go into AlternateView objects.
        /// All other media types will be treated as attachments.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="reader"></param>
        /// <param name="contentType"></param>
        private static void LoadBodyMultipart( ReceivedMessage message, TextReader reader, ContentType contentType )
        {

            /* http://www.w3.org/Protocols/rfc1341/7_2_Multipart.html */
            //each component of the multipart message will be separated by a boundary.
            //each section of text within a boundary should have headers to describe it or in the absence of headers
            //assumed to be ASCII text/plain.
            //a valid message component is preceded and followed by a the boundary text
            //which is two hyphens followed by the value Boundary property of the main ContentType object.

            //UNDONE: Unsupported for now: In the case of multipart/related, the attachments should be done as LinkedResources of the text/html AlternativeView. 
            //This is a nice to have but not critical. Last time I checked, Gmail didn't even support these kinds
            //of internal references, either.

            /*******************************************************************************************
             * the text following the message headers and preceding the first boundary is to be ignored.
             * any text at after the final boundary is also to be ignored.
            /*******************************************************************************************/

            foreach( var part in LoadMultipart( reader, contentType ) )
            {
                if( part is AlternateView )
                    message.AlternateViews.Add( part as AlternateView );
                else if( part is Attachment )
                    message.Attachments.Add( part as Attachment );
            }
            //scroll past epilogue
            string buff;
            while( null != (buff = reader.ReadLine()) && (LineType.EOF != GetLineType( buff, contentType )) );
        }

        private static IEnumerable<AttachmentBase> LoadMultipart( TextReader reader, ContentType contentType )
        {            
            List<AttachmentBase> parts = new List<AttachmentBase>();
            string buff;
            LineType lastLineType = LineType.EOF;
            //scroll past preamble
            while( null != (buff = reader.ReadLine()) && LineType.Normal == (lastLineType = GetLineType( buff, contentType )) ) ;

            while( reader.Peek() > -1 && LineType.TerminalBoundary != lastLineType && LineType.EOF != lastLineType )
            {
                foreach( var bodypart in LoadBodyPart( reader, contentType.Boundary, out lastLineType ) )
                    parts.Add( bodypart );
            }
            
            return parts;
        }

        /// <summary>
        /// Decodes the reader into a new AttachmentBase object. 
        /// Advances the reader to the end of the stream or past the next boundary.
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        private static IEnumerable<AttachmentBase> LoadBodyPart( TextReader reader, string boundary, out LineType lastLineType )
        {
            lastLineType = LineType.EOF;
            ContentType contentType = null;
            //load the headers of the body part
            NameValueCollection headers = new NameValueCollection();
            if( !LoadHeaders( reader, headers ) )
            {
                //no headers in a body part implies a default of media type of text/plain
                //(but the .ctor of ContentType defaults to application/octet-stream
                contentType = new ContentType();
                contentType.MediaType = MediaTypeNames.Text.Plain;
                contentType.Boundary = boundary;
            }
            else
                contentType = GetContentType( headers, boundary );

            List<AttachmentBase> parts = new List<AttachmentBase>();
            if( contentType.MediaType.ToLower().StartsWith( "multipart/" ) )
            {
                parts.AddRange( LoadMultipart( reader, GetContentType( headers ) ) );
                //scroll to the encapsulating boundary of the multipart/alternative
                string line;
                while( null != (line = reader.ReadLine()) && LineType.Normal == (lastLineType = GetLineType( line, contentType )) ) ;
            }
            else if( 0 == string.Compare( contentType.MediaType, MediaTypeNames.Text.Plain, true ) ||
                     0 == string.Compare( contentType.MediaType, MediaTypeNames.Text.Html, true ) )
            {
                AlternateView view = LoadAlternateView( reader, headers, contentType, out lastLineType );
                if( view != null )
                    parts.Add( view );
            }
            else
            {
                Attachment attachment = LoadAttachment( reader, headers, contentType, out lastLineType );
                if( attachment != null )
                    parts.Add( attachment );
            }
            return parts;
        }

        /// <summary>
        /// Decodes the reader into a new AlternateView object.
        /// Advances the reader to the end of the stream or past the next boundary
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="headers"></param>
        /// <param name="contentType"></param>
        /// <returns></returns>
        private static AlternateView LoadAlternateView( TextReader reader, NameValueCollection headers, ContentType contentType, out LineType lastLineType )
        {
            Debug.WriteLine( "LoadAlternateView()" );

            string bodytext = LoadBodyString( reader, headers, contentType, out lastLineType );
            AlternateView view = AlternateView.CreateAlternateViewFromString( bodytext, contentType );
            return view;
        }

        private static string LoadBodyString( TextReader reader, NameValueCollection headers, ContentType contentType, out LineType lastLineType )
        {
            Debug.WriteLine( "LoadBodyString()" );
            lastLineType = LineType.EOF;

            string transferEncoding = GetContentTransferEncoding( headers );
            StringBuilder builder = new StringBuilder();
            string buff;
            while( null != (buff = reader.ReadLine()) )
            {
                Debug.WriteLine( buff );
                lastLineType = GetLineType( buff, contentType );
                if( LineType.Normal == lastLineType )
                    DecodeAndAppendLine( builder, buff, transferEncoding, contentType.CharSet );
                else
                    break;
            }
            return builder.ToString();
        }
        /// <summary>
        /// Decodes the reader into a new Attachment object.
        /// Advances the reader to the end of the stream or past the next boundary.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="headers"></param>
        /// <param name="contentType"></param>
        /// <returns></returns>
        private static Attachment LoadAttachment( TextReader reader, NameValueCollection headers, ContentType contentType, out LineType lastLineType )
        {
            //http://www.ietf.org/rfc/rfc1740.txt
            //http://msdn.microsoft.com/en-us/library/ee219980(EXCHG.80).aspx

            if( IsMediaTypeAppleDouble( contentType ) )
            {
                //reload content-type from headers to get the sub-boundary
                ContentType appledoubleContentType = GetContentType( headers ); 
                Attachment appledouble = LoadAppleDoubleAttachment( reader, appledoubleContentType, out lastLineType );
               
                //scroll to the encapsulating boundary of the multipart/appledouble
                string line;
                while( null != (line = reader.ReadLine()) && LineType.Normal == (lastLineType = GetLineType( line, contentType )) );

                return appledouble;
            }

            string transferEncoding = GetContentTransferEncoding( headers );
            Attachment attachment = null;
            lastLineType = LineType.EOF;

            if( string.IsNullOrEmpty( contentType.Name ) )
                contentType.Name = headers["filename"];

            if( 0 == string.Compare( transferEncoding, "base64", true ) )//binary attachment
            {
                string buff;
                Stream stream = new MemoryStream();

                while( null != (buff = reader.ReadLine()) )
                {
                    //Debug.WriteLine( buff );
                    lastLineType = GetLineType( buff, contentType );
                    if( LineType.Normal == lastLineType )
                    {
                        byte[] b = Convert.FromBase64String( buff );
                        stream.Write( b, 0, b.Length );
                    }
                    else
                        break;

                }
                attachment = new Attachment( stream, contentType );
            }
            else //text attachment
            {
                string buff;
                StringBuilder builder = new StringBuilder();
                while( null != (buff = reader.ReadLine()) )
                {
                    //Debug.WriteLine( buff );
                    lastLineType = GetLineType( buff, contentType );
                    if( LineType.Normal == lastLineType )
                        DecodeAndAppendLine( builder, buff, transferEncoding, contentType.CharSet );
                    else
                        break;
                }
                attachment = Attachment.CreateAttachmentFromString( builder.ToString(), contentType );
            }
            //reset stream
            attachment.ContentStream.Seek( 0L, SeekOrigin.Begin );
            return attachment;
        }

        private static Attachment LoadAppleDoubleAttachment( TextReader reader, ContentType contentType, out LineType lastLineType )
        {
            //multipart/appledouble consists of a resource fork part and a data fork.
            //the first part is the resource fork which contains Apple Finder data which we will discard.

            string line;
            //scroll to start of resource fork
            while( null != (line = reader.ReadLine()) && LineType.Normal == (lastLineType = GetLineType( line, contentType )) );
            //scroll to end of the resource fork data
            while( null != (line = reader.ReadLine()) && LineType.Normal == (lastLineType = GetLineType( line, contentType )) ) ;

            NameValueCollection headers = new NameValueCollection();
            LoadHeaders( reader, headers );
            
            return LoadAttachment( reader, headers, GetContentType( headers, contentType.Boundary ) , out lastLineType );
        }

        private static bool IsMediaTypeAppleDouble( ContentType contentType )
        {
            const string apple_double = "multipart/appledouble";
            return (0 == string.Compare( contentType.MediaType, apple_double )) && !string.IsNullOrEmpty( contentType.Boundary );
        } 

        /// <summary>
        /// Loads From, To, CC and Bcc properties using the values stored in the headers.
        /// </summary>
        /// <param name="message"></param>
        private static void LoadMailAddresses( ReceivedMessage message )
        {
            try
            {
                if( !string.IsNullOrEmpty( message.ReceivedHeaders["from"] ) )
                    message.From = new MailAddress( message.ReceivedHeaders["from"] );
                if( !string.IsNullOrEmpty( message.ReceivedHeaders["reply-to"] ) )
#if NET_FX4
                    message.ReplyToList.Add(new MailAddress(message.ReceivedHeaders["reply-to"]));
#else
                    message.ReplyTo = new MailAddress( message.ReceivedHeaders["reply-to"] );
#endif
            }
            catch( Exception ex )
            {
                //swallow exception thrown by bad data.
                if( !(ex is FormatException) )
                    throw ex;
            }

            LoadMailAddresses( message.To, message.ReceivedHeaders["to"] );
            LoadMailAddresses( message.CC, message.ReceivedHeaders["cc"] );
            LoadMailAddresses( message.Bcc, message.ReceivedHeaders["bcc"] );
        }

        private static void LoadMailAddresses( MailAddressCollection addresses, string addressHeaderValue )
        {
            try
            {
                if( !string.IsNullOrEmpty( addressHeaderValue ) )
                    addresses.Add( addressHeaderValue );
            }
            catch( Exception ex )
            {
                //swallow exception thrown by bad data.
                if( !(ex is FormatException) )
                    throw ex;
            }
        }

        private static void LoadBodyText( ReceivedMessage message, TextReader reader, ContentType contentType )
        {
            LineType lastLineType; //not used, because the message is not multipart
            message.Body = LoadBodyString( reader, message.ReceivedHeaders, contentType, out lastLineType );
            message.IsBodyHtml = (contentType.MediaType == MediaTypeNames.Text.Html);
        }

        private static bool LoadHeaders( TextReader reader, NameValueCollection messageHeaders )
        {
            return LoadHeaders( reader, messageHeaders, null );
        }
        /// <summary>
        /// Extract headers from the text stream into the Headers collection of the ReceivedMessage object provided.
        /// </summary>
        /// <param name="reader">TextReader providing the data stream to be parsed</param>
        /// <param name="messageHeaders">Collection of message headers.</param>
        /// <param name="rawheaders">Raw text of headers as received from server.</param>
        /// <returns>Returns false if no headers were found. Otherwise returns true.</returns>
        private static bool LoadHeaders( TextReader reader, NameValueCollection messageHeaders, StringBuilder rawheaders )
        {
            Debug.WriteLine( "LoadHeaders()" );
            string line = null;
            string lastHeaderKey = null;
            //multiple values with the same header key will be delimited by semi-colon.
            //semi-colon is reserved as a special character for header encoding in RFC 2047, so this should be safe
            Dictionary<string, StringBuilder> headersBuilders = new Dictionary<string, StringBuilder>( StringComparer.InvariantCultureIgnoreCase );
            Dictionary<string, string[]> headers;

            Debug.WriteLine( "Raw:" );
            while( null != (line = reader.ReadLine()) )
            {
                Debug.WriteLine( line );
                if( rawheaders != null )
                    rawheaders.AppendLine( line );

                //headers end with an empty line
                if( line == string.Empty || line.Trim() == string.Empty  )
                    break;

                //some agents malform the key: value expression so that there is no space after the colon.
                //for example this BlackBerry Message-ID header
                //Message-ID:<343521787-1214491918-cardhu_decombobulator_blackberry.rim.net-1658171425-@bxe200.bisx.prod.on.blackberry>
                Regex regex = new Regex( @"^([^:]+):\s?(.*)$" ); 
                Match match = regex.Match( line );

                //if a line does not contain a colon it is either a continuation of a previous line or an error
                if( match.Success )
                {
                    //split header key from RFC 2047 encoded value

                    string headerkey = match.Groups[1].Value;

                    string value = match.Groups[2].Value;


                    if( !headersBuilders.ContainsKey( headerkey ) )
                        headersBuilders.Add( headerkey, new StringBuilder( value ) );
                    else
                        headersBuilders[headerkey].AppendFormat( "|~|{0}", value );
                    lastHeaderKey = headerkey;
                }
                else
                {
                    //continuation line should start with whitespace
                    if( !string.IsNullOrEmpty( lastHeaderKey ) && Regex.IsMatch( line, "^\\s" ) )
                    {
                        string h = line.TrimStart( '\t', ' ' );
                        headersBuilders[lastHeaderKey].AppendFormat( "|~|{0}", h );
                    }
                    else //error in message format, skip ahead and attempt to continue parsing
                    {
                        lastHeaderKey = null;
                        continue;
                    }
                }

            }

            if( headersBuilders.Count == 0 )
                return false;

            headers = new Dictionary<string, string[]>( headersBuilders.Count, StringComparer.InvariantCultureIgnoreCase );
            foreach( string key in headersBuilders.Keys )
            {
                List<string> list = new List<string>( headersBuilders[key].ToString().Split( new[] { "|~|" }, StringSplitOptions.RemoveEmptyEntries ) );

                for( int i = 0; i < list.Count; i++ )
                {
                    if( string.IsNullOrEmpty( list[i] ) )
                        list.RemoveAt( i );
                }
                headers.Add( key, list.ToArray() );
            }

            InPlaceDecodeExtendedHeaders( headers );

            //add decoded headers to the ReceivedMessage parameter passed in to this method.
            /**************************************************************************
             * NOTE:
             * The NameValueCollction in MailMessage.Headers is actually an internal Type
             * called HeaderCollection. HeaderCollection has different behavior for header keys
             * that are defined as singleton. MailHeaderMsft.IsSingleton replicates the internal
             * test used by HeaderCollection.
             */
            Debug.WriteLine( "Decoded:" );
            foreach( string key in headers.Keys )
            {
                Debug.WriteLine( "Key: " + key );
                //I believe IsSingleton is meant to indicate that the field should occur only once in the output
                //headers when the message is encoded for transport. The side-effect of the implementation is that it doesn't
                //allow adding multiple values to a single key in the NameValueCollection.
                if( MailHeaderInfo.IsSingleton( key ) )
                {
                    //HeaderCollection will use Set internally when Add is called
                    //need to join multiple values into a single string before adding or
                    //the last value to be added will be the final value and the others will be lost.
                    //therefore, components need to be combined into a single comma-separated string
                    //before being added.
                    string[] v = headers[key];
                    string value = string.Join( string.Empty, headers[key] );
                    //HeaderCollection.Add(string,string) throws an undocumented ArgumentException when being passed
                    //an empty string as a value. This is quite contrary to the behavior documented for NameValueCollection.Add(string,string)
                    //which explicitly permits null and empty strings to be added.
                    //the most common cause of this problem is a BCC: field with no value.
                    if( value != string.Empty )
                        messageHeaders.Add( key, value );
                }
                else
                {
                    for( int i = 0; i < headers[key].Length; i++ )
                    {
                        if( !string.IsNullOrEmpty( headers[key][i] ) )
                        {
                            Debug.WriteLine( key + ": " + headers[key][i] );
                            messageHeaders.Add( key, headers[key][i] );
                        }
                    }
                }

                Debug.WriteLine( key + ": " + messageHeaders[key] );
            }

            return true;
        }

        private static string GetContentTransferEncoding( NameValueCollection headers )
        {
            return headers["content-transfer-encoding"];
        }

        private static ContentType GetContentType( NameValueCollection headers )
        {
            return GetContentType( headers, null );
        }

        private static ContentType GetContentType( NameValueCollection headers, string boundary )
        {
            /****************************************************************
              http://tools.ietf.org/html/rfc1341#page-5
              http://tools.ietf.org/html/rfc1521#page-9
             
            /****************************************************************/

            const string CONTENT_TYPE = "content-type";
            string ct = headers[CONTENT_TYPE];

            ContentType contentType;
            Match match;

            if( string.IsNullOrEmpty( ct ) )
                contentType = new ContentType();
            else
            {
                //decode content-type
                const string P_CONTENT_TYPE = @"^([^; ]+)";
                match = Regex.Match( ct, P_CONTENT_TYPE, RegexOptions.IgnoreCase );
                if( match.Success )
                {
                    try
                    {
                        contentType = new ContentType( match.Groups[1].Value );
                    }
                    catch( Exception ex )
                    {
                        if( ex is ArgumentException || ex is FormatException )
                            contentType = new ContentType();
                        throw ex;
                    }
                }
                else
                {
                    contentType = new ContentType();
                }
            }

            if( !string.IsNullOrEmpty( ct ) )
            {
                ct = ct.Trim(); //trailing whitespace can mess up the matching

                //decode name
                const string P_NAME = "name=(?:\"([^\"]+)\"|([^;]+))(?:;|$)"; //captures value to group 1 or 2
                match = Regex.Match( ct, P_NAME, RegexOptions.IgnoreCase );
                if( match.Success )
                {
                    contentType.Name = string.IsNullOrEmpty( match.Groups[1].Value ) ? match.Groups[2].Value : match.Groups[1].Value;
                }

                //decode charset
                const string P_CHARSET = "charset=(?:\"([^\"]+)\"|([^;]+))(?:;|$)"; //captures value to group 1 or 2
                match = Regex.Match( ct, P_CHARSET, RegexOptions.IgnoreCase );
                if( match.Success )
                {
                    //some charset encodings defined in incoming are not supported by System.Text.
                    //We need to test it here to make sure that we won't crash later.

                    string charset = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                    //"cp1252" is an alias for "windows-1252" but is not a codepage identifier supported by Encoding.GetEncoding(string)
                    if( 0 == string.Compare( "cp1252", charset, true ) )
                        charset = "windows-1252";
                    //MimUtility.TryGetEncoding(string) will return a valid Encoding object. If we use the BodyName from an Encoding object
                    //we can be sure it we won't crash converting back to an Encoding object.
                    contentType.CharSet = MimeUtility.TryGetEncoding( charset ).BodyName;
                }


                contentType.Boundary = boundary;
                if( string.IsNullOrEmpty( contentType.Boundary ) )
                {

                    //decode boundary
                    const string P_BOUNDARY = "boundary=(?:\"([^\"]+)\"|([^;]+))(?:;|$)"; //captures value to group 1 or 2
                    match = Regex.Match( ct, P_BOUNDARY, RegexOptions.IgnoreCase );
                    if( match.Success )
                    {
                        contentType.Boundary = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                    }
                }

            }

            //if boundary exists, these are sub-headers and then these are the headers for a 
            //multipart component and we need to check for a filename in the content-disposition header.
            if( !string.IsNullOrEmpty( boundary) ) 
            {  
                if( string.IsNullOrEmpty( contentType.Name ) )
                {
                    const string P_FILENAME = "filename=(?:\"([^\"]+)\"|([^;]+))(?:;|$)"; //captures value to group 1 or 2

                    string disposition = headers["content-disposition"];
                    if( !string.IsNullOrEmpty( disposition ) )
                    {
                        match = Regex.Match( disposition, P_FILENAME );
                        if( match.Success )
                            contentType.Name = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                    }
                }
            }

            return contentType;
        }


        /// <summary>
        /// Decodes RFC 2047 message header extensions into unicode strings consistent with the System.Net.MailMessage object model.
        /// </summary>
        /// <param name="headers"></param>
        private static void InPlaceDecodeExtendedHeaders( Dictionary<string, string[]> headers )
        {
            /*********************************************************
               http://tools.ietf.org/search/rfc2047

               RFC 2047 "Message Header Extensions"
               
                2. Syntax of encoded-words

                   An 'encoded-word' is defined by the following ABNF grammar.  The
                   notation of RFC 822 is used, with the exception that white space
                   characters MUST NOT appear between components of an 'encoded-word'.

                   encoded-word = "=?" charset "?" encoding "?" encoded-text "?="
             
                   ...
             
               3. Character sets

                   The 'charset' portion of an 'encoded-word' specifies the character
                   set associated with the unencoded text.  A 'charset' can be any of
                   the character set names allowed in an MIME "charset" parameter of a
                   "text/plain" body part, or any character set name registered with
                   IANA for use with the MIME text/plain content-type.

                   ...

                   When there is a possibility of using more than one character set to
                   represent the text in an 'encoded-word', and in the absence of
                   private agreements between sender and recipients of a message, it is
                   recommended that members of the ISO-8859-* series be used in
                   preference to other character sets.
               
               4. Encodings

                   Initially, the legal values for "encoding" are "Q" and "B".  These
                   encodings are described below.  The "Q" encoding is recommended for
                   use when most of the characters to be encoded are in the ASCII
                   character set; otherwise, the "B" encoding should be used.
                   Nevertheless, a mail reader which claims to recognize 'encoded-word's
                   MUST be able to accept either encoding for any character set which it
                   supports.
             
                   ...
              
               4.1. The "B" encoding

                   The "B" encoding is identical to the "BASE64" encoding defined by RFC
                   2045.

                4.2. The "Q" encoding

                   The "Q" encoding is similar to the "Quoted-Printable" content-
                   transfer-encoding defined in RFC 2045.  It is designed to allow text
                   containing mostly ASCII characters to be decipherable on an ASCII
                   terminal without decoding.
            /*********************************************************/

            //convert the raw header into decoded strings.

            Debug.WriteLine( "InPlaceDecodeExtendedHeaders()" );

            const string p_split = @"(=\?[^\?]+\?[^\?]+\?[^\?]+\?=\s?)";
            //the charset field goes into capture group 1
            //the transfer encoding field goes into capture group 2
            //the raw text value goes into capture group 3.
            const string p_capture = @"^=\?([^\?]+)\?([^\?]+)\?([^\?]+)\?=\s?$"; 
            foreach( string[] hlist in headers.Values )
            {
                for( int i = 0; i < hlist.Length; i++ )
                {
                    if( Regex.IsMatch( hlist[i], p_split ) )
                    {
                        StringBuilder b = new StringBuilder();
                        foreach( string part in Regex.Split( hlist[i], p_split ) )
                        {
                            Match match = Regex.Match( part, p_capture );
                            if( match.Success ) //part is RFC 2047 encoded
                            {
                                string charset  = match.Groups[1].Value;
                                string transfer = match.Groups[2].Value;
                                string rawtext  = match.Groups[3].Value;
                                Encoding encoding = MimeUtility.TryGetEncoding( charset );
                                if( 0 == string.Compare( transfer, "b", true ) )
                                    b.Append( MimeUtility.DecodeBase64String( rawtext, encoding ) );
                                else
                                    MimeUtility.DecodeAndAppendQuotedPrintableSegment( b, rawtext, encoding );
                            }
                            else if( !string.IsNullOrEmpty( part ) )
                                b.Append( part ); //part is not RFC 2047 encoded
                        }
                        hlist[i] = b.ToString();
                    }
                }
            }
        }

        private static void DecodeAndAppendLine( StringBuilder builder, string buff, string transferEncoding, string charset )
        {
            if( null == builder )
                return;

            if( 0 == string.Compare( transferEncoding, "quoted-printable", true ) )
            {
                builder.Append( MimeUtility.DecodeQuotedPrintableLine( buff, MimeUtility.TryGetEncoding( charset ) ) );
            }
            else if( 0 == string.Compare( transferEncoding, "base64", true ) )
            {
                builder.Append( MimeUtility.DecodeBase64String( buff, MimeUtility.TryGetEncoding( charset ) ) );
            }
            else
            {
                builder.Append( buff + "\r\n" );
            }
        }

        private static LineType GetLineType( string line, ContentType contentType )
        {
            /* http://www.w3.org/Protocols/rfc1341/7_2_Multipart.html */
            if( string.IsNullOrEmpty( line ) )
                return LineType.Normal;

            if( "." == line )
                return LineType.EOF;

            string trimline = line.Trim();
            if( contentType != null && !string.IsNullOrEmpty( contentType.Boundary ) )
            {
                if( "--" + contentType.Boundary == trimline )
                    return LineType.InternalBoundary;

                if( "--" + contentType.Boundary + "--" == trimline )
                    return LineType.TerminalBoundary;
            }
  
            return LineType.Normal;
        }

        private enum LineType
        {
            Normal,
            InternalBoundary,
            TerminalBoundary,
            EOF
        }
    }
}
