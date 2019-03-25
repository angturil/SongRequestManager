using CustomUI.BeatSaber;
using EnhancedTwitchChat.Chat;
using SongRequestManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SongRequestManager
{
    // Experimental chat console
    public class KEYBOARD
    {
        List<KEY> keys = new List<KEY>();

        bool Shift = false;
        bool Caps = false;
        RectTransform container;
        Vector2 currentposition;
        Vector2 baseposition;
        float padding = 0.5f;
        private TextMeshProUGUI KeyboardText;

        KEY AddKey(string keylabel, float width = 12)
        {
            var position = currentposition;
            position.x += width / 4;
            KEY key = new KEY(this, container, position, keylabel, width, Color.white);
            keys.Add(key);
            currentposition.x += width / 2 + padding;
            return key;
        }

        KEY AddKey(string keylabel,string Shifted, float width = 12)
        {
            KEY key = AddKey(keylabel, width);
            key.shifted = Shifted;
            return key;
            
        }


        public KEYBOARD(RectTransform container)
        {
            this.container = container;
            baseposition = new Vector2(-50, 13);
            currentposition = baseposition;

            KeyboardText = BeatSaberUI.CreateText(container, "", new Vector2(0, 30f));
            KeyboardText.fontSize = 6f;
            KeyboardText.color = Color.white;
            KeyboardText.alignment = TextAlignmentOptions.Center;
            KeyboardText.enableWordWrapping = false;
            KeyboardText.text = "";

            // QWERTY
            AddKeys("`1234567890-=","~!@#$%^&*()_+").AddKey("<--", 15);
            NextRow();
            AddKey("TAB", 15f).value="\t";
            AddKeys("QWERTYUIOP");AddKeys("[]\\","{}|");
            NextRow();
            AddKey("CAPS", 20f);
            AddKeys("ASDFGHJKL").AddKeys(";'",":\"").AddKey("ENTER", 20f);
            NextRow();
            AddKey("SHIFT", 25f);
            AddKeys("ZXCVBNM");AddKeys(",./","<>?").AddKey("CLEAR", 28f);
            NextRow();
            //AddKey("BSR", 15f).value = "!bsr ";
            currentposition.x += 23;
            AddKey("!");AddKey("@");  AddKey("SPACE", 40).value = " "; AddKey("#"); AddKey("_");

            return;

            // DVORAK
            AddKeys("`1234567890[]", "~!@#$%^&*(){}").AddKey("<--", 15);
            NextRow();
            AddKey("TAB", 15f).value = "\t";
            AddKeys("',.", "\"<>"); AddKeys("PYFGCRL"); AddKeys("/=\\", "?+|");
            NextRow();
            AddKey("CAPS", 20f);
            AddKeys("AOEUIDHTNS");AddKey("-", "_"); AddKey("ENTER", 20f);
            NextRow();
            AddKey("SHIFT", 25f);
            AddKey(";", ":");  AddKeys("QJKXBMWVZ"); AddKey("CLEAR", 28f);
            NextRow();
            currentposition.x += 23;
            AddKey("!"); AddKey("@"); AddKey("SPACE", 40).value = " "; AddKey("#"); AddKey("_");


        }

        public KEYBOARD NextRow(float adjustx = 0)
        {
            currentposition.y -= 6;
            currentposition.x = baseposition.x;
            return this;
        }
        public KEYBOARD AddKeys(string Keyrow)
        {
            foreach (char c in Keyrow) AddKey(c.ToString().ToLower()).shifted=c.ToString();
            return this;
        }

        public KEYBOARD AddKeys(string Keyrow,string Shifted)
        {

            if (Keyrow.Length!=Shifted.Length)
                {
                // BUG: They should match
                return this;
                }

            for (int i=0;i<Keyrow.Length;i++)
                {
                AddKey(Keyrow[i].ToString(),Shifted[i].ToString());
                }

            return this;
        }


        class KEY
        {

            public string value = "";
            public string shifted = "";

            public Button mybutton;
            public KEY(KEYBOARD kb, RectTransform container, Vector2 position, string text, float width, Color color)
            {
                value = text;

                // Scaling math is not finalized                              

                mybutton = Button.Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "KeyboardButton")), container, false);
                TMP_Text txt = mybutton.GetComponentInChildren<TMP_Text>();
                mybutton.ToggleWordWrapping(false);
                (mybutton.transform as RectTransform).anchoredPosition = position;
                (mybutton.transform as RectTransform).sizeDelta = new Vector2(width, 10);
                mybutton.transform.localScale = new Vector3(0.5f, 0.5f, 1.0f);
                mybutton.SetButtonTextSize(5f);
                mybutton.SetButtonText(text);
                mybutton.GetComponentInChildren<Image>().color = color;

                txt.autoSizeTextContainer = true;

                mybutton.onClick.RemoveAllListeners();

                mybutton.onClick.AddListener(delegate ()
                {

                    // BUG: This needs command specific actions. Temporary hack to make it all work for today

                    if (value == "CLEAR")
                    {
                        kb.KeyboardText.text = "";
                    }

                    else if (value == "<--")
                    {
                        if (kb.KeyboardText.text.Length > 0) kb.KeyboardText.text = kb.KeyboardText.text.Substring(0, kb.KeyboardText.text.Length - 1); // Is there a cleaner way to say this?
                    }

                    else if (value == "SHIFT")
                    {
                        kb.Shift = !kb.Shift;
                        mybutton.GetComponentInChildren<Image>().color = kb.Shift ? Color.green : Color.white ;
                    }

                    else if (value == "CAPS")
                    {
                        kb.Caps = ! kb.Caps;
                        mybutton.GetComponentInChildren<Image>().color = kb.Caps ? Color.green : Color.white;
                    }

                    else if (value == "ENTER")
                    {
                        var typedtext = kb.KeyboardText.text;
                        if (typedtext != "")
                        {
                            if (typedtext[0] == '!')
                            {
                                RequestBot.COMMAND.Parse(TwitchWebSocketClient.OurTwitchUser, typedtext);
                            }
                            else
                            {
                                TwitchWebSocketClient.SendMessage(typedtext);
                            }

                            kb.KeyboardText.text = "";
                        }
                    }
                    else
                    {
                        string x = kb.Shift ? shifted : value;
                        if (x == "") x = value;
                        if (kb.Caps) x = value.ToUpper(); 
                        kb.KeyboardText.text += x;
                    }
                });
                HoverHint _MyHintText = BeatSaberUI.AddHintText(mybutton.transform as RectTransform, value);
            }
        }
    }
}
