using System;

namespace PopFree.Pop3
{
    public delegate void MessageInfoHandler( object sender, MessageInfoEventArgs e );

    public class MessageInfoEventArgs : EventArgs
    {
        public MessageInfoEventArgs( string info ) : this( info, null ) { }

        public MessageInfoEventArgs( string info, Exception exception )
        {
            Info = info;
            ExceptionInfo = exception;
        }

        public string Info { get; private set; }
        public Exception ExceptionInfo { get; private set; }
    }
}
