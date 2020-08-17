using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ChatCore.Interfaces;
using ChatCore.Models.Twitch;

// Feature requests: Add Reason for being banned to banlist

namespace SongRequestManager
{
    public partial class RequestBot : MonoBehaviour
    {
        static bool _WobbleInstalled = false;

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
            NoFilter = 256, // Return no results on failed preflight checks.

            HelpLink = 512, // Enable link to web documentation

            WhisperReply = 1024, // Reply in a whisper to the user (future feature?). Allow commands to send the results to the user, avoiding channel spam

            Timeout = 2048, // Applies a timeout to regular users after a command is succesfully invoked this is just a concept atm
            TimeoutSub = 4096, // Applies a timeout to Subs
            Autopick = 8192, // Auto pick first song when adding
            Local = 16384, // The command is being executed from console and therefore always full priveledge

            NoLinks = 32768, // Turn off any links that the command may normally generate

            //Silent = 65536, // Command produces no output at all - but still executes
            Verbose = 131072, // Turn off command output limits, This can result in excessive channel spam
            Log = 262144, // Log every use of the command to a file
            RegEx = 524288, // Enable regex check
            UserFlag1 = 1048576, // Use it for whatever bit makes you happy 
            NoParameter = 2097152, // The (subcommand) takes no parameter

            Variable = 4194304, // This is a variable 
            Dynamic = 8388608, // This command is generated dynamically, and cannot be saved/loaded 

            ToQueue = 16277216, //  Request is moved directly to queue, bypassing song check

            MoveToTop = 1 << 25, // Private, used by ATT command. Its possible to have multiple aliases for the same flag

            SilentCheck = 1 << 26, // Initial command check failure returns no message
            SilentError = 1 << 27, // Command failure returns no message
            SilentResult = 1 << 28, // Command returns no visible results

            Silent = SilentCheck | SilentError | SilentResult,

            Subcommand = 1 << 29, // This is a subcommand, it may only be invoked within a command

            Disabled = 1 << 30, // If ON, the command will not be added to the alias list at all.

        }




        const CmdFlags Default = 0;
        const CmdFlags Everyone = Default | CmdFlags.Everyone;
        const CmdFlags Broadcaster = Default | CmdFlags.Broadcaster;
        const CmdFlags Mod = Default | CmdFlags.Broadcaster | CmdFlags.Mod;
        const CmdFlags Sub = Default | CmdFlags.Sub;
        const CmdFlags VIP = Default | CmdFlags.VIP;
        const CmdFlags Help = CmdFlags.BypassRights;
        const CmdFlags Silent = CmdFlags.Silent;
        const CmdFlags Subcmd = CmdFlags.Subcommand | Broadcaster;
        const CmdFlags Var= CmdFlags.Variable | Broadcaster;

        #endregion

        #region common Regex expressions

        private static readonly Regex _digitRegex = new Regex("^[0-9a-fA-F]+$", RegexOptions.Compiled);
        private static readonly Regex _beatSaverRegex = new Regex("^[0-9]+-[0-9]+$", RegexOptions.Compiled);
        private static readonly Regex _alphaNumericRegex = new Regex("^[0-9A-Za-z]+$", RegexOptions.Compiled);
        private static readonly Regex _RemapRegex = new Regex("^[0-9a-fA-F]+,[0-9a-fA-F]+$", RegexOptions.Compiled);
        private static readonly Regex _beatsaversongversion = new Regex("^[0-9a-zA-Z]+$", RegexOptions.Compiled);
        private static readonly Regex _nothing = new Regex("$^", RegexOptions.Compiled);
        private static readonly Regex _anything = new Regex(".*", RegexOptions.Compiled); // Is this the most efficient way?
        private static readonly Regex _atleast1 = new Regex("..*", RegexOptions.Compiled); // Allow usage message to kick in for blank 
        private static readonly Regex _fail = new Regex("(?!x)x", RegexOptions.Compiled); // Not sure what the official fastest way to auto-fail a match is, so this will do
        private static readonly Regex _deck = new Regex("^(current|draw|first|last|random|unload)$|$^", RegexOptions.Compiled); // Checks deck command parameters

        private static readonly Regex _drawcard = new Regex("($^)|(^[0-9a-zA-Z]+$)", RegexOptions.Compiled);

        #endregion

        #region Command Registration 

