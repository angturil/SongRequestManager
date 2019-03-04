using SimpleJSON;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnhancedTwitchChat.Bot
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
            foreach(SongRequest song in songs)
                arr.Add(song.ToJson());

            File.WriteAllText(path, arr.ToString());
        }
    }

    public class RequestQueue
    {
        public static List<SongRequest> Songs = new List<SongRequest>();
        private static string requestsPath = Path.Combine(Environment.CurrentDirectory, "UserData", "EnhancedTwitchChat", "SongRequestQueue.json");
        public static void Read()
        {
            Songs = RequestManager.Read(requestsPath);
        }

        public static void Write()
        {
            RequestManager.Write(requestsPath, ref Songs);
        }
    }

    public class RequestHistory
    {
        public static List<SongRequest> Songs = new List<SongRequest>();
        private static string historyPath = Path.Combine(Environment.CurrentDirectory, "UserData", "EnhancedTwitchChat", "SongRequestHistory.json");
        public static void Read()
        {
            Songs = RequestManager.Read(historyPath);
        }

        public static void Write()
        {
            RequestManager.Write(historyPath, ref Songs);
        }
    }
}
