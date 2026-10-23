using System.Collections.Generic;
using UnityEngine;

// Character select grid. Real characters fill the first slots; the rest are locked "?" slots for future fighters.
public class SelectScreen : GameScreen
{
    private class Side
    {
        public int slot;
        public bool locked;
        public SpriteRenderer cursor;
        public SpriteRenderer portrait;
        public PixelText name, subtitle;
        public int flash;
    }

    private const int Columns = 3;
    private MenuBackdrop backdrop;
    private readonly Side[] sides = { new Side(), new Side() };
    private readonly List<Vector2> slotPositions = new List<Vector2>();
    private List<CharacterDef> roster;
    private int slotCount;
    private int tick;
    private int cpuShuffle = -1;
    private int doneTicks = -1;

    public override void Enter()
    {
        roster = Roster.Characters;
        backdrop = new MenuBackdrop(world, ui, flow.audio, 0.72f, false);
        new PixelText(ui, "medium", "SELECCIONA TU LUCHADOR", new Vector2(0f, 90f), TextAlign.Center, Layers.Screen + 4);
        new PixelText(ui, "small", "F/K: ELEGIR    G/L: VOLVER", new Vector2(0f, -54f), TextAlign.Center,
            Layers.Screen + 4, new Color(0.65f, 0.55f, 0.55f));

        slotCount = Mathf.Max(roster.Count + Roster.Data.lockedSlots, Columns);
        slotCount = Mathf.CeilToInt(slotCount / (float)Columns) * Columns;
        int rows = slotCount / Columns;
        for (int i = 0; i < slotCount; i++)
        {
            int column = i % Columns, row = i / Columns;
            Vector2 position = new Vector2((column - (Columns - 1) / 2f) * 50f, 40f - row * 50f + (rows - 2) * 25f);
            slotPositions.Add(position);
            bool open = i < roster.Count;
            Nodes.Sprite(ui, "Slot", Art.Sprite(open ? "UI/cell" : "UI/cell_locked"), position, Layers.Screen + 2);
            if (open)
                Nodes.Sprite(ui, "Thumb", roster[i].thumb, position, Layers.Screen + 3);
        }

        for (int s = 0; s < 2; s++)
        {
            Side side = sides[s];
            side.slot = Mathf.Min(s, roster.Count - 1);
            string cursorSprite = s == 0 ? "UI/cursor_p1" : flow.mode == GameMode.VsCpu ? "UI/cursor_cpu" : "UI/cursor_p2";
            side.cursor = Nodes.Sprite(ui, "Cursor", Art.Sprite(cursorSprite, new Vector2(0.5f, 32f / 58f)),
                slotPositions[side.slot], Layers.Screen + 6 + s);
            float x = s == 0 ? -138f : 138f;
            side.portrait = Nodes.Sprite(ui, "Portrait", null, new Vector2(x, -70f), Layers.Screen + 3);
            side.portrait.transform.localScale = new Vector3(s == 0 ? 1f : -1f, 1f, 1f);
            Color tint = s == 0 ? GameConfig.P1Color : GameConfig.P2Color;
            new PixelText(ui, "small", s == 0 ? "1P" : flow.mode == GameMode.VsCpu ? "CPU" : "2P",
                new Vector2(x, 38f), TextAlign.Center, Layers.Screen + 4, tint);
            side.name = new PixelText(ui, "medium", "", new Vector2(x, -88f), TextAlign.Center, Layers.Screen + 4);
            side.subtitle = new PixelText(ui, "small", "", new Vector2(x, -100f), TextAlign.Center, Layers.Screen + 4,
                new Color(0.7f, 0.6f, 0.6f));
            Refresh(side);
        }
        if (flow.mode == GameMode.VsCpu)
            sides[1].cursor.enabled = false;
        flow.audio.Music("select");
    }

    private void Refresh(Side side)
    {
        side.cursor.transform.localPosition = slotPositions[side.slot];
        bool open = side.slot < roster.Count;
        side.portrait.sprite = open ? roster[side.slot].portrait : null;
        side.name.Set(open ? roster[side.slot].Name : "???");
        side.subtitle.Set(open ? roster[side.slot].data.subtitle : "PROXIMAMENTE");
    }

    public override void Tick(InputFrame p1, InputFrame p2)
    {
        tick++;
        backdrop.Tick(flow.display);
        if (flow.escapePressed)
        {
            flow.Go(new ModeScreen());
            return;
        }

        HandleSide(sides[0], p1, 0);
        if (flow.mode == GameMode.Versus)
            HandleSide(sides[1], p2, 1);
        else
            HandleCpu();

        for (int s = 0; s < 2; s++)
        {
            Side side = sides[s];
            if (side.flash > 0)
                side.flash--;
            side.portrait.color = side.flash % 4 >= 2 ? new Color(2f, 2f, 2f) : Color.white;
            if (!side.locked)
                side.cursor.color = tick / 6 % 2 == 0 ? Color.white : new Color(1f, 1f, 1f, 0.55f);
        }

        if (sides[0].locked && sides[1].locked)
        {
            if (doneTicks < 0)
                doneTicks = 45;
            else if (--doneTicks == 0)
            {
                flow.pick1 = roster[sides[0].slot];
                flow.pick2 = roster[sides[1].slot];
                flow.Go(new StageSelectScreen());
            }
        }
    }

