using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Threading.Tasks;
using ChatCore.Models.Twitch;
using ChatCore.SimpleJSON;

namespace SongRequestManager
{
    public partial class RequestBot : MonoBehaviour
    {
        // BUG: This one needs to be cleaned up a lot imo
        // BUG: This file needs to be split up a little, but not just yet... Its easier for me to move around in one massive file, since I can see the whole thing at once. 

        static StringBuilder AddSongToQueueText = new StringBuilder( "Request %songName% %songSubName%/%authorName% %Rating% (%version%) added to queue.");
        static StringBuilder LookupSongDetail= new StringBuilder ("%songName% %songSubName%/%authorName% %Rating% (%version%)");
        static StringBuilder BsrSongDetail=new StringBuilder ("%songName% %songSubName%/%authorName% %Rating% (%version%)");
        static StringBuilder LinkSonglink=new StringBuilder ("%songName% %songSubName%/%authorName% %Rating% (%version%) %BeatsaverLink%");
        static StringBuilder NextSonglink=new StringBuilder ("%songName% %songSubName%/%authorName% %Rating% (%version%) requested by %user% is next.");
        static public  StringBuilder SongHintText=new StringBuilder ("Requested by %user%%LF%Status: %Status%%Info%%LF%%LF%<size=60%>Request Time: %RequestTime%</size>");
        static StringBuilder QueueTextFileFormat=new StringBuilder ("%songName%%LF%");         // Don't forget to include %LF% for these. 

        static public StringBuilder QueueListRow2 = new StringBuilder("%authorName% (%id%) <color=white>%songlength%</color>");

        static StringBuilder BanSongDetail = new StringBuilder("Blocking %songName%/%authorName% (%version%)");

        static StringBuilder QueueListFormat= new StringBuilder("%songName% (%version%)");
        static StringBuilder HistoryListFormat = new StringBuilder("%songName% (%version%)");

        static StringBuilder AddSortOrder = new StringBuilder("-rating +id");
        static StringBuilder LookupSortOrder = new StringBuilder("-rating +id");
        static StringBuilder AddSongsSortOrder = new StringBuilder("-rating +id");

        #region Utility functions

        public string Variable(ParseState state) // Basically show the value of a variable without parsing
            {
            QueueChatMessage(state.botcmd.userParameter.ToString());
            return "";
            }

        public static int MaximumTwitchMessageLength
            {
            get
                {
                return 498- RequestBotConfig.Instance.BotPrefix.Length;
                }
            }

        public string ChatMessage(ParseState state)
        {
            var dt = new DynamicText().AddUser(ref state.user);
            try
            {
                dt.AddSong(RequestHistory.Songs[0].song); // Exposing the current song 
            }
            catch (Exception ex)
            {
                Plugin.Log(ex.ToString());
            }

            dt.QueueMessage(state.parameter);
            return success;
        }

        public void RunScript(TwitchUser requestor, string request)
        {
            listcollection.runscript(request);
        }

        public static TimeSpan GetFileAgeDifference(string filename)
        {
            DateTime lastModified = System.IO.File.GetLastWriteTime(filename);
            return DateTime.Now - lastModified;
        }

        public class QueueLongMessage
        {
            private StringBuilder msgBuilder = new StringBuilder();
            private int messageCount = 1;
            private int maxMessages = 2;
            int maxoverflowtextlength = 60; // We don't know ahead of time, so we're going to do a safe estimate. 

            private int maxoverflowpoint = 0; // The offset in the string where the overflow message needs to go
            private int overflowcount = 0; // We need to save Count
            private int separatorlength = 0;
            public int Count = 0;

            // BUG: This version doesn't reallly strings > twitchmessagelength well, will support

            public QueueLongMessage(int maximummessageallowed = 2, int maxoverflowtext = 60) // Constructor supports setting max messages
            {
                maxMessages = maximummessageallowed;
                maxoverflowtextlength = maxoverflowtext;
            }

            public void Header(string text)
            {
                msgBuilder.Append(text);
            }

            // BUG: Only works form string < MaximumTwitchMessageLength
            public bool Add(string text, string separator = "") // Make sure you use Header(text) for your initial nonlist message, or your displayed message count will be wrong.
            {

                // Save the point where we would put the overflow message
                if (messageCount >= maxMessages && maxoverflowpoint == 0 && msgBuilder.Length + text.Length > MaximumTwitchMessageLength - maxoverflowtextlength)
                {
                    maxoverflowpoint = msgBuilder.Length - separatorlength;
                    overflowcount = Count;
                }

                if (msgBuilder.Length + text.Length > MaximumTwitchMessageLength)
                {
                    messageCount++;

                    if (maxoverflowpoint > 0)
                    {
                        msgBuilder.Length = maxoverflowpoint;
                        Count = overflowcount;
                        return true;
                    }
                    RequestBot.Instance.QueueChatMessage(msgBuilder.ToString(0, msgBuilder.Length - separatorlength));
                    msgBuilder.Clear();
                }

                Count++;
                msgBuilder.Append(text);
                msgBuilder.Append(separator);
                separatorlength = separator.Length;

                return false;
            }

            public void end(string overflowtext = "", string emptymsg = "")
            {
                if (Count == 0)
                    RequestBot.Instance.QueueChatMessage(emptymsg); // Note, this means header doesn't get printed either for empty lists                
                else if (messageCount > maxMessages && overflowcount > 0)
                    RequestBot.Instance.QueueChatMessage(msgBuilder.ToString() + overflowtext);
                else
                {
                    msgBuilder.Length -= separatorlength;
                    RequestBot.Instance.QueueChatMessage(msgBuilder.ToString());
                }

                // Reset the class for reuse
                maxoverflowpoint = 0;
                messageCount = 1;
                msgBuilder.Clear();
            }
        }

        #endregion

        #region Filter support functions

        private bool DoesContainTerms(string request, ref string[] terms)
        {
            if (request == "") return false;
            request = request.ToLower();

            foreach (string term in terms)
                foreach (string word in request.Split(' '))
                    if (word.Length > 2 && term.ToLower().Contains(word)) return true;

            return false;
        }


