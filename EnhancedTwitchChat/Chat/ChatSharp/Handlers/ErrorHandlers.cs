using ChatSharp.Events;
using System.Linq;

namespace ChatSharp.Handlers
{
    /// <summary>
    /// IRC error replies handler. See rfc1459 6.1.
    /// </summary>
    internal static class ErrorHandlers
    {
        /// <summary>
        /// IRC Error replies handler. See rfc1459 6.1.
        /// </summary>
        public static void HandleError(IrcClient client, IrcMessage message)
        {
            client.OnErrorReply(new Events.ErrorReplyEventArgs(message));
        }
    }
}
