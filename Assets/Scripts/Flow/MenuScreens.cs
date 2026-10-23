using UnityEngine;

public class TitleScreen : GameScreen
{
    private MenuBackdrop backdrop;
    private PixelText press;
    private SpriteRenderer logo;
    private int tick;

    public override void Enter()
    {
        backdrop = new MenuBackdrop(world, ui, flow.audio, 0.35f, true);
        logo = Nodes.Sprite(ui, "Logo", Art.Sprite("UI/logo", new Vector2(0.5f, 1f)), new Vector2(0f, 102f),
            Layers.Screen + 2);
        press = new PixelText(ui, "medium", "PRESIONA START", new Vector2(0f, -62f), TextAlign.Center,
            Layers.Screen + 3);
        new PixelText(ui, "small", "(C) 2026 ROCKY + CODEX   F1: CRT", new Vector2(0f, -98f), TextAlign.Center,
            Layers.Screen + 3, new Color(0.6f, 0.5f, 0.5f));
        flow.audio.Music("menu");
    }

    public override void Tick(InputFrame p1, InputFrame p2)
    {
        tick++;
        backdrop.Tick(flow.display);
        press.Visible = tick / 24 % 2 == 0;
        float bob = Mathf.Round(Mathf.Sin(tick * 0.05f) * 1.5f);
        logo.transform.localPosition = new Vector3(0f, 102f + bob, 0f);
        if (p1.confirm || p2.confirm || p1.start || p2.start)
        {
            flow.audio.Sfx("ui_start");
            flow.Go(new ModeScreen());
        }
    }
}

public class ModeScreen : GameScreen
{
    private MenuBackdrop backdrop;
    private MenuList menu;
    private Transform controls;
    private static readonly string[] Difficulty = { "FACIL", "NORMAL", "DIFICIL" };

    public override void Enter()
    {
        backdrop = new MenuBackdrop(world, ui, flow.audio, 0.55f, true);
        Nodes.Sprite(ui, "Logo", Art.Sprite("UI/logo", new Vector2(0.5f, 1f)), new Vector2(0f, 104f),
            Layers.Screen + 1).transform.localScale = Vector3.one;
        Nodes.Panel(ui, "Panel", new Vector2(-104f, -98f), new Vector2(208f, 112f), Layers.Screen + 2);
        menu = new MenuList(ui, flow.audio, new[]
        {
            "1 JUGADOR VS CPU", "2 JUGADORES", "CPU: " + Difficulty[flow.cpuSkillLevel],
            "CRT: " + (flow.display.CrtEnabled ? "SI" : "NO"), "CONTROLES"
        }, -12f, 18f);
    }

    public override void Tick(InputFrame p1, InputFrame p2)
    {
        backdrop.Tick(flow.display);
        InputFrame input = flow.Merge(p1, p2);
        if (controls != null)
        {
            if (input.confirm || input.cancel || flow.escapePressed)
            {
                Nodes.Destroy(controls.gameObject);
                controls = null;
            }
            return;
        }
        if (input.cancel || flow.escapePressed)
        {
            flow.Go(new TitleScreen());
            return;
        }

        switch (menu.Tick(input))
        {
            case 0:
                flow.mode = GameMode.VsCpu;
                flow.Go(new SelectScreen());
                break;
            case 1:
                flow.mode = GameMode.Versus;
                flow.Go(new SelectScreen());
                break;
            case 2:
                flow.cpuSkillLevel = (flow.cpuSkillLevel + 1) % Difficulty.Length;
                menu.SetLabel(2, "CPU: " + Difficulty[flow.cpuSkillLevel]);
                break;
            case 3:
                flow.display.CrtEnabled = !flow.display.CrtEnabled;
                menu.SetLabel(3, "CRT: " + (flow.display.CrtEnabled ? "SI" : "NO"));
                break;
            case 4:
                ShowControls();
                break;
        }
    }

    private void ShowControls()
    {
        controls = Nodes.Group(ui, "Controls");
        Nodes.Panel(controls, "Panel", new Vector2(-180f, -100f), new Vector2(360f, 196f), Layers.Overlay - 20);
        string[] left =
        {
            "JUGADOR 1", "", "MOVER     A D", "SALTAR    W", "AGACHAR   S", "GOLPE     F",
            "FUERTE    G", "PATADA    H", "AGARRE    R", "ESPECIAL  ADELANTE+G", "GIRATORIA ATRAS+H"
        };
        string[] right =
        {
            "JUGADOR 2", "", "MOVER     < >", "SALTAR    ARRIBA", "AGACHAR   ABAJO", "GOLPE     K",
            "FUERTE    L", "PATADA    N (;)", "AGARRE    P", "ESPECIAL  ADELANTE+L", "GIRATORIA ATRAS+N"
        };
        for (int i = 0; i < left.Length; i++)
        {
            Color color = i == 0 ? new Color(0.45f, 0.7f, 1f) : Color.white;
            new PixelText(controls, "small", left[i], new Vector2(-168f, 78f - i * 12f), TextAlign.Left,
                Layers.Overlay - 10, color);
            color = i == 0 ? new Color(1f, 0.45f, 0.45f) : Color.white;
            new PixelText(controls, "small", right[i], new Vector2(8f, 78f - i * 12f), TextAlign.Left,
                Layers.Overlay - 10, color);
        }
        new PixelText(controls, "small", "COMBOS: F F F / G G / H H   ATRAS = BLOQUEAR", new Vector2(0f, -56f),
            TextAlign.Center, Layers.Overlay - 10, new Color(1f, 0.85f, 0.5f));
        new PixelText(controls, "small", "ESC: PAUSA   F1: FILTRO CRT", new Vector2(0f, -70f), TextAlign.Center,
            Layers.Overlay - 10, new Color(1f, 0.85f, 0.5f));
        new PixelText(controls, "small", "FATALITY: G DECAPITA  H EN TROZOS  F/R EXPLOTA", new Vector2(0f, -86f),
            TextAlign.Center, Layers.Overlay - 10, new Color(1f, 0.4f, 0.4f));
    }
}
