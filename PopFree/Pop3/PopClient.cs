using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PopFree.Mime;
using System.Collections.Specialized;

namespace PopFree.Pop3
{
	/// <summary>
	/// PopClient is a disposable object that allows retrieving and manipulating messages on a POP3 server. It aims to be as
    /// efficient as possibly by working with streams whenever possible.
	/// </summary>
	public sealed class PopClient : IDisposable
	{

        /* reference reading
            POP  -> RFC 918  (1984)
            POP2 -> RFC 937  (1984)
            POP3 -> RFC 1081 (1988)
            POP3 -> RFC 1225 (1993) [revisions]
            POP3 -> RFC 1460 (1993) [adds APOP authentication]
            POP3 -> RFC 1734 (1994) [AUTH extensible authentication]
            POP3 -> RFC 1939 (1996) [revisions]
            POP3 -> RFC 2449 (1998) [extensions]
            POP3 -> RFC 2595 (1999) [POP3 with TLS]
        /*/

        public PopClient()
        {
            Disposing = false;

            ClientSocket = new TcpClient();
            ReceiveTimeOut = 60000; // 60,000 milliseconds = 1 minute
            SendTimeOut = 60000; //1 minute

            Port = 110;
            AuthenticationMethod = AuthenticationMethod.Auto;
            SslProtocol = SslProtocols.None;
            SslNegotiationMode = SslNegotiationMode.Connect;
        }

        public PopClient( string host, int port, string username, string password )
            : this( host, port, username, password, AuthenticationMethod.Auto, SslProtocols.None )
        {

        }

        public PopClient( string host, int port, string username, string password, AuthenticationMethod authenticationMethod )
            : this( host, port, username, password, authenticationMethod, SslProtocols.None )
        {

        }

        public PopClient( string host, int port, string username, string password, AuthenticationMethod authenticationMethod, SslProtocols sslProtocol )
            : this()
        {
            Host = host;
            Port = port;
            Username = username;
            Password = password;
            AuthenticationMethod = authenticationMethod;
            SslProtocol = sslProtocol;
        }

        private TcpClient ClientSocket { get; set; }
        private StreamReader StreamReader { get; set; }
        private StreamWriter StreamWriter { get; set; }

        /// <summary>
        /// TCP Port to connect to.
        /// </summary>
        public int Port { get; set; }
        /// <summary>
        /// Host to connect to.
        /// </summary>
        public string Host { get; set; }
        /// <summary>
        /// Username to authenticate with.
        /// </summary>
        public string Username { private get; set; }
        /// <summary>
        /// Password to authenticate with.
        /// </summary>
        public string Password { private get; set; }
        /// <summary>
        /// SslProtocol to use. Use SslProtocols.None for plaintext. SslProtocols.Default allows auto-negotiation of the encryption.
        /// </summary>
        public SslProtocols SslProtocol { get; set; }
        /// <summary>
        /// If not using SslProtocols.None, whether to use SSL on connect (POP3S) or start tls (STLS).
        /// </summary>
        public SslNegotiationMode SslNegotiationMode { get; set; }
        /// <summary>
        /// Type of authentication.
        /// </summary>
        public AuthenticationMethod AuthenticationMethod { get; set; }

        /// <summary>
        /// Is the client connected.
        /// </summary>
        /// <exception cref="System.IO.IOException">Throws if there is a problem reading from the Stream.</exception>
        /// <exception cref="Systemn.ObjectDisposedException">Throws if the Stream or StreamReader for the connection has been disposed.</exception>
        private bool Connected
        {
            get
            {
                if( null == StreamReader )
                    return false;
                StreamReader.Peek(); //throw an IOException if the StreamReader can't Peek()
                return ( StreamReader.BaseStream.CanRead );
            }
        }

        private string ApopTimestamp { get; set; }

		/// <summary>
		/// Receive timeout for the connection to the SMTP server in milliseconds.
		/// The default value is 60000 milliseconds.
		/// </summary>
        public int ReceiveTimeOut
        {
            get { return ClientSocket.ReceiveTimeout; }
            set { ClientSocket.ReceiveTimeout = value; }
        }

		/// <summary>
		/// Send timeout for the connection to the SMTP server in milliseconds.
		/// The default value is 60000 milliseconds.
		/// </summary>
        public int SendTimeOut 
        {
            get { return ClientSocket.SendTimeout; }
            set { ClientSocket.SendTimeout = value; }
        }

