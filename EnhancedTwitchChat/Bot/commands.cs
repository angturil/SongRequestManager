#if REQUEST_BOT

using EnhancedTwitchChat.Chat;
using EnhancedTwitchChat.SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using System.Text.RegularExpressions;

// Feature requests: Add Reason for being banned to banlist

namespace EnhancedTwitchChat.Bot
{
    public partial class RequestBot : MonoBehaviour
    {

#region Command Registration 

        private void InitializeCommands()
        {

        /*
           Prototype of new calling convention for adding new commands.
          
            new COMMAND("command").Action(Routine).Help(Broadcasteronly, "usage: %alias", _anything);
            new COMMAND("command").Action(Routine);

            Note: Default permissions are broadcaster only, so don't need to set them

        */

            string [] array= Config.Instance.RequestCommandAliases.Split(',');

            new COMMAND(Config.Instance.RequestCommandAliases.Split(',')).Action(ProcessSongRequest).Help(Everyone, "usage: %alias%<songname> or <song id>, omit <,>'s. %|%This adds a song to the request queue. Try and be a little specific. You can look up songs on %beatsaver%", _atleast1);

            AddCommand("queue", ListQueue, Everyone, "usage: %alias%%|% ... Displays a list of the currently requested songs.", _nothing);

            AddCommand("unblock", Unban, Mod, "usage: %alias%<song id>, do not include <,>'s.", _beatsaversongversion);

            AddCommand("block", Ban, Mod, "usage: %alias%<song id>, do not include <,>'s.", _beatsaversongversion);

            AddCommand("remove", DequeueSong, Mod, "usage: %alias%<songname>,<username>,<song id> %|%... Removes a song from the queue.", _atleast1);

            AddCommand("clearqueue", Clearqueue, Broadcasteronly, "usage: %alias%%|%... Clears the song request queue. You can still get it back from the JustCleared deck, or the history window", _nothing);

            AddCommand("mtt", MoveRequestToTop, Mod, "usage: %alias%<songname>,<username>,<song id> %|%... Moves a song to the top of the request queue.", _atleast1);

            AddCommand("remap", Remap, Mod, "usage: %alias%<songid1> , <songid2>%|%... Remaps future song requests of <songid1> to <songid2> , hopefully a newer/better version of the map.", _RemapRegex);

            AddCommand("unmap", Unmap, Mod, "usage: %alias%<songid> %|%... Remove future remaps for songid.", _beatsaversongversion);

        
            new COMMAND (new string[] { "lookup", "find" }).Coroutine(LookupSongs).Help(Mod | Sub | VIP, "usage: %alias%<song name> or <beatsaber id>, omit <>'s.%|%Get a list of songs from %beatsaver% matching your search criteria.", _atleast1);

            AddCommand(new string[] { "last", "demote", "later" }, MoveRequestToBottom, Mod, "usage: %alias%<songname>,<username>,<song id> %|%... Moves a song to the bottom of the request queue.", _atleast1);

            AddCommand(new string[] { "wrongsong", "wrong", "oops" }, WrongSong, Everyone, "usage: %alias%%|%... Removes your last requested song form the queue. It can be requested again later.", _nothing);

            AddCommand("open", OpenQueue, Mod, "usage: %alias%%|%... Opens the queue allowing song requests.", _nothing);

            AddCommand("close", CloseQueue, Mod, "usage: %alias%%|%... Closes the request queue.", _nothing);

            AddCommand("restore", restoredeck, Broadcasteronly, "usage: %alias%%|%... Restores the request queue from the previous session. Only useful if you have persistent Queue turned off.", _nothing);

            AddCommand("commandlist", showCommandlist, Everyone, "usage: %alias%%|%... Displays all the bot commands available to you.", _nothing);

            AddCommand("played", ShowSongsplayed, Mod, "usage: %alias%%|%... Displays all the songs already played this session.", _nothing);

            AddCommand("blist", ShowBanList, Broadcasteronly, "usage: Don't use, it will spam chat!", _atleast1); // Purposely annoying to use, add a character after the command to make it happen 


            AddCommand("readdeck", Readdeck, Broadcasteronly, "usage: %alias", _alphaNumericRegex);
            AddCommand("writedeck", Writedeck, Broadcasteronly, "usage: %alias", _alphaNumericRegex);

            AddCommand("clearalreadyplayed", ClearDuplicateList, Broadcasteronly, "usage: %alias%%|%... clears the list of already requested songs, allowing them to be requested again.", _nothing); // Needs a better name

            AddCommand("help", help, Everyone, "usage: %alias%<command name>, or just %alias%to show a list of all commands available to you.", _anything);

            AddCommand("link", ShowSongLink, Everyone, "usage: %alias%|%... Shows details, and a link to the current song", _nothing);

            AddCommand("allowmappers", MapperAllowList, Broadcasteronly, "usage: %alias%<mapper list> %|%... Selects the mapper list used by the AddNew command for adding the latest songs from %beatsaver%, filtered by the mapper list.", _alphaNumericRegex);  // The message needs better wording, but I don't feel like it right now

            AddCommand("chatmessage", ChatMessage, Broadcasteronly, "usage: %alias%<what you want to say in chat, supports % variables>", _atleast1); // BUG: Song support requires more intelligent %CurrentSong that correctly handles missing current song. Also, need a function to get the currenly playing song.
            AddCommand("runscript", RunScript, Broadcasteronly, "usage: %alias%<name>%|%Runs a script with a .script extension, no conditionals are allowed. startup.script will run when the bot is first started. Its probably best that you use an external editor to edit the scripts which are located in UserData/EnhancedTwitchChat", _atleast1);
            AddCommand("history", ShowHistory, Mod, "usage: %alias% %|% Shows a list of the recently played songs, starting from the most recent.", _nothing);

            new COMMAND("att").Action(AddToTop).Help(Mod, "usage: %alias%<songname> or <song id>, omit <,>'s. %|%This adds a song to the top of the request queue. Try and be a little specific. You can look up songs on %beatsaver%", _atleast1);

            AddCommand("about", nop, Broadcasteronly, $"EnhancedTwitchChat Bot version 2.0.0. Developed by brian91292 and angturil. Find us on github.", _fail);



#if UNRELEASED


            // These are future features

            //AddCommand("/at"); // Scehdule at a certain time (command property)

            new COMMAND("every").Help(Broadcasteronly, "Run a command at a certain time", _atleast1); // BUG: No action
            new COMMAND("at").Help(Broadcasteronly, "Run a command at a certain time.", _atleast1); // BUG: No action
            new COMMAND("in").Help(Broadcasteronly, "Run a command at a certain time.", _atleast1); // BUG: No action
            new COMMAND("alias").Help(Broadcasteronly, "usage: %alias %|% Create a command alias, short cuts version a commands. Single line only. Supports %variables% (processed at execution time), parameters are appended.", _atleast1); // BUG: No action


            new COMMAND("who").Action(Who).Help(Mod, "usage: %alias% <songid or name>%|%Find out who requested the song in the currently queue or recent history.",_atleast1) ; 
            new COMMAND("songmsg").Action(SongMsg).Help(Mod, "usage: %alias% <songid> Message%|%Assign a message to the song",_atleast1); 
            new COMMAND("detail"); // Get song details

            COMMAND.InitializeCommands();

            AddCommand("blockmappers", MapperBanList, Broadcasteronly, "usage: %alias%<mapper list> %|%... Selects a mapper list that will not be allowed in any song requests.", _alphaNumericRegex); // BUG: This code is behind a switch that can't be enabled yet.


            // Temporary commands for testing, most of these will be unified in a general list/parameter interface

            new COMMAND(new string[] { "addnew", "addlatest" }).Coroutine(addsongsFromnewest).Help(Mod, "usage: %alias% <listname>%|%... Adds the latest maps from %beatsaver%, filtered by the previous selected allowmappers command", _nothing);

            new COMMAND("addsongs").Coroutine(addsongs).Help(Broadcasteronly, "usage: %alias%%|% Add all songs matching a criteria (up to 40) to the queue", _atleast1);

            AddCommand("openlist", OpenList);
            AddCommand("unload", UnloadList);
            AddCommand("clearlist", ClearList);
            AddCommand("write", writelist);
            AddCommand("list", ListList);
            AddCommand("lists", showlists);
            AddCommand("addtolist", Addtolist, Broadcasteronly, "usage: %alias%<list> <value to add>", _atleast1);
            AddCommand("addtoqueue", queuelist, Broadcasteronly, "usage: %alias%<list>", _atleast1);

            AddCommand("removefromlist", RemoveFromlist, Broadcasteronly, "usage: %alias%<list> <value to add>", _atleast1);
            AddCommand("listundo", Addtolist, Broadcasteronly, "usage: %alias%<list>", _atleast1); // BUG: No function defined yet, undo the last operation

            AddCommand("deck", createdeck);
            AddCommand("unloaddeck", unloaddeck);
            AddCommand("loaddecks", loaddecks);
            AddCommand("decklist", decklist, Mod, "usage: %alias", _deck);
            AddCommand("whatdeck", whatdeck, Mod, "usage: %alias%<songid> or 'current'", _beatsaversongversion);

            new COMMAND("mapper").Coroutine(addsongsBymapper).Help(Broadcasteronly, "usage: %alias%<mapperlist>");
#endif
        }

    
       // return a songrequest match in a SongRequest list. Good for scanning Queue or History
       SongRequest FindMatch(List <SongRequest> queue,string request)
            {
            var songId = GetBeatSaverId(request);
            foreach (var entry in queue)
            {
                var song = entry.song;

                if (songId == "")
                {
                    string[] terms = new string[] { song["songName"].Value, song["songSubName"].Value, song["authorName"].Value, song["version"].Value, entry.requestor.displayName };

                    if (DoesContainTerms(request, ref terms)) return entry;
                }
                else
                {
                    if (song["id"].Value == songId) return entry;
                }
            }

            return null;
        }

