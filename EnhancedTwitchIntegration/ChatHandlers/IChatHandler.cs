using System;
using ChatCore.Models.Twitch;

namespace SongRequestManager.ChatHandlers
{
    public interface IChatHandler
    {
        bool Connected { get; }
        TwitchUser Self { get; }
        void Send(string message, bool isCommand = false);
    }
}