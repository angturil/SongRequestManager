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
using System.Text.RegularExpressions;
using CustomUI.Utilities;

namespace EnhancedTwitchChat.Textures
{
    public class CachedTextureData
    {
        public Texture2D texture = null;
        public Rect[] animationInfo = null;
        public float width;
        public float height;
        public int animIndex = -1;
        public float delay = -1f;
        public CachedTextureData(Texture2D texture, float width, float height)
        {
            this.texture = texture;
            this.width = width;
            this.height = height;
        }
        public CachedTextureData(Rect[] animationInfo)
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
        Emoji,
        Cheermote
    };

    public class ImageTypeNames
    {
        private static string[] Names = new string[] { "None", "Twitch", "BetterTwitchTV", "BetterTwitchTV", "FrankerFaceZ", "Badges", "Emojis", "Cheermotes" };

        public static string Get(ImageType type)
        {
            return Names[(int)type];
        }
    };

    public class TextureDownloadInfo
    {
        public string textureIndex;
        public ImageType type;
        public string messageIndex;
        public TextureDownloadInfo(string index, ImageType type, string messageIndex)
        {
            this.textureIndex = index;
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

    public class CheermoteTier
    {
        public int minBits = 0;
        public string color = "";
        public bool canCheer = false;
    }

    public class Cheermote
    {
        public List<CheermoteTier> tiers = new List<CheermoteTier>();

        public string GetColor(int numBits)
        {
            for (int i = 1; i < tiers.Count; i++)
            {
                if (numBits < tiers[i].minBits)
                    return tiers[i - 1].color;
            }
            return tiers[0].color;
        }

        public string GetTier(int numBits)
        {
            for (int i = 1; i < tiers.Count; i++)
            {
                if(numBits < tiers[i].minBits)
                    return tiers[i-1].minBits.ToString();
            }
            return tiers[0].minBits.ToString();
        }
    }
    
    public class TextureDownloader : MonoBehaviour
    {
        public static ConcurrentDictionary<string, string> BTTVEmoteIDs = new ConcurrentDictionary<string, string>();
        public static ConcurrentDictionary<string, string> FFZEmoteIDs = new ConcurrentDictionary<string, string>();
        public static ConcurrentDictionary<string, string> TwitchBadgeIDs = new ConcurrentDictionary<string, string>();
        public static ConcurrentDictionary<string, string> BTTVAnimatedEmoteIDs = new ConcurrentDictionary<string, string>();
        public static ConcurrentDictionary<string, Cheermote> TwitchCheermoteIDs = new ConcurrentDictionary<string, Cheermote>();

        public static ConcurrentDictionary<string, CachedTextureData> CachedTextures = new ConcurrentDictionary<string, CachedTextureData>();
        public static ConcurrentStack<TextureSaveInfo> ImageSaveQueue = new ConcurrentStack<TextureSaveInfo>();
        private ConcurrentStack<TextureDownloadInfo> _downloadQueue = new ConcurrentStack<TextureDownloadInfo>();
        private int _numDownloading = 0;
        private int _waitForFrames = 0;
        public static TextureDownloader Instance = null;
        
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
            StartCoroutine(GetCheermotes());
        }

        public void Update()
        {
            if (_waitForFrames > 0)
            {
                _waitForFrames--;
                return;
            }

            // Skip this frame if our fps is low
            //float fps = 1.0f / Time.deltaTime;
            //if (!Plugin.Instance.IsAtMainMenu && fps < XRDevice.refreshRate - 5)
            //    return;

            // Download any emotes we need cached for one of our messages
            if (_downloadQueue.Count > 0 && _numDownloading < 2)
            {
                if (_downloadQueue.TryPop(out var imageDownloadInfo))
                {
                    switch (imageDownloadInfo.type)
                    {
                        case ImageType.Twitch:
                            StartCoroutine(Download($"https://static-cdn.jtvnw.net/emoticons/v1/{imageDownloadInfo.textureIndex.Substring(1)}/3.0", imageDownloadInfo));
                            break;
                        case ImageType.BTTV:
                            StartCoroutine(Download($"https://cdn.betterttv.net/emote/{imageDownloadInfo.textureIndex.Substring(1)}/3x", imageDownloadInfo));
                            break;
                        case ImageType.BTTV_Animated:
                            StartCoroutine(Download($"https://cdn.betterttv.net/emote/{imageDownloadInfo.textureIndex.Substring(2)}/3x", imageDownloadInfo));
                            break;
                        case ImageType.FFZ:
                            StartCoroutine(Download($"https://cdn.frankerfacez.com/{imageDownloadInfo.textureIndex.Substring(1)}", imageDownloadInfo));
                            break;
                        case ImageType.Badge:
                            StartCoroutine(Download($"https://static-cdn.jtvnw.net/badges/v1/{imageDownloadInfo.textureIndex}/3", imageDownloadInfo));
                            break;
                        case ImageType.Cheermote:
                            Match match = Utilities.cheermoteRegex.Match(imageDownloadInfo.textureIndex);
                            StartCoroutine(Download($"https://d3aqoihi2n8ty8.cloudfront.net/actions/{(match.Groups["Prefix"].Value)}/dark/animated/{(match.Groups["Value"].Value)}/4.gif", imageDownloadInfo));
                            break;
                        case ImageType.Emoji:
                            StartCoroutine(Download(string.Empty, imageDownloadInfo));
                            break;
                    }
                    _numDownloading++;
                }
            }
        }

        public void Queue(TextureDownloadInfo emote)
        {
            _downloadQueue.Push(emote);
        }

        public static IEnumerator Download(string imagePath, TextureDownloadInfo imageDownloadInfo, bool isRetry = false)
        {
            if (!CachedTextures.ContainsKey(imageDownloadInfo.textureIndex))
            {
                Texture2D texture = null;
                if (imageDownloadInfo.type != ImageType.Emoji)
                {
                    string origImagePath = imagePath;

                    string imageCachePath = "Cache\\Images";
                    string oldSpriteCachePath = "Cache\\Sprites";

                    // Migrate our cached sprites/images into our renamed "Images" folder
                    if (Directory.Exists(oldSpriteCachePath))
                    {
                        if (!Directory.Exists(imageCachePath))
                            Directory.Move(oldSpriteCachePath, imageCachePath);
                        else
                            Directory.Delete(oldSpriteCachePath);
                    }
                    
                    if (!Directory.Exists(imageCachePath))
                        Directory.CreateDirectory(imageCachePath);

                    string typePath = $"{imageCachePath}\\{ImageTypeNames.Get(imageDownloadInfo.type)}";
                    if (!Directory.Exists(typePath))
                        Directory.CreateDirectory(typePath);

                    bool localPathExists = false;
                    string localFilePath = $"{typePath}\\{imageDownloadInfo.textureIndex}";

                    if (File.Exists(localFilePath))
                    {
                        localPathExists = true;
                        imagePath = $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase).Replace("\\Plugins", "")}\\{localFilePath}";
                    }

                    using (var web = UnityWebRequestTexture.GetTexture(imagePath, true))
                    {
                        yield return web.SendWebRequest();
                        if (web.isNetworkError || web.isHttpError)
                        {
                            Plugin.Log($"An error occured when requesting emote {imageDownloadInfo.textureIndex}, Message: \"{web.error}\"");
                            CachedTextures.TryAdd(imageDownloadInfo.textureIndex, null);
                            Instance._numDownloading--;
                            yield break;
                        }
                        else if (imageDownloadInfo.type == ImageType.BTTV_Animated || imageDownloadInfo.type == ImageType.Cheermote)
                        {
                            CachedTextures.TryAdd(imageDownloadInfo.textureIndex, null);
                            yield return AnimationDecoder.Process(web.downloadHandler.data, ChatHandler.Instance.OverlayAnimatedImage, imageDownloadInfo);
                            if (!localPathExists)
                                ImageSaveQueue.Push(new TextureSaveInfo(localFilePath, web.downloadHandler.data));
                        }
                        else
                        {
                            texture = UIUtilities.LoadTextureRaw(web.downloadHandler.data);
                            if (texture)
                            {
                                if (!localPathExists)
                                    ImageSaveQueue.Push(new TextureSaveInfo(localFilePath, web.downloadHandler.data));
                            }
                        }
                    }
                }
                else
                    texture = UIUtilities.LoadTextureFromResources($"EnhancedTwitchChat.Resources.Emojis.{imageDownloadInfo.textureIndex.ToLower()}");

                if (texture)
                {
                    CachedTextures.TryAdd(imageDownloadInfo.textureIndex, new CachedTextureData(texture, texture.width, texture.height));
                    yield return null;
                    ChatHandler.Instance.OverlayImage(texture, imageDownloadInfo);
                }
            }
            Instance._numDownloading--;

