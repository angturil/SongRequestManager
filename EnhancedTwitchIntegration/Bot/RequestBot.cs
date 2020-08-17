using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;

#if OLDVERSION
using TMPro;
#endif

using UnityEngine;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;
using SongCore;
using IPA.Utilities;
using SongRequestManager.UI;
using BeatSaberMarkupLanguage;
using System.Threading.Tasks;
using System.IO.Compression;
using ChatCore.Models.Twitch;
using ChatCore.SimpleJSON;

namespace SongRequestManager
{
    public partial class RequestBot : MonoBehaviour
    {
        [Flags]
        public enum RequestStatus
        {
            Invalid,
            Queued,
            Blacklisted,
            Skipped,
            Played,
            Wrongsong,
            SongSearch,
        }

        public static RequestBot Instance;
        public static ConcurrentQueue<RequestInfo> UnverifiedRequestQueue = new ConcurrentQueue<RequestInfo>();
        public static Dictionary<string, RequestUserTracker> RequestTracker = new Dictionary<string, RequestUserTracker>();

        //SpeechSynthesizer synthesizer = new SpeechSynthesizer();
        //synthesizer.Volume = 100;  // 0...100
          //  synthesizer.Rate = -2;     // -10...10

        private static Button _requestButton;
        public static bool _refreshQueue = false;

        private static Queue<string> _botMessageQueue = new Queue<string>();

        bool _mapperWhitelist = false; // BUG: Need to clean these up a bit.
        bool _configChanged = false;

        private static System.Random generator = new System.Random(); // BUG: Should at least seed from unity?

        public static List<JSONObject> played = new List<JSONObject>(); // Played list

        private static StringListManager mapperwhitelist = new StringListManager(); // BUG: This needs to switch to list manager interface
        private static StringListManager mapperBanlist = new StringListManager(); // BUG: This needs to switch to list manager interface
        private static StringListManager Whitelist = new StringListManager();
        private static StringListManager BlockedUser = new StringListManager();

        private static string duplicatelist = "duplicate.list"; // BUG: Name of the list, needs to use a different interface for this.
        private static string banlist = "banlist.unique"; // BUG: Name of the list, needs to use a different interface for this.
        private static string _whitelist = "whitelist.unique"; // BUG: Name of the list, needs to use a different interface for this.
        private static string _blockeduser = "blockeduser.unique";

        private static Dictionary<string, string> songremap = new Dictionary<string, string>();
        public static Dictionary<string, string> deck = new Dictionary<string, string>(); // deck name/content

        private static RequestFlowCoordinator _flowCoordinator;

        public static string playedfilename = "";

        internal static void SRMButtonPressed()
        {
            var soloFlow = Resources.FindObjectsOfTypeAll<SoloFreePlayFlowCoordinator>().First();
            soloFlow.InvokeMethod<object, SoloFreePlayFlowCoordinator>("PresentFlowCoordinator", _flowCoordinator, null, false, false);
        }

        internal static void SetTitle(string title)
        {
            _flowCoordinator.SetTitle(title);
        }

        public static void OnLoad()
        {
            try
            {
                var _levelListViewController = Resources.FindObjectsOfTypeAll<LevelCollectionViewController>().First();
                if (_levelListViewController)
                    {
                    _requestButton = UIHelper.CreateUIButton(_levelListViewController.rectTransform, "OkButton", new Vector2(66, -3.5f),
                        new Vector2(9f, 5.5f), () => { _requestButton.interactable = false; SRMButtonPressed(); _requestButton.interactable = true; }, "SRM");

                    (_requestButton.transform as RectTransform).anchorMin = new Vector2(1, 1);
                    (_requestButton.transform as RectTransform).anchorMax = new Vector2(1, 1);

                    _requestButton.ToggleWordWrapping(false);
                    _requestButton.SetButtonTextSize(3.5f);
                    UIHelper.AddHintText(_requestButton.transform as RectTransform, "Manage the current request queue");

                    UpdateRequestUI();
                    Plugin.Log("Created request button!");
                }
            }
            catch
            {
                Plugin.Log("Unable to create request button");
            }

            // check if flow coordinator has been setup yet
            if (_flowCoordinator == null)
            {
                _flowCoordinator = BeatSaberMarkupLanguage.BeatSaberUI.CreateFlowCoordinator<RequestFlowCoordinator>();
            }

            SongListUtils.Initialize();

            ChatHandler.instance.Init();

            WriteQueueSummaryToFile();
            WriteQueueStatusToFile(QueueMessage(RequestBotConfig.Instance.RequestQueueOpen));

            if (Instance) return;
            new GameObject("SongRequestManager").AddComponent<RequestBot>();
        }

        public static bool AddKeyboard(KEYBOARD keyboard, string keyboardname, float scale = 0.5f)
        {
            try
            {
                string fileContent = File.ReadAllText(Path.Combine(Plugin.DataPath, keyboardname));
                if (fileContent.Length > 0) keyboard.AddKeys(fileContent, scale);
                return true;
            }
            catch
            {
                return false;
                // This is a silent fail since custom keyboards are optional
            }
        }

