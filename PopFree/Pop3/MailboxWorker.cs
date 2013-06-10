using System;
using System.Collections.Generic;
using System.Text;
using PopFree.Pop3;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;

namespace PopFree.Pop3
{
    public sealed class MailboxWorker : MailboxWorkerBase, IMailboxWorker
    {
        /// <summary>
        /// MailboxWorker connects to a POP3 server and retrieves mail. POP3 usually only allows one client at a time to access
        /// the server. There must only be one MailboxWorker instance for each mailbox or the workers will corrupt each other.
        /// </summary>
        public MailboxWorker() { }

        /// <summary>
        /// Number of consecutive server errors without successful communication.
        /// </summary>
        private int _errorCount = 0;
        private const int MaxErrors = 10;

        public override int DownloadMessages()
        {
           int total = 0;
            int count;
            while( 0 < (count = FetchMessages()) )
                total += count;
            return total;
        }

        private int FetchMessages()
        {
            int count = 0;

            using(
            PopClient client = new PopClient()
            {
                Host = ConnectInfo.Host,
                Port = ConnectInfo.TcpPort,
                Username = ConnectInfo.Username,
                Password = ConnectInfo.Password,
                SslNegotiationMode = ConnectInfo.SslNegotiationMode,
                SslProtocol = ConnectInfo.SslProtocol,
                ReceiveTimeOut = ConnectInfo.Timeout,
                SendTimeOut = ConnectInfo.Timeout
            } )
            {
                string name = null;
                try
                {
                    client.ConnectAndAuthenticate();
                    if( !IsServerCompatible( client ) )
                        throw new NotSupportedException( "The server is not compatible. Please use a server that supports UIDL." );

                    OnCountReceived( client.GetMessageCount().ToString() );
                    _errorCount = 0; //if we get here, then the server is talking to us.

                    foreach( PopUid uid in client.GetMessageUids() )
                    {
                        /*
                        if( count >= 20 )  //DEBUG: short circuit here to watch threads exit
                            return 0;
                        */
                        if( MessageFileExists( uid.UniqueIdentifier ) )
                        {
                            OnPreviousDownloaded( uid.UniqueIdentifier );
                            client.DeleteMessage( uid.MessageNumber );
                        }
                        else
                        {
                            OnMessageRequested( uid.MessageNumber + " -> " + uid.UniqueIdentifier );
                            string temp = ConnectInfo.PathInfo.TempDownloadFile;
                           
                            using( Stream stream = File.Create( temp ) )
                                client.WriteMessageToStream( stream, uid.MessageNumber );

                            name = StripIllegalFilenameChars( uid.UniqueIdentifier );
                            name = ConnectInfo.PathInfo.QueueDirectory + Path.DirectorySeparatorChar + name + ".eml";

                            File.Move( temp, name );
                            client.DeleteMessage( uid.MessageNumber );
                            count++;
                            OnMessageReceived( name );
                        }
                    }
                }
                catch( IOException ex )
                {
                    _errorCount++;
                    client.Disconnect();
                    SocketException se;
                    if( null != (se = ex.InnerException as SocketException) )
                    {
                        if( ((int)SocketError.TimedOut == se.ErrorCode) )
                        {

                            if( ConnectInfo.Timeout < PopConnectInfo.MaxTimeout )
                            {
                                if( ConnectInfo.Timeout < PopConnectInfo.MinTimeout )
                                    ConnectInfo.Timeout = PopConnectInfo.MinTimeout;
                                else
                                    ConnectInfo.Timeout *= 2;
                            }
                        }
                    }
                    Debug.WriteLine( ex );
                    OnServerError( ex.Message, ex );
                    if( null == name )
                        File.Delete( name );

                    if( _errorCount < MaxErrors )
                    {
                        Thread.Sleep( ErrorSleepTimeout );
                        return FetchMessages();
                    }
                    else
                        Die( ex );
                }
                catch( PopServerResponseErrException ex )
                {
                    _errorCount++;
                    client.Disconnect();
                    Debug.WriteLine( ex );
                    OnServerError( ex.Message, ex );
                    if( null == name )
                        File.Delete( name );

                    if( _errorCount < MaxErrors )
                    {
                        Thread.Sleep( ErrorSleepTimeout );
                        return FetchMessages();
                    }
                    else
                        Die( ex );
                }
            }
            return count;
        }

        private bool MessageFileExists( string messageid )
        {
            string name = string.Format( "{0}.eml", StripIllegalFilenameChars( messageid ) );

            bool exists = 
                File.Exists( ConnectInfo.PathInfo.BadmailDirectory + Path.DirectorySeparatorChar + name ) ||
                File.Exists( ConnectInfo.PathInfo.QueueDirectory + Path.DirectorySeparatorChar + name ) ||
                File.Exists( ConnectInfo.PathInfo.ProcessedDirectory + Path.DirectorySeparatorChar + name );

            return exists;
        }

        private void Die( Exception ex )
        {
            throw new ApplicationException( "Maximum retries exceeded.", ex );
        }

    }
}
