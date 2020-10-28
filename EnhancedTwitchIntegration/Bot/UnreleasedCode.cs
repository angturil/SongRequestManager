using System.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System.Threading.Tasks;
using ChatCore.Models.Twitch;
using ChatCore.Utilities;

//using BeatBits;

// Feature requests: Add Reason for being banned to banlist
//  

//
// NOTE: Any unreleased code structure, dependencies, or files are subject to change without notice. Any dependencies you create around this code 
// are virtually guaranteed not to work in future builds. If I thought the code was release ready, it wouldn't be here.

namespace SongRequestManager
{
    // https://tmi.twitch.tv/group/user/sehria_k/chatters // User list for channel 
    public partial class RequestBot : MonoBehaviour
    {
#if UNRELEASED
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


        public static string DECKS = @"

[FUN]/0'!fun%CR%' [HARD]/0'!hard%CR%' [DANCE]/0'!dance%CR%' [CHILL]/0'!chill%CR%' [BRUTAL]/0'!brutal%CR%' [METAL]/0'!metal%CR%' [PP]/0'!pp%CR%' [Sehria]/0'!sehria%CR%' [Search]/0'!search%CR%'";

        public const string BOTKEYS =
@"[ADD]/0'!bsr ' [UPTIME]/0' !uptime%CR%' [Cat1]/0' sehriaCat1' [Cat2]/0' sehriaCat2' [sehriaXD]/0' sehriaXD' [tohruSway]/0' tohruSway' [Rainbow]/0' RainbowDance' [Love]/0' sehriaLove' [Wink]/0' sehriaWink'
";


        private IEnumerator GetRooms(ParseState state)
        {
            yield break;

        }

        private async Task UpdateMappers(ParseState state)
        {
            state.msg($"Updating all mapper lists/decks.");
            var list = listcollection.OpenList("mapper.list");

            foreach (var entry in list.list)
            {
                string mappername = entry;
                ParseState newstate = new ParseState(state); // Must use copies here, since these are all threads
                newstate.parameter = entry;
                await AddmapperToDeck(newstate);
            }
        }

        private async Task AddmapperToDeck(ParseState state)
        {
            int totalSongs = 0;

            string mapperid = "";

            var resp = await Plugin.WebClient.GetAsync($"https://beatsaver.com/api/search/text/q={state.parameter}", System.Threading.CancellationToken.None);

            if (resp.IsSuccessStatusCode)
            {
                JSONNode result = resp.ConvertToJsonNode();
                if (result["docs"].IsArray && result["totalDocs"].AsInt == 0)
                {
                    QueueChatMessage($"No results found for request \"{state.parameter}\"");
                    return;
                }

                foreach (JSONObject song in result["docs"].AsArray)
                {
                    mapperid = song["uploaderId"].Value;
                    break;
                }

                if (mapperid == "")
                {
                    QueueChatMessage($"Unable to find mapper {state.parameter}");
                    return;
                }
            }
            else
            {
                Plugin.Log($"Error {resp.ReasonPhrase} occured when trying to request song {state.parameter}!");
                QueueChatMessage($"Invalid BeatSaver ID \"{state.parameter}\" specified.");

                return;
            }

            int offset = 0;

            string deckname = $"{state.parameter}.deck";

            string requestUrl = "https://beatsaver.com/api/maps/uploader/";

            bool found = true;

            while (found)
            {
                found = false;

                var mapperResp = await Plugin.WebClient.GetAsync($"{requestUrl}/{mapperid}/{offset}", System.Threading.CancellationToken.None);

                if (mapperResp.IsSuccessStatusCode)
                {
                    JSONNode result = mapperResp.ConvertToJsonNode();
                    if (result["docs"].IsArray && result["totalDocs"].AsInt == 0)
                    {
                        QueueChatMessage($"No results found for request \"{state.parameter}\"");

                        return;
                    }
                    JSONObject song;

                    foreach (JSONObject entry in result["docs"])
                    {
                        song = entry;

                        // We ignore the duplicate filter for this

                        listcollection.add(deckname, song["id"]);

                        found = true;
                        totalSongs++; ;
                    }
                }
                else
                {
                    Plugin.Log($"Error {mapperResp.ReasonPhrase} occured when trying to request song {state.parameter}!");
                    QueueChatMessage($"Invalid BeatSaver ID \"{state.parameter}\" specified.");

                    return;
                }
                offset += 1;
            }

            state.msg($"Added {totalSongs} to {deckname}.");
        }
        #endregion



