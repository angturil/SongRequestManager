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
using static POCs.Sanjay.SharpSnippets.Drawing.ColorExtensions;
using Random = System.Random;


namespace EnhancedTwitchChat.Textures
{
    public class ImageInfo
    {
        public char swapChar;
        public string textureIndex;
        public ImageType imageType;
    }

    public class BadgeInfo : ImageInfo
    {
    };

    public class EmoteInfo : ImageInfo
    {
        public string swapString;
        public bool isEmoji;
    };

    public class MessageParser : MonoBehaviour
    {
        private static readonly Regex _emoteRegex = new Regex(@"(?<EmoteIndex>[0-9]+):(?<StartIndex>[^-]+)-(?<EndIndex>[^,^\/\s^;]+)");
        private static readonly Regex _badgeRegex = new Regex(@"(?<BadgeName>[a-z,0-9,_-]+)\/(?<BadgeVersion>[^,^;]+),*");

        private static Dictionary<int, string> _userColors = new Dictionary<int, string>();
        public static void Parse(ChatMessage newChatMessage)
        {
            char swapChar = (char)0xE000;
            List<EmoteInfo> parsedEmotes = new List<EmoteInfo>();
            List<BadgeInfo> parsedBadges = new List<BadgeInfo>();

            bool isActionMessage = newChatMessage.msg.Substring(1).StartsWith("ACTION") && newChatMessage.msg[0] == (char)0x1;
            if (isActionMessage)
                newChatMessage.msg = newChatMessage.msg.TrimEnd((char)0x1).Substring(8);

            string emojilessMessage = newChatMessage.msg;
            // Parse and download any emojis included in the message
            var matches = Utilities.GetEmojisInString(newChatMessage.msg);
            if (matches.Count > 0)
            {
                List<string> foundEmojis = new List<string>();
                foreach (Match m in matches)
                {
                    string emojiIndex = Utilities.WebParseEmojiRegExMatchEvaluator(m);
                    string replaceString = m.Value;

                    // Build up a copy of the message with no emojis so we can parse out our twitch emotes properly
                    emojilessMessage = emojilessMessage.Replace(m.Value, " ");

                    if (emojiIndex != String.Empty)
                    {
                        emojiIndex += ".png";
                        if (!ImageDownloader.CachedTextures.ContainsKey(emojiIndex))
                            ImageDownloader.Instance.Queue(new TextureDownloadInfo(emojiIndex, ImageType.Emoji, newChatMessage.twitchMessage.id));

                        if (!foundEmojis.Contains(emojiIndex))
                        {
                            foundEmojis.Add(emojiIndex);
                            EmoteInfo swapInfo = new EmoteInfo();
                            swapInfo.imageType = ImageType.Emoji;
                            swapInfo.isEmoji = true;
                            swapInfo.swapChar = swapChar;
                            swapInfo.swapString = replaceString;
                            swapInfo.textureIndex = emojiIndex;
                            parsedEmotes.Add(swapInfo);
                            swapChar++;
                        }
                    }
                }
                parsedEmotes = parsedEmotes.OrderByDescending(o => o.swapString.Length).ToList();
                Thread.Sleep(5);
            }

            var emotes = _emoteRegex.Matches(newChatMessage.twitchMessage.emotes);
            // Parse and download any twitch emotes in the message
            if (emotes.Count > 0)
            {
                foreach (Match e in emotes)
                {
                    string emoteIndex = $"T{e.Groups["EmoteIndex"].Value}";
                    if (!ImageDownloader.CachedTextures.ContainsKey(emoteIndex))
                        ImageDownloader.Instance.Queue(new TextureDownloadInfo(emoteIndex, ImageType.Twitch, newChatMessage.twitchMessage.id));
                    
                    int startReplace = Convert.ToInt32(e.Groups["StartIndex"].Value);
                    int endReplace = Convert.ToInt32(e.Groups["EndIndex"].Value);
                    
                    EmoteInfo swapInfo = new EmoteInfo();
                    swapInfo.swapChar = swapChar;
                    swapInfo.swapString = emojilessMessage.Substring(startReplace, endReplace - startReplace + 1);
                    swapInfo.textureIndex = emoteIndex;
                    swapInfo.imageType = ImageType.Twitch;
                    parsedEmotes.Add(swapInfo);
                    swapChar++;
                }
                Thread.Sleep(5);
            }
            
            var badges = _badgeRegex.Matches(newChatMessage.twitchMessage.user.badges);
            // Parse and download any twitch badges included in the message
            if (badges.Count > 0)
            {
                foreach (Match b in badges)
                {
                    string badgeName = $"{b.Groups["BadgeName"].Value}{b.Groups["BadgeVersion"].Value}";
                    string badgeIndex = string.Empty;
                    if (ImageDownloader.TwitchBadgeIDs.ContainsKey(badgeName))
                    {
                        badgeIndex = ImageDownloader.TwitchBadgeIDs[badgeName];
                        if (!ImageDownloader.CachedTextures.ContainsKey(badgeIndex))
                            ImageDownloader.Instance.Queue(new TextureDownloadInfo(badgeIndex, ImageType.Badge, newChatMessage.twitchMessage.id));

                        BadgeInfo swapInfo = new BadgeInfo();
                        swapInfo.swapChar = swapChar;
                        swapInfo.textureIndex = badgeIndex;
                        swapInfo.imageType = ImageType.Badge;
                        parsedBadges.Add(swapInfo);
                        swapChar++;
                    }
                }
                Thread.Sleep(5);
            }
            
            // Parse and download any BTTV/FFZ emotes and cheeremotes in the message
            string[] msgParts = newChatMessage.msg.Split(' ').Distinct().ToArray();
            foreach (string w in msgParts)
            {
                string word = w;
                //Plugin.Log($"WORD: {word}");
                string textureIndex = String.Empty;
                ImageType imageType = ImageType.None;
                if (ImageDownloader.BTTVEmoteIDs.ContainsKey(word))
                {
                    textureIndex = $"B{ImageDownloader.BTTVEmoteIDs[word]}";
                    imageType = ImageType.BTTV;
                }
                else if (ImageDownloader.BTTVAnimatedEmoteIDs.ContainsKey(word))
                {
                    textureIndex = $"AB{ImageDownloader.BTTVAnimatedEmoteIDs[word]}";
                    imageType = ImageType.BTTV_Animated;
                }
                else if (ImageDownloader.FFZEmoteIDs.ContainsKey(word))
                {
                    textureIndex = $"F{ImageDownloader.FFZEmoteIDs[word]}";
                    imageType = ImageType.FFZ;
                }
                else if (newChatMessage.twitchMessage.bits > 0 && Utilities.cheermoteRegex.IsMatch(word.ToLower()))
                {
                    Match match = Utilities.cheermoteRegex.Match(word.ToLower());
                    string prefix = match.Groups["Prefix"].Value;
                    if (ImageDownloader.TwitchCheermoteIDs.ContainsKey(prefix))
                    {
                        int bits = Convert.ToInt32(match.Groups["Value"].Value);
                        string tier = ImageDownloader.TwitchCheermoteIDs[prefix].GetTier(bits);
                        textureIndex = $"{prefix}{tier}";
                        imageType = ImageType.Cheermote;
                    }
                }

                if (imageType != ImageType.None)
                {
                    if (!ImageDownloader.CachedTextures.ContainsKey(textureIndex))
                        ImageDownloader.Instance.Queue(new TextureDownloadInfo(textureIndex, imageType, newChatMessage.twitchMessage.id));

                    EmoteInfo swapInfo = new EmoteInfo();
                    swapInfo.imageType = imageType;
                    swapInfo.swapChar = swapChar;
                    swapInfo.swapString = word;
                    swapInfo.textureIndex = textureIndex;
                    parsedEmotes.Add(swapInfo);
                    swapChar++;
                }
            }
            Thread.Sleep(5);
            
            string[] parts = newChatMessage.msg.Split(' ');
            // Replace each emote with a unicode character from a private range; we'll draw the emote at the position of this character later on
            foreach (EmoteInfo e in parsedEmotes.Where(e => !e.isEmoji))
            {
                string extraInfo = String.Empty;
                if (e.imageType == ImageType.Cheermote)
                {
                    Match cheermote = Utilities.cheermoteRegex.Match(e.swapString);
                    string numBits = cheermote.Groups["Value"].Value;
                    extraInfo = $"\u200A<color={ImageDownloader.TwitchCheermoteIDs[cheermote.Groups["Prefix"].Value].GetColor(Convert.ToInt32(numBits))}>\u200A<size=3><b>{numBits}</b></size></color>\u200A";
                }
                string replaceString = $"\u00A0{Drawing.imageSpacing}{Char.ConvertFromUtf32(e.swapChar)}{extraInfo}";
                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i] == e.swapString)
                        parts[i] = replaceString;
                }
            }

            // Then replace our emojis after all the emotes are handled, since these aren't sensitive to spacing
            StringBuilder sb = new StringBuilder(string.Join(" ", parts));
            foreach (EmoteInfo e in parsedEmotes.Where(e => e.isEmoji))
                sb.Replace(e.swapString, $"\u00A0{Drawing.imageSpacing}{Char.ConvertFromUtf32(e.swapChar)}");
            newChatMessage.msg = sb.ToString();

            Thread.Sleep(5);

            //// TODO: Re-add tagging, why doesn't unity have highlighting in its default rich text markup?
            //// Highlight messages that we've been tagged in
            //if (Plugin._twitchUsername != String.Empty && msg.Contains(Plugin._twitchUsername)) {
            //    msg = $"<mark=#ffff0050>{msg}</mark>";
            //}

            string displayColor = newChatMessage.twitchMessage.user.color;
            if ((displayColor == null || displayColor == String.Empty) && !_userColors.ContainsKey(newChatMessage.twitchMessage.user.displayName.GetHashCode()))
            {
                // Generate a random color
                Random rand = new Random(newChatMessage.twitchMessage.user.displayName.GetHashCode());
                int r = rand.Next(255);
                int g = rand.Next(255);
                int b = rand.Next(255);

                // Convert it to a pastel color
                System.Drawing.Color pastelColor = Drawing.GetPastelShade(System.Drawing.Color.FromArgb(255, r, g, b));
                int argb = ((int)pastelColor.R << 16) + ((int)pastelColor.G << 8) + (int)pastelColor.B;
                string colorString = String.Format("#{0:X6}", argb) + "FF";
                _userColors.Add(newChatMessage.twitchMessage.user.displayName.GetHashCode(), colorString);
            }
            newChatMessage.twitchMessage.user.color = (displayColor == null || displayColor == string.Empty) ? _userColors[newChatMessage.twitchMessage.user.displayName.GetHashCode()] : displayColor;

            // Add the users name to the message with the correct color
            newChatMessage.msg = $"<color={newChatMessage.twitchMessage.user.color}><b>{newChatMessage.twitchMessage.user.displayName}</b></color>{(isActionMessage ? String.Empty : ":")} {newChatMessage.msg}";

            // Prepend the users badges to the front of the message
            StringBuilder badgeStr = new StringBuilder();
            if (parsedBadges.Count > 0)
            {
                parsedBadges.Reverse();
                for (int i = 0; i < parsedBadges.Count; i++)
                    badgeStr.Insert(0, $"\u200A{Drawing.imageSpacing}{Char.ConvertFromUtf32(parsedBadges[i].swapChar)}\u2004");
            }
            badgeStr.Append("\u200A");
            badgeStr.Append(newChatMessage.msg);

            // Finally, store our final message, parsedEmotes and parsedBadges; then render the message
            newChatMessage.msg = badgeStr.ToString();
            newChatMessage.parsedEmotes = parsedEmotes;
            newChatMessage.parsedBadges = parsedBadges;
            newChatMessage.isActionMessage = isActionMessage;
            TwitchWebSocketClient.RenderQueue.Enqueue(newChatMessage);
        }

    };
}
