using CustomUI.MenuButton;
using CustomUI.Settings;
using CustomUI.Utilities;
using CustomUI.UIElements;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using EnhancedTwitchChat.Chat;
using EnhancedTwitchChat.Config;

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
            var reconnectButton = MenuButtonUI.AddButton("Reconnect to Twitch", "Click this button if your twitch chat stops working, and hopefully with some luck it will fix it.", () =>
            {
                Task.Run(() => TwitchWebSocketClient.Connect());
            });


            var menu = SettingsUI.CreateSubMenu("Enhanced Twitch Chat");
            var channelName = menu.AddString("Twitch Channel Name", "The name of the channel you want Enhanced Twitch Chat to monitor");
            channelName.SetValue += (channel) => { TwitchLoginConfig.Instance.TwitchChannelName = channel; };
            channelName.GetValue += () => { return TwitchLoginConfig.Instance.TwitchChannelName; };

            var fontName = menu.AddString("Menu Font Name", "The name of the system font you want to use for the chat. This can be any font you've installed on your computer!");
            fontName.SetValue += (font) => { ChatConfig.Instance.FontName = font; };
            fontName.GetValue += () => { return ChatConfig.Instance.FontName; };

            var songRequestsEnabled = menu.AddBool("Song Request Bot", "Enables song requests in chat! Click the \"Next Request\" button in the top right corner of your song list to move onto the next request!\r\n\r\n<size=60%>Use <b>!request <beatsaver-id></b> or <b>!request <song name></b> to request songs!</size>");
            songRequestsEnabled.SetValue += (requests) => { RequestBotConfig.Instance.RequestBotEnabled = requests; };
            songRequestsEnabled.GetValue += () => { return RequestBotConfig.Instance.RequestBotEnabled; };
            
            var persistentRequestQueue = menu.AddBool("Persistent Request Queue", "When enabled, the song request queue will persist after you restart the game.");
            persistentRequestQueue.SetValue += (persistentQueue) => { RequestBotConfig.Instance.PersistentRequestQueue = persistentQueue; };
            persistentRequestQueue.GetValue += () => { return RequestBotConfig.Instance.PersistentRequestQueue; };

            var animatedEmotes = menu.AddBool("Animated Emotes", "Enables animated BetterTwitchTV/FrankerFaceZ/Cheermotes in the chat. When disabled, these emotes will still appear but will not be animated.");
            animatedEmotes.SetValue += (animted) => { Plugin.Log($"Setting animated emotes to {animted}"); ChatConfig.Instance.AnimatedEmotes = animted; };
            animatedEmotes.GetValue += () => { return ChatConfig.Instance.AnimatedEmotes; };

            var reverseChatOrder = menu.AddBool("Reverse Chat Order", "Makes the chat scroll from top to bottom instead of bottom to top.");
            reverseChatOrder.SetValue += (order) => { ChatConfig.Instance.ReverseChatOrder = order; };
            reverseChatOrder.GetValue += () => { return ChatConfig.Instance.ReverseChatOrder; };

            var chatScale = menu.AddSlider("Chat Scale", "The size of text and emotes in the chat.", 0f, 10f, 0.1f, false);
            chatScale.SetValue += (scale) => { ChatConfig.Instance.ChatScale = scale; };
            chatScale.GetValue += () => { return ChatConfig.Instance.ChatScale; };

            var chatWidth = menu.AddSlider("Chat Width", "The width of the chat.", 0, 1000, 1, true);
            chatWidth.SetValue += (width) => { ChatConfig.Instance.ChatWidth = width; };
            chatWidth.GetValue += () => { return (int)ChatConfig.Instance.ChatWidth; };

            var messageSpacing = menu.AddSlider("Message Spacing", "The amount of vertical space between each message.", 0, 20, 2, true);
            messageSpacing.SetValue += (spacing) => { ChatConfig.Instance.MessageSpacing = spacing; };
            messageSpacing.GetValue += () => { return (int)ChatConfig.Instance.MessageSpacing; };
            
            var textColor = menu.AddColorPicker("Text Color", "Choose the color of the menu text.", ChatConfig.Instance.TextColor);
            textColor.SetValue += (texCol) => { ChatConfig.Instance.TextColor = texCol; };
            textColor.GetValue += () => { return ChatConfig.Instance.TextColor; };

            var backgroundColor = menu.AddColorPicker("Background Color", "Choose the color of the menu background.", ChatConfig.Instance.BackgroundColor);
            backgroundColor.SetValue += (bgCol) => {  ChatConfig.Instance.BackgroundColor = bgCol; };
            backgroundColor.GetValue += () => { return ChatConfig.Instance.BackgroundColor; };
        }
    }
}
