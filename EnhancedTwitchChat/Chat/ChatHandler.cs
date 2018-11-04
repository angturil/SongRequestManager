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

namespace EnhancedTwitchChat {
    public class ChatHandler : MonoBehaviour {
        public bool displayStatusMessage = false;

        private GameObject _gameObject = null;
        private Canvas _twitchChatCanvas = null;
        private Material _noGlowMaterial = null;
        private Material _noGlowMaterialUI = null;
        private List<CustomText> _chatMessages = new List<CustomText>();
        private SpriteLoader _spriteLoader;
        private AnimatedSpriteLoader _animatedSpriteLoader;
        private AnimationController _animationController;
        private Image _background;
        private ConcurrentStack<string> _timeoutQueue = new ConcurrentStack<string>();
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

        public void Awake() {
            _gameObject = this.gameObject;
            UnityEngine.Object.DontDestroyOnLoad(_gameObject);

            // Pre-initialize our system fonts to reduce lag later on
            Drawing.Initialize(_gameObject.transform);

            _lockedSprite = Utilities.LoadNewSprite(BuiltInResources.LockedIcon);
            _unlockedSprite = Utilities.LoadNewSprite(BuiltInResources.UnlockedIcon);

            Plugin.Instance.Config.ConfigChangedEvent += PluginOnConfigChangedEvent;

            _animationController = new GameObject().AddComponent<AnimationController>();
            _animatedSpriteLoader = new GameObject().AddComponent<AnimatedSpriteLoader>();
            _spriteLoader = new GameObject().AddComponent<SpriteLoader>();
            _twitchChatCanvas = _gameObject.AddComponent<Canvas>();
            _twitchChatCanvas.renderMode = RenderMode.WorldSpace;
            var collider = _gameObject.AddComponent<MeshCollider>();
            var scaler = _gameObject.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = Plugin.PixelsPerUnit;
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
            var shadow = lockButtonGameObj.AddComponent<Shadow>();

            _chatMoverPrimitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.DontDestroyOnLoad(_chatMoverPrimitive);
            _chatMoverCube = _chatMoverPrimitive.transform;

            _lockButtonPrimitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.DontDestroyOnLoad(_lockButtonPrimitive);
            _lockButtonSphere = _lockButtonPrimitive.transform;
            _lockButtonSphere.localScale = new Vector3(0.15f * Plugin.Instance.Config.ChatScale, 0.15f * Plugin.Instance.Config.ChatScale, 0.001f);

            SceneManager.sceneLoaded += SceneManagerOnSceneLoaded;

            Plugin.Log("EnhancedTwitchChat initialized");
        }

        private void PluginOnConfigChangedEvent(Config config) {
            TwitchIRCClient.OnConnectionComplete();
            UpdateChatUI();

            _canvasRectTransform.localScale = new Vector3(0.012f * Plugin.Instance.Config.ChatScale, 0.012f * Plugin.Instance.Config.ChatScale, 0.012f * Plugin.Instance.Config.ChatScale);
            _lockButtonSphere.localScale = new Vector3(0.15f * Plugin.Instance.Config.ChatScale, 0.15f * Plugin.Instance.Config.ChatScale, 0.001f * Plugin.Instance.Config.ChatScale);
            _background.color = Plugin.Instance.Config.BackgroundColor;

            foreach (CustomText currentMessage in _chatMessages) {
                currentMessage.color = Plugin.Instance.Config.TextColor;
            }
        }

        private void SceneManagerOnSceneLoaded(Scene scene, LoadSceneMode mode) {
            var pointer = Resources.FindObjectsOfTypeAll<VRPointer>().FirstOrDefault();
            if (pointer == null) return;
            var movePointer = pointer.gameObject.AddComponent<ChatMover>();
            movePointer.Init(_chatMoverCube);
            
            var lockPointer = pointer.gameObject.AddComponent<LockToggle>();
            lockPointer.Init(this, _lockButtonImage, _lockButtonSphere);
        }
        
