namespace ChatSharp
{
    /// <summary>
    /// The results of an IRC WHOIS query. Depending on the capabilities of the server you're connected to,
    /// some of these fields may be null.
    /// </summary>
    public class WhoIs
    {
        internal WhoIs()
        {
            User = new IrcUser();
            SecondsIdle = -1;
            Channels = new string[0];
        }

        /// <summary>
        /// A fully populated IrcUser, including hostname, real name, etc.
        /// </summary>
        public IrcUser User { get; set; }
        /// <summary>
        /// A list of channels this user is joined to. Depending on the IRC network you connect to,
        /// this may omit channels that you are not present in.
        /// </summary>
        public string[] Channels { get; set; }
        /// <summary>
        /// If true, the whois'd user is a network operator.
        /// </summary>
        public bool IrcOp { get; set; }
        /// <summary>
        /// Seconds since this user last interacted with IRC.
        /// </summary>
        public int SecondsIdle { get; set; }
        /// <summary>
        /// The server this user is connected to.
        /// </summary>
        public string Server { get; set; }
        /// <summary>
        /// Additional information about the server this user is connected to.
        /// </summary>
        /// <value>The server info.</value>
        public string ServerInfo { get; set; }
        /// <summary>
        /// The nickserv account this user is logged into, if applicable.
        /// </summary>
        public string LoggedInAs { get; set; }
    }
}
