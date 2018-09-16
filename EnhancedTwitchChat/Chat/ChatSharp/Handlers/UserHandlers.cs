using System;
using System.Linq;
using System.Collections.Generic;

namespace ChatSharp.Handlers
{
    internal static class UserHandlers
    {
        public static void HandleWhoIsUser(IrcClient client, IrcMessage message)
        {
            if (message.Parameters != null && message.Parameters.Length >= 6)
            {
                var whois = (WhoIs)client.RequestManager.PeekOperation("WHOIS " + message.Parameters[1]).State;
                whois.User.Nick = message.Parameters[1];
                whois.User.User = message.Parameters[2];
                whois.User.Hostname = message.Parameters[3];
                whois.User.RealName = message.Parameters[5];
                if (client.Users.Contains(whois.User.Nick))
                {
                    var user = client.Users[whois.User.Nick];
                    user.User = whois.User.User;
                    user.Hostname = whois.User.Hostname;
                    user.RealName = whois.User.RealName;
                    whois.User = user;
                }
            }
        }

        public static void HandleWhoIsLoggedInAs(IrcClient client, IrcMessage message)
        {
            var whois = (WhoIs)client.RequestManager.PeekOperation("WHOIS " + message.Parameters[1]).State;
            whois.LoggedInAs = message.Parameters[2];
        }

        public static void HandleWhoIsServer(IrcClient client, IrcMessage message)
        {
            var whois = (WhoIs)client.RequestManager.PeekOperation("WHOIS " + message.Parameters[1]).State;
            whois.Server = message.Parameters[2];
            whois.ServerInfo = message.Parameters[3];
        }

        public static void HandleWhoIsOperator(IrcClient client, IrcMessage message)
        {
            var whois = (WhoIs)client.RequestManager.PeekOperation("WHOIS " + message.Parameters[1]).State;
            whois.IrcOp = true;
        }

        public static void HandleWhoIsIdle(IrcClient client, IrcMessage message)
        {
            var whois = (WhoIs)client.RequestManager.PeekOperation("WHOIS " + message.Parameters[1]).State;
            whois.SecondsIdle = int.Parse(message.Parameters[2]);
        }

        public static void HandleWhoIsChannels(IrcClient client, IrcMessage message)
        {
            var whois = (WhoIs)client.RequestManager.PeekOperation("WHOIS " + message.Parameters[1]).State;
            var channels = message.Parameters[2].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < channels.Length; i++)
                if (!channels[i].StartsWith("#"))
                    channels[i] = channels[i].Substring(1);
            whois.Channels = whois.Channels.Concat(channels).ToArray();
        }

        public static void HandleWhoIsEnd(IrcClient client, IrcMessage message)
        {
            var request = client.RequestManager.DequeueOperation("WHOIS " + message.Parameters[1]);
            var whois = (WhoIs)request.State;
            if (!client.Users.Contains(whois.User.Nick))
                client.Users.Add(whois.User);
            if (request.Callback != null)
                request.Callback(request);
            client.OnWhoIsReceived(new Events.WhoIsReceivedEventArgs(whois));
        }

        public static void HandleWho(IrcClient client, IrcMessage message)
        {
            // A standard WHO request (numeric 352) is just like a WHOX request, except that it has less fields.
            foreach (var query in client.RequestManager.PendingOperations.Where(kvp => kvp.Key.StartsWith("WHO ")))
            {
                if (query.Key != string.Empty && query.Value != null)
                {
                    var whoList = (List<ExtendedWho>)client.RequestManager.PeekOperation(query.Key).State;
                    var who = new ExtendedWho();

                    who.Channel = message.Parameters[1];
                    who.User.User = message.Parameters[2];
                    who.IP = message.Parameters[3];
                    who.Server = message.Parameters[4];
                    who.User.Nick = message.Parameters[5];
                    who.Flags = message.Parameters[6];


                    var supposedRealName = message.Parameters[7];

                    // Parsing IRC spec craziness: the hops field is included in the real name field
                    var hops = supposedRealName.Substring(0, supposedRealName.IndexOf(" "));
                    who.Hops = int.Parse(hops);

                    var realName = supposedRealName.Substring(supposedRealName.IndexOf(" ") + 1);
                    who.User.RealName = realName;

                    whoList.Add(who);
                }
            }
        }