		/// <summary>
		/// Receive buffer size
		/// </summary>
        public int ReceiveBufferSize 
        {
            get { return ClientSocket.ReceiveBufferSize; }
            set { ClientSocket.ReceiveBufferSize = value; } 
        }

		/// <summary>
		/// Send buffer size
		/// </summary>
        public int SendBufferSize 
        {
            get { return ClientSocket.SendBufferSize; }
            set { ClientSocket.SendBufferSize = value; } 
        }

		/// <summary>
		/// Sends a command to the POP server.
		/// </summary>
		/// <param name="cmd">command to send to server</param>
		/// <param name="blnSilent">Do not give error</param>
		/// <returns>true if server responded "+OK"</returns>
        private PopCommandResponse SendCommand( string cmd )
        {
            PopCommandResponse response = PopCommandResponse.NullResponse;
            try
            {
                if( StreamWriter.BaseStream.CanWrite )
                {
                    StreamWriter.WriteLine( cmd );
                    StreamWriter.Flush();
                    response = PopCommandResponse.FromRawResponseString( StreamReader.ReadLine() );
                }
            }
            catch( Exception e )
            {
                if( e is IOException )
                    throw e;
                else
                {
                    string err = cmd + ":" + e.Message;
                    Debug.WriteLine( e );
                    throw new PopServerResponseErrException( err, e );
                }
            }

            return response;
        }
        /// <summary>
        /// Connect to the server and log in. Requires that the Port, Host, Username and Password properties be set.
        /// </summary>
        public void ConnectAndAuthenticate()
        {
            Connect();
            Authenticate();
        }

        /// <summary>
        /// Connect to the server. Requires that the Port and Host properties be set.
        /// </summary>
        public void Connect()
        {

            if( Connected )
                throw new InvalidOperationException( "PopClient is already connected." );
            if( Host == null || Host == default( string ) )
                throw new InvalidOperationException( "The Host property must be set." );

            string host = Host;
            int port = Port;
            SslProtocols sslProtocol = SslProtocol;

            ClientSocket.Connect( host, port );


            Stream stream = ClientSocket.GetStream();

            if( SslProtocols.None != sslProtocol )
            {
                if( SslNegotiationMode.Connect == SslNegotiationMode  )
                {
                    //Implements POP3S capability for SSL/TLS connections on an alternate port.
                    //For POPS/Connect, we negotiate an SslStream immediately.
                    stream = NegotiateSslStream( host, sslProtocol, stream );
                }
                else
                {
                    //UNDONE: test STARTTLS. (Need an account on a compatible server.)

                    //For STLS, we have to start out with plain text and then issue the STLS command and if the server says +OK 
                    //we can negotiate TLS. Have to do this without using a StreamReader or StreamWriter because we need to change
                    //the stream from a NetworkStream to an SslStream. The stream inside of a StreamReader and StreamWriter can't
                    //be changed and closing the StreamReader or StreamWriter will close the stream upon which the SslStream is based. 

                    string response;
                    try
                    {
                        //don't close or dispose these StreamReader and StreamWriter objects of the stream will be closed.
                        StreamWriter writer = new StreamWriter( stream );
                        StreamReader reader = new StreamReader( stream );
                        writer.WriteLine( "STLS" );
                        response = reader.ReadLine();
                    }
                    catch( Exception ex )
                    {
                        if( null != stream )
                            try{ stream.Dispose(); } catch( ObjectDisposedException ){}
                        this.Disconnect();

                        throw new StartTlsNegotiationException( "STARTTLS negotiation failed. Consider using SslNegotiationMode.Connect.", ex );
                    }

                    if( PopCommandResponse.IsResponseSuccess( response ) )
                    {
                        stream = NegotiateSslStream( host, sslProtocol, stream );
                    }
                    else
                    {
                        if( null != stream )
                            try{ stream.Dispose(); } catch( ObjectDisposedException ){}
                        this.Disconnect();
                        throw new StartTlsNegotiationException( response );
                    }
                }
            }

            StreamReader = new StreamReader( stream, Encoding.Default, true );
            StreamWriter = new StreamWriter( stream );
            StreamWriter.AutoFlush = true;

            PopCommandResponse popresponse = PopCommandResponse.FromRawResponseString( StreamReader.ReadLine() );
            if( popresponse.Success )
            {
                Match match = Regex.Match( popresponse.RawValue, @"^\+OK\s.+(<.+>$)" );
                if( match.Success )
                {
                    ApopTimestamp = match.Groups[1].Value;
                }
            }
            else
            {
                this.Disconnect();
                popresponse.ToException();
            }
        }

