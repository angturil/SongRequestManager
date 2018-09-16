using System;

namespace ChatSharp.Events
{
    /// <summary>
    /// Describes the response to a WHOIS query. Note that ChatSharp may generate WHOIS
    /// queries that the consumer did not ask for.
    /// </summary>
    public class WhoIsReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// The WHOIS response from the server.
        /// </summary>
        public WhoIs WhoIsResponse { get; set; }

        internal WhoIsReceivedEventArgs(WhoIs whoIsResponse)
        {
            WhoIsResponse = whoIsResponse;
        }
    }
}
