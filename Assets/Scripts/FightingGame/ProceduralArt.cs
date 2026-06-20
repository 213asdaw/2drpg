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
            return CreateGenericFighterBody(primary, accent, facingRight, false);
        }

        public static Sprite CreateFlameSwordsmanBody(bool facingRight)
        {
            return CreateFlameSwordsmanSprite(facingRight);
        }

        public static Sprite CreateFlameSlashProjectileSprite()
        {
            const int width = 40;
            const int height = 16;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "FlameSlash_Tex"
            };

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color core = new Color(1f, 0.95f, 0.55f, 1f);
            Color mid = new Color(1f, 0.45f, 0.08f, 0.95f);
            Color edge = new Color(0.85f, 0.12f, 0.02f, 0.75f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);
                    float ny = Mathf.Abs(y - (height * 0.5f)) / (height * 0.5f);
                    float taper = Mathf.Lerp(1f, 0.15f, nx);
                    if (ny <= taper)
                    {
                        Color pixel = nx > 0.72f ? core : nx > 0.35f ? mid : edge;
                        if (ny > taper * 0.55f)
                        {
                            pixel.a *= 0.55f;
                        }

                        texture.SetPixel(x, y, pixel);
                    }
                    else
                    {
                        texture.SetPixel(x, y, clear);
                    }
                }
            }

            texture.Apply(false, true);
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                FightConstants.PixelsPerUnit);
        }

        private static Sprite CreateGenericFighterBody(Color primary, Color accent, bool facingRight, bool unused)
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

        private static Sprite CreateFlameSwordsmanSprite(bool facingRight)
        {
            const int width = 40;
            const int height = 64;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "FlameSwordsman_Tex"
            };

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            Color armor = new Color(0.22f, 0.12f, 0.1f);
            Color armorLight = new Color(0.42f, 0.22f, 0.16f);
            Color skin = new Color(0.92f, 0.62f, 0.42f);
            Color cape = new Color(0.55f, 0.1f, 0.06f);
            Color blade = new Color(0.78f, 0.8f, 0.86f);
            Color flame = new Color(1f, 0.42f, 0.08f);
            Color flameCore = new Color(1f, 0.86f, 0.28f);

            FillRect(pixels, width, height, 14, 18, 12, 28, armor);
            FillRect(pixels, width, height, 15, 28, 10, 8, armorLight);
            FillRect(pixels, width, height, 16, 46, 8, 10, armor);
            FillRect(pixels, width, height, 10, 56, 7, 8, armorLight);
            FillRect(pixels, width, height, 22, 56, 7, 8, armorLight);
            FillRect(pixels, width, height, 16, 8, 8, 10, skin);
            FillRect(pixels, width, height, 17, 0, 6, 8, skin);
            FillRect(pixels, width, height, 6, 22, 6, 20, cape);
            FillRect(pixels, width, height, 24, 24, 5, 30, blade);
            FillRect(pixels, width, height, 23, 52, 7, 6, new Color(0.35f, 0.22f, 0.12f));
            FillRect(pixels, width, height, 25, 20, 3, 34, flame);
            FillRect(pixels, width, height, 26, 26, 1, 22, flameCore);
            SetPixelSafe(pixels, width, height, 27, 32, flameCore);
            SetPixelSafe(pixels, width, height, 27, 38, flameCore);
            SetPixelSafe(pixels, width, height, 28, 44, flame);
            SetPixelSafe(pixels, width, height, 24, 30, flame);
            SetPixelSafe(pixels, width, height, 24, 36, flameCore);

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

        private static void SetPixelSafe(Color[] pixels, int width, int height, int x, int y, Color color)
        {
            if (x >= 0 && x < width && y >= 0 && y < height)
            {
                pixels[y * width + x] = color;
            }
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
