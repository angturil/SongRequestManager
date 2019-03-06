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
        public float delay = 10;
        public int uvIndex = 0;
        public DateTime lastSwitch = DateTime.Now;
        public Rect[] uvs;
        public AnimControllerData(string textureIndex, Rect[] uvs, float delay)
        {
            this.textureIndex = textureIndex;
            this.delay = delay;
            this.uvs = uvs;
        }
    };

    class AnimationController : MonoBehaviour
    {
        public static AnimationController Instance = null;

        public static void OnLoad()
        {
            if (Instance) return;

            new GameObject("EnhancedTwitchChatAnimController").AddComponent<AnimationController>();
        }

        public List<AnimControllerData> registeredAnimations = new List<AnimControllerData>();
        void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public int Register(string textureIndex, Rect[] uvs, float delay)
        {
            AnimControllerData newAnim = new AnimControllerData(textureIndex, uvs, delay);
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
                    
                    if (animation.uvIndex >= animation.uvs.Length)
                        animation.uvIndex = 0;

                    Rect uv = animation.uvs[animation.uvIndex];
                    ImageDownloader.CachedTextures[animation.textureIndex].animInfo.shadowMaterial?.SetVector("_CropFactors", new Vector4(uv.x, uv.y, uv.width, uv.height));
                    ImageDownloader.CachedTextures[animation.textureIndex].animInfo.imageMaterial?.SetVector("_CropFactors", new Vector4(uv.x, uv.y, uv.width, uv.height));
                }
            }
        }
    };
}
