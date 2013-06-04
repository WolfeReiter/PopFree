using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace PopFree.Pop3
{
    public abstract class MailboxWorkerBase : IMailboxWorker
    {
        public MailboxWorkerBase()
        {
            ErrorSleepTimeout = 30000;
        }

        public PopConnectInfo ConnectInfo { get; set; }
        public int ErrorSleepTimeout { get; set; }

        public abstract int DownloadMessages();

        protected virtual bool IsServerCompatible( PopClient client )
        {
            foreach( string capability in client.Capabilities() )
            {
                if( 0 == string.Compare( capability, "uidl", true ) )
                    return true;
            }
            return false;
        }

        protected virtual void OnMessageRequested( string info )
        {
            if( MessageRequested != null )
                MessageRequested( this, new MessageInfoEventArgs( info ) );
        }
        protected virtual void OnMessageReceived( string info )
        {
            if( MessageRecieved != null )
                MessageRecieved( this, new MessageInfoEventArgs( info ) );
        }

        protected virtual void OnCountReceived( string info )
        {
            if( MessageCountReceived != null )
                MessageCountReceived( this, new MessageInfoEventArgs( info ) );
        }

        protected virtual void OnServerError( string info, Exception ex )
        {
            if( ServerError != null )
                ServerError( this, new MessageInfoEventArgs( info, ex ) );
        }

        protected virtual void OnPreviousDownloaded( string info )
        {
            if( MessagePreviouslyDownloaded != null )
                MessagePreviouslyDownloaded( this, new MessageInfoEventArgs( info ) );
        }

        public static string StripIllegalFilenameChars( string path )
        {
            if( string.IsNullOrEmpty( path ) )
                return null;

            foreach( char c in Path.GetInvalidFileNameChars() )
            {
                path = path.Replace( c.ToString(), string.Empty );
            }
            foreach( char c in Path.GetInvalidPathChars() )
            {
                path = path.Replace( c.ToString(), string.Empty );
            }
            return path;
        }

        public event MessageInfoHandler MessageRequested;
        public event MessageInfoHandler MessageRecieved;
        public event MessageInfoHandler MessageCountReceived;
        public event MessageInfoHandler ServerError;
        public event MessageInfoHandler MessagePreviouslyDownloaded;
    }
}
