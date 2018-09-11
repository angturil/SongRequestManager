using ChatSharp.Events;
using System;

namespace ChatSharp.Handlers
{
    internal static class MOTDHandlers
    {
        public static string MOTD { get; set; }

        public static void HandleMOTDStart(IrcClient client, IrcMessage message)
        {
            MOTD = string.Empty;
        }

        public static void HandleMOTD(IrcClient client, IrcMessage message)
        {
            if (message.Parameters.Length != 2)
                throw new IrcProtocolException("372 MOTD message is incorrectly formatted.");
            var part = message.Parameters[1].Substring(2);
            MOTD += part + Environment.NewLine;
            client.OnMOTDPartRecieved(new ServerMOTDEventArgs(part));
        }

        public static void HandleEndOfMOTD(IrcClient client, IrcMessage message)
        {
            client.OnMOTDRecieved(new ServerMOTDEventArgs(MOTD));
            client.OnConnectionComplete(new EventArgs());
            // Verify our identity
            VerifyOurIdentity(client);
            
        }

	public static void HandleMOTDNotFound(IrcClient client, IrcMessage message)
	{
            client.OnMOTDRecieved(new ServerMOTDEventArgs(MOTD));
            client.OnConnectionComplete(new EventArgs());

            VerifyOurIdentity(client);
	}

	private static void VerifyOurIdentity(IrcClient client)
	{
            if (client.Settings.WhoIsOnConnect)
            {
                client.WhoIs(client.User.Nick, whois =>
                    {
                        client.User.Nick = whois.User.Nick;
                        client.User.User = whois.User.User;
                        client.User.Hostname = whois.User.Hostname;
                        client.User.RealName = whois.User.RealName;
                    });
            }
        }
    }
}
