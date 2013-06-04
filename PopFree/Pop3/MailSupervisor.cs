using System;
using System.Collections.Generic;
using System.Text;
using PopFree.Pop3;
using System.IO;

namespace PopFree.Pop3
{
    public class MailSupervisor<Tpop, Tmime>
        where Tpop : IMailboxWorker, new()
        where Tmime : IMimeFileWorker, new()
    {
        public MailSupervisor( PopConnectInfo c )
        {
            ConnectInfo = c;
            WorkPool = new WorkPool();
            Total = 0;
        }

        private object synclock = new object();
        protected PopConnectInfo ConnectInfo { get; private set; }
        protected WorkPool WorkPool { get; private set; }
        public int Total { get; protected set; }
        public TextWriter LogWriter { get; set; }

        public virtual void ProcessMail()
        {
            QueueMessages( ConnectInfo.PathInfo.QueueDirectory );
            Log( string.Format( "\r\nConnecting to {0}:{1} as {2}.", ConnectInfo.Host, ConnectInfo.TcpPort, ConnectInfo.Username ) );

            IMailboxWorker mailboxWorker = Activator.CreateInstance<Tpop>();
            mailboxWorker.ConnectInfo = ConnectInfo;
            mailboxWorker.MessageCountReceived += new MessageInfoHandler( MailboxWorkerMessageCountReceived );
            mailboxWorker.MessageRecieved += new MessageInfoHandler( MailboxWorkerMessageRecieved );
            mailboxWorker.ServerError += new MessageInfoHandler( MailboxWorkerServerError );
            mailboxWorker.MessagePreviouslyDownloaded += new MessageInfoHandler( MailboxWorkerPreviouslyDownloaded );
            mailboxWorker.MessageRequested += new MessageInfoHandler( MailboxWorkerMessageRequested );
            mailboxWorker.DownloadMessages();
            WorkPool.WaitFor();
        }

        private void QueueMessages( string directory )
        {
            string[] files = Directory.GetFiles( directory );
            if( files.Length > 0 )
            {
                Log( "Found one or more existing messages queued on disk.\r\nAdding them to work set.", ConsoleColor.DarkYellow, ConsoleColor.Black );

                foreach( string file in files )
                {
                    IMimeFileWorker worker = Activator.CreateInstance<Tmime>();
                    worker.BeginProcessing += new MessageInfoHandler( MimeFileWorkerBeginProcessing );
                    worker.DoneProcessing += new MessageInfoHandler( MimeFileWorkerDoneProcessing );
                    worker.ProcessingError += new MessageInfoHandler( MimeFileWorkerProcessingError );
                    worker.ProcessingWarning += new MessageInfoHandler( MimeFileWorkerProcessingWarning );
                    WorkPool.QueueWorkItem<string>( x => worker.Process( ConnectInfo.PathInfo, x ), file );
                }
            }
        }

        protected virtual void MailboxWorkerServerError( object sender, MessageInfoEventArgs e )
        {
            string message = string.Format( "WARNING: Error from POP server: {0}", e.Info );
            Log( message, ConsoleColor.DarkYellow, ConsoleColor.Black  );
        }

        protected virtual void MailboxWorkerPreviouslyDownloaded( object sender, MessageInfoEventArgs e )
        {
            string message = string.Format( "Already have {0}. Skipping.", e.Info );
            Log( message, ConsoleColor.Magenta, ConsoleColor.Black );
        }

        protected virtual void MailboxWorkerMessageRequested( object sender, MessageInfoEventArgs e )
        {
            string message = "Message requested: " + e.Info;
            Log( message );
        }

        protected virtual void MailboxWorkerMessageRecieved( object sender, MessageInfoEventArgs e )
        {
            lock( synclock )
            {
                Total++;
                IMimeFileWorker worker = Activator.CreateInstance<Tmime>();
                worker.BeginProcessing += new MessageInfoHandler( MimeFileWorkerBeginProcessing );
                worker.DoneProcessing += new MessageInfoHandler( MimeFileWorkerDoneProcessing );
                worker.ProcessingError += new MessageInfoHandler( MimeFileWorkerProcessingError );
                worker.ProcessingWarning += new MessageInfoHandler( MimeFileWorkerProcessingWarning );
                WorkPool.QueueWorkItem<string>( x => worker.Process( ConnectInfo.PathInfo, x ), e.Info );
            }

            string message = string.Format( "Message {0} received. ", Path.GetFileName( e.Info ) );
            Log( message );
        }

        protected virtual void MimeFileWorkerProcessingWarning( object sender, MessageInfoEventArgs e )
        {
            string message = string.Format( "WARNING: MIME processing: {0}\r\n{1}", e.Info, e.ExceptionInfo );
            Log( message, ConsoleColor.DarkYellow, ConsoleColor.Black );
        }

        protected virtual void MimeFileWorkerProcessingError( object sender, MessageInfoEventArgs e )
        {
            string message = string.Format( "ERROR: MIME processing: {0}\r\n{1}", e.Info, e.ExceptionInfo );
            Log( message, ConsoleColor.Red, ConsoleColor.Black );
        }

        protected virtual void MimeFileWorkerDoneProcessing( object sender, MessageInfoEventArgs e )
        {
            string message = string.Format( "{0} processed.", Path.GetFileName( e.Info ) );
            Log( message, ConsoleColor.Green, ConsoleColor.Black );
        }

        protected virtual void MimeFileWorkerBeginProcessing( object sender, MessageInfoEventArgs e )
        {
            string message = string.Format( "{0} scheduled for decoding.", Path.GetFileName( e.Info ) );
            Log( message, ConsoleColor.Cyan, ConsoleColor.Black );
        }

        protected virtual void MailboxWorkerMessageCountReceived( object sender, MessageInfoEventArgs e )
        {
            Log( string.Format( "{0} message(s) found.\r\n", e.Info ) );
        }

        protected void Log( string message )
        {
            if( null != this.LogWriter )
                LogWriter.WriteLine( message );

            Console.WriteLine( message );
        }

        protected void Log( string message, ConsoleColor foreground, ConsoleColor background )
        {
            if( null != this.LogWriter )
                LogWriter.WriteLine( message );

            lock( synclock )
            {
                ConsoleColor fg = Console.ForegroundColor;
                ConsoleColor bg = Console.BackgroundColor;

                Console.ForegroundColor = foreground;
                Console.BackgroundColor = background;
                Console.WriteLine( message );

                Console.ForegroundColor = fg;
                Console.BackgroundColor = bg;
            }
        }
    }
}