
namespace PopFree.Pop3
{
    /// <summary>
    /// Authentication method to use
    /// </summary>
    /// <remarks>Auto means code will first attempt by using Apop method as its more secure.
    ///  In case of failure the code will fall back to UserPass method.
    /// </remarks>
    public enum AuthenticationMethod
    {
        /// <summary>
        /// Connect using the USER/PASS method. USER/PASS is secure but all Pop3 servers may not support this method
        /// </summary>
        UserPass = 1,
        /// <summary>
        /// Connect using the Apop method
        /// </summary>
        Apop = 2,
        /// <summary>
        /// Connect using USER/PASS. In case USER/PASS fails then revert to Apop
        /// </summary>
        Auto = 0,
        /// <summary>
        /// Connect using USER/PASS. In case USER/PASS fails then revert to Apop
        /// </summary>
        Default = Auto
    }
}