        private void InitializeCommands()
        {
            #region Command declarations
            /*
                *VERY IMPORTANT*
 
                The Command name or FIRST alias in the alias list is considered the Base name of the command, and absolutely should not be changed through code. Choose this first name wisely.
                We use the Base name to allow user command Customization, it is how the command is identified to the user. You can alter the alias list of the commands in 
                the command configuration file (srmcommands.ini).
 
            */


            new COMMAND(new string[] { "!request", "!bsr", "!add", "!sr","!srm" }).Action(ProcessSongRequest).Help(Everyone, "usage: %alias%<songname> or <song id>, omit <,>'s. %|%This adds a song to the request queue. Try and be a little specific. You can look up songs on %beatsaver%", _atleast1);
            new COMMAND(new string[] { "!lookup", "!find" }).AsyncAction(LookupSongs).Help(Mod | Sub | VIP, "usage: %alias%<song name> or <song id>, omit <>'s.%|%Get a list of songs from %beatsaver% matching your search criteria.", _atleast1);

            new COMMAND("!link").Action(ShowSongLink).Help(Everyone, "usage: %alias% %|%... Shows song details, and an %beatsaver% link to the current song", _nothing);

            new COMMAND("!open").Action(OpenQueue).Help(Mod, "usage: %alias%%|%... Opens the queue allowing song requests.", _nothing);
            new COMMAND("!close").Action(CloseQueue).Help(Mod, "usage: %alias%%|%... Closes the request queue.", _nothing);

            new COMMAND("!queue").Action(ListQueue).Help(Everyone, "usage: %alias%%|% ... Displays a list of the currently requested songs.", _nothing);
            new COMMAND("!played").Action(ShowSongsplayed).Help(Mod, "usage: %alias%%|%... Displays all the songs already played this session.", _nothing);
            new COMMAND("!history").Action(ShowHistory).Help(Mod, "usage: %alias% %|% Shows a list of the recently played songs, starting from the most recent.", _nothing);
            new COMMAND("!who").Action(Who).Help(Sub | VIP | Mod, "usage: %alias% <songid or name>%|%Find out who requested the song in the currently queue or recent history.", _atleast1);

            new COMMAND ("!modadd").Action(ModAdd).Help(Mod, "usage: %alias%<songname> or <song id>, omit <,>'s. %|%This adds a song to the request queue. This ignores ALL filters including bans.", _atleast1);
            new COMMAND("!mtt").Action(MoveRequestToTop).Help(Mod, "usage: %alias%<songname>,<username>,<song id> %|%... Moves a song to the top of the request queue.", _atleast1);
            new COMMAND("!att").Action(AddToTop).Help(Mod, "usage: %alias%<songname> or <song id>, omit <,>'s. %|%This adds a song to the top of the request queue. Try and be a little specific. You can look up songs on %beatsaver%", _atleast1);
            new COMMAND(new string[] { "!last", "!demote", "!later" }).Action(MoveRequestToBottom).Help(Mod, "usage: %alias%<songname>,<username>,<song id> %|%... Moves a song to the bottom of the request queue.", _atleast1);
            new COMMAND("!remove").Action(DequeueSong).Help(Mod, "usage: %alias%<songname>,<username>,<song id> %|%... Removes a song from the queue.", _atleast1);
            new COMMAND(new string[] { "!wrongsong", "!wrong", "!oops" }).Action(WrongSong).Help(Everyone, "usage: %alias%%|%... Removes your last requested song form the queue. It can be requested again later.", _nothing);

            new COMMAND("!unblock").Action(Unban).Help(Mod, "usage: %alias%<song id>, do not include <,>'s.", _beatsaversongversion);
            new COMMAND("!block").AsyncAction(Ban).Help(Mod, "usage: %alias%<song id>, do not include <,>'s.", _beatsaversongversion);
            new COMMAND("!blist").Action(ShowBanList).Help(Broadcaster, "usage: Don't use, it will spam chat!", _atleast1); // Purposely annoying to use, add a character after the command to make it happen 

            new COMMAND("!remap").Action(Remap).Help(Mod, "usage: %alias%<songid1> , <songid2>%|%... Remaps future song requests of <songid1> to <songid2> , hopefully a newer/better version of the map.", _RemapRegex);
            new COMMAND("!unmap").Action(Unmap).Help(Mod, "usage: %alias%<songid> %|%... Remove future remaps for songid.", _beatsaversongversion);

            new COMMAND("!clearqueue").Action(Clearqueue).Help(Mod, "usage: %alias%%|%... Clears the song request queue. You can still get it back from the JustCleared deck, or the history window", _nothing);
            new COMMAND("!clearalreadyplayed").Action(ClearDuplicateList).Help(Mod, "usage: %alias%%|%... clears the list of already requested songs, allowing them to be requested again.", _nothing); // Needs a better name
            new COMMAND("!restore").Action(restoredeck).Help(Mod, "usage: %alias%%|%... Restores the request queue from the previous session. Only useful if you have persistent Queue turned off.", _nothing);

            new COMMAND("!about").Help(CmdFlags.Broadcaster | CmdFlags.SilentCheck , $"Song Request Manager version {Plugin.Version.ToString()}. Github angturil/SongRequestManager", _fail); // Help commands have no code
            new COMMAND(new string[] { "!help" }).Action(help).Help(Everyone, "usage: %alias%<command name>, or just %alias%to show a list of all commands available to you.", _anything);
            new COMMAND("!commandlist").Action(showCommandlist).Help(Everyone, "usage: %alias%%|%... Displays all the bot commands available to you.", _nothing);

            new COMMAND("!readdeck").Action(Readdeck).Help(Mod, "usage: %alias", _alphaNumericRegex);
            new COMMAND("!writedeck").Action(Writedeck).Help(Broadcaster, "usage: %alias", _alphaNumericRegex);

            new COMMAND("!chatmessage").Action(ChatMessage).Help(Broadcaster, "usage: %alias%<what you want to say in chat, supports % variables>", _atleast1); // BUG: Song support requires more intelligent %CurrentSong that correctly handles missing current song. Also, need a function to get the currenly playing song.
            new COMMAND("!runscript").Action(RunScript).Help(Mod, "usage: %alias%<name>%|%Runs a script with a .script extension, no conditionals are allowed. startup.script will run when the bot is first started. Its probably best that you use an external editor to edit the scripts which are located in UserData/StreamCore", _atleast1);

            new COMMAND("!formatlist").Action(showFormatList).Help(Broadcaster, "Show a list of all the available customizable text format strings. Use caution, as this can make the output of some commands unusable. You can use /default to return a variable to its default setting.");


            new COMMAND("!songmsg").Action(SongMsg).Help(Mod, "usage: %alias% <songid> Message%|% Assign a message to a songid, which will be visible to the player during song selection.", _atleast1);

            new COMMAND("!addsongs").AsyncAction(addsongs).Help(Broadcaster, "usage: %alias%%|% Add all songs matching a criteria (up to 40) to the queue", _atleast1);

            new COMMAND("!every").Action(Every).Help(Broadcaster, "usage: every <minutes> %|% Run a command every <minutes>.", _atleast1);
            new COMMAND("!in").Action(EventIn).Help(Broadcaster, "usage: in <minutes> <bot command>.", _atleast1);
            new COMMAND("!clearevents").Action(ClearEvents).Help(Broadcaster, "usage: %alias% %|% Clear all timer events.");
            new COMMAND(new string[] { "!addnew", "!addlatest" }).AsyncAction(addsongsFromnewest).Help(Mod, "usage: %alias% <listname>%|%... Adds the latest maps from %beatsaver%, filtered by the previous selected allowmappers command", _nothing);
            new COMMAND("!backup").Action(BackupStreamcore).Help(CmdFlags.Broadcaster, "Backup %SRM% directory.", _anything);

            //new COMMAND("!refreshsongs").Coroutine(RefreshSongs).Help(Broadcaster, "Adds custom songs to bot list. This is a pre-release feature."); // BUG: Broken in 1.10
            new COMMAND("!savesongdatabase").Coroutine(SaveSongDatabase).Help(Broadcaster);

            new COMMAND("!queuestatus").Action(QueueStatus).Help(Mod, "usage: %alias% %|% Show current queue status", _nothing);

            new COMMAND("!QueueLottery").Action(QueueLottery).Help(Broadcaster, "usage: %alias% <entry count> %|% Shuffle the queue and reduce to <entry count> entries. Close the queue.", _anything);

            new COMMAND("!addtoqueue").Action(queuelist).Help(Broadcaster, "usage: %alias% <list>", _atleast1);

            #region Gamechanger Specific           
            bool _GameChangerInstalled = IPA.Loader.PluginManager.GetPlugin("Beat Bits") != null;
            _WobbleInstalled= IPA.Loader.PluginManager.GetPlugin("WobbleSaber") != null;

            if (_GameChangerInstalled)
                {
                new COMMAND("!sabotage").Coroutine(SetBombState).Help(Mod, "Usage: %alias% on/off (LIV Gamechanger only). %|% Turns bombs on and off in Gamechanger.");
                }
            #endregion

#if UNRELEASED
            new COMMAND("!makesearchdeck").AsyncAction(makelistfromsearch).Help(Broadcaster, "usage: %alias%%|% Add all songs matching a criteria to search.deck", _atleast1);

            //new COMMAND("!sdk2test").Action(livsdktest).Help(Broadcaster,"usage: don't",_anything);

            //new COMMAND("!getpp").Coroutine(GetPPData).Help(Broadcaster, "Get PP Data");

            new COMMAND("!downloadsongs").AsyncAction(DownloadEverything).Help(Broadcaster, "Adds custom songs to bot list. This is a pre-release feature.");

            // These comments contain forward looking statement that are absolutely subject to change. I make no commitment to following through
            // on any specific feature,interface or implementation. I do not promise to make them generally available. Its probably best to avoid using or making assumptions based on these.

            new COMMAND("!readarchive").Coroutine(ReadArchive).Help(Broadcaster, "Adds archived sngs to bot");

            new COMMAND("!at").Help(Broadcaster, "Run a command at a certain time.", _atleast1); // BUG: No code
            new COMMAND("!alias").Help(Broadcaster, "usage: %alias %|% Create a command alias, short cuts version a commands. Single line only. Supports %variables% (processed at execution time), parameters are appended.", _atleast1); // BUG: No action

            new COMMAND("!detail"); // Get song details BUG: NO code

            new COMMAND("!allowmappers").Action(MapperAllowList).Help(Broadcaster, "usage: %alias%<mapper list> %|%... Selects the mapper list used by the AddNew command for adding the latest songs from %beatsaver%, filtered by the mapper list.", _alphaNumericRegex);  // The message needs better wording, but I don't feel like it right now
            new COMMAND("!blockmappers").Action(MapperBanList).Help(Broadcaster, "usage: %alias%<mapper list> %|%... Selects a mapper list that will not be allowed in any song requests.", _alphaNumericRegex); // BUG: This code is behind a switch that can't be enabled yet.

            new COMMAND("!mapperdeck").AsyncAction(AddmapperToDeck).Help(Broadcaster, "usage: %alias%<mapperlist>");

            //new COMMAND("!glitter").Action(AddLIVGlitter).Help(Broadcaster, "usage: %alias% <number> Message");

            // These commands will use a completely new format in future builds and rely on a slightly more flexible parser. Commands like userlist.add george, userlist1=userlist2 will be allowed. 

            new COMMAND("!unqueuelist").Action(unqueuelist).Help(Mod, "usage %alias% <list name> %|% Remove any songs on a list from the queue",_atleast1);
            new COMMAND("!openlist").Action(OpenList); // BUG: this command makes  list available.
            new COMMAND("!unload").Action(UnloadList);
            new COMMAND("!clearlist").Action(ClearList);
            new COMMAND("!write").Action(writelist);
            new COMMAND("!list").Action(ListList);
            new COMMAND("!lists").Action(showlists);
            new COMMAND("!addtolist").Action(Addtolist).Help(Broadcaster, "usage: %alias%<list> <value to add>", _atleast1);
            new COMMAND("!removefromlist").Action(RemoveFromlist).Help(Broadcaster, "usage: %alias%<list> <value to add>", _atleast1);
            new COMMAND("!listundo").Action(Addtolist).Help(Broadcaster, "usage: %alias%<list>", _atleast1); // BUG: No function defined yet, undo the last operation

            // Deck related commandws are currently not released

            new COMMAND("!deck").Action(createdeck);
            new COMMAND("!unloaddeck").Action(unloaddeck);
            new COMMAND("!loaddecks").Action(loaddecks);
            new COMMAND("!whatdeck").Action(whatdeck).Help(Mod, "usage: %alias%<songid> or 'current'", _beatsaversongversion);
            new COMMAND("!decklist").Action(decklist).Help(Mod, "usage: %alias", _deck);

            new COMMAND("!unqueuemsg").Help(Broadcaster, "usage: %alias% msg text to match", _atleast1); // BUG: No code

            new COMMAND(new string[] { "/toggle", "subcomdtoggle" }).Action(SubcmdToggle).Help(Subcmd | Mod | CmdFlags.NoParameter, "usage: <!deck> /toggle <songid> %|% Adds a song to a deck if not present, otherwise removes it. Used primarily for button actions");

            new COMMAND("!updatemappers").AsyncAction(UpdateMappers).Help(Broadcaster, "usage: %alias% %|% Update mapper lists/decks. This may take a while, don't do live.");
            new COMMAND("!joinrooms").Coroutine(GetRooms).Help(Broadcaster, "usage: %alias% %|% This is not fully functional, allows the bot to accept commands from your other rooms.") ;
            new COMMAND("!savecommands").Action(SaveCommands);

            new COMMAND("gccount").Action(GetGCCount).Help(Broadcaster);
#endif
            #endregion

            #region Text Format fields

            //would be good to use reflections for these
            new COMMAND("AddSongToQueueText", AddSongToQueueText); // These variables are bound due to class reference assignment
            new COMMAND("LookupSongDetail", LookupSongDetail);
            new COMMAND("BsrSongDetail", BsrSongDetail);
            new COMMAND("LinkSonglink", LinkSonglink);
            new COMMAND("NextSonglink", NextSonglink);
            new COMMAND("SongHintText", SongHintText);
            new COMMAND("QueueTextFileFormat", QueueTextFileFormat);
            new COMMAND("QueueListRow2", QueueListRow2);
            new COMMAND("QueueListFormat", QueueListFormat);
            new COMMAND("HistoryListFormat", HistoryListFormat);
            new COMMAND("AddSortOrder", AddSortOrder);
            new COMMAND("LookupSortOrder", LookupSortOrder); // -ranking +id , note that +/- are mandatory
            new COMMAND("AddSongsSortOrder", AddSongsSortOrder);

            #endregion

            #region SUBCOMMAND Declarations
            // BEGIN SUBCOMMANDS - these modify the Properties of a command, or the current parse state. 
            // sub commands need to have at least one alias that does not begin with an illegal character, or you will not be able to alter them in twitch chat

            new COMMAND(new string[] { "/enable", "subcmdenable" }).Action(SubcmdEnable).Help(Subcmd, "usage: <command>/enable");
            new COMMAND(new string[] { "/disable", "subcmddisable" }).Action(SubcmdDisable).Help(Subcmd, "usage: <command>/disable");
            new COMMAND(new string[] { "/current", "subcmdcurrent" }).Action(SubcmdCurrentSong).Help(Subcmd | Everyone, "usage: <command>/current");
            new COMMAND(new string[] { "/selected", "subcmdselected" }).Action(SubcmdSelected).Help(Subcmd | Mod, "usage: <command>/selected");
            new COMMAND(new string[] { "/last", "/previous", "subcmdlast" }).Action(SubcmdPreviousSong).Help(Subcmd | Everyone, "usage: <command>/last");
            new COMMAND(new string[] { "/next", "subcmdnext" }).Action(SubcmdNextSong).Help(Subcmd | Everyone, "usage: <command>/next");

            new COMMAND(new string[] { "/requestor", "subcmduser" }).Action(SubcmdCurrentUser).Help(Subcmd | Everyone, "usage: <command>/requestor");
            new COMMAND(new string[] { "/list", "subcmdlist" }).Action(SubcmdList).Help(Subcmd | Mod, "usage: <command>/list");
            new COMMAND(new string[] { "/add", "subcmdadd" }).Action(SubcmdAdd).Help(Subcmd | Mod, "usage: <command>/add");
            new COMMAND(new string[] { "/remove", "subcmdremove" }).Action(SubcmdRemove).Help(Subcmd | Mod, "usage: <command>/remove");


            new COMMAND(new string[] { "/flags", "subcmdflags" }).Action(SubcmdShowflags).Help(Subcmd, "usage: <command>/next");
            new COMMAND(new string[] { "/set", "subcmdset" }).Action(SubcmdSetflags).Help(Subcmd, "usage: <command>/set <flags>");
            new COMMAND(new string[] { "/clear", "subcmdclear" }).Action(SubcmdClearflags).Help(Subcmd, "usage: <command>/clear <flags>");

            new COMMAND(new string[] { "/allow", "subcmdallow" }).Action(SubcmdAllow).Help(Subcmd, "usage: <command>/allow");
            new COMMAND(new string[] { "/sethelp", "/helpmsg", "subcmdsethelp" }).Action(SubcmdSethelp).Help(Subcmd, "usage: <command>/sethelp");
            new COMMAND(new string[] { "/silent", "subcmdsilent" }).Action(SubcmdSilent).Help(Subcmd | CmdFlags.NoParameter | Everyone, "usage: <command>/silent");

            new COMMAND(new string[] { "=", "subcmdequal" }).Action(SubcmdEqual).Help(Subcmd | Broadcaster, "usage: =");

            new COMMAND(new string[] { "/alias", "subcmdalias" }).Action(SubcmdAlias).Help(Subcmd | Broadcaster, "usage: %alias% %|% Defines all the aliases a command can use");
            new COMMAND(new string[] { "/default", "subcmddefault" }).Action(SubcmdDefault).Help(Subcmd | Broadcaster, "usage: <formattext> %alias%");

            new COMMAND(new string[] { "/newest", "subcmdnewest" }).Action(SubcmdNewest).Help(Subcmd |CmdFlags.NoParameter| Everyone); 
            new COMMAND(new string[] { "/best", "subcmdbest" }).Action(SubcmdBest).Help(Subcmd | CmdFlags.NoParameter | Everyone); 
            new COMMAND(new string[] { "/oldest", "subcmdoldest" }).Action(SubcmdOldest).Help(Subcmd | CmdFlags.NoParameter | Everyone); 
            new COMMAND(new string[] { "/pp", "subcmdpp" }).Action(SubcmdPP).Help(Subcmd | CmdFlags.NoParameter | Everyone); 


            new COMMAND(new string[] { "/top", "subcmdtop" }).Action(SubcmdTop).Help(Subcmd|CmdFlags.NoParameter | Mod | Broadcaster, "%alias% sets a flag to move the request(s) to the top of the queue.");
            new COMMAND(new string[] { "/mod","subcmdmod"}).Action(SubcmdMod).Help(Subcmd|CmdFlags.NoParameter | Mod | Broadcaster,"%alias% sets a flag to ignore all filtering");
            #endregion

            COMMAND.InitializeCommands(); // BUG: Currently empty
            COMMAND.SummarizeCommands(); // Save original command states string.
        }

