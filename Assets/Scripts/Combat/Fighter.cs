using UnityEngine;

public enum FighterState
{
    Intro, Idle, Walk, Crouch, Jump, Land, Attack, BlockStand, BlockCrouch, Hitstun, Launched, Lying, GetUp,
    Throwing, Thrown, Dizzy, KO, Victory, Dead, Exploded
}

// Simulation of one fighter, advanced in fixed 60 Hz ticks by Match. Positions are in pixels, y = 0 is the floor.
// `kit` is the current body state (full, one_arm, no_arms, one_leg, no_legs): it owns animations, moves and boxes.
public class Fighter
{
    private const int BufferTicks = 10;

    public readonly CharacterDef def;
    public readonly int index;
    public Fighter opponent;
    public VariantDef kit;
    public int limbsLost;

    public Vector2 pos;
    public Vector2 vel;
    public float pushVel;
    public int facing = 1;
    public int health;
    public int roundWins;
    public FighterState state = FighterState.Intro;
    public int stateTick;
    public int stunTicks;
    public InputFrame input;

    public MoveEntry move;
    public int moveTick;
    public bool moveConnected;
    public int chainCount;
    private bool airAttackUsed;
    private string bufferedAttack;
    private int bufferAge;

    public int comboTaken;
    public int juggleCount;
    public int flashTicks;
    public Color flashColor = Color.white;
    public float rotation;
    public bool hidden;
    public Fighter throwVictim;

    public Sprite sprite;
    // Purely visual vertical offset (hopping on one leg / bouncing on the torso).
    public float bob;
    private int hopTick;
    private AnimationEntry anim;
    private int animIndex;
    private int animTick;
    private string frameAnim = "idle";
    private int frameIndex;

    public Fighter(CharacterDef def, int index)
    {
        this.def = def;
        this.index = index;
        kit = def.Full;
        health = def.Stats.health;
        PlayAnim("idle", true);
    }

    public int MaxHealth => def.Stats.health;
    public bool Airborne => pos.y > 0.01f || vel.y > 0f;
    public bool IsDown => state == FighterState.Launched || state == FighterState.Lying || state == FighterState.GetUp;
    public bool Mutilated => kit != def.Full;
    public string FrameAnim => frameAnim;
    public int FrameIndex => frameIndex;

    public bool IsActive => state == FighterState.Attack && move != null && !moveConnected &&
                            moveTick >= move.startup && moveTick < move.startup + move.active;

    // Used for proximity guard: the opponent may hold back to block before the hit lands.
    public bool IsThreat => state == FighterState.Attack && move != null && !move.IsThrow &&
                            moveTick < move.startup + move.active;

    public bool InStartup => state == FighterState.Attack && move != null && moveTick < move.startup;

    public bool Neutral => state == FighterState.Idle || state == FighterState.Walk || state == FighterState.Crouch ||
                           ((state == FighterState.BlockStand || state == FighterState.BlockCrouch) && stunTicks <= 0);

    public bool Crouching => state == FighterState.Crouch || state == FighterState.BlockCrouch ||
                             (state == FighterState.Attack && move != null && move.state == "crouch") ||
                             (state == FighterState.Hitstun && frameIndex >= 4 && frameAnim == "hurt");

    public void ResetForRound(float x, int facingDirection)
    {
        kit = def.Full;
        limbsLost = 0;
        pos = new Vector2(x, 0f);
        vel = Vector2.zero;
        pushVel = 0f;
        facing = facingDirection;
        health = MaxHealth;
        state = FighterState.Intro;
        stateTick = 0;
        stunTicks = 0;
        move = null;
        comboTaken = 0;
        juggleCount = 0;
        flashTicks = 0;
        rotation = 0f;
        hidden = false;
        throwVictim = null;
        airAttackUsed = false;
        bufferedAttack = null;
        PlayAnim(kit.HasAnim("intro") ? "intro" : "idle", true);
    }

    // Switches body state after losing a limb; keeps the fighter in a sensible pose for the new kit.
    public void SetVariant(VariantDef variant)
    {
        kit = variant;
        limbsLost++;
        move = null;
        if (!kit.data.canJump)
        {
            pos.y = 0f;
            vel.y = 0f;
        }
        SetState(FighterState.Hitstun);
        stunTicks = 24;
        SetFrame("hurt", 0);
    }

