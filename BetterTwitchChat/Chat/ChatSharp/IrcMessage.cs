using System.Collections.Generic;
using System.Linq;

namespace ChatSharp
{
    /// <summary>
    /// Represents a raw IRC message. This is a low-level construct - PrivateMessage is used
    /// to represent messages sent from users.
    /// </summary>
    public class IrcMessage
    {
        /// <summary>
        /// The unparsed message.
        /// </summary>
        public string RawMessage { get; private set; }
        /// <summary>
        /// The message prefix.
        /// </summary>
        public string Prefix { get; private set; }
        /// <summary>
        /// The message command.
        /// </summary>
        public string Command { get; private set; }
        /// <summary>
        /// Additional parameters supplied with the message.
        /// </summary>
        public string[] Parameters { get; private set; }
        /// <summary>
        /// The message tags.
        /// </summary>
        public KeyValuePair<string, string>[] Tags { get; private set; }
        /// <summary>
        /// The message timestamp in ISO 8601 format.
        /// </summary>
        public Timestamp Timestamp { get; private set; }

        /// <summary>
        /// Initializes and decodes an IRC message, given the raw message from the server.
        /// </summary>
        public IrcMessage(string rawMessage)
        {
            RawMessage = rawMessage;
            Tags = new KeyValuePair<string, string>[] { };

            if (rawMessage.StartsWith("@"))
            {
                var rawTags = rawMessage.Substring(1, rawMessage.IndexOf(' ') - 1);
                rawMessage = rawMessage.Substring(rawMessage.IndexOf(' ') + 1);

                // Parse tags as key value pairs
                var tags = new List<KeyValuePair<string, string>>();
                foreach (string rawTag in rawTags.Split(';'))
                {
                    var replacedTag = rawTag.Replace(@"\:", ";");
                    // The spec declares `@a=` as a tag with an empty value, while `@b;` as a tag with a null value
                    KeyValuePair<string, string> tag = new KeyValuePair<string, string>(replacedTag, null);

                    if (replacedTag.Contains("="))
                    {
                        string key = replacedTag.Substring(0, replacedTag.IndexOf("="));
                        string value = replacedTag.Substring(replacedTag.IndexOf("=") + 1);
                        tag = new KeyValuePair<string, string>(key, value);
                    }

                    tags.Add(tag);
                }

                Tags = tags.ToArray();
            }

            if (rawMessage.StartsWith(":"))
            {
                Prefix = rawMessage.Substring(1, rawMessage.IndexOf(' ') - 1);
                rawMessage = rawMessage.Substring(rawMessage.IndexOf(' ') + 1);
            }

            if (rawMessage.Contains(' '))
            {
                Command = rawMessage.Remove(rawMessage.IndexOf(' '));
                rawMessage = rawMessage.Substring(rawMessage.IndexOf(' ') + 1);
                // Parse parameters
                var parameters = new List<string>();
                while (!string.IsNullOrEmpty(rawMessage))
                {
                    if (rawMessage.StartsWith(":"))
                    {
                        parameters.Add(rawMessage.Substring(1));
                        break;
                    }
                    if (!rawMessage.Contains(' '))
                    {
                        parameters.Add(rawMessage);
                        rawMessage = string.Empty;
                        break;
                    }
                    parameters.Add(rawMessage.Remove(rawMessage.IndexOf(' ')));
                    rawMessage = rawMessage.Substring(rawMessage.IndexOf(' ') + 1);
                }
                Parameters = parameters.ToArray();
            }
            else
            {
                // Violates RFC 1459, but we'll parse it anyway
                Command = rawMessage;
                Parameters = new string[0];
            }

            // Parse server-time message tag.
            // Fallback to server-info if both znc.in/server-info and the former exists.
            //
            // znc.in/server-time tag
            if (Tags.Any(tag => tag.Key == "t"))
            {
                var tag = Tags.SingleOrDefault(x => x.Key == "t");
                Timestamp = new Timestamp(tag.Value, true);
            }
            // server-time tag
            else if (Tags.Any(tag => tag.Key == "time"))
            {
                var tag = Tags.SingleOrDefault(x => x.Key == "time");
                Timestamp = new Timestamp(tag.Value);
            }
        }
    }
}
