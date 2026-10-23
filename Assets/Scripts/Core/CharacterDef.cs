using System;
using System.Collections.Generic;
using UnityEngine;

// JSON shapes written by Tools/pixelart/*.py into Resources/Characters/<id>/character.json.
[Serializable]
public class PixelRect
{
    public float x, y, w, h;

    public Rect ToWorld(Vector2 origin, int facing)
    {
        float left = facing >= 0 ? x : -(x + w);
        return new Rect(origin.x + left, origin.y + y, w, h);
    }
}

[Serializable]
public class SpriteEntry
{
    public string name;
    public int cell;
    public float pivotX, pivotY;
}

[Serializable]
public class AnimationEntry
{
    public string name;
    public int[] sprites;
    public int[] ticks;
    public bool loop;
    public float[] anchorX;
    public float[] anchorY;
}

[Serializable]
public class MoveEntry
{
    public string id, input, state, anim, name, level;
    public int[] startupFrames, activeFrames, recoveryFrames;
    public int startup, active, recovery, damage, hitstun, blockstun, blood, hitstop;
    public float pushback, lunge, shake;
    public bool knockdown;
    public PixelRect hitbox;
    public string sfxWhiff, sfxHit;

    public int Total => startup + active + recovery;
    public bool IsThrow => level == "throw";
}

[Serializable]
public class GoreEntry
{
    public string part;
    public int sprite;
    public float x, y;
}

[Serializable]
public class StatsEntry
{
    public int health;
    public float walkSpeed, backSpeed, jumpVelocity, jumpForward, gravity, pushWidth, weight;
}

[Serializable]
public class CharacterData
{
    public string id, displayName, subtitle, author;
    public int frameW, frameH, columns;
    public SpriteEntry[] sprites;
    public AnimationEntry[] animations;
    public MoveEntry[] moves;
    public GoreEntry[] gore;
    public StatsEntry stats;
    public PixelRect hurtStand, hurtCrouch, hurtAir;
    public string bloodColor, accentColor;
    public float neckX, neckY;
}

[Serializable]
public class RosterData
{
    public string[] characters;
    public int lockedSlots;
    public string stage;
}

// Runtime view of a character: parsed data plus sprites cut from its atlas.
public class CharacterDef
{
    public readonly CharacterData data;
    public readonly Sprite[] sprites;
    public readonly Sprite portrait;
    public readonly Sprite thumb;
    public readonly Color bloodColor;
    public readonly Color accentColor;
    private readonly Dictionary<string, AnimationEntry> animations = new Dictionary<string, AnimationEntry>();
    private readonly Dictionary<string, MoveEntry> moves = new Dictionary<string, MoveEntry>();

    public string Id => data.id;
    public string Name => data.displayName;
    public StatsEntry Stats => data.stats;

    private CharacterDef(CharacterData data, Texture2D atlas, Sprite portrait, Sprite thumb)
    {
        this.data = data;
        this.portrait = portrait;
        this.thumb = thumb;
        bloodColor = GameConfig.Hex(data.bloodColor, new Color(0.55f, 0.05f, 0.08f));
        accentColor = GameConfig.Hex(data.accentColor, Color.white);

        sprites = new Sprite[data.sprites.Length];
        for (int i = 0; i < data.sprites.Length; i++)
        {
            SpriteEntry entry = data.sprites[i];
            int column = entry.cell % data.columns;
            int row = entry.cell / data.columns;
            Rect rect = new Rect(column * data.frameW, atlas.height - (row + 1) * data.frameH, data.frameW, data.frameH);
            Vector2 pivot = new Vector2(entry.pivotX / data.frameW, entry.pivotY / data.frameH);
            sprites[i] = Sprite.Create(atlas, rect, pivot, 1f, 0, SpriteMeshType.FullRect);
            sprites[i].name = data.id + "_" + entry.name;
        }

        foreach (AnimationEntry animation in data.animations)
            animations[animation.name] = animation;
        foreach (MoveEntry move in data.moves)
            moves[move.id] = move;
    }

    public static CharacterDef Load(string id)
    {
        TextAsset json = Resources.Load<TextAsset>("Characters/" + id + "/character");
        Texture2D atlas = Art.Texture("Characters/" + id + "/atlas");
        if (json == null || atlas == null)
        {
            Debug.LogError("Character '" + id + "' is missing character.json or atlas.png");
            return null;
        }

        CharacterData data = JsonUtility.FromJson<CharacterData>(json.text);
        Sprite portrait = Art.Sprite("Characters/" + id + "/portrait", new Vector2(0.5f, 0f));
        Sprite thumb = Art.Sprite("Characters/" + id + "/thumb", new Vector2(0.5f, 0.5f));
        return new CharacterDef(data, atlas, portrait, thumb);
    }

    public AnimationEntry Anim(string name)
    {
        if (animations.TryGetValue(name, out AnimationEntry animation))
            return animation;
        return animations.TryGetValue("idle", out animation) ? animation : null;
    }

    public bool HasAnim(string name) => animations.ContainsKey(name);

    public Sprite Frame(string animation, int index)
    {
        AnimationEntry entry = Anim(animation);
        if (entry == null || entry.sprites.Length == 0)
            return null;
        return sprites[entry.sprites[Mathf.Clamp(index, 0, entry.sprites.Length - 1)]];
    }

    public MoveEntry Move(string id)
    {
        return moves.TryGetValue(id, out MoveEntry move) ? move : null;
    }

    public MoveEntry MoveForState(string state)
    {
        foreach (MoveEntry move in data.moves)
            if (move.state == state)
                return move;
        return null;
    }

    public MoveEntry MoveForInput(string input)
    {
        foreach (MoveEntry move in data.moves)
            if (move.input == input)
                return move;
        return null;
    }

    public Vector2 HandAnchor(string animation, int index)
    {
        AnimationEntry entry = Anim(animation);
        if (entry != null && entry.anchorX != null && entry.anchorX.Length > index && index >= 0)
            return new Vector2(entry.anchorX[index], entry.anchorY[index]);
        return new Vector2(30f, 60f);
    }
}

// Parallax stage made of layers in Resources/Stages/<id>/.
[Serializable]
public class StageLayer
{
    public string file;
    public float parallax;
}

[Serializable]
public class StageData
{
    public string id, displayName;
    public float groundY;
    public StageLayer[] layers;

    public static StageData Load(string id)
    {
        TextAsset json = Resources.Load<TextAsset>("Stages/" + id + "/stage");
        return json != null ? JsonUtility.FromJson<StageData>(json.text) : null;
    }
}

public static class Roster
{
    private static List<CharacterDef> characters;
    private static RosterData data;

    public static RosterData Data
    {
        get
        {
            if (data == null)
            {
                TextAsset json = Resources.Load<TextAsset>("Characters/roster");
                data = json != null ? JsonUtility.FromJson<RosterData>(json.text)
                    : new RosterData { characters = new[] { "ghost", "jason" }, lockedSlots = 4, stage = "fallback" };
            }
            return data;
        }
    }

    public static List<CharacterDef> Characters
    {
        get
        {
            if (characters == null)
            {
                characters = new List<CharacterDef>();
                foreach (string id in Data.characters)
                {
                    CharacterDef def = CharacterDef.Load(id);
                    if (def != null)
                        characters.Add(def);
                }
            }
            return characters;
        }
    }

    public static CharacterDef Find(string id)
    {
        foreach (CharacterDef def in Characters)
            if (def.Id == id)
                return def;
        return null;
    }
}
