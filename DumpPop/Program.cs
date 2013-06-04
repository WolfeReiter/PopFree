using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using Mono.Options;
using PopFree.Mime;
using PopFree.Pop3;

namespace PopFree.Pop3
{
    public static class Program
    {
        public static void Main( string[] args )
        {
            bool help;
            PopConnectInfo c = GetOptions( args, out help );
            if( help )
                return;

            var supervisor = new MailSupervisor<MailboxWorker, MimeFileWorker>( c );
            supervisor.ProcessMail();

            Console.WriteLine();
            Console.WriteLine( "Done. {0} message(s) processed.", supervisor.Total );
        }

        static PopConnectInfo GetOptions( string[] args, out bool help )
        {
            string username = null, password = null, host = null;
            int port = -1;
            SslNegotiationMode sslmode = SslNegotiationMode.Connect;
            SslProtocols sslprotocol = SslProtocols.None;
            string protocol = null, mode = null;
            string dir = null;
            DirectoryInfo directory = null;
            help = false;
			bool h = false;
            int i = 0;
            var p = new OptionSet()
            {
                { "d|directory=", "Working {DIRECTORY} in which mail will be dumped.", x=> dir = x },
                { "h|host=", "POP3 {SERVER} (e.g. pop.gmail.com).", x=> host = x },
                { "t|port=", "TCP {PORT}. Usually 110 or 995 (Gmail uses 995).", x=> int.TryParse(x, out port ) },
                { "u|username=", "{USERNAME}", x => username = x },
                { "p|password=", "{PASSWORD}", x => password = x },
                { "m|sslnegotiation=", "SSL/TLS Negotiation {MODE} (\"Pop3S\" or \"StartTls\"). Gmail uses Pop3S.", x => mode = x },
                { "e|ssl-protocol=", "Use SSL/TLS Encryption {PROTOCOL} (None, Default, Ssl2, Ssl3 or Tls).", x => protocol = x },
                { "?|help", "Show this message and exit.", x=> h = (x != null) }
            };

            p.Parse( args );
            if( (help=h) )
            {
                ShowHelp( p );
                return new PopConnectInfo();
            }

            if( string.IsNullOrEmpty( username ) || string.IsNullOrEmpty( password ) || string.IsNullOrEmpty( host )
                || string.IsNullOrEmpty( protocol ) || string.IsNullOrEmpty( mode ) || port < 0 || string.IsNullOrEmpty( dir ) )
            {
                ShowHelp( p );
                Console.WriteLine();
                Console.WriteLine( "Missing or incomplete command-line options detected." );
                Console.WriteLine( "Entering interactive mode. " );
                Console.WriteLine();
            }

            while( string.IsNullOrEmpty( dir ) )
            {
                Console.Write( "Working directory: " );
                dir = Console.ReadLine();
                if( string.IsNullOrEmpty( dir ) )
                    continue;

                dir = dir.Replace( "~", HomeDirectory );
                if( Directory.Exists( dir ) )
                {
                    directory = new DirectoryInfo( dir );
                }
                else
                {
                    if( AskCreateDir( Console.Out, Console.In, dir ) )
                        try { directory = new DirectoryInfo( dir ); }
                        catch( Exception ex ) { Console.WriteLine( ex.Message ); dir = null; }
                    else
                        dir = null;
                }
            }

            while( string.IsNullOrEmpty( host ) )
            {
                Console.Write( "POP3 Server: " );
                host = Console.ReadLine();
            }

            while( port < 0 )
            {
                Console.Write( "TCP Port (usually 110 or 995 (Gmail uses 995)): " );

                if( !int.TryParse( Console.ReadLine(), out port ) || port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort )
                {
                    Console.WriteLine( "Bad port number. Please try again." );
                    port = -1;
                }
            }

            i = 0;
            if( !string.IsNullOrEmpty( protocol ) )
            {
                try
                {
                    sslprotocol = (SslProtocols)Enum.Parse( typeof( SslProtocols ), protocol, true );
                }
                catch( ArgumentException ) { protocol = null; }
            }
            else
            {
                while( string.IsNullOrEmpty( protocol ) )
                {
                    if( i == 0 )
                    {
                        Console.WriteLine( "Encryption protocol (None, Default, Ssl2, Ssl3 or Tls)" );
                        Console.WriteLine( "Type \"Default\" to auto-negotiate SSL/TLS encryption or \"None\" for no encryption." );
                    }
                    Console.Write( "Encryption: " );
                    protocol = Console.ReadLine();
                    if( !string.IsNullOrEmpty( protocol ) )
                    {
                        try
                        {
                            sslprotocol = (SslProtocols)Enum.Parse( typeof( SslProtocols ), protocol, true );
                        }
                        catch( ArgumentException ) { protocol = null; }
                    }
                    else
                    {
                        Console.WriteLine( "Invalid protocol. Type \"None\", \"Default\", \"Ssl2\", \"Ssl3\" or \"Tls\" (sans quotes)." );
                        protocol = null;
                    }
                    i++;
                }
            }

            i = 0;
            if( SslProtocols.None != sslprotocol && !string.IsNullOrEmpty( mode ) )
            {
                try
                {
                    sslmode = (SslNegotiationMode)Enum.Parse( typeof( SslNegotiationMode ), mode, true );
                }
                catch( ArgumentException ) { mode = null; }
            }
            else
            {
                while( string.IsNullOrEmpty( mode ) )
                {
                    if( i == 0 )
                    {
                        Console.WriteLine( "SSL negotiation mode." );
                        Console.WriteLine( "Type \"Pop3S\" or \"StartTls\" (Gmail uses Pop3S)." );
                    }
                    Console.Write( "Negotiation Mode: " );
                    mode = Console.ReadLine();
                    if( !string.IsNullOrEmpty( protocol ) )
                    {
                        try
                        {
                            sslmode = (SslNegotiationMode)Enum.Parse( typeof( SslNegotiationMode ), mode, true );
                        }
                        catch( ArgumentException ) { mode = null; }
                    }
                    else
                    {
                        Console.WriteLine( "Invalid mode. Type \"Pop3S\" or \"StartTls\" (Gmail uses Pop3S)." );
                        mode = null;
                    }
                    i++;
                }
            }

            while( string.IsNullOrEmpty( username ) )
            {
                Console.Write( "Username: " );
                username = Console.ReadLine();
            }

            while( string.IsNullOrEmpty( password ) )
            {
                Console.Write( "Password: " );
                StringBuilder b = new StringBuilder();
                ConsoleKeyInfo k;
                while( ConsoleKey.Enter != (k = Console.ReadKey( true )).Key )
                {
                    if( ConsoleKey.Backspace == k.Key )
                    {
                        if( b.Length > 0 )
                        {
                            b.Remove( b.Length - 1, 1 );
                            //backspace over '*' char, write space to erase and backspace again to position the cursor
                            Console.Write( "\b \b" );
                        }
                    }
                    else
                    {
                        b.Append( k.KeyChar );
                        Console.Write( "*" );
                    }
                }
                password = b.ToString();
            }

            Console.WriteLine();

            directory = Directory.CreateDirectory( directory.FullName + Path.DirectorySeparatorChar + MailboxWorkerBase.StripIllegalFilenameChars( username ) );
            PopConnectInfo c = new PopConnectInfo()
            {
                Host = host,
                Password = password,
                SslNegotiationMode = sslmode,
                SslProtocol = sslprotocol,
                TcpPort = port,
                Username = username,
                PathInfo = new PathInfo() { WorkingDirecory = directory },
                Timeout = PopConnectInfo.MinTimeout
            };

            Directory.CreateDirectory( c.PathInfo.QueueDirectory );

            return c;
        }