        //public struct SDKVector3
        //{
        //    public double x, y, z;
        //    public static SDKVector3 zero
        //    {
        //        get
        //        {
        //            return new SDKVector3() { x = 0, y = 0, z = 0 };
        //        }
        //    }

        //    public static SDKVector3 one
        //    {
        //        get
        //        {
        //            return new SDKVector3() { x = 1, y = 1, z = 1 };
        //        }
        //    }

        //    public static implicit operator Vector3(SDKVector3 v)
        //    {
        //        return new Vector3((float) v.x, (float) v.y, (float) v.z);
        //    }

        //    public static implicit operator SDKVector3(Vector3 v)
        //    {
        //        return new SDKVector3() { x = v.x, y = v.y, z = v.z };
        //    }
        //}

        //public struct SDKQuaternion
        //{
        //    public double x, y, z, w;
        //    public static SDKQuaternion identity
        //    {
        //        get
        //        {
        //            return new SDKQuaternion() { x = 0, y = 0, z = 0, w = 0 };
        //        }
        //    }

        //    public static implicit operator Quaternion(SDKQuaternion v)
        //    {
        //        return new Quaternion((float) v.x, (float) v.y, (float) v.z,(float) v.w);
        //    }

        //    public static implicit operator SDKQuaternion(Quaternion v)
        //    {
        //        return new SDKQuaternion() { x = v.x, y = v.y, z = v.z, w = v.w };
        //    }
        //}

        //[StructLayout(LayoutKind.Sequential)]
        //public struct tracker_state
        //{
        //    public bool is_ready;
        //    public SDKVector3 position;
        //    public SDKQuaternion rotation;
        //    public SDKVector3 driverFromHeadPositionOffset;
        //    public SDKQuaternion driverFromHeadRotationOffset;
        //    public bool is_mirroring;
        //    public UInt32 real_device_id;
        //    public SDKVector3 real_position;
        //    public SDKVector3 real_rotation;
        //    public UInt32 device_class;
        //};



        //[StructLayout(LayoutKind.Sequential)]
        //struct Container
        //{
        //    [MarshalAs(UnmanagedType.ByValTStr, CharSet = CharSet.Ansi, SizeConst = 20)]
        //    string Name;
        //    IntPtr VoidData;
        //    IntPtr Link
        //}
        //var retContainer = (Container)Marshal.PtrToStructure(ret, typeof(Container));
        //private void livsdktest(TwitchUser requestor, string request)
        //{
        //    QueueChatMessage($"SDK2Test code");
            
        //    try
        //    {

        
        //        var now = LIV.SDK.Unity.SharedTextureProtocol.GetCurrentTime();
        //        QueueChatMessage($"SDK2 Time={now} C# Time={DateTime.UtcNow.Ticks}");
        //        LIV.SDK.Unity.SharedTextureProtocol.AcquireCompositorFrame(ulong.MaxValue);
        //        LIV.SDK.Unity.SharedTextureProtocol.ReleaseCompositorFrame();
        //        var ptr=LIV.SDK.Unity.SharedTextureProtocol.GetCompositorChannelObject(5, 5,UInt64.MaxValue);
        //        QueueChatMessage($"Object 5 ptr={ptr}");
        //        SharedTextureProtocol.AddString("EngineName", "Unity", 5);
        //        SharedTextureProtocol.AddString("Test", "Testing", 5);
        //        SharedTextureProtocol.AddString("ABCD", "ABCD", 5);
        //        var camerapose = LIV.SDK.Unity.SharedTextureProtocol.GetCompositorChannelObject(7, 7, UInt64.MaxValue);
        //        QueueChatMessage($"Object 7 ptr={camerapose}");

        //        var pose = (tracker_state) Marshal.PtrToStructure(camerapose, typeof(tracker_state));

                 

        //        QueueChatMessage($"rotation={pose.rotation.w} Device Class={pose.device_class} Device ID={pose.real_device_id}");


        //        SDKTexture texture = new SDKTexture();
        //        texture.width = 1920;
        //        texture.height = 1080;
                
