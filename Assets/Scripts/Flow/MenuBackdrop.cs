using UnityEngine;

// Slowly drifting night stage behind every menu, with a dark wash and the occasional lightning strike.
public class MenuBackdrop
{
    private readonly StageView stage;
    private readonly SpriteRenderer wash;
    private readonly SpriteRenderer lightning;
    private readonly AudioManager audio;
    private readonly bool storm;
    private int tick;
    private int nextStrike = 150;
    private int strike;

    public MenuBackdrop(Transform world, Transform ui, AudioManager audio, float darkness, bool storm)
    {
        this.audio = audio;
        this.storm = storm;
        stage = new StageView(world, Roster.Data.stage);
        wash = Nodes.Rect(ui, "Wash", new Vector2(-GameConfig.HalfWidth, -GameConfig.HalfHeight),
            new Vector2(GameConfig.ScreenWidth, GameConfig.ScreenHeight), new Color(0.03f, 0f, 0.02f, darkness),
            Layers.Screen - 10);
        lightning = Nodes.Rect(ui, "Lightning", new Vector2(-GameConfig.HalfWidth, -GameConfig.HalfHeight),
            new Vector2(GameConfig.ScreenWidth, GameConfig.ScreenHeight), new Color(0.85f, 0.85f, 1f, 0f),
            Layers.Screen - 9);
    }

    public void Tick(RetroDisplay display)
    {
        tick++;
        float x = Mathf.Sin(tick * 0.0025f) * 110f;
        display.SetCamera(new Vector2(x, GameConfig.CameraY), Vector2.zero);
        stage.Sync(display.CameraCentre.x);

        if (!storm)
            return;
        if (--nextStrike <= 0)
        {
            strike = 9;
            nextStrike = Random.Range(220, 520);
            audio?.Sfx("thunder", 0.6f, 0.15f);
        }
        if (strike > 0)
        {
            strike--;
            float alpha = strike > 6 ? 0.55f : strike > 4 ? 0f : strike > 2 ? 0.3f : 0f;
            lightning.color = new Color(0.85f, 0.85f, 1f, alpha);
        }
    }
}
