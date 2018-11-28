using EnhancedTwitchChat.Utils;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace EnhancedTwitchChat.Sprites {
    public class AnimationData {
        public Sprite sprite;
        public float delay;
        public AnimationData(Sprite sprite, float delay) {
            this.sprite = sprite;
            this.delay = delay;
        }
    };

    public class AnimSaveData {
        public string emoteIndex;
        public List<AnimationData> sprites;
        public AnimSaveData(string emoteIndex, List<AnimationData> textures) {
            this.emoteIndex = emoteIndex;
            this.sprites = textures;
        }
    };

    class GifInfo {
        public List<FrameInfo> frames = new List<FrameInfo>();
        public int frameCount = 0;
        public System.Drawing.Imaging.FrameDimension dimension;
        public bool initialized = false;
    };

    class FrameInfo {
        public System.Drawing.Bitmap frame;
        public List<System.Drawing.Color> colors = new List<System.Drawing.Color>();
        public float delay = 0;
        public FrameInfo(System.Drawing.Bitmap frame) {
            this.frame = frame;
        }
    };

    class AnimatedSpriteDecoder {
        public static IEnumerator Process(byte[] gifData, Action<List<AnimationData>, string> callback, string emoteIndex) {
            List<AnimationData> gifTexList = new List<AnimationData>();
            GifInfo frameInfo = new GifInfo();
            DateTime startTime = DateTime.Now;
            new Thread(() => ProcessingThread(gifData, ref frameInfo)).Start();

            while (!frameInfo.initialized) {
                yield return null;
            }

            for (int i = 0; i < frameInfo.frameCount; i++) {
                while (frameInfo.frames.Count - 1 < i) {
                    //Plugin.Log("Processing thread couldn't keep up");
                    yield return null;
                }

                FrameInfo currentFrameInfo = frameInfo.frames[i];
                var frameTexture = new Texture2D(currentFrameInfo.frame.Width, currentFrameInfo.frame.Height);
                int colorIndex = 0;
                for (int x = 0; x < currentFrameInfo.frame.Width; x++) {
                    for (int y = 0; y < currentFrameInfo.frame.Height; y++) {
                        System.Drawing.Color sourceColor = currentFrameInfo.colors[colorIndex];
                        frameTexture.SetPixel(x, currentFrameInfo.frame.Height - 1 - y, new Color32(sourceColor.R, sourceColor.G, sourceColor.B, sourceColor.A));
                        colorIndex++;
                    }
                }
                frameTexture.Apply();

                yield return null;

                frameTexture.wrapMode = TextureWrapMode.Clamp;
                gifTexList.Add(new AnimationData(Sprite.Create(frameTexture, new Rect(0, 0, frameTexture.width, frameTexture.height), new Vector2(0, 0), ChatHandler.Instance.pixelsPerUnit), currentFrameInfo.delay));
                
                if (callback != null && i == 0) {
                    callback(new List<AnimationData>() { gifTexList[0] }, emoteIndex);
                }
            }

            yield return null;

            if (callback != null) {
                callback(gifTexList, emoteIndex);
            }

            Plugin.Log($"Finished decoding gif! Elapsed time: {(DateTime.Now - startTime).TotalSeconds.ToString()} seconds.");
            yield break;
        }
        
        private static void ProcessingThread(byte[] gifData, ref GifInfo frameInfo) {
            var gifImage = Utilities.byteArrayToImage(gifData);
            var dimension = new System.Drawing.Imaging.FrameDimension(gifImage.FrameDimensionsList[0]);
            int frameCount = gifImage.GetFrameCount(dimension);
            
            frameInfo.frameCount = frameCount;
            frameInfo.dimension = dimension;
            frameInfo.initialized = true;

            int index = 0;
            for (int i = 0; i < frameCount; i++) {
                gifImage.SelectActiveFrame(dimension, i);
                var frame = new System.Drawing.Bitmap(gifImage.Width, gifImage.Height);
                System.Drawing.Graphics.FromImage(frame).DrawImage(gifImage, System.Drawing.Point.Empty);

                FrameInfo currentFrame = new FrameInfo(frame);
                for (int x = 0; x < frame.Width; x++) {
                    for (int y = 0; y < frame.Height; y++) {
                        System.Drawing.Color sourceColor = frame.GetPixel(x, y);
                        currentFrame.colors.Add(sourceColor);
                    }
                }

                currentFrame.delay = (float)BitConverter.ToInt32(gifImage.GetPropertyItem(20736).Value, index) / 100.0f;
                frameInfo.frames.Add(currentFrame);
                index += 4;
                Thread.Sleep(7);
            }
        }
    };
}