            if (Plugin.Instance.IsAtMainMenu)
                Instance._waitForFrames = 5;
            else
                Instance._waitForFrames = 15;
        }

        public static IEnumerator GetCheermotes()
        {
            int emotesCached = 0;
            UnityWebRequest web = UnityWebRequest.Get("https://api.twitch.tv/kraken/bits/actions");
            web.SetRequestHeader("Accept", "application/vnd.twitchtv.v5+json");
            web.SetRequestHeader("Channel-ID", TwitchIRCClient.CurrentChannel);
            web.SetRequestHeader("Client-ID", "jg6ij5z8mf8jr8si22i5uq8tobnmde");
            yield return web.SendWebRequest();
            if (web.isNetworkError || web.isHttpError)
            {
                Plugin.Log($"An error occured when requesting twitch global badge listing, Message: \"{web.error}\"");
                yield break;
            }
            if (web.downloadHandler.text.Length == 0) yield break;

            JSONNode json = JSON.Parse(web.downloadHandler.text);
            if (json["actions"].IsArray)
            {
                foreach (JSONNode node in json["actions"].AsArray.Values)
                {
                    Cheermote cheermote = new Cheermote();
                    string prefix = node["prefix"].ToString().ToLower();
                    foreach (JSONNode tier in node["tiers"].Values)
                    {
                        CheermoteTier newTier = new CheermoteTier();
                        newTier.minBits = tier["min_bits"].AsInt;
                        newTier.color = tier["color"];
                        newTier.canCheer = tier["can_cheer"].AsBool;
                        cheermote.tiers.Add(newTier);
                    }
                    cheermote.tiers = cheermote.tiers.OrderBy(t => t.minBits).ToList();
                    TwitchCheermoteIDs.TryAdd(prefix.Substring(1, prefix.Length-2), cheermote);
                    //Plugin.Log($"Cheermote: {prefix}");
                    emotesCached++;
                }
            }
            Plugin.Log($"Web request completed, {emotesCached.ToString()} twitch cheermotes now cached!");
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
                            //Plugin.Log($"Badge: {name}{versionID}");
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
            while (TwitchIRCClient.ChannelIds[TwitchIRCClient.CurrentChannel] == String.Empty)
            {
                yield return null;
            }
            Plugin.Log($"Downloading twitch channel badge listing");
            int emotesCached = 0;
            using (var web = UnityWebRequest.Get($"https://badges.twitch.tv/v1/badges/channels/{TwitchIRCClient.ChannelIds[TwitchIRCClient.CurrentChannel]}/display"))
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
                                TextureDownloader.BTTVAnimatedEmoteIDs.TryAdd(o["code"], o["id"]);
                            emotesCached++;
                        }
                    }
                }
                Plugin.Log($"Web request completed, {emotesCached.ToString()} BTTV global emotes now cached!");
            }
        }

        public static IEnumerator GetBTTVChannelEmotes()
        {
            Plugin.Log($"Downloading BTTV emotes for channel {TwitchIRCClient.CurrentChannel}");
            int emotesCached = 0;
            using (var web = UnityWebRequest.Get($"https://api.betterttv.net/2/channels/{TwitchIRCClient.CurrentChannel}"))
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
                        if (o["imageType"] != "gif")
                            BTTVEmoteIDs.TryAdd(o["code"], o["id"]);
                        else
                            TextureDownloader.BTTVAnimatedEmoteIDs.TryAdd(o["code"], o["id"]);
                        emotesCached++;
                    }
                }
                Plugin.Log($"Web request completed, {emotesCached.ToString()} BTTV channel emotes now cached!");
            }

            yield return PreloadBTTVAnimatedEmotes();
        }
        
        public static IEnumerator PreloadBTTVAnimatedEmotes()
        {
            int count = 0;
            foreach (string emoteIndex in BTTVAnimatedEmoteIDs.Values)
            {
                if (!Plugin.Instance.IsAtMainMenu)
                    yield return new WaitUntil(() => Plugin.Instance.IsAtMainMenu);

                if (!CachedTextures.ContainsKey(emoteIndex))
                {
                    TextureDownloadInfo downloadInfo = new TextureDownloadInfo("AB" + emoteIndex, ImageType.BTTV_Animated, "!NOTSET!");
                    Plugin.Log($"Precaching {emoteIndex}");
                    Instance.Queue(downloadInfo);
                    count++;
                    yield return new WaitUntil(() => !Instance._downloadQueue.Contains(downloadInfo));
                }
            }
            Plugin.Log($"Precached {count.ToString()} animated emotes successfully!");
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
            Plugin.Log($"Downloading FFZ emotes for channel {TwitchIRCClient.CurrentChannel}");
            int emotesCached = 0;
            using (var web = UnityWebRequest.Get($"https://api.frankerfacez.com/v1/room/{TwitchIRCClient.CurrentChannel}"))
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
