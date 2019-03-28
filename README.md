# Mod Info
Song Request Manager is an integrated, fully Customizable song request bot and Console for BeatSaber. It started life as an extensive rewrite of the built in song request bot in https://github.com/brian91292/BeatSaber-EnhancedTwitchChat, but quickly grew in scope and features. Its now a separate but dependent module. 

# TTS Notes
If you're using TTS, you'll want to reduce the amount of spam the bot produces. You can do this a number of ways. Filtering out your Name from TTS, or 
```
in RequestBotSettings.ini
BotPrefix="! "
```
then filter out the ! lines on your tts client.

# Features
```
  Full featured request bot with over 60 commands, and growing.
  Completely customizable, Every commmand can have multiple aliases, permissions and custom help text.
  Advanced filtering with Banlists, remapping, rating filters, mapper lists, and more.
  Display your song request queue and status directly on the stream.
  Different request limits based on the users subscription level.
  A rich text of moderator commands to manage the queue.
  An ingame console allowing the player to play the requested songs without having to search or downnload.
  A full featured twitch keyboard allowing interaction with twitch chat!
  Direct search of song directly from the console, without ever having to exit to song browser or downloader.
  Pick and play any of the latest 40 posted songs off Beatsaver.com 
  
  Many more features are being tested and will be released soon!.
```
  
# Dependencies
Enhanced Twitch Chat depends on [EnhancedTwitchChat](https://www.modsaber.org/mod/enhancedtwitchchat), [CustomUI](https://www.modsaber.org/mod/customui/), [SongLoader](https://www.modsaber.org/mod/song-loader/), and [AsyncTwitch](https://www.modsaber.org/mod/asynctwitchlib/). Make sure to install them, or Song Request Manager Chat won't work!
  
# Installation
Copy SongRequestManager.dll to your Beat Saber\Plugins folder, and install all of its dependencies. That's it!

# Usage
A song request icon will appear on the upper right of the main menu. It will be green if there are song requests in the queue, but you can press it regardless. Don't forget to Open the queue for requests when you are ready. It will stay that way until you close it again. The Open Queue button is on the lower right of the song request panel.

# Setup
Needs more documentation

# Config
The configuration files are located under UserData\EnhancedTwitchChat. RequestBotSettings.ini and TwitchLoginInfo.ini are the two files you need to adjust. *Keep in mind all config options will update in realtime when you save the file! This means you don't have to restart the game to see your changes!* Use the table below as a guide for setting these values (**NOTE:** You will need to setup your channel info to be able to receive song requests.)

# TwitchLoginInfo.ini
| Option | Description |
| - | - |
| **TwitchChannelName** | The name of the Twitch channel whos chat you want to join (this is your Twitch username if you want to join your own channel) |
| **TwitchUsername** | Your twitch username for the account you want to send messages as in chat (only matters if you're using the request bot) |
| **TwitchOAuthToken** | The oauth token corresponding to the TwitchUsername entered above ([Click here to generate an oauth token](https://twitchapps.com/tmi/))  |

# RequestBotSettings.ini
| Option | Description |
| - | - |
| **PersistentRequestQueue=True** | Resets the queue at the start of session - this will soon change to reset the queue after session reset, like the duplicate and played lists. |
| **RequestHistoryLimit=100** | How many entries are key in the history list of songs that you've already played/skipped |
| **RequestBotEnabled** | When set to true, users can make song requests in chat. |
| **UserRequestLimit=2** | Number of simulataneous song requests in the queue per tier
| **SubRequestLimit=5** |
| **ModRequestLimit=10** |
| **VipBonusRequests=1** | VIP's are treated as a bonus over their regular level. A non subbed VIP would get 3 song requests.
| **SessionResetAfterXHours=6** | Amount of time after session ENDS before your Duplicate song list and Played list are reset.
| **LowestAllowedRating=40** | Lowest allowed rating (as voted on [BeatSaver.com](on https://Beatsaver.com)) permitted. Unrated songs get a pass.|
| **AutopickFirstSong=False** | If on, will simply pick the first song. Otherwise, the recommended method shows a list of possible songs that match your search. Careful use of Block and Remap will make this method more effective over time |
| **UpdateQueueStatusFiles=True** | Enables the generation of queuestatus.txt and queuelist.txt. Use StreamOBS' Text (GDI+) option to display your queue status and list on your live stream! |
| **MaximumQueueTextEntries=8** | How many entries are sent to the queuelist.txt file. Any entries beyond that will display a ... |
| **BotPrefix =""** | This adds a prefix to all bot output, set it to "! " to allow filtering of all bot output by TTS or Enhanced Twitch chat. You can use other means like filtering by name to achiveve this |


# Compiling
To compile this mod simply clone the repo and update the project references to reference the corresponding assemblies in the `Beat Saber\Beat Saber_Data\Managed` and `Beat Saber\Plugins` folder, then compile. You may need to change the post build event if your Beat Saber directory isn't at the same location as mine.

# Tips
This plugin is free. If you wish to help us out though, tips to 
[our Paypal](https://paypal.me/sehria) are always appreciated.

# Download
[Click here to download the latest SongRequestManager.dll](https://github.com/angturil/SongRequestManager/releases/download/1.3.0/SongRequestManager.dll)
