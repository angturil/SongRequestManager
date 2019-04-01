using CustomUI.MenuButton;
using CustomUI.Settings;
using CustomUI.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using SongRequestManager.RequestBotConfig;
namespace SongRequestManager

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
            var menu = SettingsUI.CreateSubMenu("Song Request Manager");

            var AutopickFirstSong = menu.AddBool("Autopick First Song", "Automatically pick the first song when searching"); 
            AutopickFirstSong.SetValue += (requests) => { RequestBotConfig.RequestBotConfig.Instance.AutopickFirstSong = requests; };
            AutopickFirstSong.GetValue += () => { return RequestBotConfig.RequestBotConfig.Instance.AutopickFirstSong; };
            /*
            var channelName = menu.AddString("Twitch Channel Name", "The name of the channel you want Enhanced Twitch Chat to monitor");
            channelName.SetValue += (channel) => { RConfig.Instance.TwitchChannelName = channel; };
            channelName.GetValue += () => { return Config.Instance.TwitchChannelName; };

            var fontName = menu.AddString("Menu Font Name", "The name of the system font you want to use for the chat. This can be any font you've installed on your computer!");
            fontName.SetValue += (font) => { Config.Instance.FontName = font; };
            fontName.GetValue += () => { return Config.Instance.FontName; };
            */

            var MiniumSongRating = menu.AddSlider("Minimum rating", "Minimum allowed song rating", 0,100, 0.5f, false);
            MiniumSongRating.SetValue += (scale) => { RequestBotConfig.RequestBotConfig.Instance.LowestAllowedRating = scale; };
            MiniumSongRating.GetValue += () => { return RequestBotConfig.RequestBotConfig.Instance.LowestAllowedRating; };
            /*
            var chatWidth = menu.AddInt("Chat Width", "The width of the chat.", 100, int.MaxValue, 10);
            chatWidth.SetValue += (width) => { RequestBotConfig.Instance.ChatWidth = width; };
            chatWidth.GetValue += () => { return (int)RequestBotConfig.Instance.ChatWidth; };

            */
        }
    }
}