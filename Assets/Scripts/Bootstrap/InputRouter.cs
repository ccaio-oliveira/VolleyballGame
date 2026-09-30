using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Volley.Bootstrap
{
    /// <summary>
    /// The only place in the project that knows about hardware. Returns intent in SCREEN
    /// space: x = right, y = forward. Buttons use positional names, so the same code maps
    /// to Xbox (A/B/X/Y) and PlayStation (✕/○/▢/△).
    /// </summary>
    public static class InputRouter
    {
        private const float StickDeadzone = 0.18f;

        public static Vector2 ReadMove()
        {
            Vector2 move = Vector2.zero;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed) move.x -= 1f;
                if (keyboard.dKey.isPressed) move.x += 1f;
                if (keyboard.sKey.isPressed) move.y -= 1f;
                if (keyboard.wKey.isPressed) move.y += 1f;

                // without normalizing, diagonals would be 41% faster
                if (move.sqrMagnitude > 1f) move = move.normalized;
            }

            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                Vector2 stick = gamepad.leftStick.ReadValue();
                float magnitude = stick.magnitude;

                if (magnitude > StickDeadzone)
                {
                    // rescale to recover the full range: just past the deadzone the player
                    // still walks slowly, and at full tilt runs at 100%
                    float scaled = Mathf.InverseLerp(StickDeadzone, 1f, Mathf.Min(magnitude, 1f));
                    move = stick / magnitude * scaled;
                }
            }

            return move;
        }

        public static bool ServePressed()  => ButtonPressed(k => k.enterKey, g => g.startButton);
        public static bool JumpPressed()   => ButtonPressed(k => k.spaceKey, g => g.buttonSouth);   // ✕ / A
        public static bool PassPressed()   => ButtonPressed(k => k.fKey,     g => g.buttonEast);    // ○ / B
        public static bool AttackPressed() => ButtonPressed(k => k.jKey,     g => g.buttonWest);    // ▢ / X
        public static bool BlockPressed()  => ButtonPressed(k => k.lKey,     g => g.buttonNorth);   // △ / Y

        private static bool ButtonPressed(Func<Keyboard, KeyControl> key, Func<Gamepad, ButtonControl> button)
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;

            return (keyboard != null && key(keyboard).wasPressedThisFrame)
                || (gamepad != null && button(gamepad).wasPressedThisFrame);
        }
    }
}
