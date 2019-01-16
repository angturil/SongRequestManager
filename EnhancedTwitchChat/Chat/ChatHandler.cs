using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using EnhancedTwitchChat.Textures;
using VRUIControls;
using UnityEngine.SceneManagement;
using System.Text;
using System.Collections.Concurrent;
using System.IO;
using EnhancedTwitchChat.Utils;
using EnhancedTwitchChat.Chat;
using EnhancedTwitchChat.UI;
using AsyncTwitch;
using UnityEngine.XR;
using CustomUI.Utilities;

namespace EnhancedTwitchChat
{
    public class ChatHandler : MonoBehaviour
    {
        public static ChatHandler Instance = null;

        public bool displayStatusMessage = false;
        public Image lockButtonImage;
        public Image background;
        public GameObject lockButtonPrimitive;
        public GameObject chatMoverPrimitive;
        public bool initialized = false;
        public ObjectPool<CustomImage> imagePool;

        private Canvas _twitchChatCanvas = null;
        private List<CustomText> _chatMessages = new List<CustomText>();
        private Transform _chatMoverCube;
        private Transform _lockButtonSphere;
        private float _currentBackgroundHeight;
        private RectTransform _canvasRectTransform;
        private Sprite _lockedSprite;
        private Sprite _unlockedSprite;
        private bool _messageRendering = false;
        private int _waitForFrames = 0;
        private bool _configChanged = false;
        private ConcurrentQueue<string> _timeoutQueue = new ConcurrentQueue<string>();
        private ChatMover _movePointer = null;
        private LockToggle _lockPointer = null;
        private string _lastFontName;
        private CustomText _testMessage = null;
        private readonly WaitUntil _delay = new WaitUntil(() => { return Instance._waitForFrames == 0; });

        public static void OnLoad()
        {
            if (Instance) return;
            new GameObject("EnhancedTwitchChatHandler").AddComponent<ChatHandler>();
        }

        public void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Startup the texture downloader and anim controller
            new GameObject("EnhancedTwitchChatTextureDownloader").AddComponent<TextureDownloader>();
            new GameObject("EnhancedTwitchChatAnimationController").AddComponent<AnimationController>();

            // Initialize the chats UI
            InitializeChatUI();

            // Subscribe to events
            Config.Instance.ConfigChangedEvent += PluginOnConfigChangedEvent;

            initialized = true;
            Plugin.Log("EnhancedTwitchChat initialized");
        }

        public void SceneManager_activeSceneChanged(Scene from, Scene to)
        {
            var _vrPointer = to.name == "GameCore" ? Resources.FindObjectsOfTypeAll<VRPointer>().Last() : Resources.FindObjectsOfTypeAll<VRPointer>().First();
            if (_vrPointer == null) return;

            if (_movePointer)
                Destroy(_movePointer);
            _movePointer = _vrPointer.gameObject.AddComponent<ChatMover>();
            _movePointer.Init(_chatMoverCube);

            if (_lockPointer)
                Destroy(_lockPointer);
            _lockPointer = _vrPointer.gameObject.AddComponent<LockToggle>();
            _lockPointer.Init(lockButtonImage, _lockButtonSphere);
            Plugin.Log($"ActiveSceneChanged! ({from.name} -> {to.name})");
        }
        
        private void PluginOnConfigChangedEvent(Config config)
        {
            _configChanged = true;
        }

        string lastChannel = "!NOTSET!";
        private void OnConfigChanged()
        {
            //if (lastChannel != String.Empty)
            //    TwitchConnection.Instance.PartRoom(lastChannel);
            Plugin.Log("OnConfigChanged");
            if (TwitchIRCClient.CurrentChannel != lastChannel)
            {
                if(TwitchIRCClient.CurrentChannel != String.Empty)
                    TwitchConnection.Instance.JoinRoom(TwitchIRCClient.CurrentChannel);
                TwitchIRCClient.ConnectionTime = DateTime.Now;
                displayStatusMessage = true;
            }
            lastChannel = TwitchIRCClient.CurrentChannel;
            
            if (Config.Instance.FontName != _lastFontName)
            {
                StartCoroutine(Drawing.Initialize(gameObject.transform));
                foreach (CustomText currentMessage in _chatMessages)
                {
                    Font f = currentMessage.font;
                    currentMessage.font = Drawing.LoadSystemFont(Config.Instance.FontName);
                    currentMessage.color = Config.Instance.TextColor;
                    Destroy(f);
                }
                _lastFontName = Config.Instance.FontName;
            }

            UpdateChatUI();
            _canvasRectTransform.localScale = new Vector3(0.012f * Config.Instance.ChatScale, 0.012f * Config.Instance.ChatScale, 0.012f * Config.Instance.ChatScale);
            _lockButtonSphere.localScale = new Vector3(0.15f * Config.Instance.ChatScale, 0.15f * Config.Instance.ChatScale, 0.001f * Config.Instance.ChatScale);
            background.color = Config.Instance.BackgroundColor;

            Plugin.Log($"Config updated!");
            _configChanged = false;
        }

