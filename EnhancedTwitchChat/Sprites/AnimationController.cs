using EnhancedTwitchChat.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace EnhancedTwitchChat.Sprites
{
    class AnimControllerData
    {
        public List<AnimationData> animationInfo;
        public int index = 0;
        public DateTime lastSwitch = DateTime.Now;
        public AnimControllerData(List<AnimationData> animation)
        {
            this.animationInfo = animation;
        }
    };

    class AnimationController : MonoBehaviour
    {
        public static AnimationController Instance = null;

        private List<AnimControllerData> _registeredAnimations = new List<AnimControllerData>();
        void Awake()
        {
            UnityEngine.Object.DontDestroyOnLoad(this);

            if (Instance == null) Instance = this;
            else Destroy(this);
        }

        public void Register(List<AnimationData> _animation)
        {
            _registeredAnimations.Add(new AnimControllerData(_animation));
        }

        public Sprite Get(List<AnimationData> animation)
        {
            foreach (AnimControllerData _animation in _registeredAnimations)
            {
                if (_animation.animationInfo == animation)
                    return _animation.animationInfo[_animation.index].sprite;
            }
            return null;
        }

        void Update()
        {
            foreach (AnimControllerData animation in _registeredAnimations)
            {
                var difference = DateTime.Now - animation.lastSwitch;

                if ((float)difference.Milliseconds / 1000 >= animation.animationInfo[animation.index].delay)
                {
                    animation.lastSwitch = DateTime.Now;
                    animation.index++;

                    if (animation.index >= animation.animationInfo.Count)
                        animation.index = 0;
                }
            }
        }
    };
}