        //        SharedTextureProtocol.AddTexture(texture, 100);
      
        //    }
        //    catch
        //    {
        //        QueueChatMessage($"SDK2Test code failed.");
        //    }

        //}

        #region Deck Manager
        private string loaddecks(ParseState state)
        {
            //createdeck(state.user, RequestBotConfig.Instance.DeckList.ToLower());

            string decklist = RequestBotConfig.Instance.DeckList.ToLower();
            state.parameter = decklist;
            return createdeck(state);
        }

        private string createdeck(ParseState state)
        {
            string request = state.parameter.ToLower();
            string[] decks = request.Split(new char[] { ',', ' ', '\t' });

            if (decks[0] == "")
            {
                QueueChatMessage($"usage: deck <deckname> ... omit <>'s.");
                return success;
            }

            string msg = "deck";
            if (decks.Length > 1) msg += "s";

            msg += ": ";

            foreach (string req in decks)
            {
                try
                {
                    string[] integerStrings = listcollection.OpenList(req + ".deck").list.ToArray();

                    if (integerStrings.Length == 0)
                    {
                        if (!state.flags.HasFlag(CmdFlags.Silent)) QueueChatMessage($"Creating deck: {req}");
                    }

                    //if (integerStrings.Length > 0)
                    {
                        //deck[req] = fileContent;
                        deck[req] = string.Join(",", integerStrings);

                        if (!COMMAND.aliaslist.ContainsKey(req))
                        {
                            new COMMAND('!' + req).Action(drawcard).Help(Everyone | CmdFlags.Dynamic, "usage: %alias%   %|%Draws a song from one of the curated decks. Does not repeat or conflict.", _drawcard).User(req);
                        }

                        msg += ($"!{req} ({integerStrings.Length} cards) ");
                    }
                }
                catch
                {
                    msg += ($"!{req} (invalid) ");
                }
            }
            if (!state.flags.HasFlag(CmdFlags.Silent)) QueueChatMessage(msg);
            return success;
        }

        // Toggle a card in a deck
        public string SubcmdToggle(ParseState state)
        {
            var deckname = state.botcmd.userParameter.ToString();
            if (!deck.ContainsKey(deckname)) return state.text("You can only use %alias% on a deck.");
            string id = GetBeatSaverId(state.parameter);
            if (id == "") return state.text("%alias% requires a valid beatsaver id");
            deckname += ".deck";
            if (listcollection.contains(ref deckname, id))
                listcollection.remove(deckname, id);
            else
                listcollection.add(deckname, id);

            UpdateRequestUI();
            _refreshQueue = true;

            return endcommand;
        }

        private string whatdeck(ParseState state)
        {
            string request = GetBeatSaverId(state.parameter);

            var msg = new QueueLongMessage();

            msg.Header($"Decks containing {request}: ");
            foreach (var item in deck)
            {
                if (listcollection.OpenList(item.Key + ".deck").Contains(request)) msg.Add(item.Key, ", ");
            }
            msg.end("...", $"No decks contain {request}");

            return success;
        }


        private string decklist(ParseState state)
        {

            if (state.parameter == "draw")
            {
                int rnd = generator.Next(0, deck.Count);
                COMMAND.Parse(state.user, "!" + deck.ElementAt(rnd).Key, state.flags);
                return success;
            }

            string decks = "";
            foreach (var item in deck)
            {
                decks += "!" + item.Key + " ";
            }

            if (decks == "")
                state.msg("No decks loaded.");
            else
                state.msg(decks);

            return success;
        }


        private void unloaddeck(TwitchUser requestor, string request)
        {
            if (COMMAND.aliaslist.ContainsKey('!' + request) && deck.ContainsKey(request))
            {
                COMMAND.aliaslist.Remove('!' + request);
                deck.Remove(request);
                QueueChatMessage($"{request} unloaded.");
            }
        }



        static string empty = "";
      