        public static void Newest(KEYBOARD.KEY key)
        {
            ClearSearches();
            RequestBot.COMMAND.Parse(ChatHandler.Self, $"!addnew/top",CmdFlags.Local);
        }

        public static void Search(KEYBOARD.KEY key)
        {
            if (key.kb.KeyboardText.text.StartsWith("!"))
            {
                key.kb.Enter(key);
            }
            ClearSearches();
            RequestBot.COMMAND.Parse(ChatHandler.Self, $"!addsongs/top {key.kb.KeyboardText.text}",CmdFlags.Local);
            key.kb.Clear(key);
        }

        public static void MSD(KEYBOARD.KEY key)
        {
            if (key.kb.KeyboardText.text.StartsWith("!"))
            {
                key.kb.Enter(key);
            }
            ClearSearches();
            RequestBot.COMMAND.Parse(ChatHandler.Self, $"!makesearchdeck {key.kb.KeyboardText.text}", CmdFlags.Local);
            key.kb.Clear(key);
        }

        public static void UnfilteredSearch(KEYBOARD.KEY key)
        {
            if (key.kb.KeyboardText.text.StartsWith("!"))
            {
                key.kb.Enter(key);
            }
            ClearSearches();
            RequestBot.COMMAND.Parse(ChatHandler.Self, $"!addsongs/top/mod {key.kb.KeyboardText.text}",CmdFlags.Local);
            key.kb.Clear(key);
        }

        public static void ClearSearches()
        {
            for (int i = 0; i < RequestQueue.Songs.Count; i++)
            {
                var entry = RequestQueue.Songs[i];
                if (entry.status == RequestBot.RequestStatus.SongSearch)
                {
                    RequestBot.DequeueRequest(i, false);
                    i--;
                }
            }
        }

        public static void ClearSearch(KEYBOARD.KEY key)
        {
            ClearSearches();

            RequestBot.UpdateRequestUI();
            RequestBot._refreshQueue = true;
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Instance = this;

            #if UNRELEASED
            var startingmem = GC.GetTotalMemory(true);

            //var folder = Path.Combine(Environment.CurrentDirectory, "userdata","streamcore");

            //List<FileInfo> files = new List<FileInfo>();  // List that will hold the files and subfiles in path
            //List<DirectoryInfo> folders = new List<DirectoryInfo>(); // List that hold direcotries that cannot be accessed

            //DirectoryInfo di = new DirectoryInfo(folder);

            //Dictionary<string, string> remap = new Dictionary<string, string>();

            //foreach (var entry in listcollection.OpenList("all.list").list) 
            //    {
            //    //Instance.QueueChatMessage($"Map {entry}");

            //    string[] remapparts = entry.Split('-');
            //    if (remapparts.Length == 2)
            //    {
            //        int o;
            //        if (Int32.TryParse(remapparts[1], out o))
            //        {
            //            try
            //            {
            //                remap.Add(remapparts[0], o.ToString("x"));
            //            }
            //            catch
            //            { }
            //            //Instance.QueueChatMessage($"Map {remapparts[0]} : {o.ToString("x")}");
            //        }
            //    }
            //}

            //Instance.QueueChatMessage($"Scanning lists");

            //FullDirList(di, "*.deck");
            //void FullDirList(DirectoryInfo dir, string searchPattern)
            //{
            //    try
            //    {
            //        foreach (FileInfo f in dir.GetFiles(searchPattern))
            //        {
            //            var List = listcollection.OpenList(f.Name).list;
            //            for (int i=0;i<List.Count;i++)
            //                {
            //                if (remap.ContainsKey(List[i]))
            //                {
            //                    //Instance.QueueChatMessage($"{List[i]} : {remap[List[i]]}");
            //                    List[i] = remap[List[i]];
            //                }    
            //                }
            //            listcollection.OpenList(f.Name).Writefile(f.Name);
            //        }
            //    }
            //    catch
            //    {
            //        Console.WriteLine("Directory {0}  \n could not be accessed!!!!", dir.FullName);
            //        return;
            //    }
            //}

            //NOTJSON.UNITTEST();
#endif

            playedfilename = Path.Combine(Plugin.DataPath, "played.dat"); // Record of all the songs played in the current session

            try
            {
                string filesToDelete = Path.Combine(Environment.CurrentDirectory, "FilesToDelete");
                if (Directory.Exists(filesToDelete))
                    EmptyDirectory(filesToDelete);


                try
                {
                    DateTime LastBackup;
                    if (!DateTime.TryParse(RequestBotConfig.Instance.LastBackup,out LastBackup)) LastBackup=DateTime.MinValue;
                    TimeSpan TimeSinceBackup = DateTime.Now - LastBackup;
                    if (TimeSinceBackup > TimeSpan.FromHours(RequestBotConfig.Instance.SessionResetAfterXHours))
                    {
                        Backup();
                    }
                }
                catch(Exception ex)
                {
                    Plugin.Log(ex.ToString());
                    Instance.QueueChatMessage("Failed to run Backup");

                }

                try
                {
                    TimeSpan PlayedAge = GetFileAgeDifference(playedfilename);
                if (PlayedAge < TimeSpan.FromHours(RequestBotConfig.Instance.SessionResetAfterXHours)) played = ReadJSON(playedfilename); // Read the songsplayed file if less than x hours have passed 
                }
                catch (Exception ex)
                {
                    Plugin.Log(ex.ToString());
                    Instance.QueueChatMessage("Failed to clear played file");

                }

                if (RequestBotConfig.Instance.PPSearch) GetPPData(); // Start loading PP data

                MapDatabase.LoadDatabase();

                if (RequestBotConfig.Instance.LocalSearch) MapDatabase.LoadCustomSongs(); // This is a background process

                RequestQueue.Read(); // Might added the timespan check for this too. To be decided later.
                RequestHistory.Read();
                listcollection.OpenList("banlist.unique");

#if UNRELEASED
                //GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                //GC.Collect();
                //Instance.QueueChatMessage($"hashentries: {SongMap.hashcount} memory: {(GC.GetTotalMemory(false) - startingmem) / 1048576} MB");
#endif

                listcollection.ClearOldList("duplicate.list", TimeSpan.FromHours(RequestBotConfig.Instance.SessionResetAfterXHours));

                UpdateRequestUI();
                InitializeCommands();

                //EnhancedStreamChat.ChatHandler.ChatMessageFilters += MyChatMessageHandler; // TODO: Reimplement this filter maybe? Or maybe we put it directly into EnhancedStreamChat


                COMMAND.CommandConfiguration();
                RunStartupScripts();


                ProcessRequestQueue();

                RequestBotConfig.Instance.ConfigChangedEvent += OnConfigChangedEvent;
            }
            catch (Exception ex)
            {
            Plugin.Log(ex.ToString());
            Instance.QueueChatMessage(ex.ToString());
            }
        }

