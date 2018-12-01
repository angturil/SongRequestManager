using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using EnhancedTwitchChat.Utils;
using EnhancedTwitchChat.Chat;
using EnhancedTwitchChat.Sprites;
using AsyncTwitch;

namespace EnhancedTwitchChat.UI
{
    public class CustomText : Text
    {
        public ChatMessage messageInfo;
        public List<Image> emoteRenderers = new List<Image>();
        ~CustomText()
        {
            foreach (Image i in emoteRenderers)
            {
                Destroy(i.gameObject);
            }
        }
    };

    [RequireComponent(typeof(LayoutElement))]
    public class ContentSizeManager : MonoBehaviour
    {
        public float maximumWidth = Plugin.Instance.Config.ChatWidth;
        private LayoutElement _thisElement;
        public void Init()
        {
            //if (parentRect.width > maximumWidth) {
            _thisElement = GetComponent<LayoutElement>();
            _thisElement.preferredWidth = maximumWidth;
        }

        void Update()
        {
        }
    };

    class Drawing
    {
        private const int MaxFontUsages = 3;
        public static int fontUseCount = 0;
        public static int fontUseIndex = 0;
        public static string spriteSpacing = " ";
        private static List<Font> cachedSystemFonts = new List<Font>();
        public static void Initialize(Transform parent)
        {
            CustomText tmpText = InitText(spriteSpacing, Color.white.ColorWithAlpha(0), 10, new Vector2(1000, 1000), new Vector3(0, -100, 0), new Quaternion(0, 0, 0, 0), parent, TextAnchor.MiddleLeft);
            while (tmpText.preferredWidth < 4)
            {
                tmpText.text += " ";
            }
            spriteSpacing = tmpText.text;
            GameObject.Destroy(tmpText.gameObject);
        }

        private static Font LoadSystemFont(string font)
        {
            bool useFallback = false;
            if (font.Length == 0)
            {
                useFallback = true;
            }
            else
            {
                List<string> installedFonts = Font.GetOSInstalledFontNames().ToList().ConvertAll(f => f.ToLower());
                if (!installedFonts.Contains(font.ToLower()))
                {
                    useFallback = true;
                }
            }

            if (useFallback)
            {
                font = "Segoe UI";
                Plugin.Instance.Config.FontName = font;
                Plugin.Instance.ShouldWriteConfig = true;
                Plugin.Log($"Invalid font name specified! Falling back to Segoe UI");
            }
            return Font.CreateDynamicFontFromOSFont(font, 10);
        }

        public static CustomText InitText(string text, Color textColor, float fontSize, Vector2 sizeDelta, Vector3 position, Quaternion rotation, Transform parent, TextAnchor textAlign, Material mat = null)
        {
            GameObject newGameObj = new GameObject();
            CustomText tmpText = newGameObj.AddComponent<CustomText>();

            var scaler = newGameObj.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = ChatHandler.Instance.pixelsPerUnit;

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
            tmpText.rectTransform.pivot = new Vector2(0, 0);
            tmpText.rectTransform.sizeDelta = sizeDelta;
            tmpText.supportRichText = true;
            tmpText.text = text;
            tmpText.font = LoadSystemFont(Plugin.Instance.Config.FontName);
            tmpText.fontSize = 10;

            tmpText.verticalOverflow = VerticalWrapMode.Overflow;
            tmpText.alignment = textAlign;
            tmpText.horizontalOverflow = HorizontalWrapMode.Wrap;
            tmpText.resizeTextForBestFit = false;

            if (mat)
                tmpText.material = mat;

            return tmpText;
        }

        public static void OverlaySprite(CustomText currentMessage, char swapChar, ObjectPool<Image> imagePool, string spriteIndex)
        {
            CachedSpriteData cachedSpriteInfo = SpriteLoader.CachedSprites.ContainsKey(spriteIndex) ? SpriteLoader.CachedSprites[spriteIndex] : null;

            // If cachedSpriteInfo is null, the emote will be overlayed at a later time once it's finished being cached
            if (cachedSpriteInfo == null)
                return;

            bool animatedEmote = cachedSpriteInfo.animationInfo != null;
            foreach (int i in Utilities.IndexOfAll(currentMessage.text, Char.ConvertFromUtf32(swapChar)))
            {
                try
                {
                    if (i > 0 && i < currentMessage.text.Count() - 1 && currentMessage.text[i - 1] == ' ' && currentMessage.text[i + 1] == ' ')
                    {
                        Image image = imagePool.Alloc();
                        image.preserveAspect = true;
                        image.rectTransform.sizeDelta = new Vector2(7.0f, 7.0f);
                        image.rectTransform.pivot = new Vector2(0, 0);

                        if (animatedEmote)
                        {
                            AnimatedSprite animatedImage = image.gameObject.GetComponent<AnimatedSprite>();
                            if (animatedImage == null)
                                animatedImage = image.gameObject.AddComponent<AnimatedSprite>();

                            animatedImage.Init(image, cachedSpriteInfo.animationInfo);
                            animatedImage.enabled = true;
                        }
                        else
                        {
                            image.sprite = cachedSpriteInfo.sprite;
                            image.sprite.texture.wrapMode = TextureWrapMode.Clamp;
                        }
                        image.rectTransform.SetParent(currentMessage.rectTransform, false);

                        TextGenerator textGen = currentMessage.cachedTextGenerator;
                        Vector3 pos = new Vector3(textGen.verts[i * 4 + 3].position.x, textGen.verts[i * 4 + 3].position.y);
                        image.rectTransform.position = currentMessage.gameObject.transform.TransformPoint(pos / ChatHandler.Instance.pixelsPerUnit - new Vector3(image.preferredWidth / ChatHandler.Instance.pixelsPerUnit + 2.5f, image.preferredHeight / ChatHandler.Instance.pixelsPerUnit + 0.7f));

                        image.enabled = true;
                        image.color = image.color.ColorWithAlpha(1);
                        currentMessage.emoteRenderers.Add(image);
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log($"Exception {e.Message} occured when trying to overlay emote at index {i.ToString()}!");
                }
            }

            // Mark the sprites as having been overlayed, so we can't accidentally overlay them twice from our overlay callback
            currentMessage.messageInfo.parsedEmotes.Where(e => e.spriteIndex == spriteIndex).ToList().ForEach(e => e.hasOverlayed = true);
            currentMessage.messageInfo.parsedBadges.Where(b => b.spriteIndex == spriteIndex).ToList().ForEach(b => b.hasOverlayed = true);
        }
    };
}
