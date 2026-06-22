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
        private static bool guiArrowUpPressed;

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
                        current.Use();
                        break;
                    case KeyCode.DownArrow:
                        guiArrowDownHeld = true;
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
            guiArrowUpPressed = false;
        }

        public static float ReadPlayerTwoHorizontal()
        {
            bool left = guiArrowLeftHeld
                || ReadModernArrowLeft()
                || ReadLegacyKey(KeyCode.LeftArrow)
                || ReadModernKeyPressed(Key.E)
                || ReadLegacyKey(KeyCode.E);

            bool right = guiArrowRightHeld
                || ReadModernArrowRight()
                || ReadLegacyKey(KeyCode.RightArrow)
                || ReadModernKeyPressed(Key.O)
                || ReadLegacyKey(KeyCode.O);

            left |= ReadModernKeyPressed(Key.Numpad4) || ReadLegacyKey(KeyCode.Keypad4);
            right |= ReadModernKeyPressed(Key.Numpad6) || ReadLegacyKey(KeyCode.Keypad6);

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

        public static bool ReadPlayerTwoJumpPressed()
        {
            return guiArrowUpPressed
                || ReadModernUpPressed()
                || ReadLegacyKeyDown(KeyCode.UpArrow)
                || ReadModernKeyDown(Key.PageUp)
                || ReadLegacyKeyDown(KeyCode.PageUp)
                || ReadModernKeyDown(Key.Numpad8)
                || ReadLegacyKeyDown(KeyCode.Keypad8);
        }

        public static bool ReadPlayerTwoBlockHeld()
        {
            return guiArrowDownHeld
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