        private string drawcard(ParseState state)
        {

            var request = state.parameter;

            if (request != "")
            {
                if (state.user.IsBroadcaster || state.user.IsModerator) // BUG: These commands take 2 tiers of permission, perhaps we can handle this better with subcommands.
                {
                    request = GetBeatSaverId(request);
                    if (request == "") return empty;

                    listcollection.add(state.botcmd.userParameter + ".deck", request);

                    deck[state.botcmd.userParameter.ToString()] += "," + request;
                    QueueChatMessage($"Added {request} to deck {state.botcmd.userParameter.ToString()}.");
                    return empty;
                }
                else
                {
                    QueueChatMessage("Adding cards to a deck is restricted to moderators.");
                    return empty;
                }
            }

            if (RequestBotConfig.Instance.RequestQueueOpen == false && !state.flags.HasFlag(CmdFlags.NoFilter) && !state.flags.HasFlag(CmdFlags.Local))
            {
                QueueChatMessage("Queue is currently closed.");
                return empty;
            }

            while (true)
            {

                string[] integerStrings = deck[state.botcmd.userParameter.ToString()].Split(new char[] { ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                if (integerStrings.Length > 0)
                {
                    int entry = generator.Next(0, integerStrings.Length);
                    string newlist = "";

                    for (int x = 0; x < entry; x++)
                    {
                        newlist += integerStrings[x] + ",";
                    }

                    for (int y = entry + 1; y < integerStrings.Length; y++)
                    {
                        newlist += integerStrings[y] + ",";
                    }

                    deck[state.botcmd.userParameter.ToString()] = newlist;

                    string songid = GetBeatSaverId(integerStrings[entry]);

                    if (listcollection.contains(ref duplicatelist, songid)) continue;
                    if (listcollection.contains(ref banlist,integerStrings[entry])) continue;
                    if (IsInQueue(songid)) continue;

                    ParseState newstate = new ParseState(state); // Must use copies here, since these are all threads
                    newstate.flags =CmdFlags.NoFilter;
                    if (state.flags.HasFlag(CmdFlags.SilentResult) || state.flags.HasFlag(CmdFlags.Local)) newstate.flags |= CmdFlags.SilentResult;
                    newstate.parameter = integerStrings[entry];
                    newstate.info = $"Deck: {state.botcmd.userParameter}";
                    ProcessSongRequest(newstate);


                }
                else
                {
                    QueueChatMessage("Deck is empty.");
                }

                break;
            }

            return empty;

        }

        #endregion

#if UNRELEASED
        // BUG: Not production ready, will probably never be released. Takes hours, and puts a load on beatsaver.com. DO NOT USE
        public async Task DownloadEverything(ParseState state)
        {
            var startingmem = GC.GetTotalMemory(true);

            Instance.QueueChatMessage("Starting Beatsaver scan");
            var StarTime = DateTime.UtcNow;

            int totalSongs = 0;

            string requestUrl = "https://beatsaver.com/api/maps/latest";

            int offset = 0;
            while (true) // MaxiumAddScanRange
            {
                var resp = await Plugin.WebClient.GetAsync($"{requestUrl}/{offset}", System.Threading.CancellationToken.None);

                if (resp.IsSuccessStatusCode)
                {
                    JSONNode result = resp.ConvertToJsonNode();

                    if (result == null || result["docs"].Count == 0) break;

                    foreach (JSONObject entry in result["docs"].AsArray)
                    {
                        JSONObject song = entry;

                        if (MapDatabase.MapLibrary.ContainsKey(song["key"].Value))
                        {
                            goto done;
                        }

                        new SongMap(song);
                        totalSongs++;

                        if ((totalSongs & 127) == 0) Instance.QueueChatMessage($"Processed {totalSongs}");
                        //QueueSong(state, song);
                    }
                }
                else
                {
                    break;
                }

                offset += 1; // Magic beatsaver.com skip constant.
            }

        done:

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
                    string localPath = $"f:\\bsnew\\{song.Value.song["key"].Value}.zip";
                    if (File.Exists(localPath)) continue;

                    var songBytes = await Plugin.WebClient.DownloadSong(song.Value.song["downloadUrl"].Value, System.Threading.CancellationToken.None);

                    File.WriteAllBytes(localPath, songBytes);

                    if (!File.Exists(localPath)) continue;
                    msg.Add($"{song.Value.song["id"].Value}", ",");
                }
            }

            msg.end();
            duration = DateTime.UtcNow - StarTime;
            Instance.QueueChatMessage($"BeatSaver Download Done. ({duration.TotalSeconds} secs.");

#if UNRELEASED
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();
            Instance.QueueChatMessage($"hashentries: {SongMap.hashcount} memory: {(GC.GetTotalMemory(false) - startingmem) / 1048576} MB");
#endif
        }

#endif

