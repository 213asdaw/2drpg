using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FightingGame
{
    internal static class FightKeyCapture
    {
        private static bool guiArrowLeftHeld;
        private static bool guiArrowRightHeld;
        private static bool guiArrowUpHeld;
        private static bool guiArrowDownHeld;

        private static bool guiP1JumpPressed;
        private static bool guiP1LightPressed;
        private static bool guiP1KickPressed;
        private static bool guiP1HeavyPressed;
        private static bool guiP1Skill1Pressed;
        private static bool guiP1Skill2Pressed;

        private static bool guiP2JumpPressed;
        private static bool guiP2LightPressed;
        private static bool guiP2KickPressed;
        private static bool guiP2HeavyPressed;
        private static bool guiP2Skill1Pressed;

        private static bool guiArrowUpPressed;
        private static bool frameEnded;

        private static bool hwArrowLeft;
        private static bool hwArrowRight;
        private static bool hwArrowUp;
        private static bool hwArrowDown;
        private static bool hwArrowUpPressed;

        public static void PollHardwareKeys()
        {
            bool nextLeft = false;
            bool nextRight = false;
            bool nextUp = false;
            bool nextDown = false;

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                nextLeft = keyboard.leftArrowKey.isPressed;
                nextRight = keyboard.rightArrowKey.isPressed;
                nextUp = keyboard.upArrowKey.isPressed;
                nextDown = keyboard.downArrowKey.isPressed;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                nextLeft |= Input.GetKey(KeyCode.LeftArrow);
                nextRight |= Input.GetKey(KeyCode.RightArrow);
                nextUp |= Input.GetKey(KeyCode.UpArrow);
                nextDown |= Input.GetKey(KeyCode.DownArrow);
            }
            catch (System.InvalidOperationException)
            {
            }
#endif
            hwArrowUpPressed = nextUp && !hwArrowUp;
            hwArrowLeft = nextLeft;
            hwArrowRight = nextRight;
            hwArrowUp = nextUp;
            hwArrowDown = nextDown;
        }

        public static void ProcessGuiEvent(Event current)
        {
            if (current == null)
            {
                return;
            }

            if (current.type == EventType.KeyDown)
            {
                switch (current.keyCode)
                {
                    case KeyCode.LeftArrow:
                        guiArrowLeftHeld = true;
                        current.Use();
                        break;
                    case KeyCode.RightArrow:
                        guiArrowRightHeld = true;
                        current.Use();
                        break;
                    case KeyCode.UpArrow:
                        guiArrowUpHeld = true;
                        guiArrowUpPressed = true;
                        guiP2JumpPressed = true;
                        current.Use();
                        break;
                    case KeyCode.DownArrow:
                        guiArrowDownHeld = true;
                        current.Use();
                        break;
                    case KeyCode.W:
                        guiP1JumpPressed = true;
                        current.Use();
                        break;
                    case KeyCode.J:
                        guiP1LightPressed = true;
                        current.Use();
                        break;
                    case KeyCode.K:
                        guiP1KickPressed = true;
                        current.Use();
                        break;
                    case KeyCode.L:
                        guiP1HeavyPressed = true;
                        current.Use();
                        break;
                    case KeyCode.U:
                        guiP1Skill1Pressed = true;
                        current.Use();
                        break;
                    case KeyCode.I:
                        guiP1Skill2Pressed = true;
                        current.Use();
                        break;
                    case KeyCode.Alpha1:
                    case KeyCode.Keypad1:
                        guiP2LightPressed = true;
                        current.Use();
                        break;
                    case KeyCode.Alpha2:
                    case KeyCode.Keypad2:
                        guiP2KickPressed = true;
                        current.Use();
                        break;
                    case KeyCode.Alpha3:
                    case KeyCode.Keypad3:
                        guiP2HeavyPressed = true;
                        current.Use();
                        break;
                    case KeyCode.Alpha4:
                        guiP2Skill1Pressed = true;
                        current.Use();
                        break;
                    case KeyCode.PageUp:
                    case KeyCode.Keypad8:
                        guiP2JumpPressed = true;
                        current.Use();
                        break;
                }
            }
            else if (current.type == EventType.KeyUp)
            {
                switch (current.keyCode)
                {
                    case KeyCode.LeftArrow:
                        guiArrowLeftHeld = false;
                        current.Use();
                        break;
                    case KeyCode.RightArrow:
                        guiArrowRightHeld = false;
                        current.Use();
                        break;
                    case KeyCode.UpArrow:
                        guiArrowUpHeld = false;
                        current.Use();
                        break;
                    case KeyCode.DownArrow:
                        guiArrowDownHeld = false;
                        current.Use();
                        break;
                }
            }
        }

        public static void EndFrame()
        {
            if (frameEnded)
            {
                return;
            }

            frameEnded = true;
            guiArrowUpPressed = false;
            guiP1JumpPressed = false;
            guiP1LightPressed = false;
            guiP1KickPressed = false;
            guiP1HeavyPressed = false;
            guiP1Skill1Pressed = false;
            guiP1Skill2Pressed = false;
            guiP2JumpPressed = false;
            guiP2LightPressed = false;
            guiP2KickPressed = false;
            guiP2HeavyPressed = false;
            guiP2Skill1Pressed = false;
        }

        public static void BeginFrame()
        {
            frameEnded = false;
        }

        public static float ReadPlayerTwoHorizontal()
        {
            bool left = ReadModernKeyPressed(Key.E)
                || ReadLegacyKey(KeyCode.E)
                || ReadModernKeyPressed(Key.Comma)
                || ReadLegacyKey(KeyCode.Comma)
                || ReadModernKeyPressed(Key.Numpad4)
                || ReadLegacyKey(KeyCode.Keypad4);

            bool right = ReadModernKeyPressed(Key.O)
                || ReadLegacyKey(KeyCode.O)
                || ReadModernKeyPressed(Key.Period)
                || ReadLegacyKey(KeyCode.Period)
                || ReadModernKeyPressed(Key.Numpad6)
                || ReadLegacyKey(KeyCode.Keypad6);

            if (left && right)
            {
                return 0f;
            }

            if (right)
            {
                return 1f;
            }

            if (left)
            {
                return -1f;
            }

            return 0f;
        }

        public static bool ReadPlayerOneJumpPressed()
        {
            return guiP1JumpPressed
                || ReadModernKeyDown(Key.W)
                || ReadLegacyKeyDown(KeyCode.W);
        }

        public static bool ReadPlayerOneLightPressed()
        {
            return guiP1LightPressed
                || ReadModernKeyDown(Key.J)
                || ReadLegacyKeyDown(KeyCode.J);
        }

        public static bool ReadPlayerOneKickPressed()
        {
            return guiP1KickPressed
                || ReadModernKeyDown(Key.K)
                || ReadLegacyKeyDown(KeyCode.K);
        }

        public static bool ReadPlayerOneHeavyPressed()
        {
            return guiP1HeavyPressed
                || ReadModernKeyDown(Key.L)
                || ReadLegacyKeyDown(KeyCode.L);
        }

        public static bool ReadPlayerOneSkill1Pressed()
        {
            return guiP1Skill1Pressed
                || ReadModernKeyDown(Key.U)
                || ReadLegacyKeyDown(KeyCode.U);
        }

        public static bool ReadPlayerOneSkill2Pressed()
        {
            return guiP1Skill2Pressed
                || ReadModernKeyDown(Key.I)
                || ReadLegacyKeyDown(KeyCode.I);
        }

        public static bool ReadPlayerOneBlockHeld()
        {
            return ReadModernKeyPressed(Key.S) || ReadLegacyKey(KeyCode.S);
        }

        public static bool ReadPlayerTwoJumpPressed()
        {
            return guiP2JumpPressed
                || guiArrowUpPressed
                || hwArrowUpPressed
                || ReadModernUpPressed()
                || ReadLegacyKeyDown(KeyCode.UpArrow)
                || ReadModernKeyDown(Key.PageUp)
                || ReadLegacyKeyDown(KeyCode.PageUp)
                || ReadModernKeyDown(Key.Numpad8)
                || ReadLegacyKeyDown(KeyCode.Keypad8);
        }

        public static bool ReadPlayerTwoLightPressed()
        {
            return guiP2LightPressed
                || ReadModernKeyDown(Key.Digit1)
                || ReadModernKeyDown(Key.Numpad1)
                || ReadLegacyKeyDown(KeyCode.Alpha1)
                || ReadLegacyKeyDown(KeyCode.Keypad1);
        }

        public static bool ReadPlayerTwoKickPressed()
        {
            return guiP2KickPressed
                || ReadModernKeyDown(Key.Digit2)
                || ReadModernKeyDown(Key.Numpad2)
                || ReadLegacyKeyDown(KeyCode.Alpha2)
                || ReadLegacyKeyDown(KeyCode.Keypad2);
        }

        public static bool ReadPlayerTwoHeavyPressed()
        {
            return guiP2HeavyPressed
                || ReadModernKeyDown(Key.Digit3)
                || ReadModernKeyDown(Key.Numpad3)
                || ReadLegacyKeyDown(KeyCode.Alpha3)
                || ReadLegacyKeyDown(KeyCode.Keypad3);
        }

        public static bool ReadPlayerTwoSkill1Pressed()
        {
            return guiP2Skill1Pressed
                || ReadModernKeyDown(Key.Digit4)
                || ReadLegacyKeyDown(KeyCode.Alpha4);
        }

        public static bool ReadPlayerTwoBlockHeld()
        {
            return hwArrowDown
                || guiArrowDownHeld
                || ReadModernDownHeld()
                || ReadLegacyKey(KeyCode.DownArrow)
                || ReadModernKeyPressed(Key.PageDown)
                || ReadLegacyKey(KeyCode.PageDown)
                || ReadModernKeyPressed(Key.Numpad2)
                || ReadLegacyKey(KeyCode.Keypad2);
        }

#if ENABLE_INPUT_SYSTEM
        private static bool ReadModernArrowLeft()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.leftArrowKey.isPressed;
        }

        private static bool ReadModernArrowRight()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.rightArrowKey.isPressed;
        }

        private static bool ReadModernUpPressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.upArrowKey.wasPressedThisFrame;
        }

        private static bool ReadModernDownHeld()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.downArrowKey.isPressed;
        }

        private static bool ReadModernKeyPressed(Key key)
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard[key].isPressed;
        }

        private static bool ReadModernKeyDown(Key key)
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard[key].wasPressedThisFrame;
        }
#else
        private static bool ReadModernArrowLeft() => false;
        private static bool ReadModernArrowRight() => false;
        private static bool ReadModernUpPressed() => false;
        private static bool ReadModernDownHeld() => false;
        private static bool ReadModernKeyPressed(Key key) => false;
        private static bool ReadModernKeyDown(Key key) => false;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        private static bool ReadLegacyKey(KeyCode keyCode)
        {
            try
            {
                return Input.GetKey(keyCode);
            }
            catch (System.InvalidOperationException)
            {
                return false;
            }
        }

        private static bool ReadLegacyKeyDown(KeyCode keyCode)
        {
            try
            {
                return Input.GetKeyDown(keyCode);
            }
            catch (System.InvalidOperationException)
            {
                return false;
            }
        }
#else
        private static bool ReadLegacyKey(KeyCode keyCode) => false;
        private static bool ReadLegacyKeyDown(KeyCode keyCode) => false;
#endif
    }
}