        public void FixedUpdate()
        {
            if (Drawing.MaterialsCached)
            {
                // Wait a few seconds after we've connect to the chat, then send our welcome message
                if (displayStatusMessage && (TwitchIRCClient.CurrentChannelValid || (DateTime.Now - TwitchIRCClient.ConnectionTime).TotalSeconds >= 5))
                {
                    string msg;
                    if (TwitchConnection.IsConnected)
                    {
                        TextureDownloader.Instance.Init();

                        if (TwitchIRCClient.CurrentChannel == String.Empty)
                            msg = $"Welcome to Enhanced Twitch Chat! To continue, enter your Twitch channel name in <i>UserData\\EnhancedTwitchChat.ini</i>, which is located in your Beat Saber directory.";
                        else if (TwitchIRCClient.CurrentChannelValid)
                            msg = $"Success joining channel \"{TwitchIRCClient.CurrentChannel}\"";
                        else
                            msg = $"Failed to join channel \"{TwitchIRCClient.CurrentChannel}\". Please enter a valid Twitch channel name in <i>EnhancedTwitchChat.ini</i> or <i>AsyncTwitchConfig.json</i>, then try again.";
                    }
                    else
                        msg = "Failed to login to Twitch! Please check your login info in <i>UserData\\AsyncTwitchConfig.json</i>, then try again.\r\n\r\n<b>NOTE:</b> <i>You are not required to enter anything in AsyncTwitchConfig.json</i>! Enhanced Twitch Chat supports anonymous login; all you need to enter is your channel name! If you aren't using AsyncTwitch for anything else, it's safe to delete the AsyncTwitchConfig.json.";

                    TwitchMessage tmpMessage = new TwitchMessage();
                    tmpMessage.Author = new ChatUser();
                    tmpMessage.Author.Color = "#00000000";
                    tmpMessage.Author.DisplayName = String.Empty;

                    TwitchIRCClient.RenderQueue.Enqueue(new ChatMessage(msg, tmpMessage));

                    displayStatusMessage = false;
                }

                if (_configChanged)
                    OnConfigChanged();

                // Make sure to delete any purged messages right away
                if (_timeoutQueue.Count > 0 && _timeoutQueue.TryDequeue(out var userID))
                    PurgeChatMessagesInternal(userID);

                if (_waitForFrames > 0)
                {
                    _waitForFrames--;
                    return;
                }

                //// Wait try to display any new chat messages if our fps is tanking
                //float fps = 1.0f / Time.deltaTime;
                //if (!Plugin.Instance.IsAtMainMenu && fps < XRDevice.refreshRate - 5)
                //    return;

                // Display any messages that we've cached all the resources for and prepared for rendering
                if (TwitchIRCClient.RenderQueue.Count > 0 && !_messageRendering)
                {
                    if (TwitchIRCClient.RenderQueue.TryDequeue(out var messageToSend))
                        StartCoroutine(AddNewChatMessage(messageToSend.msg, messageToSend));
                }

                // Save images to file when we're at the main menu
                else if (Plugin.Instance.IsAtMainMenu && TextureDownloader.ImageSaveQueue.Count > 0 && TextureDownloader.ImageSaveQueue.TryDequeue(out var saveInfo))
                    File.WriteAllBytes(saveInfo.path,  saveInfo.data);
            }
        }