        public string SaveCommands(ParseState state)
        {
            COMMAND.WriteCommandConfiguration();
            return success;
        }

        const string success = "";
        const string endcommand = "X";
        const string notsubcommand="NotSubcmd";

        #region Subcommands
        public string SubcmdEnable(ParseState state)
        {
            state.botcmd.Flags &= ~CmdFlags.Disabled;
            state.botcmd.UpdateCommand(COMMAND.ChangedFlags.Flags);
            Instance?.QueueChatMessage($"{state.command} Enabled.");
            return endcommand;
        }

        public string SubcmdNewest(ParseState state)
        {
            state.flags |= CmdFlags.Autopick;
            state.sort = "-id -rating";
            return success;
        }

        public string SubcmdPP(ParseState state)
        {
            state.flags |= CmdFlags.Autopick;
            state.sort = "-pp -rating -id";
            return success;
        }


        public string SubcmdBest(ParseState state)
        {
            state.flags |= CmdFlags.Autopick;
            state.sort = "-rating -id";
            return success;
        }

        public string SubcmdOldest(ParseState state)
        {
            state.flags |= CmdFlags.Autopick;
            state.sort = "+id -rating";
            return success;
        }



        public string SubcmdDisable(ParseState state)
        {
            state.botcmd.Flags |= CmdFlags.Disabled;
            state.botcmd.UpdateCommand(COMMAND.ChangedFlags.Flags);
            Instance?.QueueChatMessage($"{state.command} Disabled.");
            return endcommand;
        }