        #region SampleCode
        /*

                public class StaticClass
                {
                    public static Dictionary<string, DynamicClass> dict = new Dictionary<string, DynamicClass>();

                    public static void add(string functionname, DynamicClass d)
                    {
                        dict.Add(functionname, d);
                    }
                }

                public class DynamicClass
                {
                    public virtual void test(string value)
                    {
                        Plugin.Log("This is the base class function call, it does nothing!");
                    }
                }

                public class YourClass : DynamicClass
                {
                    public override void test(string value)
                    {
                        Plugin.Log("This is the override function");
                    }
                }

        */

        /*
        public static IEnumerator DownloadSpriteAsync(string url, Action<Sprite> downloadCompleted)
        {
            yield return Download(url, DownloadType.Texture, null, (web) =>
            {
                downloadCompleted?.Invoke(UIUtilities.LoadSpriteFromTexture(DownloadHandlerTexture.GetContent(web)));
            });
        }

        public static IEnumerator Download(string url, DownloadType type, Action<UnityWebRequest> beforeSend, Action<UnityWebRequest> downloadCompleted, Action<UnityWebRequest> downloadFailed = null)
        {
            using (UnityWebRequest web = WebRequestForType(url, type))
            {
                if (web == null) yield break;

                beforeSend?.Invoke(web);

                // Send the web request
                yield return web.SendWebRequest();

                // Write the error if we encounter one
                if (web.isNetworkError || web.isHttpError)
                {
                    downloadFailed?.Invoke(web);
                    Plugin.Log($"Http error {web.responseCode} occurred during web request to url {url}. Error: {web.error}");
                    yield break;
                }
                downloadCompleted?.Invoke(web);
            }
        }

        public static IEnumerator DownloadFile(string url, string path)
        {
            yield return Download(url, DownloadType.Raw, null, (web) =>
            {
                byte[] data = web.downloadHandler.data;
                try
                {
                    if (!Directory.Exists(Path.GetDirectoryName(path)))
                        Directory.CreateDirectory(Path.GetDirectoryName(path));

                    File.WriteAllBytes(path, data);
                }
                catch (Exception)
                {
                    Plugin.Log("Failed to download file!");
                }
            });
        }
        */

        #endregion

        #region NOJSON

        // Container class that's not a garbage dump (tm)
        // This will get refactored again to use Pointers to reduce some un-necessary checks.
        class NOTJSON
        {
            static Dictionary<string, byte> FieldName = new Dictionary<string, byte>();
            static byte _Id = 1;

            NOTJSON(int elementcount = 1, int size = 32)
            {
                // Initialize empty container with x elements.
                int datastart = (elementcount + 1) << 2;
                if (datastart > size) size = datastart; // Make sure we have enough room.
                Dataset = new byte[size];
                for (int i = 0; i < datastart; i += 4)
                {
                    write32(i, datastart);
                }
            }

            byte[] Dataset = new byte[1024];

            enum types { Null, String, Int, Float, Binary, UTF8, NOTJSON, JSON };

            int read32(int p)
            {
                return BitConverter.ToInt32(Dataset, p);
            }

            int read24(int p)
            {
                return Dataset[p] | (Dataset[p + 1] << 8) | (Dataset[p + 2] << 16);
            }

            void write24(int p, int value)
            {
                Dataset[p] = (byte)value;
                Dataset[p + 1] = (byte)(value >> 8);
                Dataset[p + 2] = (byte)(value >> 16);
            }


            void write32(int p, int value)
            {
                Dataset[p] = (byte)value;
                Dataset[p + 1] = (byte)(value >> 8);
                Dataset[p + 2] = (byte)(value >> 16);
                Dataset[p + 3] = (byte)(value >> 24);
            }

            NOTJSON Add(string key, string value)
            {
                return Add(key, System.Text.Encoding.UTF8.GetBytes(value),0);
            }