        public void LateUpdate()
        {
            if (Drawing.MaterialsCached)
            {
                _twitchChatCanvas.transform.eulerAngles = Config.Instance.ChatRotation;
                _twitchChatCanvas.transform.position = Config.Instance.ChatPosition;
                if (!Config.Instance.ReverseChatOrder) _twitchChatCanvas.transform.position = _twitchChatCanvas.transform.TransformPoint(new Vector3(0, _currentBackgroundHeight));

                _chatMoverCube.localScale = background.rectTransform.sizeDelta * (Config.Instance.ChatScale * 1.2f) / Drawing.pixelsPerUnit;
                _chatMoverCube.eulerAngles = Config.Instance.ChatRotation;
                _chatMoverCube.position = background.rectTransform.TransformPoint(background.rectTransform.rect.width / 2, _currentBackgroundHeight / 2, 0);

                Vector3[] LocalCorners = new Vector3[4];
                background.rectTransform.GetLocalCorners(LocalCorners);
                _lockButtonSphere.eulerAngles = Config.Instance.ChatRotation;
                lockButtonImage.rectTransform.eulerAngles = Config.Instance.ChatRotation;
                lockButtonImage.rectTransform.position = background.rectTransform.TransformPoint((Config.Instance.ReverseChatOrder ? LocalCorners[2] : LocalCorners[3]) - new Vector3(lockButtonImage.rectTransform.sizeDelta.x / 2, lockButtonImage.rectTransform.sizeDelta.y / 2));
                _lockButtonSphere.position = lockButtonImage.rectTransform.TransformPoint(new Vector3(_lockButtonSphere.transform.localScale.x / 2 * Drawing.pixelsPerUnit, _lockButtonSphere.transform.localScale.y / 2 * Drawing.pixelsPerUnit, -0.01f) / Config.Instance.ChatScale);
            }
        }

        private void InitializeChatUI()
        {
            // Precache a pool of images objects that will be used for displaying emotes/badges later on
            imagePool = new ObjectPool<CustomImage>(Config.Instance.MaxChatLines * 2,
                // OnAlloc
                ((CustomImage image) =>
                {
                    image.material = Drawing.noGlowMaterialUI;
                }),
                // OnFree
                ((CustomImage image) =>
                {
                    image.texture = null;
                    image.uvRect = image.origUV;
                    if (image.textureAnimator.enabled)
                    {
                        image.textureAnimator.CancelInvoke();
                        image.textureAnimator.enabled = false;
                    }
                    image.enabled = false;
                })
            );

            _lastFontName = Config.Instance.FontName;
            StartCoroutine(Drawing.Initialize(gameObject.transform));

            _lockedSprite = UIUtilities.LoadSpriteFromResources("EnhancedTwitchChat.Resources.LockedIcon.png");
            _lockedSprite.texture.wrapMode = TextureWrapMode.Clamp;
            _unlockedSprite = UIUtilities.LoadSpriteFromResources("EnhancedTwitchChat.Resources.UnlockedIcon.png");
            _unlockedSprite.texture.wrapMode = TextureWrapMode.Clamp;

            _twitchChatCanvas = gameObject.AddComponent<Canvas>();
            _twitchChatCanvas.renderMode = RenderMode.WorldSpace;
            var collider = gameObject.AddComponent<MeshCollider>();
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = Drawing.pixelsPerUnit;
            _canvasRectTransform = _twitchChatCanvas.GetComponent<RectTransform>();
            _canvasRectTransform.localScale = new Vector3(0.012f * Config.Instance.ChatScale, 0.012f * Config.Instance.ChatScale, 0.012f * Config.Instance.ChatScale);

            background = new GameObject("EnhancedTwitchChatBackground").AddComponent<Image>();
            background.rectTransform.SetParent(gameObject.transform, false);
            background.color = Config.Instance.BackgroundColor;
            background.rectTransform.pivot = new Vector2(0, 0);
            background.rectTransform.sizeDelta = new Vector2(Config.Instance.ChatWidth + Config.Instance.BackgroundPadding, 0);
            background.rectTransform.localPosition = new Vector3(0 - (Config.Instance.ChatWidth + Config.Instance.BackgroundPadding) / 2, 0, 0);

            var lockButtonGameObj = new GameObject("EnhancedTwitchChatLockButton");
            lockButtonImage = lockButtonGameObj.AddComponent<Image>();
            lockButtonImage.preserveAspect = true;
            lockButtonImage.rectTransform.sizeDelta = new Vector2(10, 10);
            lockButtonImage.rectTransform.SetParent(gameObject.transform, false);
            lockButtonImage.rectTransform.pivot = new Vector2(0, 0);
            lockButtonImage.color = Color.white.ColorWithAlpha(0.05f);
            lockButtonImage.sprite = Config.Instance.LockChatPosition ? _lockedSprite : _unlockedSprite;
            lockButtonGameObj.AddComponent<Shadow>();
            
            chatMoverPrimitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.DontDestroyOnLoad(chatMoverPrimitive);
            _chatMoverCube = chatMoverPrimitive.transform;

            lockButtonPrimitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.DontDestroyOnLoad(lockButtonPrimitive);
            _lockButtonSphere = lockButtonPrimitive.transform;
            _lockButtonSphere.localScale = new Vector3(0.15f * Config.Instance.ChatScale, 0.15f * Config.Instance.ChatScale, 0.001f);

            while (_chatMessages.Count < Config.Instance.MaxChatLines)
            {
                var currentMessage = Drawing.InitText("", Color.clear, Config.Instance.ChatScale, new Vector2(Config.Instance.ChatWidth, 1), new Vector3(0, 0, 0), new Quaternion(0, 0, 0, 0), gameObject.transform, TextAnchor.UpperLeft, false);
                if (!Config.Instance.ReverseChatOrder) _chatMessages.Add(currentMessage);
                else _chatMessages.Insert(0, currentMessage);
            }
            var go = new GameObject();
            DontDestroyOnLoad(go);
            _testMessage = Drawing.InitText("", Color.clear, Config.Instance.ChatScale, new Vector2(Config.Instance.ChatWidth, 1), new Vector3(0, 0, 0), new Quaternion(0, 0, 0, 0), go.transform, TextAnchor.UpperLeft, true);
            _testMessage.enabled = false;
        }
        