        public string SubcmdList(ParseState state)
        {
            ListList(state.user, state.botcmd.userParameter.ToString());    
            return endcommand;
        }

        public string SubcmdAdd(ParseState state)
        {
            Addtolist(state.user, state.botcmd.userParameter.ToString()+" "+state.subparameter);
            return endcommand;
        }

        public string SubcmdRemove(ParseState state)
        {
            RemoveFromlist(state.user, state.botcmd.userParameter.ToString()+" "+state.subparameter);
            return endcommand;
        }


        public string SubcmdCurrentSong(ParseState state)
        {
            try
            {
                if (state.parameter != "") state.parameter += " ";
                state.parameter +=RequestHistory.Songs[0].song["version"];
                return "";
            }
            catch
            {
                // Being lazy, incase RequestHistory access failure.
            }

            return state.error($"Theree is no current song available");
        }

        public string SubcmdSelected(ParseState state)
        {
            try
            {
                if (state.parameter != "") state.parameter += " ";
                state.parameter  +=  RequestBotListViewController.Instance.CurrentlySelectedSong().song["version"];
                return "";
            }
            catch
            {
                // Being lazy, incase RequestHistory access failure.
            }

            return state.error($"Theree is no current song available");
        }



        public string SubcmdCurrentUser(ParseState state)
        {
            try
            {
                if (state.parameter != "") state.parameter += " ";
                //state.parameter += RequestHistory.Songs[0][""];
                return "";
            }
            catch
            {
                // Being lazy, incase RequestHistory access failure.
            }

            return state.error($"Theree is no current user available");
        }