    // ------------------------------------------------------------------ state

    public bool Hops => kit.Name == "one_leg" || kit.Name == "no_legs";

    public void SetState(FighterState next)
    {
        state = next;
        stateTick = 0;
    }

    public void Tick(Match match)
    {
        stateTick++;
        if (flashTicks > 0)
            flashTicks--;
        if (state != FighterState.Walk)
        {
            bob = Mathf.Max(0f, bob - 2f);
            hopTick = 0;
        }
        BufferInput();

        switch (state)
        {
            case FighterState.Intro:
            case FighterState.Victory:
            case FighterState.KO:
            case FighterState.Dead:
            case FighterState.Dizzy:
                AdvanceAnim();
                break;
            case FighterState.Idle:
            case FighterState.Walk:
            case FighterState.Crouch:
            case FighterState.BlockStand:
            case FighterState.BlockCrouch:
                TickNeutral(match);
                break;
            case FighterState.Land:
                if (stateTick >= 5)
                    ReturnToNeutral();
                break;
            case FighterState.Jump:
                TickJump(match);
                break;
            case FighterState.Attack:
                TickAttack(match);
                break;
            case FighterState.Hitstun:
                if (--stunTicks <= 0)
                    ReturnToNeutral();
                break;
            case FighterState.Launched:
                SetFrame("knockdown", vel.y > 1f ? 0 : vel.y > -2f ? 1 : 2);
                break;
            case FighterState.Lying:
                SetFrame("knockdown", stateTick < 8 ? 3 : 4);
                if (--stunTicks <= 0 && health > 0)
                {
                    SetState(FighterState.GetUp);
                    stunTicks = 20;
                    PlayAnim("getup", true);
                }
                break;
            case FighterState.GetUp:
                AdvanceAnimOver("getup", 20);
                if (--stunTicks <= 0)
                    ReturnToNeutral();
                break;
            case FighterState.Throwing:
                TickThrowing(match);
                break;
        }
    }

    private void BufferInput()
    {
        string pressed = input.heavy ? "heavy" : input.kick ? "kick" : input.light ? "light" : null;
        if (pressed != null)
        {
            bufferedAttack = pressed;
            bufferAge = 0;
        }
        else if (bufferedAttack != null && ++bufferAge > BufferTicks)
            bufferedAttack = null;
    }

    private MoveEntry ChooseGroundMove(InputFrame inp)
    {
        if (inp.grab && !inp.down && kit.data.canGrab)
            return kit.Move("grab");
        if (inp.down && kit.data.canCrouch)
        {
            string button = inp.heavy ? "heavy" : inp.kick ? "kick" : inp.light ? "light" : null;
            if (button == null)
                return null;
            return kit.MoveForInput(button, "crouch") ?? kit.MoveForState("crouch");
        }
        if (inp.heavy && inp.x == facing)
            return kit.MoveForInput("forward+heavy") ?? kit.Move("heavy");
        if (inp.kick && inp.x == -facing)
            return kit.MoveForInput("back+kick") ?? kit.Move("kick");
        if (inp.heavy)
            return kit.Move("heavy");
        if (inp.kick)
            return kit.Move("kick");
        if (inp.light)
            return kit.Move("light");
        return null;
    }