        //public bool MyChatMessageHandler(TwitchMessage msg)
        //{
        //    string excludefilename = "chatexclude.users";
        //    return RequestBot.Instance && RequestBot.listcollection.contains(ref excludefilename, msg.user.displayName.ToLower(), RequestBot.ListFlags.Uncached);
        //}

        private void OnConfigChangedEvent(RequestBotConfig config)
        {
            _configChanged = true;
        }

        private void OnConfigChanged()
        {
            UpdateRequestUI();

            if (RequestBotListViewController.Instance.isActivated)
            {
                RequestBotListViewController.Instance.UpdateRequestUI(true);
                RequestBotListViewController.Instance.SetUIInteractivity();
            }

            _configChanged = false;
        }
        
        // BUG: Prototype code, used for testing.
        class BotEvent
        {
            public static List<BotEvent> events = new List<BotEvent>();

            public DateTime time;
            public string command;
            public bool repeat;
            Timer timeq;

            public static void Clear()
            {                
                foreach (var ev in events) ev.timeq.Stop();
            }
            public BotEvent(DateTime time,string command,bool repeat)
            {
                this.time = time;
                this.command = command;
                this.repeat = repeat;
                timeq = new System.Timers.Timer(1000);
                timeq.Elapsed += (s, args) => ScheduledCommand(command, args);
                timeq.AutoReset = true;
                timeq.Enabled = true;
            }

            public BotEvent(TimeSpan delta, string command, bool repeat=false)
            {
                this.command = command;
                this.repeat = repeat;
                timeq = new System.Timers.Timer(delta.TotalMilliseconds);
                timeq.Elapsed += (s, args) => ScheduledCommand(command, args);
                timeq.AutoReset = repeat;

                events.Add(this);

                timeq.Enabled = true;
            }
        }

        public static void ScheduledCommand(string command, System.Timers.ElapsedEventArgs e)
        {
            COMMAND.Parse(ChatHandler.Self, command);
        }

        private void RunStartupScripts()
        {
            ReadRemapList(); // BUG: This should use list manager

            MapperBanList(ChatHandler.Self, "mapperban.list");
            WhiteList(ChatHandler.Self, "whitelist.unique");
            BlockedUserList(ChatHandler.Self, "blockeduser.unique");
            accesslist("whitelist.unique");
            accesslist("blockeduser.unique");
            accesslist("mapperban.list");

#if UNRELEASED
            OpenList(ChatHandler.Self, "mapper.list"); // Open mapper list so we can get new songs filtered by our favorite mappers.
            MapperAllowList(ChatHandler.Self, "mapper.list");
            accesslist("mapper.list");

            loaddecks(ChatHandler.Self, ""); // Load our default deck collection
            // BUG: Command failure observed once, no permission to use /chatcommand. Possible cause: OurTwitchUser isn't authenticated yet.

            RunScript(ChatHandler.Self, "startup.script"); // Run startup script. This can include any bot commands.
#endif
        }