        private IEnumerator AddNewChatMessage(string msg, ChatMessage messageInfo)
        {
            _messageRendering = true;
            CustomText currentMessage = null;
            
            _testMessage.text = msg;
            _testMessage.cachedTextGenerator.Populate(msg, _testMessage.GetGenerationSettings(_testMessage.rectTransform.rect.size));
            yield return null;

            for(int i=0; i<_testMessage.cachedTextGenerator.lineCount; i++)
            {
                msg = _testMessage.text.Substring(_testMessage.cachedTextGenerator.lines[i].startCharIdx);
                if(i < _testMessage.cachedTextGenerator.lineCount-1)
                    msg = msg.Substring(0, _testMessage.cachedTextGenerator.lines[i + 1].startCharIdx - _testMessage.cachedTextGenerator.lines[i].startCharIdx);
                
                // Italicize action messages and make the whole message the color of the users name
                if (messageInfo.isActionMessage)
                    msg = $"<i><color={messageInfo.twitchMessage.Author.Color}>{msg}</color></i>";

                if (!Config.Instance.ReverseChatOrder)
                {
                    currentMessage = _chatMessages.First();
                    _chatMessages.RemoveAt(0);
                    _chatMessages.Add(currentMessage);
                }
                else
                {
                    currentMessage = _chatMessages.Last();
                    _chatMessages.Remove(currentMessage);
                    _chatMessages.Insert(0, currentMessage);
                }
                currentMessage.hasRendered = false;
                currentMessage.text = msg;
                currentMessage.messageInfo = messageInfo;
                currentMessage.material = Drawing.noGlowMaterialUI;
                currentMessage.color = Config.Instance.TextColor;

                FreeImages(currentMessage);
                UpdateChatUI();
                yield return null;
                
                foreach (BadgeInfo b in messageInfo.parsedBadges)
                    Drawing.OverlayImage(currentMessage, b);

                foreach (EmoteInfo e in messageInfo.parsedEmotes)
                    Drawing.OverlayImage(currentMessage, e);
                
                currentMessage.hasRendered = true;

                _waitForFrames = 5;
                yield return _delay;
            }
            _testMessage.text = "";

            _messageRendering = false;
        }

