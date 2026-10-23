using System.Collections.Generic;
using UnityEngine;

// Pixel blood, floor stains, flying gore pieces, hit sparks and screen shake.
public class Fx
{
    private class Particle
    {
        public SpriteRenderer renderer;
        public Vector2 pos, vel;
        public int life;
        public bool alive;
    }

    private class GorePiece
    {
        public SpriteRenderer renderer;
        public Vector2 pos, vel;
        public float angle, spin;
        public int bleed;
        public bool resting;
        public Color blood;
    }

    private class PoolDecal
    {
        public SpriteRenderer renderer;
        public float size, target;
    }

    private class SparkAnim
    {
        public SpriteRenderer renderer;
        public Sprite[] frames;
        public int tick;
    }

    private class FountainEmitter
    {
        public Vector2 point;
        public Vector2 drift;
        public int ticks;
        public Color color;
        public System.Func<Vector2> follow;
        public int interval;
        public int age;
    }

    private const int MaxParticles = 2400;
    private const int MaxStains = 1600;

    private readonly Transform root;
    private readonly Particle[] particles = new Particle[MaxParticles];
    private readonly List<SpriteRenderer> stains = new List<SpriteRenderer>();
    private readonly List<GorePiece> gore = new List<GorePiece>();
    private readonly List<SparkAnim> sparks = new List<SparkAnim>();
    private readonly List<FountainEmitter> fountains = new List<FountainEmitter>();
    private readonly List<PoolDecal> pools = new List<PoolDecal>();
    private Sprite[] gibFrames;
    private Sprite poolSprite;
    private readonly System.Random random = new System.Random(1337);
    private int nextParticle;
    private int nextStain;
    private float trauma;
    private Sprite[] hitFrames, blockFrames, bloodFrames;

    public Fx(Transform parent)
    {
        root = Nodes.Group(parent, "FX");
        Transform particleRoot = Nodes.Group(root, "Particles");
        for (int i = 0; i < MaxParticles; i++)
        {
            SpriteRenderer renderer = Nodes.Rect(particleRoot, "p", Vector2.zero, Vector2.one, Color.red, Layers.Blood);
            renderer.enabled = false;
            particles[i] = new Particle { renderer = renderer };
        }
        hitFrames = Art.Sheet("UI/fx_hit", 32, 32, new Vector2(0.5f, 0.5f));
        blockFrames = Art.Sheet("UI/fx_block", 32, 32, new Vector2(0.5f, 0.5f));
        bloodFrames = Art.Sheet("UI/fx_blood", 32, 32, new Vector2(0.5f, 0.5f));
        gibFrames = Resources.Load<Texture2D>("UI/gibs") != null
            ? Art.Sheet("UI/gibs", 24, 24, new Vector2(0.5f, 0.5f)) : new Sprite[0];
        poolSprite = Art.Sprite("UI/blood_pool", new Vector2(0.5f, 0.5f));
    }

    private float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

    // ------------------------------------------------------------------ emitters

