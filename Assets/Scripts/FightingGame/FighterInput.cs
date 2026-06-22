using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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

        public static FighterInputSnapshot Combine(FighterInputSnapshot primary, FighterInputSnapshot secondary)
        {
            return new FighterInputSnapshot(
                0f,
                primary.JumpPressed || secondary.JumpPressed,
                primary.BlockHeld || secondary.BlockHeld,
                primary.LightPressed || secondary.LightPressed,
                primary.KickPressed || secondary.KickPressed,
                primary.HeavyPressed || secondary.HeavyPressed,
                primary.Skill1Pressed || secondary.Skill1Pressed,
                primary.Skill2Pressed || secondary.Skill2Pressed);
        }
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
            bool keyboardReady = Keyboard.current != null;
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
            float horizontal = ReadPlayerOneHorizontalAllSources();
            FighterInputSnapshot buttons = FighterInputSnapshot.Combine(
                ReadPlayerOneButtonsModern(),
                ReadPlayerOneButtonsLegacySafe());
            return new FighterInputSnapshot(
                horizontal,
                buttons.JumpPressed,
                buttons.BlockHeld,
                buttons.LightPressed,
                buttons.KickPressed,
                buttons.HeavyPressed,
                buttons.Skill1Pressed,
                buttons.Skill2Pressed);
        }

        public static FighterInputSnapshot ReadPlayerTwo()
        {
            float horizontal = FightKeyCapture.ReadPlayerTwoHorizontal();
            bool jumpPressed = FightKeyCapture.ReadPlayerTwoJumpPressed();
            bool blockHeld = FightKeyCapture.ReadPlayerTwoBlockHeld();
            FighterInputSnapshot attacks = FighterInputSnapshot.Combine(
                ReadPlayerTwoAttackButtonsModern(),
                ReadPlayerTwoAttackButtonsLegacySafe());
            return new FighterInputSnapshot(
                horizontal,
                jumpPressed,
                blockHeld,
                attacks.LightPressed,
                attacks.KickPressed,
                attacks.HeavyPressed,
                false,
                false);
        }

        private static float ReadPlayerOneHorizontalAllSources()
        {
            float modern = ReadPlayerOneHorizontalModern();
            float legacy = ReadPlayerOneHorizontalLegacySafe();
            return MergeHorizontal(modern, legacy, preferLegacyOnConflict: false);
        }

        private static float ReadPlayerTwoHorizontalAllSources()
        {
            float modern = ReadPlayerTwoHorizontalModern();
            float legacy = ReadPlayerTwoHorizontalLegacySafe();
            bool wantLeft = modern < -0.01f || legacy < -0.01f;
            bool wantRight = modern > 0.01f || legacy > 0.01f;

            if (wantLeft && wantRight)
            {
                if (Mathf.Abs(legacy) > 0.01f)
                {
                    return Mathf.Clamp(legacy, -1f, 1f);
                }

                if (Mathf.Abs(modern) > 0.01f)
                {
                    return Mathf.Clamp(modern, -1f, 1f);
                }

                return 0f;
            }

            if (wantRight)
            {
                return 1f;
            }

            if (wantLeft)
            {
                return -1f;
            }

            return 0f;
        }

        private static float MergeHorizontal(float modern, float legacy, bool preferLegacyOnConflict)
        {
            if (Mathf.Abs(modern) < 0.01f && Mathf.Abs(legacy) < 0.01f)
            {
                return 0f;
            }

            if (Mathf.Abs(modern) < 0.01f)
            {
                return Mathf.Clamp(legacy, -1f, 1f);
            }

            if (Mathf.Abs(legacy) < 0.01f)
            {
                return Mathf.Clamp(modern, -1f, 1f);
            }

            if (Mathf.Sign(modern) == Mathf.Sign(legacy))
            {
                return Mathf.Clamp(modern + legacy, -1f, 1f);
            }

            return preferLegacyOnConflict
                ? Mathf.Clamp(legacy, -1f, 1f)
                : Mathf.Clamp(modern, -1f, 1f);
        }

        private static float ReadPlayerOneHorizontalModern()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return 0f;
            }

            float horizontal = 0f;
            if (keyboard.aKey.isPressed)
            {
                horizontal -= 1f;
            }

            if (keyboard.dKey.isPressed)
            {
                horizontal += 1f;
            }

            return Mathf.Clamp(horizontal, -1f, 1f);
#else
            return 0f;
#endif
        }

        private static float ReadPlayerTwoHorizontalModern()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return 0f;
            }

            return ReadPlayerTwoHorizontalFromKeyboard(keyboard);
