using StreamCore.Chat;
using StreamCore.SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using SongRequestManager;
using StreamCore.Utils;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

using System.Security.Cryptography;
// Feature requests: Add Reason for being banned to banlist
//  

using SongBrowserPlugin;
using SongBrowserPlugin.DataAccess;
using SongLoaderPlugin;
//
// NOTE: Any unreleased code structure, dependencies, or files are subject to change without notice. Any dependencies you create around this code 
// are virtually guaranteed not to work in future builds. If I thought the code was release ready, it wouldn't be here.

namespace SongRequestManager
{
    public partial class RequestBot : MonoBehaviour
    {
        enum MapField { id, version, songName, songSubName, authorName, rating, hashMd5, hashSha1 };

        const int partialhash = 3; // Do Not ever set this below 4. It will cause severe performance loss

        public class SongMap
        {
            public List<string> Fields = new List<string>(); // For performance, we need to extract these from the JSON.
            public JSONObject song;
            public string path;
            public string LevelId;

            void IndexFields(List<string> Fields, bool Add = true)
            {
                foreach (var field in Fields)
                {
                    string[] parts = field.ToLower().Split(MapDatabase.wordseparator , StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        string mypart = (part.Length > partialhash) ? part.Substring(0, partialhash) : part;
                        if (Add)
                            MapDatabase.SearchDictionary.AddOrUpdate(mypart, (k) => { HashSet<SongMap> va = new HashSet<SongMap>(); va.Add(this); return va; }, (k, va) => { va.Add(this); return va; });
                        else
                        {
                            MapDatabase.SearchDictionary[mypart].Remove(this); // An empty keyword is fine, and actually uncommon
                        }
                    }
                }
            }

            public bool isKeyPresent(string match)
            {

                foreach (var field in Fields)
                {
                    string[] parts = field.ToLower().Split(MapDatabase.wordseparator, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        if (part.StartsWith(match)) return true;
                    }
                }
                return false;
            }


            public SongMap(string id, string version, string songName, string songSubName, string authorName, string duration, string rating)
            {
                JSONObject song = new JSONObject();
                song.Add("id", id);
                song.Add("version", version);
                song.Add("songName", songName);
                song.Add("songSubName", songSubName);
                song.Add("authorName", authorName);
                song.Add("duration", duration);
                song.Add("rating", rating);

                IndexSong(song);
            }


            public SongMap(JSONObject song, string LevelId = "", string path = "")
            {
                if (LevelId == "")
                {
                    LevelId = string.Join("∎", song["hashMd5"].Value.ToUpper(), song["songName"].Value, song["songSubName"].Value, song["authorName"], song["bpm"].AsFloat.ToString()) + "∎";             
                }

                SongMap oldmap;
                if (MapDatabase.MapLibrary.TryGetValue(song["id"].Value,out oldmap))
                {

                    if (LevelId == oldmap.LevelId && song["version"].Value == oldmap.Fields[1])
                    {
                        oldmap.song = song;
                        return;
                    }
                    oldmap.UnIndexSong();                    
                }


                this.song = song;
                this.path = path;
                this.LevelId = LevelId;
                IndexSong(song);
            }

            void UnIndexSong()
            {
                SongMap temp;
                IndexFields(Fields, false);
                MapDatabase.MapLibrary.TryRemove(Fields[0], out temp);
                MapDatabase.MapLibrary.TryRemove(Fields[1], out temp);
                MapDatabase.MapLibrary.TryRemove(song["hashMd5"].Value.ToUpper(), out temp);
                MapDatabase.LevelId.TryRemove(LevelId, out temp);
            }

            void IndexSong(JSONObject song)
            {
                try
                {
                    this.song = song;
                    Fields.Clear();
                    Fields.Add(song["id"].Value);
                    Fields.Add(song["version"].Value);
                    Fields.Add(song["songName"].Value);
                    Fields.Add(song["songSubName"].Value);
                    Fields.Add(song["authorName"].Value);

                    IndexFields(Fields);
                    MapDatabase.MapLibrary.TryAdd(Fields[0], this);
                    MapDatabase.MapLibrary.TryAdd(Fields[1], this);
                    MapDatabase.MapLibrary.TryAdd(song["hashMd5"].Value.ToUpper(), this);
                    MapDatabase.LevelId.TryAdd(LevelId, this);
                }
                catch (Exception ex)
                {
                    Instance.QueueChatMessage(ex.ToString());
                }
            }
        }