            NOTJSON Add(string key, byte[] value,int type)
            {
                byte id;
                if (!FieldName.TryGetValue(key, out id)) // Update master key dictionary
                {
                    id = _Id++;
                    FieldName.Add(key, id);
                }

                //System.Text.Encoding.UTF8.GetString(utf8Bytes);

                int pointerend = read24(0);

                for (int i = 0; i < (pointerend - 4); i += 4)
                {
                    if (Dataset[i + 3] == 0) { Dataset[i + 3] = id; return Replace(i, ref value,0); }
                    if (Dataset[i + 3] == id) return Replace(i, ref value,0);
                }


                int newoffset = Length + 4;
                if (newoffset + value.Length > Dataset.Length)
                {
                    int newsize = newoffset + value.Length;
                    Instance.QueueChatMessage($"Resizing to {newsize} from {Length}");
                    Byte[] newbuffer = new byte[newsize];

                    Dataset.CopyTo(newbuffer, 4);
                    Dataset = newbuffer;

                    for (int i = 0; i < pointerend; i += 4)
                    {
                        write32(i, read32(i + 4) + 4);
                    }
                }
                else
                {
                    Instance.QueueChatMessage($"Adding key {newoffset + value.Length} / {Dataset.Length}");
                    Array.Copy(Dataset, pointerend, Dataset, pointerend + 4, newoffset - pointerend);
                    for (int i = 0; i < pointerend; i += 4)
                    {
                        write32(i, read32(i) + 4);
                    }
                }

                write24(pointerend, newoffset + value.Length);
                Dataset[pointerend - 1] = id;
                value.CopyTo(Dataset, newoffset);
                return this;
            }

            NOTJSON Replace(int pointer, ref byte[] value,int type)
            {
                Instance.QueueChatMessage($"Replacing key in position {pointer >> 2}");

                int dataoffset = read24(pointer);
                int size = (read24(pointer + 4) - dataoffset);
                int delta = value.Length - size;
                if (Length + delta > Dataset.Length)
                {
                    Byte[] newbuffer = new byte[Length + delta];
                    Dataset.CopyTo(newbuffer, 0);
                    Dataset = newbuffer;
                }

                Array.Copy(Dataset, read24(pointer + 4), Dataset, dataoffset + value.Length, Length - read24(pointer + 4));
                value.CopyTo(Dataset, read24(pointer));
                for (int i = pointer + 4; i < read24(0); i += 4) write24(i, read24(i) + delta);

                return this;
            }


            int DataStart()
            {
                return read24(0);
            }


            public class NOTJSONLIST : NOTJSON
            {
                int entry;

                List<string> asList
                {
                    get
                    {
                        int count = read32((entry + 1) * 4);
                        return new List<string>();
                    }
                }

            }

            public class VALUE : NOTJSON
            {
                int entry;

                List<string> asList
                {
                    get
                    {
                        int count = read24((entry + 1) * 4);
                        return new List<string>();
                    }
                }

            }



            string AsJSON
            {

                get
                {
                    var o = new StringBuilder();
                    o.Append('{');
                    for (int p = 0; p < DataStart() - 4; p += 4)
                    {
                        int k = Dataset[p + 3];
                        if (k > 0)
                        {
                            o.Append(FieldName.ElementAt(k - 1).Key);
                            o.Append("=\"");
                            o.Append(System.Text.Encoding.UTF8.GetString(Dataset, read24(p), read24(p + 4) - read24(p)));
                            o.Append("\" ");
                        }
                    }
                    o.Append('}');
                    return o.ToString();
                }

            }



            int Count
            {
                get
                {
                    return (DataStart() >> 2) - 1;
                }
            }

            int Length
            {
                get
                {
                    return read24(read24(0) - 4); // End of string is the last offset in the string offset array.
                }
            }

            static string hexdigit = "0123456789ABCDEF";

            public static void HexDump(Byte[] array, int offset = 0, int length = 32)
            {
                var line = new StringBuilder();
                for (int i = 0; i < Math.Min(length, array.Length); i++)
                {
                    line.Append(hexdigit[array[i] >> 4]);
                    line.Append(hexdigit[array[i] & 15]);
                    line.Append(' ');
                }
                if (line.Length > 0) Instance.QueueChatMessage(line.ToString());
            }

        }

        #endregion

        public class CoroutineWithData
        {
            public Coroutine coroutine { get; private set; }
            public object result;
            private readonly IEnumerator target;

            public CoroutineWithData(MonoBehaviour owner, IEnumerator target)
            {
                this.target = target;
                this.coroutine = owner.StartCoroutine(Run());
            }