        public string Who(COMMAND cmd, TwitchUser requestor, string request, CmdFlags flags, string info)
            {

            SongRequest result = null;
            result = FindMatch(RequestQueue.Songs, request);
            if (result == null) result=FindMatch(RequestHistory.Songs, request);

            if (result!=null) QueueChatMessage($"{result.song["songName"].Value} requested by {result.requestor.displayName}.");
            return empty;
        }
    
        public string SongMsg(COMMAND cmd, TwitchUser requestor, string request,CmdFlags flags,string info)
        {
            string[] parts  =request.Split(new char[] { ' ', ',' }, 2);
            var songId = GetBeatSaverId(parts[0]);
            if (songId=="")
                {
                QueueChatMessage($"Usage: ... <songid>");
                return empty;  
                }
            foreach (var entry in RequestQueue.Songs)
            {
                var song = entry.song;

                if (song["id"].Value == songId)
                    {
                    entry.requestInfo = parts[1];   
                    //QueueChatMessage($"{song["songName"].Value} : {parts[1]}");
                    return empty;
                    }
            }
            QueueChatMessage($"Unable to find {songId}");

            return empty;
        }


        public void Alias(COMMAND cmd, TwitchUser requestor, string request, CmdFlags flags, string info)
            {
            
            }


        public partial class COMMAND
        {
            public static List<COMMAND> cmdlist = new List<COMMAND>(); // Collection of our command objects
            public static Dictionary<string, int> aliaslist = new Dictionary<string, int>();

