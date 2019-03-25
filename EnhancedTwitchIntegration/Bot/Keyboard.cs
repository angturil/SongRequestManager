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
        float buttonwidth = 12f;
        private TextMeshProUGUI KeyboardText;

        KEY dummy = new KEY(); 

        // Keyboard spaces and CR/LF are significicant.
        // A slash following a space or CR/LF alters the width of the space

        public const string QWERTY =
@"
(`~) (1!) (2@) (3#) (4$) (5%) (6^) (7&) (8*) (9() (0)) (-_) (=+) [<--]/15
[TAB]/15 (qQ) (wW) (eE) (rR) (tT) (yY) (uU) (iI) (oO) (pP) ([{) (]}) (\|)
[CAPS]/20 (aA) (sS) (dD) (fF) (gG) (hH) (jJ) (kK) (lL) (;:) ('"") [ENTER]/20
[SHIFT]/25 (zZ) (xX) (cC) (vV) (bB) (nN) (mM) (,<) (.>) (/?) [CLEAR]/28
/23 (!!) (@@) [SPACE]/40 (##) (__)";

        public const string FROW =
@"
[Esc] /2 [F1] [F2] [F3] [F4] /2 [F5] [F6] [F7] [F8] /2 [F9] [F10] [F11] [F12]
";

        public const string NUMPAD =
@"
[NUM] (//) (**) (--)
(77) (88) (99) (++)
(44) (55) (66)
(11) (22) (33) [ENTER]
";

        public KEY this[string index]
        {
            get 
            {
                foreach (KEY key in keys) if (key.name == index) return key;
                Plugin.Log($"Keyboard: Unable to set property of Key  [{index}]");

                return dummy;
            }

        }

        KEY AddKey(string keylabel, float width = 12)
        {
            var position = currentposition;
            position.x += width / 4;
            KEY key = new KEY(this, container, position, keylabel, width, Color.white);
            keys.Add(key);
            currentposition.x += width / 2 + padding;
            return key;
        }

        KEY AddKey(string keylabel, string Shifted, float width = 12)
        {
            KEY key = AddKey(keylabel, width);
            key.shifted = Shifted;
            return key;
        }


        // BUG: Refactor this within a keybard parser subclass once everything works.
        void EmitKey(ref float spacing,ref float Width, ref string Label, ref string Key,ref bool space)
            {
            currentposition.x += spacing;
            if (Label != "") AddKey(Label, Width);
            else if (Key != "") AddKey(Key[0].ToString(), Key[1].ToString());
            spacing = 0;
            Width = buttonwidth;
            Label = "";
            Key = "";
            space = false;
            return;
            }

        bool ReadFloat(ref String data, ref int Position,ref float result)
           {
            if (Position >= data.Length) return false;
            int start = Position;
            while (Position<data.Length)
                {
                char c = data[Position];
                if (!(c >= '0' && c <= '9' || c == '+' || c == '-' || c == '.')) break;
                Position++;
                }


           if (float.TryParse(data.Substring(start, Position - start),out result)) return true ;

           Position = start;
           return false;
           }

        // Very basic parser for the keyboard grammar - no doubt can be improved. Tricky to implement because of special characters.
        // It might possible to make grep do this, but it would be even harder to read than this!
        KEYBOARD AddKeyboard(string Keyboard)
            {
            bool space = true;
            float spacing = 0;
            float width = buttonwidth;
            string Label = "";
            string Key = "";
        
            int p = 0; // P is for parser
            while (p<Keyboard.Length)
                {

                switch (Keyboard[p])
                {
                    case '\r':
                        space = true;
                        break;

                    case '\n':
                        EmitKey(ref spacing, ref width, ref Label, ref Key, ref space);
                        space = true;
                        NextRow();
                        break;

                    case ' ':
                        space = true;
                        spacing += padding;
                        break;

                    case '[':
                        EmitKey(ref spacing, ref width, ref Label, ref Key,ref space);

                       space = false;
                       p++;
                       int label = p;
                       while (p < Keyboard.Length && Keyboard[p] != ']') p++;
                       Label = Keyboard.Substring(label, p - label);
                       break;

                    case '(':
                        EmitKey(ref spacing, ref width, ref Label, ref Key,ref space);

                        p++;
                        Key = Keyboard.Substring(p, 2);
                        p += 2;
                        space = false;
                        break;                        

                    case '/':
                                               
                        p++;
                        float number=0;
                        if (ReadFloat(ref Keyboard,ref p,ref number))
                            {

                            if (space)
                                {
                                if (Label!="" || Key!="") EmitKey(ref spacing, ref width, ref Label, ref Key, ref space);
                                spacing = number;
                                }
                            else width = number;
                            continue;
                            } 

                        break;                        
   
                    default:
                        Plugin.Log($"Unable to parse keyboard at position {p} char [{Keyboard[p]}]: [{Keyboard}]" );                    
                        return this;
                    }

                p++;
                }

            EmitKey(ref spacing, ref width, ref Label, ref Key,ref space);

            return this;
            }

        public KEYBOARD(RectTransform container)
        {
            this.container = container;
            baseposition = new Vector2(-50, 23);
            currentposition = baseposition;
            bool addhint = true;

            KeyboardText = BeatSaberUI.CreateText(container, "", new Vector2(0, 30f));
            KeyboardText.fontSize = 6f;
            KeyboardText.color = Color.white;
            KeyboardText.alignment = TextAlignmentOptions.Center;
            KeyboardText.enableWordWrapping = false;
            KeyboardText.text = "";

            // We protect this since setting nonexistent keys will throw.
            try
            {
                AddKeyboard(FROW);
            
                AddKeyboard(QWERTY);                

                // These currently do fault if the key is missing.
                this["SPACE"].Set(" ");
                this["TAB"].Set("\t");
                this["CLEAR"].keyaction = Clear;
                this["ENTER"].keyaction = Enter;
                this["<--"].keyaction = Backspace;
                this["SHIFT"].keyaction = SHIFT;
                this["CAPS"].keyaction = CAPS;
            }
            catch (Exception ex)
            {
                Plugin.Log(ex.ToString());
            }

            return;

            // QWERTY
            AddKeys("`1234567890-=", "~!@#$%^&*()_+").AddKey("<--", 15);
            NextRow();
            AddKey("TAB", 15f).value = "\t";
            AddKeys("QWERTYUIOP"); AddKeys("[]\\", "{}|");
            NextRow();
            AddKey("CAPS", 20f);
            AddKeys("ASDFGHJKL").AddKeys(";'", ":\"").AddKey("ENTER", 20f);
            NextRow();
            AddKey("SHIFT", 25f);
            AddKeys("ZXCVBNM"); AddKeys(",./", "<>?").AddKey("CLEAR", 28f);
            NextRow();
            //AddKey("BSR", 15f).value = "!bsr ";
            currentposition.x += 23;
            AddKey("!"); AddKey("@"); AddKey("SPACE", 40).value = " "; AddKey("#"); AddKey("_");

            return;

            // DVORAK
            AddKeys("`1234567890[]", "~!@#$%^&*(){}").AddKey("<--", 15);
            NextRow();
            AddKey("TAB", 15f).value = "\t";
            AddKeys("',.", "\"<>"); AddKeys("PYFGCRL"); AddKeys("/=\\", "?+|");
            NextRow();
            AddKey("CAPS", 20f);
            AddKeys("AOEUIDHTNS"); AddKey("-", "_"); AddKey("ENTER", 20f);
            NextRow();
            AddKey("SHIFT", 25f);
            AddKey(";", ":"); AddKeys("QJKXBMWVZ"); AddKey("CLEAR", 28f);
            NextRow();
            currentposition.x += 23;
            AddKey("!"); AddKey("@"); AddKey("SPACE", 40).value = " "; AddKey("#"); AddKey("_");

            // Keyboard grammar:
            //
            // 

            // NOTE: Spaces are significant. You can use the short form for alpha characters. 



          

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

        void Clear(KEY key)
            {
            key.kb.KeyboardText.text = "";
            }

        void Enter(KEY key)
        {
            var typedtext = key.kb.KeyboardText.text;
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

                key.kb.KeyboardText.text = "";
            }
        }

        void Backspace(KEY key)
            {
                // BUG: This is terribly long winded... 
                if (key.kb.KeyboardText.text.Length > 0) key.kb.KeyboardText.text = key.kb.KeyboardText.text.Substring(0, key.kb.KeyboardText.text.Length - 1); // Is there a cleaner way to say this?
            }
        void SHIFT(KEY key)
        {
            key.kb.Shift = !key.kb.Shift;
            key.mybutton.GetComponentInChildren<Image>().color = key.kb.Shift ? Color.green : Color.white;
        }

        void CAPS(KEY key)
            {
            key.kb.Caps = ! key.kb.Caps;
            key.mybutton.GetComponentInChildren<Image>().color = key.kb.Caps? Color.green : Color.white;
            }


        public class KEY
        {
            public string name = "";
            public string value = "";
            public string shifted = "";
            public Button mybutton;
            public KEYBOARD kb;
            public Action<KEY> keyaction = null;

            public KEY Set(string Value)
            {
                this.value = Value;
                this.shifted = Value;
                return this;
            }


            public KEY()
            {
            // This key is not intialized at all
            }

            public KEY(KEYBOARD kb, RectTransform container, Vector2 position, string text, float width, Color color)
            {
                value = text;
                this.kb = kb;

                // Scaling math is not finalized                              
                name = text;
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

                    if (keyaction!=null) 
                    {
                        keyaction(this);
                        return;
                    }

                    // BUG: This needs command specific actions. Temporary hack to make it all work for today

 
                   

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
