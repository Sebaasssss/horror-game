using UnityEngine;

// Scene entry point: builds the retro display, audio and game flow, then advances the game at a fixed 60 Hz.
public class GameBoot : MonoBehaviour
{
    [Tooltip("Skip menus and start a fight directly (handy while iterating on combat).")]
    public bool quickFight;
    public string quickFightP1 = "ghost";
    public string quickFightP2 = "jason";
    public GameMode quickFightMode = GameMode.VsCpu;

    private RetroDisplay display;
    private AudioManager audio;
    private GameFlow flow;
    private float accumulator;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 1;
        display = new RetroDisplay(transform);
        audio = new AudioManager(transform);
        flow = new GameFlow(transform, display, audio, new KeyboardInput(KeyBindings.PlayerOne),
            new KeyboardInput(KeyBindings.PlayerTwo));

        if (quickFight)
        {
            flow.mode = quickFightMode;
            flow.pick1 = Roster.Find(quickFightP1) ?? Roster.Characters[0];
            flow.pick2 = Roster.Find(quickFightP2) ?? Roster.Characters[Roster.Characters.Count - 1];
            flow.Go(new FightScreen(), true);
        }
        else
            flow.Go(new TitleScreen(), true);
    }

    private void Update()
    {
        flow.Sample();
        accumulator += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        int steps = 0;
        while (accumulator >= GameConfig.TickSeconds && steps < 4)
        {
            flow.Tick();
            accumulator -= GameConfig.TickSeconds;
            steps++;
        }
    }

    private void OnDestroy()
    {
        display?.Dispose();
    }
}
