using CustomUI.MenuButton;
using CustomUI.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EnhancedTwitchChat.UI
{
    public class Settings
    {
        private static float[] incrementValues(float startValue = 0.0f, float step = 0.1f, int numberOfElements = 11)
        {
            if (step < 0.01f)
            {
                throw new Exception("Step value specified was too small! Minimum supported step value is 0.01");
            }
            Int64 multiplier = 100;
            // Avoid floating point math as it results in rounding errors
            Int64 fixedStart = (Int64)(startValue * multiplier);
            Int64 fixedStep = (Int64)(step * multiplier);
            var values = new float[numberOfElements];
            for (int i = 0; i < values.Length; i++)
                values[i] = (float)(fixedStart + (fixedStep * i)) / multiplier;
            return values;
        }

        public static void OnLoad()
        {
            var menu = SettingsUI.CreateSubMenu("Enhanced Twitch Chat");
            var channelName = menu.AddString("Twitch Channel Name", "The name of the channel you want Enhanced Twitch Chat to monitor");
            channelName.SetValue += (channel) => { Config.Instance.TwitchChannel = channel; };
            channelName.GetValue += () => { return Config.Instance.TwitchChannel; };

            var fontName = menu.AddString("Menu Font Name", "The name of the system font you want to use for the chat. This can be any font you've installed on your computer!");
            fontName.SetValue += (font) => { Config.Instance.FontName = font; };
            fontName.GetValue += () => { return Config.Instance.FontName; };

            var chatScale = menu.AddList("Chat Scale", incrementValues(0, 0.1f, 51), "The size of text and emotes in the chat.");
            chatScale.SetValue += (scale) => { Config.Instance.ChatScale = scale; };
            chatScale.GetValue += () => { return Config.Instance.ChatScale; };
            chatScale.FormatValue += (value) => { return value.ToString(); };

            var chatWidth = menu.AddInt("Chat Width", "The width of the chat.", 100, int.MaxValue, 10);
            chatWidth.SetValue += (width) => { Config.Instance.ChatWidth = width; };
            chatWidth.GetValue += () => { return (int)Config.Instance.ChatWidth; };

            var reverseChatOrder = menu.AddBool("Reverse Chat Order", "Makes the chat scroll from top to bottom instead of bottom to top.");
            reverseChatOrder.SetValue += (order) => { Config.Instance.ReverseChatOrder = order; };
            reverseChatOrder.GetValue += () => { return Config.Instance.ReverseChatOrder; };

            var songRequestsEnabled = menu.AddBool("Song Request Bot", "Enables song requests in chat! Click the \"Next Request\" button in the top right corner of your song list to move onto the next request!\r\n\r\n<size=60%>Use <b>!request <beatsaver-id></b> or <b>!request <song name></b> in chat to request songs!</size>");
            songRequestsEnabled.SetValue += (requests) => { Config.Instance.SongRequestBot = requests; };
            songRequestsEnabled.GetValue += () => { return Config.Instance.SongRequestBot; };

            var animatedEmotes = menu.AddBool("Animated Emotes", "Enables animated BetterTwitchTV/FrankerFaceZ/Cheermotes in the chat. When disabled, these emotes will still appear but will not be animated.");
            animatedEmotes.SetValue += (animted) => { Config.Instance.AnimatedEmotes = animted; };
            animatedEmotes.GetValue += () => { return Config.Instance.AnimatedEmotes; };

        }
    }
}