        public void OverlayImage(Texture2D texture, TextureDownloadInfo imageDownloadInfo)
        {
            try
            {
                string textureIndex = imageDownloadInfo.textureIndex;
                string messageIndex = imageDownloadInfo.messageIndex;
                foreach (CustomText currentMessage in _chatMessages)
                {
                    if (currentMessage.messageInfo == null || !currentMessage.hasRendered) continue;

                    if (!textureIndex.StartsWith("AB"))
                    {
                        foreach (EmoteInfo e in currentMessage.messageInfo.parsedEmotes)
                        {
                            if (e.textureIndex == textureIndex)
                                Drawing.OverlayImage(currentMessage, e);
                        }

                        foreach (BadgeInfo b in currentMessage.messageInfo.parsedBadges)
                        {
                            if (b.textureIndex == textureIndex)
                                Drawing.OverlayImage(currentMessage, b);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log($"Exception when overlaying emote! {e.ToString()}");
            }
        }
        
        public void OverlayAnimatedImage(Texture2D texture, Rect[] uvs, float delay, TextureDownloadInfo imageDownloadInfo)
        {
            try
            {
                string textureIndex = imageDownloadInfo.textureIndex;
                string messageIndex = imageDownloadInfo.messageIndex;
                if (TextureDownloader.CachedTextures.ContainsKey(textureIndex))
                {
                    // If the animated image already exists, check if its only a single frame and replace it with the full animation if so
                    var animationInfo = TextureDownloader.CachedTextures[textureIndex]?.animInfo;
                    if (animationInfo != null && animationInfo.uvs.Length == 1)
                    {
                        foreach (CustomText currentMessage in _chatMessages)
                        {
                            for (int i = currentMessage.emoteRenderers.Count - 1; i >= 0; i--)
                            {
                                CustomImage img = currentMessage.emoteRenderers[i];
                                if (img.textureIndex == textureIndex)
                                {
                                    imagePool.Free(img);
                                    currentMessage.emoteRenderers.RemoveAt(i);
                                }
                            }
                        }
                    }
                }

                // Setup our CachedTextureData and CachedAnimationData, registering the animation if there is more than one uv in the array
                TextureDownloader.CachedTextures[textureIndex] = new CachedTextureData(texture, uvs[0].width, uvs[0].height);
                TextureDownloader.CachedTextures[textureIndex].animInfo = new CachedAnimationData(uvs.Length > 1 ? AnimationController.Instance.Register(textureIndex, uvs.Length, delay) : 0, uvs, delay);

                foreach (CustomText currentMessage in _chatMessages)
                {
                    if (currentMessage.messageInfo == null || !currentMessage.hasRendered) continue;
                    
                    foreach (EmoteInfo e in currentMessage.messageInfo.parsedEmotes)
                    {
                        if (e.textureIndex == textureIndex)
                            Drawing.OverlayImage(currentMessage, e);
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log($"Exception when overlaying animated emote! {e.ToString()}");
            }
        }

        private void PurgeChatMessagesInternal(string userID)
        {
            bool purged = false;
            foreach (CustomText currentMessage in _chatMessages)
            {
                if (currentMessage.messageInfo == null) continue;

                if (currentMessage.messageInfo.twitchMessage.Author.UserID == userID)
                {
                    string userName = $"<color={currentMessage.messageInfo.twitchMessage.Author.Color}><b>{currentMessage.messageInfo.twitchMessage.Author.DisplayName}</b></color>:";
                    if (currentMessage.text.Contains(userName))
                        currentMessage.text = $"{userName} <message deleted>";
                    else
                        currentMessage.text = "";

                    FreeImages(currentMessage);
                    purged = true;
                }
            }
            if(purged)
                UpdateChatUI();
        }

        public void PurgeMessagesFromUser(string userID)
        {
            _timeoutQueue.Enqueue(userID);
        }

        private void FreeImages(CustomText currentMessage)
        {
            if (currentMessage.emoteRenderers.Count > 0)
            {
                foreach (CustomImage image in currentMessage.emoteRenderers)
                    imagePool.Free(image);

                currentMessage.emoteRenderers.Clear();
            }
        }

        private void UpdateChatUI()
        {
            if (_chatMessages.Count > 0)
            {
                // Update the position of each text elem (which also moves the emotes since they are children of the text)
                float currentYValue = 0;

                float initialYValue = currentYValue;
                for (int i = 0; i < _chatMessages.Count(); i++)
                {
                    if (_chatMessages[i].text != "")
                    {
                        _chatMessages[i].transform.localPosition = new Vector3(-Config.Instance.ChatWidth / 2, currentYValue, 0);
                        currentYValue -= (_chatMessages[i].preferredHeight + (i < _chatMessages.Count() - 1 ? Config.Instance.MessageSpacing + 1.5f : 0));
                    }
                }
                _currentBackgroundHeight = (initialYValue - currentYValue) + Config.Instance.BackgroundPadding * 2;
                background.rectTransform.sizeDelta = new Vector2(Config.Instance.ChatWidth + Config.Instance.BackgroundPadding * 2, _currentBackgroundHeight);
                background.rectTransform.position = _twitchChatCanvas.transform.TransformPoint(new Vector3(-Config.Instance.ChatWidth / 2 - Config.Instance.BackgroundPadding, (initialYValue - _currentBackgroundHeight + Config.Instance.BackgroundPadding), 0));
            }
        }

        public void UpdateLockButton()
        {
            lockButtonImage.sprite = Config.Instance.LockChatPosition ? _lockedSprite : _unlockedSprite;
        }
    };
}
