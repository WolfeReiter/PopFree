using System;
namespace PopFree.Pop3
{
    public interface IMailboxWorker
    {
        PopConnectInfo ConnectInfo { get; set; }
        int DownloadMessages();
        int ErrorSleepTimeout { get; set; }
        event MessageInfoHandler MessageCountReceived;
        event MessageInfoHandler MessagePreviouslyDownloaded;
        event MessageInfoHandler MessageRequested;
        event MessageInfoHandler MessageRecieved;
        event MessageInfoHandler ServerError;
    }
}
