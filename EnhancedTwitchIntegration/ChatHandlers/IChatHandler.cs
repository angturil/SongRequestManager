using System;

namespace SongRequestManager.ChatHandlers
{
    public interface IChatHandler
    {
        bool Connected { get; }
        ChatUser Self { get; }
        void Send(string message, bool isCommand = false);
    }
}