        public void Update() {
            if (!_noGlowMaterial) {
                Shader shader = Shader.Find("Custom/SpriteNoGlow");
                if (shader) {
                    _noGlowMaterial = new Material(shader);
                    _background.material = new Material(shader);
                    _lockButtonImage.material = new Material(shader);
                }
            }

            if (!_noGlowMaterialUI) {
                Shader shader = Shader.Find("Custom/UINoGlow");
                if (shader) {
                    _noGlowMaterialUI = new Material(shader);
                    var mat = new Material(shader);
                    mat.color = Color.clear;
                    _chatMoverPrimitive.GetComponent<Renderer>().material = mat;
                    _lockButtonPrimitive.GetComponent<Renderer>().material = mat;
                }
            }
            
            if (_noGlowMaterial && _noGlowMaterialUI) {
                // Set our lock button image sprite
                _lockButtonImage.sprite = Plugin.Instance.Config.LockChatPosition ? _lockedSprite : _unlockedSprite;

                // Wait a few seconds after we've connect to the chat, then send our welcome message
                if (displayStatusMessage && (TwitchIRCClient.JoinedChannel || (DateTime.Now - TwitchIRCClient.ConnectionTime).TotalSeconds >= 5)) {
                    if (TwitchIRCClient.JoinedChannel) {
                        Plugin.Log("Reinitializing sprites!");
                        _spriteLoader.Init();
                    }

                    MessageInfo messageInfo = new MessageInfo();
                    messageInfo.nameColor = "#00000000";
                    messageInfo.sender = String.Empty;
                    if (TwitchIRCClient.JoinedChannel) {
                        TwitchIRCClient.RenderQueue.Push(new ChatMessage($"Success joining chat room \"{Plugin.Instance.Config.TwitchChannel}\"", messageInfo));
                    }
                    else {
                        TwitchIRCClient.RenderQueue.Push(new ChatMessage($"Failed to connect to the chat room!", messageInfo));
                    }
                    displayStatusMessage = false;
                }

                // Save images to file when we're at the main menu
                if (Plugin.Instance.IsAtMainMenu) {
                    if (SpriteLoader.SpriteSaveQueue.Count > 0) {
                        if (SpriteLoader.SpriteSaveQueue.TryPop(out var saveInfo)) {
                            File.WriteAllBytes(saveInfo.path, saveInfo.data);
                            //Plugin.Log("Saved local file!");
                        }
                    }
                }

                // Removed any timed out messages from view
                if (_timeoutQueue.Count > 0) {
                    if(_timeoutQueue.TryPop(out var userID)) {
                        PurgeChatMessages(userID);
                    }
                }

                // Overlay any animated emotes that have just finished processing
                if (SpriteLoader.AnimationDisplayQueue.Count > 0) {
                    if (SpriteLoader.AnimationDisplayQueue.TryPop(out var animationDisplayInfo)) {
                        if (SpriteLoader.CachedSprites.ContainsKey(animationDisplayInfo.emoteIndex)) {
                            var animationInfo = SpriteLoader.CachedSprites[animationDisplayInfo.emoteIndex]?.animationInfo;
                            if (animationInfo != null && animationInfo.Count == 1) {
                                foreach (CustomText currentMessage in _chatMessages) {
                                    for (int i = currentMessage.emoteRenderers.Count - 1; i >= 0; i--) {
                                        Image img = currentMessage.emoteRenderers[i];
                                        if (img.sprite == animationInfo[0].sprite) {
                                            Destroy(img.gameObject);
                                            currentMessage.emoteRenderers.RemoveAt(i);
                                        }
                                    }
                                }
                            }
                        }

                        SpriteLoader.CachedSprites[animationDisplayInfo.emoteIndex] = new CachedSpriteData(animationDisplayInfo.sprites);
                        if (animationDisplayInfo.sprites.Count > 1) {
                            _animationController.Register(animationDisplayInfo.sprites);
                        }
                        foreach (CustomText currentMessage in _chatMessages) {
                            if(animationDisplayInfo.emoteIndex.StartsWith("AB")) {
                                foreach (EmoteInfo e in currentMessage.messageInfo.parsedEmotes) {
                                    if (e.emoteIndex == animationDisplayInfo.emoteIndex) {
                                        e.cachedSpriteInfo = SpriteLoader.CachedSprites[animationDisplayInfo.emoteIndex];
                                        Drawing.OverlayEmote(currentMessage, e.swapChar, _noGlowMaterialUI, _animationController, e.cachedSpriteInfo);
                                    }
                                }
                            }
                        }
                    }
                }

                // Display any messages that we've cached all the resources for and prepared for rendering
                if (TwitchIRCClient.RenderQueue.Count > 0 && !_messageRendering) {
                    if (_waitForFrames == 0) {
                        if (TwitchIRCClient.RenderQueue.TryPop(out var messageToSend)) {
                            StartCoroutine(AddNewChatMessage(messageToSend.msg, messageToSend.messageInfo));
                        }
                    }
                    else {
                        _waitForFrames--;
                    }
                }
            }
        }

        public void LateUpdate() {
            if (_noGlowMaterial && _noGlowMaterialUI) {
                _twitchChatCanvas.transform.eulerAngles = Plugin.Instance.Config.ChatRotation;
                _twitchChatCanvas.transform.position = Plugin.Instance.Config.ChatPosition;
                if (!Plugin.Instance.Config.ReverseChatOrder) _twitchChatCanvas.transform.position = _twitchChatCanvas.transform.TransformPoint(new Vector3(0, _currentBackgroundHeight));

                _chatMoverCube.localScale = _background.rectTransform.sizeDelta * (Plugin.Instance.Config.ChatScale * 1.2f) / Plugin.PixelsPerUnit;
                _chatMoverCube.eulerAngles = Plugin.Instance.Config.ChatRotation;
                _chatMoverCube.position = _background.rectTransform.TransformPoint(_background.rectTransform.rect.width/2, _currentBackgroundHeight / 2, 0);

                Vector3[] LocalCorners = new Vector3[4];
                _background.rectTransform.GetLocalCorners(LocalCorners);
                _lockButtonSphere.eulerAngles = Plugin.Instance.Config.ChatRotation;
                _lockButtonImage.rectTransform.eulerAngles = Plugin.Instance.Config.ChatRotation;
                _lockButtonImage.rectTransform.position = _background.rectTransform.TransformPoint((Plugin.Instance.Config.ReverseChatOrder ? LocalCorners[2] : LocalCorners[3]) - new Vector3(_lockButtonImage.rectTransform.sizeDelta.x / 2, _lockButtonImage.rectTransform.sizeDelta.y / 2));
                _lockButtonSphere.position = _lockButtonImage.rectTransform.TransformPoint(new Vector3(_lockButtonSphere.transform.localScale.x / 2 * Plugin.PixelsPerUnit, _lockButtonSphere.transform.localScale.y / 2 * Plugin.PixelsPerUnit, -0.01f) / Plugin.Instance.Config.ChatScale);
            }
        }

