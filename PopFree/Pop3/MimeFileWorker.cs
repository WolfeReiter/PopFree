using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;
using PopFree.Mime;
using System.Net.Mail;

namespace PopFree.Pop3
{
    /// <summary>
    /// A simple MIME processor that extracts the contents of a MIME encoded file into file into a directory of the same name.
    /// </summary>
    public class MimeFileWorker : MimeFileWorkerBase, IMimeFileWorker
    {
        public MimeFileWorker() { }


        protected override void ProcessFile( PathInfo pathInfo, string file )
        {
            const string headers = "parsed-headers.txt";


            ReceivedMessage message = null;

            try
            {
                using( Stream stream = File.OpenRead( file ) )
                    message = MimeParser.Parse( stream );
            }
            catch( Exception ex )
            {
                OnProcessingError( ex.Message, ex );
                if( file.ToLower().StartsWith( pathInfo.QueueDirectory.ToLower() ) )
                    File.Move( file, pathInfo.BadmailDirectory + Path.DirectorySeparatorChar + Path.GetFileName( file ) );
                return;
            }

            DirectoryInfo d;
            string name = Path.GetFileNameWithoutExtension( file );
            if( file.ToLower().StartsWith( pathInfo.ProcessedDirectory.ToLower() ) )
                d = new DirectoryInfo( Path.GetDirectoryName( file ) + Path.DirectorySeparatorChar + name );
            else
                d = new DirectoryInfo( pathInfo.ProcessedDirectory + Path.DirectorySeparatorChar + name );

            if( !d.Exists )
            {
                d.Create();

                using( StreamWriter writer = File.CreateText( d.FullName + Path.DirectorySeparatorChar + headers ) )
                {
                    foreach( string h in message.ReceivedHeaders.Keys )
                    {
                        writer.WriteLine( "{0}: {1}", h, message.ReceivedHeaders[h] );
                    }
                }

                if( message.AlternateViews.Count > 0 )
                {
                    foreach( var view in message.AlternateViews )
                    {
                        DumpAttachment( d.FullName, view );
                    }
                }
                else
                {
                    if( message.Body != null )
                    {
                        string body = message.IsBodyHtml ? "body.html" : "body.txt";
                        using( StringReader reader = new StringReader( message.Body ) )
                        using( StreamWriter writer = File.CreateText( d.FullName + Path.DirectorySeparatorChar + body ) )
                        {
                            string line;
                            while( null != (line = reader.ReadLine()) )
                                writer.WriteLine( line );
                        }
                    }
                }

                foreach( var attachment in message.Attachments )
                {
                    DumpAttachment( d.FullName, attachment );
                }

                MoveQueueMessageToProcessed( pathInfo, file );

                foreach( string f in Directory.GetFiles( Directory.GetCurrentDirectory(), "attached-message*.eml" ) )
                {
                    FileInfo fileinfo = new FileInfo( f );
                    ProcessFile( pathInfo, fileinfo.Name );
                }


            }
            else
            {
                MoveQueueMessageToProcessed( pathInfo, file );
                OnProcessingWarning( "Message previously extracted. Continuing." );
            }
        }

        private static void MoveQueueMessageToProcessed( PathInfo pathInfo, string file )
        {
            if( file.ToLower().StartsWith( pathInfo.QueueDirectory.ToLower() ) )
            {
                FileInfo f = new FileInfo( pathInfo.ProcessedDirectory + Path.DirectorySeparatorChar + Path.GetFileName( file ) );
                if( f.Exists )
                    f.Delete();
                File.Move( file, f.FullName );
            }
        }

        private static void DumpAttachment( string dir, AttachmentBase attachment )
        {
            string name = null;
            if( !string.IsNullOrEmpty( attachment.ContentType.Name ) )
                name = attachment.ContentType.Name;
            else
            {
                name = attachment is AlternateView ? "alternate-view" : "attachment";
                string ext = MimeUtility.GetFileExtensionFromMediaType( attachment.ContentType.MediaType );
                if( ext == "eml" )
                    name = "attached-message";
                name = string.Format( name + "." + ext );
            }

            int i = 1;
            string shortname = null;
            while( File.Exists( dir + Path.DirectorySeparatorChar + name ) )
            {
                FileInfo fi = new FileInfo( name );
                if( null == shortname )
                    shortname = fi.Name.Substring( 0, fi.Name.Length - fi.Extension.Length );

                name = string.Format( "{0} ({1}){2}", shortname, i, fi.Extension );
                i++;
            }

            using( Stream stream = File.Create( dir + Path.DirectorySeparatorChar + name ) )
            {
                byte[] buffer = new byte[32768];
                while( true )
                {
                    int read = attachment.ContentStream.Read( buffer, 0, buffer.Length );
                    if( read <= 0 )
                        break;
                    stream.Write( buffer, 0, read );
                }
            }
        }

        private static IMimeParser MimeParser { get { return MimeStreamParser.Instance; } }

    }
}
