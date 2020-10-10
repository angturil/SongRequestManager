using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SongRequestManager
{
    public class RequestBotConfig
    {
        private string FilePath = Path.Combine(Plugin.DataPath, "RequestBotSettings.ini");


        public bool RequestQueueOpen = true;
        public bool PersistentRequestQueue = true;

        public bool AutoplaySong = false; // Pressing play will automatically attempt to play the song you selected at the highest difficulty level it has
        public bool ClearNoFail = true; // Pressing play will automatically attempt to play the song you selected at the highest difficulty level it has

        public int RequestHistoryLimit = 40;
        public int UserRequestLimit = 2;
        public int SubRequestLimit = 5;
        public int ModRequestLimit = 10;
        public int VipBonusRequests = 1; // VIP's get bonus requests in addition to their base limit *IMPLEMENTED*
        public int SessionResetAfterXHours = 6; // Number of hours before persistent session properties are reset (ie: Queue, Played , Duplicate List)
        public bool LimitUserRequestsToSession = false; // Request limits do not reset after a song is played.  
        // TODO: Add the ablity to have a maximum for non-sub users per session (default would be set to 3)
        // TODO: add a message to let the non-sub user to know how many requests they have left per session
        // TODO: add a !uscanisthebest command with emoji responce with extra s


        public float LowestAllowedRating = 0; // Lowest allowed song rating to be played 0-100 *IMPLEMENTED*, needs UI
        public float MaximumSongLength = 180; // Maximum song length in minutes
        public float MinimumNJS = 0;

        public int MaxiumScanRange = 5; // How far down the list to scan for new songs

        public int PPDeckMiniumumPP=150; // Minimum PP to add to pp deck

        public string DeckList = "fun hard brutal dance chill sickwalls brutal mydeck metal rap pop";

        public bool AutopickFirstSong = false; // Pick the first song that !bsr finds instead of showing a short list. *IMPLEMENTED*, needs UI
        public bool AllowModAddClosedQueue = true; // Allow moderator to add songs while queue is closed 
        public bool SendNextSongBeingPlayedtoChat = true; // Enable chat message when you hit play
        public bool UpdateQueueStatusFiles = true; // Create and update queue list and open/close status files for OBS *IMPLEMENTED*, needs UI
        public int MaximumQueueTextEntries = 8;
        public string BotPrefix ="";

        public bool ModFullRights = false; // Allow moderator full broadcaster rights. Use at own risk!

        public string QueueDelimiter = ", "; // list seperator for the queue, user might want something else besides a comma

        public bool ShowPermissionLevelRequiredInChat = false;
        public string PermissionLevelNotAcceptedText = "Sorry but you dont have access to that command!";


        // doing this explicitly so that everyone can see whats going on
        // !bsr 
        public int SongRequestPermissionLevel = (int)(RequestBot.CmdFlags.Everyone);
        // !lookup?
        public int LookupSongsPermissionLevel = (int)(RequestBot.CmdFlags.Everyone);
        // !link?
        public int ShowSongLinkPermissionLevel = (int)(RequestBot.CmdFlags.Everyone);
        // covers !open, !close
        public int ToggleQueuePermissionLevel = (int)(RequestBot.CmdFlags.Mod | RequestBot.CmdFlags.Broadcaster);
        // covers !queue
        public int ListQueuePermissionLevel = (int)(RequestBot.CmdFlags.Mod | RequestBot.CmdFlags.Broadcaster);
        // covers !played
        public int ShowSongsPlayedPermissionLevel = (int)(RequestBot.CmdFlags.Mod | RequestBot.CmdFlags.Broadcaster);
        // covers !history
        public int ShowHistoryPermissionLevel = (int)(RequestBot.CmdFlags.Mod | RequestBot.CmdFlags.Broadcaster);
        // !modadd? has its own checks

        // should cover !mtt, !att, !last
        public int MoveRequestPositionInQueuePermissionLevel = (int)(RequestBot.CmdFlags.Mod | RequestBot.CmdFlags.Broadcaster);
        // covers !remove
        public int DequeuePermissionLevel = (int)(RequestBot.CmdFlags.Mod | RequestBot.CmdFlags.Broadcaster);
        // covers !oops
        public int WrongSongPermissionLevel = (int)(RequestBot.CmdFlags.Everyone);
        // !block?
        // should cover !unblock
        public int UnBanPermissionLevel = (int)(RequestBot.CmdFlags.Mod | RequestBot.CmdFlags.Broadcaster);
        // should cover !blist
        public int ShowBanListPermissionLevel = (int)(RequestBot.CmdFlags.Broadcaster);
        // covers !remap
        public int RemapPermissionLevel = (int)(RequestBot.CmdFlags.Mod | RequestBot.CmdFlags.Broadcaster);
        // covers !unmap
        public int UnmapPermissionLevel = (int)(RequestBot.CmdFlags.Mod | RequestBot.CmdFlags.Broadcaster);
        //covers !clearQueue
        public int ClearQueuePermissionLevel = (int)(RequestBot.CmdFlags.Mod | RequestBot.CmdFlags.Broadcaster);
        // !clearalreadyplayed?
        // !help?
        public int ShowHelpPermissionLevel = (int)(RequestBot.CmdFlags.Sub | RequestBot.CmdFlags.Mod | RequestBot.CmdFlags.VIP | RequestBot.CmdFlags.Broadcaster);
        // covers !commandlist
        public int ShowCommandListPermissionLevel = (int)(RequestBot.CmdFlags.Everyone);
        // !readdeck?

        // !chatmessage?
        // covers !runscript
        public int RunScriptPermissionLevel = (int)(RequestBot.CmdFlags.Mod | RequestBot.CmdFlags.Broadcaster);
        // covers !formatlist
        public int ShowFormatListPermissionLevel = (int)(RequestBot.CmdFlags.Broadcaster);
        // !songmsg
        // !addsongs
        // !every
        // !in
        // !clearevents
        // !addnew
        // !backup
        // !songsavedatabase
        // !queuestatus
        // !QueueLottery
        // !addtoqueue

        // not yet implemented 
        // !allowmappers
        public int MapperAllowListPermissionLevel = (int)(RequestBot.CmdFlags.Mod | RequestBot.CmdFlags.Broadcaster);
        // !blockmappers
        public int MapperBanListPermissionLevel = (int)(RequestBot.CmdFlags.Mod | RequestBot.CmdFlags.Broadcaster);
        // used in startup scripts
        public int WhiteListPermissionLevel = (int)(RequestBot.CmdFlags.Broadcaster);
        public int BlockedUserPermissionLevel = (int)(RequestBot.CmdFlags.Broadcaster);





        public int maximumqueuemessages = 1;
        public int maximumlookupmessages = 1;

        public string LastBackup = DateTime.MinValue.ToString();
        public string backuppath = Path.Combine(Environment.CurrentDirectory, "userdata", "backup");

        public bool OfflineMode = false;
        public bool SavedatabaseOnNewest=false;
        public string offlinepath = "d:\\customsongs";

        public bool LocalSearch = false;
        public bool PPSearch = false;
        public string additionalsongpath = "";
        public string songdownloadpath = "";

        public event Action<RequestBotConfig> ConfigChangedEvent;

        private readonly FileSystemWatcher _configWatcher;
        private bool _saving;

        private static RequestBotConfig _instance = null;
        public static RequestBotConfig Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new RequestBotConfig();
                return _instance;
            }

            private set
            {
                _instance = value;
            }
        }

        public RequestBotConfig()
        {
            Instance = this;

            _configWatcher = new FileSystemWatcher();

            Task.Run(() =>
            {
                while (!Directory.Exists(Path.GetDirectoryName(FilePath)))
                    Thread.Sleep(100);

                Plugin.Log("FilePath exists! Continuing initialization!");

                if (File.Exists(FilePath))
                {
                    Load();
                }
                Save();

                _configWatcher.Path = Path.GetDirectoryName(FilePath);
                _configWatcher.NotifyFilter = NotifyFilters.LastWrite;
                _configWatcher.Filter = $"RequestBotSettings.ini";
                _configWatcher.EnableRaisingEvents = true;

                _configWatcher.Changed += ConfigWatcherOnChanged;
            });
        }

        ~RequestBotConfig()
        {
            _configWatcher.Changed -= ConfigWatcherOnChanged;
        }

        public void Load()
        {
            ConfigSerializer.LoadConfig(this, FilePath);
        }

        public void Save(bool callback = false)
        {
            if (!callback)
                _saving = true;
            ConfigSerializer.SaveConfig(this, FilePath);
        }

        private void ConfigWatcherOnChanged(object sender, FileSystemEventArgs fileSystemEventArgs)
        {
            if (_saving)
            {
                _saving = false;
                return;
            }

            Load();

            if (ConfigChangedEvent != null)
            {
                ConfigChangedEvent(this);
            }
        }
    }
}
