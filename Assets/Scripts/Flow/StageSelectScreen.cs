using System.Collections.Generic;
using UnityEngine;

// Stage picker: one card per stage in roster.json plus RANDOM. Either player can choose.
public class StageSelectScreen : GameScreen
{
    private const int Columns = 4;
    private readonly List<string> stages = new List<string>();
    private readonly List<SpriteRenderer> frames = new List<SpriteRenderer>();
    private PixelText title, label;
    private int selected;
    private int tick;
    private StageView preview;
    private string previewId;

    public override void Enter()
    {
        string[] ids = Roster.Data.stages != null && Roster.Data.stages.Length > 0
            ? Roster.Data.stages : new[] { Roster.Data.stage };
        foreach (string id in ids)
            if (StageData.Load(id) != null)
                stages.Add(id);
        if (stages.Count == 0)
            stages.Add("fallback");

        Nodes.Rect(ui, "Dim", new Vector2(-GameConfig.HalfWidth, -GameConfig.HalfHeight),
            new Vector2(GameConfig.ScreenWidth, 70f), new Color(0f, 0f, 0f, 0.75f), Layers.Screen);
        title = new PixelText(ui, "medium", "ELIGE EL ESCENARIO", new Vector2(0f, 92f), TextAlign.Center,
            Layers.Screen + 4);
        label = new PixelText(ui, "large", "", new Vector2(0f, -78f), TextAlign.Center, Layers.Screen + 4);
        new PixelText(ui, "small", "FLECHAS: CAMBIAR   F/K: ELEGIR   G/L: VOLVER", new Vector2(0f, -100f),
            TextAlign.Center, Layers.Screen + 4, new Color(0.7f, 0.6f, 0.6f));

        int count = stages.Count + 1;
        int rows = Mathf.CeilToInt(count / (float)Columns);
        for (int i = 0; i < count; i++)
        {
            int column = i % Columns, row = i / Columns;
            int inRow = Mathf.Min(Columns, count - row * Columns);
            float x = (column - (inRow - 1) / 2f) * 70f;
            float y = 14f - row * 46f + (rows - 2) * 23f;
            Sprite thumb = i < stages.Count ? Art.Sprite("Stages/" + stages[i] + "/thumb") : Art.Sprite("UI/cell_locked");
            Nodes.Sprite(ui, "Card", thumb, new Vector2(x, y), Layers.Screen + 2);
            SpriteRenderer frame = Nodes.Sprite(ui, "Frame", Art.Sprite("UI/cursor_p1", new Vector2(0.5f, 32f / 58f)),
                new Vector2(x, y), Layers.Screen + 3);
            frame.transform.localScale = new Vector3(1.2f, 0.9f, 1f);
            frames.Add(frame);
        }
        Refresh();
    }

    private void Refresh()
    {
        for (int i = 0; i < frames.Count; i++)
            frames[i].enabled = i == selected;
        string id = selected < stages.Count ? stages[selected] : stages[0];
        StageData data = StageData.Load(id);
        label.Set(selected < stages.Count ? (data != null ? data.displayName : id) : "RANDOM");
        if (previewId != id)
        {
            Nodes.Destroy(world.gameObject);
            world = Nodes.Group(flow.worldRoot, "Stage Preview");
            preview = new StageView(world, id);
            previewId = id;
        }
    }

    public override void Tick(InputFrame p1, InputFrame p2)
    {
        tick++;
        float x = Mathf.Sin(tick * 0.01f) * 120f;
        flow.display.SetCamera(new Vector2(x, GameConfig.CameraY), Vector2.zero);
        preview?.Sync(flow.display.CameraCentre.x);
        InputFrame input = flow.Merge(p1, p2);
        int count = stages.Count + 1;
        if (input.leftPressed || input.rightPressed || input.upPressed || input.downPressed)
        {
            int step = input.rightPressed ? 1 : input.leftPressed ? -1 : input.downPressed ? Columns : -Columns;
            selected = (selected + step + count) % count;
            flow.audio.Sfx("ui_move", 0.7f, 0f);
            Refresh();
        }
        frames[selected].color = tick / 6 % 2 == 0 ? Color.white : new Color(1f, 1f, 1f, 0.5f);
        if (input.cancel || flow.escapePressed)
        {
            flow.Go(new SelectScreen());
            return;
        }
        if (input.confirm)
        {
            flow.stageId = selected < stages.Count ? stages[selected] : stages[Random.Range(0, stages.Count)];
            flow.audio.Sfx("ui_select");
            flow.Go(new VersusScreen());
        }
    }
}