        public string SubcmdPreviousSong(ParseState state)
        {
            try
            {
                if (state.parameter != "") state.parameter += " ";
                state.parameter += RequestHistory.Songs[1].song["version"];
                return "";
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
                if (state.parameter != "") state.parameter += " ";
                state.parameter += RequestQueue.Songs[0].song["version"];
                return "";
            }
            catch
            {
                // Being lazy, incase RequestHistory access failure.
            }

            return state.error($"There are no songs in the queue.");
        }


        public string SubcmdShowflags(ParseState state)
        {
            if (state.subparameter == "")
            {
                Instance?.QueueChatMessage($"{state.command} flags: {state.botcmd.Flags.ToString()}");
            }
            else
            {
                
                return SubcmdSetflags(state);
            }
            return endcommand;
        }


        public string SubcmdSetflags(ParseState state)
        {
            try
            {

                string[] flags = state.subparameter.Split(new char[] { ' ', ','}, StringSplitOptions.RemoveEmptyEntries);

                CmdFlags flag = (CmdFlags)Enum.Parse(typeof(CmdFlags), state.subparameter);
                state.botcmd.Flags |= flag;
                state.botcmd.UpdateCommand(COMMAND.ChangedFlags.Flags);

                if (!state.flags.HasFlag(CmdFlags.SilentResult)) Instance?.QueueChatMessage($"{state.command} flags: {state.botcmd.Flags.ToString()}");

            }
            catch   
            {
                return $"Unable to set  {state.command} flags to {state.subparameter}";
            }

            return endcommand;
        }

        public string SubcmdClearflags(ParseState state)
        {
            string[] flags = state.subparameter.Split(new char[] { ' ', ',' });

            CmdFlags flag = (CmdFlags)Enum.Parse(typeof(CmdFlags), state.subparameter);

            state.botcmd.Flags &= ~flag;

            state.botcmd.UpdateCommand(COMMAND.ChangedFlags.Flags);
            if (!state.flags.HasFlag(CmdFlags.SilentResult)) Instance?.QueueChatMessage($"{state.command} flags: {state.botcmd.Flags.ToString()}");

            return endcommand;
        }


        public string SubcmdAllow(ParseState state)
        {
            // BUG: No parameter checking
            string key = state.subparameter.ToLower();
            state.botcmd.permittedusers = key;
            if (!state.flags.HasFlag(CmdFlags.SilentResult)) Instance?.QueueChatMessage($"Permit custom userlist set to  {key}.");
            return endcommand;
        }

        public string SubcmdAlias(ParseState state)
        {

            state.subparameter.ToLower();

            if (state.botcmd.aliases.Contains(state.botcmd.aliases[0]) || COMMAND.aliaslist.ContainsKey(state.botcmd.aliases[0]))
                {
                foreach (var alias in state.botcmd.aliases) COMMAND.aliaslist.Remove(alias);
                state.botcmd.aliases = state.subparameter.Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                state.botcmd.AddAliases();
                state.botcmd.UpdateCommand(COMMAND.ChangedFlags.Aliases);

            }
            else
                {
                return $"Unable to set {state.command} aliases to {state.subparameter}";    
                }

            return endcommand;
        }


        public string SubcmdSethelp(ParseState state)
        {
            state.botcmd.ShortHelp = state.subparameter + state.parameter; // This one's different
            state.botcmd.UpdateCommand(COMMAND.ChangedFlags.Help);

            if (!state.flags.HasFlag(CmdFlags.SilentResult)) Instance?.QueueChatMessage($"{state.command} help: {state.botcmd.ShortHelp}");
            return endcommand;
        }


        public string SubcmdSilent(ParseState state)
        {
            state.flags |= CmdFlags.Silent;
            return success;
        }

        public string SubcmdTop(ParseState state)
        {
            state.flags |= CmdFlags.MoveToTop;
            return success;
        }

        public string SubcmdMod(ParseState state)
        {
            state.flags |= CmdFlags.NoFilter;
            return success;
        }


        public string SubcmdEqual(ParseState state)
        {
            state.flags |= CmdFlags.SilentResult; // Turn off success messages, but still allow errors.

            if (state.botcmd.Flags.HasFlag(CmdFlags.Variable)) 
                {
                state.botcmd.userParameter.Clear().Append(state.subparameter+state.parameter);
                state.botcmd.UpdateCommand(COMMAND.ChangedFlags.Variable);

            }

            return endcommand; // This is an assignment, we're not executing the object.
        }


        public string SubcmdDefault(ParseState state)
        {
            if (state.botcmd.Flags.HasFlag(CmdFlags.Variable))
            {
                state.botcmd.userParameter.Clear().Append(state.botcmd.UserString);
                state.botcmd.UpdateCommand(COMMAND.ChangedFlags.Variable);
                return state.msg($"{state.command} has been reset to its original value.",endcommand);
            }

            return state.text("You cannot use /default on anything except a Format variable at this time.");
        }

        #endregion

            #region COMMAND Class
        public partial class COMMAND
        {
            public static StringBuilder commandsummary=new StringBuilder();
            public static bool Loading = false;

            public static Dictionary<string, COMMAND> aliaslist = new Dictionary<string, COMMAND>(); // There can be only one (static)!

            // BUG: Extra methods will be removed after the offending code is migrated, There will likely always be 2-3.
            private Action<TwitchUser, string> Method = null;  // Method to call
            private Action<TwitchUser, string, CmdFlags, string> Method2 = null; // Alternate method
            //private Func<COMMAND, TwitchUser, string, CmdFlags, string, string> Method3 = null; // Prefered method, returns the error msg as a string.
            private Func<ParseState, IEnumerator> func1 = null;

            public Func<ParseState, string> subcommand = null; // Prefered calling convention. It does expose calling command base properties, so be careful.
            public Func<ParseState> subcommand2 = null;
            public Func<ParseState, Task> AsyncSubCommand = null;

            public CmdFlags Flags = Broadcaster;          // flags
            public string ShortHelp = "";                   // short help text (on failing preliminary check
            public List<string> aliases = null;               // list of command aliases
            public Regex regexfilter = _anything;                 // reg ex filter to apply. For now, we're going to use a single string

