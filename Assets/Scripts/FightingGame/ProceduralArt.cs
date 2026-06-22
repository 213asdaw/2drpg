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
            return CreateKaronSprite(facingRight);
        }

        public static Sprite CreateKaronSprite(bool facingRight)
        {
            return CreateKaronSpriteInternal(facingRight);
        }

        public static Sprite CreateFlameSlashProjectileSprite()
        {
            const int width = 48;
            const int height = 20;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "KaronFlameSlash_Tex"
            };

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color core = new Color(1f, 0.96f, 0.65f, 1f);
            Color mid = new Color(1f, 0.48f, 0.1f, 0.95f);
            Color edge = new Color(0.75f, 0.08f, 0.02f, 0.8f);
            Color trail = new Color(1f, 0.25f, 0.05f, 0.45f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);
                    float ny = Mathf.Abs(y - (height * 0.5f)) / (height * 0.5f);
                    float crescent = Mathf.Sin(nx * Mathf.PI) * (1f - ny * 0.85f);
                    if (ny <= crescent)
                    {
                        Color pixel = nx > 0.78f ? core : nx > 0.42f ? mid : edge;
                        if (ny > crescent * 0.5f)
                        {
                            pixel = Color.Lerp(pixel, trail, 0.35f);
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

            FlipPixelsVertical(pixels, width, height);

            texture.SetPixels(pixels);
            texture.Apply(false, true);

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0f),
                FightConstants.PixelsPerUnit);
        }

        private static Sprite CreateKaronSpriteInternal(bool facingRight)
        {
            const int width = 56;
            const int height = 80;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Karon_Tex"
            };

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            Color hairWhite = new Color(0.96f, 0.95f, 0.92f);
            Color hairBright = new Color(1f, 1f, 0.98f);
            Color hairShadow = new Color(0.72f, 0.7f, 0.68f);
            Color eyeRed = new Color(0.92f, 0.1f, 0.12f);
            Color eyeGlow = new Color(1f, 0.28f, 0.22f);
            Color skin = new Color(0.86f, 0.74f, 0.66f);
            Color skinShadow = new Color(0.62f, 0.5f, 0.44f);
            Color coatDark = new Color(0.1f, 0.08f, 0.12f);
            Color coatCrimson = new Color(0.52f, 0.08f, 0.1f);
            Color armorPlate = new Color(0.28f, 0.24f, 0.3f);
            Color armorEdge = new Color(0.78f, 0.58f, 0.22f);
            Color bladeSteel = new Color(0.82f, 0.84f, 0.9f);
            Color bladeDark = new Color(0.48f, 0.5f, 0.58f);
            Color flameOuter = new Color(1f, 0.38f, 0.05f, 0.95f);
            Color flameCore = new Color(1f, 0.92f, 0.42f);
            Color boot = new Color(0.14f, 0.1f, 0.12f);

            FillRect(pixels, width, height, 18, 8, 10, 11, skin);
            FillRect(pixels, width, height, 19, 6, 8, 3, skinShadow);
            FillRect(pixels, width, height, 20, 18, 6, 2, skinShadow);

            FillRect(pixels, width, height, 17, 0, 12, 8, hairWhite);
            FillRect(pixels, width, height, 15, 6, 4, 10, hairBright);
            FillRect(pixels, width, height, 25, 6, 4, 12, hairWhite);
            FillRect(pixels, width, height, 16, 0, 3, 6, hairBright);
            FillRect(pixels, width, height, 27, 0, 3, 7, hairBright);
            FillRect(pixels, width, height, 14, 2, 2, 8, hairShadow);
            FillRect(pixels, width, height, 29, 1, 2, 9, hairShadow);
            SetPixelSafe(pixels, width, height, 13, 10, hairBright);
            SetPixelSafe(pixels, width, height, 30, 11, hairWhite);
            SetPixelSafe(pixels, width, height, 31, 8, hairBright);

            SetPixelSafe(pixels, width, height, 20, 12, eyeRed);
            SetPixelSafe(pixels, width, height, 21, 12, eyeGlow);
            SetPixelSafe(pixels, width, height, 24, 12, eyeRed);
            SetPixelSafe(pixels, width, height, 25, 12, eyeGlow);
            SetPixelSafe(pixels, width, height, 21, 13, eyeGlow);
            SetPixelSafe(pixels, width, height, 25, 13, eyeGlow);

            FillRect(pixels, width, height, 16, 20, 14, 30, coatDark);
            FillRect(pixels, width, height, 14, 24, 3, 22, coatCrimson);
            FillRect(pixels, width, height, 29, 26, 3, 20, coatCrimson);
            FillRect(pixels, width, height, 17, 22, 12, 4, coatCrimson);
            FillRect(pixels, width, height, 18, 30, 10, 2, armorEdge);

            FillRect(pixels, width, height, 17, 24, 12, 14, armorPlate);
            FillRect(pixels, width, height, 16, 26, 2, 10, armorEdge);
            FillRect(pixels, width, height, 28, 26, 2, 10, armorEdge);
            FillRect(pixels, width, height, 20, 28, 6, 6, new Color(0.18f, 0.14f, 0.18f));
            SetPixelSafe(pixels, width, height, 22, 30, flameCore);
            SetPixelSafe(pixels, width, height, 23, 31, flameOuter);

            FillRect(pixels, width, height, 8, 28, 5, 18, coatDark);
            FillRect(pixels, width, height, 7, 32, 2, 12, coatCrimson);

            FillRect(pixels, width, height, 17, 50, 5, 12, coatDark);
            FillRect(pixels, width, height, 24, 50, 5, 12, coatDark);
            FillRect(pixels, width, height, 16, 60, 7, 10, boot);
            FillRect(pixels, width, height, 25, 60, 7, 10, boot);
            FillRect(pixels, width, height, 17, 62, 5, 2, armorEdge);
            FillRect(pixels, width, height, 26, 62, 5, 2, armorEdge);

            FillRect(pixels, width, height, 32, 18, 6, 38, bladeSteel);
            FillRect(pixels, width, height, 33, 20, 4, 34, bladeDark);
            FillRect(pixels, width, height, 31, 52, 8, 8, new Color(0.32f, 0.2f, 0.1f));
            FillRect(pixels, width, height, 32, 54, 6, 4, armorEdge);

            FillRect(pixels, width, height, 34, 16, 4, 40, flameOuter);
            FillRect(pixels, width, height, 35, 22, 2, 28, flameCore);
            SetPixelSafe(pixels, width, height, 36, 28, flameCore);
            SetPixelSafe(pixels, width, height, 36, 36, flameCore);
            SetPixelSafe(pixels, width, height, 36, 44, flameOuter);
            SetPixelSafe(pixels, width, height, 33, 24, flameOuter);
            SetPixelSafe(pixels, width, height, 33, 40, flameCore);
            SetPixelSafe(pixels, width, height, 37, 32, hairBright);

            FillRect(pixels, width, height, 30, 14, 3, 6, skin);
            FillRect(pixels, width, height, 29, 16, 2, 4, skinShadow);

            if (!facingRight)
            {
                MirrorHorizontal(pixels, width, height);
            }

            FlipPixelsVertical(pixels, width, height);

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

            Color skyTop = new Color(0.45f, 0.62f, 0.88f);
            Color skyBottom = new Color(0.78f, 0.72f, 0.82f);
            Color mountain = new Color(0.55f, 0.42f, 0.58f);
            Color farGround = new Color(0.62f, 0.56f, 0.48f);
            Color arenaGlow = new Color(0.92f, 0.78f, 0.55f, 0.35f);

            for (int y = 0; y < height; y++)
            {
                float t = y / (float)(height - 1);
                Color rowColor = Color.Lerp(skyBottom, skyTop, t);
                for (int x = 0; x < width; x++)
                {
                    if (y < 34)
                    {
                        texture.SetPixel(x, y, rowColor);
                    }
                    else if (y < 52)
                    {
                        float ridge = Mathf.PerlinNoise(x * 0.05f, y * 0.1f);
                        texture.SetPixel(x, y, Color.Lerp(mountain, farGround, ridge));
                    }
                    else if (y < 96)
                    {
                        texture.SetPixel(x, y, farGround);
                    }
                    else
                    {
                        float centerGlow = 1f - Mathf.Abs(x - width * 0.5f) / (width * 0.5f);
                        float platformBand = Mathf.Clamp01(1f - Mathf.Abs(y - 112f) / 10f);
                        Color baseColor = Color.Lerp(farGround, arenaGlow, centerGlow * platformBand);
                        texture.SetPixel(x, y, baseColor);
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

        public static Sprite CreateArenaPlatform()
        {
            const int width = 192;
            const int height = 24;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "ArenaPlatform_Tex"
            };

            Color top = new Color(0.96f, 0.88f, 0.72f);
            Color mid = new Color(0.82f, 0.66f, 0.42f);
            Color edge = new Color(0.58f, 0.42f, 0.28f);
            Color stripe = new Color(0.95f, 0.55f, 0.28f, 0.55f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);
                    float ny = y / (float)(height - 1);
                    Color color = ny > 0.72f ? top : ny > 0.35f ? mid : edge;
                    if (ny > 0.45f && ny < 0.58f && (int)(nx * 12f) % 2 == 0)
                    {
                        color = Color.Lerp(color, stripe, 0.45f);
                    }

                    if (x < 3 || x >= width - 3)
                    {
                        color = Color.Lerp(color, edge, 0.65f);
                    }

                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply(false, true);
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                FightConstants.PixelsPerUnit * 0.45f);
        }

        public static Sprite CreateSwordSwingEffect(AttackType attackType, float progress)
        {
            const int width = 40;
            const int height = 40;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "SwordSwing_Tex"
            };

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color blade = new Color(0.92f, 0.94f, 1f, 0.95f);
            Color edge = new Color(1f, 0.55f, 0.18f, 0.9f);
            Color trail = new Color(1f, 0.35f, 0.08f, 0.55f);
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            float arcStart = Mathf.Lerp(-2.4f, -0.8f, progress);
            float arcEnd = Mathf.Lerp(0.2f, 2.1f, progress);
            float thickness = attackType == AttackType.Heavy ? 3.4f : 2.6f;
            Vector2 pivot = new Vector2(width * 0.28f, height * 0.22f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2 point = new Vector2(x, y);
                    Vector2 dir = point - pivot;
                    float angle = Mathf.Atan2(dir.y, dir.x);
                    float radius = dir.magnitude;
                    if (angle >= arcStart && angle <= arcEnd && radius >= 6f && radius <= 24f + progress * 6f)
                    {
                        float edgeBlend = Mathf.InverseLerp(arcStart, arcEnd, angle);
                        Color pixel = Color.Lerp(trail, blade, edgeBlend);
                        if (radius > 18f)
                        {
                            pixel = Color.Lerp(pixel, edge, 0.55f);
                        }

                        pixels[y * width + x] = pixel;
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.28f, 0.22f),
                FightConstants.PixelsPerUnit);
        }

        public static Sprite CreatePunchEffect(AttackType attackType, float progress)
        {
            int size = attackType == AttackType.Heavy ? 22 : 16;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Punch_Tex"
            };

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color fist = new Color(0.96f, 0.82f, 0.72f, 0.98f);
            Color glove = new Color(0.72f, 0.18f, 0.16f, 0.95f);
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            int fistWidth = attackType == AttackType.Heavy ? 12 : 9;
            int fistHeight = attackType == AttackType.Heavy ? 10 : 8;
            int offsetX = Mathf.RoundToInt(Mathf.Lerp(1f, size - fistWidth - 1f, progress));
            FillRect(pixels, size, size, offsetX, 3, fistWidth, fistHeight, fist);
            FillRect(pixels, size, size, offsetX + 1, 2, fistWidth - 2, 2, glove);

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.15f, 0.5f),
                FightConstants.PixelsPerUnit);
        }

        public static Sprite CreateKickEffect(float progress)
        {
            const int width = 24;
            const int height = 14;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Kick_Tex"
            };

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color boot = new Color(0.22f, 0.16f, 0.18f, 0.98f);
            Color sole = new Color(0.72f, 0.24f, 0.18f, 0.95f);
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            int offsetX = Mathf.RoundToInt(Mathf.Lerp(2f, width - 11f, progress));
            FillRect(pixels, width, height, offsetX, 4, 10, 6, boot);
            FillRect(pixels, width, height, offsetX + 8, 3, 3, 8, sole);

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.1f, 0.5f),
                FightConstants.PixelsPerUnit);
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

        private static void FlipPixelsVertical(Color[] pixels, int width, int height)
        {
            for (int y = 0; y < height / 2; y++)
            {
                int oppositeY = height - 1 - y;
                for (int x = 0; x < width; x++)
                {
                    int bottomIndex = y * width + x;
                    int topIndex = oppositeY * width + x;
                    Color temp = pixels[bottomIndex];
                    pixels[bottomIndex] = pixels[topIndex];
                    pixels[topIndex] = temp;
                }
            }
        }
    }
}
