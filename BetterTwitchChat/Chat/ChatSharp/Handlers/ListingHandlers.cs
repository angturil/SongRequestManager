namespace ChatSharp.Handlers
{
    internal static class ListingHandlers
    {
        public static void HandleBanListPart(IrcClient client, IrcMessage message)
        {
            var parameterString = message.RawMessage.Substring(message.RawMessage.IndexOf(' '));
            var parameters = parameterString.Substring(parameterString.IndexOf(' ')).Split(' ');
            var request = client.RequestManager.PeekOperation("GETMODE b " + parameters[1]);
            var list = (MaskCollection)request.State;
            list.Add(new Mask(parameters[2], client.Users.GetOrAdd(parameters[3]),
                IrcClient.DateTimeFromIrcTime(int.Parse(parameters[4]))));
        }

        public static void HandleBanListEnd(IrcClient client, IrcMessage message)
        {
            var request = client.RequestManager.DequeueOperation("GETMODE b " + message.Parameters[1]);
            if (request.Callback != null)
                request.Callback(request);
        }

        public static void HandleExceptionListPart(IrcClient client, IrcMessage message)
        {
            var parameterString = message.RawMessage.Substring(message.RawMessage.IndexOf(' ') + 1);
            var parameters = parameterString.Substring(parameterString.IndexOf(' ') + 1).Split(' ');
            var request = client.RequestManager.PeekOperation("GETMODE e " + parameters[1]);
            var list = (MaskCollection)request.State;
            list.Add(new Mask(parameters[2], client.Users.GetOrAdd(parameters[3]),
                IrcClient.DateTimeFromIrcTime(int.Parse(parameters[4]))));
        }

        public static void HandleExceptionListEnd(IrcClient client, IrcMessage message)
        {
            var request = client.RequestManager.DequeueOperation("GETMODE e " + message.Parameters[1]);
            if (request.Callback != null)
                request.Callback(request);
        }

        public static void HandleInviteListPart(IrcClient client, IrcMessage message)
        {
            var parameterString = message.RawMessage.Substring(message.RawMessage.IndexOf(' ') + 1);
            var parameters = parameterString.Substring(parameterString.IndexOf(' ') + 1).Split(' ');
            var request = client.RequestManager.PeekOperation("GETMODE I " + parameters[1]);
            var list = (MaskCollection)request.State;
            list.Add(new Mask(parameters[2], client.Users.GetOrAdd(parameters[3]),
                IrcClient.DateTimeFromIrcTime(int.Parse(parameters[4]))));
        }

        public static void HandleInviteListEnd(IrcClient client, IrcMessage message)
        {
            var request = client.RequestManager.DequeueOperation("GETMODE I " + message.Parameters[1]);
            if (request.Callback != null)
                request.Callback(request);
        }

        public static void HandleQuietListPart(IrcClient client, IrcMessage message)
        {
            var parameterString = message.RawMessage.Substring(message.RawMessage.IndexOf(' ') + 1);
            var parameters = parameterString.Substring(parameterString.IndexOf(' ') + 1).Split(' ');
            var request = client.RequestManager.PeekOperation("GETMODE q " + parameters[1]);
            var list = (MaskCollection)request.State;
            list.Add(new Mask(parameters[2], client.Users.GetOrAdd(parameters[3]),
                IrcClient.DateTimeFromIrcTime(int.Parse(parameters[4]))));
        }

        public static void HandleQuietListEnd(IrcClient client, IrcMessage message)
        {
            var request = client.RequestManager.DequeueOperation("GETMODE q " + message.Parameters[1]);
            if (request.Callback != null)
                request.Callback(request);
        }
    }
}
