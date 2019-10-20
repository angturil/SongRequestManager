
using CustomUI.BeatSaber;
using StreamCore.Chat;
using StreamCore.Utils;
using StreamCore.SimpleJSON;

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
using UnityEngine.Networking;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;
using SongCore;
using StreamCore;
using StreamCore.Twitch;

namespace SongRequestManager
{
    public partial class RequestBot : MonoBehaviour, ITwitchMessageHandler
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

        private static CustomMenu _songRequestMenu = null;


        public static RequestBotListViewController _songRequestListViewController = null;

        public static CustomViewController _KeyboardViewController = null;

        public static string playedfilename = "";

        public Action<TwitchMessage> Twitch_OnPrivmsgReceived { get; set; }
        public Action<TwitchMessage> Twitch_OnRoomstateReceived { get; set; }
        public Action<TwitchMessage> Twitch_OnUsernoticeReceived { get; set; }
        public Action<TwitchMessage> Twitch_OnUserstateReceived { get; set; }
        public Action<TwitchMessage> Twitch_OnClearchatReceived { get; set; }
        public Action<TwitchMessage> Twitch_OnClearmsgReceived { get; set; }
        public Action<TwitchMessage> Twitch_OnModeReceived { get; set; }
        public Action<TwitchMessage> Twitch_OnJoinReceived { get; set; }
        public bool ChatCallbacksReady { get; set; } = false;

        public static void OnLoad()
        {
            try
            {
                var _levelListViewController = Resources.FindObjectsOfTypeAll<LevelPackLevelsViewController>().First();
                if (_levelListViewController)
                    {
                    _requestButton = BeatSaberUI.CreateUIButton(_levelListViewController.rectTransform, "OkButton", new Vector2(66, -3.5f),
                        new Vector2(9f, 5.5f), () => { _requestButton.interactable = false; _songRequestMenu.Present(); _requestButton.interactable = true; }, "SRM");

                    (_requestButton.transform as RectTransform).anchorMin = new Vector2(1, 1);
                    (_requestButton.transform as RectTransform).anchorMax = new Vector2(1, 1);

                    _requestButton.ToggleWordWrapping(false);
                    _requestButton.SetButtonTextSize(3.5f);
                    BeatSaberUI.AddHintText(_requestButton.transform as RectTransform, "Manage the current request queue");

                    UpdateRequestUI();
                    Plugin.Log("Created request button!");
                }
            }
            catch
            {
                Plugin.Log("Unable to create request button");
            }

            if (_songRequestListViewController == null)
                _songRequestListViewController = BeatSaberUI.CreateViewController<RequestBotListViewController>();


            if (_KeyboardViewController == null)
            {
                _KeyboardViewController = BeatSaberUI.CreateViewController<CustomViewController>();

                RectTransform KeyboardContainer = new GameObject("KeyboardContainer", typeof(RectTransform)).transform as RectTransform;
                KeyboardContainer.SetParent(_KeyboardViewController.rectTransform, false);
                KeyboardContainer.sizeDelta = new Vector2(60f, 40f);

                var mykeyboard = new KEYBOARD(KeyboardContainer, "");

#if UNRELEASED
                //mykeyboard.AddKeys(BOTKEYS, 0.4f);
                AddKeyboard(mykeyboard, "emotes.kbd", 0.4f);
#endif
                mykeyboard.AddKeys(KEYBOARD.QWERTY); // You can replace this with DVORAK if you like
                mykeyboard.DefaultActions();



#if UNRELEASED
                const string SEARCH = @"

[CLEAR SEARCH]/0 /2 [NEWEST]/0 /2 [UNFILTERED]/30 /2 [PP]/0'!addsongs/top/pp pp%CR%' /2 [SEARCH]/0";

#else
                const string SEARCH = @"

[CLEAR SEARCH]/0 /2 [NEWEST]/0 /2 [UNFILTERED]/30 /2 [SEARCH]/0";

#endif


                mykeyboard.SetButtonType("OkButton"); // Adding this alters button positions??! Why?
                mykeyboard.AddKeys(SEARCH, 0.75f);

                mykeyboard.SetAction("CLEAR SEARCH", ClearSearch);
                mykeyboard.SetAction("UNFILTERED", UnfilteredSearch);
                mykeyboard.SetAction("SEARCH", MSD);
                mykeyboard.SetAction("NEWEST", Newest);


#if UNRELEASED
                AddKeyboard(mykeyboard, "decks.kbd", 0.4f);
#endif
            
                // The UI for this might need a bit of work.

                AddKeyboard(mykeyboard, "RightPanel.kbd");
            }

            if (_songRequestMenu == null)
            {
                _songRequestMenu = BeatSaberUI.CreateCustomMenu<CustomMenu>("Song Request Queue");
                _songRequestMenu.SetMainViewController(_songRequestListViewController, true);
                _songRequestMenu.SetRightViewController(_KeyboardViewController, false);
            }

            SongListUtils.Initialize();

            WriteQueueSummaryToFile();
            WriteQueueStatusToFile(QueueMessage(RequestBotConfig.Instance.RequestQueueOpen));

            // Yes, this is disabled on purpose. StreamCore will init this class for you now, so don't uncomment this! -Brian
            //if (Instance) return;
            //new GameObject("SongRequestManager").AddComponent<RequestBot>();
        }