            private Action<TwitchUser, string> Method = null;  // Method to call
            private Action<TwitchUser, string, CmdFlags, string> Method2 = null; // Alternate method
            private Func<COMMAND, TwitchUser, string, CmdFlags, string,string> Method3 = null; // Prefered method, returns the error msg as a string.
            private Func<TwitchUser,string,IEnumerator> func1=null;

            public CmdFlags Flags = Broadcasteronly;          // flags
            public string ShortHelp = "";                   // short help text (on failing preliminary check
            public List<string> aliases = null;               // list of command aliases
            public Regex regexfilter = _anything;                 // reg ex filter to apply. For now, we're going to use a single string

            public string LongHelp = null; // Long help text
            public string HelpLink = null; // Help website link, Using a wikia might be the way to go
            public string permittedusers = ""; // Name of list of permitted users.
            public string userParameter = ""; // This is here incase I need it for some specific purpose
            public int userNumber = 0;
            public int UseCount = 0;  // Number of times command has been used, sadly without references, updating this is costly.

            public void SetPermittedUsers(string listname)
            {
                // BUG: Needs additional checking

                string fixedname = listname.ToLower();
                if (!fixedname.EndsWith(".users")) fixedname += ".users";
                permittedusers = fixedname;
            }

            public void Execute(ref TwitchUser user, ref string request, CmdFlags flags, ref string Info)
            {
                if (Method2 != null) Method2(user, request, flags, Info);
                else if (Method != null) Method(user, request);
                else if (Method3 != null) Method3(this, user, request, flags, Info);
                else if (func1 != null) Instance.StartCoroutine(func1(user, request));
            }