        // Song primary key can be song ID, or level hashes. This dictionary is many:1
        public class MapDatabase
        {
            public static ConcurrentDictionary<string, SongMap> MapLibrary = new ConcurrentDictionary<string, SongMap>();
            public static ConcurrentDictionary<string, SongMap> LevelId = new ConcurrentDictionary<string, SongMap>();
            public static ConcurrentDictionary<string, HashSet<SongMap>> SearchDictionary = new ConcurrentDictionary<string, HashSet<SongMap>>();

            static public char [] wordseparator=new char[] {'&','/','-','[',']','(',')','.',' ','<','>',',','*'};

            static int tempid = 100000; // For now, we use these for local ID less songs

            static bool DatabaseImported = false;
            static bool DatabaseLoading = false;


            // Fast? Full Text Search
            public static List<SongMap> Search(string SearchKey)
            {
                if (!DatabaseImported && RequestBotConfig.Instance.LocalSearch )
                {
                    LoadCustomSongs();                  
                }

 
                List<SongMap> result = new List<SongMap>();
                List<HashSet<SongMap>> resultlist = new List<HashSet<SongMap>>();

                if (RequestBot.Instance.GetBeatSaverId(SearchKey) != "")
                {
                    SongMap song;
                    if (MapDatabase.MapLibrary.TryGetValue(SearchKey, out song))
                    {
                        result.Add(song);
                        return result;
                    }
                }

                string[] SearchParts = SearchKey.ToLower().Split(wordseparator, StringSplitOptions.RemoveEmptyEntries);

                foreach (var part in SearchParts)
                {
                    HashSet<SongMap> partresult;

                    string subhash = (part.Length > partialhash) ? part.Substring(0, partialhash) : part;  

                    if (!SearchDictionary.TryGetValue(subhash, out partresult)) return result; // Keyword must be found
                    resultlist.Add(partresult);
                }

                // We now have n lists of candidates

                resultlist.Sort((L1, L2) => L1.Count.CompareTo(L2.Count));

                // We now have an optimized query

                // Compute all matches
                foreach (var map in resultlist[0])
                {
                    for (int i = 1; i < resultlist.Count; i++)
                    {
                        if (!resultlist[i].Contains(map)) goto next; // We can't continue from here :(    
                    }

                foreach (var part in SearchParts) // This is costly
                    {
                        if (part.Length > partialhash && !map.isKeyPresent(part)) goto next;
                    }
                result.Add(map);

                next:
                    ;
                }

                return result;
            }



            public void RemoveMap(JSONObject song)
            {


            }
            public void AddDirectory()
            {

            }

            public void DownloadSongs()
            {

            }

            public void SaveDatabase()
            {

            }

           

            public static void ImportLoaderDatabase()
            {
                foreach (var level in SongLoader.CustomLevels)
                {
                 //   new SongMap(level.customSongInfo.path);
                }
            }


            public static async void LoadZIPDirectory(string folder = @"d:\beatsaver")
            {
                if (MapDatabase.DatabaseLoading) return;


                await Task.Run(() =>
                {

                    var di = new DirectoryInfo(folder);

                    foreach (FileInfo f in di.GetFiles("*.zip"))
                    {
                        
                    }

                });

                MapDatabase.DatabaseLoading = false;
            }


