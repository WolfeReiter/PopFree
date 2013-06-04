using System;
namespace PopFree.Pop3
{
    public interface IMimeFileWorker
    {
        event MessageInfoHandler BeginProcessing;
        event MessageInfoHandler DoneProcessing;
        void Process( PathInfo pathInfo, string file );
        event MessageInfoHandler ProcessingError;
        event MessageInfoHandler ProcessingWarning;
    }
}
