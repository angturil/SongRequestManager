using System;

namespace ChatSharp.Events
{
    /// <summary>
    /// Describes an invalid nick event.
    /// </summary>
    public class ErronousNickEventArgs : EventArgs
    {
        private static Random random;
        private static string GenerateRandomNick()
        {
            const string nickCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

            if (random == null)
                random = new Random();
            var nick = new char[8];
            for (int i = 0; i < nick.Length; i++)
                nick[i] = nickCharacters[random.Next(nickCharacters.Length)];
            return new string(nick);
        }

        /// <summary>
        /// The nick that was not accepted by the server.
        /// </summary>
        /// <value>The invalid nick.</value>
        public string InvalidNick { get; set; }
        /// <summary>
        /// The nick ChatSharp intends to use instead.
        /// </summary>
        /// <value>The new nick.</value>
        public string NewNick { get; set; }
        /// <summary>
        /// Set to true to instruct ChatSharp NOT to send a valid nick.
        /// </summary>
        public bool DoNotHandle { get; set; }

        internal ErronousNickEventArgs(string invalidNick)
        {
            InvalidNick = invalidNick;
            NewNick = GenerateRandomNick();
            DoNotHandle = false;
        }
    }
}
