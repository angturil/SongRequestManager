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
using EnhancedTwitchChat.UI;
using SimpleJSON;

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

    public class SpriteDownloader : MonoBehaviour
    {
        public static ConcurrentDictionary<string, string> BTTVEmoteIDs = new ConcurrentDictionary<string, string>();
        public static ConcurrentDictionary<string, string> FFZEmoteIDs = new ConcurrentDictionary<string, string>();
        public static ConcurrentDictionary<string, string> TwitchBadgeIDs = new ConcurrentDictionary<string, string>();
        public static ConcurrentDictionary<string, string> BTTVAnimatedEmoteIDs = new ConcurrentDictionary<string, string>();

        public static ConcurrentDictionary<string, CachedSpriteData> CachedSprites = new ConcurrentDictionary<string, CachedSpriteData>();
        public static ConcurrentStack<TextureSaveInfo> SpriteSaveQueue = new ConcurrentStack<TextureSaveInfo>();
        private ConcurrentStack<SpriteDownloadInfo> _downloadQueue = new ConcurrentStack<SpriteDownloadInfo>();
        private int _numDownloading = 0;
        public static SpriteDownloader Instance = null;
        
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
                return;

            // Download any emotes we need cached for one of our messages
            if (_downloadQueue.Count > 0 && _numDownloading < 2)
            {
                if (_downloadQueue.TryPop(out var spriteDownloadInfo))
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
                    _numDownloading++;
                }
            }
        }

        public void Queue(SpriteDownloadInfo emote)
        {
            _downloadQueue.Push(emote);
        }

        public static IEnumerator Download(string spritePath, SpriteDownloadInfo spriteDownloadInfo, bool isRetry = false)
        {
            if (!CachedSprites.ContainsKey(spriteDownloadInfo.index))
            {
                string origSpritePath = spritePath;

                string spriteCachePath = "Cache\\Sprites";
                if (!Directory.Exists(spriteCachePath))
                    Directory.CreateDirectory(spriteCachePath);

                string typePath = $"{spriteCachePath}\\{ImageTypeNames.Get(spriteDownloadInfo.type)}";
                if (!Directory.Exists(typePath))
                    Directory.CreateDirectory(typePath);

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
                        Instance._numDownloading--;
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
                            yield return AnimatedSpriteDecoder.Process(web.downloadHandler.data, ChatHandler.Instance.OverlayAnimatedEmote, spriteDownloadInfo);
                            if (!localPathExists)
                                SpriteSaveQueue.Push(new TextureSaveInfo(localFilePath, web.downloadHandler.data));
                        }
                        else
                        {
                            bool success = false;
                            try
                            {
                                Texture2D tex = DownloadHandlerTexture.GetContent(web);
                                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0, 0), Drawing.pixelsPerUnit);
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

                            ChatHandler.Instance.OverlaySprite(sprite, spriteDownloadInfo);

                            if (!localPathExists && success)
                                SpriteSaveQueue.Push(new TextureSaveInfo(localFilePath, web.downloadHandler.data));
                        }
                    }
                }
            }
            Instance._numDownloading--;
        }

        public static IEnumerator GetTwitchGlobalBadges()
        {
            Plugin.Log($"Downloading twitch global badge listing");
            int emotesCached = 0;
            using (var web = UnityWebRequest.Get("https://badges.twitch.tv/v1/badges/global/display"))
            {
                yield return web.SendWebRequest();
                if (web.isNetworkError || web.isHttpError)
                {
                    Plugin.Log($"An error occured when requesting twitch global badge listing, Message: \"{web.error}\"");
                    yield break;
                }
                if (web.downloadHandler.text.Length == 0) yield break;
                
                JSONNode json = JSON.Parse(web.downloadHandler.text);
                if (json["badge_sets"].IsObject)
                {
                    foreach (KeyValuePair<string, JSONNode> kvp in json["badge_sets"])
                    {
                        string name = kvp.Key;
                        JSONObject badge = kvp.Value.AsObject;
                        foreach (KeyValuePair<string, JSONNode> version in badge["versions"].AsObject)
                        {
                            JSONObject versionObject = version.Value.AsObject;
                            string versionID = version.Key;
                            string url = versionObject["image_url_4x"];
                            string index = url.Substring(url.IndexOf("/v1/") + 4).Replace("/3", "");
                            TwitchBadgeIDs.TryAdd($"{name}{versionID}", index);
                            emotesCached++;
                        }
                    }
                }
                Plugin.Log($"Web request completed, {emotesCached.ToString()} twitch global badges now cached!");
            }
        }

        public static IEnumerator GetTwitchChannelBadges()
        {
            while (TwitchIRCClient.ChannelIds[Config.Instance.TwitchChannel] == String.Empty)
            {
                yield return null;
            }
            Plugin.Log($"Downloading twitch channel badge listing");
            int emotesCached = 0;
            using (var web = UnityWebRequest.Get($"https://badges.twitch.tv/v1/badges/channels/{TwitchIRCClient.ChannelIds[Config.Instance.TwitchChannel]}/display"))
            {
                yield return web.SendWebRequest();
                if (web.isNetworkError || web.isHttpError)
                {
                    Plugin.Log($"An error occured when requesting twitch channel badge listing, Message: \"{web.error}\"");
                    yield break;
                }
                if (web.downloadHandler.text.Length == 0) yield break;

                JSONNode json = SimpleJSON.JSON.Parse(web.downloadHandler.text);
                if (json["badge_sets"]["subscriber"].IsObject)
                {
                    string name = "subscriber";
                    JSONObject badge = json["badge_sets"]["subscriber"]["versions"].AsObject;
                    foreach (KeyValuePair<string, SimpleJSON.JSONNode> version in badge)
                    {
                        string versionID = version.Key;
                        JSONObject versionObject = version.Value.AsObject;
                        string url = versionObject["image_url_4x"];
                        string index = url.Substring(url.IndexOf("/v1/") + 4).Replace("/3", "");
                        string finalName = $"{name}{versionID}";
                        if(!TwitchBadgeIDs.TryAdd(finalName, index) && name == "subscriber")
                        {
                            // Overwrite the affiliate sub badges if the channel has any custom ones
                            if (TwitchBadgeIDs.TryGetValue(finalName, out var existing))
                            {
                                TwitchBadgeIDs[finalName] = index;
                                Plugin.Log("Replaced default sub icon!");
                            }
                        }
                        emotesCached++;
                    }
                }
                Plugin.Log($"Web request completed, {emotesCached.ToString()} twitch channel badges now cached!");
            }
        }

        public static IEnumerator GetBTTVGlobalEmotes()
        {
            Plugin.Log("Downloading BTTV global emote listing");
            int emotesCached = 0;
            using (var web = UnityWebRequest.Get("https://api.betterttv.net/2/emotes"))
            {
                yield return web.SendWebRequest();
                if (web.isNetworkError || web.isHttpError)
                {
                    Plugin.Log($"An error occured when requesting BTTV global emote listing, Message: \"{web.error}\"");
                    yield break;
                }
                if (web.downloadHandler.text.Length == 0) yield break;
                JSONNode json = SimpleJSON.JSON.Parse(web.downloadHandler.text);
                if (json["status"].AsInt == 200)
                {
                    JSONArray emotes = json["emotes"].AsArray;
                    foreach (SimpleJSON.JSONObject o in emotes)
                    {
                        if (o["channel"] == null)
                        {
                            if (o["imageType"] != "gif")
                                BTTVEmoteIDs.TryAdd(o["code"], o["id"]);
                            else
                                SpriteDownloader.BTTVAnimatedEmoteIDs.TryAdd(o["code"], o["id"]);
                            emotesCached++;
                        }
                    }
                }
                Plugin.Log($"Web request completed, {emotesCached.ToString()} BTTV global emotes now cached!");
            }
        }

        public static IEnumerator GetBTTVChannelEmotes()
        {
            Plugin.Log($"Downloading BTTV emotes for channel {Config.Instance.TwitchChannel}");
            int emotesCached = 0;
            using (var web = UnityWebRequest.Get($"https://api.betterttv.net/2/channels/{Config.Instance.TwitchChannel}"))
            {
                yield return web.SendWebRequest();
                if (web.isNetworkError || web.isHttpError)
                {
                    Plugin.Log($"An error occured when requesting BTTV channel emote listing, Message: \"{web.error}\"");
                    yield break;
                }
                if (web.downloadHandler.text.Length == 0) yield break;
                JSONNode json = SimpleJSON.JSON.Parse(web.downloadHandler.text);
                if (json["status"].AsInt == 200)
                {
                    JSONArray emotes = json["emotes"].AsArray;
                    foreach (SimpleJSON.JSONObject o in emotes)
                    {
                        Plugin.Log($"BTTV Channel Emote: {o["code"]}");
                        if (o["imageType"] != "gif")
                            BTTVEmoteIDs.TryAdd(o["code"], o["id"]);
                        else
                            SpriteDownloader.BTTVAnimatedEmoteIDs.TryAdd(o["code"], o["id"]);
                        emotesCached++;
                    }
                }
                Plugin.Log($"Web request completed, {emotesCached.ToString()} BTTV channel emotes now cached!");
            }
        }

        public static IEnumerator GetFFZGlobalEmotes()
        {
            Plugin.Log("Downloading FFZ global emote listing");
            int emotesCached = 0;
            using (var web = UnityWebRequest.Get("https://api.frankerfacez.com/v1/set/global"))
            {
                yield return web.SendWebRequest();
                if (web.isNetworkError || web.isHttpError)
                {
                    Plugin.Log($"An error occured when requesting FFZ global emote listing, Message: \"{web.error}\"");
                    yield break;
                }
                if (web.downloadHandler.text.Length == 0) yield break;

                JSONNode json = SimpleJSON.JSON.Parse(web.downloadHandler.text);
                if (json["sets"].IsObject)
                {
                    JSONArray emotes = json["sets"]["3"]["emoticons"].AsArray;
                    foreach (SimpleJSON.JSONObject o in emotes)
                    {
                        JSONObject urls = o["urls"].AsObject;
                        string url = urls[urls.Count - 1];
                        string index = url.Substring(url.IndexOf(".com/") + 5);
                        FFZEmoteIDs.TryAdd(o["name"], index);
                        emotesCached++;
                    }
                }
                Plugin.Log($"Web request completed, {emotesCached.ToString()} FFZ global emotes now cached!");
            }
        }
        public static IEnumerator GetFFZChannelEmotes()
        {
            Plugin.Log($"Downloading FFZ emotes for channel {Config.Instance.TwitchChannel}");
            int emotesCached = 0;
            using (var web = UnityWebRequest.Get($"https://api.frankerfacez.com/v1/room/{Config.Instance.TwitchChannel}"))
            {
                yield return web.SendWebRequest();
                if (web.isNetworkError || web.isHttpError)
                {
                    Plugin.Log($"An error occured when requesting FFZ channel emote listing, Message: \"{web.error}\"");
                    yield break;
                }
                if (web.downloadHandler.text.Length == 0) yield break;

                JSONNode json = SimpleJSON.JSON.Parse(web.downloadHandler.text);
                if (json["sets"].IsObject)
                {
                    JSONArray emotes = json["sets"][json["room"]["set"].ToString()]["emoticons"].AsArray;
                    foreach (SimpleJSON.JSONObject o in emotes)
                    {
                        JSONObject urls = o["urls"].AsObject;
                        string url = urls[urls.Count - 1];
                        string index = url.Substring(url.IndexOf(".com/") + 5);
                        FFZEmoteIDs.TryAdd(o["name"], index);
                        emotesCached++;
                    }
                }
                Plugin.Log($"Web request completed, {emotesCached.ToString()} FFZ channel emotes now cached!");
            }
        }
    };
}
