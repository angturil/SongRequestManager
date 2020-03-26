using HMUI;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using IPA.Utilities;
using BeatSaberMarkupLanguage;

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

        public static Button CreateUIButton(RectTransform parent, string buttonTemplate, Vector2 anchoredPosition, Vector2 sizeDelta, UnityAction onClick = null, string buttonText = "BUTTON", Sprite icon = null)
        {
            Button btn = Instantiate(Resources.FindObjectsOfTypeAll<Button>().Last(x => (x.name == buttonTemplate)), parent, false);
            btn.onClick = new Button.ButtonClickedEvent();
            if (onClick != null)
                btn.onClick.AddListener(onClick);
            btn.name = "VersusUIButton";

            (btn.transform as RectTransform).anchorMin = new Vector2(0.5f, 0.5f);
            (btn.transform as RectTransform).anchorMax = new Vector2(0.5f, 0.5f);
            (btn.transform as RectTransform).anchoredPosition = anchoredPosition;
            (btn.transform as RectTransform).sizeDelta = sizeDelta;

            btn.SetButtonText(buttonText);
            if (icon != null)
                btn.SetButtonIcon(icon);

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
}