        public static  void AddKeyboard(KEYBOARD keyboard, string keyboardname,float scale=0.5f)
            {
            try
            {
                string fileContent = File.ReadAllText(Path.Combine(Plugin.DataPath, keyboardname));
                if (fileContent.Length > 0) keyboard.AddKeys(fileContent,scale);
            }
            catch           
            {
            // This is a silent fail since custom keyboards are optional
            }
            }



        public static void Newest(KEYBOARD.KEY key)
        {
            ClearSearches();
            RequestBot.COMMAND.Parse(TwitchWebSocketClient.OurTwitchUser, $"!addnew/top",CmdFlags.Local);
        }

        public static void Search(KEYBOARD.KEY key)
        {
            if (key.kb.KeyboardText.text.StartsWith("!"))
            {
                key.kb.Enter(key);
            }
            ClearSearches();
            RequestBot.COMMAND.Parse(TwitchWebSocketClient.OurTwitchUser, $"!addsongs/top {key.kb.KeyboardText.text}",CmdFlags.Local);
            key.kb.Clear(key);
        }

        public static void MSD(KEYBOARD.KEY key)
        {
            if (key.kb.KeyboardText.text.StartsWith("!"))
            {
                key.kb.Enter(key);
            }
            ClearSearches();
            RequestBot.COMMAND.Parse(TwitchWebSocketClient.OurTwitchUser, $"!makesearchdeck {key.kb.KeyboardText.text}", CmdFlags.Local);
            key.kb.Clear(key);
        }