        private void FixedUpdate()
        {
            if (_configChanged)
                OnConfigChanged();

            //if (_botMessageQueue.Count > 0)
              //  SendChatMessage(_botMessageQueue.Dequeue());

            if (_refreshQueue)
            {
                if (RequestBotListViewController.Instance.isActivated)
                {
                    RequestBotListViewController.Instance.UpdateRequestUI(true);
                    RequestBotListViewController.Instance.SetUIInteractivity();
                }
                _refreshQueue = false;
            }
        }

// if (!silence) QueueChatMessage($"{request.Key.song["songName"].Value}/{request.Key.song["authorName"].Value} ({songId}) added to the blacklist.");
        private void SendChatMessage(string message)
        {
            try
            {
                Plugin.Log($"Sending message: \"{message}\"");
                //TwitchWebSocketClient.SendMessage($"PRIVMSG #{TwitchLoginConfig.Instance.TwitchChannelName} :{message}");
                ChatHandler.SendCommand(message);
                //TwitchMessage tmpMessage = new TwitchMessage();
                //tmpMessage.Sender = ChatHandler.Self;
                //MessageParser.Parse(new ChatMessage(message, tmpMessage)); // This call is obsolete, when sending a message through TwitchWebSocketClient, the message should automatically appear in chat.
            }
            catch (Exception e)
            {
                Plugin.Log($"Exception was caught when trying to send bot message. {e.ToString()}");
            }
        }

        public void QueueChatMessage(string message)
        {
            if (ChatHandler.Connected)
            {
                ChatHandler.SendCommand($"{RequestBotConfig.Instance.BotPrefix}\uFEFF{message}");
            }
            //else
            //{
            //    Plugin.Log($"Message sent before twitch connected! \"{message}\"");
            //}
        }

        private async void ProcessRequestQueue()
        {
            while (!Plugin.Instance.IsApplicationExiting)
            {
                await Task.Run(async () => {
                    while (UnverifiedRequestQueue.Count == 0) await Task.Delay(25);
                });

                if (UnverifiedRequestQueue.TryDequeue(out var requestInfo))
                    await CheckRequest(requestInfo);
            }
        }

        int CompareSong(JSONObject song2, JSONObject song1, ref string [] sortorder)            
            {
            int result=0;

            foreach (string s in sortorder)
            {
                string sortby = s.Substring(1);
                switch (sortby)
                {
                    case "rating":
                    case "pp":

                        //QueueChatMessage($"{song2[sortby].AsFloat} < {song1[sortby].AsFloat}");
                        result = song2[sortby].AsFloat.CompareTo(song1[sortby].AsFloat);
                        break;

                    case "id":
                    case "version":
                        // BUG: This hack makes sorting by version and ID sort of work. In reality, we're comparing 1-2 numbers
                        result=GetBeatSaverId(song2[sortby].Value).PadLeft(6).CompareTo(GetBeatSaverId(song1[sortby].Value).PadLeft(6));
                        break;

                    default:
                        result= song2[sortby].Value.CompareTo(song1[sortby].Value);
                        break;
                }
                if (result == 0) continue;

                if (s[0] == '-') return -result;
                
                return result;
            }
            return result;
        }

        private async void UpdateSongMap(JSONObject song)
        {
            var resp = await Plugin.WebClient.GetAsync($"https://beatsaver.com/api/maps/detail/{song["id"].Value.ToString()}", System.Threading.CancellationToken.None);

            if (resp.IsSuccessStatusCode)
            {
                var result = resp.ConvertToJsonNode();

                QueueChatMessage($"{result.AsObject}");

                if (result != null && result["id"].Value != "")
                {
                    song = result.AsObject;
                    new SongMap(result.AsObject);
                }
            }
        }

