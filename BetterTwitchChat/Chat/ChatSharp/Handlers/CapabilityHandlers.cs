using System;
using System.Collections.Generic;
using System.Linq;

namespace ChatSharp.Handlers
{
    internal static class CapabilityHandlers
    {
        public static void HandleCapability(IrcClient client, IrcMessage message)
        {
            var serverCaps = new List<string>();
            var supportedCaps = client.Capabilities.ToArray();
            var requestedCaps = new List<string>();

            switch (message.Parameters[1])
            {
                case "LS":
                    client.IsNegotiatingCapabilities = true;
                    // Parse server capabilities
                    var serverCapsString = (message.Parameters[2] == "*" ? message.Parameters[3] : message.Parameters[2]);
                    serverCaps.AddRange(serverCapsString.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));

                    // CAP 3.2 multiline support. Send CAP requests on the last CAP LS line.
                    // The last CAP LS line doesn't have * set as Parameters[2]
                    if (message.Parameters[2] != "*")
                    {
                        // Check which capabilities we support that the server supports
                        requestedCaps.AddRange(supportedCaps.Select(cap => cap.Name).Intersect(serverCaps));

                        // Check if we have to request any capability to be enabled.
                        // If not, end the capability negotiation.
                        if (requestedCaps.Count > 0)
                            client.SendRawMessage("CAP REQ :{0}", string.Join(" ", requestedCaps));
                        else
                        {
                            client.SendRawMessage("CAP END");
                            client.IsNegotiatingCapabilities = false;
                        }
                    }
                    break;
                case "ACK":
                    // Get the accepted capabilities
                    var acceptedCaps = message.Parameters[2].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string acceptedCap in acceptedCaps)
                    {
                        client.Capabilities.Enable(acceptedCap);
                        // Begin SASL authentication
                        if (acceptedCap == "sasl")
                        {
                            client.SendRawMessage("AUTHENTICATE PLAIN");
                            client.IsAuthenticatingSasl = true;
                        }
                    }

                    // Check if the enabled capabilities count is the same as the ones
                    // acknowledged by the server.
                    if (client.IsNegotiatingCapabilities && client.Capabilities.Enabled.Count() == acceptedCaps.Count() && !client.IsAuthenticatingSasl)
                    {
                        client.SendRawMessage("CAP END");
                        client.IsNegotiatingCapabilities = false;
                    }

                    break;
                case "NAK":
                    // Get the rejected capabilities
                    var rejectedCaps = message.Parameters[2].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string acceptedCap in rejectedCaps)
                        client.Capabilities.Disable(acceptedCap);

                    // Check if the disabled capabilities count is the same as the ones
                    // rejected by the server.
                    if (client.IsNegotiatingCapabilities && client.Capabilities.Disabled.Count() == rejectedCaps.Count())
                    {
                        client.SendRawMessage("CAP END");
                        client.IsNegotiatingCapabilities = false;
                    }

                    break;
                case "LIST":
                    var activeCapsString = (message.Parameters[2] == "*" ? message.Parameters[3] : message.Parameters[2]);
                    var activeCaps = activeCapsString.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    // Check which cap we have that isn't active but the server lists
                    // as active.
                    foreach (string cap in activeCaps)
                    {
                        if (client.Capabilities.Contains(cap))
                            if (!client.Capabilities[cap].IsEnabled)
                                client.Capabilities.Enable(cap);
                    }

                    break;
                case "NEW":
                    var newCaps = message.Parameters[2].Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                    var wantCaps = new List<string>();

                    // Check which new capabilities we support and send a REQ for them
                    wantCaps.AddRange(newCaps.Where(cap => client.Capabilities.Contains(cap) && !client.Capabilities[cap].IsEnabled));

                    client.SendRawMessage(string.Format("CAP REQ :{0}", string.Join(" ", wantCaps)));
                    break;
                case "DEL":
                    var disabledCaps = message.Parameters[2].Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries).ToList();

                    // Disable each recently server-disabled capability
                    disabledCaps.ForEach(
                        cap => {
                            if (client.Capabilities.Contains(cap) && client.Capabilities[cap].IsEnabled)
                                client.Capabilities.Disable(cap);
                        }
                    );
                    break;
                default:
                    break;
            }
        }
    }
}
