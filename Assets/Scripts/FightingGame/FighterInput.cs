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
                parts.Add(p2.Horizontal < 0f ? "P2←" : "P2→");
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
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                return ReadPlayerOneFromKeyboard(keyboard);
            }
#endif
            return ReadPlayerOneLegacySafe();
        }

        public static FighterInputSnapshot ReadPlayerTwo()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                return ReadPlayerTwoFromKeyboard(keyboard);
            }
#endif
            return ReadPlayerTwoLegacySafe();
        }

#if ENABLE_INPUT_SYSTEM
        private static FighterInputSnapshot ReadPlayerOneFromKeyboard(Keyboard keyboard)
        {
            float horizontal = 0f;
            if (IsPressed(keyboard, Key.A))
            {
                horizontal -= 1f;
            }

            if (IsPressed(keyboard, Key.D))
            {
                horizontal += 1f;
            }

            return new FighterInputSnapshot(
                horizontal,
                WasPressedThisFrame(keyboard, Key.W),
                IsPressed(keyboard, Key.S),
                WasPressedThisFrame(keyboard, Key.J),
                WasPressedThisFrame(keyboard, Key.K),
                WasPressedThisFrame(keyboard, Key.L),
                WasPressedThisFrame(keyboard, Key.U),
                WasPressedThisFrame(keyboard, Key.I));
        }

        private static FighterInputSnapshot ReadPlayerTwoFromKeyboard(Keyboard keyboard)
        {
            float horizontal = 0f;
            if (IsPressed(keyboard, Key.LeftArrow))
            {
                horizontal -= 1f;
            }

            if (IsPressed(keyboard, Key.RightArrow))
            {
                horizontal += 1f;
            }

            return new FighterInputSnapshot(
                horizontal,
                WasPressedThisFrame(keyboard, Key.UpArrow),
                IsPressed(keyboard, Key.DownArrow),
                WasPressedThisFrame(keyboard, Key.Digit1) || WasPressedThisFrame(keyboard, Key.Numpad1),
                WasPressedThisFrame(keyboard, Key.Digit2) || WasPressedThisFrame(keyboard, Key.Numpad2),
                WasPressedThisFrame(keyboard, Key.Digit3) || WasPressedThisFrame(keyboard, Key.Numpad3),
                WasPressedThisFrame(keyboard, Key.Digit4) || WasPressedThisFrame(keyboard, Key.Numpad4),
                WasPressedThisFrame(keyboard, Key.Digit5) || WasPressedThisFrame(keyboard, Key.Numpad5));
        }

        private static bool IsPressed(Keyboard keyboard, Key key)
        {
            return keyboard[key].isPressed;
        }

        private static bool WasPressedThisFrame(Keyboard keyboard, Key key)
        {
            return keyboard[key].wasPressedThisFrame;
        }
#endif

        private static FighterInputSnapshot ReadPlayerOneLegacySafe()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                return ReadPlayerOneLegacy();
            }
            catch (System.InvalidOperationException)
            {
                return FighterInputSnapshot.Empty;
            }
#else
            return FighterInputSnapshot.Empty;
#endif
        }

        private static FighterInputSnapshot ReadPlayerTwoLegacySafe()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                return ReadPlayerTwoLegacy();
            }
            catch (System.InvalidOperationException)
            {
                return FighterInputSnapshot.Empty;
            }
#else
            return FighterInputSnapshot.Empty;
#endif
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        private static FighterInputSnapshot ReadPlayerOneLegacy()
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

            return new FighterInputSnapshot(
                horizontal,
                Input.GetKeyDown(KeyCode.W),
                Input.GetKey(KeyCode.S),
                Input.GetKeyDown(KeyCode.J),
                Input.GetKeyDown(KeyCode.K),
                Input.GetKeyDown(KeyCode.L),
                Input.GetKeyDown(KeyCode.U),
                Input.GetKeyDown(KeyCode.I));
        }

        private static FighterInputSnapshot ReadPlayerTwoLegacy()
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

            return new FighterInputSnapshot(
                horizontal,
                Input.GetKeyDown(KeyCode.UpArrow),
                Input.GetKey(KeyCode.DownArrow),
                Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1),
                Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2),
                Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3),
                Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4),
                Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5));
        }
#endif
    }
}
