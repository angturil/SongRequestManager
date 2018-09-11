using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using VRUIControls;
using UnityEngine.UI;

namespace BetterTwitchChat.Chat {
    class LockToggle : MonoBehaviour {
        protected Transform _lockSphere;
        protected VRPointer _vrPointer;
        protected VRController _vrController = null;
        protected Image _lockImage;
        protected bool _alphaNeedsReset = false;
        protected bool _raycastHitTarget = false;
        
        protected const float MaxLaserDistance = 50;
        protected ChatHandler _betterTwitchChat;
        protected static LockToggle _this;

        public void Init(ChatHandler betterTwitchChat, Image lockImage, Transform lockSphere) {
            _this = this;
            _vrPointer = GetComponent<VRPointer>();
            _lockSphere = lockSphere;
            _lockImage = lockImage;
            _betterTwitchChat = betterTwitchChat;
        }

        public void Update() {
            if (this != _this) {
                Destroy(this);
                return;
            }
            if (_vrPointer.vrController != null) {
                if (_vrController != null && _vrPointer.vrController != _vrController) return;
                if (Physics.Raycast(_vrPointer.vrController.transform.position, _vrPointer.vrController.forward, out var hit, MaxLaserDistance)) {
                    if (hit.transform == _lockSphere) {
                        _lockImage.color = _lockImage.color.ColorWithAlpha(0.7f);
                        _raycastHitTarget = true;
                        _alphaNeedsReset = true;
                        if (_vrPointer.vrController.triggerValue > 0.9f && !_vrController) {
                            _vrController = _vrPointer.vrController;

                            Plugin.Instance.Config.LockChatPosition = !Plugin.Instance.Config.LockChatPosition;
                        

                            Plugin.Instance.ShouldWriteConfig = true;
                        }
                    }
                    else {
                        _raycastHitTarget = false;
                    }
                }
                else {
                    _raycastHitTarget = false;
                }

                if (!_raycastHitTarget && _alphaNeedsReset) {
                    _lockImage.color = _lockImage.color.ColorWithAlpha(0.15f);
                    _alphaNeedsReset = false;
                }

                if (_vrController && _vrPointer.vrController.triggerValue < 0.9f) {
                    _vrController = null;
                }
            }
        }
    };
}