        private IEnumerator AddNewChatMessage(string msg, MessageInfo messageInfo) {
            _messageRendering = true;
            CustomText currentMessage = null;
            if (_chatMessages.Count < Plugin.Instance.Config.MaxMessages) {
                currentMessage = Drawing.InitText(msg, Color.clear, Plugin.Instance.Config.ChatScale, new Vector2(Plugin.Instance.Config.ChatWidth, 1), new Vector3(0, 0, 0), new Quaternion(0, 0, 0, 0), _gameObject.transform, TextAnchor.UpperLeft, _noGlowMaterialUI);
                if (!Plugin.Instance.Config.ReverseChatOrder) {
                    _chatMessages.Add(currentMessage);
                }
                else {
                    _chatMessages.Insert(0, currentMessage);
                }
            }
            else {
                if (!Plugin.Instance.Config.ReverseChatOrder) {
                    currentMessage = _chatMessages.First();
                    _chatMessages.RemoveAt(0);
                    _chatMessages.Add(currentMessage);
                }
                else {
                    currentMessage = _chatMessages.Last();
                    _chatMessages.Remove(currentMessage);
                    _chatMessages.Insert(0, currentMessage);
                }
                currentMessage.text = msg;
            }
            currentMessage.messageInfo = messageInfo;

            if (currentMessage.emoteRenderers.Count > 0) {
                currentMessage.emoteRenderers.ForEach(e => Destroy(e.gameObject));
                currentMessage.emoteRenderers.Clear();
            }

            UpdateChatUI();

            yield return null;

            currentMessage.color = Plugin.Instance.Config.TextColor;
            messageInfo.parsedEmotes.ForEach(e => Drawing.OverlayEmote(currentMessage, e.swapChar, _noGlowMaterialUI, _animationController, e.cachedSpriteInfo));
            messageInfo.parsedBadges.ForEach(b => Drawing.OverlayEmote(currentMessage, b.swapChar, _noGlowMaterialUI, _animationController, new CachedSpriteData(b.sprite)));


            _messageRendering = false;
            _waitForFrames = 2;
        }
        
        private void PurgeChatMessages(string userID) {
            foreach (CustomText currentMessage in _chatMessages) {
                if (currentMessage.messageInfo.userID == userID) {
                    currentMessage.text = $"<color={currentMessage.messageInfo.nameColor}><b>{currentMessage.messageInfo.sender}</b></color> <message deleted>";
                    currentMessage.emoteRenderers.ForEach(e => Destroy(e.gameObject));
                    currentMessage.emoteRenderers.Clear();
                    UpdateChatUI();
                }
            }
        }
        
        private void UpdateChatUI() {
            if (_chatMessages.Count > 0) {
                // Update the position of each text elem (which also moves the emotes since they are children of the text)
                float currentYValue = 0;
                float initialYValue = currentYValue;
                for (int i = 0; i < _chatMessages.Count(); i++) {
                    _chatMessages[i].transform.localPosition = new Vector3(-Plugin.Instance.Config.ChatWidth / 2, currentYValue, 0);
                    currentYValue -= (_chatMessages[i].preferredHeight + (i < _chatMessages.Count() - 1 ? Plugin.Instance.Config.LineSpacing + 1.5f : 0));
                }
                _currentBackgroundHeight = (initialYValue - currentYValue) + Plugin.Instance.Config.BackgroundPadding * 2;

                _background.rectTransform.sizeDelta = new Vector2(Plugin.Instance.Config.ChatWidth + Plugin.Instance.Config.BackgroundPadding * 2, _currentBackgroundHeight);
                _background.rectTransform.position = _twitchChatCanvas.transform.TransformPoint(new Vector3(-Plugin.Instance.Config.ChatWidth / 2 - Plugin.Instance.Config.BackgroundPadding, initialYValue - _currentBackgroundHeight + Plugin.Instance.Config.BackgroundPadding + 1, 0));
            }
        }
       
        public void QueueDownload(SpriteDownloadInfo emote) {
            if (emote.type == ImageType.BTTV_Animated) {
                _animatedSpriteLoader.Queue(emote);
            }
            else {
                _spriteLoader.Queue(emote);
            }
        }

        public void OnUserTimedOut(string userID) {
            _timeoutQueue.Push(userID);
        }
    };
}
