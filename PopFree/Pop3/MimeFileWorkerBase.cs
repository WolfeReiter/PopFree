using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace PopFree.Pop3
{
    public abstract class MimeFileWorkerBase : IMimeFileWorker
    {

        protected abstract void ProcessFile( PathInfo pathInfo, string file );

        public void Process( PathInfo pathInfo, string file )
        {
            string f = Path.GetFileName( file );
            OnBeginProcessing( f );
            ProcessFile( pathInfo, file );
            OnDoneProcessing( f );
        }

        protected virtual void OnBeginProcessing( string info )
        {
            if( BeginProcessing != null )
                BeginProcessing( this, new MessageInfoEventArgs( info ) );
        }

        protected virtual void OnDoneProcessing( string info )
        {
            if( DoneProcessing != null )
                DoneProcessing( this, new MessageInfoEventArgs( info ) );
        }

        protected virtual void OnProcessingError( string info, Exception ex )
        {
            if( ProcessingError != null )
                ProcessingError( this, new MessageInfoEventArgs( info, ex ) );
        }

        protected virtual void OnProcessingWarning( string info )
        {
            OnProcessingWarning( info, null );
        }

        protected virtual void OnProcessingWarning( string info, Exception ex )
        {
            if( ProcessingWarning != null )
                ProcessingWarning( this, new MessageInfoEventArgs( info, ex ) );
        }

        public event MessageInfoHandler BeginProcessing;
        public event MessageInfoHandler DoneProcessing;
        public event MessageInfoHandler ProcessingError;
        public event MessageInfoHandler ProcessingWarning;
    
    }
}
