using System.Collections.Generic;
using UnityEngine;

// Fight HUD: health bars with damage trail, round skulls, timer, names, combo counter, banners and screen flash.
public class Hud
{
    private class Splat
    {
        public SpriteRenderer blob, streak;
        public float length, speed;
        public int age, life;
    }

    private class Banner
    {
        public PixelText text;
        public int ticks, age;
        public float y;
    }

    private const int FillWidth = 140;
    private const int FillHeight = 9;
    private const float BarTop = 104f;
    private const float BarOuter = 190f;

    private readonly Transform root;
    private readonly Fighter[] fighters;
    private readonly SpriteRenderer[] fills = new SpriteRenderer[2];
    private readonly SpriteRenderer[] trails = new SpriteRenderer[2];
    private readonly SpriteRenderer[,] skulls = new SpriteRenderer[2, GameConfig.RoundsToWin];
    private readonly PixelText[] combos = new PixelText[2];
    private readonly int[] comboTicks = new int[2];
    private readonly float[] trailValue = { 1f, 1f };
    private readonly int[] trailDelay = new int[2];
    private readonly PixelText timer;
    private readonly List<Banner> banners = new List<Banner>();
    private readonly List<Splat> splats = new List<Splat>();
    private readonly PixelText[] callouts = new PixelText[2];
    private readonly int[] calloutTicks = new int[2];
    private readonly SpriteRenderer flash;
    private Sprite skullOn, skullOff;
    private int flashTicks, flashLength;
    private int tick;

    public Hud(Transform parent, Fighter p1, Fighter p2)
    {
        root = Nodes.Group(parent, "HUD");
        fighters = new[] { p1, p2 };
        skullOn = Art.Sprite("UI/skull_on");
        skullOff = Art.Sprite("UI/skull_off");
        Sprite frame = Art.Sprite("UI/bar_frame", new Vector2(0f, 1f));

        for (int side = 0; side < 2; side++)
        {
            int sign = side == 0 ? -1 : 1;
            SpriteRenderer frameRenderer = Nodes.Sprite(root, "Bar Frame", frame, new Vector2(sign * BarOuter, BarTop),
                Layers.Hud + 2);
            frameRenderer.transform.localScale = new Vector3(-sign, 1f, 1f);
            Vector2 fillOrigin = FillOrigin(side, 1f);
            Nodes.Rect(root, "Bar Back", fillOrigin - Vector2.one, new Vector2(FillWidth + 2, FillHeight + 2),
                new Color(0.03f, 0.02f, 0.025f), Layers.Hud - 1);
            trails[side] = Nodes.Rect(root, "Trail", fillOrigin, new Vector2(FillWidth, FillHeight),
                new Color(1f, 0.25f, 0.2f), Layers.Hud);
            fills[side] = Nodes.Sprite(root, "Fill", Art.Sprite("UI/bar_fill", Vector2.zero), fillOrigin, Layers.Hud + 1);
            fills[side].transform.localScale = new Vector3(FillWidth, 1f, 1f);

            string name = fighters[side].def.Name;
            new PixelText(root, "small", name, new Vector2(sign * (BarOuter - 12f), 80f),
                side == 0 ? TextAlign.Left : TextAlign.Right, Layers.Hud + 3);
            for (int k = 0; k < GameConfig.RoundsToWin; k++)
            {
                float x = sign * (36f + k * 13f);
                skulls[side, k] = Nodes.Sprite(root, "Skull", skullOff, new Vector2(x, 84f), Layers.Hud + 3);
            }
            combos[side] = new PixelText(root, "medium", "", new Vector2(sign * (BarOuter - 12f), 46f),
                side == 0 ? TextAlign.Left : TextAlign.Right, Layers.Hud + 4);
            callouts[side] = new PixelText(root, "small", "", new Vector2(sign * (BarOuter - 12f), 34f),
                side == 0 ? TextAlign.Left : TextAlign.Right, Layers.Hud + 4, new Color(1f, 0.35f, 0.3f));
        }

        Nodes.Sprite(root, "Timer Box", Art.Sprite("UI/timer_box"), new Vector2(0f, 92f), Layers.Hud + 2);
        timer = new PixelText(root, "medium", "99", new Vector2(0f, 85f), TextAlign.Center, Layers.Hud + 3);

        flash = Nodes.Rect(root, "Flash", new Vector2(-GameConfig.HalfWidth, -GameConfig.HalfHeight),
            new Vector2(GameConfig.ScreenWidth, GameConfig.ScreenHeight), new Color(1f, 1f, 1f, 0f), Layers.Overlay);
    }

