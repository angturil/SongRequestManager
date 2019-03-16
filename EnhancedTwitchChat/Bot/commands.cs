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
        #region COMMANDFLAGS
        [Flags]
        public enum CmdFlags
        {
            None = 0,
            Everyone = 1, // Im
            Sub = 2,
            Mod = 4,
            Broadcaster = 8,
            VIP = 16,
            UserList = 32,  // If this is enabled, users on a list are allowed to use a command (this is an OR, so leave restrictions to Broadcaster if you want ONLY users on a list)
            TwitchLevel = 63, // This is used to show ONLY the twitch user flags when showing permissions

            ShowRestrictions = 64, // Using the command without the right access level will show permissions error. Mostly used for commands that can be unlocked at different tiers.

            BypassRights = 128, // Bypass right check on command, allowing error messages, and a later code based check. Often used for help only commands. 
            xxxQuietFail = 256, // Return no results on failed preflight checks.

            HelpLink = 512, // Enable link to web documentation

            WhisperReply = 1024, // Reply in a whisper to the user (future feature?). Allow commands to send the results to the user, avoiding channel spam

            Timeout = 2048, // Applies a timeout to regular users after a command is succesfully invoked this is just a concept atm
            TimeoutSub = 4096, // Applies a timeout to Subs
            TimeoutVIP = 8192, // Applies a timeout to VIP's
            TimeoutMod = 16384, // Applies a timeout to MOD's. A way to slow spamming of channel for overused commands. 

            NoLinks = 32768, // Turn off any links that the command may normally generate

            //Silent = 65536, // Command produces no output at all - but still executes
            Verbose = 131072, // Turn off command output limits, This can result in excessive channel spam
            Log = 262144, // Log every use of the command to a file
            RegEx = 524288, // Enable regex check
            UserFlag1 = 1048576, // Use it for whatever bit makes you happy 
            UserFlag2 = 2097152, // Use it for whatever bit makes you happy 
            UserFlag3 = 4194304, // Use it for whatever bit makes you happy 
            UserFlag4 = 8388608, // Use it for whatever bit makes you happy 

            SilentPreflight = 16277216, //  

            MoveToTop = 1 << 25, // Private, used by ATT command. Its possible to have multiple aliases for the same flag

            SilentCheck = 1 << 26, // Initial command check failure returns no message
            SilentError = 1 << 27, // Command failure returns no message
            SilentResult = 1 << 28, // Command returns no visible results

            Silent = SilentCheck | SilentError | SilentResult,

            Subcommand=1<<29, // This is a subcommand, it may only be invoked within a command

            Disabled = 1 << 30, // If ON, the command will not be added to the alias list at all.

        }




        const CmdFlags Default = 0;
        const CmdFlags Everyone = Default | CmdFlags.Everyone;
        const CmdFlags Broadcasteronly = Default | CmdFlags.Broadcaster;
        const CmdFlags Mod = Default | CmdFlags.Broadcaster | CmdFlags.Mod;
        const CmdFlags Sub = Default | CmdFlags.Sub;
        const CmdFlags VIP = Default | CmdFlags.VIP;
        const CmdFlags Help = CmdFlags.BypassRights;
        const CmdFlags Silent = CmdFlags.Silent;
        const CmdFlags Subcmd = CmdFlags.Subcommand | Broadcasteronly;

        #endregion

        #region common Regex expressions

        private static readonly Regex _digitRegex = new Regex("^[0-9]+$", RegexOptions.Compiled);
        private static readonly Regex _beatSaverRegex = new Regex("^[0-9]+-[0-9]+$", RegexOptions.Compiled);
        private static readonly Regex _alphaNumericRegex = new Regex("^[0-9A-Za-z]+$", RegexOptions.Compiled);
        private static readonly Regex _RemapRegex = new Regex("^[0-9]+,[0-9]+$", RegexOptions.Compiled);
        private static readonly Regex _beatsaversongversion = new Regex("^[0-9]+$|^[0-9]+-[0-9]+$", RegexOptions.Compiled);
        private static readonly Regex _nothing = new Regex("$^", RegexOptions.Compiled);
        private static readonly Regex _anything = new Regex(".*", RegexOptions.Compiled); // Is this the most efficient way?
        private static readonly Regex _atleast1 = new Regex("..*", RegexOptions.Compiled); // Allow usage message to kick in for blank 
        private static readonly Regex _fail = new Regex("(?!x)x", RegexOptions.Compiled); // Not sure what the official fastest way to auto-fail a match is, so this will do
        private static readonly Regex _deck = new Regex("^(current|draw|first|last|random|unload)$|$^", RegexOptions.Compiled); // Checks deck command parameters

        private static readonly Regex _drawcard = new Regex("($^)|(^[0-9]+$|^[0-9]+-[0-9]+$)", RegexOptions.Compiled);

        #endregion


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

            AddCommand(new string[] { "help",".help" }, help, Everyone, "usage: %alias%<command name>, or just %alias%to show a list of all commands available to you.", _anything);

            AddCommand("link", ShowSongLink, Everyone, "usage: %alias%|%... Shows details, and a link to the current song", _nothing);

            AddCommand("allowmappers", MapperAllowList, Broadcasteronly, "usage: %alias%<mapper list> %|%... Selects the mapper list used by the AddNew command for adding the latest songs from %beatsaver%, filtered by the mapper list.", _alphaNumericRegex);  // The message needs better wording, but I don't feel like it right now

            AddCommand("chatmessage", ChatMessage, Broadcasteronly, "usage: %alias%<what you want to say in chat, supports % variables>", _atleast1); // BUG: Song support requires more intelligent %CurrentSong that correctly handles missing current song. Also, need a function to get the currenly playing song.
            AddCommand("runscript", RunScript, Broadcasteronly, "usage: %alias%<name>%|%Runs a script with a .script extension, no conditionals are allowed. startup.script will run when the bot is first started. Its probably best that you use an external editor to edit the scripts which are located in UserData/EnhancedTwitchChat", _atleast1);
            AddCommand("history", ShowHistory, Mod, "usage: %alias% %|% Shows a list of the recently played songs, starting from the most recent.", _nothing);

            new COMMAND("att").Action(AddToTop).Help(Mod, "usage: %alias%<songname> or <song id>, omit <,>'s. %|%This adds a song to the top of the request queue. Try and be a little specific. You can look up songs on %beatsaver%", _atleast1);

            new COMMAND("about").Help(Broadcasteronly, $"EnhancedTwitchChat Bot version 2.0.0. Developed by brian91292 and angturil. Find us on github.", _fail); // Help commands have no code

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


            // BEGIN SUBCOMMANDS - these modify the Properties of a command, or the current parse state. 

            new COMMAND("/enable").Action(SubcmdEnable).Help(Subcmd, "usage: <command>/enable");
            new COMMAND("/disable").Action(SubcmdDisable).Help(Subcmd, "usage: <command>/disable");
            new COMMAND("/current").Action(SubcmdCurrentSong).Help(Subcmd | Everyone, "usage: <command>/current");
            new COMMAND("/last").Action(SubcmdPreviousSong).Help(Subcmd | Everyone, "usage: <command>/last");
            new COMMAND("/next").Action(SubcmdNextSong).Help(Subcmd | Everyone, "usage: <command>/next");

            new COMMAND("/flags").Action(SubcmdShowflags).Help(Subcmd, "usage: <command>/next");
            new COMMAND("/set").Action(SubcmdSetflags).Help(Subcmd, "usage: <command>/set");
            new COMMAND("/clear").Action(SubcmdClearflags).Help(Subcmd, "usage: <command>/clear");

            new COMMAND("/allow").Action(SubcmdAllow).Help(Subcmd, "usage: <command>/allow");
            new COMMAND("/helpmsg").Action(SubcmdSethelp).Help(Subcmd, "usage: <command>/helpmsg");
            new COMMAND("/silent").Action(SubcmdSilent).Help(Subcmd | Everyone, "usage: <command>/silent");


        }


        public void Alias(COMMAND cmd, TwitchUser requestor, string request, CmdFlags flags, string info)
        {

        }

        #region Subcommands
        public string SubcmdEnable(ParseState state)
        {
            state.botcmd.Flags &= ~CmdFlags.Disabled;
            Instance?.QueueChatMessage($"{state.command} Enabled.");
            return "X";
        }

        public string SubcmdDisable(ParseState state)
        {
            state.botcmd.Flags |= CmdFlags.Disabled;
            Instance?.QueueChatMessage($"{state.command} Disabled.");
            return "X";
        }

        public string SubcmdCurrentSong(ParseState state)
            {
            try
            {
            state.parameter += RequestHistory.Songs[0].song["version"];
            return empty;
            }
            catch
            {
            // Being lazy, incase RequestHistory access failure.
            }

        return state.error($"Theree is no current song available");
        }

        public string SubcmdPreviousSong(ParseState state)
        {
            try
            {
                state.parameter += RequestHistory.Songs[1].song["version"];
                return empty;
            }
            catch
            {
                // Being lazy, incase RequestHistory access failure.
            }

            return state.error($"Theree is no previous song available");
        }

        public string SubcmdNextSong(ParseState state)
        {
            try
            {
                state.parameter += RequestQueue.Songs[0].song["version"];
                return empty;
            }
            catch
            {
                // Being lazy, incase RequestHistory access failure.
            }

            return state.error($"There are no songs in the queue.");
        }


        public string SubcmdShowflags(ParseState state)
            {
            Instance?.QueueChatMessage($"{state.command} flags: {state.botcmd.Flags.ToString()}");
            return "X";
            }

        public string SubcmdSetflags(ParseState state)
            {
            string[] flags = state.parameter.Split(new char[] { ' ', ',' });

            CmdFlags flag = (CmdFlags)Enum.Parse(typeof(CmdFlags), state.parameter);

            state.botcmd.Flags |= flag;

            Instance?.QueueChatMessage($"{state.command} flags: {state.botcmd.Flags.ToString()}");

            return "X";
            }

        public string SubcmdClearflags(ParseState state)
        {
            string[] flags = state.parameter.Split(new char[] { ' ', ',' });

            CmdFlags flag = (CmdFlags)Enum.Parse(typeof(CmdFlags), state.parameter);

            state.botcmd.Flags &= ~flag;

            Instance?.QueueChatMessage($"{state.command} flags: {state.botcmd.Flags.ToString()}");

            return "X";
        }


        public string SubcmdAllow(ParseState state)
            { 
            // BUG: No parameter checking
            string key = state.parameter.ToLower();
            state.botcmd.permittedusers = key;
            Instance?.QueueChatMessage($"Permit custom userlist set to  {key}.");
            return "X";
            }

        public string SubcmdSethelp(ParseState state)
            {
            state.botcmd.ShortHelp = state.parameter;
            Instance?.QueueChatMessage($"{state.command} help: {state.parameter}");
            return "X";
            }


        public string SubcmdSilent(ParseState state)
            {
            state.flags |= CmdFlags.Silent;
            return "";
            }



        #endregion


        #region COMMAND Class
        public partial class COMMAND
        {
            //public static List<COMMAND> cmdlist = new List<COMMAND>(); // Collection of our command objects
            public static Dictionary<string, COMMAND> aliaslist = new Dictionary<string, COMMAND>();
            //public static Dictionary<string, COMMAND> subcommands = new Dictionary<string, COMMAND>();

            private Action<TwitchUser, string> Method = null;  // Method to call
            private Action<TwitchUser, string, CmdFlags, string> Method2 = null; // Alternate method
            private Func<COMMAND, TwitchUser, string, CmdFlags, string, string> Method3 = null; // Prefered method, returns the error msg as a string.
            private Func<TwitchUser, string, IEnumerator> func1 = null;

            public Func<ParseState,string> subcommand = null;

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
                    var cmdname = entry;
                    if (entry.Length == 0) continue; // Make sure we don't get a blank command
                    cmdname = (entry[0] == '.') ? entry.Substring(1) : '!' + entry;
                    if (entry[0] == '/') cmdname = entry;
                    if (!aliaslist.ContainsKey(cmdname)) aliaslist.Add(cmdname, this);
                }
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

            public COMMAND Action(Func<ParseState,string> action)
            {
                subcommand = action;
                return this;
            }


            public COMMAND Help(CmdFlags flags=Broadcasteronly, string ShortHelp="", Regex regexfilter=null)
            {
                this.Flags = flags;
                this.ShortHelp = ShortHelp;
                this.regexfilter = regexfilter!=null ? regexfilter : _anything ;
                
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



            public static void Parse(TwitchUser user, string request, CmdFlags flags = 0, string info = "")
            {
                if (!Instance || request.Length==0) return;

                // This will be used for all parsing type operations, allowing subcommands efficient access to parse state logic
                ParseState parse = new ParseState(ref user, ref request, flags, ref info).ParseCommand();            

            }

        }
        #endregion


        public class ParseState
            {
            public TwitchUser user;
            public String request;
            public CmdFlags flags;
            public string info;

            public string command =null;
            public string parameter = "";

            public COMMAND botcmd =null;

            public ParseState(ref TwitchUser user, ref string request, CmdFlags flags, ref string info)
                {
                this.user = user ;
                this.request=request;
                this.flags=flags;
                this.info=info;
                }

            public string ExecuteSubcommand() // BUG: Only one supported for now (till I finalize the parse logic) ,we'll make it all work eventually
                {
                int commandstart = 0;
                int parameterstart = 0;


                while (parameterstart < parameter.Length && ((parameter[parameterstart] < '0' || parameter[parameterstart] > '9')  && parameter[parameterstart] != ' ')) parameterstart++;  // Command name ends with #... for now, I'll clean up some more later           
                int commandlength = parameterstart - commandstart;
                while (parameterstart < parameter.Length && parameter[parameterstart] == ' ') parameterstart++; // Eat the space(s) if that's the separator after the command
                if (commandlength == 0) return "";

                string subcommand = parameter.Substring(commandstart, commandlength).ToLower();


                COMMAND subcmd;
                if (!COMMAND.aliaslist.TryGetValue(subcommand, out subcmd)) return "";

                // BUG: Need to check subcmd permissions here.     

                if (!HasRights(ref subcmd, ref user)) return error($"No permission to use {subcommand}");

                parameter = parameter.Substring(parameterstart);

                try
                {
                    return subcmd.subcommand(this);                        
                }
                catch (Exception ex)
                {
                    // Display failure message, and lock out command for a time period. Not yet.

                    Plugin.Log(ex.ToString());

                }


                return "";
                }

            
           public string msg(string Message)
                {
                Instance.QueueChatMessage(Message);
                return "";
                }

            public string error(string Error)
                {
                return Error;
                }


            //static string done = "X";
            public void ExecuteCommand()
            {
                if (!COMMAND.aliaslist.TryGetValue(command, out botcmd)) return; // Unknown command

                // Permissions for these sub commands will always be by Broadcaster,or the (BUG: Future feature) user list of the EnhancedTwitchBot command. Note command behaviour that alters with permission should treat userlist as an escalation to Broadcaster.
                // Since these are never meant for an end user, they are not going to be configurable.

                // Example: !challenge/allow myfriends
                //          !decklist/setflags SUB
                //          !lookup/sethelp usage: %alias%<song name or id>
                //

                string errormsg=ExecuteSubcommand();
                if (errormsg != empty)
                    {
                    //if (errormsg==done) return;

                    ShowHelpMessage(ref botcmd, ref user, parameter, false);
                    return;
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

                if (parameter == "?") // Handle per command help requests - If permitted.
                {
                    ShowHelpMessage(ref botcmd, ref user, parameter, true);
                    return;
                }

                // Check regex

                if (!botcmd.regexfilter.IsMatch(parameter))
                {
                    ShowHelpMessage(ref botcmd, ref user, parameter, false);
                    return;
                }

                try
                {
                    botcmd.Execute(ref user, ref parameter, flags, ref info); // Call the command
                }
                catch (Exception ex)
                {
                    // Display failure message, and lock out command for a time period. Not yet.
                    Plugin.Log(ex.ToString());
                }
            }



            public ParseState ParseCommand()
                {

                // Notes for later.
                //var match = Regex.Match(request, "^!(?<command>[^ ^/]*?<parameter>.*)");
                //string username = match.Success ? match.Groups["command"].Value : null;

                int commandstart = 0;
                int parameterstart = 0;

                // This is a replacement for the much simpler Split code. It was changed to support /fakerest parameters, and sloppy users ... ie: !add4334-333 should now work, so should !command/flags
                while (parameterstart < request.Length && ((request[parameterstart] < '0' || request[parameterstart] > '9') && request[parameterstart] != '/' && request[parameterstart] != ' ')) parameterstart++;  // Command name ends with #... for now, I'll clean up some more later           
                int commandlength = parameterstart - commandstart;
                while (parameterstart < request.Length && request[parameterstart] == ' ') parameterstart++; // Eat the space(s) if that's the separator after the command
                if (commandlength == 0) return this;

                command = request.Substring(commandstart, commandlength).ToLower();
                if (COMMAND.aliaslist.ContainsKey(command))
                {
                    parameter = request.Substring(parameterstart);

                    try
                    {
                        ExecuteCommand();
                    }
                    catch (Exception ex)
                    {
                        // Display failure message, and lock out command for a time period. Not yet.

                        Plugin.Log(ex.ToString());

                    }

                }

            return this;

            }

        }


        // BUG: These are all the same. This interface needs more cleanup.

        public void AddCommand(string[] alias, Action<TwitchUser, string> method, CmdFlags flags = Broadcasteronly, string shorthelptext = "usage: [%alias%] ... Rights: %rights%", Regex regex = null)
        {
            new COMMAND(alias).Action(method).Help(flags, shorthelptext, regex);

        }

        public void AddCommand(string alias, Action<TwitchUser, string> method, CmdFlags flags = Broadcasteronly, string shorthelptext = "usage: [%alias%] ... Rights: %rights%", Regex regex = null)
        {
            new COMMAND(alias).Action(method).Help(flags, shorthelptext, regex);
        }


        // A much more general solution for extracting dymatic values into a text string. If we need to convert a text message to one containing local values, but the availability of those values varies by calling location
        // We thus build a table with only those values we have. 

        // BUG: This is actually part of botcmd, please move
        public static void ShowHelpMessage(ref COMMAND botcmd, ref TwitchUser user, string param, bool showlong)
        {
            if (botcmd.Flags.HasFlag(CmdFlags.SilentCheck) || botcmd.Flags.HasFlag(CmdFlags.Disabled)) return; // Make sure we're allowed to show help

            new DynamicText().AddUser(ref user).AddBotCmd(ref botcmd).QueueMessage(ref botcmd.ShortHelp, showlong);
            return;
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
                    var botcmd = entry.Value;
                    if (HasRights(ref botcmd, ref requestor) && !botcmd.Flags.HasFlag(Subcmd))
                        msg.Add($"{entry.Key.Substring(1)}", " "); // BUG: Removes the built in ! in the commands. 
                }
                msg.Add(">");
                msg.end("...", $"No commands available >");
                return;
            }
            if (COMMAND.aliaslist.ContainsKey(request.ToLower()))
            {
                var BotCmd = COMMAND.aliaslist[request.ToLower()];
                ShowHelpMessage(ref BotCmd, ref requestor, request, true);
            }
            else if (COMMAND.aliaslist.ContainsKey("!"+request.ToLower())) // BUG: Ugly code, gets help on ! version of command
            {
                var BotCmd = COMMAND.aliaslist["!"+request.ToLower()];
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