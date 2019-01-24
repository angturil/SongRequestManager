using CustomUI.Utilities;
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

namespace EnhancedTwitchChat.Textures
{
    class AnimationDecoder
    {
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

        private static int GetTextureSize(GifInfo frameInfo, int i)
        {
            int testNum = 2;
        retry:
            int numFrames = frameInfo.frameCount;
            // Make sure the number of frames is cleanly divisible by our testNum
            if (!(numFrames % testNum != 0))
                numFrames += numFrames % testNum;

            int numFramesInRow = numFrames / testNum;
            int numFramesInColumn = numFrames / numFramesInRow;

            if (numFramesInRow > numFramesInColumn)
            {
                testNum += 2;
                goto retry;
            }

            var textureWidth = Mathf.Clamp(numFramesInRow * frameInfo.frames[i].frame.Width, 0, 2048);
            var textureHeight = Mathf.Clamp(numFramesInColumn * frameInfo.frames[i].frame.Height, 0, 2048);
            return Mathf.Max(textureWidth, textureHeight);
        }

        public static IEnumerator Process(byte[] gifData, Action<Texture2D, Rect[], float, TextureDownloadInfo> callback, TextureDownloadInfo imageDownloadInfo)
        {
            Plugin.Log($"Started decoding gif {imageDownloadInfo.spriteIndex}");

            List<Texture2D> texList = new List<Texture2D>();
            GifInfo frameInfo = new GifInfo();
            DateTime startTime = DateTime.Now;
            Task.Run(() => ProcessingThread(gifData, ref frameInfo));
            yield return new WaitUntil(() => { return frameInfo.initialized; });


            int textureSize = 2048;
            Texture2D texture = null;
            float delay = -1f;
            for (int i = 0; i < frameInfo.frameCount; i++)
            {
                yield return new WaitUntil(() => { return frameInfo.frames.Count > i; });
                //Plugin.Log($"Frame {i} is ready for processing! Frame is {frameInfo.frames[i].frame.Width}x{frameInfo.frames[i].frame.Height}");

                if(texture == null)
                {
                    textureSize = GetTextureSize(frameInfo, i);
                    texture = new Texture2D(textureSize, textureSize);
                }

                FrameInfo currentFrameInfo = frameInfo.frames[i];
                if (delay == -1f)
                    delay = currentFrameInfo.delay;
                
                var frameTexture = new Texture2D(currentFrameInfo.frame.Width, currentFrameInfo.frame.Height);
                try
                {
                    int colorIndex = 0;
                    Color32[] colors = new Color32[currentFrameInfo.frame.Width * currentFrameInfo.frame.Height];
                    for (int x = 0; x < currentFrameInfo.frame.Width; x++)
                    {
                        for (int y = 0; y < currentFrameInfo.frame.Height; y++)
                        {
                            System.Drawing.Color sourceColor = currentFrameInfo.colors[colorIndex];
                            colors[(currentFrameInfo.frame.Height - y - 1) * currentFrameInfo.frame.Width + x] = new Color32(sourceColor.R, sourceColor.G, sourceColor.B, sourceColor.A);
                            colorIndex++;
                        }
                    }
                    frameTexture.wrapMode = TextureWrapMode.Clamp;
                    frameTexture.SetPixels32(colors);
                    frameTexture.Apply(i == 0);
                }
                catch (Exception e)
                {
                    Plugin.Log($"Exception while decoding gif! Frame: {i}, Exception: {e}");
                    yield break;
                }
                yield return null;
                
                texList.Add(frameTexture);
                
                // Instant callback after we decode the first frame in order to display a still image until the animated one is finished loading
                if (i == 0)
                   callback?.Invoke(frameTexture, texture.PackTextures(new Texture2D[] { frameTexture }, 2, textureSize, true), delay, imageDownloadInfo);
            }
            Rect[] atlas = texture.PackTextures(texList.ToArray(), 2, textureSize, true);

            yield return null;

            callback?.Invoke(texture, atlas, delay, imageDownloadInfo);
            Plugin.Log($"Finished decoding gif {imageDownloadInfo.spriteIndex}! Elapsed time: {(DateTime.Now - startTime).TotalSeconds} seconds.");
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

                Thread.Sleep(25);
            }
        }
    };
}
