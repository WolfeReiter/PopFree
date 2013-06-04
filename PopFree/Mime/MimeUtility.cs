using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;

namespace PopFree.Mime
{
    public static class MimeUtility
    {
        /// <summary>
        /// Decodes a string encoded with Quoted Printable (QP) transfer encoding into a standard
        /// string object. Note that the charset is important because different Encodings will use
        /// variable numbers of bytes to encode a single character. Using the wrong encoding can
        /// yield gibberish non-ascii characters.
        /// </summary>
        /// <param name="s">string to decode</param>
        /// <param name="encoding">Encoding (charset) used to encode the QP string. If encoding is null then
        /// the ISO-8859-1 charset is used.
        /// </param>
        /// <returns></returns>
        public static string DecodeQuotedPrintable( string s, Encoding encoding )
        {
            StringBuilder b = new StringBuilder();
            using( StringReader reader = new StringReader( s ) )
            {
                string line;
                while( null != (line=reader.ReadLine() ) )
                {
                    b.Append( DecodeQuotedPrintableLine( line, encoding ) );
                }
            }
            string value = b.ToString();
            return value;
        }

        /// <summary>Decodes quoted-printable line-by-line. Appends \r\n if the line is not marked with 
        /// a soft break terminator.</summary>
        /// <remarks>
        /// Line-based decoding of quoted-printable string is more efficient for streaming than the 
        /// DecodeQuotedPrintable(string) but the semantics are a little weird. There is an implicit
        /// assumption of streaming and the output being added to a StringBuilder or other buffer.
        /// Safer for this to be private.
        /// </remarks>
        /// <param name="s"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        internal static string DecodeQuotedPrintableLine( string s, Encoding encoding )
        {
            if( string.IsNullOrEmpty( s ) )
                return s;

            encoding = (null == encoding) ? DefaultEncoding : encoding;

            StringBuilder b = new StringBuilder();


            //line breaks terminated with = are "soft" and not included in the decoded text.
            //otherwise, the line break is intended to be included in the encoded text.
            bool softline;
            if( (softline = s.EndsWith( "=" )) )
                s.TrimEnd( '=' );

            DecodeAndAppendQuotedPrintableSegment( b, s, encoding );

            if( !softline )
                b.Append( "\r\n" );
            string value = b.ToString();

            return value;
        }

        internal static void DecodeAndAppendQuotedPrintableSegment( StringBuilder b, string s, Encoding encoding )
        {

            List<byte> chrs = new List<byte>();
            for( int i = 0; i < s.Length; i++ )
            {
                if( s[i] == '=' )
                {
                    //format ex "=3D" where the 2 chars following "=" are a hexadecimal byte
                    if( i + 3 >= s.Length ) //malformed, string too short
                        break;

                    byte chr;
                    if( byte.TryParse( s.Substring( i + 1, 2 ), NumberStyles.HexNumber, null, out chr ) )
                    {
                        chrs.Add( chr );
                    }
                    i += 2;  //2 + iterator of 1 == 3 chars
                }
                else
                {
                    if( chrs.Count > 0 )
                    {
                        b.Append( encoding.GetString( chrs.ToArray() ) );
                        chrs = new List<byte>();
                    }
                    if( s[i] == '_' )
                        b.Append( ' ' );
                    else
                        b.Append( s[i] );
                }
            }
        }

        /// <summary>
        /// Decodes a base64-encoded transfer encoded string into a standard
        /// string object.
        /// </summary>
        /// <param name="s">string to decode</param>
        /// <param name="encoding">Encoding (charset) used to encode the base64 string. If encoding is null then
        /// the ISO-8859-1 charset is used. 
        /// </param>
        /// <returns></returns>
        public static string DecodeBase64String( string s, Encoding encoding )
        {
            if( string.IsNullOrEmpty( s ) )
                return s;

            encoding = (null == encoding) ? DefaultEncoding : encoding;
            return encoding.GetString( Convert.FromBase64String( s ) );
        }

        /// <summary>
        /// Default to ISO-8859-1 Encoding per RFC 2045. 
        /// </summary>
        public static Encoding DefaultEncoding
        {
            get
            {
                return Encoding.GetEncoding( "iso-8859-1" ); 
            }
        }

        /// <summary>
        /// Try to return an encoding from the string. If there is no matching encoding, return the value of DefaultEncoding.
        /// </summary>
        /// <param name="encoding">code page name</param>
        /// <returns>Specified encoding if possible, otherwise DefaultEncoding</returns>
        public static Encoding TryGetEncoding( string encoding )
        {
            if( string.IsNullOrEmpty( encoding ) )
                return DefaultEncoding;

            try{ return Encoding.GetEncoding( encoding ); }
            catch( ArgumentException ){ return DefaultEncoding; }
        }

        public static string GetFileExtensionFromMediaType( string mediaTypeName )
        {
            string value = "bin";
            XmlNode node = MimeMappingXml.SelectSingleNode( string.Format( "descendant::mime-mapping[mime-type=\"{0}\"]", mediaTypeName ) );
            if( null != node )
            {
                if( node.HasChildNodes )
                    value = !string.IsNullOrEmpty( node.FirstChild.InnerText ) ? node.FirstChild.InnerText : value;
            }
            return value;
        }

        static XmlDocument s_map = null;
        static XmlDocument MimeMappingXml
        {
            get
            {
                if( s_map == null )
                {
                    s_map = new XmlDocument();
                    using( Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream( "PopFree.Mime.mime-mapping.xml" ) )
                        s_map.Load( stream );
                }
                return s_map;
            }
        }

     }
}
