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
using EnhancedTwitchChat.Textures;
using AsyncTwitch;
using System.Collections;
using static POCs.Sanjay.SharpSnippets.Drawing.ColorExtensions;
using Random = System.Random;

namespace EnhancedTwitchChat.UI
{
    public class CustomImage : RawImage
    {
        public string textureIndex;
        public ImageType imageType;
        public Rect origUV;
    }

    public class CustomText : Text
    {
        public ChatMessage messageInfo;
        public List<CustomImage> emoteRenderers = new List<CustomImage>();
        public bool hasRendered = false;
        ~CustomText()
        {
            foreach (CustomImage i in emoteRenderers)
            {
                Destroy(i.gameObject);
            }
        }
    };

    [RequireComponent(typeof(LayoutElement))]
    public class ContentSizeManager : MonoBehaviour
    {
        private LayoutElement _thisElement;
        public void Init()
        {
            _thisElement = GetComponent<LayoutElement>();
            _thisElement.preferredWidth = Config.Instance.ChatWidth;
        }
    };

    class Drawing
    {
        public static int pixelsPerUnit = 100;
        public static Material noGlowMaterial = null;
        public static Material noGlowMaterialUI = null;
        public static string imageSpacing;
        public static float imageSpacingWidth;

        public static bool MaterialsCached
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

        public static IEnumerator Initialize(Transform parent)
        {
            imageSpacing = "\u200A";
            CustomText tmpText = InitText(imageSpacing, Color.clear, Config.Instance.ChatScale, new Vector2(Config.Instance.ChatWidth, 1), new Vector3(0, -100, 0), new Quaternion(0, 0, 0, 0), parent, TextAnchor.UpperLeft);
            yield return null;
            while (tmpText.preferredWidth < 5.3f)
            {
                tmpText.text += "\u200A";
                yield return null;
            }
            imageSpacingWidth = tmpText.preferredWidth;
            Plugin.Log($"Preferred width was {tmpText.preferredWidth.ToString()} with {tmpText.text.Length.ToString()} spaces");
            imageSpacing = tmpText.text;
            GameObject.Destroy(tmpText.gameObject);
        }

        public static Font LoadSystemFont(string font)
        {
            bool useFallback = false;
            if (font.Length > 0)
            {
                List<string> matchingFonts = Font.GetOSInstalledFontNames().ToList().Where(f => f.ToLower() == font.ToLower()).ToList();
                if (matchingFonts.Count == 0)
                    useFallback = true;
                else
                    font = matchingFonts.First();
            }
            else
                useFallback = true;

            if (useFallback)
            {
                font = "Segoe UI";
                Config.Instance.FontName = font;
                Plugin.Instance.ShouldWriteConfig = true;
                Plugin.Log($"Invalid font name specified! Falling back to Segoe UI");
            }
            return Font.CreateDynamicFontFromOSFont(font, 230);
        }

        public static CustomText InitText(string text, Color textColor, float fontSize, Vector2 sizeDelta, Vector3 position, Quaternion rotation, Transform parent, TextAnchor textAlign, Material mat = null)
        {
            GameObject newGameObj = new GameObject("CustomText");
            CustomText tmpText = newGameObj.AddComponent<CustomText>();
            var mcs = newGameObj.AddComponent<ContentSizeManager>();
            mcs.transform.SetParent(tmpText.rectTransform, false);
            mcs.Init();
            var fitter = newGameObj.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            newGameObj.AddComponent<Shadow>();

            CanvasScaler scaler = newGameObj.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = pixelsPerUnit;
            tmpText.color = textColor;
            tmpText.rectTransform.SetParent(parent.transform, false);
            tmpText.rectTransform.localPosition = position;
            tmpText.rectTransform.localRotation = rotation;
            tmpText.rectTransform.pivot = new Vector2(0, 0);
            tmpText.rectTransform.sizeDelta = sizeDelta;
            tmpText.supportRichText = true;
            tmpText.font = LoadSystemFont(Config.Instance.FontName);
            tmpText.text = text;
            tmpText.fontSize = 230;
            tmpText.verticalOverflow = VerticalWrapMode.Overflow;
            tmpText.alignment = textAlign;
            tmpText.horizontalOverflow = HorizontalWrapMode.Wrap;
            //tmpText.resizeTextForBestFit = true;
            
            if (mat)
                tmpText.material = mat;

            return tmpText;
        }
        
