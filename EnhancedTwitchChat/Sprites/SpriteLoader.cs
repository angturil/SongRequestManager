using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using EnhancedTwitchChat.Utils;
using EnhancedTwitchChat.Chat;
using UnityEngine.XR;

namespace EnhancedTwitchChat.Sprites
{
    public class CachedSpriteData
    {
        public Sprite sprite = null;
        public List<AnimationData> animationInfo = null;
        public CachedSpriteData(Sprite sprite)
        {
            this.sprite = sprite;
        }
        public CachedSpriteData(List<AnimationData> animationInfo)
        {
            this.animationInfo = animationInfo;
        }
    };

    public enum ImageType
    {
        None,
        Twitch,
        BTTV,
        BTTV_Animated,
        FFZ,
        Badge,
        Emoji
    };

    public class ImageTypeNames
    {
        private static string[] Names = new string[] { "None", "Twitch", "BetterTwitchTV", "BetterTwitchTV", "FrankerFaceZ", "Badges", "Emojis" };

        public static string Get(ImageType type)
        {
            return Names[(int)type];
        }
    };

    public class SpriteDownloadInfo
    {
        public string index;
        public ImageType type;
        public string messageIndex;
        public SpriteDownloadInfo(string index, ImageType type, string messageIndex)
        {
            this.index = index;
            this.type = type;
            this.messageIndex = messageIndex;
        }
    };

    public class TextureSaveInfo
    {
        public string path;
        public byte[] data;
        public TextureSaveInfo(string path, byte[] data)
        {
            this.path = path;
            this.data = data;
        }
    };

    public class SpriteLoader : MonoBehaviour
    {
        public static ConcurrentDictionary<string, string> BTTVEmoteIDs = new ConcurrentDictionary<string, string>();
        public static ConcurrentDictionary<string, string> FFZEmoteIDs = new ConcurrentDictionary<string, string>();
        public static ConcurrentDictionary<string, string> TwitchBadgeIDs = new ConcurrentDictionary<string, string>();
        public static ConcurrentDictionary<string, string> BTTVAnimatedEmoteIDs = new ConcurrentDictionary<string, string>();

        public static ConcurrentDictionary<string, CachedSpriteData> CachedSprites = new ConcurrentDictionary<string, CachedSpriteData>();
        public static ConcurrentStack<TextureSaveInfo> SpriteSaveQueue = new ConcurrentStack<TextureSaveInfo>();
        private ConcurrentStack<SpriteDownloadInfo> _spriteDownloadQueue = new ConcurrentStack<SpriteDownloadInfo>();
        private bool _loaderBusy = false;
        public static SpriteLoader Instance = null;
        private int _waitForFrames = 0;

        public void Awake()
        {
            UnityEngine.Object.DontDestroyOnLoad(this);

            if (Instance == null) Instance = this;
            else Destroy(this);
        }

        public void Init()
        {
            BTTVEmoteIDs.Clear();
            FFZEmoteIDs.Clear();
            TwitchBadgeIDs.Clear();

            // Grab a list of bttv/ffz emotes BEFORE we join a channel and start getting spammed with messages
            StartCoroutine(GetBTTVGlobalEmotes());
            StartCoroutine(GetBTTVChannelEmotes());
            StartCoroutine(GetFFZGlobalEmotes());
            StartCoroutine(GetFFZChannelEmotes());
            StartCoroutine(GetTwitchChannelBadges());
            StartCoroutine(GetTwitchGlobalBadges());
        }

        public void Update()
        {
            // Skip this frame if our fps is low
            float fps = 1.0f / Time.deltaTime;
            if (!Plugin.Instance.IsAtMainMenu && fps < XRDevice.refreshRate - 5)
            {
                //_waitForFrames = (int)XRDevice.refreshRate;
                return;
            }

            // Download any emotes we need cached for one of our messages
            if (_spriteDownloadQueue.Count > 0 && !_loaderBusy && _waitForFrames == 0)
            {
                if (_spriteDownloadQueue.TryPop(out var spriteDownloadInfo))
                {
                    switch (spriteDownloadInfo.type)
                    {
                        case ImageType.Twitch:
                            StartCoroutine(Download($"https://static-cdn.jtvnw.net/emoticons/v1/{spriteDownloadInfo.index.Substring(1)}/3.0", spriteDownloadInfo));
                            break;
                        case ImageType.BTTV:
                            StartCoroutine(Download($"https://cdn.betterttv.net/emote/{spriteDownloadInfo.index.Substring(1)}/3x", spriteDownloadInfo));
                            break;
                        case ImageType.BTTV_Animated:
                            StartCoroutine(Download($"https://cdn.betterttv.net/emote/{spriteDownloadInfo.index.Substring(2)}/3x", spriteDownloadInfo));
                            break;
                        case ImageType.FFZ:
                            StartCoroutine(Download($"https://cdn.frankerfacez.com/{spriteDownloadInfo.index.Substring(1)}", spriteDownloadInfo));
                            break;
                        case ImageType.Badge:
                            StartCoroutine(Download($"https://static-cdn.jtvnw.net/badges/v1/{spriteDownloadInfo.index}/3", spriteDownloadInfo));
                            break;
                        case ImageType.Emoji:
                            StartCoroutine(Download(string.Empty, spriteDownloadInfo));
                            break;
                    }
                }
            }
            else if (_waitForFrames > 0)
            {
                _waitForFrames--;
            }
        }

