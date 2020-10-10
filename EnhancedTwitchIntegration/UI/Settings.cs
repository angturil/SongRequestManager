//using CustomUI.MenuButton;
//using CustomUI.Settings;
//using CustomUI.Utilities;
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using UnityEngine;
//using UnityEngine.SceneManagement;
//using SongRequestManager;
//namespace SongRequestManager

//{
//    public class Settings
//    {
//        private static float[] incrementValues(float startValue = 0.0f, float step = 0.1f, int numberOfElements = 11)
//        {
//            if (step < 0.01f)
//            {
//                throw new Exception("Step value specified was too small! Minimum supported step value is 0.01");
//            }
//            Int64 multiplier = 100;
//            // Avoid floating point math as it results in rounding errors
//            Int64 fixedStart = (Int64)(startValue * multiplier);
//            Int64 fixedStep = (Int64)(step * multiplier);
//            var values = new float[numberOfElements];
//            for (int i = 0; i < values.Length; i++)
//                values[i] = (float)(fixedStart + (fixedStep * i)) / multiplier;
//            return values;
//        }

//        public static void OnLoad()
//        {
//            var menu = SettingsUI.CreateSubMenu("Song Request Manager");

//            var AutopickFirstSong = menu.AddBool("Autopick First Song", "Automatically pick the first song with sr!");
//            AutopickFirstSong.SetValue += (requests) => { RequestBotConfig.Instance.AutopickFirstSong = requests; };
//            AutopickFirstSong.GetValue += () => { return RequestBotConfig.Instance.AutopickFirstSong; };

//            var MiniumSongRating = menu.AddSlider("Minimum rating", "Minimum allowed song rating", 0, 100, 0.5f, false);
//            MiniumSongRating.SetValue += (scale) => { RequestBotConfig.Instance.LowestAllowedRating = scale; };
//            MiniumSongRating.GetValue += () => { return RequestBotConfig.Instance.LowestAllowedRating; };

//            var MaximumAllowedSongLength = menu.AddSlider("Maximum Song Length", "Longest allowed song length in minutes", 0, 999, 1.0f, false);
//            MaximumAllowedSongLength.SetValue += (scale) => { RequestBotConfig.Instance.MaximumSongLength =  scale; };
//            MaximumAllowedSongLength.GetValue += () => { return RequestBotConfig.Instance.MaximumSongLength; };


//            var MinimumNJS = menu.AddSlider("Minimum NJS allowed", "Disallow songs below a certain NJS", 0, 50, 1.0f, false);
//            MinimumNJS.SetValue += (scale) => { RequestBotConfig.Instance.MinimumNJS= scale; };
//            MinimumNJS.GetValue += () => { return RequestBotConfig.Instance.MinimumNJS; };

//            var TTSSupport = menu.AddBool("TTS Support", "Add ! to all command outputs for TTS Filtering");
//            TTSSupport.SetValue += (requests) => { RequestBotConfig.Instance.BotPrefix = requests ? "! " : ""; };
//            TTSSupport.GetValue += () => { return RequestBotConfig.Instance.BotPrefix!=""; };

//            var UserRequestLimit = menu.AddSlider("User Request limit", "Maximum requests in queue at one time", 0, 10, 1f, true);
//            UserRequestLimit.SetValue += (scale) => { RequestBotConfig.Instance.UserRequestLimit= (int ) scale; };
//            UserRequestLimit.GetValue += () => { return RequestBotConfig.Instance.UserRequestLimit; };

//            var SubRequestLimit = menu.AddSlider("Sub Request limit", "Maximum requests in queue at one time", 0, 10, 1f, true);
//            SubRequestLimit.SetValue += (scale) => { RequestBotConfig.Instance.SubRequestLimit = (int)scale; };
//            SubRequestLimit.GetValue += () => { return RequestBotConfig.Instance.SubRequestLimit; };

//            var ModRequestLimit = menu.AddSlider("Moderator Request limit", "Maximum requests in queue at one time", 0, 100, 1f, true);
//            ModRequestLimit.SetValue += (scale) => { RequestBotConfig.Instance.ModRequestLimit = (int)scale; };
//            ModRequestLimit.GetValue += () => { return RequestBotConfig.Instance.ModRequestLimit; };

//            var VIPBonus = menu.AddSlider("VIP Request bonus", "Additional requests allowed in queue", 0, 10, 1f, true);
//            VIPBonus.SetValue += (scale) => { RequestBotConfig.Instance.VipBonusRequests = (int)scale; };
//            VIPBonus.GetValue += () => { return RequestBotConfig.Instance.VipBonusRequests; };

//            var ModeratorRights = menu.AddBool("Full moderator rights", "Allow moderators access to ALL bot commands. Do you trust your mods?");
//            ModeratorRights.SetValue += (requests) => { RequestBotConfig.Instance.ModFullRights = requests ; };
//            ModeratorRights.GetValue += () => { return RequestBotConfig.Instance.ModFullRights; };



//        }
//    }
//}