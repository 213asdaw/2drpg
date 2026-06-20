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

        public static FighterInputSnapshot ReadPlayerOne()
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

        public static FighterInputSnapshot ReadPlayerTwo()
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
    }
}
