using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Floating joystick: touch / click and drag anywhere (below the HUD) to steer. WASD / arrows also work.
/// Uses the new Input System (the project has the old one disabled).
/// </summary>
public class InputReader
{
    public Vector2 Move { get; private set; }
    public bool Dragging { get; private set; }
    public Vector2 BaseScreen { get; private set; }
    public Vector2 KnobScreen { get; private set; }
    public float MaxRadiusPx => Mathf.Min(Screen.width, Screen.height) * 0.11f;

    private Vector2 origin;

    public void Tick(bool allowPointer)
    {
        Vector2 kb = Vector2.zero;
        var k = Keyboard.current;
        if (k != null)
        {
            if (k.aKey.isPressed || k.leftArrowKey.isPressed) kb.x -= 1f;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) kb.x += 1f;
            if (k.sKey.isPressed || k.downArrowKey.isPressed) kb.y -= 1f;
            if (k.wKey.isPressed || k.upArrowKey.isPressed) kb.y += 1f;
        }

        Vector2 stick = Vector2.zero;
        var p = Pointer.current;
        if (!allowPointer || p == null)
        {
            Dragging = false;
        }
        else
        {
            bool pressed = p.press.isPressed;
            Vector2 pos = p.position.ReadValue();

            if (!Dragging && p.press.wasPressedThisFrame && pos.y < Screen.height * 0.86f)
            {
                Dragging = true;
                origin = pos;
            }

            if (Dragging)
            {
                if (!pressed)
                {
                    Dragging = false;
                }
                else
                {
                    Vector2 delta = pos - origin;
                    float max = MaxRadiusPx;
                    if (delta.magnitude > max)
                    {
                        origin = pos - delta.normalized * max; // base follows the finger
                        delta = pos - origin;
                    }
                    stick = delta / max;
                    if (stick.magnitude < 0.08f) stick = Vector2.zero;
                    BaseScreen = origin;
                    KnobScreen = pos;
                }
            }
        }

        Move = kb.sqrMagnitude > 0.01f ? kb.normalized : Vector2.ClampMagnitude(stick, 1f);
    }
}
