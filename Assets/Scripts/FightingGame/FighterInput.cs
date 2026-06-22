using UnityEngine;

namespace FightingGame
{
    public readonly struct FighterInputSnapshot
    {
        public readonly float Horizontal;
        public readonly bool JumpPressed;
        public readonly bool BlockHeld;
        public readonly bool LightPressed;
        public readonly bool KickPressed;
        public readonly bool HeavyPressed;
        public readonly bool Skill1Pressed;
        public readonly bool Skill2Pressed;

        public FighterInputSnapshot(
            float horizontal,
            bool jumpPressed,
            bool blockHeld,
            bool lightPressed,
            bool kickPressed,
            bool heavyPressed,
            bool skill1Pressed,
            bool skill2Pressed)
        {
            Horizontal = horizontal;
            JumpPressed = jumpPressed;
            BlockHeld = blockHeld;
            LightPressed = lightPressed;
            KickPressed = kickPressed;
            HeavyPressed = heavyPressed;
            Skill1Pressed = skill1Pressed;
            Skill2Pressed = skill2Pressed;
        }

        public static FighterInputSnapshot Empty => new FighterInputSnapshot(0f, false, false, false, false, false, false, false);
    }

    public static class FighterInputReader
    {
        public static bool HasGameplayInput(FighterInputSnapshot input)
        {
            return Mathf.Abs(input.Horizontal) > 0.01f
                || input.JumpPressed
                || input.BlockHeld
                || input.LightPressed
                || input.KickPressed
                || input.HeavyPressed
                || input.Skill1Pressed
                || input.Skill2Pressed;
        }

        public static bool HasAnyLocalGameplayInput()
        {
            return HasGameplayInput(ReadPlayerOne()) || HasGameplayInput(ReadPlayerTwo());
        }

        public static string DescribeActiveKeys()
        {
            var parts = new System.Collections.Generic.List<string>(8);
            FighterInputSnapshot p1 = ReadPlayerOne();
            FighterInputSnapshot p2 = ReadPlayerTwo();

            if (Mathf.Abs(p1.Horizontal) > 0.01f)
            {
                parts.Add(p1.Horizontal < 0f ? "P1←" : "P1→");
            }

            if (p1.JumpPressed)
            {
                parts.Add("P1점프");
            }

            if (p1.BlockHeld)
            {
                parts.Add("P1가드");
            }

            if (p1.LightPressed)
            {
                parts.Add("P1J");
            }

            if (p1.KickPressed)
            {
                parts.Add("P1K");
            }

            if (p1.HeavyPressed)
            {
                parts.Add("P1L");
            }

            if (p1.Skill1Pressed)
            {
                parts.Add("P1U");
            }

            if (p1.Skill2Pressed)
            {
                parts.Add("P1I");
            }

            if (Mathf.Abs(p2.Horizontal) > 0.01f)
            {
                parts.Add("P2 h=" + p2.Horizontal.ToString("0.##") + (p2.Horizontal < 0f ? "←" : "→"));
            }

            if (p2.JumpPressed)
            {
                parts.Add("P2점프");
            }

            if (p2.BlockHeld)
            {
                parts.Add("P2가드");
            }

            if (p2.LightPressed)
            {
                parts.Add("P21");
            }

            if (p2.KickPressed)
            {
                parts.Add("P22");
            }

            if (p2.HeavyPressed)
            {
                parts.Add("P23");
            }

            return parts.Count == 0 ? string.Empty : string.Join(" ", parts);
        }

        public static string DescribeInputBackend()
        {
#if ENABLE_INPUT_SYSTEM
            bool keyboardReady = UnityEngine.InputSystem.Keyboard.current != null;
#else
            bool keyboardReady = false;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            bool legacyReady = true;
#else
            bool legacyReady = false;
#endif

            return "키보드=" + (keyboardReady ? "OK" : "없음")
                + " | 구입력=" + (legacyReady ? "ON" : "OFF");
        }

        public static FighterInputSnapshot ReadPlayerOne()
        {
            float horizontal = ReadPlayerOneHorizontal();
            return new FighterInputSnapshot(
                horizontal,
                FightKeyCapture.ReadPlayerOneJumpPressed(),
                FightKeyCapture.ReadPlayerOneBlockHeld(),
                FightKeyCapture.ReadPlayerOneLightPressed(),
                FightKeyCapture.ReadPlayerOneKickPressed(),
                FightKeyCapture.ReadPlayerOneHeavyPressed(),
                FightKeyCapture.ReadPlayerOneSkill1Pressed(),
                FightKeyCapture.ReadPlayerOneSkill2Pressed());
        }

        public static FighterInputSnapshot ReadPlayerTwo()
        {
            return new FighterInputSnapshot(
                FightKeyCapture.ReadPlayerTwoHorizontal(),
                FightKeyCapture.ReadPlayerTwoJumpPressed(),
                FightKeyCapture.ReadPlayerTwoBlockHeld(),
                FightKeyCapture.ReadPlayerTwoLightPressed(),
                FightKeyCapture.ReadPlayerTwoKickPressed(),
                FightKeyCapture.ReadPlayerTwoHeavyPressed(),
                false,
                false);
        }

        private static float ReadPlayerOneHorizontal()
        {
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null)
            {
                float horizontal = 0f;
                if (keyboard.aKey.isPressed)
                {
                    horizontal -= 1f;
                }

                if (keyboard.dKey.isPressed)
                {
                    horizontal += 1f;
                }

                if (Mathf.Abs(horizontal) > 0.01f)
                {
                    return horizontal;
                }
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                float horizontal = 0f;
                if (Input.GetKey(KeyCode.A))
                {
                    horizontal -= 1f;
                }

                if (Input.GetKey(KeyCode.D))
                {
                    horizontal += 1f;
                }

                return Mathf.Clamp(horizontal, -1f, 1f);
            }
            catch (System.InvalidOperationException)
            {
                return 0f;
            }
#else
            return 0f;
#endif
        }
    }
}