    private static Vector2 FillOrigin(int side, float amount)
    {
        float width = FillWidth * amount;
        float y = BarTop - 3f - FillHeight;
        return side == 0
            ? new Vector2(-BarOuter + 11f, y)
            : new Vector2(BarOuter - 11f - width, y);
    }

    public void Tick(Match match)
    {
        tick++;
        for (int side = 0; side < 2; side++)
        {
            Fighter f = fighters[side];
            float amount = Mathf.Clamp01(f.health / (float)f.MaxHealth);
            float width = Mathf.Round(FillWidth * amount);
            fills[side].transform.localPosition = FillOrigin(side, width / FillWidth);
            fills[side].transform.localScale = new Vector3(width, 1f, 1f);
            Color barColor = amount > 0.5f ? new Color(1f, 0.82f, 0.22f)
                : amount > 0.2f ? new Color(1f, 0.52f, 0.12f) : new Color(0.95f, 0.12f, 0.1f);
            if (amount <= 0.2f && tick / 6 % 2 == 0)
                barColor = Color.Lerp(barColor, Color.white, 0.5f);
            fills[side].color = barColor;

            if (trailValue[side] > amount)
            {
                if (trailDelay[side] > 0)
                    trailDelay[side]--;
                else
                    trailValue[side] = Mathf.Max(amount, trailValue[side] - 0.008f);
            }
            else
            {
                trailValue[side] = amount;
                trailDelay[side] = 30;
            }
            float trailWidth = Mathf.Round(FillWidth * trailValue[side]);
            Nodes.SetRect(trails[side], FillOrigin(side, trailWidth / FillWidth), new Vector2(trailWidth, FillHeight));

            for (int k = 0; k < GameConfig.RoundsToWin; k++)
                skulls[side, k].sprite = f.roundWins > k ? skullOn : skullOff;

            if (comboTicks[side] > 0 && --comboTicks[side] == 0)
                combos[side].Set("");
            if (calloutTicks[side] > 0 && --calloutTicks[side] == 0)
                callouts[side].Set("");
        }

        timer.Set(Mathf.Max(0, match.timer).ToString("00"));
        timer.Color = match.timer <= 10 && tick / 15 % 2 == 0 ? new Color(1f, 0.4f, 0.4f) : Color.white;

        for (int i = banners.Count - 1; i >= 0; i--)
        {
            Banner banner = banners[i];
            banner.age++;
            float drop = Mathf.Max(0f, 8 - banner.age) * 3f;
            banner.text.Position = new Vector2(0f, banner.y + drop);
            banner.text.Visible = banner.ticks - banner.age > 12 || (banner.age / 3) % 2 == 0;
            banner.text.Tick();
            if (banner.age >= banner.ticks)
            {
                banner.text.Destroy();
                banners.RemoveAt(i);
            }
        }

        for (int i = splats.Count - 1; i >= 0; i--)
        {
            Splat splat = splats[i];
            splat.age++;
            splat.length += splat.speed;
            splat.speed *= 0.985f;
            Vector3 p = splat.blob.transform.localPosition;
            Nodes.SetRect(splat.streak, new Vector2(p.x + 1f, p.y - Mathf.Round(splat.length)),
                new Vector2(Mathf.Max(1f, splat.blob.transform.localScale.x - 2f), Mathf.Round(splat.length)));
            float alpha = Mathf.Clamp01((splat.life - splat.age) / 40f);
            Color c = splat.blob.color;
            c.a = alpha;
            foreach (SpriteRenderer part in splat.blob.GetComponentsInChildren<SpriteRenderer>())
                part.color = c;
            splat.streak.color = c;
            if (splat.age >= splat.life)
            {
                Nodes.Destroy(splat.blob.gameObject);
                Nodes.Destroy(splat.streak.gameObject);
                splats.RemoveAt(i);
            }
        }

        if (flashTicks > 0)
        {
            flashTicks--;
            Color c = flash.color;
            c.a = 0.85f * flashTicks / Mathf.Max(1, flashLength);
            flash.color = c;
        }
    }