        bool isNotModerator(TwitchUser requestor, string message = "")
        {
            if (requestor.IsBroadcaster || requestor.IsModerator) return false;
            if (message != "") QueueChatMessage($"{message} is moderator only.");
            return true;
        }

        private bool filtersong(JSONObject song)
        {
            string songid = song["id"].Value;
            if (IsInQueue(songid)) return true;
            if (listcollection.contains(ref banlist,songid)) return true;
            if (listcollection.contains(ref duplicatelist, songid)) return true;
            return false;
        }

        // Returns error text if filter triggers, or "" otherwise, "fast" version returns X if filter triggers

        [Flags] enum SongFilter { none = 0, Queue = 1, Blacklist = 2, Mapper = 4, Duplicate = 8, Remap = 16, Rating = 32, Duration=64,NJS=128,All = -1 };

        private string SongSearchFilter(JSONObject song, bool fast = false, SongFilter filter = SongFilter.All) // BUG: This could be nicer
        {
            string songid = song["id"].Value;
            if (filter.HasFlag(SongFilter.Queue) && RequestQueue.Songs.Any(req => req.song["version"] == song["version"])) return fast ? "X" : $"Request {song["songName"].Value} by {song["authorName"].Value} already exists in queue!";

            if (filter.HasFlag(SongFilter.Blacklist) && listcollection.contains(ref banlist,songid)) return fast ? "X" : $"{song["songName"].Value} by {song["authorName"].Value} ({song["version"].Value}) is banned!";

            if (filter.HasFlag(SongFilter.Mapper) &&  mapperfiltered(song,_mapperWhitelist)) return fast ? "X" : $"{song["songName"].Value} by {song["authorName"].Value} does not have a permitted mapper!";

            if (filter.HasFlag(SongFilter.Duplicate) && listcollection.contains(ref duplicatelist, songid)) return fast ? "X" : $"{song["songName"].Value} by  {song["authorName"].Value} already requested this session!";

            if (listcollection.contains(ref _whitelist, songid)) return "";

            if (filter.HasFlag(SongFilter.Duration) && song["songduration"].AsFloat > RequestBotConfig.Instance.MaximumSongLength*60) return fast ? "X" : $"{song["songName"].Value} ({song["songlength"].Value}) by {song["authorName"].Value} ({song["version"].Value}) is too long!";

            if (filter.HasFlag(SongFilter.NJS) && song["njs"].AsInt < RequestBotConfig.Instance.MinimumNJS) return fast ? "X" : $"{song["songName"].Value} ({song["songlength"].Value}) by {song["authorName"].Value} ({song["version"].Value}) NJS ({song["njs"].Value}) is too low!";

            if (filter.HasFlag(SongFilter.Remap) && songremap.ContainsKey(songid)) return fast ? "X" : $"no permitted results found!";

            if (filter.HasFlag(SongFilter.Rating) && song["rating"].AsFloat < RequestBotConfig.Instance.LowestAllowedRating && song["rating"] != 0) return fast ? "X" : $"{song["songName"].Value} by {song["authorName"].Value} is below {RequestBotConfig.Instance.LowestAllowedRating}% rating!";

            return "";
        }

        // checks if request is in the RequestQueue.Songs - needs to improve interface
        private string IsRequestInQueue(string request, bool fast = false)
        {
            string matchby = "";
            if (_beatSaverRegex.IsMatch(request)) matchby = "version";
            else if (_digitRegex.IsMatch(request)) matchby = "id";
            if (matchby == "") return fast ? "X" : $"Invalid song id {request} used in RequestInQueue check";

            foreach (SongRequest req in RequestQueue.Songs.ToArray())
            {
                var song = req.song;
                if (song[matchby].Value == request) return fast ? "X" : $"Request {song["songName"].Value} by {song["authorName"].Value} ({song["version"].Value}) already exists in queue!";
            }
            return ""; // Empty string: The request is not in the RequestQueue.Songs
        }

        bool IsInQueue(string request) // unhappy about naming here
        {
            return !(IsRequestInQueue(request) == "");
        }

        private string ClearDuplicateList(ParseState state)
        {
            if (!state.botcmd.Flags.HasFlag(CmdFlags.SilentResult)) QueueChatMessage("Session duplicate list is now clear.");
            listcollection.ClearList(duplicatelist);
            return success;
        }
        #endregion

        #region Ban/Unban Song
        //public void Ban(TwitchUser requestor, string request)
        //{
        //    Ban(requestor, request, false);
        //}

        private async Task Ban(ParseState state)
        {
            var id = GetBeatSaverId(state.parameter.ToLower());

            if (listcollection.contains(ref banlist, id))
            {
                QueueChatMessage($"{id} is already on the ban list.");
                return;
            }

            if (!MapDatabase.MapLibrary.TryGetValue(id, out SongMap song))
            {
                JSONNode result = null;

                if (!RequestBotConfig.Instance.OfflineMode)
                {
                    var requestUrl = $"https://beatsaver.com/api/maps/detail/{id}";
                    var resp = await Plugin.WebClient.GetAsync(requestUrl, System.Threading.CancellationToken.None);

                    if (resp.IsSuccessStatusCode)
                    {
                        result = resp.ConvertToJsonNode();
                    }
                    else
                    {
                        Plugin.Log($"Ban: Error {resp.ReasonPhrase} occured when trying to request song {requestUrl}!");
                    }
                }

                if (result != null) song = new SongMap(result.AsObject);
            }

            listcollection.add(banlist, id);

            if (song == null)
            {
                QueueChatMessage($"{id} is now on the ban list.");
            }
            else
            {
                state.msg(new DynamicText().AddSong(ref song.song).Parse(BanSongDetail), ", ");
            }   
        }

        //public void Ban(TwitchUser requestor, string request, bool silence)
        //{
        //    if (isNotModerator(requestor)) return;