#else
            return 0f;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static float ReadPlayerTwoHorizontalFromKeyboard(Keyboard keyboard)
        {
            float horizontal = 0f;
            if (keyboard.leftArrowKey.isPressed)
            {
                horizontal -= 1f;
            }

            if (keyboard.rightArrowKey.isPressed)
            {
                horizontal += 1f;
            }

            if (Mathf.Abs(horizontal) > 0.01f)
            {
                return Mathf.Clamp(horizontal, -1f, 1f);
            }

            if (keyboard.eKey.isPressed)
            {
                horizontal -= 1f;
            }

            if (keyboard.oKey.isPressed)
            {
                horizontal += 1f;
            }

            if (Mathf.Abs(horizontal) > 0.01f)
            {
                return Mathf.Clamp(horizontal, -1f, 1f);
            }

            if (keyboard.numpad4Key.isPressed)
            {
                horizontal -= 1f;
            }

            if (keyboard.numpad6Key.isPressed)
            {
                horizontal += 1f;
            }

            return Mathf.Clamp(horizontal, -1f, 1f);
        }
#endif

        private static FighterInputSnapshot ReadPlayerOneButtonsModern()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                return new FighterInputSnapshot(
                    0f,
                    keyboard.wKey.wasPressedThisFrame,
                    keyboard.sKey.isPressed,
                    keyboard.jKey.wasPressedThisFrame,
                    keyboard.kKey.wasPressedThisFrame,
                    keyboard.lKey.wasPressedThisFrame,
                    keyboard.uKey.wasPressedThisFrame,
                    keyboard.iKey.wasPressedThisFrame);
            }
#endif
            return FighterInputSnapshot.Empty;
        }

        private static FighterInputSnapshot ReadPlayerTwoAttackButtonsModern()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                return new FighterInputSnapshot(
                    0f,
                    false,
                    false,
                    keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame,
                    keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame,
                    keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame,
                    false,
                    false);
            }
#endif
            return FighterInputSnapshot.Empty;
        }

        private static float ReadPlayerOneHorizontalLegacySafe()
        {
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

        private static float ReadPlayerTwoHorizontalLegacySafe()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                return ReadPlayerTwoHorizontalLegacy();
            }
            catch (System.InvalidOperationException)
            {
                return 0f;
            }
#else
            return 0f;
#endif
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        private static float ReadPlayerTwoHorizontalLegacy()
        {
            float horizontal = 0f;
            if (Input.GetKey(KeyCode.LeftArrow))
            {
                horizontal -= 1f;
            }

            if (Input.GetKey(KeyCode.RightArrow))
            {
                horizontal += 1f;
            }

            if (Mathf.Abs(horizontal) > 0.01f)
            {
                return Mathf.Clamp(horizontal, -1f, 1f);
            }

            if (Input.GetKey(KeyCode.E))
            {
                horizontal -= 1f;
            }

            if (Input.GetKey(KeyCode.O))
            {
                horizontal += 1f;
            }

            if (Mathf.Abs(horizontal) > 0.01f)
            {
                return Mathf.Clamp(horizontal, -1f, 1f);
            }

            if (Input.GetKey(KeyCode.Keypad4))
            {
                horizontal -= 1f;
            }

            if (Input.GetKey(KeyCode.Keypad6))
            {
                horizontal += 1f;
            }

            return Mathf.Clamp(horizontal, -1f, 1f);
        }
#endif

        private static FighterInputSnapshot ReadPlayerOneButtonsLegacySafe()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                return new FighterInputSnapshot(
                    0f,
                    Input.GetKeyDown(KeyCode.W),
                    Input.GetKey(KeyCode.S),
                    Input.GetKeyDown(KeyCode.J),
                    Input.GetKeyDown(KeyCode.K),
                    Input.GetKeyDown(KeyCode.L),
                    Input.GetKeyDown(KeyCode.U),
                    Input.GetKeyDown(KeyCode.I));
            }
            catch (System.InvalidOperationException)
            {
                return FighterInputSnapshot.Empty;
            }
#else
            return FighterInputSnapshot.Empty;
#endif
        }

        private static FighterInputSnapshot ReadPlayerTwoAttackButtonsLegacySafe()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                return new FighterInputSnapshot(
                    0f,
                    false,
                    false,
                    Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1),
                    Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2),
                    Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3),
                    false,
                    false);
            }
            catch (System.InvalidOperationException)
            {
                return FighterInputSnapshot.Empty;
            }
#else
            return FighterInputSnapshot.Empty;
#endif
        }
    }
}
