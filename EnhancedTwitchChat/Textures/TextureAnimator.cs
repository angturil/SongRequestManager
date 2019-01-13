using EnhancedTwitchChat.UI;
using EnhancedTwitchChat.Utils;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace EnhancedTwitchChat.Textures
{
    public class TextureAnimator : MonoBehaviour
    {
        private CustomImage _image;
        private CachedTextureData _cachedTextureInfo;
            
        void Awake()
        {
            UnityEngine.Object.DontDestroyOnLoad(this);
        }

        public void Init(string textureIndex, float delay, CustomImage image, CachedTextureData cachedTextureInfo)
        {
            _image = image;
            _cachedTextureInfo = cachedTextureInfo;
            _image.texture = cachedTextureInfo.texture;
            enabled = true;
        }
        
        private void FixedUpdate()
        {
            if (!Config.Instance.AnimatedEmotes)
                _image.uvRect = _cachedTextureInfo.animInfo.uvs[0];
            else if (_cachedTextureInfo.animInfo.uvs.Length > 1)
                _image.uvRect = _cachedTextureInfo.animInfo.uvs[AnimationController.Instance.registeredAnimations[_cachedTextureInfo.animInfo.index].uvIndex];
        }
    };
}