        public static void OverlayImage(CustomText currentMessage, ImageInfo imageInfo)
        {
            CachedTextureData cachedTextureData = TextureDownloader.CachedTextures.ContainsKey(imageInfo.textureIndex) ? TextureDownloader.CachedTextures[imageInfo.textureIndex] : null;

            // If cachedTextureData is null, the emote will be overlayed at a later time once it's finished being cached
            if (cachedTextureData == null || (cachedTextureData.texture == null && cachedTextureData.animationInfo == null))
                return;

            float delay = cachedTextureData.delay;
            bool animatedEmote = cachedTextureData.animationInfo != null;
            foreach (int i in Utilities.IndexOfAll(currentMessage.text, Char.ConvertFromUtf32(imageInfo.swapChar)))
            {
                CustomImage image = null;
                try
                {
                    if (i > 0 && i < currentMessage.text.Count())
                    {
                        image = ChatHandler.Instance.imagePool.Alloc();
                        image.textureIndex = imageInfo.textureIndex;
                        image.imageType = imageInfo.imageType;

                        //image.preserveAspect = true;
                        image.rectTransform.pivot = new Vector2(0, 0);
                        image.texture = cachedTextureData.texture;
                        image.texture.wrapMode = TextureWrapMode.Clamp;

                        if (animatedEmote)
                        {
                            TextureAnimator texAnimator = image.gameObject.GetComponent<TextureAnimator>();
                            if (!texAnimator)
                                texAnimator = image.gameObject.AddComponent<TextureAnimator>();
                            texAnimator.Init(imageInfo.textureIndex, delay, image, cachedTextureData);
                        }

                        image.rectTransform.SetParent(currentMessage.rectTransform, false);

                        float aspectRatio = cachedTextureData.width / cachedTextureData.height;
                        if (aspectRatio > 1)
                            image.rectTransform.localScale = new Vector3(0.064f * aspectRatio, 0.064f, 0.064f); 
                        else
                            image.rectTransform.localScale = new Vector3(0.064f, 0.064f, 0.064f);

                        TextGenerator textGen = currentMessage.cachedTextGenerator;
                        Vector3 pos = new Vector3(textGen.verts[i * 4 + 3].position.x, textGen.verts[i * 4 + 3].position.y);
                        image.rectTransform.position = currentMessage.gameObject.transform.TransformPoint(pos / pixelsPerUnit - new Vector3(cachedTextureData.width / pixelsPerUnit + 2.5f, cachedTextureData.height / pixelsPerUnit + 1f));
                        image.rectTransform.localPosition -= new Vector3(imageSpacingWidth/2.3f, 0);
                        image.color = Config.Instance.TextColor;
                        image.enabled = true;
                        currentMessage.emoteRenderers.Add(image);
                    }
                }
                catch (Exception e)
                {
                    if (image)
                        ChatHandler.Instance.imagePool.Free(image);

                    Plugin.Log($"Exception {e.ToString()} occured when trying to overlay emote at index {i.ToString()}!");
                }
            }
        }

        public static System.Drawing.Color GetPastelShade(System.Drawing.Color source)
        {
            return (generateColor(source, true, new HSB { H = 0, S = 0.2d, B = 255 }, new HSB { H = 360, S = 0.5d, B = 255 }));
        }

        static Random randomizer = new Random();
        private static System.Drawing.Color generateColor(System.Drawing.Color source, bool isaShadeOfSource, HSB min, HSB max)
        {
            HSB hsbValues = ConvertToHSB(new RGB { R = source.R, G = source.G, B = source.B });
            double h_double = randomizer.NextDouble();
            double s_double = randomizer.NextDouble();
            double b_double = randomizer.NextDouble();
            if (max.B - min.B == 0) b_double = 0; //do not change Brightness
            if (isaShadeOfSource)
            {
                min.H = hsbValues.H;
                max.H = hsbValues.H;
                h_double = 0;
            }
            hsbValues = new HSB
            {
                H = Convert.ToDouble(randomizer.Next(Convert.ToInt32(min.H), Convert.ToInt32(max.H))) + h_double,
                S = Convert.ToDouble((randomizer.Next(Convert.ToInt32(min.S * 100), Convert.ToInt32(max.S * 100))) / 100d),
                B = Convert.ToDouble(randomizer.Next(Convert.ToInt32(min.B), Convert.ToInt32(max.B))) + b_double
            };
            //Debug.WriteLine("H:{0} | S:{1} | B:{2} [Min_S:{3} | Max_S{4}]", hsbValues.H, _hsbValues.S, _hsbValues.B, min.S, max.S);
            RGB rgbvalues = ConvertToRGB(hsbValues);
            return System.Drawing.Color.FromArgb(source.A, (int)rgbvalues.R, (int)rgbvalues.G, (int)rgbvalues.B);
        }
    };

}