            public static void InitializeCommands()
            {

            }


            COMMAND AddAliases()
                {

                foreach (var entry in aliases)
                {
                    if (!aliaslist.ContainsKey(entry)) aliaslist.Add(entry, cmdlist.Count);
                }
                cmdlist.Add(this);
                return this;
                }

            public COMMAND(string alias)
                {
                aliases = new List<string>();
                aliases.Add(alias.ToLower());
                AddAliases();
                }

            public COMMAND (string [] alias)
            {
                aliases = new List<string>();
                foreach (var element in alias)
                {
                    aliases.Add(element.ToLower());
                }
                AddAliases();
            }

            public COMMAND Help(CmdFlags flags=Broadcasteronly, string ShortHelp="", Regex regexfilter=null)
            {
                this.Flags = flags;
                this.ShortHelp = ShortHelp;
                this.regexfilter = regexfilter!=null ? regexfilter : _anything ;
                
                COMMAND.cmdlist.Add(this);

                return this;
            }

            public COMMAND User(string userstring)
                {
                userParameter = userstring;
                return this;
                }

            public COMMAND Action(Func<COMMAND, TwitchUser, string, CmdFlags, string,string> action)
                {
                Method3 = action;       
                return this;
                }

            public COMMAND Action(Action<TwitchUser, string, CmdFlags, string> action)
                {
                Method2 = action;
                return this;
                }

            public COMMAND Action(Action<TwitchUser, string> action)
                {
                Method = action;
                return this;
                }

            public COMMAND Coroutine( Func <TwitchUser,string,IEnumerator> action)
            {
                func1 = action;
                return this;
            }


