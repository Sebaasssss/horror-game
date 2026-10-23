using UnityEngine;

public enum FighterState
{
    Intro, Idle, Walk, Crouch, Jump, Attack, BlockStand, BlockCrouch, Hitstun, Launched, Lying, GetUp,
    Throwing, Thrown, Dizzy, KO, Victory, Dead, Exploded
}

// Simulation of one fighter, advanced in fixed 60 Hz ticks by Match. Positions are in pixels, y = 0 is the floor.
public class Fighter
{
    public readonly CharacterDef def;
    public readonly int index;
    public Fighter opponent;

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
    private bool airAttackUsed;

    public int comboTaken;
    public int juggleCount;
    public int flashTicks;
    public Color flashColor = Color.white;
    public float rotation;
    public bool hidden;
    public Fighter throwVictim;

    public Sprite sprite;
    private AnimationEntry anim;
    private int animIndex;
    private int animTick;
    private string frameAnim = "idle";
    private int frameIndex;

    public Fighter(CharacterDef def, int index)
    {
        this.def = def;
        this.index = index;
        health = def.Stats.health;
        PlayAnim("idle", true);
    }

    public int MaxHealth => def.Stats.health;
    public bool Airborne => pos.y > 0.01f || vel.y > 0f;
    public bool IsDown => state == FighterState.Launched || state == FighterState.Lying || state == FighterState.GetUp;
    public string FrameAnim => frameAnim;
    public int FrameIndex => frameIndex;

    public bool IsActive => state == FighterState.Attack && move != null && !moveConnected &&
                            moveTick >= move.startup && moveTick < move.startup + move.active;

    // Used for proximity guard: the opponent may hold back to block before the hit lands.
    public bool IsThreat => state == FighterState.Attack && move != null && !move.IsThrow &&
                            moveTick < move.startup + move.active;

    public bool Neutral => state == FighterState.Idle || state == FighterState.Walk || state == FighterState.Crouch ||
                           ((state == FighterState.BlockStand || state == FighterState.BlockCrouch) && stunTicks <= 0);

    public bool Crouching => state == FighterState.Crouch || state == FighterState.BlockCrouch ||
                             (state == FighterState.Attack && move != null && move.state == "crouch") ||
                             (state == FighterState.Hitstun && frameIndex == 2 && frameAnim == "hurt");

    public void ResetForRound(float x, int facingDirection)
    {
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
        PlayAnim("idle", true);
    }

