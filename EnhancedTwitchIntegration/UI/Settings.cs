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
using SongRequestManager;
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
            AutopickFirstSong.SetValue += (requests) => { RequestBotConfig.Instance.AutopickFirstSong = requests; };
            AutopickFirstSong.GetValue += () => { return RequestBotConfig.Instance.AutopickFirstSong; };

            var MiniumSongRating = menu.AddSlider("Minimum rating", "Minimum allowed song rating", 0, 100, 0.5f, false);
            MiniumSongRating.SetValue += (scale) => { RequestBotConfig.Instance.LowestAllowedRating = scale; };
            MiniumSongRating.GetValue += () => { return RequestBotConfig.Instance.LowestAllowedRating; };
        }
    }
}