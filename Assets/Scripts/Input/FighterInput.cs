using UnityEngine;
using UnityEngine.InputSystem;

// One simulation tick of controls. Directions are held state; buttons are presses since the last tick.
public struct InputFrame
{
    public int x;
    public bool up, down;
    public bool light, heavy, kick, grab;
    public bool upPressed, downPressed, leftPressed, rightPressed;
    public bool confirm, cancel, start;

    public bool AnyAttack => light || heavy || kick;
}

public interface IInputSource
{
    // Called every rendered frame so short taps between ticks are never lost.
    void Sample();
    InputFrame Consume();
}

public class KeyBindings
{
    public Key left, right, up, down, light, heavy, kick, grab, start;
    public Key[] altLight = new Key[0], altHeavy = new Key[0], altKick = new Key[0], altGrab = new Key[0];

    // P1: WASD + F/G/H, R grab. Enter / F confirm, G back.
    public static KeyBindings PlayerOne => new KeyBindings
    {
        left = Key.A, right = Key.D, up = Key.W, down = Key.S,
        light = Key.F, heavy = Key.G, kick = Key.H, grab = Key.R, start = Key.Enter,
        altLight = new[] { Key.Space }
    };

    // P2: arrows + K/L/; (Ñ on a Spanish layout), P grab, with numpad alternatives.
    public static KeyBindings PlayerTwo => new KeyBindings
    {
        left = Key.LeftArrow, right = Key.RightArrow, up = Key.UpArrow, down = Key.DownArrow,
        light = Key.K, heavy = Key.L, kick = Key.Semicolon, grab = Key.P, start = Key.NumpadEnter,
        altLight = new[] { Key.Numpad1 }, altHeavy = new[] { Key.Numpad2 }, altKick = new[] { Key.Numpad3 },
        altGrab = new[] { Key.Numpad0 }
    };
}

public class KeyboardInput : IInputSource
{
    private readonly KeyBindings keys;
    private InputFrame pending;

    public KeyboardInput(KeyBindings keys)
    {
        this.keys = keys;
    }

    public void Sample()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        bool left = keyboard[keys.left].isPressed;
        bool right = keyboard[keys.right].isPressed;
        pending.x = (right ? 1 : 0) - (left ? 1 : 0);
        pending.up = keyboard[keys.up].isPressed;
        pending.down = keyboard[keys.down].isPressed;

        pending.upPressed |= keyboard[keys.up].wasPressedThisFrame;
        pending.downPressed |= keyboard[keys.down].wasPressedThisFrame;
        pending.leftPressed |= keyboard[keys.left].wasPressedThisFrame;
        pending.rightPressed |= keyboard[keys.right].wasPressedThisFrame;

        bool light = Pressed(keyboard, keys.light, keys.altLight);
        bool heavy = Pressed(keyboard, keys.heavy, keys.altHeavy);
        pending.light |= light;
        pending.heavy |= heavy;
        pending.kick |= Pressed(keyboard, keys.kick, keys.altKick);
        pending.grab |= Pressed(keyboard, keys.grab, keys.altGrab);
        bool start = keyboard[keys.start].wasPressedThisFrame;
        pending.start |= start;
        pending.confirm |= light || start;
        pending.cancel |= heavy;
    }

    public InputFrame Consume()
    {
        InputFrame frame = pending;
        pending = new InputFrame { x = frame.x, up = frame.up, down = frame.down };
        return frame;
    }

    private static bool Pressed(Keyboard keyboard, Key main, Key[] alternatives)
    {
        if (keyboard[main].wasPressedThisFrame)
            return true;
        foreach (Key key in alternatives)
            if (keyboard[key].wasPressedThisFrame)
                return true;
        return false;
    }
}

// Merges several sources (used by menus so either player can navigate).
public class MergedInput : IInputSource
{
    private readonly IInputSource[] sources;

    public MergedInput(params IInputSource[] sources)
    {
        this.sources = sources;
    }

    public void Sample()
    {
    }

    public InputFrame Consume()
    {
        InputFrame merged = default;
        foreach (IInputSource source in sources)
        {
            InputFrame frame = source.Consume();
            merged.x = merged.x != 0 ? merged.x : frame.x;
            merged.up |= frame.up;
            merged.down |= frame.down;
            merged.light |= frame.light;
            merged.heavy |= frame.heavy;
            merged.kick |= frame.kick;
            merged.grab |= frame.grab;
            merged.upPressed |= frame.upPressed;
            merged.downPressed |= frame.downPressed;
            merged.leftPressed |= frame.leftPressed;
            merged.rightPressed |= frame.rightPressed;
            merged.confirm |= frame.confirm;
            merged.cancel |= frame.cancel;
            merged.start |= frame.start;
        }
        return merged;
    }
}

// Deterministic input for automated captures and tests.
public class ScriptedInput : IInputSource
{
    private readonly System.Func<int, InputFrame> script;
    private int tick;

    public ScriptedInput(System.Func<int, InputFrame> script)
    {
        this.script = script;
    }

    public void Sample()
    {
    }

    public InputFrame Consume()
    {
        return script(tick++);
    }
}
