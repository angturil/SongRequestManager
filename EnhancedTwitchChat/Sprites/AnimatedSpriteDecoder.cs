using EnhancedTwitchChat.UI;
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

namespace EnhancedTwitchChat.Sprites
{
    public class AnimationData
    {
        public Sprite sprite;
        public float delay;
        public AnimationData(Sprite sprite, float delay)
        {
            this.sprite = sprite;
            this.delay = delay;
        }
    };

    public class AnimSaveData
    {
        public string emoteIndex;
        public List<AnimationData> sprites;
        public AnimSaveData(string emoteIndex, List<AnimationData> textures)
        {
            this.emoteIndex = emoteIndex;
            this.sprites = textures;
        }
    };

    class GifInfo
    {
        public List<FrameInfo> frames = new List<FrameInfo>();
        public int frameCount = 0;
        public System.Drawing.Imaging.FrameDimension dimension;
        public bool initialized = false;
    };

    class FrameInfo
    {
        public System.Drawing.Bitmap frame;
        public List<System.Drawing.Color> colors = new List<System.Drawing.Color>();
        public float delay = 0;
        public FrameInfo(System.Drawing.Bitmap frame)
        {
            this.frame = frame;
        }
    };

    class AnimatedSpriteDecoder
    {
        public static IEnumerator Process(byte[] gifData, Action<List<AnimationData>, SpriteDownloadInfo> callback, SpriteDownloadInfo spriteDownloadInfo)
        {
            List<AnimationData> gifTexList = new List<AnimationData>();
            GifInfo frameInfo = new GifInfo();
            DateTime startTime = DateTime.Now;
            new Thread(() => ProcessingThread(gifData, ref frameInfo)).Start();

            while (!frameInfo.initialized)
                yield return null;

            for (int i = 0; i < frameInfo.frameCount; i++)
            {
                // The processing thread is purposely slowed down to reduce CPU load, so yield here as it won't keep up with the game if it's running at 90fps
                while (frameInfo.frames.Count - 1 < i)
                    yield return null;

                FrameInfo currentFrameInfo = frameInfo.frames[i];
                var frameTexture = new Texture2D(currentFrameInfo.frame.Width, currentFrameInfo.frame.Height);
                int colorIndex = 0;
                for (int x = 0; x < currentFrameInfo.frame.Width; x++)
                {
                    for (int y = 0; y < currentFrameInfo.frame.Height; y++)
                    {
                        System.Drawing.Color sourceColor = currentFrameInfo.colors[colorIndex];
                        frameTexture.SetPixel(x, currentFrameInfo.frame.Height - 1 - y, new Color32(sourceColor.R, sourceColor.G, sourceColor.B, sourceColor.A));
                        colorIndex++;
                    }
                }
                frameTexture.Apply();

                // Yield here for a frame to allow the texture data to be populated
                yield return null;

                frameTexture.wrapMode = TextureWrapMode.Clamp;
                gifTexList.Add(new AnimationData(Sprite.Create(frameTexture, new Rect(0, 0, frameTexture.width, frameTexture.height), new Vector2(0, 0), Drawing.pixelsPerUnit), currentFrameInfo.delay));

                // Instant callback after we decode the first frame in order to display a still image until the animated one is finished loading
                if (callback != null && i == 0)
                    callback(new List<AnimationData>() { gifTexList[0] }, spriteDownloadInfo);
            }

            yield return null;

            if (callback != null)
                callback(gifTexList, spriteDownloadInfo);

            Plugin.Log($"Finished decoding gif! Elapsed time: {(DateTime.Now - startTime).TotalSeconds.ToString()} seconds.");
            yield break;
        }

        private static void ProcessingThread(byte[] gifData, ref GifInfo frameInfo)
        {
            var gifImage = Utilities.byteArrayToImage(gifData);
            var dimension = new System.Drawing.Imaging.FrameDimension(gifImage.FrameDimensionsList[0]);
            int frameCount = gifImage.GetFrameCount(dimension);

            frameInfo.frameCount = frameCount;
            frameInfo.dimension = dimension;
            frameInfo.initialized = true;

            int index = 0;
            for (int i = 0; i < frameCount; i++)
            {
                gifImage.SelectActiveFrame(dimension, i);
                var frame = new System.Drawing.Bitmap(gifImage.Width, gifImage.Height);
                System.Drawing.Graphics.FromImage(frame).DrawImage(gifImage, System.Drawing.Point.Empty);

                FrameInfo currentFrame = new FrameInfo(frame);
                for (int x = 0; x < frame.Width; x++)
                {
                    for (int y = 0; y < frame.Height; y++)
                    {
                        System.Drawing.Color sourceColor = frame.GetPixel(x, y);
                        currentFrame.colors.Add(sourceColor);
                    }
                }

                int delayPropertyValue = BitConverter.ToInt32(gifImage.GetPropertyItem(20736).Value, index);
                // If the delay property is 0, assume that it's a 10fps emote
                if (delayPropertyValue == 0)
                    delayPropertyValue = 10;
                
                currentFrame.delay = (float)delayPropertyValue / 100.0f;
                frameInfo.frames.Add(currentFrame);
                index += 4;
                Thread.Sleep(7);
            }
        }
    };
}
