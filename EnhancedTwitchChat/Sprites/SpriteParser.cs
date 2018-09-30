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

namespace EnhancedTwitchChat.Sprites {
    public class BadgeInfo {
        public char swapChar;
        public Sprite sprite;
    };

    public class EmoteInfo {
        public char swapChar;
        public CachedSpriteData cachedSpriteInfo;
        public string swapString;
        public string emoteIndex;
        public bool isEmoji = false;
    };

    public class SpriteParser : MonoBehaviour {
        public static void Parse(ChatMessage newChatMessage, ChatHandler _chatHandler) {
            Dictionary<string, string> downloadQueue = new Dictionary<string, string>();
            List<EmoteInfo> parsedEmotes = new List<EmoteInfo>();
            List<BadgeInfo> parsedBadges = new List<BadgeInfo>();
            char swapChar = (char)0xE000;
            bool isActionMessage = newChatMessage.msg.Substring(1).StartsWith("ACTION") && newChatMessage.msg[0] == (char)0x1;

            if (isActionMessage) {
                newChatMessage.msg = newChatMessage.msg.TrimEnd((char)0x1).Substring(8);
            }
            
            var matches = Utilities.GetEmojisInString(newChatMessage.msg);
            if (matches.Count > 0) {
                foreach (Match m in matches) {
                    string emojiIndex = Utilities.WebParseEmojiRegExMatchEvaluator(m);
                    string replaceString = m.Value;

                    if (emojiIndex != String.Empty) {
                        emojiIndex += ".png";
                        if (!SpriteLoader.CachedSprites.ContainsKey(emojiIndex)) {
                            _chatHandler.QueueDownload(new SpriteDownloadInfo(emojiIndex, ImageType.Emoji, newChatMessage.messageInfo.messageID));
                        }
                        if (!downloadQueue.ContainsKey(emojiIndex)) {
                            downloadQueue.Add(emojiIndex, replaceString);
                        }
                    }

                    foreach (string index in downloadQueue.Keys.Distinct()) {
                        while (!SpriteLoader.CachedSprites.ContainsKey(index)) Thread.Sleep(0);
                        if (SpriteLoader.CachedSprites.TryGetValue(index, out var cachedSpriteInfo)) {
                            EmoteInfo swapInfo = new EmoteInfo();
                            swapInfo.isEmoji = true;
                            swapInfo.cachedSpriteInfo = cachedSpriteInfo;
                            swapInfo.swapChar = swapChar;
                            swapInfo.swapString = downloadQueue[index];
                            swapInfo.emoteIndex = index;
                            parsedEmotes.Add(swapInfo);

                            swapChar++;
                        }
                    }
                }
                parsedEmotes = parsedEmotes.OrderByDescending(o => o.swapString.Length).ToList();

                downloadQueue.Clear();
            }

            // Parse and download any twitch badges included in the message
            if (newChatMessage.messageInfo.badges.Count() > 0) {
                string[] badges = newChatMessage.messageInfo.badges.Split(',');
                foreach (string badgeInfo in badges) {
                    string[] parts = badgeInfo.Split('/');
                    string badgeName = parts[0];
                    string badgeVersion = parts[1];
                    
                    badgeName = $"{badgeName}{badgeVersion}";
                    string badgeIndex = string.Empty;
                    if (SpriteLoader.TwitchBadgeIDs.ContainsKey(badgeName)) {
                        badgeIndex = SpriteLoader.TwitchBadgeIDs[badgeName];
                        if (!SpriteLoader.CachedSprites.ContainsKey(badgeIndex)) {
                            _chatHandler.QueueDownload(new SpriteDownloadInfo(badgeIndex, ImageType.Badge, newChatMessage.messageInfo.messageID));
                        }
                        downloadQueue.Add(badgeIndex, badgeName);
                    }
                }
                foreach (string badgeIndex in downloadQueue.Keys) {
                    while (!SpriteLoader.CachedSprites.ContainsKey(badgeIndex)) Thread.Sleep(0);
                    if (SpriteLoader.CachedSprites.TryGetValue(badgeIndex, out var cachedSpriteInfo)) {
                        BadgeInfo swapInfo = new BadgeInfo();
                        swapInfo.sprite = cachedSpriteInfo.sprite;
                        swapInfo.swapChar = swapChar;

                        parsedBadges.Add(swapInfo);

                        swapChar++;
                    }
                }
                downloadQueue.Clear();
            }

            // Parse and download any twitch emotes in the message
            if (newChatMessage.messageInfo.twitchEmotes.Count() > 0) {
                string[] emotes = newChatMessage.messageInfo.twitchEmotes.Split('/');
                foreach (string emote in emotes) {
                    string[] emoteParts = emote.Split(':');
                    string emoteIndex = $"T{emoteParts[0]}";
                    if (!SpriteLoader.CachedSprites.ContainsKey(emoteIndex)) {
                        _chatHandler.QueueDownload(new SpriteDownloadInfo(emoteIndex, ImageType.Twitch, newChatMessage.messageInfo.messageID));
                    }
                    downloadQueue.Add(emoteIndex, emoteParts[1]);
                }
                foreach (string emoteIndex in downloadQueue.Keys) {
                    while (!SpriteLoader.CachedSprites.ContainsKey(emoteIndex)) Thread.Sleep(0);
                    if (SpriteLoader.CachedSprites.TryGetValue(emoteIndex, out var cachedSpriteInfo)) {
                        string emoteInfo = downloadQueue[emoteIndex].Split(',')[0];
                        string[] charsToReplace = emoteInfo.Split('-');

                        int startReplace = Convert.ToInt32(charsToReplace[0]);
                        int endReplace = Convert.ToInt32(charsToReplace[1]);

                        EmoteInfo swapInfo = new EmoteInfo();
                        string msg = newChatMessage.msg;
                        swapInfo.cachedSpriteInfo = cachedSpriteInfo;
                        swapInfo.swapChar = swapChar;
                        swapInfo.swapString = msg.Substring(startReplace, endReplace - startReplace + 1);
                        swapInfo.emoteIndex = emoteIndex;
                        parsedEmotes.Add(swapInfo);

                        swapChar++;
                    }
                }
                downloadQueue.Clear();
            }

            // Parse and download any BTTV/FFZ emotes in the message
            string[] msgParts = newChatMessage.msg.Split(' ').Distinct().ToArray();
            foreach (string word in msgParts) {
                //Plugin.Log($"WORD: {word}");
                string emoteIndex = String.Empty;
                ImageType emoteType = ImageType.None;
                if (SpriteLoader.BTTVEmoteIDs.ContainsKey(word)) {
                    emoteIndex = $"B{SpriteLoader.BTTVEmoteIDs[word]}";
                    emoteType = ImageType.BTTV;
                }
                else if (AnimatedSpriteLoader.BTTVAnimatedEmoteIDs.ContainsKey(word)) {
                    emoteIndex = $"AB{AnimatedSpriteLoader.BTTVAnimatedEmoteIDs[word]}";
                    emoteType = ImageType.BTTV_Animated;
                }
                else if (SpriteLoader.FFZEmoteIDs.ContainsKey(word)) {
                    emoteIndex = $"F{SpriteLoader.FFZEmoteIDs[word]}";
                    emoteType = ImageType.FFZ;
                }

                if (emoteType != ImageType.None) {
                    if (!SpriteLoader.CachedSprites.ContainsKey(emoteIndex)) {
                        _chatHandler.QueueDownload(new SpriteDownloadInfo(emoteIndex, emoteType, newChatMessage.messageInfo.messageID));
                    }
                    downloadQueue.Add(emoteIndex, word);
                }
            }
            foreach (string emoteIndex in downloadQueue.Keys) {
                while (!SpriteLoader.CachedSprites.ContainsKey(emoteIndex)) Thread.Sleep(0);
                if (SpriteLoader.CachedSprites.TryGetValue(emoteIndex, out var cachedSpriteInfo)) {

                    EmoteInfo swapInfo = new EmoteInfo();
                    swapInfo.cachedSpriteInfo = cachedSpriteInfo;
                    swapInfo.swapChar = swapChar;
                    swapInfo.swapString = downloadQueue[emoteIndex];
                    swapInfo.emoteIndex = emoteIndex;
                    parsedEmotes.Add(swapInfo);

                    swapChar++;
                }
            }

            // Replace each emote with a hex character; we'll draw the emote at the position of this character later on
            foreach (EmoteInfo e in parsedEmotes) {
                string replaceString = $" {Drawing.spriteSpacing}{Char.ConvertFromUtf32(e.swapChar)}{Drawing.spriteSpacing}";
                if (!e.isEmoji) {
                    string[] parts = newChatMessage.msg.Split(' ');
                    for (int i = 0; i < parts.Length; i++) {
                        if (parts[i] == e.swapString) {
                            parts[i] = replaceString;
                        }
                    }
                    newChatMessage.msg = string.Join(" ", parts);
                }
                else {
                    //Plugin.Log($"Replacing {e.emoteIndex} of length {e.swapString.Length.ToString()}");
                    // Replace emojis using the Replace function, since we don't care about spacing
                    newChatMessage.msg = newChatMessage.msg.Replace(e.swapString, replaceString);
                }
            }

            //// TODO: Re-add tagging, why doesn't unity have highlighting in its default rich text markup?
            //// Highlight messages that we've been tagged in
            //if (Plugin._twitchUsername != String.Empty && msg.Contains(Plugin._twitchUsername)) {
            //    msg = $"<mark=#ffff0050>{msg}</mark>";
            //}

            // Add the users name to the message with the correct color
            newChatMessage.msg = $"<color={newChatMessage.messageInfo.nameColor}><b>{newChatMessage.messageInfo.sender}</b></color><color=#00000000>|</color> {newChatMessage.msg}";

            // Prepend the users badges to the front of the message
            string badgeStr = String.Empty;
            if (parsedBadges.Count > 0) {
                parsedBadges.Reverse();
                for (int i = 0; i < parsedBadges.Count; i++) {
                    badgeStr = $"{Drawing.spriteSpacing}{Char.ConvertFromUtf32(parsedBadges[i].swapChar)}{Drawing.spriteSpacing}{Drawing.spriteSpacing} {badgeStr}";
                }
            }
            newChatMessage.msg = $"{badgeStr}{newChatMessage.msg}";

            // Italicize action messages and make the whole message the color of the users name
            if (isActionMessage) {
                newChatMessage.msg = $"<i><color={newChatMessage.messageInfo.nameColor}>{newChatMessage.msg}</color></i>";
            }

            // Finally, store our parsedEmotes and parsedBadges lists and render the message
            newChatMessage.messageInfo.parsedEmotes = parsedEmotes;
            newChatMessage.messageInfo.parsedBadges = parsedBadges;
            TwitchIRCClient.RenderQueue.Push(newChatMessage);
        }
    };
}
