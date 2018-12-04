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
    
    public class SpriteDownloader : MonoBehaviour
    {
        public static ConcurrentDictionary<string, string> BTTVEmoteIDs = new ConcurrentDictionary<string, string>();
        public static ConcurrentDictionary<string, string> FFZEmoteIDs = new ConcurrentDictionary<string, string>();
        public static ConcurrentDictionary<string, string> TwitchBadgeIDs = new ConcurrentDictionary<string, string>();
        public static ConcurrentDictionary<string, string> BTTVAnimatedEmoteIDs = new ConcurrentDictionary<string, string>();
        public static ConcurrentDictionary<string, Cheermote> TwitchCheermoteIDs = new ConcurrentDictionary<string, Cheermote>();

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
            StartCoroutine(GetCheermotes());
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
                        case ImageType.Cheermote:
                            Match match = Utilities.cheermoteRegex.Match(spriteDownloadInfo.index);
                            StartCoroutine(Download($"https://d3aqoihi2n8ty8.cloudfront.net/actions/{(match.Groups["Prefix"].Value)}/dark/animated/{(match.Groups["Value"].Value)}/4.gif", spriteDownloadInfo));
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
                Sprite sprite = null;
                if (spriteDownloadInfo.type != ImageType.Emoji)
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
                    }

                    using (var web = UnityWebRequestTexture.GetTexture(spritePath, true))
                    {
                        yield return web.SendWebRequest();
                        if (web.isNetworkError || web.isHttpError)
                        {
                            Plugin.Log($"An error occured when requesting emote {spriteDownloadInfo.index}, Message: \"{web.error}\"");
                            CachedSprites.TryAdd(spriteDownloadInfo.index, null);
                            Instance._numDownloading--;
                            yield break;
                        }
                        else if (spriteDownloadInfo.type == ImageType.BTTV_Animated || spriteDownloadInfo.type == ImageType.Cheermote)
                        {
                            CachedSprites.TryAdd(spriteDownloadInfo.index, null);
                            yield return AnimatedSpriteDecoder.Process(web.downloadHandler.data, ChatHandler.Instance.OverlayAnimatedEmote, spriteDownloadInfo);
                            if (!localPathExists)
                                SpriteSaveQueue.Push(new TextureSaveInfo(localFilePath, web.downloadHandler.data));
                        }
                        else
                        {
                            sprite = UIUtilities.LoadSpriteRaw(web.downloadHandler.data);
                            if (sprite)
                            {
                                if (!localPathExists)
                                    SpriteSaveQueue.Push(new TextureSaveInfo(localFilePath, web.downloadHandler.data));
                            }
                        }
                    }
                }
                else
                    sprite = UIUtilities.LoadSpriteFromResources($"EnhancedTwitchChat.Resources.Emojis.{spriteDownloadInfo.index.ToLower()}");

                if (sprite)
                {
                    CachedSprites.TryAdd(spriteDownloadInfo.index, new CachedSpriteData(sprite));
                    yield return null;
                    ChatHandler.Instance.OverlaySprite(sprite, spriteDownloadInfo);
                }
            }
            Instance._numDownloading--;
        }

        public static IEnumerator GetCheermotes()
        {
            int emotesCached = 0;
            UnityWebRequest web = UnityWebRequest.Get("https://api.twitch.tv/kraken/bits/actions");
            web.SetRequestHeader("Accept", "application/vnd.twitchtv.v5+json");
            web.SetRequestHeader("Channel-ID", Config.Instance.TwitchChannel);
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
