# Mod Info
Song Request Manager is an integrated, fully Customizable song request bot and Console for BeatSaber. It started life as an extensive rewrite of the built in song request bot in https://github.com/brian91292/BeatSaber-EnhancedTwitchChat, but quickly grew in scope and features. Its now a separate but dependent module. We just split from Enhanced twitch chat.

# TTS Notes
If you're using TTS, you'll want to reduce the amount of spam the bot produces. You can do this a number of ways. Filtering out your Name from TTS, or 
```
in RequestBotSettings.ini
BotPrefix="! "
```
then filter out the ! lines on your tts client.

# Features
Completely customizable, Full featured song request bot for Beatsaber, currently supports Twitch. 
Over 80 commands, all with custom help, permissions and customization options.
Persistent Song request Queue, History, Played history and already played list. 
In game song console allows you to Play requested songs. 
Integrated twitch keyboard allows using your chat console directly Ingame! User twitch chat or liv for chat display.
Experimental song search and newest feature allows you to search for songs directly from console without exiting to browser or downloader.
Dozens of new features are being worked on, and will be released as testing allows.

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
| **RequestBotEnabled** | When set to true, users can make song requests in chat. |

# Compiling
To compile this mod simply clone the repo and update the project references to reference the corresponding assemblies in the `Beat Saber\Beat Saber_Data\Managed` and `Beat Saber\Plugins` folder, then compile. You may need to change the post build event if your Beat Saber directory isn't at the same location as mine.

# Tips
This plugin is free. If you wish to help us out though, tips to 
[our Paypal](https://paypal.me/sehria) are always appreciated.

# Download
[Click here to download the latest SongRequestManager.dll](https://github.com/angturil/SongRequestManager/releases/download/1.3.0/SongRequestManager.dll)
