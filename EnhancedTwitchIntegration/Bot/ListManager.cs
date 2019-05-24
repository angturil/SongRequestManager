
using StreamCore;
using StreamCore.Chat;
using StreamCore.SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace SongRequestManager
{
    public partial class RequestBot : MonoBehaviour
    {

        #region List Manager Related functions ...

        // List types:

        // This is a work in progress. 

        // .deck = lists of songs
        // .mapper = mapper lists
        // .users = twitch user lists
        // .command = command lists = linear scripting
        // .dict = list contains key value pairs
        // .json = (not part of list manager.. yet)

        // This code is currently in an extreme state of flux. Underlying implementation will change.
        
        private void OpenList(TwitchUser requestor, string request)
        {
            listcollection.OpenList(request.ToLower());
        }
        
        public static ListCollectionManager listcollection = new ListCollectionManager();
        
        [Flags] public enum ListFlags { ReadOnly = 1, InMemory = 2, Uncached = 4, Dynamic = 8, LineSeparator = 16, Unchanged = 256 };

        // The list collection maintains a dictionary of named, persistent lists. Accessing a collection by name automatically loads or crates it.
        // What I really want though is a collection of container objects with the same interface. I need to look into Dynamic to see if I can make this work. Damn being a c# noob
        public class ListCollectionManager
        {

            // BUG: DoNotCreate flags currently do nothing
            // BUG: List name case normalization is inconsistent. I'll probably fix it by changing the list interface (its currently just the filename)

            public Dictionary<string, StringListManager> ListCollection = new Dictionary<string, StringListManager>();
            
            public ListCollectionManager()
            {
                // Add an empty list so we can set various lists to empty
                StringListManager empty = new StringListManager();
                ListCollection.Add("empty", empty);
            }

            public StringListManager ClearOldList(string request, TimeSpan delta, ListFlags flags = ListFlags.Unchanged)
            {
                string listfilename = Path.Combine(Plugin.DataPath, request);
                TimeSpan UpdatedAge = GetFileAgeDifference(listfilename);

                StringListManager list = OpenList(request, flags);

                if (File.Exists(listfilename) && UpdatedAge > delta) // BUG: There's probably a better way to handle this
                {
                    RequestBot.Instance.QueueChatMessage($"Clearing old session {request}");
                    list.Clear();
                    if (!(flags.HasFlag(ListFlags.InMemory) | flags.HasFlag(ListFlags.ReadOnly))) list.Writefile(request);

                }

                return list;
            }
            
            public StringListManager OpenList(string request, ListFlags flags = ListFlags.Unchanged) // All lists are accessed through here, flags determine mode
            {
                StringListManager list;
                if (!ListCollection.TryGetValue(request, out list))
                {
                    list = new StringListManager();
                    ListCollection.Add(request, list);
                    if (!flags.HasFlag(ListFlags.InMemory)) list.Readfile(request); // If in memory, we never read from disk
                }
                else
                {
                    if (flags.HasFlag(ListFlags.Uncached)) list.Readfile(request); // If Cache is off, ALWAYS re-read file.
                }
                return list;
            }

            public bool contains(ref string listname, string key, ListFlags flags = ListFlags.Unchanged)
            {
                try
                {
                    StringListManager list = OpenList(listname);
                    return list.Contains(key);
                }
                catch (Exception ex) { Plugin.Log(ex.ToString()); } // Going to try this form, to reduce code verbosity.              

                return false;
            }

            public bool add(string listname, string key, ListFlags flags = ListFlags.Unchanged)
            {
                return add(ref listname, ref key, flags);
            }

            public bool add(ref string listname, ref string key, ListFlags flags = ListFlags.Unchanged)
            {
                try
                {
                    StringListManager list = OpenList(listname);

                    list.Add(key);


                    if (!(flags.HasFlag(ListFlags.InMemory) | flags.HasFlag(ListFlags.ReadOnly))) list.Writefile(listname);
                    return true;

                }
                catch (Exception ex) { Plugin.Log(ex.ToString()); }

                return false;
            }

            public bool remove(string listname, string key, ListFlags flags = ListFlags.Unchanged)
            {
                return remove(ref listname, ref key, flags);
            }
            public bool remove(ref string listname, ref string key, ListFlags flags = ListFlags.Unchanged)
            {
                try
                {
                    StringListManager list = OpenList(listname);

                    list.Removeentry(key);

                    if (!(flags.HasFlag(ListFlags.InMemory) | flags.HasFlag(ListFlags.ReadOnly))) list.Writefile(listname);

                    return false;

                }
                catch (Exception ex) { Plugin.Log(ex.ToString()); } // Going to try this form, to reduce code verbosity.              

                return false;
            }


            public void runscript(string listname, ListFlags flags = ListFlags.Unchanged)
            {

                try
                {
                    OpenList(listname, flags).runscript();

                }
                catch (Exception ex) { Plugin.Log(ex.ToString()); } // Going to try this form, to reduce code verbosity.              
            }
            
            public void ClearList(string listname, ListFlags flags = ListFlags.Unchanged)
            {
                try
                {
                    OpenList(listname).Clear();
                }
                catch (Exception ex) { Plugin.Log(ex.ToString()); } // Going to try this form, to reduce code verbosity.              
            }

        }

        // All variables are public for now until we finalize the interface
        public class StringListManager
        {

            private static char[] anyseparator = { ',', ' ', '\t', '\r', '\n' };
            private static char[] lineseparator = { '\n', '\r' };

            public List<string> list = new List<string>();
            private HashSet<string> hashlist = new HashSet<string>();


            ListFlags flags = 0;

            // Callback function prototype here

            public StringListManager(ListFlags ReadOnly = ListFlags.Unchanged)
            {

            }

            public bool Readfile(string filename, bool ConvertToLower = false)
            {
                if (flags.HasFlag(ListFlags.InMemory)) return false;

                try
                {
                    string listfilename = Path.Combine(Plugin.DataPath, filename);
                    string fileContent = File.ReadAllText(listfilename);
                    if (listfilename.EndsWith(".script"))
                        list = fileContent.Split(lineseparator, StringSplitOptions.RemoveEmptyEntries).ToList();
                    else
                        list = fileContent.Split(anyseparator, StringSplitOptions.RemoveEmptyEntries).ToList();

                    if (ConvertToLower) LowercaseList();
                    return true;
                }
                catch
                {
                    // Ignoring this for now, I expect it to fail
                }

                return false;
            }

            public void runscript()
            {

                try
                {

                    // BUG: A DynamicText context needs to be applied to each command to allow use of dynamic variables

                    foreach (var line in list) COMMAND.Parse(TwitchWebSocketClient.OurTwitchUser, line);
                }
                catch (Exception ex) { Plugin.Log(ex.ToString()); } // Going to try this form, to reduce code verbosity.            

            }


            public bool Writefile(string filename)
            {
                string separator = filename.EndsWith(".script") ? "\r\n" : ",";

                try
                {
                    string listfilename = Path.Combine(Plugin.DataPath, filename);

                    var output = String.Join(separator, list.ToArray());
                    File.WriteAllText(listfilename, output);
                    return true;
                }
                catch
                {
                    // Ignoring this for now, failed write can be silent
                }
                return false;
            }


            public bool Contains(string entry)
            {
                if (list.Contains(entry)) return true;
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

            public void LowercaseList()
            {
                for (int i = 0; i < list.Count; i++)
                {
                    list[i] = list[i].ToLower();
                }
            }
            public void Outputlist(ref QueueLongMessage msg, string separator = ", ")
            {
                foreach (string entry in list) msg.Add(entry, separator);
            }

        }

        public static List<JSONObject> ReadJSON(string path)
        {
            List<JSONObject> objs = new List<JSONObject>();
            if (File.Exists(path))
            {
                JSONNode json = JSON.Parse(File.ReadAllText(path));
                if (!json.IsNull)
                {
                    foreach (JSONObject j in json.AsArray)
                        objs.Add(j);
                }
            }
            return objs;
        }

        public static void WriteJSON(string path, ref List<JSONObject> objs)
        {
            if (!Directory.Exists(Path.GetDirectoryName(path)))
                Directory.CreateDirectory(Path.GetDirectoryName(path));

            JSONArray arr = new JSONArray();
            foreach (JSONObject obj in objs)
                arr.Add(obj);

            File.WriteAllText(path, arr.ToString());
        }


        #endregion


    }
}