    // ------------------------------------------------------------------ state

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
                SetFrame("knockdown", vel.y > 0f ? 0 : 1);
                break;
            case FighterState.Lying:
                if (--stunTicks <= 0 && health > 0)
                {
                    SetState(FighterState.GetUp);
                    stunTicks = 18;
                    SetFrame("knockdown", 3);
                }
                break;
            case FighterState.GetUp:
                if (--stunTicks <= 0)
                    ReturnToNeutral();
                break;
            case FighterState.Throwing:
                TickThrowing(match);
                break;
        }
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
        MoveEntry chosen = null;
        if (inp.grab && !inp.down)
            chosen = def.Move("grab");
        else if (inp.heavy && inp.x == facing && !inp.down)
            chosen = def.MoveForInput("forward+heavy") ?? def.Move("heavy");
        else if (inp.down && inp.AnyAttack)
            chosen = def.MoveForState("crouch");
        else if (inp.heavy)
            chosen = def.Move("heavy");
        else if (inp.kick)
            chosen = def.Move("kick");
        else if (inp.light)
            chosen = def.Move("light");

        if (chosen != null)
        {
            StartMove(chosen, match);
            return;
        }

        if (inp.up)
        {
            SetState(FighterState.Jump);
            vel = new Vector2(inp.x * def.Stats.jumpForward, def.Stats.jumpVelocity);
            airAttackUsed = false;
            SetFrame("jump", 0);
            match.Sfx("jump", 0.5f);
            return;
        }

        bool holdingBack = inp.x == -facing;
        bool threatened = opponent != null && opponent.IsThreat && Mathf.Abs(opponent.pos.x - pos.x) < 120f;
        if (inp.down)
        {
            if (holdingBack && threatened)
            {
                state = FighterState.BlockCrouch;
                SetFrame("block", 1);
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
        else if (holdingBack && threatened)
        {
            state = FighterState.BlockStand;
            vel.x = 0f;
            SetFrame("block", 0);
        }
        else if (inp.x != 0)
        {
            state = FighterState.Walk;
            vel.x = inp.x * (inp.x == facing ? def.Stats.walkSpeed : def.Stats.backSpeed);
            PlayAnim("walk");
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
            MoveEntry air = def.MoveForState("air");
            if (air != null)
            {
                StartMove(air, match);
                return;
            }
        }
        SetFrame("jump", stateTick < 5 ? 0 : (vel.y > 0f ? 1 : 2));
    }

    private void StartMove(MoveEntry entry, Match match)
    {
        bool air = entry.state == "air";
        SetState(FighterState.Attack);
        move = entry;
        moveTick = 0;
        moveConnected = false;
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
            vel.x = lunging ? facing * move.lunge : 0f;
        }
        if (moveTick == move.startup)
            match.Sfx(move.sfxWhiff, 0.7f);

        UpdateMoveFrame();
        if (!air && moveTick >= move.Total)
        {
            if (move.state == "crouch" && input.down)
            {
                SetState(FighterState.Crouch);
                PlayAnim("crouch", true);
                SetFrame("crouch", 1);
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
        victim.SetFrame("hurt", 0);
    }

    private void TickThrowing(Match match)
    {
        int t = stateTick;
        string throwAnim = move != null ? move.anim : "grab_throw";
        SetFrame(throwAnim, t < 10 ? 1 : t < 24 ? 2 : t < 32 ? 3 : 4);
        if (throwVictim != null && throwVictim.state == FighterState.Thrown)
        {
            if (t < 24)
                match.PlaceThrowVictim(this, throwVictim, t);
            if (t == 24)
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
        int frame = crouched ? 2 : (level == "high" || level == "overhead" ? 0 : 1);
        SetFrame("hurt", frame);
    }

    public void EnterBlockstun(int ticks)
    {
        bool crouched = state == FighterState.BlockCrouch || input.down;
        state = crouched ? FighterState.BlockCrouch : FighterState.BlockStand;
        stateTick = 0;
        stunTicks = ticks;
        vel.x = 0f;
        SetFrame("block", crouched ? 1 : 0);
    }

    public void Launch(float vx, float vy)
    {
        SetState(FighterState.Launched);
        move = null;
        vel = new Vector2(vx, vy);
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
                ReturnToNeutral();
                vel.x = 0f;
                match.Sfx("land", 0.5f);
                break;
            case FighterState.Attack:
                if (move != null && move.state == "air")
                {
                    ReturnToNeutral();
                    vel.x = 0f;
                    match.Sfx("land", 0.5f);
                }
                break;
            case FighterState.Launched:
                SetState(FighterState.Lying);
                stunTicks = 36;
                vel = Vector2.zero;
                pushVel = 0f;
                juggleCount = 0;
                SetFrame("knockdown", 2);
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
                return juggleCount < 3 ? def.data.hurtAir.ToWorld(pos, facing) : (Rect?)null;
            case FighterState.Jump:
                return def.data.hurtAir.ToWorld(pos, facing);
        }
        if (state == FighterState.Attack && move != null && move.state == "air")
            return def.data.hurtAir.ToWorld(pos, facing);
        return Crouching ? def.data.hurtCrouch.ToWorld(pos, facing) : def.data.hurtStand.ToWorld(pos, facing);
    }

    public Rect Hitbox() => move.hitbox.ToWorld(pos, facing);

    public bool CanBeThrown => !Airborne && (Neutral || state == FighterState.Dizzy ||
                               (state == FighterState.Attack && move != null && moveTick >= move.startup + move.active));

    public bool CanBlock(string level, bool attackerAirborne)
    {
        if (Airborne || !(Neutral || state == FighterState.BlockStand || state == FighterState.BlockCrouch))
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

    // ------------------------------------------------------------------ animation

    public void PlayAnim(string name, bool restart = false)
    {
        if (!restart && anim != null && anim.name == name && frameAnim == name)
            return;
        anim = def.Anim(name);
        animIndex = 0;
        animTick = 0;
        frameAnim = name;
        frameIndex = 0;
        sprite = def.Frame(name, 0);
    }

    public void AdvanceAnim()
    {
        if (anim == null || anim.ticks == null || anim.ticks.Length == 0 || frameAnim != anim.name)
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
        sprite = def.Frame(anim.name, animIndex);
    }

    public void SetFrame(string animation, int index)
    {
        frameAnim = animation;
        frameIndex = index;
        sprite = def.Frame(animation, index);
    }
}
