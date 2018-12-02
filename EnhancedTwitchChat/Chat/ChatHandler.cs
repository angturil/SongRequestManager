using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using EnhancedTwitchChat.Sprites;
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
        
        private Canvas _twitchChatCanvas = null;
        private List<CustomText> _chatMessages = new List<CustomText>();
        private ObjectPool<Image> _imagePool;
        private Transform _chatMoverCube;
        private Transform _lockButtonSphere;
        private float _currentBackgroundHeight;
        private RectTransform _canvasRectTransform;
        private Sprite _lockedSprite;
        private Sprite _unlockedSprite;
        private bool _messageRendering = false;
        private int _waitForFrames = 0;

        public void Awake()
        {
            DontDestroyOnLoad(gameObject);

            if (Instance == null) Instance = this;
            else
            {
                Destroy(this);
                return;
            }

            // Precache a pool of images objects that will be used for displaying emotes/badges later on
            _imagePool = new ObjectPool<Image>(50,
                // OnAlloc
                ((Image image) =>
                {
                    image.material = Drawing.noGlowMaterialUI;
                    var shadow = image.gameObject.GetComponent<Shadow>();
                    if (shadow == null) shadow = image.gameObject.AddComponent<Shadow>();
                }),
                // OnFree
                ((Image image) =>
                {
                    image.enabled = false;
                    var anim = image.GetComponent<AnimatedSprite>();
                    if (anim) anim.enabled = false;
                })
            );

            // Pre-initialize our system fonts to reduce potential lag later on
            Drawing.Initialize(gameObject.transform);
            
            // Startup the sprite loader and anim controller
            new GameObject("EnhancedTwitchChatSpriteLoader").AddComponent<SpriteDownloader>();
            new GameObject("EnhancedTwitchChatAnimController").AddComponent<AnimationController>();

            // Initialize the chats UI
            InitializeChatUI();

            // Subscribe to events
            SceneManager.sceneLoaded += SceneManagerOnSceneLoaded;
            Config.Instance.ConfigChangedEvent += PluginOnConfigChangedEvent;

            Plugin.Log("EnhancedTwitchChat initialized");
        }

        private void SceneManagerOnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var pointer = Resources.FindObjectsOfTypeAll<VRPointer>().FirstOrDefault();
            if (pointer == null) return;
            var movePointer = pointer.gameObject.AddComponent<ChatMover>();
            movePointer.Init(_chatMoverCube);

            var lockPointer = pointer.gameObject.AddComponent<LockToggle>();
            lockPointer.Init(lockButtonImage, _lockButtonSphere);
        }
        
        private void PluginOnConfigChangedEvent(Config config)
        {
            TwitchConnection.Instance.JoinRoom(config.TwitchChannel);
            UpdateChatUI();

            _canvasRectTransform.localScale = new Vector3(0.012f * config.ChatScale, 0.012f * config.ChatScale, 0.012f * config.ChatScale);
            _lockButtonSphere.localScale = new Vector3(0.15f * config.ChatScale, 0.15f * config.ChatScale, 0.001f * config.ChatScale);
            background.color = config.BackgroundColor;

            foreach (CustomText currentMessage in _chatMessages)
                currentMessage.color = config.TextColor;

            Plugin.Log($"Joining channel {config.TwitchChannel}");
        }

        public void Update()
        {
            if (Drawing.SpritesCached)
            {
                // Wait a few seconds after we've connect to the chat, then send our welcome message
                if (displayStatusMessage && (TwitchIRCClient.ChannelIds[Config.Instance.TwitchChannel] != String.Empty || (DateTime.Now - TwitchIRCClient.ConnectionTime).TotalSeconds >= 5))
                {
                    SpriteDownloader.Instance.Init();

                    string msg;
                    if (TwitchIRCClient.ChannelIds[Config.Instance.TwitchChannel] != String.Empty)
                        msg = $"Success joining channel \"{Config.Instance.TwitchChannel}\"";
                    else
                        msg = $"Failed to join channel \"{Config.Instance.TwitchChannel}\"";

                    TwitchMessage tmpMessage = new TwitchMessage();
                    tmpMessage.Author = new ChatUser();
                    tmpMessage.Author.Color = "#00000000";
                    tmpMessage.Author.DisplayName = String.Empty;

                    TwitchIRCClient.RenderQueue.Push(new ChatMessage(msg, tmpMessage));

                    displayStatusMessage = false;
                }

                if (_waitForFrames > 0)
                {
                    _waitForFrames--;
                    return;
                }

                // Wait try to display any new chat messages if our fps is tanking
                float fps = 1.0f / Time.deltaTime;
                if (!Plugin.Instance.IsAtMainMenu && fps < XRDevice.refreshRate - 5)
                    return;

                // Display any messages that we've cached all the resources for and prepared for rendering
                if (TwitchIRCClient.RenderQueue.Count > 0 && !_messageRendering)
                {
                    if (TwitchIRCClient.RenderQueue.TryPop(out var messageToSend))
                        StartCoroutine(AddNewChatMessage(messageToSend.msg, messageToSend));
                }

                // Save images to file when we're at the main menu
                else if (Plugin.Instance.IsAtMainMenu && SpriteDownloader.SpriteSaveQueue.Count > 0 && SpriteDownloader.SpriteSaveQueue.TryPop(out var saveInfo))
                    File.WriteAllBytes(saveInfo.path, saveInfo.data);
            }
        }

        public void LateUpdate()
        {
            if (Drawing.SpritesCached)
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
            _lockedSprite = Utilities.LoadSpriteFromResources("EnhancedTwitchChat.Resources.LockedIcon.png");
            _unlockedSprite = Utilities.LoadSpriteFromResources("EnhancedTwitchChat.Resources.UnlockedIcon.png");

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
            lockButtonImage.color = Color.white.ColorWithAlpha(0.15f);
            lockButtonImage.sprite = Config.Instance.LockChatPosition ? _lockedSprite : _unlockedSprite;
            lockButtonGameObj.AddComponent<Shadow>();

            chatMoverPrimitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.DontDestroyOnLoad(chatMoverPrimitive);
            _chatMoverCube = chatMoverPrimitive.transform;

            lockButtonPrimitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.DontDestroyOnLoad(lockButtonPrimitive);
            _lockButtonSphere = lockButtonPrimitive.transform;
            _lockButtonSphere.localScale = new Vector3(0.15f * Config.Instance.ChatScale, 0.15f * Config.Instance.ChatScale, 0.001f);


            while (_chatMessages.Count < Config.Instance.MaxMessages)
            {
                var currentMessage = Drawing.InitText("", Color.clear, Config.Instance.ChatScale, new Vector2(Config.Instance.ChatWidth, 1), new Vector3(0, 0, 0), new Quaternion(0, 0, 0, 0), gameObject.transform, TextAnchor.UpperLeft, Drawing.noGlowMaterialUI);
                if (!Config.Instance.ReverseChatOrder) _chatMessages.Add(currentMessage);
                else _chatMessages.Insert(0, currentMessage);
            }
        }

        private IEnumerator AddNewChatMessage(string msg, ChatMessage messageInfo)
        {
            _messageRendering = true;
            CustomText currentMessage = null;

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
            currentMessage.text = msg;
            currentMessage.messageInfo = messageInfo;
            currentMessage.material = Drawing.noGlowMaterialUI;

            ClearSprites(currentMessage);
            UpdateChatUI();

            yield return null;
            
            currentMessage.color = Config.Instance.TextColor;
            foreach (BadgeInfo b in messageInfo.parsedBadges)
                Drawing.OverlaySprite(currentMessage, b.swapChar, _imagePool, b.spriteIndex);

            foreach (EmoteInfo e in messageInfo.parsedEmotes)
                Drawing.OverlaySprite(currentMessage, e.swapChar, _imagePool, e.spriteIndex);
            
            currentMessage.hasRendered = true;

            _waitForFrames = 3;
            _messageRendering = false;
        }

        public void OverlaySprite(Sprite sprite, SpriteDownloadInfo spriteDownloadInfo)
        {
            try
            {
                string emoteIndex = spriteDownloadInfo.index;
                string messageIndex = spriteDownloadInfo.messageIndex;
                foreach (CustomText currentMessage in _chatMessages)
                {
                    if (currentMessage.messageInfo == null || !currentMessage.hasRendered) continue;

                    if (!emoteIndex.StartsWith("AB"))
                    {
                        foreach (EmoteInfo e in currentMessage.messageInfo.parsedEmotes)
                        {
                            if (e.spriteIndex == emoteIndex)
                                Drawing.OverlaySprite(currentMessage, e.swapChar, _imagePool, e.spriteIndex);
                        }

                        foreach (BadgeInfo b in currentMessage.messageInfo.parsedBadges)
                        {
                            if (b.spriteIndex == emoteIndex)
                                Drawing.OverlaySprite(currentMessage, b.swapChar, _imagePool, b.spriteIndex);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log($"Exception when overlaying emote! {e.ToString()}");
            }
        }
        
        public void OverlayAnimatedEmote(List<AnimationData> textureList, SpriteDownloadInfo spriteDownloadInfo)
        {
            try
            {
                string emoteIndex = spriteDownloadInfo.index;
                string messageIndex = spriteDownloadInfo.messageIndex;
                if (SpriteDownloader.CachedSprites.ContainsKey(emoteIndex))
                {
                    // If the animated sprite already exists, check if its only a single frame and replace it with the full animation if so
                    var animationInfo = SpriteDownloader.CachedSprites[emoteIndex]?.animationInfo;
                    if (animationInfo != null && animationInfo.Count == 1)
                    {
                        foreach (CustomText currentMessage in _chatMessages)
                        {
                            for (int i = currentMessage.emoteRenderers.Count - 1; i >= 0; i--)
                            {
                                Image img = currentMessage.emoteRenderers[i];
                                if (img.sprite == animationInfo[0].sprite)
                                {
                                    _imagePool.Free(img);
                                    currentMessage.emoteRenderers.RemoveAt(i);
                                }
                            }
                        }
                    }
                }

                // Initialize our animated emote data, registering it with the animation controller if it's longer than 1 frame
                SpriteDownloader.CachedSprites[emoteIndex] = new CachedSpriteData(textureList);
                if (textureList.Count > 1)
                    AnimationController.Instance.Register(textureList);

                foreach (CustomText currentMessage in _chatMessages)
                {
                    if (currentMessage.messageInfo == null || !currentMessage.hasRendered) continue;

                    if (emoteIndex.StartsWith("AB"))
                    {
                        foreach (EmoteInfo e in currentMessage.messageInfo.parsedEmotes)
                        {
                            if (e.spriteIndex == emoteIndex)
                                Drawing.OverlaySprite(currentMessage, e.swapChar, _imagePool, e.spriteIndex);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log($"Exception when overlaying animated emote! {e.ToString()}");
            }
        }

        public void PurgeChatMessages(string userID)
        {
            foreach (CustomText currentMessage in _chatMessages)
            {
                if (currentMessage.messageInfo == null) continue;

                if (currentMessage.messageInfo.twitchMessage.Author.UserID == userID)
                {
                    currentMessage.text = $"<color={currentMessage.messageInfo.twitchMessage.Author.Color}><b>{currentMessage.messageInfo.twitchMessage.Author.DisplayName}</b></color> <message deleted>";
                    ClearSprites(currentMessage);
                    UpdateChatUI();
                }
            }
        }

        private void ClearSprites(CustomText currentMessage)
        {
            if (currentMessage.emoteRenderers.Count > 0)
            {
                currentMessage.emoteRenderers.ForEach(e => _imagePool.Free(e));
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
                        currentYValue -= (_chatMessages[i].preferredHeight + (i < _chatMessages.Count() - 1 ? Config.Instance.LineSpacing + 1.5f : 0));
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