        // BUG: Testing major changes. This will get seriously refactored soon.
        private async Task CheckRequest(RequestInfo requestInfo)
        {
            TwitchUser requestor = requestInfo.requestor;
            string request = requestInfo.request;

            string normalrequest= normalize.NormalizeBeatSaverString(requestInfo.request);

            var id = GetBeatSaverId(normalize.RemoveSymbols(ref request, normalize._SymbolsNoDash));

            if (id!="")
            {
                // Remap song id if entry present. This is one time, and not correct as a result. No recursion right now, could be confusing to the end user.
                if (songremap.ContainsKey(id) && !requestInfo.flags.HasFlag(CmdFlags.NoFilter))
                {
                    request = songremap[id];
                    QueueChatMessage($"Remapping request {requestInfo.request} to {request}");
                }

                string requestcheckmessage = IsRequestInQueue(normalize.RemoveSymbols(ref request, normalize._SymbolsNoDash));               // Check if requested ID is in Queue  
                if (requestcheckmessage != "")
                {
                    QueueChatMessage(requestcheckmessage);
                    return;
                }

                if (RequestBotConfig.Instance.OfflineMode && RequestBotConfig.Instance.offlinepath!="" && !MapDatabase.MapLibrary.ContainsKey(id))
                {
                    foreach (string directory in Directory.GetDirectories(RequestBotConfig.Instance.offlinepath, id+"*"))
                    {
                        await MapDatabase.LoadCustomSongs(directory, id);

                        await Task.Run(async () =>
                        {
                            while (MapDatabase.DatabaseLoading) await Task.Delay(25);
                        });
                        
                        break;
                    }
                }
            }

            JSONNode result = null;

            string errorMessage = "";

            // Get song query results from beatsaver.com
            if (!RequestBotConfig.Instance.OfflineMode)
            {
                string requestUrl = (id != "") ? $"https://beatsaver.com/api/maps/detail/{normalize.RemoveSymbols(ref request, normalize._SymbolsNoDash)}" : $"https://beatsaver.com/api/search/text/0?q={normalrequest}";

                var resp = await Plugin.WebClient.GetAsync(requestUrl, System.Threading.CancellationToken.None);

                if (resp.IsSuccessStatusCode)
                {
                    result = resp.ConvertToJsonNode();
                }
                else
                {
                    errorMessage = $"Invalid BeatSaver ID \"{request}\" specified. {requestUrl}";
                }
            }

            SongFilter filter = SongFilter.All;
            if (requestInfo.flags.HasFlag(CmdFlags.NoFilter)) filter = SongFilter.Queue;
            List<JSONObject> songs = GetSongListFromResults(result, request, ref errorMessage, filter, requestInfo.state.sort != "" ? requestInfo.state.sort : AddSortOrder.ToString());

            bool autopick = RequestBotConfig.Instance.AutopickFirstSong || requestInfo.flags.HasFlag(CmdFlags.Autopick);

            // Filter out too many or too few results
            if (songs.Count == 0)
                {
                    if (errorMessage == "")
                        errorMessage = $"No results found for request \"{request}\"";
                }
                else if (!autopick && songs.Count >= 4)
                {
                    errorMessage = $"Request for '{request}' produces {songs.Count} results, narrow your search by adding a mapper name, or use https://beatsaver.com to look it up.";
                }
                else if (!autopick && songs.Count > 1 && songs.Count < 4)
                {
                    var msg = new QueueLongMessage(1, 5);
                    msg.Header($"@{requestor.DisplayName}, please choose: ");
                    foreach (var eachsong in songs) msg.Add(new DynamicText().AddSong(eachsong).Parse(BsrSongDetail), ", ");
                    msg.end("...", $"No matching songs for for {request}");
                    return;
                }
                else
                {
                    if (!requestInfo.flags.HasFlag(CmdFlags.NoFilter)) errorMessage = SongSearchFilter(songs[0], false);
                }

                // Display reason why chosen song was rejected, if filter is triggered. Do not add filtered songs
                if (errorMessage != "")
                {
                    QueueChatMessage(errorMessage);
                    return;
                }

                JSONObject song = songs[0];

                // Song requests should try to be current. If the song was local, we double check for a newer version

                //if ((song["downloadUrl"].Value == "") && !RequestBotConfig.Instance.OfflineMode )
                //{
                //    //QueueChatMessage($"song:  {song["id"].Value.ToString()} ,{song["songName"].Value}");

                //    yield return Utilities.Download($"https://beatsaver.com/api/maps/detail/{song["id"].Value.ToString()}", Utilities.DownloadType.Raw, null,
                //     // Download success
                //     (web) =>
                //     {
                //         result = JSON.Parse(web.downloadHandler.text);
                //         var newsong = result["song"].AsObject;

                //         if (result != null && newsong["version"].Value != "")
                //         {
                //             new SongMap(newsong);
                //             song = newsong;
                //         }
                //     },
                //     // Download failed,  song probably doesn't exist on beatsaver
                //     (web) =>
                //     {
                //         // Let player know that the song is not current on BeatSaver
                //         requestInfo.requestInfo += " *LOCAL ONLY*";
                //         ; //errorMessage = $"Invalid BeatSaver ID \"{request}\" specified. {requestUrl}";
                //     });

                //}

            RequestTracker[requestor.Id].numRequests++;
                listcollection.add(duplicatelist, song["id"].Value);
                if ((requestInfo.flags.HasFlag(CmdFlags.MoveToTop)))
                    RequestQueue.Songs.Insert(0, new SongRequest(song, requestor, requestInfo.requestTime, RequestStatus.Queued, requestInfo.requestInfo));
                else
                    RequestQueue.Songs.Add(new SongRequest(song, requestor, requestInfo.requestTime, RequestStatus.Queued, requestInfo.requestInfo));

                RequestQueue.Write();

                Writedeck(requestor, "savedqueue"); // This can be used as a backup if persistent Queue is turned off.

            if (!requestInfo.flags.HasFlag(CmdFlags.SilentResult))
            {
                new DynamicText().AddSong(ref song).QueueMessage(AddSongToQueueText.ToString());
            }

            Dispatcher.RunOnMainThread(() =>
            {
                UpdateRequestUI();
                _refreshQueue = true;
            });
        }