                    // Update Database from Directory
            public static async void LoadCustomSongs(string folder = "")
            {
                if (MapDatabase.DatabaseLoading) return;

                await Task.Run(() =>
                {

                    DatabaseLoading = true;

                    Instance.QueueChatMessage("Starting song indexing");
                    var StarTime = DateTime.UtcNow;

                    if (folder == "") folder = Path.Combine(Environment.CurrentDirectory, "customsongs");

                    List<FileInfo> files = new List<FileInfo>();  // List that will hold the files and subfiles in path
                    List<DirectoryInfo> folders = new List<DirectoryInfo>(); // List that hold direcotries that cannot be accessed

                    DirectoryInfo di = new DirectoryInfo(folder);
                    FullDirList(di, "*");

                    if (RequestBotConfig.Instance.additionalsongpath!="")
                    {
                        di = new DirectoryInfo(RequestBotConfig.Instance.additionalsongpath);
                        FullDirList(di, "*");
                    }

                    void FullDirList(DirectoryInfo dir, string searchPattern)
                    {
                        try
                        {
                            foreach (FileInfo f in dir.GetFiles(searchPattern))
                            {
                                if (f.FullName.EndsWith("info.json"))
                                    files.Add(f);
                            }
                        }
                        catch
                        {
                            Console.WriteLine("Directory {0}  \n could not be accessed!!!!", dir.FullName);
                            return;
                        }

                        foreach (DirectoryInfo d in dir.GetDirectories())
                        {
                            folders.Add(d);
                            FullDirList(d, searchPattern);
                        }
                    }

                    // This might need some optimization


                    Instance.QueueChatMessage($"Processing {files.Count} maps. ");
                    foreach (var item in files)
                    {

                        //msg.Add(item.FullName,", ");

                        string id = "", version = "0";

                        GetIdFromPath(item.DirectoryName, ref id, ref version);

                        try
                        {

                            JSONObject song = JSONObject.Parse(File.ReadAllText(item.FullName)).AsObject;

                            string hash;

                            JSONNode difficultylevels = song["difficultyLevels"].AsArray;
                            var FileAccumulator = new StringBuilder();
                            foreach (var level in difficultylevels)
                            {
                                //Instance.QueueChatMessage($"key={level.Key} value={level.Value}");
                                try
                                {
                                    FileAccumulator.Append(File.ReadAllText($"{item.DirectoryName}\\{level.Value["jsonPath"].Value}"));
                                }
                                catch
                                {
                                    //Instance.QueueChatMessage($"key={level.Key} value={level.Value}");
                                    //throw;
                                }
                            }

                            hash = Utils.CreateMD5FromString(FileAccumulator.ToString());

                            string levelId = string.Join("∎", hash, song["songName"].Value, song["songSubName"].Value, song["authorName"], song["beatsPerMinute"].AsFloat.ToString()) + "∎";

                            if (LevelId.ContainsKey(levelId))
                            {
                                LevelId[levelId].path = item.DirectoryName;
                                continue;
                            }

                            song.Add("id", id);
                            song.Add("version", version);
                            song.Add("hashMd5", hash);

                            new SongMap(song, levelId, item.DirectoryName);
                        }
                        catch (Exception e)
                        {
                            Instance.QueueChatMessage($"Failed to process {item}.");
                        }

                    }
                    var duration = DateTime.UtcNow - StarTime;
                    Instance.QueueChatMessage($"Song indexing done. ({duration.TotalSeconds} secs.");

                    DatabaseImported = true;
                    DatabaseLoading = false;
                });
            }

            static bool GetIdFromPath(string path, ref string id, ref string version)
            {
                string[] parts = path.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);

                id = "";
                version = "0";

                foreach (var part in parts)
                {
                    id = RequestBot.Instance.GetBeatSaverId(part);
                    if (id != "")
                    {
                        version = part;
                        return true;
                    }
                }

                id = tempid++.ToString();
                version = $"{id}-0";
                return false;
            }


        }