            private IEnumerator Run()
            {
                while (target.MoveNext())
                {
                    result = target.Current;
                    yield return result;
                }
            }
        }

        //var updateFriendsApiCR = new CoroutineWithData(Instance, VersusApi.GetFriends());
        //yield return updateFriendsApiCR.coroutine;
        //var steamFriends = (SteamFriendsData)updateFriendsApiCR.result;

        public static IEnumerator MessageUpdater(TextMeshProUGUI textObject, string baseMessage, char indicator)
        {
            var time = 0f;
            var indicatorCount = 0;

            // set initial message
            textObject.SetText($"{baseMessage}");

            // begin loop
            while (true)
            {
                // increment time counter
                time += Time.deltaTime;

                // auto break after 60 seconds
                if (time > 60) yield break;

                // if we're past a second interval, update message
                if (time > 1)
                {
                    // increment count for number of indicators
                    indicatorCount++;

                    // set the text
                    textObject.SetText($"{baseMessage}{new string(indicator, indicatorCount)}");

                    // modulus 5 to cycle
                    indicatorCount %= 5;

                    // reset time
                    time = 0;
                }

                // yield to frame wait
                yield return null;
            }
        }


        //        // create message indicator for downloading
        //        MessageEnumerator = Utilities.MessageUpdater(downloadingText, "Downloading Song", '.');

        //                // start indicator
        //                StartCoroutine(MessageEnumerator);

        //// do yield stuff

        //                // stop message indicator
        //                if (MessageEnumerator != null)
        //                {
        //                    StopCoroutine(MessageEnumerator);
        //        MessageEnumerator = null;
        //                }

        //downloadingText = BeatSaberUI.CreateText(rect, "Downloading", new Vector2(0.0f, -20f));
        //        downloadingText.alignment = TextAlignmentOptions.Center;
        //        downloadingText.fontSize = 4f;
        //        downloadingText.gameObject.SetActive(false);

        //private IEnumerator MessageEnumerator;

        //public static class LevelListTableCellExtensions
        //{
        //    public static void DestroyBeatmapCharacteristics(this LevelListTableCell cell)
        //    {
        //        // remove characteristics images
        //        cell.SetPrivateField("_beatmapCharacteristicAlphas", new float[0]);
        //        cell.SetPrivateField("_beatmapCharacteristicImages", new UnityEngine.UI.Image[0]);
        //        cell.SetPrivateField("_bought", true);

        //        foreach (var icon in cell.GetComponentsInChildren<UnityEngine.UI.Image>().Where(x => x.name.StartsWith("LevelTypeIcon")))
        //        {
        //            UnityEngine.Object.Destroy(icon.gameObject);
        //        }
        //    }
        //}

        //var pi = typeof(SongLoaderPlugin.SongLoader).GetField("CustomSongsIcon", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        //    if (pi != null)
        //    {
        //        CustomSongsIcon = (Sprite) pi.GetValue(null);
        //}

        //public static Texture2D LoadTextureFromFile(string fileName)
        //{
        //    if (System.IO.File.Exists(fileName))
        //    {
        //        var data = System.IO.File.ReadAllBytes(fileName);
        //        var tex = new Texture2D(256, 256);
        //        tex.LoadImage(data);
        //        return tex;
        //    }
        //    return null;
        //}

        //var t = SongLoaderPlugin.SongLoader.CustomLevels.FirstOrDefault(c => c.levelID.StartsWith(hash.Hash));

        //    if (t != null)
        //    {
        //        if (t.coverImageTexture2D == Plugin.CustomSongsIcon.texture)
        //        {
        //            var tex = Utilities.LoadTextureFromFile(t.customSongInfo.path + "/" + t.customSongInfo.coverImagePath);
        //image.texture = tex;
        //        }
        //        else
        //        {
        //            image.texture = t.coverImageTexture2D;
        //        }
        //d    }

        //$"{t.customSongInfo.path}/{t.customSongInfo.coverImagePath}"

        //var ldata = BS_Utils.Plugin.LevelData;
        //var level = ldata.GameplayCoreSceneSetupData.difficultyBeatmap;
        //var song = SongLoader.CustomLevels.FirstOrDefault(x => x.levelID == level?.level.levelID);

#endif

    }

}