        private static Stream NegotiateSslStream( string host, SslProtocols sslProtocol, Stream stream )
        {
            SslStream sslstream = new SslStream( stream );
            sslstream.AuthenticateAsClient( host, null, sslProtocol, false );
            stream = sslstream;
            return stream;
        }

 		/// <summary>
		/// Disconnect from Pop3 server. Same as calling Dispose().
		/// </summary>
		public void Disconnect()
		{
            Dispose();
		}

        /// <summary>
        /// Authenticate to the server. The Username, Password  properties must first be set.
        /// </summary>
        /// <exception cref="InvalidOperationException">Throws if Username or Password are not set.</exception>
        /// <exception cref="PopAuthenticationException">Throws if authentication fails.</exception>
        public void Authenticate()
        {
            if( Username == null || Username == default( string ) )
                throw new InvalidOperationException( "The Username property must first be set to a non-null value." );
            if( Password == null || Password == default( string ) )
                throw new InvalidOperationException( "The Password property must first be set to a non-null value." );

            string username = Username;
            string password = Password;
            AuthenticationMethod authenticationMethod = AuthenticationMethod;

            if( authenticationMethod == AuthenticationMethod.UserPass )
            {
                SendUserPass( username, password );
            }
            else if( authenticationMethod == AuthenticationMethod.Apop )
            {
                SendApop( username, password );
            }
            else if( authenticationMethod == AuthenticationMethod.Auto )
            {

                //APOP is supported if the ApopTimestamp property is set during login.
                if( !string.IsNullOrEmpty( ApopTimestamp ) ) 
                    SendApop( username, password );
                else
                    SendUserPass( username, password );
            }
        }

		/// <summary>
		/// Authenticate the user with USER/PASS
		/// </summary>
		/// <param name="username">user name</param>
		/// <param name="password">password</param>
		private void SendUserPass(string username,string password)
		{				
            PopCommandResponse response = SendCommand("USER " + username);
			if( !response.Success )
			{
                throw new PopAuthenticationException( "USER/PASS Authentication: " + response.RawValue, response.ToException() );
			}
			
            response = SendCommand("PASS " + password);
			if( !response.Success )	
			{
				if( Regex.IsMatch( response.RawValue, "lock", RegexOptions.IgnoreCase ) )
				{
                    throw new MailboxLockedException( "Mailbox locked by another client. Try again later.", response.ToException() );	
				}
				else
				{
                    throw new PopAuthenticationException( "USER/PASS Authentication: " + response.RawValue, response.ToException() );
				}
			}
		}

		/// <summary>
		/// Authenticate the user with APOP
		/// </summary>
		/// <param name="username">user name</param>
		/// <param name="password">password</param>
		private void SendApop(string username, string password)
		{
            PopCommandResponse response = SendCommand("APOP " + username + " " + ComputeHashAsHexString( password ) );
			if( !response.Success )
			{
                if( Regex.IsMatch( response.RawValue, "lock", RegexOptions.IgnoreCase ) )
                {
                    throw new MailboxLockedException( "Mailbox locked by another client. Try again later.", response.ToException() );
                }
                else
                {
                    throw new PopAuthenticationException( "APOP Authentication: " + response.RawValue, response.ToException() );
                }
			}
		}

        /// <summary>
        /// Get max message number and mailbox size.
        /// </summary>
        /// <returns>PopStat object with the max message number and mailbox size.</returns>
        public PopStat SendStat()
        {
            PopCommandResponse response = SendCommand( "STAT" );
            PopStat stat = new PopStat();
            IList<int> v = response.IntValues();
            if( v.Count > 1 )
            {
                stat.MessageNumber = v[0];
                stat.Size = v[1];
            }
            return stat;
        }

		/// <summary>
		/// Get message count
		/// </summary>
		/// <returns>message count</returns>
		public int GetMessageCount()
		{
            return SendStat().MessageNumber;
		}

		/// <summary>
		/// Deletes message with given index when Close() is called
		/// </summary>
		/// <param name="messageNumber"> </param>
		public void DeleteMessage(int messageNumber) 
		{
            SendCommand( "DELE " + messageNumber.ToString() );
		}

		/// <summary>
		/// Send QUIT to the Pop3 server
		/// </summary>
		private void SendQuit()
		{
            SendCommand( "QUIT" );
		}