        public static bool CreateMD5FromFile(string path, out string hash)
        {
            hash = "";
            if (!File.Exists(path)) return false;
            using (MD5 md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(path))
                {
                    byte[] hashBytes = md5.ComputeHash(stream);

                    StringBuilder sb = new StringBuilder();
                    foreach (byte hashByte in hashBytes)
                    {
                        sb.Append(hashByte.ToString("X2"));
                    }

                    hash = sb.ToString();
                    return true;
                }
            }
        }


        #if UNRELEASED
        // BUG: Not production ready, will probably never be released. Takes hours, and puts a load on beatsaver.com. DO NOT USE
        public IEnumerator DownloadEverything(ParseState state)
        {
        Instance.QueueChatMessage("Starting Beatsaver scan");
        var StarTime = DateTime.UtcNow;
 
        
        int totalSongs = 0;

        string requestUrl = "https://beatsaver.com/api/songs/new";

        int offset = 0;
        while (true) // MaxiumAddScanRange
        {
       
            using (var web = UnityWebRequest.Get($"{requestUrl}/{offset}"))
            {
                yield return web.SendWebRequest();
                if (web.isNetworkError || web.isHttpError)
                {
                   break;
                }



                    JSONNode result = JSON.Parse(web.downloadHandler.text);

                    if (result == null || result["songs"].Count==0) break;

                    foreach (JSONObject entry in result["songs"].AsArray)
                {
                    JSONObject song = entry;

                        //Instance.QueueChatMessage(entry.ToString().Substring(350));

                        new SongMap(song);
                        totalSongs++;

                

                        if ((totalSongs & 127) == 0) Instance.QueueChatMessage($"Processed {totalSongs}");
                    //QueueSong(state, song);
                }
        }
            offset += 20; // Magic beatsaver.com skip constant.
        }

            var duration = DateTime.UtcNow - StarTime;
            Instance.QueueChatMessage($"BeatSaver Database Scan done. ({duration.TotalSeconds} secs.");

            
            StarTime = DateTime.UtcNow;

            Instance.QueueChatMessage("Starting Full Download");
            var msg = new QueueLongMessage(9999);
            Instance.QueueChatMessage($"Attempting to download up to {MapDatabase.LevelId.Count} files.");
            foreach (var song in MapDatabase.LevelId)
                {
                if (song.Value.path == "")
                {
                    string localPath = $"d:\\beatsaver\\{song.Value.Fields[1]}.zip";
                    if (File.Exists(localPath)) continue;
                    msg.Add($"{song.Value.song["id"].Value}", ",");

                    yield return Utilities.DownloadFile(song.Value.song["downloadUrl"].Value, localPath);
                }
            }

            msg.end();
            duration = DateTime.UtcNow - StarTime;
            Instance.QueueChatMessage($"BeatSaver Download Done. ({duration.TotalSeconds} secs.");

            yield return null;           
        }

        #endif

        private List<JSONObject> GetSongListFromResults(JSONNode result,string SearchString, ref string errorMessage, SongFilter filter = SongFilter.All, string sortby = "-rating", int reverse = 1)
        {
            List<JSONObject> songs = new List<JSONObject>();


            if (result != null)
            {
                // Add query results to out song database.
                if (result["songs"].IsArray)
                {
                     foreach (JSONObject currentSong in result["songs"].AsArray)
                    {
                        new SongMap(currentSong);
                    }
                }
                else
                {
                    new SongMap(result["song"].AsObject);
                }
            }

            var list = MapDatabase.Search(SearchString);
    
            try
            {
                string[] sortorder = sortby.Split(' ');

                list.Sort(delegate (SongMap c1, SongMap c2)
                {
                    return reverse * CompareSong(c1.song, c2.song, ref sortorder);
                });
            }
            catch (Exception e)
            {
                //QueueChatMessage($"Exception {e} sorting song list");
                Plugin.Log($"Exception sorting a returned song list. {e.ToString()}");
            }

            foreach (var song in list)
                {
                errorMessage = SongSearchFilter(song.song, false, filter);
                if (errorMessage == "") songs.Add(song.song);
                }

            return songs;
        }


        /*
		public static string CreateMD5FromString(string input)
		{
			// Use input string to calculate MD5 hash
			using (var md5 = MD5.Create())
			{
				var inputBytes = Encoding.ASCII.GetBytes(input);
				var hashBytes = md5.ComputeHash(inputBytes);

				// Convert the byte array to hexadecimal string
				var sb = new StringBuilder();
				for (int i = 0; i < hashBytes.Length; i++)
				{
					sb.Append(hashBytes[i].ToString("X2"));
				}
				return sb.ToString();
			}
		}
 
         public string GetIdentifier()
         {
             var combinedJson = "";
             foreach (var diffLevel in difficultyLevels)
             {
                 if (!File.Exists(path + "/" + diffLevel.jsonPath))
                 {
                     continue;
                 }

                 diffLevel.json = File.ReadAllText(path + "/" + diffLevel.jsonPath);
                 combinedJson += diffLevel.json;
             }

             var hash = Utils.CreateMD5FromString(combinedJson);
             levelId = hash + "∎" + string.Join("∎", songName, songSubName, GetSongAuthor(), beatsPerMinute.ToString()) + "∎";
             return levelId;
         }

         public static string GetLevelID(Song song)
         {
             string[] values = new string[] { song.hash, song.songName, song.songSubName, song.authorName, song.beatsPerMinute };
             return string.Join("∎", values) + "∎";
         }

         public static BeatmapLevelSO GetLevel(string levelId)
         {
             return SongLoader.CustomLevelCollectionSO.beatmapLevels.FirstOrDefault(x => x.levelID == levelId) as BeatmapLevelSO;
         }

         public static bool CreateMD5FromFile(string path, out string hash)
         {
             hash = "";
             if (!File.Exists(path)) return false;
             using (MD5 md5 = MD5.Create())
             {
                 using (var stream = File.OpenRead(path))
                 {
                     byte[] hashBytes = md5.ComputeHash(stream);

                     StringBuilder sb = new StringBuilder();
                     foreach (byte hashByte in hashBytes)
                     {
                         sb.Append(hashByte.ToString("X2"));
                     }

                     hash = sb.ToString();
                     return true;
                 }
             }
         }

         public void RequestSongByLevelID(string levelId, Action<Song> callback)
         {
             StartCoroutine(RequestSongByLevelIDCoroutine(levelId, callback));
         }
         */

    }
}