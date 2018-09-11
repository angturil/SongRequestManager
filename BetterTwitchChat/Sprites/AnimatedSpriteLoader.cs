using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using BetterTwitchChat.Utils;

namespace BetterTwitchChat.Sprites {
    class AnimatedSpriteLoader : MonoBehaviour {
        public static ConcurrentDictionary<string, string> BTTVAnimatedEmoteIDs = new ConcurrentDictionary<string, string>();
        private ConcurrentStack<SpriteDownloadInfo> _spriteDownloadQueue = new ConcurrentStack<SpriteDownloadInfo>();

        void Awake() {
            UnityEngine.Object.DontDestroyOnLoad(this);
        }

        void Update() {
            // Download any emotes we need cached for one of our messages
            if (_spriteDownloadQueue.Count > 0) {
                if (_spriteDownloadQueue.TryPop(out var spriteDownloadInfo)) {
                    switch (spriteDownloadInfo.type) {
                        case ImageType.BTTV_Animated:
                            StartCoroutine(SpriteLoader.Download($"https://cdn.betterttv.net/emote/{spriteDownloadInfo.index.Substring(2)}/3x", spriteDownloadInfo));
                            break;
                    }
                }
            }
        }

        public void Queue(SpriteDownloadInfo emote) {
            _spriteDownloadQueue.Push(emote);
        }
    };
}