        private static async void ProcessSongRequest(int index, bool fromHistory = false)
        {
            if ((RequestQueue.Songs.Count > 0 && !fromHistory) || (RequestHistory.Songs.Count > 0 && fromHistory))
            {
                SongRequest request = null;
                if (!fromHistory)
                {
                    SetRequestStatus(index, RequestStatus.Played);
                    request = DequeueRequest(index);
                }
                else
                {
                    request = RequestHistory.Songs.ElementAt(index);
                }

                if (request == null)
                {
                    Plugin.Log("Can't process a null request! Aborting!");
                    return;
                }
                else
                    Plugin.Log($"Processing song request {request.song["songName"].Value}");

 
                string songName = request.song["songName"].Value;
                string songIndex = $"{request.song["id"].Value} ({request.song["songName"].Value} - {request.song["levelAuthor"].Value})";
                songIndex = normalize.RemoveDirectorySymbols(ref songIndex); // Remove invalid characters.

                string currentSongDirectory = Path.Combine(Environment.CurrentDirectory, "Beat Saber_Data\\CustomLevels", songIndex);
                string songHash = request.song["hash"].Value.ToUpper();


                // Check to see if level exists, download if not.

                // Replace with level check.
                //CustomLevel[] levels = SongLoader.CustomLevels.Where(l => l.levelID.StartsWith(songHash)).ToArray();
                //if (levels.Length == 0)

                var rat = SongCore.Collections.levelIDsForHash(songHash);
                bool mapexists = (rat.Count>0) && (rat[0] != "");
                

                if (!SongCore.Loader.CustomLevels.ContainsKey(currentSongDirectory) && !mapexists)
                {


                    EmptyDirectory(".requestcache", false);


                    //SongMap map;
                    //if (MapDatabase.MapLibrary.TryGetValue(songIndex, out map))
                    //{
                    //    if (map.path != "")
                    //    {
                    //        songIndex = map.song["version"].Value;
                    //        songName = map.song["songName"].Value;
                    //        currentSongDirectory = Path.Combine(Environment.CurrentDirectory, "CustomSongs", songIndex);
                    //        songHash = map.song["hashMd5"].Value.ToUpper();

                    //        Directory.CreateDirectory(currentSongDirectory);
                    //        // HACK to allow playing alternate songs not in custom song directory
                    //        CopyFilesRecursively(new DirectoryInfo(map.path),new DirectoryInfo( currentSongDirectory));                           

                    //        goto here;
                    //    }
                    //}

                    //Plugin.Log("Downloading");

                    if (Directory.Exists(currentSongDirectory))
                    {
                        EmptyDirectory(currentSongDirectory, true);
                        Plugin.Log($"Deleting {currentSongDirectory}");
                    }

                    string localPath = Path.Combine(Environment.CurrentDirectory, ".requestcache", $"{request.song["id"].Value}.zip");
                    //string dl = $"https://beatsaver.com {request.song["downloadURL"].Value}";
                    //Instance.QueueChatMessage($"Download url: {dl}, {request.song}");



                    // Insert code to replace local path with ZIP path here
                    //SongMap map;
                    //if (MapDatabase.MapLibrary.TryGetValue(songIndex, out map))
                    //{
                    //    if (map.path != "")
                    //    {
                    //        songIndex = map.song["version"].Value;
                    //        songName = map.song["songName"].Value;
                    //        currentSongDirectory = Path.Combine(Environment.CurrentDirectory, "CustomSongs", songIndex);
                    //        songHash = map.song["hashMd5"].Value.ToUpper();

                    //        Directory.CreateDirectory(currentSongDirectory);
                    //        // HACK to allow playing alternate songs not in custom song directory
                    //        CopyFilesRecursively(new DirectoryInfo(map.path),new DirectoryInfo( currentSongDirectory));                           

                    //        goto here;
                    //    }
                    //}


#if UNRELEASED
                    // Direct download hack
                    var ext = Path.GetExtension(request.song["coverURL"].Value);
                    var k = request.song["coverURL"].Value.Replace(ext, ".zip");

                    var songZip = await Plugin.WebClient.DownloadSong($"https://beatsaver.com{k}", System.Threading.CancellationToken.None);
#else
                    var songZip = await Plugin.WebClient.DownloadSong($"https://beatsaver.com{request.song["downloadURL"].Value}", System.Threading.CancellationToken.None);
#endif

                    Stream zipStream = new MemoryStream(songZip);
                    try
                    {
                        // open zip archive from memory stream
                        ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
                        archive.ExtractToDirectory(currentSongDirectory);
                        archive.Dispose();
                    }
                    catch (Exception e)
                    {
                        Plugin.Log($"Unable to extract ZIP! Exception: {e}");
                        return;
                    }
                    zipStream.Close();

                here:

                    await Task.Run(async () =>
                    {
                        while (!SongCore.Loader.AreSongsLoaded && SongCore.Loader.AreSongsLoading) await Task.Delay(25);
                    });

                    Loader.Instance.RefreshSongs();

                    await Task.Run(async () =>
                    {
                        while (!SongCore.Loader.AreSongsLoaded && SongCore.Loader.AreSongsLoading) await Task.Delay(25);
                    });

                    EmptyDirectory(".requestcache", true);
                    //levels = SongLoader.CustomLevels.Where(l => l.levelID.StartsWith(songHash)).ToArray();
                }
                else
                {
                    //Instance.QueueChatMessage($"Directory exists: {currentSongDirectory}");

                    Plugin.Log($"Song {songName} already exists!");
                }

                // Dismiss the song request viewcontroller now
                //_songRequestMenu.Dismiss();
                _flowCoordinator.Dismiss();

                if (true)
                {
                    //Plugin.Log($"Scrolling to level {levels[0].levelID}");

                    bool success = false;

                    Dispatcher.RunCoroutine(SongListUtils.ScrollToLevel(request.song["hash"].Value.ToUpper(), (s) => success = s, false));

                    // Redownload the song if we failed to scroll to it
                }
                else
                {
                    Plugin.Log("Failed to find new level!");
                }

                if (!request.song.IsNull && RequestBotConfig.Instance.SendNextSongBeingPlayedtoChat)
                {
                    new DynamicText().AddUser(ref request.requestor).AddSong(request.song).QueueMessage(NextSonglink.ToString()); // Display next song message
                }

                #if UNRELEASED
                if (!request.song.IsNull) // Experimental!
                {
                    ChatHandler.SendCommand("/marker "+ new DynamicText().AddUser(ref request.requestor).AddSong(request.song).Parse(NextSonglink.ToString()));
                }
                #endif
            }
        }


