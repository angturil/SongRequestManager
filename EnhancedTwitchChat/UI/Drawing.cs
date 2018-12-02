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
        public bool hasRendered = false;
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
        public float maximumWidth = Config.Instance.ChatWidth;
        private LayoutElement _thisElement;
        public void Init()
        {
            //if (parentRect.width > maximumWidth) {
            _thisElement = GetComponent<LayoutElement>();
            _thisElement.preferredWidth = maximumWidth;
        }
    };

    class Drawing
    {
        public static int pixelsPerUnit = 100;
        public static Material noGlowMaterial = null;
        public static Material noGlowMaterialUI = null;
        public static string spriteSpacing = " ";

        public static bool SpritesCached
        {
            get
            {
                if (!noGlowMaterial)
                {
                    Material material = Resources.FindObjectsOfTypeAll<Material>().Where(m => m.name == "UINoGlow").FirstOrDefault();
                    if (material)
                    {
                        noGlowMaterial = new Material(material);
                        noGlowMaterialUI = new Material(material);
                        ChatHandler.Instance.background.material = new Material(material);
                        ChatHandler.Instance.lockButtonImage.material = new Material(material);
                        var mat = new Material(material);
                        mat.color = Color.clear;
                        ChatHandler.Instance.chatMoverPrimitive.GetComponent<Renderer>().material = mat;
                        ChatHandler.Instance.lockButtonPrimitive.GetComponent<Renderer>().material = mat;
                    }
                }
                return noGlowMaterial && noGlowMaterialUI;
            }
        }

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
                Config.Instance.FontName = font;
                Plugin.Instance.ShouldWriteConfig = true;
                Plugin.Log($"Invalid font name specified! Falling back to Segoe UI");
            }
            return Font.CreateDynamicFontFromOSFont(font, 10);
        }

        public static CustomText InitText(string text, Color textColor, float fontSize, Vector2 sizeDelta, Vector3 position, Quaternion rotation, Transform parent, TextAnchor textAlign, Material mat = null)
        {
            GameObject newGameObj = new GameObject();
            newGameObj.AddComponent<Shadow>();

            CanvasScaler scaler = newGameObj.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = pixelsPerUnit;

            CustomText tmpText = newGameObj.AddComponent<CustomText>();
            tmpText.color = textColor;
            tmpText.rectTransform.SetParent(parent.transform, false);
            tmpText.rectTransform.localPosition = position;
            tmpText.rectTransform.localRotation = rotation;
            tmpText.rectTransform.pivot = new Vector2(0, 0);
            tmpText.rectTransform.sizeDelta = sizeDelta;
            tmpText.supportRichText = true;
            tmpText.text = text;
            tmpText.font = LoadSystemFont(Config.Instance.FontName);
            tmpText.fontSize = 10;
            tmpText.verticalOverflow = VerticalWrapMode.Overflow;
            tmpText.alignment = textAlign;
            tmpText.horizontalOverflow = HorizontalWrapMode.Wrap;
            tmpText.resizeTextForBestFit = false;

            var mcs = tmpText.gameObject.AddComponent<ContentSizeManager>();
            mcs.transform.SetParent(tmpText.rectTransform, false);
            mcs.Init();
            var fitter = tmpText.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (mat)
                tmpText.material = mat;

            return tmpText;
        }

        public static void OverlaySprite(CustomText currentMessage, char swapChar, ObjectPool<Image> imagePool, string spriteIndex)
        {
            CachedSpriteData cachedSpriteInfo = SpriteDownloader.CachedSprites.ContainsKey(spriteIndex) ? SpriteDownloader.CachedSprites[spriteIndex] : null;

            // If cachedSpriteInfo is null, the emote will be overlayed at a later time once it's finished being cached
            if (cachedSpriteInfo == null || (cachedSpriteInfo.sprite == null && cachedSpriteInfo.animationInfo == null))
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
                        image.rectTransform.position = currentMessage.gameObject.transform.TransformPoint(pos / pixelsPerUnit - new Vector3(image.preferredWidth / pixelsPerUnit + 2.5f, image.preferredHeight / pixelsPerUnit + 0.7f));

                        image.enabled = true;
                        currentMessage.emoteRenderers.Add(image);
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log($"Exception {e.Message} occured when trying to overlay emote at index {i.ToString()}!");
                }
            }
        }
    };
}
