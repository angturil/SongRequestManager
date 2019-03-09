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

        #region Utility functions

        const int MaximumTwitchMessageLength = 498; 

        public void ChatMessage(TwitchUser requestor, string request)
        {
            var dt = new DynamicText().AddUser(ref requestor);
            try
            {
                dt.AddSong(RequestBotListViewController.currentsong.song);
            }
            catch
            {
            
            }
            dt.QueueMessage(request);

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
            if (duplicatelist.Contains(songid)) return true;
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

            if (filter.HasFlag(SongFilter.Duplicate) && duplicatelist.Contains(songid)) return fast ? "X" : $"{song["songName"].Value} by {song["authorName"].Value} has already been requested this session!";

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
            duplicatelist.Clear();
        }

        #endregion


        #region AddSongs/AddSongsByMapper Commands

        /*
        Route::get('/songs/top/{start?}','ApiController@topDownloads');
        Route::get('/songs/plays/{start?}','ApiController@topPlayed');
        Route::get('/songs/new/{start?}','ApiController@newest');
        Route::get('/songs/rated/{start?}','ApiController@topRated');
        Route::get('/songs/byuser/{id}/{start?}','ApiController@byUser');
        Route::get('/songs/detail/{key}','ApiController@detail');
        Route::get('/songs/vote/{key}/{type}/{accessToken}', 'ApiController@vote');
        Route::get('/songs/search/{type}/{key}','ApiController@search');
        */


        private void addNewSongs(TwitchUser requestor, string request)
        {
            if (isNotModerator(requestor, "addnew")) return;

            StartCoroutine(addsongsFromnewest(requestor, request));
        }

        private IEnumerator addsongsFromnewest(TwitchUser requestor, string request)
        {
            int totalSongs = 0;

            string requestUrl = "https://beatsaver.com/api/songs/new";

            int offset = 0;

            bool found = true;

            while (found && offset < Config.Instance.MaxiumAddScanRange)
            {
                found = false;

                using (var web = UnityWebRequest.Get($"{requestUrl}/{offset}"))
                {
                    yield return web.SendWebRequest();
                    if (web.isNetworkError || web.isHttpError)
                    {
                        Plugin.Log($"Error {web.error} occured when trying to request song {requestUrl}!");
                        QueueChatMessage($"Invalid BeatSaver ID \"{requestUrl}\" specified.");

                        yield break;
                    }

                    JSONNode result = JSON.Parse(web.downloadHandler.text);
                    if (result["songs"].IsArray && result["total"].AsInt == 0)
                    {
                        QueueChatMessage($"No results found for request \"{requestUrl}\"");

                        yield break;
                    }

                    // BUG: (Pre-merge) Non reproducible bug occured one time, resulting in unusual list - duplicate and incorrect entries. Not sure if its local, or beastaver db inconsistency?

                    if (result["songs"].IsArray)
                    {
                        foreach (JSONObject entry in result["songs"])
                        {
                            found = true;
                            JSONObject song = entry;

                            if (mapperfiltered(song)) continue; // This ignores the mapper filter flags.
                            if (filtersong(song)) continue;
                            ProcessSongRequest(requestor, song["version"].Value);
                            totalSongs++;
                            if (totalSongs > Config.Instance.maxaddnewresults) yield break;  // We're done once the maximum resuts are produced

                        }
                    }
                }
                offset += 20; // Magic beatsaver.com skip constant.
            }

            if (totalSongs == 0)
            {
                QueueChatMessage($"No new songs found.");
            }
            yield return null;
        }


        private IEnumerator addsongsBymapper(TwitchUser requestor, string request)
        {
            int totalSongs = 0;

            string mapperid = "";

            using (var web = UnityWebRequest.Get($"https://beatsaver.com/api/songs/search/user/{request}"))
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

                foreach (JSONObject song in result["songs"].AsArray)
                {
                    mapperid = song["uploaderId"].Value;
                    break;
                }


                if (mapperid == "")
                {
                    QueueChatMessage($"Unable to find mapper {request}");
                    yield break;
                }

            }
            int offset = 0;

            string requestUrl = "https://beatsaver.com/api/songs/byuser";

            bool found = true;

            while (found)
            {
                found = false;

                using (var web = UnityWebRequest.Get($"{requestUrl}/{mapperid}/{offset}"))
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

                    foreach (JSONObject entry in result["songs"])
                    {
                        song = entry;

                        // We ignore the duplicate filter for this

                        if (IsInQueue(song["id"].Value)) continue;
                        if (SongBlacklist.Songs.ContainsKey(song["id"].Value)) continue;

                        ProcessSongRequest(requestor, song["version"].Value);
                        found = true;
                        totalSongs++; ;
                    }

                }
                offset += 20;
            }

            yield return null;
        }

        // General search version
        private IEnumerator addsongs(TwitchUser requestor, string request)
        {
            int totalSongs = 0;

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

                if (result["songs"].IsArray)
                {
                    int count = 0;
                    foreach (JSONObject entry in result["songs"])
                    {
                        song = entry;
  
                        if (filtersong(song)) continue;
                        ProcessSongRequest(requestor, song["version"].Value);
                        count++;
                        totalSongs++; ;
                    }
                }
                else
                {
                    song = result["song"].AsObject;
                     // $"{song["songName"].Value}-{song["songSubName"].Value}-{song["authorName"].Value} ({song["version"].Value})";

                    ProcessSongRequest(requestor, song["version"].Value);
                    totalSongs++;
                }
                yield return null;
            }
            //QueueChatMessage($"Added {totalSongs} songs.");
        }


        private void addsongsbymapper(TwitchUser requestor, string request)
        {
            if (isNotBroadcaster(requestor, "mapper")) return;

            StartCoroutine(addsongsBymapper(requestor, request));
        }

        private void addSongs(TwitchUser requestor, string request)
        {

            if (isNotBroadcaster(requestor, "addsongs")) return;

            StartCoroutine(addsongs(requestor, request));
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
                    if (IsInQueue( integerStrings[n])) continue;
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

        #region List Manager Related functions ... probably needs its own file

        // List types:

        // This is a work in progress. 

        // .deck = lists of songs
        // .mapper = mapper lists
        // .user = twitch user lists
        // .command = command lists = linear scripting
        // .dict = list contains key value pairs

        private void LoadList(TwitchUser requestor, string request)
            {
             StringListManager newlist = new StringListManager();
            if (newlist.Readfile(request))
            {
                QueueChatMessage($"{request} ({newlist.Count()}) is loaded.");
                listcollection.ListCollection.Add(request.ToLower(), newlist);
            }
            else
            {
            QueueChatMessage($"Unable to load {request}.");
            }
        }

        private void OpenList(TwitchUser requestor, string request)
        {
            StringListManager newlist = new StringListManager();
            if (newlist.Readfile(request))
            {
                QueueChatMessage($"{request} ({newlist.Count()}) is loaded.");
                listcollection.ListCollection.Add(request.ToLower(), newlist);
            }
            else
            {
                listcollection.ListCollection.Add(request.ToLower(), newlist);
                QueueChatMessage($"{request} ({newlist.Count()}) is created.");
            }
        }


        private void OpenList(string request)
        {
            StringListManager newlist = new StringListManager();
            if (newlist.Readfile(request))
            {
                QueueChatMessage($"{request} ({newlist.Count()}) is loaded.");
                listcollection.ListCollection.Add(request.ToLower(), newlist);
            }
            else
            {
                listcollection.ListCollection.Add(request.ToLower(), newlist);
                QueueChatMessage($"{request} ({newlist.Count()}) is created.");
            }
        }


        private void writelist(TwitchUser requestor, string request)
            {
            
            }

        // Add list to queue, filtered by InQueue and duplicatelist
        private void queuelist(TwitchUser requestor, string request)
            {

            }

        // Remove entire list from queue
        private void unqueuelist(TwitchUser requestor, string request)
            {
            
            }


        private void Addtolist (TwitchUser requestor, string request)
        {
        string[] parts = request.Split(new char[] { ' ',',' }, 2);
        if (parts.Length<2)
            {
           //     NewCommands[Addtolist].ShortHelp();
            QueueChatMessage("Usage text... use the official help method");
            return;    
            }

            try
            {
                var list = listcollection.ListCollection[parts[0].ToLower()];
                list.Add(parts[1]);
                QueueChatMessage($"Added {parts[1]} to {parts[0]}");

            }
            catch
            {
                QueueChatMessage($"list {parts[0]} not found.");
            }




        }


        private void ClearList(TwitchUser requestor, string request)
        {

        try
            {
                listcollection.ListCollection[request.ToLower()].Clear();
                QueueChatMessage($"{request} is cleared.");
            }
            catch
            {
                QueueChatMessage($"Unable to clear {request}");
            }
        }

        private void UnloadList(TwitchUser requestor, string request)
        {

            try
            {
                listcollection.ListCollection.Remove(request.ToLower());
                QueueChatMessage($"{request} unloaded.");
            }
        catch    
            {
                QueueChatMessage($"Unable to unload {request}");
            }
        }


        private void ListList(TwitchUser requestor, string request)
        {

            try
            {
                var list = listcollection.ListCollection[request.ToLower()];

                var msg = new QueueLongMessage();
                foreach (var entry in list.list) msg.Add(entry, ", ");
                msg.end("...", $"{request} is empty");
            }
        catch
            {
                QueueChatMessage($"{request} not found.");
            }
        }

        private void showlists(TwitchUser requestor, string request)
        {

            var msg = new QueueLongMessage();

            msg.Header("Loaded lists: ");
            foreach (var entry in listcollection.ListCollection) msg.Add($"{entry.Key} ({entry.Value.Count()})",", ");
            msg.end("...", "No lists loaded."); 
        }

        // The list collection maintains a dictionary of named, PERSISTENT lists. Accessing a collection by name automatically loads or crates it.
        public class ListCollectionManager
            {

            // BUG: DoNotCreate flags currently do nothing

            public Dictionary<string, StringListManager> ListCollection = new Dictionary<string, StringListManager>();

            public ListCollectionManager()
                {
                // Add an empty list so we can set various lists to empty
                StringListManager empty = new StringListManager();
                ListCollection.Add("empty", empty);
                }   

            // Normalize any keys, checking for case, and naming rules 
            // BUG: Naming check does not verify valid list names
            private string normalize(ref string  listkey)
                {
                return listkey.ToLower();
                }

            public bool contains(ref string listname,string key, bool DoNotCreate = false)
                {
                try
                {
                    RequestBot.Instance.QueueChatMessage($"{listname} {key}");
                return ListCollection[normalize(ref listname)].list.Contains(key);
                }
                catch (Exception ex) { Plugin.Log(ex.ToString()); } // Going to try this form, to reduce code verbosity.              
         
                return false;
                }
        public void ClearList(ref string listname,bool DoNotCreate=false)
                {
                try
                {
                ListCollection[normalize(ref listname)].Clear();
                }
                catch (Exception ex) { Plugin.Log(ex.ToString()); } // Going to try this form, to reduce code verbosity.              
                }

        }

        public static ListCollectionManager listcollection = new ListCollectionManager();

        
        

        public class StringListManager
        {
            public List<string> list = new List<string>();

            public bool Readfile(string filename,bool ConvertToLower=true)
            {
                try
                {
                    string listfilename = Path.Combine(datapath, filename);
                    string fileContent = File.ReadAllText(listfilename);
                    list = fileContent.Split(new char[] { ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    if (ConvertToLower) LowercaseList();
                    return true;
                }
                catch
                {
                    // Ignoring this for now, I expect it to fail
                }

                return false;
            }

            public bool Writefile(string filename, string separator = ",")
            {
                try
                {
                    string listfilename = Path.Combine(datapath, filename);
                    var output = String.Join(",", list.ToArray());
                    File.WriteAllText(listfilename, output);
                    return true;
                }
                catch
                {
                    // Ignoring this for now, failed write can be silent
                }

                return false;
            }

            public bool Add(string entry)
            {
                if (list.Contains(entry)) return false;
                list.Add(entry);
                return true;
            }

            public bool Removeentry(string entry)
            {
                return list.Remove(entry);
            }

            // Picks a random entry and returns it, removing it from the list
            public string Drawentry()
            {
                if (list.Count == 0) return "";
                int entry = generator.Next(0, list.Count);
                string result = list.ElementAt(entry);
                list.RemoveAt(entry);
                return result;
            }

            // Picks a random entry but does not remove it
            public string Randomentry()
            {
                if (list.Count == 0) return "";
                int entry = generator.Next(0, list.Count);
                string result = list.ElementAt(entry);
                return result;
            }

            public int Count()
            {
                return list.Count;
            }

            public void Clear()
            {
                list.Clear();
            }

            public void LowercaseList ()                
                {
                for (int i=0;i<list.Count;i++)
                   {
                    list[i] = list[i].ToLower();
                    }
                }
            public void Outputlist(ref QueueLongMessage msg,string separator=", ")
                {
                foreach (string entry in list) msg.Add(entry, separator);                 
                }
            
            }

        private void MapperAllowList(TwitchUser requestor, string request)
        {
            string key = request.ToLower();
            if (listcollection.ListCollection.ContainsKey(key))
                {
                mapperwhitelist = listcollection.ListCollection[key];
                QueueChatMessage($"Mapper whitelist set to {request}.");
                }
            else
                {
                QueueChatMessage($"Unable to set mapper whitelist to {request}.");
                } 
        }

        private void MapperBanList(TwitchUser requestor, string request)
        {

            string key = request.ToLower();
            if (listcollection.ListCollection.ContainsKey(key))
            {
                mapperBanlist = listcollection.ListCollection[key];
                QueueChatMessage($"Mapper black list set to {request}.");
            }
            else
            {
                QueueChatMessage($"Unable to set mapper banlist to {request}.");
            }
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


        #endregion

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
                        msg.Add(new DynamicText().AddSong(ref song).Parse(ref Config.Instance.LookupSongDetail),", ");
                    }

                }
                else
                {
                    song = result["song"].AsObject;
                    msg.Add(new DynamicText().AddSong(ref song).Parse(ref Config.Instance.LookupSongDetail));
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
            QueueOpen = state;
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
                    queuesummary += new DynamicText().AddSong(song).Parse(Config.Instance.QueueTextFileFormat);  // Format of Queue is now user configurable

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
            foreach (var song in RequestQueue.Songs)
                RequestHistory.Songs.Insert(0, song);
            RequestHistory.Write();

            // Clear request queue and save it to file
            RequestQueue.Songs.Clear();
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

                    duplicatelist.Remove(song["id"].Value); // Special case, user removing own request does not result in a persistent duplicate. This can also be caused by a false !wrongsong which was unintended.
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
            if (RequestBotListViewController.currentsong.song.IsNull) return;

            try  // We're accessing an element across threads, this is only 99.99% safe
            {
                var song = RequestBotListViewController.currentsong.song;
                if (!song.IsNull) new DynamicText().AddSong(ref song).QueueMessage(Config.Instance.LinkSonglink);

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
            public List<KeyValuePair<string, string>> dynamicvariables = new List<KeyValuePair<string, string>>();  // A list of the variables available to us, we're using a list of pairs because the match we use uses BeginsWith,since the name of the string is unknown. The list is very short, so no biggie

            public bool AllowLinks = true;

            string Get(ref string fieldname) // Get the field. Failure is an option,  The fieldname may include extra characters. It is case sensitive.
            {
                string result = "";
                foreach (var entry in dynamicvariables)
                {
                    if (fieldname.StartsWith(entry.Key)) return entry.Value;
                }
                return result;
            }

            public DynamicText Add(string key, string value)
            {
                dynamicvariables.Add(new KeyValuePair<string, string>(key, value)); // Make the code slower but more readable :(
                return this;
            }

            public DynamicText()
            {
                Add("endusage", "");

                AddLinks();

                DateTime Now = DateTime.Now; //"MM/dd/yyyy hh:mm:ss.fffffff";         
                Add("Time", Now.ToString("hh:mm"));
                Add("LongTime", Now.ToString("hh:mm:ss"));
                Add("Date", Now.ToString("yyyy/MM/dd"));
                Add("EOL", "\n"); // Allow carriage return
                Add("LF", "\n"); // Allow carriage return

            }

            // To make this efficient, The return type needs to be a ref (using ref struct for the class). c# 7.2 supports this. This might be ugly IRL. Not sure if Unused return types execute a copy (assume not).
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
                aliastext.Append(botcmd.cmdflags & CmdFlags.TwitchLevel).ToString();
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
                AddJSON(ref song, prefix);
                Add("StarRating", GetStarRating(ref song));
                Add("Rating", GetRating(ref song));
                Add("BeatsaverLink", $"https://beatsaver.com/browse/detail/{song["version"].Value}");
                Add("BeatsaberLink", $"https://bsaber.com/songs/{song["id"].Value}");

                return this;
            }


            public string Parse(string text, bool parselong = false) // We implement a path for ref or nonref
            {
                return Parse(ref text, parselong);
            }

            public string Parse(ref string text, bool parselong = false)
            {
                StringBuilder msgtext = new StringBuilder();
                string[] parts = text.Split(new char[] { '%' }); // Split entire help message by % boundaries


                if (parts.Length == 0) return "";
                for (int i = 0; i < parts.Length; i++)
                {

                    bool found = false;
                    foreach (var entry in dynamicvariables)
                    {
                        if (parts[i].StartsWith(entry.Key))
                        {
                            if (entry.Key == "endusage" && !parselong) return msgtext.ToString(); // BUG: This works, but isn't the most elegant solution. Look into this later.

                            msgtext.Append(entry.Value);
                            msgtext.Append(parts[i].Substring(entry.Key.Length));
                            found = true;
                            break;
                        }
                    }
                    if (found) continue;

                    if (i != 0) msgtext.Append('%'); // Basically, we need to put the %'s back that were removed by split. The first % though is always fake.
                    msgtext.Append(parts[i]);
                }

                return msgtext.ToString();
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
