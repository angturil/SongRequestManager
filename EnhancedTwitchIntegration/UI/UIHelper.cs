using HMUI;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using IPA.Utilities;
using BeatSaberMarkupLanguage;
using TMPro;

namespace SongRequestManager.UI
{
    internal class UIHelper : MonoBehaviour
    {
        public static HoverHint AddHintText(RectTransform parent, string text)
        {
            var hoverHint = parent.gameObject.AddComponent<HoverHint>();
            hoverHint.text = text;
            var hoverHintController = Resources.FindObjectsOfTypeAll<HoverHintController>().First();
            hoverHint.SetField("_hoverHintController", hoverHintController);
            return hoverHint;
        }

        public static Button CreateUIButton(string name, RectTransform parent, string buttonTemplate, Vector2 anchoredPosition, Vector2 sizeDelta, UnityAction onClick = null, string buttonText = "BUTTON", Sprite icon = null)
        {
            var btn = UnityEngine.Object.Instantiate(Resources.FindObjectsOfTypeAll<Button>().Last(x => (x.name == buttonTemplate)), parent, false);
            btn.gameObject.SetActive(true);
            btn.name = name;
            btn.interactable = true;

            var localizer = btn.GetComponentInChildren<Polyglot.LocalizedTextMeshProUGUI>();
            if (localizer != null)
            {
                GameObject.Destroy(localizer);
            }
            BeatSaberMarkupLanguage.Components.ExternalComponents externalComponents = btn.gameObject.AddComponent<BeatSaberMarkupLanguage.Components.ExternalComponents>();
            var textMesh = btn.GetComponentInChildren<TextMeshProUGUI>();
            textMesh.richText = true;
            externalComponents.components.Add(textMesh);

            var contentTransform = btn.transform.Find("Content").GetComponent<LayoutElement>();
            if (contentTransform != null)
            {
                GameObject.Destroy(contentTransform);
            }

            var buttonSizeFitter = btn.gameObject.AddComponent<ContentSizeFitter>();
            buttonSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            buttonSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var stackLayoutGroup = btn.GetComponentInChildren<LayoutGroup>();
            if (stackLayoutGroup != null)
            {
                externalComponents.components.Add(stackLayoutGroup);
            }
            
            btn.onClick.RemoveAllListeners();
            if (onClick != null)
            {
                btn.onClick.AddListener(onClick);
            }

            var btnTransform = btn.transform as RectTransform;
            btnTransform.anchorMin = new Vector2(0.5f, 0.5f);
            btnTransform.anchorMax = new Vector2(0.5f, 0.5f);
            btnTransform.anchoredPosition = anchoredPosition;
            btnTransform.sizeDelta = sizeDelta;

            btn.SetButtonText(buttonText);

            return btn;
        }

        private static Sprite _blankSprite = null;
        public static Sprite BlankSprite
        {
            get
            {
                if (!_blankSprite)
                    _blankSprite = Sprite.Create(Texture2D.blackTexture, new Rect(), Vector2.zero);
                return _blankSprite;
            }
        }
    }

    public static class ButtonExtensions
    {
        #region Button Extensions
        public static void HighlightDeckButton(this Button button, Color color)
        {
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            text.color = color;

            var bgImage = button.GetComponentsInChildren<HMUI.ImageView>().FirstOrDefault(x => x.name == "BG");
            var stroke = button.GetComponentsInChildren<HMUI.ImageView>().FirstOrDefault(x => x.name == "Stroke");

            if (stroke != null)
            {
                color.a = 0.2509804f;

                stroke.color0 = color;
                stroke.color1 = color;
                stroke.color = color;
                stroke.fillMethod = Image.FillMethod.Horizontal;
                stroke.SetAllDirty();
            }
        }

        public static void SetButtonUnderlineColor(this Button parent, Color color)
        {
            HMUI.ImageView img = parent.GetComponentsInChildren<HMUI.ImageView>().FirstOrDefault(x => x.name == "Underline");
            if (img != null)
            {
                img.color = color;
            }
        }
        #endregion
    }

    public static class ViewControllerExtensions
    {
        public static Button CreateUIButton(this HMUI.ViewController parent, string name, string buttonTemplate, Vector2 anchoredPosition, Vector2 sizeDelta, UnityAction onClick = null, string buttonText = "BUTTON")
        {
            var btn = UIHelper.CreateUIButton(name, parent.rectTransform, buttonTemplate, anchoredPosition, sizeDelta, onClick, buttonText);
            return btn;
        }
    }
}