        //    var songId = GetBeatSaverId(request);
        //    if (songId == "" && !silence)
        //    {
        //        QueueChatMessage($"usage: !block <songid>, omit <>'s.");
        //        return;
        //    }

        //    if (listcollection.contains(ref banlist,songId) && !silence)
        //    {
        //        QueueChatMessage($"{request} is already on the ban list.");
        //    }
        //    else
        //    {

        //        listcollection.add(banlist, songId);
        //        QueueChatMessage($"{request} is now on the ban list.");

        //    }
        //}

        private void Unban(TwitchUser requestor, string request)
        {
            var unbanvalue = GetBeatSaverId(request);

            if (listcollection.contains(ref banlist,unbanvalue))
            {
                QueueChatMessage($"Removed {request} from the ban list.");
                listcollection.remove(banlist,unbanvalue);
            }
            else
            {
                QueueChatMessage($"{request} is not on the ban list.");
            }
        }
        #endregion

        #region Deck Commands
        private string restoredeck(ParseState state)
        {
            return Readdeck(new ParseState(state,"savedqueue"));
        }

        private void Writedeck(TwitchUser requestor, string request)
        {
            try
            {
                int count = 0;
                if (RequestQueue.Songs.Count == 0)
                {
                    QueueChatMessage("Queue is empty  .");
                    return;
                }

                string queuefile = Path.Combine(Plugin.DataPath, request + ".deck");
                StreamWriter fileWriter = new StreamWriter(queuefile);

                foreach (SongRequest req in RequestQueue.Songs.ToArray())
                {
                    var song = req.song;
                    if (count > 0) fileWriter.Write(",");
                    fileWriter.Write(song["id"].Value);
                    count++;
                }

                fileWriter.Close();
                if (request != "savedqueue") QueueChatMessage($"wrote {count} entries to {request}");
            }
            catch
            {
                QueueChatMessage("Was unable to write {queuefile}.");
            }
        }