            public string LongHelp = null; // Long help text
            public string HelpLink = null; // Help website link, Using a wikia might be the way to go
            public string permittedusers = ""; // Name of list of permitted users.
            public StringBuilder userParameter =new StringBuilder(); // This is here incase I need it for some specific purpose
            public string UserString = "";
            public int userNumber = 0;
            public int UseCount = 0;  // Number of times command has been used, sadly without references, updating this is costly.
            public ChangedFlags ChangedParameters=0; // Indicates if any prameters were changed by the user

            public void UpdateCommand(ChangedFlags changed)
                {
                ChangedParameters |= changed;
                ChangedParameters &= ~ChangedFlags.Saved;
                }   

                

            [Flags]
            public enum ChangedFlags
                {
                none=0,
                Aliases=1,
                Flags=2,
                Help=4,
                Allow=8,
                Any=15,
                Variable=16,

                Saved=1<<30, // These changes were saved
                All=-1
                }
            public void SetPermittedUsers(string listname)
            {
                // BUG: Needs additional checking

                string fixedname = listname.ToLower();
                if (!fixedname.EndsWith(".users")) fixedname += ".users";
                permittedusers = fixedname;
            }

            public async Task<string> Execute(ParseState state)
            {
                // BUG: Most of these will be replaced.  

                if (Method2 != null) Method2(state.user, state.parameter, state.flags, state.info);
                else if (Method != null) Method(state.user, state.parameter);
                //else if (Method3 != null) return Method3(this, state.user, state.parameter, state.flags, state.info);
                else if (func1 != null) Instance.StartCoroutine(func1(state));
                else if (subcommand != null) return subcommand(state); // Recommended.
                else if (subcommand2 != null) subcommand(state);
                else if (AsyncSubCommand != null) await AsyncSubCommand(state);
                return success;
            }

            public static void InitializeCommands()
            {
                // BUG: Currently unused due to common variable scope visibility, but could change later.
            }

            public COMMAND AddAliases()
            {
                foreach (var entry in aliases)
                {
                    var cmdname = entry;
                    entry.ToLower();
                    if (entry.Length == 0) continue; // Make sure we don't get a blank command
                    if (!aliaslist.ContainsKey(cmdname)) aliaslist.Add(entry, this);
                }
                return this;
            }
            
            public COMMAND(string variablename, StringBuilder reference)                
                {
                userParameter = reference;
                Flags = CmdFlags.Variable | CmdFlags.Broadcaster;
                aliases = new List<string>();
                aliases.Add(variablename.ToLower());
                subcommand = RequestBot.Instance.Variable ;
                regexfilter = _anything;
                ShortHelp = "the = operator currently requires a space after it";
                UserString = reference.ToString(); // Save a backup
                
                AddAliases();
                }

            public COMMAND(string alias)
            {
                aliases = new List<string>();
                aliases.Add(alias.ToLower());
                AddAliases();
            }
            public COMMAND(string[] alias)
            {
                aliases = new List<string>();
                foreach (var element in alias)
                {
                    aliases.Add(element.ToLower());
                }
                AddAliases();
            }
            public COMMAND Action(Func<ParseState, string> action)
            {
                subcommand = action;
                return this;
            }
            public COMMAND Action(Func<ParseState> action)
            {
                subcommand2 = action;
                return this;
            }
            public COMMAND AsyncAction(Func<ParseState, Task> action)
            {
                AsyncSubCommand = action;
                return this;
            }
            public COMMAND Help(CmdFlags flags = Broadcaster, string ShortHelp = "", Regex regexfilter = null)
            {
                this.Flags = flags;
                this.ShortHelp = ShortHelp;
                this.regexfilter = regexfilter != null ? regexfilter : _anything;

                return this;
            }

            public COMMAND User(string userstring)
            {
                userParameter.Clear().Append(userstring);
                return this;
            }

          //  public COMMAND Action(Func<COMMAND, TwitchUser, string, CmdFlags, string, string> action)
            //{
            //    Method3 = action;
              //  return this;
            //}

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

            public COMMAND Coroutine(Func<ParseState, IEnumerator> action)
            {
                func1 = action;
                return this;
            }

            public static void Parse(TwitchUser user, string request, CmdFlags flags = 0, string info = "")
            {
                if (!Instance || request.Length == 0) return;

                if (listcollection.contains(ref _blockeduser, user.UserName.ToLower())) return;

                // This will be used for all parsing type operations, allowing subcommands efficient access to parse state logic
                ParseState parse = new ParseState(ref user, ref request, flags, ref info).ParseCommand();
            }

            #region Command List Save / Load functionality
            private string GetHelpText()
                {
                return ShortHelp;
                }

            private string GetFlags()
                {
                return Flags.ToString();
                }

            private string GetAliases()
                {
                return String.Join(",", aliases.ToArray());
                }

            public static void SummarizeCommands(StringBuilder target=null,bool everything=true)
                {
                SortedDictionary<string, COMMAND> unique = new SortedDictionary<string, COMMAND>();

                if (target == null) target = commandsummary;

                foreach (var alias in aliaslist)
                {
                    var BaseKey = alias.Value.aliases[0];
                    if (!unique.ContainsKey(BaseKey)) unique.Add(BaseKey, alias.Value); // Create a sorted dictionary of each unique command object
                }

 
                foreach (var entry in unique)
                {
                    var command = entry.Value;

                    if (command.Flags.HasFlag(CmdFlags.Dynamic) || command.Flags.HasFlag(CmdFlags.Subcommand)) continue; // we do not allow customization of Subcommands or dynamic commands at this time

                    var cmdname = command.aliases[0];
                    if (everything) cmdname += new string(' ', 20 - cmdname.Length);

                    if (command.Flags.HasFlag(CmdFlags.Variable) && (everything | command.ChangedParameters.HasFlag(ChangedFlags.Variable)))
                        {
                        if (everything) target.Append("// ");
                        target.Append($"{cmdname}= {command.userParameter.ToString()}\r\n");
                        }
                    else
                    {
                        if (everything || (command.ChangedParameters & ChangedFlags.Any) != 0)
                        {
                            if (everything) target.Append("// ");
                            target.Append($"{cmdname} =");
                            if (everything || command.ChangedParameters.HasFlag(ChangedFlags.Aliases)) target.Append($" /alias {command.GetAliases()}");
                            if (everything || command.ChangedParameters.HasFlag(ChangedFlags.Flags)) target.Append($" /flags {command.GetFlags()}");
                            if (everything || command.ChangedParameters.HasFlag(ChangedFlags.Help)) target.Append($" /sethelp {command.GetHelpText()}");
                            target.Append("\r\n");
                        }
                    }
                }
            }

