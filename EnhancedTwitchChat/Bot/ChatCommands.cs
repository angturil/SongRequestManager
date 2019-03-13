using EnhancedTwitchChat.Chat;
using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace EnhancedTwitchChat.Bot
{
    public partial class RequestBot : MonoBehaviour
    {
        // BUG: This one needs to be cleaned up a lot imo
        // BUG: This file needs to be split up a little, but not just yet... Its easier for me to move around in one massive file, since I can see the whole thing at once. 

        // *** WARNING ***
        // These are here for this release, since we're not yet ready to support users customizing this. 
        // The final location and naming of these variables is not yet decided. I've moved them out of config to avoid
        // Filling the configuration file with variables that will no longer work in the future, and confuse the end user trying
        // to edit things that are located elsewhere. If you already have these in your .ini, and run this build, you should
        // Probably remove them to avoid future confusion.

        public static string AddSongToQueueText = "Request %songName% %songSubName%/%authorName% %Rating% (%version%) added to queue.";

        public static string LookupSongDetail = "%songName% %songSubName%/%authorName% %Rating% (%version%)";

        public static string BsrSongDetail = "%songName% %songSubName%/%authorName% %Rating% (%version%)";

        public static string LinkSonglink = "%songName% %songSubName%/%authorName% %Rating% (%version%) %BeatsaverLink%";

        public static string NextSonglink = "%songName% %songSubName%/%authorName% %Rating% (%version%) is next. %BeatsaberLink%";

        public static string SongHintText = "Requested by %user%%LF%Status: %Status%%Info%%LF%%LF%<size=60%>Request Time: %RequestTime%</size>%LF%<size=60%>Song ID %version% ,rating: %Rating%</size>";

        public static string QueueTextFileFormat = "%songName%%LF%";         // Don't forget to include %LF% for these.


        #region Utility functions

        const int MaximumTwitchMessageLength = 498;

        public void ChatMessage(TwitchUser requestor, string request)
        {
            var dt = new DynamicText().AddUser(ref requestor);
            try
            {
                dt.AddSong(RequestBotListViewController.currentsong.song); // Exposing the current song 
            }
            catch (Exception ex)
            {
                  Plugin.Log(ex.ToString());
            }

            dt.QueueMessage(request);

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

        // BUG: Attempted rewrite of CheckSong/partial song list produced unexpected formatting... please investigate
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

        bool isNotBroadcaster(TwitchUser requestor, string message = "")
        {
            if (requestor.isBroadcaster) return false;
            if (message != "") QueueChatMessage($"{message} is broadcaster only.");
            return true;

        }

        bool isNotModerator(TwitchUser requestor, string message = "")
        {
            if (requestor.isBroadcaster || requestor.isMod) return false;
            if (message != "") QueueChatMessage($"{message} is moderator only.");
            return true;
        }

        private bool filtersong(JSONObject song)
        {
            string songid = song["id"].Value;
            if (IsInQueue(songid)) return true;
            if (SongBlacklist.Songs.ContainsKey(songid)) return true;
            if (listcollection.contains(ref duplicatelist, songid)) return true;
            return false;
        }

        // Returns error text if filter triggers, or "" otherwise, "fast" version returns X if filter triggers

        [Flags] enum SongFilter { none = 0, Queue = 1, Blacklist = 2, Mapper = 4, Duplicate = 8, Remap = 16, Rating = 32, all = -1 };

        private string SongSearchFilter(JSONObject song, bool fast = false, SongFilter filter = SongFilter.all)
        {
            string songid = song["id"].Value;
            if (filter.HasFlag(SongFilter.Queue) && RequestQueue.Songs.Any(req => req.song["version"] == song["version"])) return fast ? "X" : $"Request {song["songName"].Value} by {song["authorName"].Value} already exists in queue!";

            if (filter.HasFlag(SongFilter.Blacklist) && SongBlacklist.Songs.ContainsKey(songid)) return fast ? "X" : $"{song["songName"].Value} by {song["authorName"].Value} ({song["version"].Value}) is banned!";

            if (filter.HasFlag(SongFilter.Mapper) && mapperwhiteliston && mapperfiltered(song)) return fast ? "X" : $"{song["songName"].Value} by {song["authorName"].Value} does not have a permitted mapper!";

            if (filter.HasFlag(SongFilter.Duplicate) && listcollection.contains(ref duplicatelist,songid)) return fast ? "X" : $"{song["songName"].Value} by {song["authorName"].Value} has already been requested this session!";

            if (filter.HasFlag(SongFilter.Remap) && songremap.ContainsKey(songid)) return fast ? "X" : $"no permitted results found!";

            if (filter.HasFlag(SongFilter.Rating) && song["rating"].AsFloat < Config.Instance.lowestallowedrating && song["rating"] != 0) return fast ? "X" : $"{song["songName"].Value} by {song["authorName"].Value} is below the lowest permitted rating!";

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

        private void ClearDuplicateList(TwitchUser requestor, string request)
        {
            if (isNotBroadcaster(requestor)) return;

            QueueChatMessage("Session duplicate list is now clear.");
            
            listcollection.ClearList(ref duplicatelist);
        }

        #endregion



        #region Ban/Unban Song
        public void Ban(TwitchUser requestor, string request)
        {
            Ban(requestor, request, false);
        }

        public void Ban(TwitchUser requestor, string request, bool silence)
        {
            if (isNotModerator(requestor)) return;

            var songId = GetBeatSaverId(request);
            if (songId == "")
            {
                QueueChatMessage($"usage: !block <songid>, omit <>'s.");
                return;
            }

            if (SongBlacklist.Songs.ContainsKey(songId))
            {
                QueueChatMessage($"{request} is already on the ban list.");
            }
            else
            {
                var song = new JSONObject();
                song.Add("id", songId);
                BlacklistQueue.Enqueue(new KeyValuePair<SongRequest, bool>(new SongRequest(song, requestor, DateTime.UtcNow, RequestStatus.Blacklisted), silence));
            }
        }

        private void Unban(TwitchUser requestor, string request)
        {
            var unbanvalue = GetBeatSaverId(request);

            if (SongBlacklist.Songs.ContainsKey(unbanvalue))
            {
                QueueChatMessage($"Removed {request} from the ban list.");
                SongBlacklist.Songs.Remove(unbanvalue);
                SongBlacklist.Write();
            }
            else
            {
                QueueChatMessage($"{request} is not on the ban list.");
            }
        }
        #endregion

        #region Deck Commands
        private void restoredeck(TwitchUser requestor, string request)
        {
            Readdeck(requestor, "savedqueue");
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

                string queuefile = Path.Combine(datapath, request + ".deck");

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

        private void Readdeck(TwitchUser requestor, string request)
        {

            try
            {
                string queuefile = Path.Combine(datapath, request + ".deck");

                string fileContent = File.ReadAllText(queuefile);

                string[] integerStrings = fileContent.Split(new char[] { ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);


                for (int n = 0; n < integerStrings.Length; n++)
                {
                    if (IsInQueue(integerStrings[n])) continue;
                    ProcessSongRequest(requestor, integerStrings[n]);
                }
            }
            catch
            {
                QueueChatMessage("Unable to read deck {request}.");
            }
        }
        #endregion

        #region Dequeue Song
        private void DequeueSong(TwitchUser requestor, string request)
        {

            var songId = GetBeatSaverId(request);
            for (int i = RequestQueue.Songs.Count - 1; i >= 0; i--)
            {
                bool dequeueSong = false;
                var song = RequestQueue.Songs[i].song;

                if (songId == "")
                {
                    string[] terms = new string[] { song["songName"].Value, song["songSubName"].Value, song["authorName"].Value, song["version"].Value, RequestQueue.Songs[i].requestor.displayName };

                    if (DoesContainTerms(request, ref terms))
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
                    return;
                }
            }
            QueueChatMessage($"{request} was not found in the queue.");
        }
        #endregion

 
        // BUG: This actually needs to store the name of the list. Period.
        private void MapperAllowList(TwitchUser requestor, string request)
        {
            string key = request.ToLower();
            mapperwhitelist = listcollection.OpenList(key); // BUG: this is still not the final interface
            QueueChatMessage($"Mapper whitelist set to {request}.");

        }

        private void MapperBanList(TwitchUser requestor, string request)
        {

            string key = request.ToLower();
            mapperBanlist = listcollection.ListCollection[key];
            QueueChatMessage($"Mapper ban list set to {request}.");
        }

        // Not super efficient, but what can you do
        private bool mapperfiltered(JSONObject song)
        {
            string normalizedauthor = song["authorName"].Value.ToLower();

            if (mapperwhitelist.list.Count > 0)
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
                    string[] terms = new string[] { song["songName"].Value, song["songSubName"].Value, song["authorName"].Value, song["version"].Value, RequestQueue.Songs[i].requestor.displayName };
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
    
        // BUG: once we have aliases and command permissions, we can filter the results, so users do not see commands they have no access to    
    private void showCommandlist(TwitchUser requestor, string request)
        {

            var msg = new QueueLongMessage();

            foreach (var entry in NewCommands)
            {
                var botcmd = entry.Value;
                if (HasRights(ref botcmd, ref requestor)) msg.Add($"!{entry.Key}", " "); // Only show commands you're allowed to use
            }
            msg.end("...", $"No commands available.");
        }

        private IEnumerator LookupSongs(TwitchUser requestor, string request)
        {
            bool isBeatSaverId = _digitRegex.IsMatch(request) || _beatSaverRegex.IsMatch(request);

            string requestUrl = isBeatSaverId ? "https://beatsaver.com/api/songs/detail" : "https://beatsaver.com/api/songs/search/song";
            using (var web = UnityWebRequest.Get($"{requestUrl}/{request}"))
            {
                yield return web.SendWebRequest();
                if (web.isNetworkError || web.isHttpError)
                {
                    Plugin.Log($"Error {web.error} occured when trying to request song {request}!");
                    QueueChatMessage($"Invalid BeatSaver ID \"{request}\" specified.");

                    yield break;
                }

                JSONNode result = JSON.Parse(web.downloadHandler.text);
                if (result["songs"].IsArray && result["total"].AsInt == 0)
                {
                    QueueChatMessage($"No results found for request \"{request}\"");

                    yield break;
                }
                JSONObject song;

                var msg=new QueueLongMessage(1,5); // One message maximum, 5 bytes reserved for the ...

                if (result["songs"].IsArray)
                {
                    foreach (JSONObject entry in result["songs"])
                    {
                        song = entry;
                        msg.Add(new DynamicText().AddSong(ref song).Parse(ref LookupSongDetail),", ");
                    }

                }
                else
                {
                    song = result["song"].AsObject;
                    msg.Add(new DynamicText().AddSong(ref song).Parse(ref LookupSongDetail));
                }

                msg.end("...","No results for for request <request>");

                yield return null;

            }
        }

        private void ListQueue(TwitchUser requestor, string request)
        {

            var msg = new QueueLongMessage();

            foreach (SongRequest req in RequestQueue.Songs.ToArray())
            {
                var song = req.song;

                if (msg.Add(song["songName"].Value + " (" + song["version"] + ")", ", ")) break;
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
                if (msg.Add(song["songName"].Value + " (" + song["version"] + ")", ", ")) break;
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

            foreach (string songId in SongBlacklist.Songs.Keys)
            {
                if (msg.Add(songId, ", ")) break;
            }
            msg.end($" ... and {SongBlacklist.Songs.Count - msg.Count} more entries.", "is empty.");

        }

        #endregion

        #region Queue Related

        // This function existing to unify the queue message strings, and to allow user configurable QueueMessages in the future
        public static string QueueMessage(bool QueueState) 
            {
            return QueueState ? "Queue is open" : "Queue is closed";
            }
        private void OpenQueue(TwitchUser requestor, string request)
        {
            ToggleQueue(requestor, request, true);
        }

        private void CloseQueue(TwitchUser requestor, string request)
        {
            ToggleQueue(requestor, request, false);
        }

        private void ToggleQueue(TwitchUser requestor, string request, bool state)
        {
            Config.Instance.QueueOpen = state;
            Config.Instance.Save();

            QueueChatMessage(state ? "Queue is now open." : "Queue is now closed.");
            WriteQueueStatusToFile(QueueMessage(state));
            _refreshQueue = true;
        }
        private static void WriteQueueSummaryToFile()
        {

            if (!Config.Instance.UpdateQueueStatusFiles) return;

            try
            {
                string statusfile = Path.Combine(datapath, "queuelist.txt");
                StreamWriter fileWriter = new StreamWriter(statusfile);

                string queuesummary = "";
                int count = 0;

                foreach (SongRequest req in RequestQueue.Songs.ToArray())
                {
                    var song = req.song;
                    queuesummary += new DynamicText().AddSong(song).Parse(ref QueueTextFileFormat);  // Format of Queue is now user configurable

                    if (++count > Config.Instance.MaximumQueueTextEntries)
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
                string statusfile = Path.Combine(datapath, "queuestatus.txt");
                StreamWriter fileWriter = new StreamWriter(statusfile);
                fileWriter.Write(status);
                fileWriter.Close();              
            }

            catch (Exception ex)
            {
                Plugin.Log(ex.ToString());
            }
        }


        private void Clearqueue(TwitchUser requestor, string request)
        {      
            // Write our current queue to file so we can restore it if needed
            Writedeck(requestor, "justcleared");

            // Cycle through each song in the final request queue, adding them to the song history

            while (RequestQueue.Songs.Count > 0) DequeueRequest(0, false); // More correct now, previous version did not keep track of user requests 
         
            RequestQueue.Write();

            // Update the request button ui accordingly
            UpdateRequestButton();

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
                string remapfile = Path.Combine(datapath, "remap.list");

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
            string remapfile = Path.Combine(datapath, "remap.list");

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
                if (RequestQueue.Songs[i].requestor.id == requestor.id)
                {
                    QueueChatMessage($"{song["songName"].Value} ({song["version"].Value}) removed.");

                    listcollection.remove(duplicatelist, song["id"].Value);
                    RequestBot.Skip(i);
                    return;
                }
            }
            QueueChatMessage($"You have no requests in the queue.");
        }
        #endregion


        // BUG: This requires a switch, or should be disabled for those who don't allow links
        private void ShowSongLink(TwitchUser requestor, string request)
        {

            try  // We're accessing an element across threads, this is only 99.99% safe
            {
                if (RequestBotListViewController.currentsong.song.IsNull) return; // Is this needed?
                var song = RequestBotListViewController.currentsong.song;
                if (!song.IsNull) new DynamicText().AddSong(ref song).QueueMessage(LinkSonglink);

            }
            catch (Exception ex)
            {
                Plugin.Log(ex.ToString());
            }

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
            public Dictionary <string, string> dynamicvariables = new Dictionary <string, string>();  // A list of the variables available to us, we're using a list of pairs because the match we use uses BeginsWith,since the name of the string is unknown. The list is very short, so no biggie

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
                Add("Time", Now.ToString("hh:mm"));
                Add("LongTime", Now.ToString("hh:mm:ss"));
                Add("Date", Now.ToString("yyyy/MM/dd"));
                Add("LF", "\n"); // Allow carriage return

            }

            public DynamicText AddUser(ref TwitchUser user)
            {
                Add("user", user.displayName);
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


            public DynamicText AddBotCmd(ref BOTCOMMAND botcmd)
            {

                StringBuilder aliastext = new StringBuilder();
                foreach (var alias in botcmd.aliases) aliastext.Append($"!{alias} ");
                Add("alias", aliastext.ToString());

                aliastext.Clear();
                aliastext.Append('[');
                aliastext.Append(botcmd.rights & CmdFlags.TwitchLevel).ToString();
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

                Add("StarRating", GetStarRating(ref song)); // Add additional dynamic properties
                Add("Rating", GetRating(ref song));
                Add("BeatsaverLink", $"https://beatsaver.com/browse/detail/{song["version"].Value}");
                Add("BeatsaberLink", $"https://bsaber.com/songs/{song["id"].Value}");

                return this;
            }


            public string Parse(string text, bool parselong = false) // We implement a path for ref or nonref
            {
                return Parse(ref text, parselong);
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
