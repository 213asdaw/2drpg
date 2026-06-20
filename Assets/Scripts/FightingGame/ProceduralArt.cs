using UnityEngine;

namespace FightingGame
{
    public static class ProceduralArt
    {
        public static Sprite CreateRectSprite(int width, int height, Color color, string name)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = name + "_Tex"
            };

            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0f),
                FightConstants.PixelsPerUnit);
        }

        public static Sprite CreateFighterBody(Color primary, Color accent, bool facingRight)
        {
            const int width = 32;
            const int height = 64;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "FighterBody_Tex"
            };

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            FillRect(pixels, width, height, 10, 18, 12, 28, primary);
            FillRect(pixels, width, height, 11, 46, 10, 10, accent);
            FillRect(pixels, width, height, 8, 56, 6, 8, accent);
            FillRect(pixels, width, height, 18, 56, 6, 8, accent);
            FillRect(pixels, width, height, 12, 8, 8, 10, accent);
            FillRect(pixels, width, height, 13, 0, 6, 8, primary);

            if (!facingRight)
            {
                MirrorHorizontal(pixels, width, height);
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0f),
                FightConstants.PixelsPerUnit);
        }

        public static Sprite CreateStageBackground()
        {
            const int width = 256;
            const int height = 144;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "StageBackground_Tex"
            };

            Color skyTop = new Color(0.12f, 0.16f, 0.32f);
            Color skyBottom = new Color(0.45f, 0.28f, 0.42f);
            Color ground = new Color(0.18f, 0.14f, 0.22f);
            Color floor = new Color(0.32f, 0.26f, 0.34f);

            for (int y = 0; y < height; y++)
            {
                float t = y / (float)(height - 1);
                Color rowColor = Color.Lerp(skyBottom, skyTop, t);
                for (int x = 0; x < width; x++)
                {
                    if (y < 28)
                    {
                        texture.SetPixel(x, y, rowColor);
                    }
                    else if (y < 40)
                    {
                        float mountain = Mathf.PerlinNoise(x * 0.04f, y * 0.08f);
                        Color mountainColor = Color.Lerp(ground, new Color(0.55f, 0.35f, 0.55f), mountain);
                        texture.SetPixel(x, y, mountainColor);
                    }
                    else if (y < 118)
                    {
                        texture.SetPixel(x, y, ground);
                    }
                    else
                    {
                        texture.SetPixel(x, y, floor);
                    }
                }
            }

            texture.Apply(false, true);
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                FightConstants.PixelsPerUnit * 0.5f);
        }

        private static void FillRect(Color[] pixels, int width, int height, int x, int y, int w, int h, Color color)
        {
            for (int py = y; py < y + h && py < height; py++)
            {
                for (int px = x; px < x + w && px < width; px++)
                {
                    if (px >= 0 && py >= 0)
                    {
                        pixels[py * width + px] = color;
                    }
                }
            }
        }

        private static void MirrorHorizontal(Color[] pixels, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width / 2; x++)
                {
                    int left = y * width + x;
                    int right = y * width + (width - 1 - x);
                    Color temp = pixels[left];
                    pixels[left] = pixels[right];
                    pixels[right] = temp;
                }
            }
        }
    }
}