        public void Queue(SpriteDownloadInfo emote)
        {
            _spriteDownloadQueue.Push(emote);
        }

        public static IEnumerator Download(string spritePath, SpriteDownloadInfo spriteDownloadInfo, bool isRetry = false)
        {
            Instance._loaderBusy = true;
            if (!CachedSprites.ContainsKey(spriteDownloadInfo.index))
            {
                string origSpritePath = spritePath;

                string spriteCachePath = "Cache\\Sprites";
                if (!Directory.Exists(spriteCachePath))
                {
                    Directory.CreateDirectory(spriteCachePath);
                }

                string typePath = $"{spriteCachePath}\\{ImageTypeNames.Get(spriteDownloadInfo.type)}";
                if (!Directory.Exists(typePath))
                {
                    Directory.CreateDirectory(typePath);
                }

                bool localPathExists = false;
                string localFilePath = $"{typePath}\\{spriteDownloadInfo.index}";
                if (File.Exists(localFilePath))
                {
                    localPathExists = true;
                    spritePath = $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase).Replace("\\Plugins", "")}\\{localFilePath}";
                    //Plugin.Log("Local path exists!");
                }
                else
                {
                    if (spriteDownloadInfo.type == ImageType.Emoji)
                    {
                        Plugin.Log($"Local path did not exist for Emoji {spriteDownloadInfo.index}!");
                        while (!CachedSprites.TryAdd(spriteDownloadInfo.index, new CachedSpriteData((Sprite)null))) yield return null;
                        Instance._loaderBusy = false;
                        yield break;
                    }
                    //Plugin.Log($"Path {spritePath} does not exist!");
                }

