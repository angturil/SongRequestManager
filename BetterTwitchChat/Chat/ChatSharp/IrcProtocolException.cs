using System;

namespace ChatSharp
{
    /// <summary>
    /// Raised when the server complains about IRC protocol errors.
    /// </summary>
    public class IrcProtocolException : Exception
    {
        internal IrcProtocolException()
        {
        }

        internal IrcProtocolException(string message) : base(message)
        {
            
        }
    }
}