            public static void WriteCommandConfiguration(string configfilename = "SRMCommands")
                {
                var UserSettings = new StringBuilder("// This section contains ONLY commands that have changed.\r\n\r\n");

                SummarizeCommands(UserSettings, false);

                UserSettings.Append("\r\n");
                UserSettings.Append("// This is a summary of the current command states, these are for reference only. Use the uncommented section for your changes.\r\n\r\n");

                UserSettings.Append(commandsummary.ToString());
                // BUG: Ok, we should probably just use a text file. But I very 

                UserSettings.Append(
@"//
// The Command name or FIRST alias in the alias list is considered the Base name of the command, and absolutely should not be changed through code. Choose this first name wisely
// We use the Base name to allow user command Customization, it is how the command is identified to the user. You can alter the alias list of the commands in
// the command configuration file(botcommands.ini).
// 
// The file format is as follows:
//  
// < !Base commandname > /alias < alias(s) /flags < command flags > /sethelp Help text
//            
// You only need to change the parts that you wish to modify. Leaving a section out, or blank will result in it being ignored.
// /sethelp MUST be the last section, since it allows command text with /'s, up to and including help messages for /sethelp.
// Command lines with errors will be displayed, possibly ignored. 
//                    
// Examples:
//                   
// !request /alias request bsr add sr /flags Mod Sub VIP Broadcaster /sethelp New help text for request
// !queue /alias queue, requested
// !block /alias block, Ban 
// !lookup /disable
//");
                var filename = Path.Combine(Plugin.DataPath, configfilename + ".ini");
                File.WriteAllText(filename, UserSettings.ToString());
            }

            // BUG: This is pass 1, refactoring will get done eventually.
            public static void CommandConfiguration(string configfilename="SRMCommands")
                {

                var UserSettings = new StringBuilder("// This section contains ONLY commands that have changed.\r\n\r\n");

                var filename = Path.Combine(Plugin.DataPath, configfilename + ".ini");

                Loading = true; // Prevents file updates during command load.

                // This is probably just runscript

                try
                {
                    using (StreamReader sr = new StreamReader(filename))
                    {
                        while (sr.Peek() >= 0)
                        {
                            string line = sr.ReadLine();
                            line.Trim(' ');
                            if (line.Length < 2 || line.StartsWith("//")) continue;

                            UserSettings.Append(line).Append("\r\n");
                            // MAGICALLY configure the customized commands 

                            COMMAND.Parse(ChatHandler.Self, line, CmdFlags.SilentResult | CmdFlags.Local);
                        }
                        sr.Close();
                    }
                    
                }
                catch       
                {
                // If it doesn't exist, or ends early, that's fine.
                }
                Loading = false;

                WriteCommandConfiguration();
            }

            #endregion

        }
        #endregion

        public class ParseState
        {
            public TwitchUser user;
            public String request;
            public CmdFlags flags;
            public string info;

            public string command = null;
            public string parameter = "";
            public string sort = "";

            public COMMAND botcmd = null;

            public string subparameter="";

            // Object clone constructor. Mostly used when spawning multiple threads against a single command
            public ParseState(ParseState state, string parameter = null)
            {
                // These are references
                this.user = state.user;
                this.botcmd = state.botcmd;

                this.flags = state.flags;
                this.parameter = state.parameter;
                if (parameter != null) this.parameter = parameter;
                this.subparameter = state.subparameter;
                this.command = state.command;
                this.info = state.info;
                this.sort = state.sort;
            }

            public ParseState(ref TwitchUser user, ref string request, CmdFlags flags, ref string info)
            {
                this.user = user;
                this.request = request;
                this.flags = flags;
                this.info = info;
            }

            // BUG: Execute command and subcommand can probably be largely unified soon

            public string ExecuteSubcommand() // BUG: Only one supported for now (till I finalize the parse logic) ,we'll make it all work eventually
            {
                int commandstart = 0;

                if (parameter.Length<2) return notsubcommand;

                int subcommandend = parameter.IndexOfAny(new[] { ' ', '/' }, 1);
                if (subcommandend == -1) subcommandend = parameter.Length;

                int subcommandsectionend = parameter.IndexOf('/', 1);
                if (subcommandsectionend == -1) subcommandsectionend = parameter.Length;

                //RequestBot.Instance.QueueChatMessage($"parameter [{parameter}] ({subcommandend},{subcommandsectionend})");

                int commandlength = subcommandend - commandstart;

                if (commandlength == 0) return notsubcommand;

                string subcommand = parameter.Substring(commandstart, commandlength).ToLower();

                subparameter = (subcommandsectionend - subcommandend>0) ? parameter.Substring(subcommandend, subcommandsectionend - subcommandend).Trim(' ') : "";

                COMMAND subcmd;
                if (!COMMAND.aliaslist.TryGetValue(subcommand, out subcmd)) return notsubcommand;

                if (!subcmd.Flags.HasFlag(CmdFlags.Subcommand)) return notsubcommand;
                // BUG: Need to check subcmd permissions here.     

                if (!HasRights(ref subcmd, ref user,flags)) return error($"No permission to use {subcommand}");

                if (subcmd.Flags.HasFlag(CmdFlags.NoParameter))
                {
                    parameter = parameter.Substring(subcommandend).Trim(' ');
                }
                else
                {
                    parameter = parameter.Substring(subcommandsectionend);
                }

                try
                {
                    return subcmd.subcommand(this);
                }
                catch (Exception ex)
                {
                    Plugin.Log(ex.ToString());
                }

                return "";
            }

            public string msg(string text,string result=success)
            {
                if (!flags.HasFlag(CmdFlags.SilentResult))  new DynamicText().AddUser(ref user).AddBotCmd(ref botcmd).QueueMessage(ref text);
                return result;
            }

            public string error(string Error)
            {
                return text(Error);
            }

            public string helptext(bool showlong=false)
            {
                return new DynamicText().AddUser(ref user).AddBotCmd(ref botcmd).Parse(ref botcmd.ShortHelp, showlong);
            }

            public string text(string text) // Return a formatted text message
            {
                return new DynamicText().AddUser(ref user).AddBotCmd(ref botcmd).Parse(ref text);
            }