            public static void ExecuteCommand(string command, ref TwitchUser user, string param, CmdFlags commandflags = 0, string info = "")
            {
                COMMAND botcmd;

                int botindex;

                //if (!NewCommands.TryGetValue(command, out botcmd)) return; // Unknown command

                if (!aliaslist.TryGetValue(command, out botindex)) return; // Unknown command

                botcmd = cmdlist[botindex];

                // BUG: This is prototype code, it will of course be replaced. This message will be removed when its no longer prototype code

                // Permissions for these sub commands will always be by Broadcaster,or the (BUG: Future feature) user list of the EnhancedTwitchBot command. Note command behaviour that alters with permission should treat userlist as an escalation to Broadcaster.
                // Since these are never meant for an end user, they are not going to be configurable.

                // Example: !challenge/allow myfriends
                //          !decklist/setflags SUB
                //          !lookup/sethelp usage: %alias%<song name or id>

                string[] parts= { };
                string subcommand = "";

                if (param.StartsWith("/"))
                    {
                    parts = param.Split(new char[] { ' ', ',' }, 2);
                    subcommand = parts[0].ToLower();
                    }


                if (user.isBroadcaster && subcommand.Length>0)
                {
 
                    if (subcommand.StartsWith("/allow")) // 
                    {
                        if (parts.Length > 1)
                        {
                            string key = parts[1].ToLower();
                            cmdlist[botindex].permittedusers = key;
                            Instance?.QueueChatMessage($"Permit custom userlist set to  {key}.");
                        }

                        return;
                    }

                    if (subcommand.StartsWith("/disable")) // 
                    {
                        Instance?.QueueChatMessage($"{command} Disabled.");

                        botcmd.Flags |= CmdFlags.Disabled;

                        //botcmd.rights |= CmdFlags.Disabled;
                        //NewCommands[command] = botcmd;
                        return;
                    }

                    if (subcommand.StartsWith("/enable")) // 
                    {

                        Instance?.QueueChatMessage($"{command} Enabled.");
                        //botcmd.rights &= ~CmdFlags.Disabled;
                        //NewCommands[command] = botcmd;

                        botcmd.Flags &= ~CmdFlags.Disabled;

                        return;
                    }


                    if (subcommand.StartsWith("/sethelp")) // 
                    {
                        if (parts.Length > 1)
                        {
                            botcmd.ShortHelp = parts[1];

                            Instance?.QueueChatMessage($"{command} help: {parts[1]}");
                        }

                        return;
                    }

                    if (subcommand.StartsWith("/flags")) // 
                    {
                        Instance?.QueueChatMessage($"{command} flags: {botcmd.Flags.ToString()}");
                        return;
                    }

                    if (subcommand.StartsWith("/setflags")) // 
                    {
                        if (parts.Length > 1)
                        {
                            string[] flags = parts[1].Split(new char[] { ' ', ',' });


                            CmdFlags flag;

                            //NewCommands[command].rights ;

                            // BUG: Not working yet

                            Instance?.QueueChatMessage($"Not implemented");
                        }
                        return;

                    }

                    if (subcommand.StartsWith("/silent"))
                    {
                        // BUG: Making the output silent doesn't work yet.

                        param = parts[1]; // Eat the switch, allowing the command to continue
                    }

                if (subcommand.StartsWith("/test"))
                    {
                        // BUG: Making the output silent doesn't work yet.

                        //cmdlist[botindex].Method = cmdlist[botindex].TestList;
                        param = parts[1]; // Eat the switch, allowing the command to continue
                    }


                }

                if (botcmd.Flags.HasFlag(CmdFlags.Disabled)) return; // Disabled commands fail silently

                // Check permissions first

                bool allow = HasRights(ref botcmd, ref user);

                if (!allow && !botcmd.Flags.HasFlag(CmdFlags.BypassRights) && !listcollection.contains(ref botcmd.permittedusers, user.displayName.ToLower()))
                {
                    CmdFlags twitchpermission = botcmd.Flags & CmdFlags.TwitchLevel;
                    if (!botcmd.Flags.HasFlag(CmdFlags.SilentPreflight)) Instance?.QueueChatMessage($"{command} is restricted to {twitchpermission.ToString()}");
                    return;
                }

                if (param == "?") // Handle per command help requests - If permitted.
                {
                    ShowHelpMessage(ref botcmd, ref user, param, true);
                    return;
                }


                // Command local switches. These must be run before regex to avoid having to check for all of them.
                try
                {
                    if (subcommand.StartsWith("/current"))
                    {
                        param = RequestHistory.Songs[0].song["version"];
                    }
                    else if (subcommand.StartsWith("/prev"))
                    {
                        param = RequestHistory.Songs[1].song["version"];
                    }
                }
                catch       
                {
                    RequestBot.Instance.QueueChatMessage($"There is no {subcommand} song available.");
                    return;

                }

                // Check regex

                if (!botcmd.regexfilter.IsMatch(param))
                {
                    ShowHelpMessage(ref botcmd, ref user, param, false);
                    return;
                }


                try
                {
                    botcmd.Execute(ref user, ref param, commandflags, ref info); // Call the command
                }
                catch (Exception ex)
                {
                    // Display failure message, and lock out command for a time period. Not yet.

                    Plugin.Log(ex.ToString());

                }

            }


