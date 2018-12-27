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
        private string _textureIndex;
        private CachedTextureData _cachedTextureInfo;
            
        void Awake()
        {
            UnityEngine.Object.DontDestroyOnLoad(this);
        }

        public void Init(string textureIndex, float delay, CustomImage image, CachedTextureData cachedTextureInfo)
        {
            _textureIndex = textureIndex;
            _cachedTextureInfo = cachedTextureInfo;
            _image = image;
            _image.texture = cachedTextureInfo.texture;
            if(cachedTextureInfo.animationInfo.Length > 1)
                InvokeRepeating("UpdateAnimation", 0, delay);
            enabled = true;
        }
        
        private void UpdateAnimation()
        {
            _image.uvRect = _cachedTextureInfo.animationInfo[AnimationController.Instance.registeredAnimations[_cachedTextureInfo.animIndex].uvIndex];
        }
    };
}
