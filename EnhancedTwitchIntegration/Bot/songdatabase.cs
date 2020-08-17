using System;
using System.Runtime;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using ChatCore.SimpleJSON;
// Feature requests: Add Reason for being banned to banlist
//  

namespace SongRequestManager
{
    public partial class RequestBot : MonoBehaviour
    {
        enum MapField { id, version, songName, songSubName, authorName, rating, hashMd5, hashSha1 };

        const int partialhash = 3; // Do Not ever set this below 4. It will cause severe performance loss

        public class SongMap
        {
            public JSONObject song;
            public string path;
            public string LevelId;
            public float pp = 0;

            public static int hashcount = 0;

            void IndexFields(bool Add,int id,params string[] parameters)
                {
                    foreach (var field in parameters)
                    {
                        string[] parts = normalize.Split(field);
                        foreach (var part in parts)
                        {
                            if (part.Length < partialhash) UpdateSearchEntry(part,id, Add);
                            for (int i = partialhash; i <= part.Length; i++)
                            {
                                UpdateSearchEntry(part.Substring(0, i), id,Add);
                            }
                        }
                    }
                }

            void UpdateSearchEntry(string key, int id,bool Add = true)
            {

            if (Add) hashcount++; else hashcount--;

            if (Add)
                MapDatabase.SearchDictionary.AddOrUpdate(key, (k) => { HashSet<int> va = new HashSet<int>(); va.Add(id); return va; }, (k, va) => { va.Add(id); return va; });
            else
            {
                MapDatabase.SearchDictionary[key].Remove(id); // An empty keyword is fine, and actually uncommon
            }

            }

            public SongMap(string id, string version, string songName, string songSubName, string authorName, string duration, string rating)
            {
                //JSONObject song = new JSONObject();

                //IndexSong(song);
            }


            public SongMap(JSONObject song, string LevelId = "", string path = "")
            {
 
                if (!song["version"].IsString)
                    {
                    //RequestBot.Instance.QueueChatMessage($"{song["key"].Value}: {song["metadata"]}");
                    song.Add("id", song["key"]);
                    song.Add("version", song["key"]);

                    var metadata = song["metadata"];
                    song.Add("songName", metadata["songName"].Value);
                    song.Add("songSubName", metadata["songSubName"].Value);
                    song.Add("authorName", metadata["songAuthorName"].Value);
                    song.Add("levelAuthor", metadata["levelAuthorName"].Value);
                    song.Add("rating", song["stats"]["rating"].AsFloat*100);

                    bool degrees90 = false;
                    bool degrees360 = false;

                    try
                    {

                        var characteristics = metadata["characteristics"][0]["difficulties"];

                        //Instance.QueueChatMessage($"{characteristics}");

                        foreach (var entry in metadata["characteristics"])
                        {
                            if (entry.Value["name"] == "360Degree") degrees360 = true;
                            if (entry.Value["name"] == "90Degree") degrees90 = true;
                        }

                        int maxnjs = 0;
                        foreach (var entry in characteristics)
                        {
                            if (entry.Value.IsNull) continue;
                            var diff = entry.Value["length"].AsInt;
                            var njs = entry.Value["njs"].AsInt;
                            if (njs > maxnjs) maxnjs = njs;



                            if (diff > 0)
                            {
                                song.Add("songlength", $"{diff / 60}:{diff % 60:00}");
                                song.Add("songduration", diff);
                                //Instance.QueueChatMessage($"{diff / 60}:{diff % 60}");
                            }
                        }

                        if (maxnjs>0)
                        {
                            song.Add("njs", maxnjs);
                        }
                        if (degrees360 || degrees90) song.Add("maptype", "360");
                    }
                    catch
                    {
                    }

                }


                float songpp = 0;
                if (ppmap.TryGetValue(song["id"].Value,out songpp))
                    {
                    song.Add("pp", songpp);
                    }

                //SongMap oldmap;
                //if (MapDatabase.MapLibrary.TryGetValue(song["id"].Value,out oldmap))
                //{
                
                //    if (LevelId == oldmap.LevelId && song["version"].Value == oldmap.song["version"].Value)
                //    {
                //        oldmap.song = song;
                //        return;
                //    }

                //    int id = int.Parse(song["id"].Value.ToUpper(), System.Globalization.NumberStyles.HexNumber);

                //    oldmap.UnIndexSong(id);                    
                //}

                this.path = path;
                //this.LevelId = LevelId;
                IndexSong(song);
            }