        public static void HandleWhox(IrcClient client, IrcMessage message)
        {
            int msgQueryType = int.Parse(message.Parameters[1]);
            var whoxQuery = new KeyValuePair<string, RequestOperation>();

            foreach (var query in client.RequestManager.PendingOperations.Where(kvp => kvp.Key.StartsWith("WHO ")))
            {
                // Parse query to retrieve querytype
                string key = query.Key;
                string[] queryParts = key.Split(new[] { ' ' });

                int queryType = int.Parse(queryParts[2]);

                // Check querytype against message querytype
                if (queryType == msgQueryType) whoxQuery = query;
            }

            if (whoxQuery.Key != string.Empty && whoxQuery.Value != null)
            {
                var whoxList = (List<ExtendedWho>)client.RequestManager.PeekOperation(whoxQuery.Key).State;
                var whox = new ExtendedWho();

                string key = whoxQuery.Key;
                string[] queryParts = key.Split(new[] { ' ' });

                // Handle what fields were included in the WHOX request
                WhoxField fields = (WhoxField)int.Parse(queryParts[3]);
                int fieldIdx = 1;
                do
                {
                    if ((fields & WhoxField.QueryType) != 0)
                    {
                        whox.QueryType = msgQueryType;
                        fieldIdx++;
                    }

                    if ((fields & WhoxField.Channel) != 0)
                    {
                        whox.Channel = message.Parameters[fieldIdx];
                        fieldIdx++;
                    }

                    if ((fields & WhoxField.Username) != 0)
                    {
                        whox.User.User = message.Parameters[fieldIdx];
                        fieldIdx++;
                    }

                    if ((fields & WhoxField.UserIp) != 0)
                    {
                        whox.IP = message.Parameters[fieldIdx];
                        fieldIdx++;
                    }

                    if ((fields & WhoxField.Hostname) != 0)
                    {
                        whox.User.Hostname = message.Parameters[fieldIdx];
                        fieldIdx++;
                    }

                    if ((fields & WhoxField.ServerName) != 0)
                    {
                        whox.Server = message.Parameters[fieldIdx];
                        fieldIdx++;
                    }

                    if ((fields & WhoxField.Nick) != 0)
                    {
                        whox.User.Nick = message.Parameters[fieldIdx];
                        fieldIdx++;
                    }

                    if ((fields & WhoxField.Flags) != 0)
                    {
                        whox.Flags = message.Parameters[fieldIdx];
                        fieldIdx++;
                    }

                    if ((fields & WhoxField.Hops) != 0)
                    {
                        whox.Hops = int.Parse(message.Parameters[fieldIdx]);
                        fieldIdx++;
                    }

                    if ((fields & WhoxField.TimeIdle) != 0)
                    {
                        whox.TimeIdle = int.Parse(message.Parameters[fieldIdx]);
                        fieldIdx++;
                    }

                    if ((fields & WhoxField.AccountName) != 0)
                    {
                        whox.User.Account = message.Parameters[fieldIdx];
                        fieldIdx++;
                    }

                    if ((fields & WhoxField.OpLevel) != 0)
                    {
                        whox.OpLevel = message.Parameters[fieldIdx];
                        fieldIdx++;
                    }

                    if ((fields & WhoxField.RealName) != 0)
                    {
                        whox.User.RealName = message.Parameters[fieldIdx];
                        fieldIdx++;
                    }
                }
                while (fieldIdx < message.Parameters.Length - 1);
                whoxList.Add(whox);
            }
        }

        public static void HandleLoggedIn(IrcClient client, IrcMessage message)
        {
            client.User.Account = message.Parameters[2];
        }

        public static void HandleWhoEnd(IrcClient client, IrcMessage message)
        {
            if (client.ServerInfo.ExtendedWho)
            {
                var query = client.RequestManager.PendingOperations.Where(kvp => kvp.Key.StartsWith("WHO " + message.Parameters[1])).FirstOrDefault();
                var request = client.RequestManager.DequeueOperation(query.Key);
                var whoxList = (List<ExtendedWho>)request.State;

                foreach (var whox in whoxList)
                    if (!client.Users.Contains(whox.User.Nick))
                        client.Users.Add(whox.User);

                request.Callback?.Invoke(request);
                client.OnWhoxReceived(new Events.WhoxReceivedEventArgs(whoxList.ToArray()));
            }
            else
            {
                var query = client.RequestManager.PendingOperations.Where(kvp => kvp.Key == "WHO " + message.Parameters[1]).FirstOrDefault();
                var request = client.RequestManager.DequeueOperation(query.Key);
                var whoList = (List<ExtendedWho>)request.State;

                foreach (var who in whoList)
                    if (!client.Users.Contains(who.User.Nick))
                        client.Users.Add(who.User);

                request.Callback?.Invoke(request);
                client.OnWhoxReceived(new Events.WhoxReceivedEventArgs(whoList.ToArray()));
            }
        }

        public static void HandleAccount(IrcClient client, IrcMessage message)
        {
            var user = client.Users.GetOrAdd(message.Prefix);
            user.Account = message.Parameters[0];
        }

        public static void HandleChangeHost(IrcClient client, IrcMessage message)
        {
            var user = client.Users.Get(message.Prefix);

            // Only handle CHGHOST for users we know
            if (user != null)
            {
                var newIdent = message.Parameters[0];
                var newHostname = message.Parameters[1];

                if (user.User != newIdent)
                    user.User = newIdent;
                if (user.Hostname != newHostname)
                    user.Hostname = newHostname;
            }
        }
    }
}
