using HMUI;
using UnityEngine;

namespace SongRequestManager.UI
{
    public class KeyboardViewController : ViewController
    {
        protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            if (firstActivation)
            {
                RectTransform KeyboardContainer = new GameObject("KeyboardContainer", typeof(RectTransform)).transform as RectTransform;
                KeyboardContainer.SetParent(rectTransform, false);
                KeyboardContainer.sizeDelta = new Vector2(60f, 40f);

                var mykeyboard = new KEYBOARD(KeyboardContainer, "");

#if UNRELEASED
                //mykeyboard.AddKeys(BOTKEYS, 0.4f);
                RequestBot.AddKeyboard(mykeyboard, "emotes.kbd", 0.4f);
#endif
                mykeyboard.AddKeys(KEYBOARD.QWERTY); // You can replace this with DVORAK if you like
                mykeyboard.DefaultActions();

#if UNRELEASED
                const string SEARCH = @"

[CLEAR SEARCH]/0 /2 [NEWEST]/0 /2 [UNFILTERED]/30 /2 [PP]/0'!addsongs/top/pp pp%CR%' /2 [SEARCH]/0";

#else
                const string SEARCH = @"

[CLEAR SEARCH]/0 /2 [NEWEST]/0 /2 [UNFILTERED]/30 /2 [SEARCH]/0";

#endif


                mykeyboard.SetButtonType("OkButton"); // Adding this alters button positions??! Why?
                mykeyboard.AddKeys(SEARCH, 0.75f);

                mykeyboard.SetAction("CLEAR SEARCH", RequestBot.ClearSearch);
                mykeyboard.SetAction("UNFILTERED", RequestBot.UnfilteredSearch);
                mykeyboard.SetAction("SEARCH", RequestBot.MSD);
                mykeyboard.SetAction("NEWEST", RequestBot.Newest);


#if UNRELEASED
                RequestBot.AddKeyboard(mykeyboard, "decks.kbd", 0.4f);
#endif

                // The UI for this might need a bit of work.
                RequestBot.AddKeyboard(mykeyboard, "RightPanel.kbd");
            }
        }
    }
}
