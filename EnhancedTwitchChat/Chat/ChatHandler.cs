using CustomUI.Utilities;
using EnhancedTwitchChat.Chat;
using EnhancedTwitchChat.Textures;
using EnhancedTwitchChat.UI;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRUIControls;
using EnhancedTwitchChat.Config;
#if REQUEST_BOT
using EnhancedTwitchIntegration.Bot;
#endif

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
        private Queue<CustomText> _chatMessages = new Queue<CustomText>();
        private Transform _chatMoverCube;
        private Transform _lockButtonSphere;
        private float _currentBackgroundHeight;
        private RectTransform _canvasRectTransform;
        private Sprite _lockedSprite;
        private Sprite _unlockedSprite;
        private bool _messageRendering = false;
        private int _waitForFrames = 0;
        private bool _configChanged = false;
        private ConcurrentQueue<KeyValuePair<string, bool>> _timeoutQueue = new ConcurrentQueue<KeyValuePair<string, bool>>();
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
            ImageDownloader.OnLoad();
            AnimationController.OnLoad();

            // Initialize the chats UI
            InitializeChatUI();

            // Subscribe to events
            ChatConfig.Instance.ConfigChangedEvent += ChatConfigChanged;

            initialized = true;
            Plugin.Log("EnhancedTwitchChat initialized");
        }

        public void SceneManager_activeSceneChanged(Scene from, Scene to)
        {
            var vrPointers = Resources.FindObjectsOfTypeAll<VRPointer>();
            if (vrPointers.Count() == 0)
            {
                Plugin.Log("Failed to get VRPointer!");
                return;
            }
            
            var _vrPointer = to.name != "GameCore" ? vrPointers.First() : vrPointers.Last();
            if (_movePointer)
                Destroy(_movePointer);
            _movePointer = _vrPointer.gameObject.AddComponent<ChatMover>();
            _movePointer.Init(_chatMoverCube);

            if (_lockPointer)
                Destroy(_lockPointer);
            _lockPointer = _vrPointer.gameObject.AddComponent<LockToggle>();
            _lockPointer.Init(lockButtonImage, _lockButtonSphere);
            Plugin.Log($"{from.name} -> {to.name}");
        }

        private void ChatConfigChanged(ChatConfig config)
        {
            _configChanged = true;
        }

        private void OnConfigChanged()
        {
            _configChanged = false;
            if (ChatConfig.Instance.FontName != _lastFontName)
            {
                StartCoroutine(Drawing.Initialize(gameObject.transform));
                foreach (CustomText currentMessage in _chatMessages)
                {
                    Font f = currentMessage.font;
                    currentMessage.font = Drawing.LoadSystemFont(ChatConfig.Instance.FontName);
                    currentMessage.color = ChatConfig.Instance.TextColor;
                    Destroy(f);
                }
                _lastFontName = ChatConfig.Instance.FontName;
            }

            UpdateChatUI();
            _canvasRectTransform.localScale = new Vector3(0.012f * ChatConfig.Instance.ChatScale, 0.012f * ChatConfig.Instance.ChatScale, 0.012f * ChatConfig.Instance.ChatScale);
            _lockButtonSphere.localScale = new Vector3(0.15f * ChatConfig.Instance.ChatScale, 0.15f * ChatConfig.Instance.ChatScale, 0.001f * ChatConfig.Instance.ChatScale);
            background.color = ChatConfig.Instance.BackgroundColor;
        }

        public void FixedUpdate()
        {
            if (Drawing.MaterialsCached)
            {
                // Wait a few seconds after we've connect to the chat, then send our welcome message
                if (displayStatusMessage && (TwitchWebSocketClient.IsChannelValid || (DateTime.Now - TwitchWebSocketClient.ConnectionTime).TotalSeconds >= 5))
                {
                    string msg;
                    if (TwitchWebSocketClient.Initialized && TwitchWebSocketClient.LoggedIn)
                    {
                        ImageDownloader.Instance.Init();

                        if (TwitchLoginConfig.Instance.TwitchChannelName == String.Empty)
                            msg = $"Welcome to Enhanced Twitch Chat! To continue, enter your Twitch channel name in the Enhanced Twitch Chat settings submenu, or manually in TwitchLoginInfo.ini, which is located in your Beat Saber directory.";
                        else if (TwitchWebSocketClient.IsChannelValid)
                            msg = $"Success joining channel \"{TwitchLoginConfig.Instance.TwitchChannelName}\"";
                        else
                            msg = $"Failed to join channel \"{TwitchLoginConfig.Instance.TwitchChannelName}\". Please enter a valid Twitch channel name in the Enhanced Twitch Chat settings submenu, or manually in TwitchLoginInfo.ini, then try again.";
                    }
                    else
                        msg = "Failed to login to Twitch! Please check your login info in UserData\\EnhancedTwitchChat\\TwitchLoginInfo.ini, then try again.";
                    
                    TwitchWebSocketClient.RenderQueue.Enqueue(new ChatMessage(msg, new TwitchMessage()));

                    displayStatusMessage = false;
                }

                if (_configChanged)
                    OnConfigChanged();

                // Make sure to delete any purged messages right away
                if (_timeoutQueue.Count > 0 && _timeoutQueue.TryDequeue(out var id))
                    PurgeChatMessagesInternal(id);

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
                if (TwitchWebSocketClient.RenderQueue.Count > 0 && !_messageRendering)
                {
                    if (TwitchWebSocketClient.RenderQueue.TryDequeue(out var messageToSend))
                    {
                        if (ChatConfig.Instance.FilterBroadcasterMessages && messageToSend.twitchMessage.user.isBroadcaster)
                            return;
                        if (ChatConfig.Instance.FilterCommandMessages && messageToSend.twitchMessage.message.StartsWith("!"))
                            return;

                        if (ChatConfig.Instance.FilterUserlistMessages)
                        {
                            if (Plugin.Instance.RequestBotInstalled)
                            {
                                if (IsExcludedUser(messageToSend.twitchMessage.user)) return;
                            }
                        }
                        StartCoroutine(AddNewChatMessage(messageToSend.msg, messageToSend));
                    }

                }

                // Save images to file when we're at the main menu
                else if (Plugin.Instance.IsAtMainMenu && ImageDownloader.ImageSaveQueue.Count > 0 && ImageDownloader.ImageSaveQueue.TryDequeue(out var saveInfo))
                    File.WriteAllBytes(saveInfo.path, saveInfo.data);
            }
        }

        private bool IsExcludedUser(TwitchUser user)
        {
#if REQUEST_BOT
            string excludefilename = "chatexclude.users";
            return RequestBot.Instance && RequestBot.listcollection.contains(ref excludefilename, user.displayName.ToLower(), RequestBot.ListFlags.Uncached);
#endif
            return false;
        }

        public void LateUpdate()
        {
            if (Drawing.MaterialsCached)
            {
                _twitchChatCanvas.transform.eulerAngles = ChatConfig.Instance.ChatRotation;
                _twitchChatCanvas.transform.position = ChatConfig.Instance.ChatPosition;
                if (!ChatConfig.Instance.ReverseChatOrder) _twitchChatCanvas.transform.position = _twitchChatCanvas.transform.TransformPoint(new Vector3(0, _currentBackgroundHeight));

                _chatMoverCube.localScale = background.rectTransform.sizeDelta * (ChatConfig.Instance.ChatScale * 1.2f) / Drawing.pixelsPerUnit;
                _chatMoverCube.eulerAngles = ChatConfig.Instance.ChatRotation;
                _chatMoverCube.position = background.rectTransform.TransformPoint(background.rectTransform.rect.width / 2, _currentBackgroundHeight / 2, 0);

                Vector3[] LocalCorners = new Vector3[4];
                background.rectTransform.GetLocalCorners(LocalCorners);
                _lockButtonSphere.eulerAngles = ChatConfig.Instance.ChatRotation;
                lockButtonImage.rectTransform.eulerAngles = ChatConfig.Instance.ChatRotation;
                lockButtonImage.rectTransform.position = background.rectTransform.TransformPoint((ChatConfig.Instance.ReverseChatOrder ? LocalCorners[2] : LocalCorners[3]) - new Vector3(lockButtonImage.rectTransform.sizeDelta.x / 2, lockButtonImage.rectTransform.sizeDelta.y / 2));
                _lockButtonSphere.position = lockButtonImage.rectTransform.TransformPoint(new Vector3(lockButtonImage.preferredWidth / Drawing.pixelsPerUnit, lockButtonImage.preferredHeight / Drawing.pixelsPerUnit, 0));
            }
        }

        private void InitializeChatUI()
        {
            // Precache a pool of images objects that will be used for displaying emotes/badges later on
            imagePool = new ObjectPool<CustomImage>(0,
                // FirstAlloc
                null,
                // OnAlloc
                ((CustomImage image) =>
                {
                    image.shadow.enabled = false;
                }),
                // OnFree
                ((CustomImage image) =>
                {
                    image.material = null;
                    image.enabled = false;
                })
            );

            _lastFontName = ChatConfig.Instance.FontName;
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
            _canvasRectTransform.localScale = new Vector3(0.012f * ChatConfig.Instance.ChatScale, 0.012f * ChatConfig.Instance.ChatScale, 0.012f * ChatConfig.Instance.ChatScale);

            background = new GameObject("EnhancedTwitchChatBackground").AddComponent<Image>();
            background.rectTransform.SetParent(gameObject.transform, false);
            background.color = ChatConfig.Instance.BackgroundColor;
            background.rectTransform.pivot = new Vector2(0, 0);
            background.rectTransform.sizeDelta = new Vector2(ChatConfig.Instance.ChatWidth + ChatConfig.Instance.BackgroundPadding, 0);
            background.rectTransform.localPosition = new Vector3(0 - (ChatConfig.Instance.ChatWidth + ChatConfig.Instance.BackgroundPadding) / 2, 0, 0);

            var lockButtonGameObj = new GameObject("EnhancedTwitchChatLockButton");
            lockButtonImage = lockButtonGameObj.AddComponent<Image>();
            lockButtonImage.preserveAspect = true;
            lockButtonImage.rectTransform.sizeDelta = new Vector2(10, 10);
            lockButtonImage.rectTransform.SetParent(gameObject.transform, false);
            lockButtonImage.rectTransform.pivot = new Vector2(0, 0);
            lockButtonImage.color = Color.white.ColorWithAlpha(0.05f);
            lockButtonImage.sprite = ChatConfig.Instance.LockChatPosition ? _lockedSprite : _unlockedSprite;
            lockButtonGameObj.AddComponent<Shadow>();

            chatMoverPrimitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.DontDestroyOnLoad(chatMoverPrimitive);
            _chatMoverCube = chatMoverPrimitive.transform;

            lockButtonPrimitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.DontDestroyOnLoad(lockButtonPrimitive);
            _lockButtonSphere = lockButtonPrimitive.transform;
            _lockButtonSphere.localScale = new Vector3(0.15f * ChatConfig.Instance.ChatScale, 0.15f * ChatConfig.Instance.ChatScale, 0.001f);

            while (_chatMessages.Count < ChatConfig.Instance.MaxChatLines)
                _chatMessages.Enqueue(Drawing.InitText("", Color.clear, ChatConfig.Instance.ChatScale, new Vector2(ChatConfig.Instance.ChatWidth, 1), new Vector3(0, 0, 0), new Quaternion(0, 0, 0, 0), gameObject.transform, TextAnchor.UpperLeft, false));

            var go = new GameObject();
            DontDestroyOnLoad(go);
            _testMessage = Drawing.InitText("", Color.clear, ChatConfig.Instance.ChatScale, new Vector2(ChatConfig.Instance.ChatWidth, 1), new Vector3(0, 0, 0), new Quaternion(0, 0, 0, 0), go.transform, TextAnchor.UpperLeft, true);
            _testMessage.enabled = false;
        }

        private IEnumerator AddNewChatMessage(string msg, ChatMessage messageInfo)
        {
            _messageRendering = true;
            CustomText currentMessage = null;

            _testMessage.text = msg;
            _testMessage.cachedTextGenerator.Populate(msg, _testMessage.GetGenerationSettings(_testMessage.rectTransform.rect.size));
            yield return null;

            for (int i = 0; i < _testMessage.cachedTextGenerator.lineCount; i++)
            {
                int index = ChatConfig.Instance.ReverseChatOrder ? _testMessage.cachedTextGenerator.lineCount - 1 - i : i;
                msg = _testMessage.text.Substring(_testMessage.cachedTextGenerator.lines[index].startCharIdx);
                if (index < _testMessage.cachedTextGenerator.lineCount - 1)
                    msg = msg.Substring(0, _testMessage.cachedTextGenerator.lines[index + 1].startCharIdx - _testMessage.cachedTextGenerator.lines[index].startCharIdx);

                // Italicize action messages and make the whole message the color of the users name
                if (messageInfo.isActionMessage)
                    msg = $"<i><color={messageInfo.twitchMessage.user.color}>{msg}</color></i>";

                currentMessage = _chatMessages.Dequeue();
                currentMessage.hasRendered = false;
                currentMessage.text = msg;
                currentMessage.messageInfo = messageInfo;
                currentMessage.material = Drawing.noGlowMaterialUI;
                currentMessage.color = ChatConfig.Instance.TextColor;
                _chatMessages.Enqueue(currentMessage);

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

        public void OverlayImage(Sprite sprite, TextureDownloadInfo imageDownloadInfo)
        {
            try
            {
                string spriteIndex = imageDownloadInfo.spriteIndex;
                string messageIndex = imageDownloadInfo.messageIndex;
                foreach (CustomText currentMessage in _chatMessages)
                {
                    if (currentMessage.messageInfo == null || !currentMessage.hasRendered) continue;

                    if (!spriteIndex.StartsWith("AB"))
                    {
                        foreach (EmoteInfo e in currentMessage.messageInfo.parsedEmotes)
                        {
                            if (e.textureIndex == spriteIndex)
                                Drawing.OverlayImage(currentMessage, e);
                        }

                        foreach (BadgeInfo b in currentMessage.messageInfo.parsedBadges)
                        {
                            if (b.textureIndex == spriteIndex)
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
                string spriteIndex = imageDownloadInfo.spriteIndex;
                string messageIndex = imageDownloadInfo.messageIndex;
                if (ImageDownloader.CachedTextures.ContainsKey(spriteIndex))
                {
                    // If the animated image already exists, check if its only a single frame and replace it with the full animation if so
                    var animationInfo = ImageDownloader.CachedTextures[spriteIndex]?.animInfo;
                    if (animationInfo != null && animationInfo.uvs.Length == 1)
                    {
                        foreach (CustomText currentMessage in _chatMessages)
                        {
                            for (int i = currentMessage.emoteRenderers.Count - 1; i >= 0; i--)
                            {
                                CustomImage img = currentMessage.emoteRenderers[i];
                                if (img.spriteIndex == spriteIndex)
                                {
                                    imagePool.Free(img);
                                    currentMessage.emoteRenderers.RemoveAt(i);
                                }
                            }
                        }
                    }
                }

                // Setup our CachedTextureData and CachedAnimationData, registering the animation if there is more than one uv in the array
                ImageDownloader.CachedTextures[spriteIndex] = new CachedSpriteData(null, uvs[0].width, uvs[0].height);
                ImageDownloader.CachedTextures[spriteIndex].animInfo = new CachedAnimationData(uvs.Length > 1 ? AnimationController.Instance.Register(spriteIndex, uvs, delay) : 0, texture, uvs, delay);

                if (ChatConfig.Instance.DrawShadows)
                {
                    var _shadowMaterial = Instantiate(Drawing.CropMaterialColorMultiply);
                    _shadowMaterial.mainTexture = texture;
                    _shadowMaterial.SetVector("_CropFactors", new Vector4(uvs[0].x, uvs[0].y, uvs[0].width, uvs[0].height));
                    _shadowMaterial.SetColor("_Color", Color.black.ColorWithAlpha(0.2f));
                    _shadowMaterial.renderQueue = 3001;
                    ImageDownloader.CachedTextures[spriteIndex].animInfo.shadowMaterial = _shadowMaterial;
                }

                var _animMaterial = Instantiate(Drawing.CropMaterial);
                _animMaterial.mainTexture = texture;
                _animMaterial.SetVector("_CropFactors", new Vector4(uvs[0].x, uvs[0].y, uvs[0].width, uvs[0].height));
                ImageDownloader.CachedTextures[spriteIndex].animInfo.imageMaterial = _animMaterial;

                foreach (CustomText currentMessage in _chatMessages)
                {
                    if (currentMessage.messageInfo == null || !currentMessage.hasRendered) continue;

                    foreach (EmoteInfo e in currentMessage.messageInfo.parsedEmotes)
                    {
                        if (e.textureIndex == spriteIndex)
                            Drawing.OverlayImage(currentMessage, e);
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log($"Exception when overlaying animated emote! {e.ToString()}");
            }
        }

        private bool PurgeChatMessage(CustomText currentMessage)
        {
            string userName = $"<color={currentMessage.messageInfo.twitchMessage.user.color}><b>{currentMessage.messageInfo.twitchMessage.user.displayName}</b></color>:";
            if (currentMessage.text.Contains(userName))
                currentMessage.text = $"{userName} <message deleted>";
            else
                currentMessage.text = "";

            FreeImages(currentMessage);
            return true;
        }

        private void PurgeChatMessagesInternal(KeyValuePair<string, bool> messageInfo)
        {
            bool isUserId = messageInfo.Value;
            string id = messageInfo.Key;

            bool purged = false;
            foreach (CustomText currentMessage in _chatMessages)
            {
                if (currentMessage.messageInfo == null) continue;

                // Handle purging messages by user id or by message id, since both are possible
                if (id == "!FULLCLEAR!" || (isUserId && currentMessage.messageInfo.twitchMessage.user.id == id) || (!isUserId && currentMessage.messageInfo.twitchMessage.id == id))
                {
                    string userName = $"<color={currentMessage.messageInfo.twitchMessage.user.color}><b>{currentMessage.messageInfo.twitchMessage.user.displayName}</b></color>:";
                    if (currentMessage.text.Contains(userName))
                        currentMessage.text = $"{userName} <message deleted>";
                    else
                        currentMessage.text = "";

                    FreeImages(currentMessage);
                    purged = true;
                }
            }
            if (purged)
                UpdateChatUI();
        }

        public void PurgeChatMessageById(string messageId)
        {
            _timeoutQueue.Enqueue(new KeyValuePair<string, bool>(messageId, false));
        }

        public void PurgeMessagesFromUser(string userID)
        {
            _timeoutQueue.Enqueue(new KeyValuePair<string, bool>(userID, true));
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
                var _tmpArray = _chatMessages.ToArray();
                for (int i = 0; i < _tmpArray.Length; i++)
                {
                    int index = ChatConfig.Instance.ReverseChatOrder ? _tmpArray.Length - 1 - i : i;
                    if (_tmpArray[index].text != "")
                    {
                        _tmpArray[index].transform.localPosition = new Vector3(-ChatConfig.Instance.ChatWidth / 2, currentYValue, 0);
                        currentYValue -= (_tmpArray[index].preferredHeight + (i < _chatMessages.Count() - 1 ? ChatConfig.Instance.LineSpacing + 1.5f : 0));
                    }
                }
                _currentBackgroundHeight = (initialYValue - currentYValue) + ChatConfig.Instance.BackgroundPadding * 2;
                background.rectTransform.sizeDelta = new Vector2(ChatConfig.Instance.ChatWidth + ChatConfig.Instance.BackgroundPadding * 2, _currentBackgroundHeight);
                background.rectTransform.position = _twitchChatCanvas.transform.TransformPoint(new Vector3(-ChatConfig.Instance.ChatWidth / 2 - ChatConfig.Instance.BackgroundPadding, (initialYValue - _currentBackgroundHeight + ChatConfig.Instance.BackgroundPadding), 0.1f));
            }
        }

        public void UpdateLockButton()
        {
            lockButtonImage.sprite = ChatConfig.Instance.LockChatPosition ? _lockedSprite : _unlockedSprite;
        }
    };
}
