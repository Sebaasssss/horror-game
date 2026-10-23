using UnityEngine;
using UnityEngine.InputSystem;

public enum GameMode { VsCpu, Versus }

public abstract class GameScreen
{
    protected GameFlow flow;
    protected Transform world;
    protected Transform ui;

    public void Attach(GameFlow owner)
    {
        flow = owner;
        world = Nodes.Group(owner.worldRoot, GetType().Name);
        ui = Nodes.Group(owner.display.uiRoot, GetType().Name);
    }

    public virtual void Enter()
    {
    }

    public abstract void Tick(InputFrame p1, InputFrame p2);

    public virtual void Exit()
    {
        Nodes.Destroy(world.gameObject);
        Nodes.Destroy(ui.gameObject);
    }
}

// Empty placeholder (used by tools that drive a Match directly).
public class BlankScreen : GameScreen
{
    public override void Tick(InputFrame p1, InputFrame p2)
    {
    }
}

// Hands a frame produced by GameFlow to a Match (keeps one Consume per tick for each keyboard).
public class RelayInput : IInputSource
{
    public InputFrame frame;

    public void Sample()
    {
    }

    public InputFrame Consume()
    {
        InputFrame value = frame;
        frame = default;
        return value;
    }
}

// Owns the display, audio, inputs and the active screen; advanced in fixed ticks by GameBoot.
public class GameFlow
{
    public readonly RetroDisplay display;
    public readonly AudioManager audio;
    public readonly Transform worldRoot;
    public readonly IInputSource input1, input2;
    public readonly RelayInput relay1 = new RelayInput(), relay2 = new RelayInput();

    public GameMode mode = GameMode.VsCpu;
    public CharacterDef pick1, pick2;
    public int cpuSkillLevel = 1;
    public bool escapePressed;
    public int tick;

    private GameScreen current, pending;
    private readonly SpriteRenderer fade;
    private int fadeTick;
    private const int FadeLength = 12;

    public GameFlow(Transform parent, RetroDisplay display, AudioManager audio, IInputSource input1,
        IInputSource input2)
    {
        this.display = display;
        this.audio = audio;
        this.input1 = input1;
        this.input2 = input2;
        worldRoot = Nodes.Group(parent, "World");
        fade = Nodes.Rect(display.uiRoot, "Fade", new Vector2(-GameConfig.HalfWidth - 2, -GameConfig.HalfHeight - 2),
            new Vector2(GameConfig.ScreenWidth + 4, GameConfig.ScreenHeight + 4), Color.black, Layers.Fade);
    }

    public GameScreen Current => current;

    public void Go(GameScreen next, bool instant = false)
    {
        next.Attach(this);
        if (current == null || instant)
        {
            current?.Exit();
            current = next;
            current.Enter();
            fadeTick = instant ? FadeLength : FadeLength;
            return;
        }
        pending = next;
        fadeTick = -FadeLength;
    }

    // Reads keyboard state every rendered frame so quick taps between ticks are kept.
    public void Sample()
    {
        input1.Sample();
        input2.Sample();
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;
        if (keyboard.f1Key.wasPressedThisFrame)
            display.CrtEnabled = !display.CrtEnabled;
        escapePressed |= keyboard.escapeKey.wasPressedThisFrame;
    }

    public void Tick()
    {
        tick++;
        InputFrame f1 = input1.Consume();
        InputFrame f2 = input2.Consume();
        relay1.frame = f1;
        relay2.frame = f2;

        if (fadeTick < 0)
        {
            fadeTick++;
            SetFade(1f - (-fadeTick) / (float)FadeLength);
            if (fadeTick == 0 && pending != null)
            {
                current?.Exit();
                current = pending;
                pending = null;
                current.Enter();
                fadeTick = FadeLength;
            }
            escapePressed = false;
            return;
        }

        if (fadeTick > 0)
        {
            fadeTick--;
            SetFade(fadeTick / (float)FadeLength);
        }

        current?.Tick(f1, f2);
        escapePressed = false;
        display.Tick(tick / GameConfig.TickRate);
    }

    private void SetFade(float alpha)
    {
        fade.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
        fade.enabled = alpha > 0.001f;
    }

    public InputFrame Merge(InputFrame a, InputFrame b)
    {
        return new InputFrame
        {
            x = a.x != 0 ? a.x : b.x,
            up = a.up || b.up,
            down = a.down || b.down,
            upPressed = a.upPressed || b.upPressed,
            downPressed = a.downPressed || b.downPressed,
            leftPressed = a.leftPressed || b.leftPressed,
            rightPressed = a.rightPressed || b.rightPressed,
            confirm = a.confirm || b.confirm,
            cancel = a.cancel || b.cancel,
            start = a.start || b.start
        };
    }

    public IInputSource SourceForSide(int side)
    {
        if (side == 1 && mode == GameMode.VsCpu)
            return new CpuInput(0.35f + cpuSkillLevel * 0.25f, Random.Range(0, 99999));
        return side == 0 ? relay1 : (IInputSource)relay2;
    }
}
