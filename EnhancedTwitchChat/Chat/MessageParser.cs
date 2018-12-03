using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using EnhancedTwitchChat.Utils;
using EnhancedTwitchChat.Chat;
using System.Text.RegularExpressions;
using EnhancedTwitchChat.UI;
using AsyncTwitch;

using Random = System.Random;

namespace EnhancedTwitchChat.Sprites
{
    public class SpriteInfo
    {
        public char swapChar;
        public string spriteIndex;
    }

    public class BadgeInfo : SpriteInfo
    {
    };

    public class EmoteInfo : SpriteInfo
    {
        public string swapString;
        public bool isEmoji;
    };

    public class MessageParser : MonoBehaviour
    {
        private static Dictionary<int, string> _userColors = new Dictionary<int, string>();
        public static void Parse(ChatMessage newChatMessage)
        {
            //Plugin.Log($"Parsing message for {(newChatMessage.twitchMessage.Author.IsBroadcaster ? "broadcaster" : "user")} {newChatMessage.twitchMessage.Author.DisplayName} with user-id {newChatMessage.twitchMessage.Author.UserID}");
            Dictionary<string, string> downloadQueue = new Dictionary<string, string>();
            List<EmoteInfo> parsedEmotes = new List<EmoteInfo>();
            List<BadgeInfo> parsedBadges = new List<BadgeInfo>();

            char swapChar = (char)0xE000;
            bool isActionMessage = newChatMessage.msg.Substring(1).StartsWith("ACTION") && newChatMessage.msg[0] == (char)0x1;

            if (isActionMessage)
                newChatMessage.msg = newChatMessage.msg.TrimEnd((char)0x1).Substring(8);

            // Parse and download any twitch emotes in the message
            if (newChatMessage.twitchMessage.Emotes.Count() > 0)
            {
                foreach (TwitchEmote e in newChatMessage.twitchMessage.Emotes)
                {
                    string emoteIndex = $"T{e.Id}";
                    if (!SpriteDownloader.CachedSprites.ContainsKey(emoteIndex))
                        SpriteDownloader.Instance.Queue(new SpriteDownloadInfo(emoteIndex, ImageType.Twitch, newChatMessage.twitchMessage.Id));

                    downloadQueue.Add(emoteIndex, string.Join("-", e.Index[0]));
                }
                Thread.Sleep(5);

                foreach (string emoteIndex in downloadQueue.Keys)
                {
                    string emoteInfo = downloadQueue[emoteIndex].Split(',')[0];
                    string[] charsToReplace = emoteInfo.Split('-');
                    int startReplace = Convert.ToInt32(charsToReplace[0]);
                    int endReplace = Convert.ToInt32(charsToReplace[1]);
                    string msg = newChatMessage.msg;

                    EmoteInfo swapInfo = new EmoteInfo();
                    swapInfo.swapChar = swapChar;
                    swapInfo.swapString = msg.Substring(startReplace, endReplace - startReplace + 1);
                    swapInfo.spriteIndex = emoteIndex;
                    parsedEmotes.Add(swapInfo);
                    swapChar++;
                }
                downloadQueue.Clear();
            }

            // Parse and download any twitch badges included in the message
            if (newChatMessage.twitchMessage.Author.Badges.Count() > 0)
            {
                foreach (Badge b in newChatMessage.twitchMessage.Author.Badges)
                {
                    string badgeName = $"{b.BadgeName}{b.BadgeVersion}";
                    string badgeIndex = string.Empty;
                    if (SpriteDownloader.TwitchBadgeIDs.ContainsKey(badgeName))
                    {
                        badgeIndex = SpriteDownloader.TwitchBadgeIDs[badgeName];
                        if (!SpriteDownloader.CachedSprites.ContainsKey(badgeIndex))
                            SpriteDownloader.Instance.Queue(new SpriteDownloadInfo(badgeIndex, ImageType.Badge, newChatMessage.twitchMessage.Id));

                        downloadQueue.Add(badgeIndex, badgeName);
                    }
                }
                Thread.Sleep(5);

                foreach (string badgeIndex in downloadQueue.Keys)
                {
                    BadgeInfo swapInfo = new BadgeInfo();
                    swapInfo.swapChar = swapChar;
                    swapInfo.spriteIndex = badgeIndex;
                    parsedBadges.Add(swapInfo);
                    swapChar++;
                }
                downloadQueue.Clear();
            }

            var matches = Utilities.GetEmojisInString(newChatMessage.msg);
            if (matches.Count > 0)
            {
                foreach (Match m in matches)
                {
                    string emojiIndex = Utilities.WebParseEmojiRegExMatchEvaluator(m);
                    string replaceString = m.Value;

                    if (emojiIndex != String.Empty)
                    {
                        emojiIndex += ".png";
                        if (!SpriteDownloader.CachedSprites.ContainsKey(emojiIndex))
                            SpriteDownloader.Instance.Queue(new SpriteDownloadInfo(emojiIndex, ImageType.Emoji, newChatMessage.twitchMessage.Id));

                        if (!downloadQueue.ContainsKey(emojiIndex))
                            downloadQueue.Add(emojiIndex, replaceString);
                    }
                    Thread.Sleep(5);

                    foreach (string index in downloadQueue.Keys.Distinct())
                    {
                        EmoteInfo swapInfo = new EmoteInfo();
                        swapInfo.isEmoji = true;
                        swapInfo.swapChar = swapChar;
                        swapInfo.swapString = downloadQueue[index];
                        swapInfo.spriteIndex = index;
                        parsedEmotes.Add(swapInfo);
                        swapChar++;
                    }
                }
                parsedEmotes = parsedEmotes.OrderByDescending(o => o.swapString.Length).ToList();
                downloadQueue.Clear();
            }

            // Parse and download any BTTV/FFZ emotes in the message
            string[] msgParts = newChatMessage.msg.Split(' ').Distinct().ToArray();
            foreach (string word in msgParts)
            {
                //Plugin.Log($"WORD: {word}");
                string emoteIndex = String.Empty;
                ImageType emoteType = ImageType.None;
                if (SpriteDownloader.BTTVEmoteIDs.ContainsKey(word))
                {
                    emoteIndex = $"B{SpriteDownloader.BTTVEmoteIDs[word]}";
                    emoteType = ImageType.BTTV;
                }
                else if (SpriteDownloader.BTTVAnimatedEmoteIDs.ContainsKey(word))
                {
                    emoteIndex = $"AB{SpriteDownloader.BTTVAnimatedEmoteIDs[word]}";
                    emoteType = ImageType.BTTV_Animated;
                }
                else if (SpriteDownloader.FFZEmoteIDs.ContainsKey(word))
                {
                    emoteIndex = $"F{SpriteDownloader.FFZEmoteIDs[word]}";
                    emoteType = ImageType.FFZ;
                }

                if (emoteType != ImageType.None)
                {
                    if (!SpriteDownloader.CachedSprites.ContainsKey(emoteIndex))
                        SpriteDownloader.Instance.Queue(new SpriteDownloadInfo(emoteIndex, emoteType, newChatMessage.twitchMessage.Id));

                    downloadQueue.Add(emoteIndex, word);
                }
            }
            Thread.Sleep(5);

            foreach (string emoteIndex in downloadQueue.Keys)
            {
                EmoteInfo swapInfo = new EmoteInfo();
                swapInfo.swapChar = swapChar;
                swapInfo.swapString = downloadQueue[emoteIndex];
                swapInfo.spriteIndex = emoteIndex;
                parsedEmotes.Add(swapInfo);
                swapChar++;
            }

            // Replace each emote with a hex character; we'll draw the emote at the position of this character later on
            foreach (EmoteInfo e in parsedEmotes)
            {
                string replaceString = $"\uE969 {Drawing.spriteSpacing}{Char.ConvertFromUtf32(e.swapChar)}{Drawing.spriteSpacing} ";
                if (!e.isEmoji)
                {
                    string[] parts = newChatMessage.msg.Split(' ');
                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (parts[i] == e.swapString)
                            parts[i] = replaceString;
                    }
                    newChatMessage.msg = string.Join(" ", parts);
                }
                else
                {
                    // Replace emojis using the Replace function, since we don't care about spacing
                    newChatMessage.msg = newChatMessage.msg.Replace(e.swapString, replaceString);
                }
            }

