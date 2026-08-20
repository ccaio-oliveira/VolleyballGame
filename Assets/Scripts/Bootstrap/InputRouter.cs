using UnityEngine;
using UnityEngine.InputSystem;

namespace Volley.Bootstrap
{
    /// <summary
    /// Único lugar do projeto que conhece o hardware. Devolve intenção em espaço de TELA:
    /// x = direita, y = frente.
    /// /<summary>
    public static class InputRouter
    {
        private const float Deadzone = 0.18f;

        public static Vector2 ReadMove()
        {
            Vector2 v = Vector2.zero;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed) v.x -= 1f;
                if (kb.dKey.isPressed) v.x += 1f;
                if (kb.sKey.isPressed) v.y -= 1f;
                if (kb.wKey.isPressed) v.y += 1f;
                if (v.sqrMagnitude > 1f) v = v.normalized;
            }

            var gp = Gamepad.current;
            if (gp != null)
            {
                Vector2 stick = gp.leftStick.ReadValue();
                float mag = stick.magnitude;

                if (mag > Deadzone)
                {
                    // reescala pra recuperar o range cheio: logo após a deadzone
                    // o jogador ainda anda devagar, e no talo anda 100%.
                    float scaled = Mathf.InverseLerp(Deadzone, 1f, Mathf.Min(mag, 1f));
                    v = stick / mag * scaled;
                }
            }

            return v;
        }

        public static bool ReceivePressed()
        {
            var kb = Keyboard.current;
            var gp = Gamepad.current;
            return (kb != null && kb.fKey.wasPressedThisFrame) || (gp != null && gp.buttonSouth.wasPressedThisFrame);
        }

        public static bool ServePressed()
        {
            var kb = Keyboard.current;
            var gp = Gamepad.current;
            return (kb != null && kb.spaceKey.wasPressedThisFrame) || (gp != null && gp.buttonWest.wasPressedThisFrame);
        }
    }
}