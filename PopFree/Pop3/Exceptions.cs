using System;
using System.Security.Authentication;

namespace PopFree.Pop3
{

    public class StartTlsNegotiationException : Exception
    {
        public StartTlsNegotiationException() : base() { }
        public StartTlsNegotiationException( string message ) : base( message ) { }
        public StartTlsNegotiationException( string message, Exception ex ) : base( message, ex ) { } 
    }

    public class PopServerResponseErrException : Exception
    {
        public PopServerResponseErrException() : base() { }
        public PopServerResponseErrException( string message ) : base( message ) { }
        public PopServerResponseErrException( string message, Exception innerException ) : base( message, innerException ) { } 
    }

    /// <summary>
    /// Base class for authentication errors.
    /// </summary>
    public class PopAuthenticationException : AuthenticationException
    {
        public PopAuthenticationException() : base() { }
        public PopAuthenticationException( string message ) : base( message ) { }
        public PopAuthenticationException( string message, Exception innerException ) : base( message, innerException ) { }

    }

	/// <summary>
	/// Thrown when the user mailbox is in a locked state
	/// </summary>
	/// <remarks>The mail boxes are locked when an existing session is open on the mail server. Lock conditions are also met in case of aborted sessions</remarks>
	public class MailboxLockedException : PopAuthenticationException
	{
        public MailboxLockedException() : base() { }
        public MailboxLockedException( string message ) : base( message ) { }
        public MailboxLockedException( string message, Exception innerException ) : base( message, innerException ) { }
    }

}