    private void HandleSide(Side side, InputFrame input, int index)
    {
        if (side.locked)
        {
            if (input.cancel)
            {
                side.locked = false;
                doneTicks = -1;
                if (index == 0 && flow.mode == GameMode.VsCpu)
                {
                    sides[1].locked = false;
                    sides[1].cursor.enabled = false;
                    cpuShuffle = -1;
                }
                flow.audio.Sfx("ui_back");
            }
            return;
        }
        if (input.cancel && index == 0)
        {
            flow.Go(new ModeScreen());
            return;
        }

        int previous = side.slot;
        if (input.leftPressed) side.slot = Wrap(side.slot - 1);
        if (input.rightPressed) side.slot = Wrap(side.slot + 1);
        if (input.upPressed) side.slot = Wrap(side.slot - Columns);
        if (input.downPressed) side.slot = Wrap(side.slot + Columns);
        if (side.slot != previous)
        {
            flow.audio.Sfx("ui_move", 0.7f, 0f);
            Refresh(side);
        }

        if (input.confirm)
        {
            if (side.slot >= roster.Count)
            {
                flow.audio.Sfx("ui_back");
                return;
            }
            Lock(side);
            if (index == 0 && flow.mode == GameMode.VsCpu)
            {
                cpuShuffle = 50;
                sides[1].cursor.enabled = true;
            }
        }
    }

    private void HandleCpu()
    {
        if (cpuShuffle < 0 || sides[1].locked)
            return;
        Side cpu = sides[1];
        if (cpuShuffle % 5 == 0)
        {
            cpu.slot = Random.Range(0, roster.Count);
            flow.audio.Sfx("ui_move", 0.5f, 0f);
            Refresh(cpu);
        }
        if (--cpuShuffle <= 0)
        {
            if (roster.Count > 1 && cpu.slot == sides[0].slot)
                cpu.slot = (sides[0].slot + 1) % roster.Count;
            Refresh(cpu);
            Lock(cpu);
        }
    }

    private void Lock(Side side)
    {
        side.locked = true;
        side.flash = 16;
        side.cursor.color = Color.white;
        flow.audio.Sfx("ui_select");
        flow.audio.Sfx("announcer_" + roster[side.slot].Id, 0.9f, 0f);
    }

    private int Wrap(int slot) => (slot % slotCount + slotCount) % slotCount;
}

public class VersusScreen : GameScreen
{
    private SpriteRenderer left, right, badge;
    private PixelText leftName, rightName;
    private int tick;

    public override void Enter()
    {
        Nodes.Rect(ui, "Left", new Vector2(-GameConfig.HalfWidth, -GameConfig.HalfHeight),
            new Vector2(GameConfig.HalfWidth, GameConfig.ScreenHeight), new Color(0.05f, 0.07f, 0.16f), Layers.Screen);
        Nodes.Rect(ui, "Right", new Vector2(0f, -GameConfig.HalfHeight),
            new Vector2(GameConfig.HalfWidth, GameConfig.ScreenHeight), new Color(0.16f, 0.03f, 0.04f), Layers.Screen);
        for (int i = 0; i < 14; i++)
            Nodes.Rect(ui, "Stripe", new Vector2(-GameConfig.HalfWidth, -GameConfig.HalfHeight + i * 16f),
                new Vector2(GameConfig.ScreenWidth, 1f), new Color(0f, 0f, 0f, 0.35f), Layers.Screen + 1);

        left = Nodes.Sprite(ui, "P1", flow.pick1.portrait, new Vector2(-320f, -78f), Layers.Screen + 2);
        right = Nodes.Sprite(ui, "P2", flow.pick2.portrait, new Vector2(320f, -78f), Layers.Screen + 2);
        left.transform.localScale = new Vector3(2f, 2f, 1f);
        right.transform.localScale = new Vector3(-2f, 2f, 1f);
        badge = Nodes.Sprite(ui, "VS", Art.Sprite("UI/vs"), new Vector2(0f, 10f), Layers.Screen + 5);
        badge.enabled = false;
        leftName = new PixelText(ui, "large", flow.pick1.Name, new Vector2(-96f, -100f), TextAlign.Center,
            Layers.Screen + 6);
        rightName = new PixelText(ui, "large", flow.pick2.Name, new Vector2(96f, -100f), TextAlign.Center,
            Layers.Screen + 6);
        leftName.Visible = rightName.Visible = false;
        flow.audio.StopMusic();
        flow.audio.Sfx("versus_sting", 1f, 0f);
    }

    public override void Tick(InputFrame p1, InputFrame p2)
    {
        tick++;
        float t = Mathf.Clamp01(tick / 18f);
        float ease = 1f - (1f - t) * (1f - t);
        left.transform.localPosition = new Vector3(Mathf.Round(Mathf.Lerp(-330f, -100f, ease)), -78f, 0f);
        right.transform.localPosition = new Vector3(Mathf.Round(Mathf.Lerp(330f, 100f, ease)), -78f, 0f);
        if (tick == 20)
        {
            badge.enabled = true;
            leftName.Visible = rightName.Visible = true;
            flow.audio.Sfx("vs_hit");
        }
        if (tick > 20)
        {
            int shake = tick < 30 ? (tick % 2 == 0 ? 2 : -2) : 0;
            badge.transform.localPosition = new Vector3(shake, 10f, 0f);
        }
        bool skip = tick > 40 && (p1.confirm || p2.confirm);
        if (tick == 160 || skip)
            flow.Go(new FightScreen());
    }
}
