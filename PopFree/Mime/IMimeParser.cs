using System.Collections.Specialized;
using System.IO;

namespace PopFree.Mime
{
    public interface IMimeParser
    {
        /// <summary>
        /// Create NameValueCollection from a MIME stream reader. The calling code is responsible for closing the reader and the stream.
        /// </summary>
        /// <returns>Collection of headers. Returns null if the stream is null.</returns>
        NameValueCollection ParseHeaders( TextReader reader );
        /// <summary>
        /// Create NameValueCollection from a MIME stream. The calling code is responsible for closing the reader and the stream.
        /// </summary>
        /// <param name="stream">MIME stream</param>
        /// <returns>Collection of headers. Returns null if the stream is null.</returns>
        NameValueCollection ParseHeaders( Stream stream );
        /// <summary>
        /// Parse a TextReader. The calling code is responsible for closing the reader and the stream.
        /// </summary>
        /// <param name="reader"></param>
        /// <returns>Returns a ReceivedMessage object loaded from the TextReader. Return null if the TextReader is null.</returns>
        ReceivedMessage Parse( TextReader reader );
        /// <summary>
        /// Parse a Stream. The calling code is responsible for closing the reader and the stream.
        /// </summary>
        /// <param name="stream">MIME stream.</param>
        /// <returns>Returns a ReceivedMessage object loaded from the stream. Returns null if the stream is null.</returns>
        ReceivedMessage Parse( Stream stream );
    }
}
