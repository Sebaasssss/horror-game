using UnityEngine;

public class FightScreen : GameScreen
{
    private Match match;
    private Transform pause;
    private MenuList pauseMenu;
    private int endTicks = -1;

    public override void Enter()
    {
        match = new Match(world, flow.display, flow.audio, flow.pick1, flow.pick2, flow.SourceForSide(0),
            flow.SourceForSide(1), flow.stageId ?? Roster.Data.stage);
        StageData stage = StageData.Load(flow.stageId ?? Roster.Data.stage);
        flow.audio.Music(stage != null && !string.IsNullOrEmpty(stage.music) ? stage.music : "fight");
    }

    public Match Match => match;

    public override void Tick(InputFrame p1, InputFrame p2)
    {
        if (pause != null)
        {
            int choice = pauseMenu.Tick(flow.Merge(p1, p2));
            if (choice == 0 || flow.escapePressed)
                ClosePause();
            else if (choice == 1)
                flow.Go(new TitleScreen());
            return;
        }
        if (flow.escapePressed && endTicks < 0)
        {
            OpenPause();
            return;
        }

        match.Tick();
        if (match.Finished && endTicks < 0)
        {
            endTicks = 0;
            Fighter winner = match.winner ?? (match.p1.roundWins >= match.p2.roundWins ? match.p1 : match.p2);
            flow.Go(new ResultsScreen(winner.def, winner.index, match.fatality));
        }
    }

    private void OpenPause()
    {
        pause = Nodes.Group(ui, "Pause");
        Nodes.Rect(pause, "Dim", new Vector2(-GameConfig.HalfWidth, -GameConfig.HalfHeight),
            new Vector2(GameConfig.ScreenWidth, GameConfig.ScreenHeight), new Color(0f, 0f, 0f, 0.6f), Layers.Screen);
        Nodes.Panel(pause, "Panel", new Vector2(-90f, -40f), new Vector2(180f, 92f), Layers.Screen + 2);
        new PixelText(pause, "large", "PAUSA", new Vector2(0f, 22f), TextAlign.Center, Layers.Screen + 5);
        pauseMenu = new MenuList(pause, flow.audio, new[] { "CONTINUAR", "MENU PRINCIPAL" }, -6f, 18f);
        flow.audio.Sfx("ui_select");
    }

    private void ClosePause()
    {
        Nodes.Destroy(pause.gameObject);
        pause = null;
        pauseMenu = null;
    }

    public override void Exit()
    {
        match.Dispose();
        base.Exit();
    }
}

public class ResultsScreen : GameScreen
{
    private readonly CharacterDef winner;
    private readonly int side;
    private readonly bool fatality;
    private MenuBackdrop backdrop;
    private MenuList menu;
    private PixelText title;

    private static readonly string[] Quotes =
    {
        "NADIE ESCAPA DE LA NOCHE.", "VUELVE CUANDO TENGAS VALOR.", "TU SANGRE MANCHA MI PISO.",
        "EL LAGO GUARDA TU SECRETO.", "OTRA VICTIMA PARA LA LISTA."
    };

    public ResultsScreen(CharacterDef winner, int side, bool fatality)
    {
        this.winner = winner;
        this.side = side;
        this.fatality = fatality;
    }

    public override void Enter()
    {
        backdrop = new MenuBackdrop(world, ui, flow.audio, 0.68f, true);
        SpriteRenderer portrait = Nodes.Sprite(ui, "Winner", winner.portrait, new Vector2(-112f, -108f),
            Layers.Screen + 2);
        portrait.transform.localScale = new Vector3(2f, 2f, 1f);
        string who = side == 0 ? "1P" : flow.mode == GameMode.VsCpu ? "CPU" : "2P";
        new PixelText(ui, "small", who, new Vector2(60f, 90f), TextAlign.Center, Layers.Screen + 4,
            side == 0 ? GameConfig.P1Color : GameConfig.P2Color);
        title = new PixelText(ui, "large", winner.Name + " GANA", new Vector2(60f, 64f), TextAlign.Center,
            Layers.Screen + 4);
        title.Wave = 1f;
        if (fatality)
            new PixelText(ui, "blood", "FATALITY", new Vector2(60f, 36f), TextAlign.Center, Layers.Screen + 4);
        new PixelText(ui, "small", "\"" + Quotes[Random.Range(0, Quotes.Length)] + "\"", new Vector2(60f, 18f),
            TextAlign.Center, Layers.Screen + 4, new Color(0.85f, 0.75f, 0.7f));
        Nodes.Panel(ui, "Panel", new Vector2(-10f, -96f), new Vector2(140f, 84f), Layers.Screen + 2);
        menu = new MenuList(ui, flow.audio, new[] { "REVANCHA", "SELECCION", "MENU" }, -30f, 20f, 60f);
        flow.audio.Music("results");
    }

    public override void Tick(InputFrame p1, InputFrame p2)
    {
        backdrop.Tick(flow.display);
        title.Tick();
        switch (menu.Tick(flow.Merge(p1, p2)))
        {
            case 0:
                flow.Go(new VersusScreen());
                break;
            case 1:
                flow.Go(new SelectScreen());
                break;
            case 2:
                flow.Go(new TitleScreen());
                break;
        }
    }
}