            static string done = "X";
            public async void ExecuteCommand()
            {
                if (!COMMAND.aliaslist.TryGetValue(command, out botcmd)) return; // Unknown command

                // Permissions for these sub commands will always be by Broadcaster,or the (BUG: Future feature) user list of the EnhancedTwitchBot command. Note command behaviour that alters with permission should treat userlist as an escalation to Broadcaster.
                // Since these are never meant for an end user, they are not going to be configurable.

                // Example: !challenge/allow myfriends
                //          !decklist/setflags SUB
                //          !lookup/sethelp usage: %alias%<song name or id>
                //
                while (true)
                {
                    string errormsg = ExecuteSubcommand();
                    if (errormsg == notsubcommand)  break;
                    if (errormsg != "")
                    {
                        if (errormsg == done)
                        {
                            flags |= CmdFlags.Disabled; // Temporarily disable the rest of the command - flags is local parse state flag.
                            continue;
                        }
                    else
                    {
                        Instance.QueueChatMessage(errormsg);
                        //ShowHelpMessage(ref botcmd, ref user, parameter, false);
                    }
                        return;
                    }
                }

                if (botcmd.ChangedParameters != 0 && !botcmd.ChangedParameters.HasFlag(COMMAND.ChangedFlags.Saved))
                {
                    if (COMMAND.Loading==false) COMMAND.WriteCommandConfiguration();
                    botcmd.ChangedParameters |= COMMAND.ChangedFlags.Saved;
                }

                if (botcmd.Flags.HasFlag(CmdFlags.Disabled) || flags.HasFlag(CmdFlags.Disabled)) return; // Disabled commands fail silently

                // Check permissions first

                bool allow = HasRights(ref botcmd, ref user,flags);

                if (!allow && !botcmd.Flags.HasFlag(CmdFlags.BypassRights) && !listcollection.contains(ref botcmd.permittedusers, user.DisplayName.ToLower()))
                {
                    CmdFlags twitchpermission = botcmd.Flags & CmdFlags.TwitchLevel;
                    if (!botcmd.Flags.HasFlag(CmdFlags.SilentCheck)) Instance?.QueueChatMessage($"{command} is restricted to {twitchpermission.ToString()}");
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
                    if (!botcmd.Flags.HasFlag(CmdFlags.SilentCheck)) ShowHelpMessage(ref botcmd, ref user, parameter, false);
                    return;
                }

                try
                {
                    string errormsg = await botcmd.Execute(this); // Call the command
                    if (errormsg != "" && !flags.HasFlag(CmdFlags.SilentError))
                    {
                        Instance.QueueChatMessage(errormsg);
                    }
                }
                catch (Exception ex)
                {
                    // Display failure message, and lock out command for a time period. Not yet.
                    Plugin.Log(ex.ToString());
                }
            }


            public static string GetCommand(ref string request)
            {
                int commandlength = 0;
                // This is a replacement for the much simpler Split code. It was changed to support /fakerest parameters, and sloppy users ... ie: !add4334-333 should now work, so should !command/flags
                while (commandlength < request.Length && (request[commandlength] != '=' && request[commandlength] != '/' && request[commandlength] != ' ')) commandlength++;  // Command name ends with #... for now, I'll clean up some more later    
                if (commandlength == 0) return "";
                return request.Substring(0, commandlength).ToLower();
            }

            public ParseState ParseCommand()
            {
                // Notes for later.
                //var match = Regex.Match(request, "^!(?<command>[^ ^/]*?<parameter>.*)");
                //string username = match.Success ? match.Groups["command"].Value : null;

                int commandstart = 0;
                int parameterstart = 0;

                // This is a replacement for the much simpler Split code. It was changed to support /fakerest parameters, and sloppy users ... ie: !add4334-333 should now work, so should !command/flags
                while (parameterstart < request.Length && (request[parameterstart] != '=' && request[parameterstart] != '/' && request[parameterstart] != ' ')) parameterstart++;  // Command name ends with #... for now, I'll clean up some more later           
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
                        Plugin.Log(ex.ToString());
                    }
                }

                return this;
            }
        }

        // A much more general solution for extracting dymatic values into a text string. If we need to convert a text message to one containing local values, but the availability of those values varies by calling location
        // We thus build a table with only those values we have. 

        // BUG: This is actually part of botcmd, please move
        public static void ShowHelpMessage(ref COMMAND botcmd, ref TwitchUser user, string param, bool showlong)
        {
            if (botcmd.Flags.HasFlag(CmdFlags.Disabled)) return; // Make sure we're allowed to show help

            new DynamicText().AddUser(ref user).AddBotCmd(ref botcmd).QueueMessage(ref botcmd.ShortHelp, showlong);
            return;
        }

        // Get help on a command
        private string help(ParseState state)
        {
            if (state.parameter == "")
            {
                var msg = new QueueLongMessage();
                msg.Header("Usage: help < ");
                foreach (var entry in COMMAND.aliaslist)
                {
                    var botcmd = entry.Value;
                    if (HasRights(ref botcmd, ref state.user,0) && !botcmd.Flags.HasFlag(Subcmd) && !botcmd.Flags.HasFlag(Var))
                                
                        msg.Add($"{entry.Key.TrimStart('!')}", " "); // BUG: Removes the built in ! in the commands, letting it slide... for now 
                }
                msg.Add(">");
                msg.end("...", $"No commands available >");
                return success;
            }
            if (COMMAND.aliaslist.ContainsKey(state.parameter.ToLower()))
            {
                var BotCmd = COMMAND.aliaslist[state.parameter.ToLower()];
                ShowHelpMessage(ref BotCmd, ref state.user, state.parameter, true);
            }
            else if (COMMAND.aliaslist.ContainsKey("!" + state.parameter.ToLower())) // BUG: Ugly code, gets help on ! version of command
            {
                var BotCmd = COMMAND.aliaslist["!" + state.parameter.ToLower()];
                ShowHelpMessage(ref BotCmd, ref state.user, state.parameter, true);
            }
            else
            {
                QueueChatMessage($"Unable to find help for {state.parameter}.");
            }
            return success;
        }

        public static bool HasRights(ref COMMAND botcmd, ref TwitchUser user,CmdFlags flags)
        {
            if (flags.HasFlag(CmdFlags.Local)) return true;
            if (botcmd.Flags.HasFlag(CmdFlags.Disabled)) return false;
            if (botcmd.Flags.HasFlag(CmdFlags.Everyone)) return true; // Not sure if this is the best approach actually, not worth thinking about right now
            if (user.IsModerator & RequestBotConfig.Instance.ModFullRights) return true;
            if (user.IsBroadcaster & botcmd.Flags.HasFlag(CmdFlags.Broadcaster)) return true;
            if (user.IsModerator & botcmd.Flags.HasFlag(CmdFlags.Mod)) return true;
            if (user.IsSubscriber & botcmd.Flags.HasFlag(CmdFlags.Sub)) return true;
            if (user.IsVip & botcmd.Flags.HasFlag(CmdFlags.VIP)) return true;
            return false;

        }
        #endregion
    }
}