		/// <summary>
		/// Keep the connection alive but do nothing
		/// </summary>
        public void SendNoop()
		{
            if( !Connected )
                throw new InvalidOperationException( "You must be connected." );

            SendCommand( "NOOP" );
		}

		/// <summary>
		/// Reset the server state.
		/// </summary>
        public void SendReset()
		{
            if( !Connected )
                throw new InvalidOperationException( "You must be connected." );

            SendCommand( "RSET" );
		}

		/// <summary>
        /// Get unique identifier for the given message number.
		/// </summary>
		/// <param name="intMessageNumber">message number</param>
        public PopUid GetMessageUid( int intMessageNumber )
		{
            if( !Connected )
                throw new InvalidOperationException( "You must be connected." );

            PopCommandResponse response = SendCommand( "UIDL " + intMessageNumber.ToString() );
            if( response.Success )
            {
                if( response.Parameters.Count > 1 )
                {
                    PopUid uid = new PopUid();
                    int.TryParse( response.Parameters[0], out uid.MessageNumber );
                    uid.UniqueIdentifier = response.Parameters[1];
                }
            }
            throw new InvalidDataException( "Server responded with invalid data: " + response.RawValue );
		}

		/// <summary>
		/// Get a list of message numbers with a unique identifier for each message number.
		/// </summary>
        public IEnumerable<PopUid> GetMessageUids()
		{
            if( !Connected )
                throw new InvalidOperationException( "You must be connected." );

            List<PopUid> uids = new List<PopUid>();
            PopCommandResponse response = SendCommand( "UIDL" );
			if( response.Success )
			{
                string line;
				while( null != (line=StreamReader.ReadLine()) && "." != line )
				{
                    string[] s = line.Split(' ');
                    if( s.Length > 1 )
                    {
                        PopUid uid = new PopUid();
                        int.TryParse( s[0], out uid.MessageNumber );
                        uid.UniqueIdentifier = s[1];
                        uids.Add( uid );
                    }
				}
			}
            return new ReadOnlyCollection<PopUid>( uids );
		}

        /// <summary>
        /// Get the capabilities of the server.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> Capabilities()
        {
            if( !Connected )
                throw new InvalidOperationException( "You must be connected." );

            List<string> caps = new List<string>();
            PopCommandResponse response = SendCommand( "CAPA" );

            if( response.Success )
            {
                string line;
                while( null != (line = StreamReader.ReadLine()) && "." != line )
                    caps.Add( line );
            }

            return new ReadOnlyCollection<string>( caps );
        }

		/// <summary>
		/// Get the sizes of all the messages.
		/// </summary>
		/// <returns>Size of each message</returns>
		public IEnumerable<PopStat> List()
		{
            if( !Connected )
                throw new InvalidOperationException( "You must be connected." );

			List<PopStat> sizes=null;
            PopCommandResponse response = SendCommand( "LIST" );
			if( response.Success )
			{
                sizes = new List<PopStat>();
                string line;
				while ( null != (line = StreamReader.ReadLine() ) && "." != line )
				{
                    string[] s = line.Split( ' ' );
                    if( s.Length > 1 )
                    {
                        PopStat stat = new PopStat();
                        int.TryParse( s[0], out stat.MessageNumber );
                        int.TryParse( s[1], out stat.Size );
                        sizes.Add( stat );
                    }
				}
			}
            return new ReadOnlyCollection<PopStat>(sizes);
		}

		/// <summary>
		/// Get the size of a message
		/// </summary>
		/// <param name="intMessageNumber">message number</param>
		/// <returns>Size of message</returns>
		public PopStat List(int intMessageNumber)
		{
            if( !Connected )
                throw new InvalidOperationException( "You must be connected." );

            PopCommandResponse response = SendCommand( "LIST " + intMessageNumber.ToString() );
            PopStat stat = new PopStat();
            IList<int> v = response.IntValues();
            stat.MessageNumber = v[0];
            stat.Size = v[1];
            return stat;
		}

        /// <summary>
        /// Get a message stream from the message number.
        /// Pass in an open stream for the method to write the email text into.
        /// </summary>
        /// <param name="messageNumber">message number from 1 to the value of GetMessageCount() or a UIDL message number</param>
        public void GetMessageHeaderStream( Stream stream, int messageNumber )
        {
            if( !Connected )
                throw new InvalidOperationException( "You must be connected." );
            
            WriteMessageToStream( stream, messageNumber, true );
        }

