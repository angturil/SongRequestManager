using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using BetterTwitchChat.Utils;
using BetterTwitchChat.Chat;
using BetterTwitchChat.Sprites;

namespace BetterTwitchChat.UI {
    public class CustomText : Text {
        public MessageInfo messageInfo;
        public List<Image> emoteRenderers = new List<Image>();
        ~CustomText() {
            foreach (Image i in emoteRenderers) {
                Destroy(i.gameObject);
            }
        }
    };

    [RequireComponent(typeof(LayoutElement))]
    public class ContentSizeManager : MonoBehaviour {
        public float maximumWidth = Plugin.Instance.Config.ChatWidth;
        private LayoutElement _thisElement;
        public void Init() {
            //if (parentRect.width > maximumWidth) {
            _thisElement = GetComponent<LayoutElement>();
            _thisElement.preferredWidth = maximumWidth;
        }

        void Update() {
        }
    };

    class Drawing {
        private const int MaxFontUsages = 5;
        public static int fontUseCount = 0;
        public static int fontUseIndex = 0;
        private static List<Font> cachedSystemFonts = new List<Font>();
        public static void InitSystemFonts() {
            for (int i = 0; i < Plugin.Instance.Config.MaxMessages + 1; i+= MaxFontUsages) {
                cachedSystemFonts.Add(LoadSystemFont(Plugin.Instance.Config.FontName));
            }
        }
        
        private static Font LoadSystemFont(string font) {
            Font newFont = Font.CreateDynamicFontFromOSFont(font, 10);
            if (newFont != null) {
                return newFont;
            }
            else {
                return LoadSystemFont("Segoe UI");
            }
        }

        public static CustomText InitText(string text, Color textColor, float fontSize, Vector2 sizeDelta, Vector3 position, Quaternion rotation, Transform parent, TextAnchor textAlign, Material mat = null) {
            GameObject newGameObj = new GameObject();
            CustomText tmpText = newGameObj.AddComponent<CustomText>();

            var scaler = newGameObj.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = Plugin.PixelsPerUnit;

            var shadow = newGameObj.AddComponent<Shadow>();
            //shadow.effectDistance = new Vector2(0.5f, -0.5f);

            var mcs = tmpText.gameObject.AddComponent<ContentSizeManager>();
            mcs.transform.SetParent(tmpText.rectTransform, false);
            mcs.Init();
            var fitter = tmpText.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            tmpText.color = textColor;
            tmpText.rectTransform.SetParent(parent.transform, false);
            tmpText.rectTransform.localPosition = position;
            tmpText.rectTransform.localRotation = rotation;
            tmpText.rectTransform.sizeDelta = sizeDelta;
            tmpText.rectTransform.pivot = new Vector2(0, 0);
            tmpText.supportRichText = true;
            tmpText.text = text;
            tmpText.font = cachedSystemFonts[fontUseIndex];
            tmpText.fontSize = 10;

            // For some reason when too many text elems are using the same font asset, shit starts to get wonky.
            // If anyone knows how to fix this without this ghetto shit let me know... would be much appreciated :)
            // (If you want to see what I'm talking about remove this shit below, and go in any asian chat)
            fontUseCount++;
            if (fontUseCount >= MaxFontUsages) {
                fontUseCount = 0;
                fontUseIndex++;
            }
            
            tmpText.verticalOverflow = VerticalWrapMode.Overflow;
            tmpText.alignment = textAlign;
            tmpText.horizontalOverflow = HorizontalWrapMode.Wrap;
            tmpText.resizeTextForBestFit = false;
            tmpText.alignByGeometry = true;

            if (mat) {
                tmpText.material = mat;
            }

            return tmpText;
        }

        public static void OverlayEmote(CustomText currentMessage, char swapChar, Material noGlowMaterialUI, AnimationController animationController, CachedSpriteData cachedSpriteInfo) {
            bool animatedEmote = false;
            if (cachedSpriteInfo != null) {
                if (cachedSpriteInfo.sprite == null) {
                    animatedEmote = true;
                }
            }
            else {
                return;
            }

            foreach (int i in Utilities.IndexOfAll(currentMessage.text, Char.ConvertFromUtf32(swapChar))) {
                if (i > 1 && i < currentMessage.text.Count() - 2 && currentMessage.text[i - 1] == ' ' && currentMessage.text[i - 2] == ' ' && currentMessage.text[i + 1] == ' ' && currentMessage.text[i + 2] == ' ') {
                    GameObject newGameObject = new GameObject();
                    var image = newGameObject.AddComponent<Image>();
                    image.material = noGlowMaterialUI;
                    
                    var shadow = newGameObject.AddComponent<Shadow>();

                    if (animatedEmote) {
                        var animatedImage = newGameObject.AddComponent<AnimatedSprite>();
                        animatedImage.Init(image, cachedSpriteInfo.animationInfo, animationController);
                    }
                    else {
                        image.sprite = cachedSpriteInfo.sprite;
                        image.sprite.texture.wrapMode = TextureWrapMode.Clamp;
                    }

                    image.rectTransform.SetParent(currentMessage.rectTransform, false);
                    image.preserveAspect = true;
                    image.rectTransform.sizeDelta = new Vector2(7.0f, 7.0f);
                    image.rectTransform.pivot = new Vector2(0, 0);


                    var textGen = currentMessage.cachedTextGenerator;
                    var pos = new Vector3(textGen.verts[i * 4 + 3].position.x, textGen.verts[i * 4 + 3].position.y);
                    image.rectTransform.position = currentMessage.gameObject.transform.TransformPoint(pos / Plugin.PixelsPerUnit - new Vector3(image.preferredWidth / Plugin.PixelsPerUnit + 2.5f, image.preferredHeight / Plugin.PixelsPerUnit + 0.7f));
                    currentMessage.emoteRenderers.Add(image);
                }
            }
        }
    };
}
