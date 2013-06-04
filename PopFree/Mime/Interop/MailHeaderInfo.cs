using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PopFree.Mime.Interop
{
    /// <summary>
    /// This class exists to emulate the behavior in MailMessage.Headers.Add(string, string). The NameValueCollection implemented in 
    /// MailMessage.Headers is actually an internal Type called HeaderCollection. HeaderCollection has a leaky abstraction whereby
    /// sometimes calling Add(string,string) will "Add" as called for in the documentation of NameValueCollection but other times it will
    /// replace the existing value.
    /// </summary>
    internal static class MailHeaderInfo
    {
        private static readonly Dictionary<string, int> m_HeaderDictionary = new Dictionary<string, int>(0x21,StringComparer.OrdinalIgnoreCase);
        private static readonly HeaderInfo[] m_HeaderInfo = new [] { 
            new HeaderInfo(MailHeaderID.Bcc, "Bcc", true), new HeaderInfo(MailHeaderID.Cc, "Cc", true), 
            new HeaderInfo(MailHeaderID.Comments, "Comments", false), 
            new HeaderInfo(MailHeaderID.ContentDescription, "Content-Description", true), 
            new HeaderInfo(MailHeaderID.ContentDisposition, "Content-Disposition", true), 
            new HeaderInfo(MailHeaderID.ContentID, "Content-ID", true), 
            new HeaderInfo(MailHeaderID.ContentLocation, "Content-Location", true), 
            new HeaderInfo(MailHeaderID.ContentTransferEncoding, "Content-Transfer-Encoding", true), 
            new HeaderInfo(MailHeaderID.ContentType, "Content-Type", true), 
            new HeaderInfo(MailHeaderID.Date, "Date", true), new HeaderInfo(MailHeaderID.From, "From", true), 
            new HeaderInfo(MailHeaderID.Importance, "Importance", true), 
            new HeaderInfo(MailHeaderID.InReplyTo, "In-Reply-To", true), 
            new HeaderInfo(MailHeaderID.Keywords, "Keywords", false), 
            new HeaderInfo(MailHeaderID.Max, "Max", false), 
            new HeaderInfo(MailHeaderID.MessageID, "Message-ID", true), 
            new HeaderInfo(MailHeaderID.MimeVersion, "MIME-Version", true), 
            new HeaderInfo(MailHeaderID.Priority, "Priority", true), 
            new HeaderInfo(MailHeaderID.References, "References", true), 
            new HeaderInfo(MailHeaderID.ReplyTo, "Reply-To", true), 
            new HeaderInfo(MailHeaderID.ResentBcc, "Resent-Bcc", false), 
            new HeaderInfo(MailHeaderID.ResentCc, "Resent-Cc", false), 
            new HeaderInfo(MailHeaderID.ResentDate, "Resent-Date", false), 
            new HeaderInfo(MailHeaderID.ResentFrom, "Resent-From", false), 
            new HeaderInfo(MailHeaderID.ResentMessageID, "Resent-Message-ID", false), 
            new HeaderInfo(MailHeaderID.ResentSender, "Resent-Sender", false), 
            new HeaderInfo(MailHeaderID.ResentTo, "Resent-To", false), 
            new HeaderInfo(MailHeaderID.Sender, "Sender", true), 
            new HeaderInfo(MailHeaderID.Subject, "Subject", true), 
            new HeaderInfo(MailHeaderID.To, "To", true), 
            new HeaderInfo(MailHeaderID.XPriority, "X-Priority", true), 
            new HeaderInfo(MailHeaderID.XReceiver, "X-Receiver", false), 
            new HeaderInfo(MailHeaderID.XSender, "X-Sender", true),
            /* non-MSFT values  */
            new HeaderInfo(MailHeaderID.ListUnsubscribe, "List-Unsubscribe", true),
            new HeaderInfo(MailHeaderID.MailingList, "Mailing-List", true),
            new HeaderInfo(MailHeaderID.ListId, "List-Id", true),
            new HeaderInfo(MailHeaderID.ListPost, "List-Post", true),
            new HeaderInfo(MailHeaderID.ListHelp, "List-Help", true)
         };

        static MailHeaderInfo()
        {
            for (int i = 0; i < m_HeaderInfo.Length; i++)
            {
                m_HeaderDictionary.Add(m_HeaderInfo[i].NormalizedName, i);
            }
        }

        internal static MailHeaderID GetID(string name)
        {
            int num;
            if (m_HeaderDictionary.TryGetValue(name, out num))
            {
                return (MailHeaderID) num;
            }
            return MailHeaderID.Unknown;
        }

        internal static string GetString(MailHeaderID id)
        {
            MailHeaderID rid = id;
            if ((rid != MailHeaderID.Unknown) && (rid != (MailHeaderID.ZMaxEnumValue | MailHeaderID.Cc)))
            {
                return m_HeaderInfo[(int) id].NormalizedName;
            }
            return null;
        }

        internal static bool IsMatch(string name, MailHeaderID header)
        {
            int num;
            return (m_HeaderDictionary.TryGetValue(name, out num) && (num == (int)header));
        }

        internal static bool IsSingleton(string name)
        {
            int num;
            return (m_HeaderDictionary.TryGetValue(name, out num) && m_HeaderInfo[num].IsSingleton);
        }

        internal static bool IsWellKnown(string name)
        {
            int num;
            return m_HeaderDictionary.TryGetValue(name, out num);
        }

        internal static string NormalizeCase(string name)
        {
            int num;
            if (m_HeaderDictionary.TryGetValue(name, out num))
            {
                return m_HeaderInfo[num].NormalizedName;
            }
            return name;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HeaderInfo
        {
            public readonly string NormalizedName;
            public readonly bool IsSingleton;
            public readonly MailHeaderID ID;
            public HeaderInfo(MailHeaderID id, string name, bool isSingleton)
            {
                this.ID = id;
                this.NormalizedName = name;
                this.IsSingleton = isSingleton;
            }
        }
    }


}
