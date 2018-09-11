# Mod Info
BetterTwitchChat is a rich text Twitch chat integration mod with full unicode, emote, and emoji support.

# Beta
The mod is currently in beta, which means you may experience some bugs. Please report any bugs via the issues tab on GitHub.

# Features
- Full Rich Text Support, see ALL of your Twitch chat when immersed in Beat Saber!
  - This includes all Twitch badges and emotes, BetterTwitchTV emotes, FrankerFaceZ emotes, and all Emojis!
  - This also includes full Unicode Support!
- Customizable User Interface
  - Change the scale, width, text/background colors, and chat order!
- *Coming soon*
  - Popular alert API integration with services such as streamlabs
  - More chat customization options!

# Getting Started
After installing BetterTwitchChat.dll, run the game once to generate the BetterTwitchChat.ini file in the UserData folder inside your Beat Saber directory.

# Setup
All you need to enter is the channel name which you want to join, the chat will show up to your right in game, and you can move it by pointing at it with the laser from your controller and grabbing it with the trigger. You can also move the chat towards you and away from you by pressing up and down on the analog stick or trackpad on your controller. Finally, you can use the lock button in the corner of the chat to lock the chat position in place so you don't accidentally move it.

# Config
For the rest of the setup, you will have to manually edit the config file (in UserData\BetterTwitchChat.ini).  *Keep in mind all config options will update in realtime when you save the file! This means you don't have to restart the game to see your changes!* Use the table below as a guide for setting these values:

| Option                     | Description                                                                                                                  |
|----------------------------|------------------------------------------------------------------------------------------------------------------------------|
| **TwitchChannel**          | The name of the Twitch channel whos chat you want to join                                                                    |
| **FontName**               | The name of the system font that should be used for chat messages. You can specify any font installed on your computer!      |
| **ChatScale**              | How large the chat messages/emotes should be displayed.                                                                      |
| **ChatWidth**              | The width of the chat, regardless of ChatScale.                                                                              |
| **LineSpacing**            | Determines the amount of extra spacing between lines of chat                                                                 |
| **MaxMessages**            | The maximum number of messages allowed in chat at once.                                                                      |
| **PositionX/Y/Z**          | The location of the chat in game (this can be adjusted in game, see description above!)                                      |
| **RotationX/Y/Z**          | The rotation of the chat in game (this can be adjusted in game, see description above!)                                      |
| **TextColorR/G/B/A**       | The color of chat messages, on a scale of 0-1. If your colors are between 0-255, just divide by 255 to get this value!       |
| **BackgroundColorR/G/B/A** | The color of the chat background, on a scale of 0-1. If your colors are between 0-255, just divide by 255 to get this value! |
| **LockChatPosition**       | Whether or not the chat can be moved by pointing at it with the controller laser and gripping with the trigger.              |
| **ReverseChatOrder**       | When set to true, chat messages will enter from the top and exit on bottom instead of entering on bottom and exiting on top. |

# Download
[Click here to download the latest BetterTwitchChat.dll](https://github.com/brian91292/BeatSaber-BetterTwitchChat/releases)