    private void TickNeutral(Match match)
    {
        bool inBlockstun = (state == FighterState.BlockStand || state == FighterState.BlockCrouch) && stunTicks > 0;
        if (inBlockstun)
        {
            stunTicks--;
            vel.x = 0f;
            return;
        }

        FaceOpponent();
        InputFrame inp = input;
        MoveEntry chosen = ChooseGroundMove(inp);
        if (chosen != null)
        {
            chainCount = 0;
            StartMove(chosen);
            return;
        }

        if (inp.up && kit.data.canJump)
        {
            SetState(FighterState.Jump);
            float jump = def.Stats.jumpVelocity * kit.data.jumpScale;
            vel = new Vector2(inp.x * def.Stats.jumpForward * kit.data.speedScale, jump);
            airAttackUsed = false;
            PlayAnim(inp.x == facing && kit.HasAnim("jump_forward") ? "jump_forward" : "jump_up", true);
            match.Sfx("jump", 0.5f);
            return;
        }

        bool holdingBack = inp.x == -facing;
        bool threatened = opponent != null && opponent.IsThreat && Mathf.Abs(opponent.pos.x - pos.x) < 120f;
        bool canBlock = kit.data.canBlock;
        if (inp.down && kit.data.canCrouch)
        {
            if (holdingBack && threatened && canBlock)
            {
                state = FighterState.BlockCrouch;
                SetFrame("block", 3);
            }
            else
            {
                if (state != FighterState.Crouch)
                    PlayAnim("crouch", true);
                state = FighterState.Crouch;
                AdvanceAnim();
            }
            vel.x = 0f;
        }
        else if (holdingBack && threatened && canBlock)
        {
            state = FighterState.BlockStand;
            vel.x = 0f;
            SetFrame("block", 1);
        }
        else if (inp.x != 0)
        {
            state = FighterState.Walk;
            float speed = inp.x == facing ? def.Stats.walkSpeed : def.Stats.backSpeed;
            vel.x = inp.x * speed * kit.data.speedScale;
            if (Hops)
            {
                // Hop locomotion: move in bursts while airborne, plant on landing.
                const int period = 22;
                float phase = (hopTick++ % period) / (float)period;
                float height = kit.Name == "no_legs" ? 7f : 9f;
                bob = Mathf.Round(Mathf.Sin(phase * Mathf.PI) * height);
                vel.x *= phase < 0.85f ? 1.7f : 0.1f;
                if (hopTick % period == 0)
                    match.OnHopLanded(this);
            }
            PlayAnim(inp.x == facing ? "walk_forward" : "walk_back");
            AdvanceAnim();
        }
        else
        {
            state = FighterState.Idle;
            vel.x = 0f;
            PlayAnim("idle");
            AdvanceAnim();
        }
    }

    private void TickJump(Match match)
    {
        InputFrame inp = input;
        if (!airAttackUsed && inp.AnyAttack)
        {
            MoveEntry air = inp.kick ? kit.MoveForInput("kick", "air") : null;
            air ??= kit.MoveForState("air");
            if (air != null)
            {
                StartMove(air);
                return;
            }
        }
        AdvanceAnimOver(frameAnim, 34);
    }

    private void StartMove(MoveEntry entry)
    {
        bool air = entry.state == "air";
        SetState(FighterState.Attack);
        move = entry;
        moveTick = 0;
        moveConnected = false;
        bufferedAttack = null;
        if (air)
            airAttackUsed = true;
        else
            vel.x = 0f;
        UpdateMoveFrame();
    }

    private void TickAttack(Match match)
    {
        moveTick++;
        bool air = move.state == "air";
        if (!air)
        {
            bool lunging = move.lunge > 0f && moveTick >= move.startup - 2 && moveTick < move.startup + move.active;
            vel.x = lunging ? facing * move.lunge * Mathf.Max(0.5f, kit.data.speedScale) : 0f;
        }
        if (moveTick == move.startup)
            match.Sfx(move.sfxWhiff, 0.7f);

        // Chain cancel: a connected (hit or blocked) move can flow into its follow-up.
        if (moveConnected && bufferedAttack != null && move.chains != null && moveTick >= move.startup)
        {
            foreach (ChainEntry chain in move.chains)
            {
                if (chain.input != bufferedAttack)
                    continue;
                MoveEntry next = kit.Move(chain.move);
                if (next == null)
                    continue;
                chainCount++;
                StartMove(next);
                return;
            }
        }

        UpdateMoveFrame();
        if (!air && moveTick >= move.Total)
        {
            if (move.state == "crouch" && input.down)
            {
                SetState(FighterState.Crouch);
                PlayAnim("crouch", true);
                SetFrame("crouch", 3);
                move = null;
            }
            else
                ReturnToNeutral();
        }
    }