        static void ShowHelp( OptionSet p )
        {
            Console.WriteLine( "Usage: DumpPop [OPTIONS]+" );
            Console.WriteLine( "PopFree Demo program downloads the contents of your mailbox to files in folders." );
            Console.WriteLine( "DumpPop.exe version {0}; PopFree.dll version {1}.",
                Assembly.GetExecutingAssembly().GetName().Version,
                Assembly.GetAssembly( typeof( PopClient ) ).GetName().Version );
            Console.WriteLine();
            Console.WriteLine( "Options:" );
            p.WriteOptionDescriptions( Console.Out );
        }

        static bool AskCreateDir( TextWriter writer, TextReader reader, string dir )
        {
            bool success = false;
            writer.WriteLine( "Directory not found." );
            DirectoryInfo d = new DirectoryInfo( dir );
            writer.Write( string.Format( "Do you want me to create \"{0}\" (y/n)? ", d.FullName ) );
            try
            {
                if( reader.ReadLine().StartsWith( "y", StringComparison.CurrentCultureIgnoreCase ) )
                {
                    d.Create();
                    success = true;
                }
            }
            catch( IOException ex )
            {
                Debug.WriteLine( ex );
                writer.WriteLine( ex.Message );
                success = false;
            }

            return success;
        }

        static string HomeDirectory
        {
            get
            {

                return (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX)
                    ? Environment.GetEnvironmentVariable( "HOME" )
                    : Environment.ExpandEnvironmentVariables( "%USERPROFILE%" );
            }
        }
    }
}