        /// <summary>
        /// Get message headers.
        /// </summary>
        /// <param name="parser"></param>
        /// <param name="messageNumber"></param>
        /// <returns></returns>
        public NameValueCollection GetMessageHeaders( IMimeParser parser, int messageNumber )
        {
            if( parser == null )
                throw new ArgumentNullException( "parser" );
            TextReader reader = GetMessageReader( messageNumber, true );
            NameValueCollection headers = parser.ParseHeaders( reader );
            return headers;
        }

        /// <summary>
        /// Get a ReceivedMessage object using the provided parser and message number.
        /// </summary>
        /// <param name="parser">IMimeParser object</param>
        /// <param name="messageNumber">message number</param>
        /// <returns></returns>
        public ReceivedMessage GetMessage( IMimeParser parser, int messageNumber )
        {
            if( parser == null )
                throw new ArgumentNullException( "parser" );

            TextReader reader = GetMessageReader( messageNumber, false );
            ReceivedMessage message = parser.Parse( reader );
            return message;
        }

        /// <summary>
        /// Write a message retrieved from the server into the stream provided.
        /// </summary>
        /// <param name="stream">Stream to write the message into.</param>
        /// <param name="messageNumber">message number from 1 to the value of GetMessageCount()</param>
        public void WriteMessageToStream( Stream stream, int messageNumber )
        {
            if( !Connected )
                throw new InvalidOperationException( "You must be connected." );
            
            WriteMessageToStream( stream, messageNumber, false );
        }

		/// <summary>
        /// Write a message retrieved from the server into the stream provided.
        /// </summary>
        /// <param name="stream">Stream to write the message into.</param>
        /// <param name="number">message number on server</param>
        /// <param name="fetchHeaderOnly">Whether to fetch header instead of entire message.</param>
        public void WriteMessageToStream( Stream stream, int messageNumber, bool fetchHeaderOnly )
		{
            if( !Connected )
                throw new InvalidOperationException( "You must be connected." );

            TextWriter writer = new StreamWriter( stream );
            TextReader reader = GetMessageReader( messageNumber, fetchHeaderOnly );

            string line;
            while( null != (line = reader.ReadLine()) && "." != line )
                writer.WriteLine( line );

            writer.Flush();
		}

		/// <summary>
		/// fetches a message or a message header
		/// </summary>
		/// <param name="cmd">Command to send to Pop server</param>
		/// <param name="blnOnlyHeader">Only return message header?</param>
		/// <returns>Stream of mime-encoded text data. Returns null of the command failed.</returns>
        private TextReader GetMessageReader( int messageNumber, bool fetchHeaderOnly )
		{
            string cmd;
            if( fetchHeaderOnly )
                cmd = string.Format( "TOP {0} 0", messageNumber.ToString() );
            else
                cmd = string.Format( "RETR {0}", messageNumber.ToString() );

            PopCommandResponse response = SendCommand( cmd );

            if( !response.Success )
                response.Throw();

            return StreamReader;
		}

        static string ComputeHashAsHexString(string input )
        {
            MD5 md5 = MD5.Create();

            //the GetBytes method returns byte array equivalent of a string
			byte[] buff = md5.ComputeHash( Encoding.Default.GetBytes( input ) );
            StringBuilder sb = new StringBuilder();
            foreach( byte b in buff )
            {
                string bytestr = Convert.ToInt32( b ).ToString( "X" ).ToLower();
                if( bytestr.Length == 1 )
                    bytestr = '0' + bytestr;
                sb.Append( bytestr );
            }
            return sb.ToString();
        }

        #region IDisposable Members
        private bool Disposing { get; set; }

        public void Dispose()
        {
            if( !Disposing )
            {
                Disposing = true;

                if( TryGetConnected() )
                {
                    try
                    {
                        ClientSocket.ReceiveTimeout = 500;
                        ClientSocket.SendTimeout = 500;
                        try
                        {
                            SendQuit();
                        }
                        catch( IOException ) { }
                        catch( PopServerResponseErrException ) { }
                        ClientSocket.Close();
                        StreamReader.Close();
                        StreamWriter.Close();
                    }
                    catch( ObjectDisposedException ) { }
                    finally
                    {
                        StreamReader = null;
                        StreamWriter = null;
                        ClientSocket = null;
                    }
                }
            }
        }

        private bool TryGetConnected()
        {
            bool connected = false;
            try { connected = Connected; }
            catch( IOException ) { }
            catch( ObjectDisposedException ) { }
            return connected;
        }

        ~PopClient()
        {
            Dispose();
        }
        #endregion
    }
}