        public static void UnfilteredSearch(KEYBOARD.KEY key)
        {
            if (key.kb.KeyboardText.text.StartsWith("!"))
            {
                key.kb.Enter(key);
            }
            ClearSearches();
            RequestBot.COMMAND.Parse(TwitchWebSocketClient.OurTwitchUser, $"!addsongs/top/mod {key.kb.KeyboardText.text}",CmdFlags.Local);
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
                    Utilities.EmptyDirectory(filesToDelete);


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


                if (RequestBotConfig.Instance.PPSearch) StartCoroutine(GetPPData()); // Start loading PP data

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


                StartCoroutine(ProcessRequestQueue());

                Twitch_OnPrivmsgReceived += PRIVMSG;

                RequestBotConfig.Instance.ConfigChangedEvent += OnConfigChangedEvent;
                ChatCallbacksReady = true;
            }
        catch (Exception ex)
            {
            Plugin.Log(ex.ToString());
            Instance.QueueChatMessage(ex.ToString());
            }
        }



        public bool MyChatMessageHandler(TwitchMessage msg)
        {
            string excludefilename = "chatexclude.users";



            return RequestBot.Instance && RequestBot.listcollection.contains(ref excludefilename, msg.user.displayName.ToLower(), RequestBot.ListFlags.Uncached);
        }

        private void PRIVMSG(TwitchMessage msg)
          {

            RequestBot.COMMAND.Parse(msg.user.Twitch, msg.message);
        }

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
            COMMAND.Parse(TwitchWebSocketClient.OurTwitchUser, command);
            }

        private void RunStartupScripts()
        {
            ReadRemapList(); // BUG: This should use list manager

            MapperBanList(TwitchWebSocketClient.OurTwitchUser, "mapperban.list");
            WhiteList(TwitchWebSocketClient.OurTwitchUser, "whitelist.unique");
            BlockedUserList(TwitchWebSocketClient.OurTwitchUser, "blockeduser.unique");
            accesslist("whitelist.unique");
            accesslist("blockeduser.unique");
            accesslist("mapper.list");
            accesslist("mapperban.list");

#if UNRELEASED


            OpenList(TwitchWebSocketClient.OurTwitchUser, "mapper.list"); // Open mapper list so we can get new songs filtered by our favorite mappers.
            MapperAllowList(TwitchWebSocketClient.OurTwitchUser, "mapper.list");
            loaddecks(TwitchWebSocketClient.OurTwitchUser, ""); // Load our default deck collection
            // BUG: Command failure observed once, no permission to use /chatcommand. Possible cause: Ourtwitchuser isn't authenticated yet.

            RunScript(TwitchWebSocketClient.OurTwitchUser, "startup.script"); // Run startup script. This can include any bot commands.
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
                TwitchWebSocketClient.SendMessage(message);
                TwitchMessage tmpMessage = new TwitchMessage();
                tmpMessage.user = TwitchWebSocketClient.OurTwitchUser;
                //MessageParser.Parse(new ChatMessage(message, tmpMessage)); // This call is obsolete, when sending a message through TwitchWebSocketClient, the message should automatically appear in chat.
            }
            catch (Exception e)
            {
                Plugin.Log($"Exception was caught when trying to send bot message. {e.ToString()}");
            }
        }

        public void QueueChatMessage(string message)
        {
            TwitchWebSocketClient.SendCommand($"{RequestBotConfig.Instance.BotPrefix}\uFEFF{message}");
        }
        
        private IEnumerator ProcessRequestQueue()
        {
            var waitForRequests = new WaitUntil(() => UnverifiedRequestQueue.Count > 0);
            while (!Plugin.Instance.IsApplicationExiting)
            {
                yield return waitForRequests;

                if (UnverifiedRequestQueue.TryDequeue(out var requestInfo))
                    yield return CheckRequest(requestInfo);
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


        private IEnumerator UpdateSongMap(JSONObject song)
            {

            yield return Utilities.Download($"https://beatsaver.com/api/maps/detail/{song["id"].Value.ToString()}", Utilities.DownloadType.Raw, null,
            // Download success
            (web) =>
            {
                var result = JSON.Parse(web.downloadHandler.text);

                QueueChatMessage($"{result.AsObject}");

                if (result != null && result["id"].Value != "")
                {
                    song = result.AsObject;
                    new SongMap(result.AsObject);
                }
            },
            // Download failed,  song probably doesn't exist on beatsaver
            (web) =>
            {

                ; //errorMessage = $"Invalid BeatSaver ID \"{request}\" specified. {requestUrl}";
            });

        }


        // BUG: Testing major changes. This will get seriously refactored soon.
        private IEnumerator CheckRequest(RequestInfo requestInfo)
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
                    yield break;
                }

                if (RequestBotConfig.Instance.OfflineMode && RequestBotConfig.Instance.offlinepath!="" && !MapDatabase.MapLibrary.ContainsKey(id))
                {
                    foreach (string directory in Directory.GetDirectories(RequestBotConfig.Instance.offlinepath, id+"*"))
                    {
                        
                        MapDatabase.LoadCustomSongs(directory, id);
                        while (MapDatabase.DatabaseLoading)  
                            {
                            yield return 0;
                            }
                        
                        break;
                    }
                }
            }

            JSONNode result = null;

            string errorMessage = "";

            // Get song query results from beatsaver.com

            string requestUrl = (id!="") ? $"https://beatsaver.com/api/maps/detail/{normalize.RemoveSymbols(ref request, normalize._SymbolsNoDash)}" : $"https://beatsaver.com/api/search/text/0?q={normalrequest}";

            if (RequestBotConfig.Instance.OfflineMode) requestUrl = "";

            yield return Utilities.Download(requestUrl, Utilities.DownloadType.Raw, null,
                // Download success
                (web) =>
                {
                    result = JSON.Parse(web.downloadHandler.text);
                },
                // Download failed,  song probably doesn't exist on beatsaver
                (web) =>
                {
                    errorMessage=$"Invalid BeatSaver ID \"{request}\" specified. {requestUrl}";
                }
            );
 
            yield return null;

 
                SongFilter filter = SongFilter.All;
                if (requestInfo.flags.HasFlag(CmdFlags.NoFilter)) filter = SongFilter.Queue;
                List<JSONObject> songs = GetSongListFromResults(result, request, ref errorMessage, filter, requestInfo.state.sort != "" ? requestInfo.state.sort : AddSortOrder.ToString());

            yield return null;

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
                    msg.Header($"@{requestor.displayName}, please choose: ");
                    foreach (var eachsong in songs) msg.Add(new DynamicText().AddSong(eachsong).Parse(BsrSongDetail), ", ");
                    msg.end("...", $"No matching songs for for {request}");
                    yield break;
                }
                else
                {
                    if (!requestInfo.flags.HasFlag(CmdFlags.NoFilter)) errorMessage = SongSearchFilter(songs[0], false);
                }

                // Display reason why chosen song was rejected, if filter is triggered. Do not add filtered songs
                if (errorMessage != "")
                {
                    QueueChatMessage(errorMessage);
                    yield break;
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


            yield return null;

            RequestTracker[requestor.id].numRequests++;
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
                UpdateRequestUI();
                _refreshQueue = true;

        }




        private static IEnumerator ProcessSongRequest(int index, bool fromHistory = false)
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
                    yield break;
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
                

                if (!SongCore.Loader.CustomLevels.ContainsKey(currentSongDirectory) && !mapexists )
                {


                    Utilities.EmptyDirectory(".requestcache", false);


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
                        Utilities.EmptyDirectory(currentSongDirectory, true);
                        Plugin.Log($"Deleting {currentSongDirectory}");
                    }

                    string localPath = Path.Combine(Environment.CurrentDirectory, ".requestcache", $"{request.song["id"].Value}.zip");
                    //string dl = $"https://beatsaver.com {request.song["downloadURL"].Value}";
                    //Instance.QueueChatMessage($"Download url: {dl}, {request.song}");

                    yield return Utilities.DownloadFile($"https://beatsaver.com{request.song["downloadURL"].Value}", localPath);
                    yield return Utilities.ExtractZip(localPath, currentSongDirectory);
                    
                    here:

                    yield return new WaitUntil(() => SongCore.Loader.AreSongsLoaded && !SongCore.Loader.AreSongsLoading);

                    Loader.Instance.RefreshSongs();
                    yield return new WaitUntil(() => SongCore.Loader.AreSongsLoaded && !SongCore.Loader.AreSongsLoading);

                    Utilities.EmptyDirectory(".requestcache", true);
                    //levels = SongLoader.CustomLevels.Where(l => l.levelID.StartsWith(songHash)).ToArray();
                }
                else
                {
                    //Instance.QueueChatMessage($"Directory exists: {currentSongDirectory}");

                    Plugin.Log($"Song {songName} already exists!");
                }

                // Dismiss the song request viewcontroller now
                _songRequestMenu.Dismiss();

                if (true)
                {
                    //Plugin.Log($"Scrolling to level {levels[0].levelID}");

                    bool success = false;
                    yield return SongListUtils.ScrollToLevel(request.song["hash"].Value.ToUpper(), (s) => success = s, false);
                    yield return null;

                    // Redownload the song if we failed to scroll to it
                }
                else
                {
                    Plugin.Log("Failed to find new level!");
                }

                if (!request.song.IsNull) new DynamicText().AddUser(ref request.requestor).AddSong(request.song).QueueMessage(NextSonglink.ToString()); // Display next song message

            }
        }


        public static void UpdateRequestUI(bool writeSummary = true)
        {
            try
            {
                if (writeSummary)
                    WriteQueueSummaryToFile(); // Write out queue status to file, do it first

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
                if (RequestTracker.ContainsKey(request.requestor.id)) RequestTracker[request.requestor.id].numRequests--;
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
            Instance?.StartCoroutine(ProcessSongRequest(index, fromHistory));
        }

        public static void Next()
        {
            Instance?.StartCoroutine(ProcessSongRequest(0));
        }


        private string GetBeatSaverId(string request)
        {
            request=normalize.RemoveSymbols(ref request, normalize._SymbolsNoDash);
            if (_digitRegex.IsMatch(request)) return request;
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

                if (!RequestTracker.ContainsKey(state.user.id))
                    RequestTracker.Add(state.user.id, new RequestUserTracker());

                int limit = RequestBotConfig.Instance.UserRequestLimit;
                if (state.user.isSub) limit = Math.Max(limit, RequestBotConfig.Instance.SubRequestLimit);
                if (state.user.isMod) limit = Math.Max(limit, RequestBotConfig.Instance.ModRequestLimit);
                if (state.user.isVip) limit += RequestBotConfig.Instance.VipBonusRequests; // Current idea is to give VIP's a bonus over their base subscription class, you can set this to 0 if you like

                if (!state.user.isBroadcaster)
                {
                    if (RequestTracker[state.user.id].numRequests >= limit)
                    {
                        if (RequestBotConfig.Instance.LimitUserRequestsToSession)
                        {
                            new DynamicText().Add("Requests", RequestTracker[state.user.id].numRequests.ToString()).Add("RequestLimit", RequestBotConfig.Instance.SubRequestLimit.ToString()).QueueMessage("You've already used %Requests% requests this stream. Subscribers are limited to %RequestLimit%.");
                        }
                        else
                        {
                            new DynamicText().Add("Requests", RequestTracker[state.user.id].numRequests.ToString()).Add("RequestLimit", RequestBotConfig.Instance.SubRequestLimit.ToString()).QueueMessage("You already have %Requests% on the queue. You can add another once one is played. Subscribers are limited to %RequestLimit%.");
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