
using StreamCore;
using StreamCore.Chat;
using StreamCore.SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SongRequestManager
{
    public class RequestManager
    {
        public static List<SongRequest> Read(string path)
        {
            List<SongRequest> songs = new List<SongRequest>();
            if (File.Exists(path))
            {
                JSONNode json = JSON.Parse(File.ReadAllText(path));
                if (!json.IsNull)
                {
                    foreach (JSONObject j in json.AsArray)
                        songs.Add(new SongRequest().FromJson(j));
                }
            }
            return songs;
        }

        public static void Write(string path, ref List<SongRequest> songs)
        {
            if (!Directory.Exists(Path.GetDirectoryName(path)))
                Directory.CreateDirectory(Path.GetDirectoryName(path));

            JSONArray arr = new JSONArray();
            foreach (SongRequest song in songs)
                arr.Add(song.ToJson());

            File.WriteAllText(path, arr.ToString());
        }
    }

    public class RequestQueue
    {
        public static List<SongRequest> Songs = new List<SongRequest>();
        private static string requestsPath = Path.Combine(RequestBot.SRMData, "SongRequestQueue.json");
        public static void Read()
        {
            try
            {
                Songs = RequestManager.Read(requestsPath);
            }
            catch
            {
                RequestBot.Instance.QueueChatMessage("There was an error reading the request queue.");
            }

        }

        public static void Write()
        {
            RequestManager.Write(requestsPath, ref Songs);
        }
    }

    public class RequestHistory
    {
        public static List<SongRequest> Songs = new List<SongRequest>();
        private static string historyPath = Path.Combine(RequestBot.SRMData, "SongRequestHistory.json");
        public static void Read()
        {
            try
            {
                Songs = RequestManager.Read(historyPath);
            }
            catch
            {
                RequestBot.Instance.QueueChatMessage("There was an error reading the request history.");
            }

        }

        public static void Write()
        {
            RequestManager.Write(historyPath, ref Songs);
        }
    }

    public class SongBlacklist
    {
        public static Dictionary<string, SongRequest> Songs = new Dictionary<string, SongRequest>();
        private static string blacklistPath = Path.Combine(RequestBot.SRMData, "SongBlacklist.json");
        public static void Read()
        {
            try
            {

                Songs = RequestManager.Read(blacklistPath).ToDictionary(e => e.song["id"].Value);
            }
            catch
            {
                RequestBot.Instance.QueueChatMessage("There was an error reading the ban list. You may need to restore this file from a backup.");
                throw;
            }

        }

        public static void Write()
        {
            List<SongRequest> songs = Songs.Values.ToList();
            RequestManager.Write(blacklistPath, ref songs);
        }

        public static void ConvertFromList(string[] list)
        {
            SharedCoroutineStarter.instance.StartCoroutine(ConvertOnceInitialized(list));
        }

        private static IEnumerator ConvertOnceInitialized(string[] list)
        {
            yield return new WaitUntil(() => RequestBot.Instance);

            var user = new TwitchUser("Unknown");
            user.isMod = true;
            foreach (string s in list)
                RequestBot.Instance.Ban(user, s, true);
        }
    }
}