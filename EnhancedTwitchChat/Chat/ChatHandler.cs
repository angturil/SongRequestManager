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
        public int pixelsPerUnit = 100;
        public Material noGlowMaterial = null;
        public Material noGlowMaterialUI = null;

        private GameObject _gameObject = null;
        private Canvas _twitchChatCanvas = null;
        private List<CustomText> _chatMessages = new List<CustomText>();
        private ObjectPool<Image> _imagePool;
        private Image _background;
        private GameObject _chatMoverPrimitive;
        private Transform _chatMoverCube;
        private GameObject _lockButtonPrimitive;
        private Transform _lockButtonSphere;
        private Image _lockButtonImage;
        private float _currentBackgroundHeight;
        private RectTransform _canvasRectTransform;
        private Sprite _lockedSprite;
        private Sprite _unlockedSprite;
        private bool _messageRendering = false;
        private int _waitForFrames = 0;

        public void Awake()
        {
            _gameObject = this.gameObject;
            DontDestroyOnLoad(_gameObject);

            if (Instance == null) Instance = this;
            else
            {
                Destroy(this);
                return;
            }

            _imagePool = new ObjectPool<Image>(50,
                // OnAlloc
                ((Image image) =>
                {
                    image.material = noGlowMaterialUI;
                    var shadow = image.gameObject.GetComponent<Shadow>();
                    if (shadow == null) shadow = image.gameObject.AddComponent<Shadow>();
                }),
                // OnFree
                ((Image image) =>
                {
                    image.enabled = false;
                    image.sprite = null;
                    image.color = image.color.ColorWithAlpha(0);
                    var anim = image.GetComponent<AnimatedSprite>();
                    if (anim) anim.enabled = false;
                })
            );

            // Pre-initialize our system fonts to reduce lag later on
            Drawing.Initialize(_gameObject.transform);

            _lockedSprite = Utilities.LoadSpriteFromResources("EnhancedTwitchChat.Resources.LockedIcon.png");
            _unlockedSprite = Utilities.LoadSpriteFromResources("EnhancedTwitchChat.Resources.UnlockedIcon.png");

            Plugin.Instance.Config.ConfigChangedEvent += PluginOnConfigChangedEvent;

            new GameObject().AddComponent<AnimationController>();
            new GameObject().AddComponent<SpriteLoader>();

            _twitchChatCanvas = _gameObject.AddComponent<Canvas>();
            _twitchChatCanvas.renderMode = RenderMode.WorldSpace;
            var collider = _gameObject.AddComponent<MeshCollider>();
            var scaler = _gameObject.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = pixelsPerUnit;
            _canvasRectTransform = _twitchChatCanvas.GetComponent<RectTransform>();
            _canvasRectTransform.localScale = new Vector3(0.012f * Plugin.Instance.Config.ChatScale, 0.012f * Plugin.Instance.Config.ChatScale, 0.012f * Plugin.Instance.Config.ChatScale);

            _background = new GameObject().AddComponent<Image>();
            _background.rectTransform.SetParent(_gameObject.transform, false);
            _background.color = Plugin.Instance.Config.BackgroundColor;
            _background.rectTransform.pivot = new Vector2(0, 0);
            _background.rectTransform.sizeDelta = new Vector2(Plugin.Instance.Config.ChatWidth + Plugin.Instance.Config.BackgroundPadding, 0);
            _background.rectTransform.localPosition = new Vector3(0 - (Plugin.Instance.Config.ChatWidth + Plugin.Instance.Config.BackgroundPadding) / 2, 0, 0);

            var lockButtonGameObj = new GameObject();
            _lockButtonImage = lockButtonGameObj.AddComponent<Image>();
            _lockButtonImage.preserveAspect = true;
            _lockButtonImage.rectTransform.sizeDelta = new Vector2(10, 10);
            _lockButtonImage.rectTransform.SetParent(_gameObject.transform, false);
            _lockButtonImage.rectTransform.pivot = new Vector2(0, 0);
            _lockButtonImage.color = Color.white.ColorWithAlpha(0.15f);
            lockButtonGameObj.AddComponent<Shadow>();

            _chatMoverPrimitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.DontDestroyOnLoad(_chatMoverPrimitive);
            _chatMoverCube = _chatMoverPrimitive.transform;

            _lockButtonPrimitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.DontDestroyOnLoad(_lockButtonPrimitive);
            _lockButtonSphere = _lockButtonPrimitive.transform;
            _lockButtonSphere.localScale = new Vector3(0.15f * Plugin.Instance.Config.ChatScale, 0.15f * Plugin.Instance.Config.ChatScale, 0.001f);

            while (_chatMessages.Count < Plugin.Instance.Config.MaxMessages)
            {
                var currentMessage = Drawing.InitText("", Color.clear, Plugin.Instance.Config.ChatScale, new Vector2(Plugin.Instance.Config.ChatWidth, 1), new Vector3(0, 0, 0), new Quaternion(0, 0, 0, 0), _gameObject.transform, TextAnchor.UpperLeft, noGlowMaterialUI);
                if (!Plugin.Instance.Config.ReverseChatOrder) _chatMessages.Add(currentMessage);
                else _chatMessages.Insert(0, currentMessage);
            }

            SceneManager.sceneLoaded += SceneManagerOnSceneLoaded;

            Plugin.Log("EnhancedTwitchChat initialized");
        }

        private void PluginOnConfigChangedEvent(Config config)
        {
            TwitchConnection.Instance.JoinRoom(config.TwitchChannel);
            UpdateChatUI();

            _canvasRectTransform.localScale = new Vector3(0.012f * config.ChatScale, 0.012f * config.ChatScale, 0.012f * config.ChatScale);
            _lockButtonSphere.localScale = new Vector3(0.15f * config.ChatScale, 0.15f * config.ChatScale, 0.001f * config.ChatScale);
            _background.color = config.BackgroundColor;

            foreach (CustomText currentMessage in _chatMessages)
                currentMessage.color = config.TextColor;

            Plugin.Log($"Joining channel {config.TwitchChannel}");
        }

        private void SceneManagerOnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var pointer = Resources.FindObjectsOfTypeAll<VRPointer>().FirstOrDefault();
            if (pointer == null) return;
            var movePointer = pointer.gameObject.AddComponent<ChatMover>();
            movePointer.Init(_chatMoverCube);

            var lockPointer = pointer.gameObject.AddComponent<LockToggle>();
            lockPointer.Init(_lockButtonImage, _lockButtonSphere);
        }

        public void Update()
        {
            if (!noGlowMaterial)
            {
                Material shader = Resources.FindObjectsOfTypeAll<Material>().Where(m => m.name == "UINoGlow").FirstOrDefault();
                if (shader)
                {
                    noGlowMaterial = new Material(shader);
                    noGlowMaterialUI = new Material(shader);
                    _background.material = new Material(shader);
                    _lockButtonImage.material = new Material(shader);
                    var mat = new Material(shader);
                    mat.color = Color.clear;
                    _chatMoverPrimitive.GetComponent<Renderer>().material = mat;
                    _lockButtonPrimitive.GetComponent<Renderer>().material = mat;
                }
            }

            if (noGlowMaterial && noGlowMaterialUI)
            {
                // Set our lock button image sprite
                _lockButtonImage.sprite = Plugin.Instance.Config.LockChatPosition ? _lockedSprite : _unlockedSprite;

                // Wait a few seconds after we've connect to the chat, then send our welcome message
                if (displayStatusMessage && (TwitchIRCClient.ChannelID != String.Empty || (DateTime.Now - TwitchIRCClient.ConnectionTime).TotalSeconds >= 5))
                {
                    SpriteLoader.Instance.Init();

                    string msg;
                    if (TwitchIRCClient.ChannelID != String.Empty)
                        msg = $"Success joining channel \"{Plugin.Instance.Config.TwitchChannel}\"";
                    else
                        msg = $"Failed to join channel \"{Plugin.Instance.Config.TwitchChannel}\"";

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

                // Wait a second if FPS dips below the displays refresh rate
                float fps = 1.0f / Time.deltaTime;
                if (!Plugin.Instance.IsAtMainMenu && fps < XRDevice.refreshRate - 5)
                {
                    //_waitForFrames = 15;
                    //Plugin.Log($"[{DateTime.Now.ToLongTimeString()}] FPS: {fps}, {TwitchIRCClient.MessageQueue.Count.ToString()} messages queued!");
                    return;
                }

                // Display any messages that we've cached all the resources for and prepared for rendering
                if (TwitchIRCClient.RenderQueue.Count > 0 && !_messageRendering)
                {
                    if (TwitchIRCClient.RenderQueue.TryPop(out var messageToSend))
                    {
                        StartCoroutine(AddNewChatMessage(messageToSend.msg, messageToSend));
                    }
                }

                // Save images to file when we're at the main menu
                else if (Plugin.Instance.IsAtMainMenu && SpriteLoader.SpriteSaveQueue.Count > 0 && SpriteLoader.SpriteSaveQueue.TryPop(out var saveInfo))
                    File.WriteAllBytes(saveInfo.path, saveInfo.data);
            }
        }

        public void LateUpdate()
        {
            if (noGlowMaterial && noGlowMaterialUI)
            {
                _twitchChatCanvas.transform.eulerAngles = Plugin.Instance.Config.ChatRotation;
                _twitchChatCanvas.transform.position = Plugin.Instance.Config.ChatPosition;
                if (!Plugin.Instance.Config.ReverseChatOrder) _twitchChatCanvas.transform.position = _twitchChatCanvas.transform.TransformPoint(new Vector3(0, _currentBackgroundHeight));

                _chatMoverCube.localScale = _background.rectTransform.sizeDelta * (Plugin.Instance.Config.ChatScale * 1.2f) / pixelsPerUnit;
                _chatMoverCube.eulerAngles = Plugin.Instance.Config.ChatRotation;
                _chatMoverCube.position = _background.rectTransform.TransformPoint(_background.rectTransform.rect.width / 2, _currentBackgroundHeight / 2, 0);

                Vector3[] LocalCorners = new Vector3[4];
                _background.rectTransform.GetLocalCorners(LocalCorners);
                _lockButtonSphere.eulerAngles = Plugin.Instance.Config.ChatRotation;
                _lockButtonImage.rectTransform.eulerAngles = Plugin.Instance.Config.ChatRotation;
                _lockButtonImage.rectTransform.position = _background.rectTransform.TransformPoint((Plugin.Instance.Config.ReverseChatOrder ? LocalCorners[2] : LocalCorners[3]) - new Vector3(_lockButtonImage.rectTransform.sizeDelta.x / 2, _lockButtonImage.rectTransform.sizeDelta.y / 2));
                _lockButtonSphere.position = _lockButtonImage.rectTransform.TransformPoint(new Vector3(_lockButtonSphere.transform.localScale.x / 2 * pixelsPerUnit, _lockButtonSphere.transform.localScale.y / 2 * pixelsPerUnit, -0.01f) / Plugin.Instance.Config.ChatScale);
            }
        }

        private IEnumerator AddNewChatMessage(string msg, ChatMessage messageInfo)
        {
            _messageRendering = true;
            CustomText currentMessage = null;

            if (!Plugin.Instance.Config.ReverseChatOrder)
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
            currentMessage.material = noGlowMaterialUI;

            ClearEmotes(currentMessage);
            UpdateChatUI();

            yield return null;

            currentMessage.color = Plugin.Instance.Config.TextColor;
            foreach (BadgeInfo b in messageInfo.parsedBadges)
            {
                Drawing.OverlayEmote(currentMessage, b.swapChar, _imagePool, new CachedSpriteData(b.sprite));
                yield return null;
            }
            foreach (EmoteInfo e in messageInfo.parsedEmotes)
            {
                Drawing.OverlayEmote(currentMessage, e.swapChar, _imagePool, e.cachedSpriteInfo);
                yield return null;
            }

            _messageRendering = false;
            if (Plugin.Instance.IsAtMainMenu)
                _waitForFrames = 3;
            else
                _waitForFrames = 10;
        }

        public void PurgeChatMessages(string userID)
        {
            foreach (CustomText currentMessage in _chatMessages)
            {
                if (currentMessage.messageInfo.twitchMessage.Author.UserID == userID)
                {
                    currentMessage.text = $"<color={currentMessage.messageInfo.twitchMessage.Author.Color}><b>{currentMessage.messageInfo.twitchMessage.Author.DisplayName}</b></color> <message deleted>";
                    ClearEmotes(currentMessage);
                    UpdateChatUI();
                }
            }
        }

        public void OverlayEmote(Sprite emote, string emoteIndex)
        {
            try
            {
                foreach (CustomText currentMessage in _chatMessages)
                {
                    if (currentMessage.messageInfo == null) continue;

                    if (!emoteIndex.StartsWith("AB"))
                    {
                        foreach (EmoteInfo e in currentMessage.messageInfo.parsedEmotes)
                        {
                            if (e.emoteIndex == emoteIndex && e.cachedSpriteInfo == null)
                            {
                                e.cachedSpriteInfo = new CachedSpriteData(emote);
                                Drawing.OverlayEmote(currentMessage, e.swapChar, _imagePool, e.cachedSpriteInfo);
                            }
                        }

                        foreach (BadgeInfo b in currentMessage.messageInfo.parsedBadges)
                        {
                            if (b.badgeIndex == emoteIndex && b.sprite == null)
                            {
                                b.sprite = emote;
                                Drawing.OverlayEmote(currentMessage, b.swapChar, _imagePool, new CachedSpriteData(b.sprite));
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log($"Exception when overlaying emote! {e.ToString()}");
            }
        }
        
        public void OverlayAnimatedEmote(List<AnimationData> textureList, string emoteIndex)
        {
            try
            {
                if (SpriteLoader.CachedSprites.ContainsKey(emoteIndex))
                {
                    var animationInfo = SpriteLoader.CachedSprites[emoteIndex]?.animationInfo;
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

                SpriteLoader.CachedSprites[emoteIndex] = new CachedSpriteData(textureList);
                if (textureList.Count > 1)
                    AnimationController.Instance.Register(textureList);

                foreach (CustomText currentMessage in _chatMessages)
                {
                    if (currentMessage.messageInfo == null) continue;

                    if (emoteIndex.StartsWith("AB"))
                    {
                        foreach (EmoteInfo e in currentMessage.messageInfo.parsedEmotes)
                        {
                            if (e.emoteIndex == emoteIndex)
                            {
                                e.cachedSpriteInfo = SpriteLoader.CachedSprites[emoteIndex];
                                Drawing.OverlayEmote(currentMessage, e.swapChar, _imagePool, e.cachedSpriteInfo);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log($"Exception when overlaying animated emote! {e.ToString()}");
            }
        }

        private void ClearEmotes(CustomText currentMessage)
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
                        _chatMessages[i].transform.localPosition = new Vector3(-Plugin.Instance.Config.ChatWidth / 2, currentYValue, 0);
                        currentYValue -= (_chatMessages[i].preferredHeight + (i < _chatMessages.Count() - 1 ? Plugin.Instance.Config.LineSpacing + 1.5f : 0));
                    }
                }
                _currentBackgroundHeight = (initialYValue - currentYValue) + Plugin.Instance.Config.BackgroundPadding * 2;

                _background.rectTransform.sizeDelta = new Vector2(Plugin.Instance.Config.ChatWidth + Plugin.Instance.Config.BackgroundPadding * 2, _currentBackgroundHeight);
                _background.rectTransform.position = _twitchChatCanvas.transform.TransformPoint(new Vector3(-Plugin.Instance.Config.ChatWidth / 2 - Plugin.Instance.Config.BackgroundPadding, (initialYValue - _currentBackgroundHeight + Plugin.Instance.Config.BackgroundPadding), 0));
            }
        }
    };
}
