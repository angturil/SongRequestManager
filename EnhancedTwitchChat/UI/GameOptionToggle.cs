using HMUI;
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using IllusionPlugin;

namespace EnhancedTwitchChat.UI {
    public class GameOptionToggle {
        public GameObject gameObject;

        private HMUI.Toggle _toggle;

        private TextMeshProUGUI _nameText;

        private string _prefKey;

        internal string NameText {
            get {
                return this._nameText.text;
            }
            set {
                this._nameText.text = value;
            }
        }

        internal bool Value { get; set; }

        internal GameOptionToggle(GameObject parent, GameObject target, string prefKey, Sprite icon, string text, bool defaultValue) {
            this.Value = defaultValue;
            this._prefKey = prefKey;
            this.gameObject = NGUIUtil.SetCloneChild(parent, target, prefKey);
            this._toggle = gameObject.GetComponentInChildren<HMUI.Toggle>();
            this._toggle.isOn = this.Value;
            this._nameText = gameObject.GetComponentInChildren<TextMeshProUGUI>();
            this.NameText = text;
            this._toggle.didSwitchEvent += new Action<HMUI.Toggle, bool>(this.HandleNoEnergyToggleDidSwitch);
            foreach (MonoBehaviour obj in gameObject.GetComponentsInChildren<UnityEngine.UI.Image>()) {
                if ("Image".Equals(obj.name)) {
                    ((UnityEngine.UI.Image)obj).sprite = icon;
                    return;
                }
            }
        }

        public virtual void HandleNoEnergyToggleDidSwitch(HMUI.Toggle toggle, bool isOn) {
            this.Value = isOn;
            //Plugin.enableHiddenBlocks = isOn;
            Plugin.Instance.ShouldWriteConfig = true;
        }

    }
}