        private string Readdeck(ParseState state)
        {
            try
            {
                string queuefile = Path.Combine(Plugin.DataPath, state.parameter + ".deck");
                string fileContent = File.ReadAllText(queuefile);
                string[] integerStrings = fileContent.Split(new char[] { ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                for (int n = 0; n < integerStrings.Length; n++)
                {
                    if (IsInQueue(integerStrings[n])) continue;

                    ParseState newstate = new ParseState(state); // Must use copies here, since these are all threads
                    newstate.parameter = integerStrings[n];
                    ProcessSongRequest(newstate);
                }
            }
            catch
            {
                QueueChatMessage("Unable to read deck {request}.");
            }

        return success;
        }
        #endregion

        #region Dequeue Song
        private string DequeueSong(ParseState state)
        {

            var songId = GetBeatSaverId(state.parameter);
            for (int i = RequestQueue.Songs.Count - 1; i >= 0; i--)
            {
                bool dequeueSong = false;
                var song = RequestQueue.Songs[i].song;

                if (songId == "")
                {
                    string[] terms = new string[] { song["songName"].Value, song["songSubName"].Value, song["authorName"].Value, song["version"].Value, RequestQueue.Songs[i].requestor.DisplayName };

                    if (DoesContainTerms(state.parameter, ref terms))
                        dequeueSong = true;
                }
                else
                {
                    if (song["id"].Value == songId)
                        dequeueSong = true;
                }

                if (dequeueSong)
                {
                    QueueChatMessage($"{song["songName"].Value} ({song["version"].Value}) removed.");
                    RequestBot.Skip(i);
                    return success;
                }
            }
            return $"{state.parameter} was not found in the queue.";
        }
        #endregion


        // BUG: Will use a new interface to the list manager
        private void MapperAllowList(TwitchUser requestor, string request)
        {
            string key = request.ToLower();
            mapperwhitelist = listcollection.OpenList(key); // BUG: this is still not the final interface
            QueueChatMessage($"Mapper whitelist set to {request}.");
        }

        private void MapperBanList(TwitchUser requestor, string request)
        {
            string key = request.ToLower();
            mapperBanlist = listcollection.OpenList(key);
            //QueueChatMessage($"Mapper ban list set to {request}.");
        }

        private void WhiteList(TwitchUser requestor, string request)
        {
            string key = request.ToLower();
            Whitelist = listcollection.OpenList(key);
        }

        private void BlockedUserList(TwitchUser requestor, string request)
        {
            string key = request.ToLower();
            BlockedUser = listcollection.OpenList(key);
        }

        // Not super efficient, but what can you do
        private bool mapperfiltered(JSONObject song,bool white)
        {
            string normalizedauthor = song["metadata"]["levelAuthorName"].Value.ToLower();
            if (white && mapperwhitelist.list.Count > 0)
            {
                foreach (var mapper in mapperwhitelist.list)
                {
                    if (normalizedauthor.Contains(mapper)) return false;
                }
                return true;
            }

            foreach (var mapper in mapperBanlist.list)
            {
                if (normalizedauthor.Contains(mapper)) return true;
            }

            return false;
        }

        // return a songrequest match in a SongRequest list. Good for scanning Queue or History
        SongRequest FindMatch(List<SongRequest> queue, string request,QueueLongMessage qm)
        {
            var songId = GetBeatSaverId(request);

            SongRequest result = null;

            string lastuser = "";
            foreach (var entry in queue)
            {
                var song = entry.song;

                if (songId == "")
                {
                    string[] terms = new string[] { song["songName"].Value, song["songSubName"].Value, song["authorName"].Value, song["levelAuthor"].Value, song["version"].Value, entry.requestor.DisplayName };

                    if (DoesContainTerms(request, ref terms))
                        {
                        result = entry;

                        if (lastuser != result.requestor.DisplayName) qm.Add($"{result.requestor.DisplayName}: ");
                        qm.Add($"{result.song["songName"].Value} ({result.song["version"].Value})", ",");
                        lastuser = result.requestor.DisplayName;
                        }
                }
                else
                {
                    if (song["id"].Value == songId)
                    {
                        result = entry;
                        qm.Add($"{result.requestor.DisplayName}: {result.song["songName"].Value} ({result.song["version"].Value})");
                        return entry;
                    }
                }
            }
            return result;
        }

        public string ClearEvents(ParseState state)
        {
            BotEvent.Clear();
            return success;
        }    

        public string Every(ParseState state)
        {
            float period;

            string[] parts = state.parameter.Split(new char[] { ' ', ',' }, 2);

            if (!float.TryParse(parts[0], out period)) return state.error($"You must specify a time in minutes after {state.command}.");
            if (period < 1) return state.error($"You must specify a period of at least 1 minute");
            new BotEvent(TimeSpan.FromMinutes(period), parts[1], true);
            return success;
        }

        public string EventIn(ParseState state)
        {
            float period;
            string[] parts = state.parameter.Split(new char[] { ' ', ',' }, 2);

            if (!float.TryParse(parts[0], out period)) return state.error($"You must specify a time in minutes after {state.command}.");
            if (period < 0) return state.error($"You must specify a period of at least 0 minutes");
            new BotEvent(TimeSpan.FromMinutes(period), parts[1], false);
            return success;
        }
        public string Who(ParseState state)
        {

            var qm = new QueueLongMessage();
            SongRequest result = null;
            result = FindMatch(RequestQueue.Songs, state.parameter,qm);
            if (result == null) result = FindMatch(RequestHistory.Songs, state.parameter, qm);

            //if (result != null) QueueChatMessage($"{result.song["songName"].Value} requested by {result.requestor.displayName}.");
            if (result != null) qm.end("...");
            return "";
        }

        public string SongMsg(ParseState state)
        {
            string[] parts = state.parameter.Split(new char[] { ' ', ',' }, 2);
            var songId = GetBeatSaverId(parts[0]);
            if (songId == "") return state.helptext(true);
        
           foreach (var entry in RequestQueue.Songs)
           {
                var song = entry.song;

                if (song["id"].Value == songId)
                {
                    entry.requestInfo = "!"+parts[1];
                    QueueChatMessage($"{song["songName"].Value} : {parts[1]}");
                    return success;
                }
            }
            QueueChatMessage($"Unable to find {songId}");
            return success;
        }

        private IEnumerator SetBombState(ParseState state)
        {
            state.parameter = state.parameter.ToLower();

            if (state.parameter == "on") state.parameter = "enable";
            if (state.parameter == "off") state.parameter = "disable";

            if (state.parameter != "enable" && state.parameter != "disable")
            {
                state.msg(state.botcmd.ShortHelp);
                yield break;
            }

            //System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo($"liv-streamerkit://gamechanger/beat-saber-sabotage/{state.parameter}"));

            System.Diagnostics.Process.Start($"liv-streamerkit://gamechanger/beat-saber-sabotage/{state.parameter}");

            //if (_WobbleInstalled)
            //   {
            //    string wobblestate = "off";
            //    if (state.parameter == "enable") wobblestate = "on"; 
            //   TwitchWebSocketClient.SendCommand($"!wadmin toggle {wobblestate} ");


            //}

            state.msg($"The !bomb command is now {state.parameter}d.");

            yield break;
        }


        private async Task addsongsFromnewest(ParseState state)
        {
            int totalSongs = 0;

            string requestUrl = "https://beatsaver.com/api/maps/latest";

            //if (RequestBotConfig.Instance.OfflineMode) return;

            int offset = 0;

            listcollection.ClearList("latest.deck");

            //state.msg($"Flags: {state.flags}");

            while (offset < RequestBotConfig.Instance.MaxiumScanRange) // MaxiumAddScanRange
            {
                var resp = await Plugin.WebClient.GetAsync($"{requestUrl}/{offset}", System.Threading.CancellationToken.None);

                if (resp.IsSuccessStatusCode)
                {
                    var result = resp.ConvertToJsonNode();
                    if (result["docs"].IsArray && result["totalDocs"].AsInt == 0)
                    {
                        return;
                    }

                    if (result["docs"].IsArray)
                    {
                        foreach (JSONObject entry in result["docs"])
                        {
                            JSONObject song = entry;
                            new SongMap(song);

                            if (mapperfiltered(song, true)) continue; // This forces the mapper filter
                            if (filtersong(song)) continue;

                            if (state.flags.HasFlag(CmdFlags.Local)) QueueSong(state, song);
                            listcollection.add("latest.deck", song["id"].Value);
                            totalSongs++;
                        }
                    }

                }
                else
                {
                    Plugin.Log($"Error {resp.ReasonPhrase} occured when trying to request song {requestUrl}!");
                    return;
                }

                offset += 1; // Magic beatsaver.com skip constant.
            }

            if (totalSongs == 0)
            {
                //QueueChatMessage($"No new songs found.");
            }
            else
            {
#if UNRELEASED
                COMMAND.Parse(ChatHandler.Self, "!deck latest",state.flags);
#endif

                if (state.flags.HasFlag(CmdFlags.Local))
                {
                    Dispatcher.RunOnMainThread(() =>
                    {
                        UpdateRequestUI();
                        _refreshQueue = true;
                    });
                }
            }
        }

        private async Task makelistfromsearch(ParseState state)
        {
            int totalSongs = 0;

            var id = GetBeatSaverId(state.parameter);

            string requestUrl = (id != "") ? $"https://beatsaver.com/api/maps/detail/{normalize.RemoveSymbols(ref state.parameter, normalize._SymbolsNoDash)}" : $"https://beatsaver.com/api/search/text";

            if (RequestBotConfig.Instance.OfflineMode) return;

            int offset = 0;

            listcollection.ClearList("search.deck");

            //state.msg($"Flags: {state.flags}");

            while (offset < RequestBotConfig.Instance.MaxiumScanRange) // MaxiumAddScanRange
            {
                var resp = await Plugin.WebClient.GetAsync($"{requestUrl}/{offset}?q={state.parameter}", System.Threading.CancellationToken.None);

                if (resp.IsSuccessStatusCode)
                {
                    var result = resp.ConvertToJsonNode();
                    if (result["docs"].IsArray && result["totalDocs"].AsInt == 0)
                    {
                        return;
                    }

                    if (result["docs"].IsArray)
                    {
                        foreach (JSONObject entry in result["docs"])
                        {
                            JSONObject song = entry;
                            new SongMap(song);

                            if (mapperfiltered(song, true)) continue; // This forces the mapper filter
                            if (filtersong(song)) continue;

                            if (state.flags.HasFlag(CmdFlags.Local)) QueueSong(state, song);
                            listcollection.add("search.deck", song["id"].Value);
                            totalSongs++;
                        }
                    }
                }
                else
                {
                    Plugin.Log($"Error {resp.ReasonPhrase} occured when trying to request song {requestUrl}!");
                    return;
                }
                offset += 1; 
            }

            if (totalSongs == 0)
            {
                //QueueChatMessage($"No new songs found.");
            }
            else
            {
#if UNRELEASED
                COMMAND.Parse(ChatHandler.Self, "!deck search", state.flags);
#endif

                if (state.flags.HasFlag(CmdFlags.Local))
                {
                    Dispatcher.RunOnMainThread(() =>
                    {
                        UpdateRequestUI();
                        _refreshQueue = true;
                    });
                }
            }
        }

        // General search version
        private async Task addsongs(ParseState state)
        {
 
            var id = GetBeatSaverId(state.parameter);
            string requestUrl = (id != "") ? $"https://beatsaver.com/api/maps/detail/{normalize.RemoveSymbols(ref state.parameter, normalize._SymbolsNoDash)}" : $"https://beatsaver.com/api/search/text/0?q={state.request}";

            string errorMessage = "";

            if (RequestBotConfig.Instance.OfflineMode) requestUrl = "";

            JSONNode result = null;

            if (!RequestBotConfig.Instance.OfflineMode)
            {
                var resp = await Plugin.WebClient.GetAsync($"{requestUrl}/{normalize.NormalizeBeatSaverString(state.parameter)}", System.Threading.CancellationToken.None);

                if (resp.IsSuccessStatusCode)
                {
                    result = resp.ConvertToJsonNode();

                }
                else
                {
                    Plugin.Log($"Error {resp.ReasonPhrase} occured when trying to request song {state.parameter}!");
                    errorMessage = $"Invalid BeatSaver ID \"{state.parameter}\" specified.";
                }
            }

            SongFilter filter = SongFilter.All;
            if (state.flags.HasFlag(CmdFlags.NoFilter)) filter = SongFilter.Queue;
            List<JSONObject> songs = GetSongListFromResults(result,state.parameter, ref errorMessage, filter, state.sort != "" ? state.sort : LookupSortOrder.ToString(), -1);

            foreach (JSONObject entry in songs)
            {
                QueueSong(state, entry);
            }

            Dispatcher.RunOnMainThread(() =>
            {
                UpdateRequestUI();
                _refreshQueue = true;
            });
        }

        public static void QueueSong(ParseState state, JSONObject song)
        {
            if ((state.flags.HasFlag(CmdFlags.MoveToTop)))
                RequestQueue.Songs.Insert(0, new SongRequest(song, state.user, DateTime.UtcNow, RequestStatus.SongSearch, "search result"));
            else
                RequestQueue.Songs.Add(new SongRequest(song, state.user, DateTime.UtcNow, RequestStatus.SongSearch, "search result"));
        }

        #region Move Request To Top/Bottom

        private void MoveRequestToTop(TwitchUser requestor, string request)
        {
            MoveRequestPositionInQueue(requestor, request, true);
        }

        private void MoveRequestToBottom(TwitchUser requestor, string request)
        {
            MoveRequestPositionInQueue(requestor, request, false);
        }

        private void MoveRequestPositionInQueue(TwitchUser requestor, string request, bool top)
        {

            string moveId = GetBeatSaverId(request);
            for (int i = RequestQueue.Songs.Count - 1; i >= 0; i--)
            {
                SongRequest req = RequestQueue.Songs.ElementAt(i);
                var song = req.song;

                bool moveRequest = false;
                if (moveId == "")
                {
                    string[] terms = new string[] { song["songName"].Value, song["songSubName"].Value, song["authorName"].Value, song["levelAuthor"].Value, song["version"].Value, RequestQueue.Songs[i].requestor.DisplayName };
                    if (DoesContainTerms(request, ref terms))
                        moveRequest = true;
                }
                else
                {
                    if (song["id"].Value == moveId)
                        moveRequest = true;
                }

                if (moveRequest)
                {
                    // Remove the request from the queue
                    RequestQueue.Songs.RemoveAt(i);

                    // Then readd it at the appropriate position
                    if (top)
                        RequestQueue.Songs.Insert(0, req);
                    else
                        RequestQueue.Songs.Add(req);

                    // Write the modified request queue to file
                    RequestQueue.Write();

                    // Refresh the queue ui
                    _refreshQueue = true;

                    // And write a summary to file
                    WriteQueueSummaryToFile();

                    QueueChatMessage($"{song["songName"].Value} ({song["version"].Value}) {(top ? "promoted" : "demoted")}.");
                    return;
                }
            }
            QueueChatMessage($"{request} was not found in the queue.");
        }
        #endregion

        #region List Commands

        private void showCommandlist(TwitchUser requestor, string request)
        {

            var msg = new QueueLongMessage();

            foreach (var entry in COMMAND.aliaslist)
            {
                var botcmd = entry.Value;
                // BUG: Please refactor this its getting too damn long
                if (HasRights(ref botcmd, ref requestor,0) && !botcmd.Flags.HasFlag(Var) && !botcmd.Flags.HasFlag(Subcmd)) msg.Add($"{entry.Key}", " "); // Only show commands you're allowed to use
            }
            msg.end("...", $"No commands available.");
        }

        private void showFormatList (TwitchUser requestor, string request)
        {

            var msg = new QueueLongMessage();

            foreach (var entry in COMMAND.aliaslist)
            {
                var botcmd = entry.Value;
                // BUG: Please refactor this its getting too damn long
                if (HasRights(ref botcmd, ref requestor,0) && botcmd.Flags.HasFlag(Var) ) msg.Add($"{entry.Key}", ", "); // Only show commands you're allowed to use
            }
            msg.end("...", $"No commands available.");
        }


        private async Task LookupSongs(ParseState state)
        {                
   
            var id = GetBeatSaverId(state.parameter);

            JSONNode result = null;
            
            if (!RequestBotConfig.Instance.OfflineMode)
            {
                string requestUrl = (id != "") ? $"https://beatsaver.com/api/maps/detail/{id}" : $"https://beatsaver.com/api/search/text/0?q={normalize.NormalizeBeatSaverString(state.parameter)}";
                var resp = await Plugin.WebClient.GetAsync(requestUrl, System.Threading.CancellationToken.None);

                if (resp.IsSuccessStatusCode)
                {
                    result = resp.ConvertToJsonNode();
                }
                else
                {
                    Plugin.Log($"Error {resp.ReasonPhrase} occured when trying to request song {requestUrl}!");
                }
            }

            string errorMessage="";
            SongFilter filter = SongFilter.none;
            if (state.flags.HasFlag(CmdFlags.NoFilter)) filter = SongFilter.Queue;
            List<JSONObject> songs = GetSongListFromResults(result,state.parameter, ref errorMessage,filter,state.sort!="" ? state.sort : LookupSortOrder.ToString());

            JSONObject song;

            var msg = new QueueLongMessage(1, 5); // One message maximum, 5 bytes reserved for the ...
            msg.Header($"{songs.Count} found: ");
            foreach (JSONObject entry in songs)
            {
                //entry.Add("pp", 100);
                //SongBrowserPlugin.DataAccess.ScoreSaberDataFile

                song = entry;
                msg.Add(new DynamicText().AddSong(ref song).Parse(LookupSongDetail), ", ");
            }

            msg.end("...", $"No results for {state.parameter}");
        }

        // BUG: Should be dynamic text
        private void ListQueue(TwitchUser requestor, string request)
        {

            var msg = new QueueLongMessage(RequestBotConfig.Instance.maximumqueuemessages);

            foreach (SongRequest req in RequestQueue.Songs.ToArray())
            {
                var song = req.song;
                if (msg.Add(new DynamicText().AddSong(ref song).Parse(QueueListFormat), ", ")) break;
            }
            msg.end($" ... and {RequestQueue.Songs.Count - msg.Count} more songs.", "Queue is empty.");
            return;

        }

        private void ShowHistory(TwitchUser requestor, string request)
        {

            var msg = new QueueLongMessage(1);

            foreach (var entry in RequestHistory.Songs)
            {
                var song = entry.song;
                if (msg.Add(new DynamicText().AddSong(ref song).Parse(HistoryListFormat), ", ")) break;
            }
            msg.end($" ... and {RequestHistory.Songs.Count - msg.Count} more songs.", "History is empty.");
            return;

        }

        private void ShowSongsplayed(TwitchUser requestor, string request) // Note: This can be spammy.
        {
            var msg = new QueueLongMessage(2);

            msg.Header($"{played.Count} songs played tonight: ");

            foreach (JSONObject song in played)
            {
                if (msg.Add(song["songName"].Value + " (" + song["version"] + ")", ", ")) break;
            }
            msg.end($" ... and {played.Count - msg.Count} other songs.", "No songs have been played.");
            return;

        }

        private void ShowBanList(TwitchUser requestor, string request)
        {

            var msg = new QueueLongMessage(1);

            msg.Header("Banlist ");

            foreach (string songId in listcollection.OpenList("banlist.unique").list)
            {
                if (msg.Add(songId, ", ")) break;
            }
            msg.end($" ... and {listcollection.OpenList("banlist.unique").list.Count - msg.Count} more entries.", "is empty.");

        }

        #endregion

        #region Queue Related

        // This function existing to unify the queue message strings, and to allow user configurable QueueMessages in the future
        public static string QueueMessage(bool QueueState)
        {
            return QueueState ? "Queue is open" : "Queue is closed";
        }
        private string OpenQueue(ParseState state)
        {
            ToggleQueue(state.user, state.parameter, true);
            return success;
        }

        private string CloseQueue(ParseState state)
        {
            ToggleQueue(state.user, state.parameter, false);
            return success;
        }

        private void ToggleQueue(TwitchUser requestor, string request, bool state)
        {
            RequestBotConfig.Instance.RequestQueueOpen = state;
            RequestBotConfig.Instance.Save();

            QueueChatMessage(state ? "Queue is now open." : "Queue is now closed.");
            WriteQueueStatusToFile(QueueMessage(state));
            _refreshQueue = true;
        }
        private static void WriteQueueSummaryToFile()
        {

            if (!RequestBotConfig.Instance.UpdateQueueStatusFiles) return;

            try
            {
                string statusfile = Path.Combine(Plugin.DataPath, "queuelist.txt");
                StreamWriter fileWriter = new StreamWriter(statusfile);

                string queuesummary = "";
                int count = 0;

                foreach (SongRequest req in RequestQueue.Songs.ToArray())
                {
                    var song = req.song;
                    queuesummary += new DynamicText().AddSong(song).Parse(QueueTextFileFormat);  // Format of Queue is now user configurable

                    if (++count > RequestBotConfig.Instance.MaximumQueueTextEntries)
                    {
                        queuesummary += "...\n";
                        break;
                    }
                }

                fileWriter.Write(count > 0 ? queuesummary : "Queue is empty.");
                fileWriter.Close();
            }
            catch (Exception ex)
            {
                Plugin.Log(ex.ToString());
            }
        }

        public static void WriteQueueStatusToFile(string status)
        {
            try
            {
                string statusfile = Path.Combine(Plugin.DataPath, "queuestatus.txt");
                StreamWriter fileWriter = new StreamWriter(statusfile);
                fileWriter.Write(status);
                fileWriter.Close();
            }

            catch (Exception ex)
            {
                Plugin.Log(ex.ToString());
            }
        }

        public static void Shuffle<T>(List<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = generator.Next(0, n+1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        private string QueueLottery(ParseState state)
        {
            Int32.TryParse(state.parameter, out int entrycount);

            Shuffle(RequestQueue.Songs);

            var list = RequestQueue.Songs;
            for (int i = entrycount; i < list.Count; i++)
            {
                try
                {
                    if (RequestTracker.ContainsKey(list[i].requestor.Id)) RequestTracker[list[i].requestor.Id].numRequests--;
                    listcollection.remove(duplicatelist, list[i].song["id"]);
                }
                catch { }
            }

            if (entrycount > 0)
            {
                try
                {
                    Writedeck(state.user, "prelotto");
                    RequestQueue.Songs.RemoveRange(entrycount, RequestQueue.Songs.Count - entrycount);
                }
                catch { }
            }

            RequestQueue.Write();

            // Notify the chat that the queue was cleared
            QueueChatMessage($"Queue lottery complete!");

            ToggleQueue(state.user, state.parameter, false); // Close the queue.
            // Reload the queue
            UpdateRequestUI();
            _refreshQueue = true;
            return success;
        }

        private void Clearqueue(TwitchUser requestor, string request)
        {
            // Write our current queue to file so we can restore it if needed
            Writedeck(requestor, "justcleared");

            // Cycle through each song in the final request queue, adding them to the song history

            while (RequestQueue.Songs.Count > 0) DequeueRequest(0, false); // More correct now, previous version did not keep track of user requests 

            RequestQueue.Write();

            // Update the request button ui accordingly
            UpdateRequestUI();

            // Notify the chat that the queue was cleared
            QueueChatMessage($"Queue is now empty.");

            // Reload the queue
            _refreshQueue = true;
        }

        #endregion

        #region Unmap/Remap Commands
        private void Remap(TwitchUser requestor, string request)
        {
            string[] parts = request.Split(',', ' ');

            if (parts.Length < 2)
            {
                QueueChatMessage("usage: !remap <songid>,<songid>, omit the <>'s");
                return;
            }

            if (songremap.ContainsKey(parts[0])) songremap.Remove(parts[0]);
            songremap.Add(parts[0], parts[1]);
            QueueChatMessage($"Song {parts[0]} remapped to {parts[1]}");
            WriteRemapList();
        }

        private void Unmap(TwitchUser requestor, string request)
        {

            if (songremap.ContainsKey(request))
            {
                QueueChatMessage($"Remap entry {request} removed.");
                songremap.Remove(request);
            }
            WriteRemapList();
        }

        private void WriteRemapList()
        {

            // BUG: Its more efficient to write it in one call

            try
            {
                string remapfile = Path.Combine(Plugin.DataPath, "remap.list");

                StreamWriter fileWriter = new StreamWriter(remapfile);

                foreach (var entry in songremap)
                {
                    fileWriter.Write($"{entry.Key},{entry.Value}\n");
                }

                fileWriter.Close();
            }
            catch (Exception ex)
            {
                Plugin.Log(ex.ToString());
            }
        }

        private void ReadRemapList()
        {
            string remapfile = Path.Combine(Plugin.DataPath, "remap.list");

            try
            {
                string fileContent = File.ReadAllText(remapfile);

                string[] maps = fileContent.Split('\r', '\n');
                for (int i = 0; i < maps.Length; i++)
                {
                    string[] parts = maps[i].Split(',', ' ');
                    if (parts.Length > 1) songremap.Add(parts[0], parts[1]);
                }

            }
            catch (Exception ex)
            {
                Plugin.Log(ex.ToString());
            }
        }
        #endregion

        #region Wrong Song
        private void WrongSong(TwitchUser requestor, string request)
        {
            // Note: Scanning backwards to remove LastIn, for loop is best known way.
            for (int i = RequestQueue.Songs.Count - 1; i >= 0; i--)
            {
                var song = RequestQueue.Songs[i].song;
                if (RequestQueue.Songs[i].requestor.Id == requestor.Id)
                {
                    QueueChatMessage($"{song["songName"].Value} ({song["version"].Value}) removed.");

                    listcollection.remove(duplicatelist, song["id"].Value);
                    RequestBot.Skip(i,RequestStatus.Wrongsong);
                    return;
                }
            }
            QueueChatMessage($"You have no requests in the queue.");
        }
        #endregion

        // BUG: This requires a switch, or should be disabled for those who don't allow links
        private string ShowSongLink(ParseState state)
        {
            try  // We're accessing an element across threads, and currentsong doesn't need to be defined
            {
                var song = RequestHistory.Songs[0].song;
                if (!song.IsNull) new DynamicText().AddSong(ref song).QueueMessage(LinkSonglink.ToString());
            }
            catch (Exception ex)
            {
                Plugin.Log(ex.ToString());
            }

            return success;
        }

        public string queueduration()
        {
            int total = 0;
            try
            {
                foreach (var songrequest in RequestQueue.Songs)
                {
                    total += songrequest.song["songduration"];
                }
            }
            catch
            {


            }

            return $"{total / 60}:{ total % 60:00}";
        }

        private string QueueStatus(ParseState state)
        {
            string queuestate = RequestBotConfig.Instance.RequestQueueOpen ? "Queue is open. " : "Queue is closed. ";
            QueueChatMessage($"{queuestate} There are {RequestQueue.Songs.Count} maps ({queueduration()}) in the queue.");
            return success;
        }

        public static string GetStarRating(ref JSONObject song, bool mode = true)
        {
            if (!mode) return "";

            string stars = "******";
            float rating = song["rating"].AsFloat;
            if (rating < 0 || rating > 100) rating = 0;
            string starrating = stars.Substring(0, (int)(rating / 17)); // 17 is used to produce a 5 star rating from 80ish to 100.
            return starrating;
        }

        public static string GetRating(ref JSONObject song, bool mode = true)
        {
            if (!mode) return "";

            string rating = song["rating"].AsInt.ToString();
            if (rating == "0") return "";
            return rating + '%';
        }

        #region DynamicText class and support functions.

        public class DynamicText
        {
            public Dictionary<string, string> dynamicvariables = new Dictionary<string, string>();  // A list of the variables available to us, we're using a list of pairs because the match we use uses BeginsWith,since the name of the string is unknown. The list is very short, so no biggie

            public bool AllowLinks = true;


            public DynamicText Add(string key, string value)
            {
                dynamicvariables.Add(key, value); // Make the code slower but more readable :(
                return this;
            }

            public DynamicText()
            {
                Add("|", ""); // This is the official section separator character, its used in help to separate usage from extended help, and because its easy to detect when parsing, being one character long

                // BUG: Note -- Its my intent to allow sections to be used as a form of conditional. If a result failure occurs within a section, we should be able to rollback the entire section, and continue to the next. Its a better way of handline missing dynamic fields without excessive scripting
                // This isn't implemented yet.

                AddLinks();

                DateTime Now = DateTime.Now; //"MM/dd/yyyy hh:mm:ss.fffffff";         
                Add("SRM", "Song Request Manager");
                Add("Time", Now.ToString("hh:mm"));
                Add("LongTime", Now.ToString("hh:mm:ss"));
                Add("Date", Now.ToString("yyyy/MM/dd"));
                Add("LF", "\n"); // Allow carriage return

            }

            public DynamicText AddUser(ref TwitchUser user)
            {
                try
                {
                    Add("user", user.DisplayName);
                }
                catch   
                {
                // Don't care. Twitch user doesn't HAVE to be defined.
                }
                return this;
            }

            public DynamicText AddLinks()
            {
                if (AllowLinks)
                {
                    Add("beatsaver", "https://beatsaver.com");
                    Add("beatsaber", "https://beatsaber.com");
                    Add("scoresaber", "https://scoresaber.com");
                }
                else
                {
                    Add("beatsaver", "beatsaver site");
                    Add("beatsaver", "beatsaber site");
                    Add("scoresaber", "scoresaber site");
                }

                return this;
            }


            public DynamicText AddBotCmd(ref COMMAND botcmd)
            {

                StringBuilder aliastext = new StringBuilder();
                foreach (var alias in botcmd.aliases) aliastext.Append($"{alias} ");
                Add("alias", aliastext.ToString());

                aliastext.Clear();
                aliastext.Append('[');
                aliastext.Append(botcmd.Flags & CmdFlags.TwitchLevel).ToString();
                aliastext.Append(']');
                Add("rights", aliastext.ToString());
                return this;
            }

            // Adds a JSON object to the dictionary. You can define a prefix to make the object identifiers unique if needed.
            public DynamicText AddJSON(ref JSONObject json, string prefix = "")
            {
                foreach (var element in json) Add(prefix + element.Key, element.Value);
                return this;
            }

            public DynamicText AddSong(JSONObject json, string prefix = "") // Alternate call for direct object
            {
                return AddSong(ref json, prefix);
            }

            public DynamicText AddSong(ref JSONObject song, string prefix = "")
            {
                AddJSON(ref song, prefix); // Add the song JSON

                //SongMap map;
                //if (MapDatabase.MapLibrary.TryGetValue(song["version"].Value, out map) && map.pp>0)
                //{
                //    Add("pp", map.pp.ToString());
                //}
                //else
                //{
                //    Add("pp", "");
                //}


                if (song["pp"].AsFloat > 0) Add("PP", song["pp"].AsInt.ToString() + " PP"); else Add("PP", "");

                Add("StarRating", GetStarRating(ref song)); // Add additional dynamic properties
                Add("Rating", GetRating(ref song));
                Add("BeatsaverLink", $"https://beatsaver.com/beatmap/{song["id"].Value}");
                Add("BeatsaberLink", $"https://bsaber.com/songs/{song["id"].Value}");
                return this;
            }

            public string Parse(string text, bool parselong = false) // We implement a path for ref or nonref
            {
                return Parse(ref text, parselong);
            }

            public string Parse(StringBuilder text, bool parselong = false) // We implement a path for ref or nonref
            {
                return Parse(text.ToString(), parselong);
            }

            // Refactor, supports %variable%, and no longer uses split, should be closer to c++ speed.
            public string Parse(ref string text, bool parselong = false)
            {
                StringBuilder output = new StringBuilder(text.Length); // We assume a starting capacity at LEAST = to length of original string;

                for (int p = 0; p < text.Length; p++) // P is pointer, that's good enough for me
                {
                    char c = text[p];
                    if (c == '%')
                    {
                        int keywordstart = p + 1;
                        int keywordlength = 0;

                        int end = Math.Min(p + 32, text.Length); // Limit the scan for the 2nd % to 32 characters, or the end of the string
                        for (int k = keywordstart; k < end; k++) // Pretty sure there's a function for this, I'll look it up later
                        {
                            if (text[k] == '%')
                            {
                                keywordlength = k - keywordstart;
                                break;
                            }
                        }

                        string substitutetext;

                        if (keywordlength > 0 && keywordlength != 0 && dynamicvariables.TryGetValue(text.Substring(keywordstart, keywordlength), out substitutetext))
                        {

                            if (keywordlength == 1 && !parselong) return output.ToString(); // Return at first sepearator on first 1 character code. 

                            output.Append(substitutetext);

                            p += keywordlength + 1; // Reset regular text
                            continue;
                        }
                    }
                    output.Append(c);
                }

                return output.ToString();
            }

            public DynamicText QueueMessage(string text, bool parselong = false)
            {
                QueueMessage(ref text, parselong);
                return this;
            }


            public DynamicText QueueMessage(ref string text, bool parselong = false)
            {
                Instance.QueueChatMessage(Parse(ref text, parselong));
                return this;
            }

        }

        #endregion
    }
}