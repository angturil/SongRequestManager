using EnhancedTwitchChat.UI;
using EnhancedTwitchChat.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR;

namespace EnhancedTwitchChat.Textures
{
    class AnimControllerData
    {
        public string textureIndex;
        public int animCount = 0;
        public float delay = 10;
        public int uvIndex = 0;
        public DateTime lastSwitch = DateTime.Now;
        public AnimControllerData(string textureIndex, int animCount, float delay)
        {
            this.textureIndex = textureIndex;
            this.animCount = animCount;
            this.delay = delay;
        }
    };

    class AnimationController : MonoBehaviour
    {
        public static AnimationController Instance = null;

        public List<AnimControllerData> registeredAnimations = new List<AnimControllerData>();
        void Awake()
        {
            UnityEngine.Object.DontDestroyOnLoad(this);

            if (Instance == null) Instance = this;
            else Destroy(this);
        }

        public int Register(string textureIndex, int animCount, float delay)
        {
            AnimControllerData newAnim = new AnimControllerData(textureIndex, animCount, delay);
            registeredAnimations.Add(newAnim);
            return registeredAnimations.IndexOf(newAnim);
        }

        void Update()
        {
            foreach (AnimControllerData animation in registeredAnimations)
            {
                var difference = DateTime.Now - animation.lastSwitch;

                if ((float)difference.Milliseconds / 1000 >= animation.delay)
                {
                    animation.lastSwitch = DateTime.Now;
                    animation.uvIndex++;

                    if (animation.uvIndex >= animation.animCount)
                        animation.uvIndex = 0;
                }
            }
        }
    };
}
