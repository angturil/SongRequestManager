# Mod Info
Song Request Manager is an integrated, fully Customizable song request bot and Console for BeatSaber.

# Features
Documentation needs work. Type !help.
  
# Dependencies
Enhanced Twitch Chat depends on [EnhancedTwitchChat](https://www.modsaber.org/mod/enhancedtwitchchat), [CustomUI](https://www.modsaber.org/mod/customui/), [SongLoader](https://www.modsaber.org/mod/song-loader/), and [AsyncTwitch](https://www.modsaber.org/mod/asynctwitchlib/). Make sure to install them, or Song Request Manager Chat won't work!
  
# Installation
Copy SongRequestManager.dll to your Beat Saber\Plugins folder, and install all of its dependencies. That's it!

# Usage
All you need to enter is the channel name which you want to join (see the `Setup` section below), the chat will show up to your right in game, and you can move it by pointing at it with the laser from your controller and grabbing it with the trigger. You can also move the chat towards you and away from you by pressing up and down on the analog stick or trackpad on your controller. Finally, you can use the lock button in the corner of the chat to lock the chat position in place so you don't accidentally move it.

# Setup
Needs more documentation

# Config
For the rest of the config options, you will have to manually edit the config file (in UserData\EnhancedTwitchChat.ini).  *Keep in mind all config options will update in realtime when you save the file! This means you don't have to restart the game to see your changes!* Use the table below as a guide for setting these values (**NOTE:** The only required config option is TwitchChannelName):

| Option | Description |
| - | - |
| **TwitchChannelName** | The name of the Twitch channel whos chat you want to join (this is your Twitch username if you want to join your own channel) |
| **TwitchUsername** | Your twitch username for the account you want to send messages as in chat (only matters if you're using the request bot) |
| **TwitchOAuthToken** | The oauth token corresponding to the TwitchUsername entered above ([Click here to generate an oauth token](https://twitchapps.com/tmi/))  |
| **SongRequestBot** | When set to true, users can make song requests in chat. |

# Compiling
To compile this mod simply clone the repo and update the project references to reference the corresponding assemblies in the `Beat Saber\Beat Saber_Data\Managed` folder, then compile. You may need to change the post build event if your Beat Saber directory isn't at the same location as mine.

# Download
[Click here to download the latest SongRequestManager.dll](https://www.modsaber.org/mod/SongRequestManager/1.3.0)
