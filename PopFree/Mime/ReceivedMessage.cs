using System.Collections.Specialized;
using System.Net.Mail;
using System.Text;

namespace PopFree.Mime
{
    /// <summary>
    /// RecievedMessage extends System.Net.Mail.MailMessage to provide a container received headers. Using the MailMessage.Headers collection
    /// is unsatisfactory for this purposed because decoded incoming headers are .NET Unicode strings while outgoing headers are ANSI text
    /// and encoded with quoted-printable or base64 transfer encoding when necessary as specified by RFC 2047. The unicode decoded values are
    /// not necessarily compatible with System.Net.Mail.MailMessage.Headers. If a value is not ANSI codepage, the call to MailMessage.Headers.Add(string)
    /// will throw a FormatException.
    /// </summary>
    public class ReceivedMessage : MailMessage
    {
        public ReceivedMessage()
        {
            RawReceivedHeaders = new StringBuilder();
            ReceivedHeaders = new NameValueCollection();
        }
        /// <summary>
        /// Buffer to hold raw header text.
        /// </summary>
        /// <remarks>This property has no interaction with the RecievedHeaders property.</remarks>
        public StringBuilder RawReceivedHeaders { get; private set; }
        /// <summary>
        /// Collection of parsed headers.
        /// </summary>
        public NameValueCollection ReceivedHeaders { get; private set; }
    }
}