            void UnIndexSong(int id)
            {
                SongMap temp;
                string indexpp = (song["pp"].AsFloat > 0) ? "PP" : "";

                IndexFields(false,id, song["songName"].Value, song["songSubName"].Value, song["authorName"].Value , song["levelAuthor"].Value, indexpp,song["maptype"].Value);

                MapDatabase.MapLibrary.TryRemove(song["id"].Value, out temp);
                MapDatabase.MapLibrary.TryRemove(song["version"].Value, out temp);
                //MapDatabase.LevelId.TryRemove(LevelId, out temp);
            }

            public void IndexSong(JSONObject song)
            {
                try
                {
                    this.song = song;
                    string indexpp = (song["pp"].AsFloat > 0) ? "PP" : "";

                    int id=int.Parse(song["id"].Value.ToUpper(), System.Globalization.NumberStyles.HexNumber);

                    //Instance.QueueChatMessage($"id={song["id"].Value} = {id}");

                    IndexFields(true,id, song["songName"].Value, song["songSubName"].Value, song["authorName"].Value,song ["levelAuthor"],indexpp, song["maptype"].Value);

                    MapDatabase.MapLibrary.TryAdd(song["id"].Value, this);
                    MapDatabase.MapLibrary.TryAdd(song["version"].Value, this);
                    //MapDatabase.LevelId.TryAdd(LevelId, this);
                }
                catch (Exception ex)
                {
                    Instance.QueueChatMessage(ex.ToString());
                }
            }
        }

           

        // Song primary key can be song ID/version , or level hashes. This dictionary is many:1
        public class MapDatabase
        {
            public static ConcurrentDictionary<string, SongMap> MapLibrary = new ConcurrentDictionary<string, SongMap>();
            public static ConcurrentDictionary<string, SongMap> LevelId = new ConcurrentDictionary<string, SongMap>();
            public static ConcurrentDictionary<string, HashSet<int>> SearchDictionary = new ConcurrentDictionary<string, HashSet<int>>();

            static int tempid = 100000; // For now, we use these for local ID less songs

            static bool DatabaseImported = false;
            public static bool DatabaseLoading = false;