    public void ShowBanner(string text, string font, int ticks, float y = 14f, float wave = 0f)
    {
        PixelText label = new PixelText(root, font, text, new Vector2(0f, y), TextAlign.Center, Layers.Banner);
        label.Wave = wave;
        banners.Add(new Banner { text = label, ticks = ticks, y = y });
    }

    public void ClearBanners()
    {
        foreach (Banner banner in banners)
            banner.text.Destroy();
        banners.Clear();
    }

    public void Combo(Fighter attacker, int hits)
    {
        if (hits < 2)
            return;
        int side = attacker.index;
        combos[side].Set(hits + " HITS");
        comboTicks[side] = 70;
    }

    // Blood thrown onto the "screen glass": blobs that run down and fade.
    public void Splatter(int count, Color blood)
    {
        for (int i = 0; i < count; i++)
        {
            float size = Random.Range(3, 9);
            Vector2 at = new Vector2(Mathf.Round(Random.Range(-180f, 180f)), Mathf.Round(Random.Range(-60f, 90f)));
            Color color = new Color(blood.r * Random.Range(0.8f, 1.3f), blood.g, blood.b, 1f);
            SpriteRenderer blob = Nodes.Rect(root, "Splat", at, new Vector2(size, Mathf.Round(size * 0.7f)), color,
                Layers.Overlay - 30);
            // Irregular edges: a few smaller lobes and loose droplets around the main blob.
            for (int k = 0; k < 4; k++)
            {
                float lobe = Mathf.Max(1f, Mathf.Round(size * Random.Range(0.3f, 0.6f)));
                Vector2 offset = new Vector2(Random.Range(-size * 0.6f, size), Random.Range(-size * 0.4f, size * 0.8f));
                Nodes.Rect(blob.transform, "Lobe", offset / size, new Vector2(lobe / size, lobe / (size * 0.7f)),
                    color, Layers.Overlay - 30);
            }
            for (int k = 0; k < 3; k++)
            {
                Vector2 drop = new Vector2(Random.Range(-2.5f, 3.5f), Random.Range(-1.5f, 2.5f));
                Nodes.Rect(blob.transform, "Drop", drop, new Vector2(1f / size, 1f / (size * 0.7f)), color,
                    Layers.Overlay - 30);
            }
            SpriteRenderer streak = Nodes.Rect(root, "Streak", at, Vector2.one, color, Layers.Overlay - 31);
            splats.Add(new Splat
            {
                blob = blob, streak = streak, speed = Random.Range(0.15f, 0.6f), life = Random.Range(110, 200)
            });
        }
    }

    public void Callout(Fighter fighter, string text)
    {
        int side = fighter.index;
        callouts[side].Set(text);
        calloutTicks[side] = 60;
    }

    public void Flash(Color color, int ticks)
    {
        color.a = 0.85f;
        flash.color = color;
        flashTicks = ticks;
        flashLength = ticks;
    }

    public void SetVisible(bool visible)
    {
        root.gameObject.SetActive(visible);
    }

    public void ClearSplatter()
    {
        foreach (Splat splat in splats)
        {
            Nodes.Destroy(splat.blob.gameObject);
            Nodes.Destroy(splat.streak.gameObject);
        }
        splats.Clear();
    }

    public void Destroy()
    {
        Nodes.Destroy(root.gameObject);
    }

    public void ResetTrails()
    {
        trailValue[0] = trailValue[1] = 1f;
    }
}