    private void UpdateMoveFrame()
    {
        int[] frames;
        float local;
        if (moveTick < move.startup)
        {
            frames = move.startupFrames;
            local = moveTick / (float)Mathf.Max(1, move.startup);
        }
        else if (moveTick < move.startup + move.active || move.recoveryFrames == null || move.recoveryFrames.Length == 0)
        {
            frames = move.activeFrames;
            local = (moveTick - move.startup) / (float)Mathf.Max(1, move.active);
        }
        else
        {
            frames = move.recoveryFrames;
            local = (moveTick - move.startup - move.active) / (float)Mathf.Max(1, move.recovery);
        }

        if (frames == null || frames.Length == 0)
            frames = move.activeFrames;
        int index = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(local) * frames.Length), 0, frames.Length - 1);
        SetFrame(move.anim, frames[index]);
    }

    // ------------------------------------------------------------------ throws

    public void BeginThrow(Fighter victim)
    {
        SetState(FighterState.Throwing);
        throwVictim = victim;
        vel = Vector2.zero;
        victim.SetState(FighterState.Thrown);
        victim.move = null;
        victim.vel = Vector2.zero;
        victim.pushVel = 0f;
        victim.facing = -facing;
        victim.SetFrame(victim.kit.HasAnim("thrown") ? "thrown" : "hurt", 0);
    }

    private void TickThrowing(Match match)
    {
        int t = stateTick;
        string throwAnim = move != null ? move.anim : "grab_throw";
        SetFrame(throwAnim, t < 6 ? 1 : t < 16 ? 2 : t < 24 ? 3 : t < 32 ? 4 : 5);
        if (throwVictim != null && throwVictim.state == FighterState.Thrown)
        {
            if (t < 26)
                match.PlaceThrowVictim(this, throwVictim, t);
            if (t == 26)
                match.ReleaseThrow(this, throwVictim);
        }
        if (t >= 46)
        {
            throwVictim = null;
            ReturnToNeutral();
        }
    }

    // ------------------------------------------------------------------ getting hit

    public void EnterHitstun(int ticks, string level)
    {
        bool crouched = Crouching;
        SetState(FighterState.Hitstun);
        move = null;
        stunTicks = ticks;
        vel.x = 0f;
        int frame = crouched ? 4 : (level == "high" || level == "overhead" ? 0 : 2);
        SetFrame("hurt", frame + (Random.value < 0.5f ? 1 : 0));
    }

    public void EnterBlockstun(int ticks)
    {
        bool crouched = state == FighterState.BlockCrouch || input.down;
        state = crouched ? FighterState.BlockCrouch : FighterState.BlockStand;
        stateTick = 0;
        stunTicks = ticks;
        vel.x = 0f;
        SetFrame("block", crouched ? 3 : 1);
    }

    public void Launch(float vx, float vy)
    {
        SetState(FighterState.Launched);
        move = null;
        float jump = kit.data.canJump ? 1f : 0.35f;
        vel = new Vector2(vx, vy * jump);
        pos.y = Mathf.Max(pos.y, 1f);
        juggleCount++;
        rotation = 0f;
        SetFrame("knockdown", 0);
    }

    public void ReturnToNeutral()
    {
        SetState(FighterState.Idle);
        move = null;
        comboTaken = 0;
        juggleCount = 0;
        stunTicks = 0;
        rotation = 0f;
        chainCount = 0;
        PlayAnim("idle", true);
    }

    public void FaceOpponent()
    {
        if (opponent == null || Airborne)
            return;
        float dx = opponent.pos.x - pos.x;
        if (Mathf.Abs(dx) > 1f)
            facing = dx > 0f ? 1 : -1;
    }

    // ------------------------------------------------------------------ physics

    public void Physics(Match match)
    {
        if (state == FighterState.Thrown || state == FighterState.Exploded)
            return;

        bool wasAirborne = Airborne;
        if (wasAirborne)
            vel.y -= def.Stats.gravity;

        pos += vel;
        pos.x += pushVel;
        pushVel *= 0.8f;
        if (Mathf.Abs(pushVel) < 0.05f)
            pushVel = 0f;

        if (pos.y <= 0f)
        {
            pos.y = 0f;
            if (wasAirborne && vel.y <= 0f)
            {
                vel.y = 0f;
                Land(match);
            }
        }
    }

    private void Land(Match match)
    {
        switch (state)
        {
            case FighterState.Jump:
                SetState(FighterState.Land);
                vel.x = 0f;
                SetFrame("crouch", 1);
                match.Sfx("land", 0.5f);
                break;
            case FighterState.Attack:
                if (move != null && move.state == "air")
                {
                    move = null;
                    SetState(FighterState.Land);
                    vel.x = 0f;
                    SetFrame("crouch", 1);
                    match.Sfx("land", 0.5f);
                }
                break;
            case FighterState.Launched:
                SetState(FighterState.Lying);
                stunTicks = 36;
                vel = Vector2.zero;
                pushVel = 0f;
                juggleCount = 0;
                SetFrame("knockdown", 3);
                match.OnFighterLanded(this);
                break;
        }
    }

    // ------------------------------------------------------------------ boxes

    public Rect? Hurtbox()
    {
        switch (state)
        {
            case FighterState.Lying:
            case FighterState.GetUp:
            case FighterState.Thrown:
            case FighterState.Exploded:
            case FighterState.Dead:
            case FighterState.KO:
            case FighterState.Intro:
            case FighterState.Victory:
                return null;
            case FighterState.Launched:
                return juggleCount < 3 ? kit.data.hurtAir.ToWorld(pos, facing) : (Rect?)null;
            case FighterState.Jump:
                return kit.data.hurtAir.ToWorld(pos, facing);
        }
        if (state == FighterState.Attack && move != null && move.state == "air")
            return kit.data.hurtAir.ToWorld(pos, facing);
        return Crouching ? kit.data.hurtCrouch.ToWorld(pos, facing) : kit.data.hurtStand.ToWorld(pos, facing);
    }

    public Rect StandBox => kit.data.hurtStand.ToWorld(pos, facing);

    public Rect Hitbox() => move.hitbox.ToWorld(pos, facing);

    public Vector2 StumpPoint => pos + new Vector2(kit.data.stumpX * facing, kit.data.stumpY);

    public Vector2 NeckPoint => pos + new Vector2(def.data.neckX * facing, def.data.neckY);

    public bool CanBeThrown => !Airborne && (Neutral || state == FighterState.Dizzy || state == FighterState.Land ||
                               (state == FighterState.Attack && move != null && moveTick >= move.startup + move.active));

    public bool CanBlock(string level, bool attackerAirborne)
    {
        if (!kit.data.canBlock || Airborne || !(Neutral || state == FighterState.BlockStand || state == FighterState.BlockCrouch))
            return false;
        if (input.x != -facing)
            return false;
        bool crouched = input.down;
        if (level == "low")
            return crouched;
        if (level == "overhead" || attackerAirborne)
            return !crouched;
        return true;
    }

    public Vector2 HandAnchor()
    {
        AnimationEntry entry = kit.Anim(frameAnim);
        if (entry != null && entry.anchorX != null && entry.anchorX.Length > frameIndex && frameIndex >= 0)
            return new Vector2(entry.anchorX[frameIndex], entry.anchorY[frameIndex]);
        return new Vector2(30f, 60f);
    }

    // ------------------------------------------------------------------ animation

    public void PlayAnim(string name, bool restart = false)
    {
        if (!restart && anim != null && frameAnim == name && anim == kit.Anim(name))
            return;
        anim = kit.Anim(name);
        animIndex = 0;
        animTick = 0;
        frameAnim = name;
        frameIndex = 0;
        sprite = SpriteOf(anim, 0);
    }

    public void AdvanceAnim()
    {
        if (anim == null || anim.ticks == null || anim.ticks.Length == 0)
            return;
        animTick++;
        int duration = anim.ticks[Mathf.Min(animIndex, anim.ticks.Length - 1)];
        if (animTick < duration)
            return;
        animTick = 0;
        if (animIndex < anim.sprites.Length - 1)
            animIndex++;
        else if (anim.loop)
            animIndex = 0;
        frameIndex = animIndex;
        sprite = SpriteOf(anim, animIndex);
    }

    // Spreads an animation evenly over a fixed duration (getup, jump arcs).
    private void AdvanceAnimOver(string name, int totalTicks)
    {
        AnimationEntry entry = kit.Anim(name);
        if (entry == null || entry.sprites.Length == 0)
            return;
        int index = Mathf.Clamp(stateTick * entry.sprites.Length / Mathf.Max(1, totalTicks), 0, entry.sprites.Length - 1);
        SetFrame(name, index);
    }

    public void SetFrame(string animation, int index)
    {
        AnimationEntry entry = kit.Anim(animation);
        frameAnim = animation;
        frameIndex = entry != null ? Mathf.Clamp(index, 0, entry.sprites.Length - 1) : 0;
        sprite = SpriteOf(entry, frameIndex);
    }

    private Sprite SpriteOf(AnimationEntry entry, int index)
    {
        if (entry == null || entry.sprites == null || entry.sprites.Length == 0)
            return sprite;
        return def.SpriteAt(entry.sprites[Mathf.Clamp(index, 0, entry.sprites.Length - 1)]);
    }
}
