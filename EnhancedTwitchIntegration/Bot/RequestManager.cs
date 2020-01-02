using StreamCore.SimpleJSON;
using System.Collections.Generic;
using System.IO;

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
        private static string requestsPath = Path.Combine(Plugin.DataPath, "SongRequestQueue.dat");
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
        private static string historyPath = Path.Combine(Plugin.DataPath, "SongRequestHistory.dat");
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

}