    public void Blood(Vector2 at, Vector2 direction, int count, Color color, float speed = 2.6f)
    {
        direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
        for (int i = 0; i < count; i++)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) + Range(-0.9f, 0.9f);
            float magnitude = speed * Range(0.35f, 1.25f);
            Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * magnitude + new Vector2(0f, Range(0.2f, 1.6f));
            Emit(at + new Vector2(Range(-2f, 2f), Range(-2f, 2f)), vel, color);
        }
    }

    public void Burst(Vector2 at, int count, Color color, float speed)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = Range(0f, Mathf.PI * 2f);
            float magnitude = speed * Range(0.2f, 1.2f);
            Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.8f + 0.9f) * magnitude;
            Emit(at + new Vector2(Range(-8f, 8f), Range(-14f, 14f)), vel, color);
        }
        Spark(at, "blood");
    }

    private void Emit(Vector2 pos, Vector2 vel, Color color)
    {
        Particle p = particles[nextParticle];
        nextParticle = (nextParticle + 1) % MaxParticles;
        p.pos = pos;
        p.vel = vel;
        p.life = 160;
        p.alive = true;
        float shade = Range(0.55f, 1.25f);
        Color tinted = new Color(Mathf.Clamp01(color.r * shade), color.g * shade, color.b * shade, 1f);
        if (random.NextDouble() < 0.08)
            tinted = Color.Lerp(tinted, new Color(1f, 0.45f, 0.45f), 0.6f);
        p.renderer.color = tinted;
        float size = random.NextDouble() < 0.3 ? 2f : 1f;
        p.renderer.transform.localScale = new Vector3(size, size, 1f);
        p.renderer.enabled = true;
    }

    public void Fountain(Vector2 point, Color color, int ticks, System.Func<Vector2> follow = null)
    {
        fountains.Add(new FountainEmitter { point = point, ticks = ticks, color = color, follow = follow });
    }

    // Endless slow drip (a bleeding stump); follow returns null to stop it.
    public void Drip(System.Func<Vector2?> follow, Color color, int interval)
    {
        fountains.Add(new FountainEmitter
        {
            ticks = -1, color = color, interval = interval,
            follow = () => follow() ?? new Vector2(float.NaN, float.NaN)
        });
    }

    // Chunks of flesh, bone and organs flying out of a wound.
    public void Gibs(Vector2 at, int count, float direction, Color blood)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 vel = new Vector2(direction * Range(0.6f, 3.6f) + Range(-1.4f, 1.4f), Range(2.5f, 7.5f));
            Sprite sprite = gibFrames.Length > 0 ? gibFrames[random.Next(gibFrames.Length)] : null;
            SpriteRenderer renderer = sprite != null
                ? Nodes.Sprite(root, "gib", sprite, at, Layers.Gore + 1)
                : Nodes.Rect(root, "gib", at, new Vector2(random.Next(2, 4), random.Next(2, 4)), blood * 0.8f, Layers.Gore + 1);
            if (sprite != null)
            {
                float scale = Range(0.45f, 0.85f);
                renderer.transform.localScale = new Vector3(scale, scale, 1f);
            }
            gore.Add(new GorePiece
            {
                renderer = renderer, pos = at, vel = vel, spin = Range(-24f, 24f), bleed = 30, blood = blood
            });
        }
    }

    // A pool of blood on the floor that spreads out over time.
    public void Pool(float x, float width, Color color)
    {
        if (poolSprite == null)
            return;
        SpriteRenderer renderer = Nodes.Sprite(root, "pool", poolSprite, new Vector2(Mathf.Round(x), -2f), Layers.Stains + 1,
            new Color(color.r * 0.8f, color.g * 0.8f, color.b * 0.8f, 1f));
        renderer.transform.localScale = new Vector3(0.1f, 1f, 1f);
        pools.Add(new PoolDecal { renderer = renderer, size = 0.1f, target = width / 64f });
    }

    public void Spark(Vector2 at, string kind)
    {
        Sprite[] frames = kind == "block" ? blockFrames : kind == "blood" ? bloodFrames : hitFrames;
        if (frames.Length == 0)
            return;
        SpriteRenderer renderer = Nodes.Sprite(root, "spark", frames[0], Nodes.Snap(at), Layers.Sparks);
        sparks.Add(new SparkAnim { renderer = renderer, frames = frames, tick = 0 });
    }

    public void Gore(Fighter fighter, GoreEntry entry, Vector2 velocity, float spin)
    {
        Sprite sprite = fighter.def.sprites[entry.sprite];
        Vector2 at = fighter.pos + new Vector2(entry.x * fighter.facing, entry.y);
        SpriteRenderer renderer = Nodes.Sprite(root, "gore_" + entry.part, sprite, at, Layers.Gore);
        renderer.transform.localScale = new Vector3(fighter.facing, 1f, 1f);
        gore.Add(new GorePiece
        {
            renderer = renderer, pos = at, vel = velocity, spin = spin, bleed = 90, blood = fighter.def.bloodColor
        });
    }

    public void Shake(float amount)
    {
        trauma = Mathf.Min(1.6f, trauma + amount);
    }

    public Vector2 ShakeOffset()
    {
        float magnitude = trauma * trauma * 6f;
        return new Vector2(Range(-magnitude, magnitude), Range(-magnitude, magnitude));
    }

    // ------------------------------------------------------------------ update

    public void Tick()
    {
        trauma = Mathf.Max(0f, trauma - 0.045f);

        for (int i = fountains.Count - 1; i >= 0; i--)
        {
            FountainEmitter f = fountains[i];
            Vector2 point = f.follow != null ? f.follow() : f.point;
            if (f.ticks < 0)
            {
                if (float.IsNaN(point.x))
                {
                    fountains.RemoveAt(i);
                    continue;
                }
                if (++f.age % Mathf.Max(1, f.interval) == 0)
                    Emit(point + new Vector2(Range(-1.5f, 1.5f), 0f), new Vector2(Range(-0.6f, 0.6f), Range(-0.2f, 1.4f)), f.color);
                continue;
            }
            int count = f.ticks > 60 ? 4 : f.ticks > 25 ? 2 : 1;
            for (int k = 0; k < count; k++)
                Emit(point, new Vector2(Range(-0.9f, 0.9f), Range(2.2f, 4.6f) * Mathf.Min(1f, f.ticks / 50f + 0.4f)),
                    f.color);
            if (--f.ticks <= 0)
                fountains.RemoveAt(i);
        }

        foreach (Particle p in particles)
        {
            if (!p.alive)
                continue;
            p.vel.y -= 0.2f;
            p.vel.x *= 0.99f;
            p.pos += p.vel;
            if (p.pos.y <= 0f && p.vel.y < 0f)
            {
                Stain(p.pos.x, p.renderer.color);
                Kill(p);
                continue;
            }
            if (--p.life <= 0 || Mathf.Abs(p.pos.x) > GameConfig.StageHalfWidth + 40f)
            {
                Kill(p);
                continue;
            }
            p.renderer.transform.localPosition = Nodes.Snap(p.pos);
        }

        foreach (PoolDecal pool in pools)
        {
            if (pool.size >= pool.target)
                continue;
            pool.size = Mathf.Min(pool.target, pool.size + 0.012f);
            pool.renderer.transform.localScale = new Vector3(Mathf.Round(pool.size * 64f) / 64f, 1f, 1f);
        }

        for (int i = sparks.Count - 1; i >= 0; i--)
        {
            SparkAnim s = sparks[i];
            s.tick++;
            int frame = s.tick / 3;
            if (frame >= s.frames.Length)
            {
                Nodes.Destroy(s.renderer.gameObject);
                sparks.RemoveAt(i);
                continue;
            }
            s.renderer.sprite = s.frames[frame];
        }

        foreach (GorePiece piece in gore)
        {
            if (!piece.resting)
            {
                piece.vel.y -= 0.24f;
                piece.pos += piece.vel;
                piece.angle += piece.spin;
                if (piece.pos.y <= 3f && piece.vel.y < 0f)
                {
                    piece.pos.y = 3f;
                    piece.vel = new Vector2(piece.vel.x * 0.55f, -piece.vel.y * 0.32f);
                    piece.spin *= 0.5f;
                    Blood(piece.pos, Vector2.up, 4, piece.blood, 1.4f);
                    if (Mathf.Abs(piece.vel.y) < 0.6f)
                    {
                        piece.resting = true;
                        piece.angle = Mathf.Round(piece.angle / 90f) * 90f;
                    }
                }
                float limit = GameConfig.StageHalfWidth - 6f;
                if (Mathf.Abs(piece.pos.x) > limit)
                {
                    piece.pos.x = Mathf.Sign(piece.pos.x) * limit;
                    piece.vel.x = -piece.vel.x * 0.5f;
                }
            }
            if (piece.bleed > 0)
            {
                piece.bleed--;
                if (piece.bleed % 2 == 0)
                    Emit(piece.pos, new Vector2(Range(-0.4f, 0.4f), Range(0.2f, 1.2f)), piece.blood);
            }
            piece.renderer.transform.localPosition = Nodes.Snap(piece.pos);
            piece.renderer.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Round(piece.angle / 15f) * 15f);
        }
    }

    private void Kill(Particle p)
    {
        p.alive = false;
        p.renderer.enabled = false;
    }

    private void Stain(float x, Color color)
    {
        SpriteRenderer stain;
        if (stains.Count < MaxStains)
        {
            stain = Nodes.Rect(root, "stain", Vector2.zero, Vector2.one, color, Layers.Stains);
            stains.Add(stain);
        }
        else
        {
            stain = stains[nextStain];
            nextStain = (nextStain + 1) % MaxStains;
        }
        float width = random.Next(1, 4);
        float y = -random.Next(0, 7);
        Color dark = color * 0.75f;
        dark.a = 1f;
        stain.color = dark;
        Nodes.SetRect(stain, new Vector2(Mathf.Round(x), y), new Vector2(width, 1f));
    }

    // Gore and airborne blood go away between rounds; floor stains stay for the whole match.
    public void ClearRound()
    {
        foreach (GorePiece piece in gore)
            Nodes.Destroy(piece.renderer.gameObject);
        gore.Clear();
        foreach (SparkAnim spark in sparks)
            Nodes.Destroy(spark.renderer.gameObject);
        sparks.Clear();
        foreach (PoolDecal pool in pools)
            Nodes.Destroy(pool.renderer.gameObject);
        pools.Clear();
        fountains.Clear();
        foreach (Particle p in particles)
            if (p.alive)
                Kill(p);
        trauma = 0f;
    }
}
