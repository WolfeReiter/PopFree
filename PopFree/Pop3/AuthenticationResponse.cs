
namespace PopFree.Pop3
{
    /// <summary>
    /// Possible responses received from the server when performing an Authentication
    /// </summary>
    public enum AuthenticationResponse
    {
        /// <summary>
        /// Authentication succeeded
        /// </summary>
        Success = 0,
        /// <summary>
        /// Login doesn't exist on the Pop3 server
        /// </summary>		
        InvalidUser = 1,
        /// <summary>
        /// Password is invalid for the give login
        /// </summary>
        InvalidPassword = 2,
        /// <summary>
        /// Invalid login and/or password
        /// </summary>
        InvalidUserOrPassword = 3
    }	
}