            //// TODO: Re-add tagging, why doesn't unity have highlighting in its default rich text markup?
            //// Highlight messages that we've been tagged in
            //if (Plugin._twitchUsername != String.Empty && msg.Contains(Plugin._twitchUsername)) {
            //    msg = $"<mark=#ffff0050>{msg}</mark>";
            //}

            Thread.Sleep(5);

            string displayColor = newChatMessage.twitchMessage.Author.Color;
            if ((displayColor == null || displayColor == String.Empty) && !_userColors.ContainsKey(newChatMessage.twitchMessage.Author.DisplayName.GetHashCode()))
                _userColors.Add(newChatMessage.twitchMessage.Author.DisplayName.GetHashCode(), (String.Format("#{0:X6}", new Random(newChatMessage.twitchMessage.Author.DisplayName.GetHashCode()).Next(0x1000000)) + "FF"));
            newChatMessage.twitchMessage.Author.Color = (displayColor == null || displayColor == string.Empty) ? _userColors[newChatMessage.twitchMessage.Author.DisplayName.GetHashCode()] : displayColor;

            // Add the users name to the message with the correct color
            newChatMessage.msg = $"<color={newChatMessage.twitchMessage.Author.Color}><b>{newChatMessage.twitchMessage.Author.DisplayName}</b></color><color=#00000000>|</color> {newChatMessage.msg}";

            // Prepend the users badges to the front of the message
            string badgeStr = String.Empty;
            if (parsedBadges.Count > 0)
            {
                parsedBadges.Reverse();
                for (int i = 0; i < parsedBadges.Count; i++)
                    badgeStr = $" {Drawing.spriteSpacing}{Char.ConvertFromUtf32(parsedBadges[i].swapChar)}{Drawing.spriteSpacing}{Drawing.spriteSpacing}{Drawing.spriteSpacing} {badgeStr}";
            }
            newChatMessage.msg = $"{badgeStr}{newChatMessage.msg}";

            // Italicize action messages and make the whole message the color of the users name
            if (isActionMessage)
                newChatMessage.msg = $"<i><color={newChatMessage.twitchMessage.Author.Color}>{newChatMessage.msg}</color></i>";

            // Finally, store our parsedEmotes and parsedBadges lists and render the message
            newChatMessage.parsedEmotes = parsedEmotes;
            newChatMessage.parsedBadges = parsedBadges;
            TwitchIRCClient.RenderQueue.Push(newChatMessage);
        }
    };
}