                Sprite sprite;
                using (var web = UnityWebRequestTexture.GetTexture(spritePath, true))
                {
                    yield return web.SendWebRequest();
                    if (web.isNetworkError || web.isHttpError)
                    {
                        Plugin.Log($"An error occured when requesting emote {spriteDownloadInfo.index}, Message: \"{web.error}\"");
                        if (spriteDownloadInfo.type == ImageType.BTTV_Animated)
                        {
                            while (!CachedSprites.TryAdd(spriteDownloadInfo.index, null)) yield return null;
                        }
                        else
                        {
                            while (!CachedSprites.TryAdd(spriteDownloadInfo.index, new CachedSpriteData((Sprite)null))) yield return null;
                        }
                        sprite = null;
                    }
                    else
                    {
                        if (spriteDownloadInfo.type == ImageType.BTTV_Animated)
                        {
                            while (!CachedSprites.TryAdd(spriteDownloadInfo.index, null)) yield return null;
                            yield return AnimatedSpriteDecoder.Process(web.downloadHandler.data, ChatHandler.Instance.OverlayAnimatedEmote, spriteDownloadInfo.index);
                            if (!localPathExists)
                                SpriteSaveQueue.Push(new TextureSaveInfo(localFilePath, web.downloadHandler.data));
                        }
                        else
                        {
                            var tex = DownloadHandlerTexture.GetContent(web);

                            yield return null;

                            bool success = false;
                            try
                            {
                                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0, 0), ChatHandler.Instance.pixelsPerUnit);
                                success = true;
                            }
                            catch (Exception e)
                            {
                                Plugin.Log(e.ToString());
                                if (File.Exists(localFilePath)) File.Delete(localFilePath);
                                sprite = null;
                            }

                            if (!success && !isRetry)
                            {
                                yield return Download(origSpritePath, spriteDownloadInfo, true);
                                yield break;
                            }

                            while (!CachedSprites.TryAdd(spriteDownloadInfo.index, new CachedSpriteData(sprite))) yield return null;
                            yield return null;

                            ChatHandler.Instance.OverlayEmote(sprite, spriteDownloadInfo.index);

                            if (!localPathExists && success)
                                SpriteSaveQueue.Push(new TextureSaveInfo(localFilePath, web.downloadHandler.data));
                        }
                    }
                }
                //Plugin.Log($"Web request completed, {CachedSprites.Count} emotes now cached!");
                if (Plugin.Instance.IsAtMainMenu)
                    Instance._waitForFrames = 10;
                else
                    Instance._waitForFrames = 25;
            }

            Instance._loaderBusy = false;
        }

        public static IEnumerator GetTwitchGlobalBadges()
        {
            Plugin.Log($"Downloading twitch global badge listing");

            using (var web = UnityWebRequest.Get("https://badges.twitch.tv/v1/badges/global/display"))
            {
                yield return web.SendWebRequest();
                if (web.isNetworkError || web.isHttpError)
                {
                    Plugin.Log($"An error occured when requesting twitch global badge listing, Message: \"{web.error}\"");
                }
                else
                {
                    var json = DownloadHandlerBuffer.GetContent(web);

                    if (json.Count() > 0)
                    {
                        var j = SimpleJSON.JSON.Parse(json);
                        if (j["badge_sets"].IsObject)
                        {
                            foreach (KeyValuePair<string, SimpleJSON.JSONNode> kvp in j["badge_sets"])
                            {
                                string name = kvp.Key;
                                var o = kvp.Value.AsObject;
                                foreach (KeyValuePair<string, SimpleJSON.JSONNode> version in o["versions"].AsObject)
                                {
                                    string versionID = version.Key;
                                    var versionObject = version.Value.AsObject;
                                    string url = versionObject["image_url_4x"];
                                    string index = url.Substring(url.IndexOf("/v1/") + 4).Replace("/3", "");
                                    while (!TwitchBadgeIDs.TryAdd($"{name}{versionID}", index))
                                    {
                                        if (name == "subscriber")
                                        {
                                            Plugin.Log("Subscriber badge already exists! Skipping!");
                                            break;
                                        }
                                        yield return null;
                                    }
                                }
                            }
                        }
                    }
                    Plugin.Log("Web request completed, twitch global badges now cached!");
                }
            }
        }

        public static IEnumerator GetTwitchChannelBadges()
        {
            while (TwitchIRCClient.ChannelID == string.Empty)
            {
                yield return null;
            }
            Plugin.Log($"Downloading twitch channel badge listing");
            using (var web = UnityWebRequest.Get($"https://badges.twitch.tv/v1/badges/channels/{TwitchIRCClient.ChannelID}/display"))
            {
                yield return web.SendWebRequest();
                if (web.isNetworkError || web.isHttpError)
                {
                    Plugin.Log($"An error occured when requesting twitch channel badge listing, Message: \"{web.error}\"");
                }
                else
                {
                    var json = DownloadHandlerBuffer.GetContent(web);

                    if (json.Count() > 0)
                    {
                        var j = SimpleJSON.JSON.Parse(json);
                        if (j["badge_sets"]["subscriber"].IsObject)
                        {
                            var cur = j["badge_sets"]["subscriber"]["versions"].AsObject;
                            string name = "subscriber";

                            foreach (KeyValuePair<string, SimpleJSON.JSONNode> version in cur)
                            {
                                string versionID = version.Key;
                                var versionObject = version.Value.AsObject;
                                string url = versionObject["image_url_4x"];
                                string index = url.Substring(url.IndexOf("/v1/") + 4).Replace("/3", "");
                                string finalName = $"{name}{versionID}";

                                while (!TwitchBadgeIDs.TryAdd(finalName, index))
                                {
                                    // Overwrite the affiliate sub badges if the channel has any custom ones
                                    if (TwitchBadgeIDs.TryGetValue(finalName, out var existing))
                                    {
                                        TwitchBadgeIDs[finalName] = index;
                                        Plugin.Log("Replaced default sub icon!");
                                        break;
                                    }
                                    yield return null;
                                }
                            }
                        }
                    }
                    Plugin.Log("Web request completed, twitch channel badges now cached!");
                }
            }
        }

        public static IEnumerator GetBTTVGlobalEmotes()
        {
            Plugin.Log("Downloading BTTV global emote listing");

            using (var web = UnityWebRequest.Get("https://api.betterttv.net/2/emotes"))
            {
                yield return web.SendWebRequest();
                if (web.isNetworkError || web.isHttpError)
                {
                    Plugin.Log($"An error occured when requesting BTTV global emote listing, Message: \"{web.error}\"");
                }
                else
                {
                    var json = DownloadHandlerBuffer.GetContent(web);
                    if (json.Count() > 0)
                    {
                        var j = SimpleJSON.JSON.Parse(json);
                        if (j["status"].AsInt == 200)
                        {
                            var emotes = j["emotes"].AsArray;
                            foreach (SimpleJSON.JSONObject o in emotes)
                            {
                                if (o["channel"] == null)
                                {
                                    if (o["imageType"] != "gif")
                                    {
                                        while (!BTTVEmoteIDs.TryAdd(o["code"], o["id"])) yield return null;
                                    }
                                    else
                                    {
                                        while (!SpriteLoader.BTTVAnimatedEmoteIDs.TryAdd(o["code"], o["id"])) yield return null;
                                    }
                                }
                            }
                        }
                    }
                    Plugin.Log("Web request completed, BTTV global emotes now cached!");
                }
            }
        }

        public static IEnumerator GetBTTVChannelEmotes()
        {
            Plugin.Log($"Downloading BTTV emotes for channel {Plugin.Instance.Config.TwitchChannel}");

            using (var web = UnityWebRequest.Get($"https://api.betterttv.net/2/channels/{Plugin.Instance.Config.TwitchChannel}"))
            {
                yield return web.SendWebRequest();
                if (web.isNetworkError || web.isHttpError)
                {
                    Plugin.Log($"An error occured when requesting BTTV channel emote listing, Message: \"{web.error}\"");
                }
                else
                {
                    var json = DownloadHandlerBuffer.GetContent(web);
                    if (json.Count() > 0)
                    {
                        var j = SimpleJSON.JSON.Parse(json);
                        if (j["status"].AsInt == 200)
                        {
                            var emotes = j["emotes"].AsArray;
                            foreach (SimpleJSON.JSONObject o in emotes)
                            {
                                if (o["imageType"] != "gif")
                                {
                                    while (!BTTVEmoteIDs.TryAdd(o["code"], o["id"])) yield return null;
                                }
                                else
                                {
                                    while (!SpriteLoader.BTTVAnimatedEmoteIDs.TryAdd(o["code"], o["id"])) yield return null;
                                }
                            }
                        }
                    }
                    Plugin.Log("Web request completed, BTTV channel emotes now cached!");
                }
            }
        }

        public static IEnumerator GetFFZGlobalEmotes()
        {
            Plugin.Log("Downloading FFZ global emote listing");

            using (var web = UnityWebRequest.Get("https://api.frankerfacez.com/v1/set/global"))
            {
                yield return web.SendWebRequest();
                if (web.isNetworkError || web.isHttpError)
                {
                    Plugin.Log($"An error occured when requesting FFZ global emote listing, Message: \"{web.error}\"");
                }
                else
                {
                    var json = DownloadHandlerBuffer.GetContent(web);
                    if (json.Count() > 0)
                    {
                        var j = SimpleJSON.JSON.Parse(json);
                        if (j["sets"].IsObject)
                        {
                            var emotes = j["sets"]["3"]["emoticons"].AsArray;
                            foreach (SimpleJSON.JSONObject o in emotes)
                            {
                                var urls = o["urls"].AsObject;
                                string url = urls[urls.Count - 1];
                                string index = url.Substring(url.IndexOf(".com/") + 5);

                                while (!FFZEmoteIDs.TryAdd(o["name"], index)) yield return null;
                            }
                        }
                    }
                    Plugin.Log("Web request completed, FFZ global emotes now cached!");
                }
            }
        }
        public static IEnumerator GetFFZChannelEmotes()
        {
            Plugin.Log($"Downloading FFZ emotes for channel {Plugin.Instance.Config.TwitchChannel}");

            using (var web = UnityWebRequest.Get($"https://api.frankerfacez.com/v1/room/{Plugin.Instance.Config.TwitchChannel}"))
            {
                yield return web.SendWebRequest();
                if (web.isNetworkError || web.isHttpError)
                {
                    Plugin.Log($"An error occured when requesting FFZ channel emote listing, Message: \"{web.error}\"");
                }
                else
                {
                    var json = DownloadHandlerBuffer.GetContent(web);
                    if (json.Count() > 0)
                    {
                        var j = SimpleJSON.JSON.Parse(json);
                        if (j["sets"].IsObject)
                        {
                            var emotes = j["sets"][j["room"]["set"].ToString()]["emoticons"].AsArray;
                            foreach (SimpleJSON.JSONObject o in emotes)
                            {
                                var urls = o["urls"].AsObject;
                                string url = urls[urls.Count - 1];
                                string index = url.Substring(url.IndexOf(".com/") + 5);

                                while (!FFZEmoteIDs.TryAdd(o["name"], index)) yield return null;
                            }
                        }
                    }
                    Plugin.Log("Web request completed, FFZ channel emotes now cached!");
                }
            }
        }
    };
}