            // Fast? Full Text Search
            public static List<SongMap> Search(string SearchKey)
            {
                if (!DatabaseImported && RequestBotConfig.Instance.LocalSearch )
                {
                    LoadCustomSongs();                  
                }

                List<SongMap> result = new List<SongMap>();

                if (RequestBot.Instance.GetBeatSaverId(SearchKey) != "")
                {
                    SongMap song;
                    if (MapDatabase.MapLibrary.TryGetValue(normalize.RemoveSymbols(ref SearchKey,normalize._SymbolsNoDash), out song))
                    {
                        result.Add(song);
                        return result;
                    }
                }

                List<HashSet<int>> resultlist = new List<HashSet<int>>();

                string[] SearchParts = normalize.Split(SearchKey);

                foreach (var part in SearchParts)
                {
                     HashSet<int>  idset;

                    if (!SearchDictionary.TryGetValue(part, out idset)) return result; // Keyword must be found
                    resultlist.Add(idset);
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


                    try
                    {
                        result.Add(MapDatabase.MapLibrary[map.ToString("x")]);
                    }
                catch
                    {
                        RequestBot.Instance.QueueChatMessage($"map fail = {map}");
                    }

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

            public static  void SaveDatabase()
            {
            try
            {
                DateTime start = DateTime.Now;
                //JSONArray arr = new JSONArray();
                JSONObject arr = new JSONObject();
                foreach (var entry in MapLibrary)
                arr.Add(entry.Value.song["id"],entry.Value.song);
                File.WriteAllText(Path.Combine(Plugin.DataPath, "SongDatabase.dat"), arr.ToString());
                Instance.QueueChatMessage($"Saved Song Databse in  {(DateTime.Now - start).Seconds} secs.");
            }
            catch (Exception ex)
            {
                Plugin.Log(ex.ToString());
            }

            }

            public static void LoadDatabase()
            {


                try
                {

                    DateTime start = DateTime.Now;
                    string path = Path.Combine(Plugin.DataPath, "SongDatabase.dat");

                    if (File.Exists(path))
                    {
   
                        JSONNode json = JSON.Parse(File.ReadAllText(path));
                        if (!json.IsNull)
                        {
                            

                            int Count = json.Count;

                            //foreach (JSONObject j in json.AsArray)
                            //{                                    
                            //    new SongMap(j);
                            //}


                            foreach (KeyValuePair<string, JSONNode> kvp in json)
                            {
                                new SongMap((JSONObject) kvp.Value);
                            }


                            json = 0; // BUG: This doesn't actually help. The problem is that the json object is still being referenced.

                            Instance.QueueChatMessage($"Finished reading {Count} in {(DateTime.Now - start).Seconds} secs.");
                        }
                }
                }
            catch (Exception ex)
                {
                    Plugin.Log(ex.ToString());
                    Instance.QueueChatMessage($"{ex.ToString()}");
                }


            }

 
            public static void ImportLoaderDatabase()
            {
                //foreach (var level in SongLoader.CustomLevels)
                {
                    //new SongMap(level.customSongInfo.path);
                }
            }

            public static string readzipjson(ZipArchive archive,string filename="info.json")
                {
                var info = archive.Entries.First<ZipArchiveEntry>(e => (e.Name.EndsWith(filename)));
                if (info == null) return "";

                StreamReader reader = new StreamReader(info.Open());
                string result=reader.ReadToEnd();
                reader.Close();
                return result;
            }


            // Early code... index a full zip archive.
            public static async void LoadZIPDirectory(string folder = @"d:\beatsaver")
            {
                if (MapDatabase.DatabaseLoading) return;

                await Task.Run(() =>
                {

                    var startingmem = GC.GetTotalMemory(true);

                    Instance.QueueChatMessage($"Starting to read archive.");
                    int addcount = 0;
                    var StarTime = DateTime.Now;

                    var di = new DirectoryInfo(folder);

                    foreach (FileInfo f in di.GetFiles("*.zip"))
                    {
 
                        try
                        {
                            var x = System.IO.Compression.ZipFile.OpenRead(f.FullName);
                            var info = x.Entries.First<ZipArchiveEntry>(e => (e.Name.EndsWith("info.json")));

                             string id = "";
                            string version = "";
                            GetIdFromPath(f.Name, ref id, ref version);

                            if (MapDatabase.MapLibrary.ContainsKey(id))
                                {
                                if (MapLibrary[id].path!="") MapLibrary[id].path = f.FullName;
                                continue;
                                }

                           JSONObject song = JSONObject.Parse(readzipjson(x)).AsObject;

                            string hash;

                            JSONNode difficultylevels = song["difficultyLevels"].AsArray;
                            var FileAccumulator = new StringBuilder();
                            foreach (var level in difficultylevels)
                            {
                                try
                                {
                                    FileAccumulator.Append(readzipjson(x,level.Value));
                                }
                                catch
                                {
                                    //Instance.QueueChatMessage($"key={level.Key} value={level.Value}");
                                    //throw;
                                }
                            }

                            hash = CreateMD5FromString(FileAccumulator.ToString());

                            string levelId = string.Join("∎", hash, song["songName"].Value, song["songSubName"].Value, song["authorName"], song["beatsPerMinute"].AsFloat.ToString()) + "∎";

                            if (LevelId.ContainsKey(levelId))
                            {

                                LevelId[levelId].path = f.FullName;
                                continue;
                            }

                            addcount++;

                            song.Add("id", id);
                            song.Add("version", version);
                            song.Add("hashMd5", hash);

                            new SongMap(song, levelId, f.FullName);

                            x = null;

                        }
                        catch (Exception)
                        {
                            Instance.QueueChatMessage($"Failed to process {f.FullName}");   
                            //Instance.QueueChatMessage(ex.ToString());
                        }

 
                    }
                    Instance.QueueChatMessage($"Archive indexing done, {addcount} files added. ({(DateTime.Now-StarTime).TotalSeconds} secs.");
                    GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                    GC.Collect();
                    Instance.QueueChatMessage($"hashentries: {SongMap.hashcount} memory: {(GC.GetTotalMemory(false) - startingmem) / 1048576} MB");


                });

 
                MapDatabase.DatabaseLoading = false;
            }


            // Update Database from Directory
            public static async Task LoadCustomSongs(string folder = "",string songid="")
            {
                if (MapDatabase.DatabaseLoading) return;

                DatabaseLoading = true;

                await Task.Run(() =>
                {

                    if (songid=="") Instance.QueueChatMessage($"Starting song indexing {folder}");

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
                            if (MapDatabase.MapLibrary.ContainsKey(id)) continue;

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

                            hash = CreateMD5FromString(FileAccumulator.ToString());

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
                        catch (Exception)
                        {
                            Instance.QueueChatMessage($"Failed to process {item}.");
                        }

                    }
                    var duration = DateTime.UtcNow - StarTime;
                    if (songid=="") Instance.QueueChatMessage($"Song indexing done. ({duration.TotalSeconds} secs.");

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


     



        private List<JSONObject> GetSongListFromResults(JSONNode result,string SearchString, ref string errorMessage, SongFilter filter = SongFilter.All, string sortby = "-rating", int reverse = 1)
        {
            List<JSONObject> songs = new List<JSONObject>();

            if (result != null)
            {
                // Add query results to out song database.
                if (result["docs"].IsArray)    
                {
                    var downloadedsongs = result["docs"].AsArray;
                    for (int i = 0; i < downloadedsongs.Count; i++) new SongMap(downloadedsongs[i].AsObject);
                        
                    foreach (JSONObject currentSong in result["docs"].AsArray)
                    {
                        new SongMap(currentSong);
                    }
                }
                else
                {
                    new SongMap(result.AsObject);
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

        public IEnumerator RefreshSongs(ParseState state)
        {
           
            MapDatabase.LoadCustomSongs();

            yield break;
        }

        public string GetGCCount(ParseState state)
        {

            state.msg($"Gc0:{GC.CollectionCount(0)} GC1:{GC.CollectionCount(1)} GC2:{GC.CollectionCount(2)}");
            state.msg($"{GC.GetTotalMemory(false)}");
            return success;     
        }


        public IEnumerator ReadArchive(ParseState state)
        {

            MapDatabase.LoadZIPDirectory();
            yield break;
        }

        public IEnumerator SaveSongDatabase(ParseState state)
        {
            MapDatabase.SaveDatabase();
            yield break;
        }


        /*

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

         // Beatsaver.com filtered characters
         '@', '*', '+', '-', '<', '~', '>', '(', ')'



         */

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


        //SongLoader.Instance.RemoveSongWithLevelID(level.levelID);
        //SongLoader.CustomLevelCollectionSO.beatmapLevels.FirstOrDefault(x => x.levelID == levelId) as BeatmapLevelSO;

        public static ConcurrentDictionary<String, float> ppmap = new ConcurrentDictionary<string, float>();
        public static bool pploading = false;

        public async Task GetPPData()
        {
            if (pploading) return;

            pploading = true;

            //Instance.QueueChatMessage("Getting PP Data");
            var StarTime = DateTime.UtcNow;
            string requestUrl = "https://cdn.wes.cloud/beatstar/bssb/v2-ranked.json";
            //public const String SCRAPED_SCORE_SABER_ALL_JSON_URL = "https://cdn.wes.cloud/beatstar/bssb/v2-all.json";

            string result;

            System.Globalization.NumberStyles style = System.Globalization.NumberStyles.AllowDecimalPoint;

            var resp = await Plugin.WebClient.GetAsync(requestUrl, System.Threading.CancellationToken.None);

            if (resp.IsSuccessStatusCode)
            {
                result = resp.ContentToString();
            }
            else
            {
                Plugin.Log("Failed to get pp");
                pploading = false;
                return;
            }

            //Instance.QueueChatMessage($"Parsing PP Data {result.Length}");

            JSONNode rootNode = JSON.Parse(result);

            listcollection.ClearList("pp.deck");

            foreach (KeyValuePair<string, JSONNode> kvp in rootNode)
            {
                JSONNode difficultyNodes = kvp.Value;

                string id = "";
                float maxpp = 0;
                float maxstar = 0;

                //Instance.QueueChatMessage($"{kvp.Value}");

                id = difficultyNodes["key"];

                //Instance.QueueChatMessage($"{id}");

                foreach (var innerKvp in difficultyNodes["diffs"])
                {
                    JSONNode node = innerKvp.Value;

                    //Instance.QueueChatMessage($"{node}");

                    float.TryParse(node["pp"], style, System.Globalization.CultureInfo.InvariantCulture, out float pp);
                    if (pp > maxpp) maxpp = pp;

                    float.TryParse(node["star"], style, System.Globalization.CultureInfo.InvariantCulture, out float starDifficulty);
                    if (starDifficulty > maxstar) maxstar = starDifficulty;
                }

                if (maxpp > 0)
                {
                    //Instance.QueueChatMessage($"{id} = {maxpp}");

                    ppmap.TryAdd(id, (int)(maxpp));

                    if (id != "" && maxpp > 200) listcollection.add("pp.deck", id);

                    if (MapDatabase.MapLibrary.TryGetValue(id, out SongMap map))
                    {
                        map.pp = (int)(maxpp);
                        map.song.Add("pp", maxpp);
                        map.IndexSong(map.song);

                    }
                }
            }

            COMMAND.Parse(ChatHandler.Self, "!deck pp", RequestBot.CmdFlags.Local);
            Instance.QueueChatMessage("PP Data indexed");
            pploading = false;
        }

    }
}