            public static void Parse(TwitchUser user, string request, CmdFlags flags = 0, string info = "")
            {
                if (!Instance) return;

                if (request.Length == 0) return; // Since we allow user configurable commands, blanks are a possibility

                if (request[0] != '!') return; // This won't always be here

                int commandstart = 1; // This is technically 0, right now we're setting it to 1 to maintain the ! behaviour
                int parameterstart = 1;

                //var match = Regex.Match(request, "^!(?<command>[^ ^/]*?<parameter>.*)");
                //string username = match.Success ? match.Groups["command"].Value : null;

                // This is a replacement for the much simpler Split code. It was changed to support /fakerest parameters, and sloppy users ... ie: !add4334-333 should now work, so should !command/flags
                while (parameterstart < request.Length && ((request[parameterstart] < '0' || request[parameterstart] > '9') && request[parameterstart] != '/' && request[parameterstart] != ' ')) parameterstart++;  // Command name ends with #... for now, I'll clean up some more later           
                int commandlength = parameterstart - commandstart;
                while (parameterstart < request.Length && request[parameterstart] == ' ') parameterstart++; // Eat the space(s) if that's the separator after the command

                if (commandlength == 0) return;

                string command = request.Substring(commandstart, commandlength).ToLower();
                if (aliaslist.ContainsKey(command))
                {
                    string param = request.Substring(parameterstart);


                    try
                    {
                        ExecuteCommand(command, ref user, param, flags, info);
                    }
                    catch (Exception ex)
                    {
                        // Display failure message, and lock out command for a time period. Not yet.

                        Plugin.Log(ex.ToString());

                    }
                }
            }


        }

        // BUG: These are all the same. This interface needs more cleanup.

        public void AddCommand(string[] alias, Action<TwitchUser, string> method, CmdFlags flags = Broadcasteronly, string shorthelptext = "usage: [%alias] ... Rights: %rights", Regex regex = null)
        {
            new COMMAND(alias).Action(method).Help(flags, shorthelptext, regex);

        }

        public void AddCommand(string alias, Action<TwitchUser, string> method, CmdFlags flags = Broadcasteronly, string shorthelptext = "usage: [%alias] ... Rights: %rights", Regex regex = null)
        {
            new COMMAND(alias).Action(method).Help(flags, shorthelptext, regex);
        }



        // A much more general solution for extracting dymatic values into a text string. If we need to convert a text message to one containing local values, but the availability of those values varies by calling location
        // We thus build a table with only those values we have. 



        // BUG: This is actually part of botcmd, please move
        public static void ShowHelpMessage(ref COMMAND botcmd, ref TwitchUser user, string param, bool showlong)
        {
            if (botcmd.Flags.HasFlag(CmdFlags.QuietFail) || botcmd.Flags.HasFlag(CmdFlags.Disabled)) return; // Make sure we're allowed to show help

            new DynamicText().AddUser(ref user).AddBotCmd(ref botcmd).QueueMessage(ref botcmd.ShortHelp, showlong);

            return;
        }


        private void nop(TwitchUser requestor, string request)
        {
            // This is command does nothing, it can be used as a placeholder for help text aliases.
        }

        // Get help on a command
        private void help(TwitchUser requestor, string request)
        {
            if (request == "")
            {
                var msg = new QueueLongMessage();
                msg.Header("Usage: help < ");
                foreach (var entry in COMMAND.aliaslist)
                {
                    var botcmd = COMMAND.cmdlist[entry.Value];
                    if (HasRights(ref botcmd, ref requestor))
                        msg.Add($"{entry.Key}", " ");
                }
                msg.Add(">");
                msg.end("...", $"No commands available >");
                return;
            }
            if (COMMAND.aliaslist.ContainsKey(request.ToLower()))
            {
                var BotCmd = COMMAND.cmdlist[COMMAND.aliaslist[request.ToLower()]];
                ShowHelpMessage(ref BotCmd, ref requestor, request, true);
            }
            else
            {
                QueueChatMessage($"Unable to find help for {request}.");
            }
        }

        public static bool HasRights(ref COMMAND botcmd, ref TwitchUser user)
        {
            if (botcmd.Flags.HasFlag(CmdFlags.Disabled)) return false;
            if (botcmd.Flags.HasFlag(CmdFlags.Everyone)) return true; // Not sure if this is the best approach actually, not worth thinking about right now
            if (user.isBroadcaster & botcmd.Flags.HasFlag(CmdFlags.Broadcaster)) return true;
            if (user.isMod & botcmd.Flags.HasFlag(CmdFlags.Mod)) return true;
            if (user.isSub & botcmd.Flags.HasFlag(CmdFlags.Sub)) return true;
            if (user.isVip & botcmd.Flags.HasFlag(CmdFlags.VIP)) return true;
            return false;

        }

        // You can modify commands using !allow !setflags !clearflags and !sethelp
#endregion

  

    }
}
#endif