        public static void UpdateRequestUI(bool writeSummary = true)
        {
            try
            {
                if (writeSummary)
                    WriteQueueSummaryToFile(); // Write out queue status to file, do it first

                if (_requestButton != null)
                {
                    _requestButton.interactable = true;

                    if (RequestQueue.Songs.Count == 0)
                    {
                        _requestButton.gameObject.GetComponentInChildren<Image>().color = Color.red;
                    }
                    else
                    {
                        _requestButton.gameObject.GetComponentInChildren<Image>().color = Color.green;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log(ex.ToString());
            }
        }


        public static void DequeueRequest(SongRequest request, bool updateUI = true)
        {
            if (request.status!=RequestStatus.Wrongsong && request.status!=RequestStatus.SongSearch) RequestHistory.Songs.Insert(0, request); // Wrong song requests are not logged into history, is it possible that other status states shouldn't be moved either?

            if (RequestHistory.Songs.Count > RequestBotConfig.Instance.RequestHistoryLimit)
            {
                int diff = RequestHistory.Songs.Count - RequestBotConfig.Instance.RequestHistoryLimit;
                RequestHistory.Songs.RemoveRange(RequestHistory.Songs.Count - diff - 1, diff);
            }
            RequestQueue.Songs.Remove(request);
            RequestHistory.Write();
            RequestQueue.Write();

            // Decrement the requestors request count, since their request is now out of the queue

            if (!RequestBotConfig.Instance.LimitUserRequestsToSession)
            {
                if (RequestTracker.ContainsKey(request.requestor.Id)) RequestTracker[request.requestor.Id].numRequests--;
            }

            if (updateUI == false) return;

            UpdateRequestUI();
            _refreshQueue = true;
        }

        public static SongRequest DequeueRequest(int index, bool updateUI = true)
        {
            SongRequest request = RequestQueue.Songs.ElementAt(index);

            if (request != null)
                DequeueRequest(request, updateUI);

#if UNRELEASED
            // If the queue is empty, Execute a custom command, the could be a chat message, a deck request, or nothing
            try
            {
                if (RequestBotConfig.Instance.RequestQueueOpen && updateUI == true && RequestQueue.Songs.Count == 0) RequestBot.listcollection.runscript("emptyqueue.script");
            }
            catch (Exception ex) { Plugin.Log(ex.ToString()); }
#endif
            return request;
        }

        public static void SetRequestStatus(int index, RequestStatus status, bool fromHistory = false)
        {
            if (!fromHistory)
                RequestQueue.Songs[index].status = status;
            else
                RequestHistory.Songs[index].status = status;
        }

        public static void Blacklist(int index, bool fromHistory, bool skip)
        {
            // Add the song to the blacklist
            SongRequest request = fromHistory ? RequestHistory.Songs.ElementAt(index) : RequestQueue.Songs.ElementAt(index);

            listcollection.add(banlist, request.song["id"].Value);
 
            Instance.QueueChatMessage($"{request.song["songName"].Value} by {request.song["authorName"].Value} ({request.song["id"].Value}) added to the blacklist.");

            if (!fromHistory)
            {
                if (skip)
                    Skip(index, RequestStatus.Blacklisted);
            }
            else
                SetRequestStatus(index, RequestStatus.Blacklisted, fromHistory);
        }

        public static void Skip(int index, RequestStatus status = RequestStatus.Skipped)
        {
            // Set the final status of the request
            SetRequestStatus(index, status);

            // Then dequeue it
            DequeueRequest(index);
        }

        public static void Process(int index, bool fromHistory)
        {
            ProcessSongRequest(index, fromHistory);
        }

        public static void Next()
        {
            ProcessSongRequest(0);
        }


        private string GetBeatSaverId(string request)
        {
            request=normalize.RemoveSymbols(ref request, normalize._SymbolsNoDash);
            if (request!="360" && _digitRegex.IsMatch(request)) return request;
            if (_beatSaverRegex.IsMatch(request))
            {
                string[] requestparts = request.Split(new char[] { '-' }, 2);
                //return requestparts[0];
           
                int o;
                Int32.TryParse(requestparts[1], out o);
                     {
                        //Instance.QueueChatMessage($"key={o.ToString("x")}");
                    return o.ToString("x");
                    }
      
            }
            return "";
        }


        private string AddToTop(ParseState state)
        {
            ParseState newstate = new ParseState(state); // Must use copies here, since these are all threads
            newstate.flags |= CmdFlags.MoveToTop | CmdFlags.NoFilter;
            newstate.info = "!ATT";
            return ProcessSongRequest(newstate);
        }

        private string ModAdd(ParseState state)
        {
            ParseState newstate = new ParseState(state); // Must use copies here, since these are all threads
            newstate.flags |= CmdFlags.NoFilter;
            newstate.info = "Unfiltered";
            return ProcessSongRequest(newstate);
        }


        private string ProcessSongRequest(ParseState state)
        {
            try
            {
                if (RequestBotConfig.Instance.RequestQueueOpen == false && !state.flags.HasFlag(CmdFlags.NoFilter) && !state.flags.HasFlag(CmdFlags.Local)) // BUG: Complex permission, Queue state message needs to be handled higher up
                {
                    QueueChatMessage($"Queue is currently closed.");
                    return success;
                }

                if (!RequestTracker.ContainsKey(state.user.Id))
                    RequestTracker.Add(state.user.Id, new RequestUserTracker());

                int limit = RequestBotConfig.Instance.UserRequestLimit;
                if (state.user.IsSubscriber) limit = Math.Max(limit, RequestBotConfig.Instance.SubRequestLimit);
                if (state.user.IsModerator) limit = Math.Max(limit, RequestBotConfig.Instance.ModRequestLimit);
                if (state.user.IsVip) limit += RequestBotConfig.Instance.VipBonusRequests; // Current idea is to give VIP's a bonus over their base subscription class, you can set this to 0 if you like

                if (!state.user.IsBroadcaster)
                {
                    if (RequestTracker[state.user.Id].numRequests >= limit)
                    {
                        if (RequestBotConfig.Instance.LimitUserRequestsToSession)
                        {
                            new DynamicText().Add("Requests", RequestTracker[state.user.Id].numRequests.ToString()).Add("RequestLimit", RequestBotConfig.Instance.SubRequestLimit.ToString()).QueueMessage("You've already used %Requests% requests this stream. Subscribers are limited to %RequestLimit%.");
                        }
                        else
                        {
                            new DynamicText().Add("Requests", RequestTracker[state.user.Id].numRequests.ToString()).Add("RequestLimit", RequestBotConfig.Instance.SubRequestLimit.ToString()).QueueMessage("You already have %Requests% on the queue. You can add another once one is played. Subscribers are limited to %RequestLimit%.");
                        }

                        return success;
                    }
                }

                // BUG: Need to clean up the new request pipeline
                string testrequest = normalize.RemoveSymbols(ref state.parameter,normalize._SymbolsNoDash);

                RequestInfo newRequest = new RequestInfo(state.user, state.parameter, DateTime.UtcNow, _digitRegex.IsMatch(testrequest) || _beatSaverRegex.IsMatch(testrequest),state, state.flags, state.info);

                if (!newRequest.isBeatSaverId && state.parameter.Length < 2)
                    QueueChatMessage($"Request \"{state.parameter}\" is too short- Beat Saver searches must be at least 3 characters!");
                 if (!UnverifiedRequestQueue.Contains(newRequest))
                    UnverifiedRequestQueue.Enqueue(newRequest);

            }
            catch (Exception ex)
            {
                Plugin.Log(ex.ToString());

            }
        return success;
        }

 
    }
}