using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SongRequestManager.UI
{
    class Base64Sprites
    {
        public static Sprite InfoIcon;
        public static Sprite VersusChallengeIcon;

        // https://icons8.com/icon/77/info
        public static string InfoBase64 = @"data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAQAAAAAYLlVAAAAAmJLR0QA/4ePzL8AAAKwSURBVGje7ZlNT1NBFIa7xIihF1hSoBoVgQYXVoy606UGlLijV7f1I7W4UEh/AAtD4kYRgtH6sXAl1n+iVFiYICoNpRgMGwNtHjemdeC2nZk799YFb9fvOU96Z86cORMI7EtLWAwygk2cODYjDGL5k/gkY2TI46Q1MiQZ8Cp1JymWkNEiKUJmkx/hKTuoqMRbTphJ3s4cJXRUYpY2t+lvsIEbFbD1kx/iFSb0kmad9GE+Y0pZulTT9/ENk1pV2p5E+IlpbdAvmz7ECl7oB91yS28Rr7QgsRx5LR1uk9uECXOHTWlPuv6+l9UWR8uuY2xJ++zaVa8gHei+4ByX9q3XqI7MKXzPU4IzquCcqZa+V6nmnxW855TOiOPOAGmlFf1Q8E4peZ87n/dqB+4OF8rei4rebYd+gZTyri7ymEtc5onGcT2xF8C78uOkpb29nt+KiAD3fAe4KwJ88B1gXgRYU9wBX4RfQQMgJ14z1LS8awUltf6DYCXAmYYARCsBrihafzHJJOddAgxVAGytAEmXAKMVgFsNAbj5HwE0/BNcbQjAkP42NAPwzzYMNgQgqF+KTQDkxFqW8R3gnQgw5jtAQgQY8B1g91VVYxrgBiBroil1A/DAfVvuBmCbDqebwQvfAJ4534x6FPt7XYBilatZIMCsZIgVLCyayr4mLCzpucp09dtxK+s6Ldlf97Lk5LC91oTgulSQPNccfnkpb6zejCSNl3pTf0bUTNaz9B85KDMn6+CrJ+m/S89L6Xc5onYeVPapzEp7DQ8rV5VfUuhiwVj6T3TqzMsPSBemOsNJqaVXBcKWLE3VK0Ys4E60MaP5ZFNkmlYz70aHecRvxQM3TY/Zl7MQE5JdU5Zxx/PeCEaEBO/JOTfazJOQfpZwCdLCaYaJESfOKMNEadl/0NbTH/bpZxlwkGlLAAAAAElFTkSuQmCC";

        // https://icons8.com/icon/59335/battle
        public static string VersusChallengeBase64 = @"data:image/png;base64,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";

        public static void Init()
        {
            InfoIcon = Base64ToSprite(InfoBase64);
            VersusChallengeIcon = Base64ToSprite(VersusChallengeBase64);
        }

        public static string SpriteToBase64(Sprite input)
        {
            return Convert.ToBase64String(input.texture.EncodeToJPG());
        }

        public static string Texture2DToBase64(Texture2D tex)
        {
            return Convert.ToBase64String(tex.EncodeToJPG());
        }

        public static Sprite Base64ToSprite(string base64)
        {
            // prune base64 encoded image header
            Regex r = new Regex(@"data:image.*base64,");
            base64 = r.Replace(base64, "");

            Sprite s = null;
            try
            {
                Texture2D tex = Base64ToTexture2D(base64);
                s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), (Vector2.one / 2f));
            }
            catch (Exception)
            {
                Console.WriteLine("Exception loading texture from base64 data.");
                s = null;
            }

            return s;
        }

        public static Texture2D Base64ToTexture2D(string encodedData)
        {
            byte[] imageData = Convert.FromBase64String(encodedData);

            int width, height;
            GetImageSize(imageData, out width, out height);

            Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false, true);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.filterMode = FilterMode.Trilinear;
            texture.LoadImage(imageData);
            return texture;
        }

        private static void GetImageSize(byte[] imageData, out int width, out int height)
        {
            width = ReadInt(imageData, 3 + 15);
            height = ReadInt(imageData, 3 + 15 + 2 + 2);
        }

        private static int ReadInt(byte[] imageData, int offset)
        {
            return (imageData[offset] << 8) | imageData[offset + 1];
        }
    }
}
