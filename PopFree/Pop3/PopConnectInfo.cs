using System.IO;
using System.Security.Authentication;
using PopFree.Pop3;
using System.Collections.Generic;

namespace PopFree.Pop3
{
    public sealed class PopConnectInfo
    {
        public string Host { get; set;}
        public string Username { get; set; }
        public int TcpPort { get; set; }
        public string Password { get; set; }
        public SslProtocols SslProtocol { get; set; }
        public SslNegotiationMode SslNegotiationMode { get; set; }

        public int Timeout { get; set; }

        public const int MinTimeout = 60000;
        public const int MaxTimeout = 480000;

        public PathInfo PathInfo { get; set; }
    }

    public struct PathInfo
    {
        DirectoryInfo _workdir;
        public string TempDownloadFile { get; private set; }
        public string QueueDirectory { get; private set; }
        public string BadmailDirectory { get; private set; }
        public string ProcessedDirectory { get; private set; }
        public IDictionary<string, string> Meta { get; set; }

        public DirectoryInfo WorkingDirecory
        {
            get { return _workdir; }
            set
            {
                TempDownloadFile = value.FullName + Path.DirectorySeparatorChar + "~tmp.eml";
                QueueDirectory = value.FullName + Path.DirectorySeparatorChar + "queue";
                BadmailDirectory = value.FullName + Path.DirectorySeparatorChar + "badmail";
                ProcessedDirectory = value.FullName + Path.DirectorySeparatorChar + "processed";
                _workdir = value;
                CreateDirectories();
            }
        }

        private void CreateDirectories()
        {
            WorkingDirecory.Create();
            Directory.CreateDirectory( QueueDirectory );
            Directory.CreateDirectory( BadmailDirectory );
            Directory.CreateDirectory( ProcessedDirectory );
        }
    }
}
