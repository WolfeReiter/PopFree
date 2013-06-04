
namespace PopFree.Pop3
{
    public enum SslNegotiationMode
    {
        /// <summary>
        /// The entire dialog with the server is negotiated over SSL.
        /// </summary>
        Connect,
        Pop3S = Connect,
        /// <summary>
        /// Dialog with the server begins in plain text. The client issues the STLS command to initialize the encryption.
        /// </summary>
        StartTls
    }
}
