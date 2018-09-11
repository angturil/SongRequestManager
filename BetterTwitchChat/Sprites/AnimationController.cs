using BetterTwitchChat.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BetterTwitchChat.Sprites {
    class AnimControllerData {
        public List<AnimationData> animationInfo;
        public int index = 0;
        public DateTime lastSwitch = DateTime.Now;
        public AnimControllerData(List<AnimationData> animation) {
            this.animationInfo = animation;
        }
    };

    class AnimationController : MonoBehaviour {
        private List<AnimControllerData> _registeredAnimations = new List<AnimControllerData>();
        public void Register(List<AnimationData> _animation) {
            _registeredAnimations.Add(new AnimControllerData(_animation));
        }

        public Sprite Get(List<AnimationData> animation) {
            foreach (AnimControllerData _animation in _registeredAnimations) {
                if (_animation.animationInfo == animation) {
                    return _animation.animationInfo[_animation.index].sprite;
                }
            }
            return null;
        }

        void Update() {
            foreach (AnimControllerData aci in _registeredAnimations) {
                var difference = DateTime.Now - aci.lastSwitch;

                if ((float)difference.Milliseconds / 1000 >= aci.animationInfo[aci.index].delay) {
                    aci.lastSwitch = DateTime.Now;
                    aci.index++;

                    if (aci.index >= aci.animationInfo.Count) {
                        aci.index = 0;
                    }
                        
                }
            }
        }
    };
}
