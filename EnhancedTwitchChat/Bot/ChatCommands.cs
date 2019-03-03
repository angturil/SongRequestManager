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
        // This one needs to be cleaned up a lot imo

        #region Filter support functions
        bool isNotBroadcaster(TwitchUser requestor, string message = "")
        {
            if (requestor.isBroadcaster) return false;
            if (message != "") QueueChatMessage("{message} is broadcaster only.");
            return true;

        }

        bool isNotModerator(TwitchUser requestor, string message = "")
        {
            if (requestor.isBroadcaster || requestor.isMod) return false;
            if (message != "") QueueChatMessage("{message} is moderator only.");
            return true;
        }

        private bool filtersong(JSONObject song)
        {
            string songid = song["id"].Value;
            if (_songBlacklist.Contains(songid)) return true;
            if (duplicatelist.Contains(songid)) return true;
            return false;
        }

        // Returns error text if filter triggers, or "" otherwise, "fast" version returns X if filter triggers

        
        [Flags] enum SongFilter { noQueue, noBlacklist, noMapper, noDuplicate, noRemap, noRating };

        private string SongSearchFilter(JSONObject song, bool fast = false,FlagsAttribute disable=null)
        {
            string songid = song["id"].Value;
            if (FinalRequestQueue.Any(req => req.song["version"] == song["version"])) return fast ? "X" : $"Request {song["songName"].Value} by {song["authorName"].Value} already exists in queue!";

            if (_songBlacklist.Contains(songid)) return fast ? "X" : $"{song["songName"].Value} by {song["authorName"].Value} ({song["version"].Value}) is blacklisted!";

            if (mapperwhiteliston && mapperfiltered(song)) return fast ? "X" : $"{song["songName"].Value} by {song["authorName"].Value} does not have a permitted mapper!";

            if (duplicatelist.Contains(songid)) return fast ? "X" : $"{song["songName"].Value} by {song["authorName"].Value} has already been requested this session!";

            if (songremap.ContainsKey(songid)) return fast ? "X" : $"{song["songName"].Value} by {song["authorName"].Value} was supposed to be remapped!";

            if (song["rating"].AsFloat < Config.Instance.lowestallowedrating) return fast ? "X" : $"{song["songName"].Value} by {song["authorName"].Value} is below the lowest permitted rating!";

            return "";
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

                            if (IsInQueue(song["id"].Value)) continue;
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

                        if (IsInQueue(song["id"].Value)) continue;
                        if (filtersong(song)) continue;

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

                string songlist = "";


                if (result["songs"].IsArray)
                {
                    int count = 0;
                    foreach (JSONObject entry in result["songs"])
                    {
                        song = entry;
                        if (count > 0) songlist += ", ";

                        if (filtersong(song)) continue;
                        ProcessSongRequest(requestor, song["version"].Value);
                        count++;
                        totalSongs++; ;
                    }
                }
                else
                {
                    song = result["song"].AsObject;
                    songlist += $"{song["songName"].Value}-{song["songSubName"].Value}-{song["authorName"].Value} ({song["version"].Value})";

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
        private void Ban(TwitchUser requestor, string request)
        {
            if (isNotModerator(requestor)) return;

            var songId = GetBeatSaverId(request);
            if (songId == "")
            {
                QueueChatMessage($"usage: !block <songid>, omit <>'s.");
                return;
            }

            if (_songBlacklist.Contains(songId))
            {
                QueueChatMessage($"{request} is already on the blacklist.");
            }
            else
            {
                QueueChatMessage($"{request} added to the blacklist.");
                _songBlacklist.Add(songId);
                Config.Instance.Blacklist = _songBlacklist;
            }
        }

        private void Unban(TwitchUser requestor, string request)
        {
            if (!requestor.isMod && !requestor.isBroadcaster) return;

            var unbanvalue = GetBeatSaverId(request);
            if (unbanvalue == "")
            {
                QueueChatMessage($"usage: !unblock <songid>, omit <>'s");
                return;
            }

            if (_songBlacklist.Contains(unbanvalue))
            {
                QueueChatMessage($"Removed {request} from the blacklist.");
                _songBlacklist.Remove(unbanvalue);
                Config.Instance.Blacklist = _songBlacklist;
            }
            else
            {
                QueueChatMessage($"{request} is not on the blacklist.");
            }
        }
        #endregion

        #region Clear Queue
        private void Clearqueue(TwitchUser requestor, string request)
        { 
            if (isNotBroadcaster(requestor)) return;

            // Write our current queue to file so we can restore it if needed
            Writedeck(requestor, "justcleared");

            // Cycle through each song in the final request queue, adding them to the song history
            foreach (var song in FinalRequestQueue)
                SongRequestHistory.Insert(0, song);

            // Clear request queue and save it to file
            FinalRequestQueue.Clear();
            _persistentRequestQueue.Clear();
            Config.Instance.RequestQueue = _persistentRequestQueue;

            // Update the request button ui accordingly
            UpdateRequestButton();

            // Notify the chat that the queue was cleared
            QueueChatMessage($"Queue is now empty.");

            // Reload the queue
            _refreshQueue = true;
        }        

        #endregion

        #region Deck Commands
        private void restoredeck(TwitchUser requestor, string request) 
        {
            Readdeck(requestor, "savedqueue");
        }

    
        private void Writedeck(TwitchUser requestor, string request)
        {
            if (isNotBroadcaster(requestor) && request != "savedqueue") return;

            try
            {
                if (!_alphaNumericRegex.IsMatch(request))
                {
                    QueueChatMessage("usage: writedeck <alphanumeric deck name>");
                    return;
                }

                int count = 0;

                if (FinalRequestQueue.Count == 0)
                {
                    QueueChatMessage("Queue is empty  .");
                    return;
                }

                string queuefile = $"{Environment.CurrentDirectory}\\requestqueue\\" + request + ".deck";

                StreamWriter fileWriter = new StreamWriter(queuefile);

                foreach (SongRequest req in FinalRequestQueue.ToArray())
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

            if (isNotBroadcaster(requestor)) return;

            if (!_alphaNumericRegex.IsMatch(request))
            {
                QueueChatMessage("usage: readdeck <alphanumeric deck name>");
                return;
            }

            try
            {
                string queuefile = $"{Environment.CurrentDirectory}\\requestqueue\\" + request + ".deck";

                string fileContent = File.ReadAllText(queuefile);

                string[] integerStrings = fileContent.Split(new char[] { ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                int[] integers = new int[integerStrings.Length];

                for (int n = 0; n < integerStrings.Length; n++)
                {
                    integers[n] = int.Parse(integerStrings[n]);
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
            if (!requestor.isMod && !requestor.isBroadcaster) return;

            if (request == "")
            {
                QueueChatMessage($"Usage: !remove <song>, omit <>'s.");
                return;
            }

            var songId = GetBeatSaverId(request);
            for (int i = FinalRequestQueue.Count - 1; i >= 0; i--)
            {
                bool dequeueSong = false;
                var song = FinalRequestQueue[i].song;

                if (songId == "")
                {
                    string[] terms = new string[] { song["songName"].Value, song["songSubName"].Value, song["authorName"].Value, song["version"].Value, FinalRequestQueue[i].requestor.displayName };
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

        #region Mapper Blacklist/Whitelist
        private void mapperWhitelist(TwitchUser requestor, string request)
        {
            if (!requestor.isBroadcaster) return;
            
            if (request == "")
            {
                QueueChatMessage("usage: mapperwhitelist <on>,<off>,<clear> or name of mapper file.");
                return;
            }
            
            if (request == "on")
            {
                QueueChatMessage("Only approved mapper songs are now allowed.");
                mapperwhiteliston = true;
                return;
            }

            if (request == "off")
            {
                QueueChatMessage("Mapper whitelist is disabled.");
                mapperwhiteliston = false;
                return;
            }

            if (request == "clear")
            {
                QueueChatMessage("Mapper whitelist is now cleared.");

                mapperwhitelist.Clear();
                return;
            }

            string queuefile = $"{Environment.CurrentDirectory}\\requestqueue\\" + request + ".list";

            string fileContent = File.ReadAllText(queuefile);

            string[] Strings = fileContent.Split(new char[] { ' ', ',', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            string whitelist = "Permitted mappers: ";
            foreach (string mapper in Strings)
            {
                mapperwhitelist.Add(mapper.ToLower());
                whitelist += mapper + " ";
            }

            if (mapperwhitelist.Count > 0) QueueChatMessage(whitelist);

        }

        // Not super efficient, but what can you do
        private bool mapperfiltered(JSONObject song)
        {
            string normalizedauthor = song["authorName"].Value.ToLower();

            if (mapperwhitelist.Count > 0)
            {
                foreach (var mapper in mapperwhitelist)
                {
                    if (normalizedauthor.Contains(mapper)) return false;
                }
                return true;
            }

            foreach (var mapper in mapperblacklist)
            {
                if (normalizedauthor.Contains(mapper)) return true;
            }

            return false;
        }


        private void mapperBlacklist(TwitchUser requestor, string request)
        {
            if (!requestor.isBroadcaster) return;

            if (request == "")
            {
                QueueChatMessage("usage: mapperblacklist <on>,<off>,<clear> or name of mapper file.");
                return;
            }

            if (request == "on")
            {
                QueueChatMessage("Songs with known bad mappers are disabled.");
                mapperblackliston = true;
                return;
            }

            if (request == "off")
            {
                QueueChatMessage("Bad mapper filtering is disabled.");
                mapperblackliston = false;
                return;
            }

            if (request == "clear")
            {
                QueueChatMessage("Bad mapper list is now cleared.");
                mapperblacklist.Clear();
                return;
            }

            string queuefile = $"{Environment.CurrentDirectory}\\requestqueue\\" + request + ".list";

            string fileContent = File.ReadAllText(queuefile);

            string[] Strings = fileContent.Split(new char[] { ',', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            string blacklist = "Mapper blacklist: ";
            foreach (string mapper in Strings)
            {
                mapperblacklist.Add(mapper.ToLower());
                blacklist += mapper + " ";
            }

            if (mapperblacklist.Count > 0) QueueChatMessage(blacklist);
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
            if (!requestor.isMod && !requestor.isBroadcaster) return;

            if (request == "")
            {
                QueueChatMessage($"usage: !{(top ? "mtt" : "last")} <song id> , omit <>'s.");
                return;
            }

            string moveId = GetBeatSaverId(request);
            for (int i = FinalRequestQueue.Count - 1; i >= 0; i--)
            {
                SongRequest req = FinalRequestQueue.ElementAt(i);
                var song = req.song;

                bool moveRequest = false;
                if (moveId == "")
                {
                    string[] terms = new string[] { song["songName"].Value, song["songSubName"].Value, song["authorName"].Value, song["version"].Value, FinalRequestQueue[i].requestor.displayName };
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
                    FinalRequestQueue.RemoveAt(i);

                    // Then readd it at the appropriate position
                    if (top)
                        FinalRequestQueue.Insert(0, req);
                    else
                        FinalRequestQueue.Add(req);

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
            if (isNotModerator(requestor)) return;

            string commands = "";
            foreach (var item in Commands)
            {
                if (deck.ContainsKey(item.Key)) continue;  // Do not show deck names
                commands += "!" + item.Key + " ";
            }

            QueueChatMessage(commands);
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

                string songlist = "";

                if (result["songs"].IsArray)
                {
                    int count = 0;
                    foreach (JSONObject entry in result["songs"])
                    {
                        song = entry;
                        string songdetail = $"{song["songName"].Value}-{song["songSubName"].Value}-{song["authorName"].Value} ({song["version"].Value})";
                        //QueueChatMessage($"{song["songName"].Value} by {song["authorName"].Value} (#{song["id"]})");

                        if (songlist.Length + songdetail.Length > MaximumTwitchMessageLength) break;
                        if (count > 0) songlist += ", ";
                        songlist += songdetail;
                        count++;

                    }

                }
                else
                {
                    song = result["song"].AsObject;
                    songlist += $"{song["songName"].Value}-{song["songSubName"].Value}-{song["authorName"].Value} ({song["version"].Value})";
                }

                QueueChatMessage(songlist);

                yield return null;

            }
        }

        const int MaximumTwitchMessageLength = 498; // BUG: Replace this with a cannonical source

        private void ListQueue(TwitchUser requestor, string request)
        {
            int count = 0;
            var queuetext = "Queue: ";
            foreach (SongRequest req in FinalRequestQueue.ToArray())
            {
                var song = req.song;

                string songdetail = song["songName"].Value + " (" + song["version"] + ")";

                if (queuetext.Length + songdetail.Length > MaximumTwitchMessageLength)
                {
                    QueueChatMessage(queuetext);
                    queuetext = "";
                }

                if (count > 0) queuetext += ", ";
                queuetext += songdetail;
                count++;
            }

            if (count == 0) queuetext = "Queue is empty.";
            QueueChatMessage(queuetext);
        }

        private void ShowSongsplayed(TwitchUser requestor, string request) // Note: This can be spammy.
        {
            if (played.Count == 0)
            {
                QueueChatMessage("No songs have been played.");
                return;
            }

            int count = 0;
            var queuetext = $"{played.Count} songs played this session: ";
            foreach (JSONObject song in played)
            {
                string songdetail = song["songName"].Value + " (" + song["version"] + ")";

                if (queuetext.Length + songdetail.Length > MaximumTwitchMessageLength)
                {
                    QueueChatMessage(queuetext);
                    queuetext = "";
                }

                if (count > 0) queuetext += " , ";
                queuetext += songdetail;
                count++;
            }
            QueueChatMessage(queuetext);
        }

        private void ShowBanList(TwitchUser requestor, string request)
        {
            if (isNotModerator(requestor)) return;

            int count = 0;
            var queuetext = "Banlist: ";
            foreach (string req in _songBlacklist.ToArray())
            {

                if (queuetext.Length + req.Length > MaximumTwitchMessageLength)
                {
                    QueueChatMessage(queuetext);
                    queuetext = "";
                }
                else if (count > 0) queuetext += " , ";

                queuetext += req;
                count++;
            }

            if (count == 0) queuetext = "Banlist is empty.";
            QueueChatMessage(queuetext);
        }

        private void ListPlayedList(TwitchUser requestor, string request)
        {
            if (isNotModerator(requestor)) return;


            int count = 0;
            var queuetext = "Requested this session: ";
            foreach (string req in duplicatelist.ToArray())
            {
                if (queuetext.Length + req.Length > MaximumTwitchMessageLength)
                {
                    QueueChatMessage(queuetext);
                    queuetext = "";
                }
                else if (count > 0) queuetext += " , ";

                queuetext += req;
                count++;
            }

            if (count == 0) queuetext = "Played list is empty.";
            QueueChatMessage(queuetext);
        }
        #endregion

        #region Toggle Queue
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
            if (!isNotModerator(requestor)) return;

            QueueOpen = state;
            QueueChatMessage(state ? "Queue is now open." : "Queue is now closed.");
            WriteQueueStatusToFile(state ? "Queue is now open." : "Queue is closed");
            _refreshQueue = true;
        }
        #endregion

        #region Unmap/Remap Commands
        private void Remap(TwitchUser requestor, string request)
        {
            if (!requestor.isMod && !requestor.isBroadcaster) return;


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
            if (!requestor.isMod && !requestor.isBroadcaster) return;

            if (songremap.ContainsKey(request))
            {
                QueueChatMessage($"Remap entry {request} removed.");
                songremap.Remove(request);
            }
            WriteRemapList();
        }

        private void WriteRemapList()
        {
            //string remapfile = $"c:\\beatsaber\\remap.list";

            try
            {
                string remapfile = $"{Environment.CurrentDirectory}\\requestqueue\\remap.list";

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
            string remapfile = $"{Environment.CurrentDirectory}\\requestqueue\\remap.list";

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
            for (int i = FinalRequestQueue.Count - 1; i >= 0; i--)
            {
                var song = FinalRequestQueue[i].song;
                if (FinalRequestQueue[i].requestor.id == requestor.id)
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
    }
}
