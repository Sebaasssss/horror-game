using UnityEngine;

public enum MatchPhase { Intro, Fight, KO, RoundOver, FinishHim, Fatality, MatchOver, Done }

// Best-of-three fight: rounds, timer, hit resolution, throws, KO / FINISH HIM / FATALITY flow.
public class Match
{
    public readonly Fighter p1, p2;
    public readonly Fx fx;
    public readonly Hud hud;
    private readonly FighterView view1, view2;
    private readonly StageView stage;
    private readonly AudioManager audio;
    private readonly RetroDisplay display;
    private readonly IInputSource input1, input2;
    private readonly Transform worldRoot;

    public MatchPhase phase = MatchPhase.Intro;
    public int phaseTick;
    public int round = 1;
    public int timer = GameConfig.RoundSeconds;
    public Fighter winner, loser;
    public bool fatality;
    private int timerTick;
    private int hitstop;
    private int slowTicks;
    private int frame;
    private float cameraX;
    private bool finishPending;

    public bool Finished => phase == MatchPhase.Done;

    public Match(Transform parent, RetroDisplay display, AudioManager audio, CharacterDef c1, CharacterDef c2,
        IInputSource input1, IInputSource input2, string stageId)
    {
        this.display = display;
        this.audio = audio;
        this.input1 = input1;
        this.input2 = input2;
        worldRoot = Nodes.Group(parent, "Match");
        stage = new StageView(worldRoot, stageId);
        fx = new Fx(worldRoot);

        p1 = new Fighter(c1, 0);
        p2 = new Fighter(c2, 1);
        p1.opponent = p2;
        p2.opponent = p1;
        view1 = new FighterView(p1, worldRoot);
        view2 = new FighterView(p2, worldRoot);
        hud = new Hud(display.uiRoot, p1, p2);

        if (input1 is CpuInput cpu1)
            cpu1.Bind(p1, p2, this);
        if (input2 is CpuInput cpu2)
            cpu2.Bind(p2, p1, this);

        StartRound();
    }

    public void Sfx(string key, float volume = 1f)
    {
        audio?.Sfx(key, volume);
    }

    // ------------------------------------------------------------------ main loop

    public void Tick()
    {
        frame++;
        InputFrame i1 = input1.Consume();
        InputFrame i2 = input2.Consume();

        if (slowTicks > 0)
        {
            slowTicks--;
            if (frame % 3 != 0)
            {
                fx.Tick();
                SyncViews();
                hud.Tick(this);
                return;
            }
        }

        bool fight = phase == MatchPhase.Fight;
        bool finish = phase == MatchPhase.FinishHim;
        p1.input = fight || (finish && winner == p1) ? i1 : default;
        p2.input = fight || (finish && winner == p2) ? i2 : default;

        if (hitstop > 0)
            hitstop--;
        else
        {
            p1.Tick(this);
            p2.Tick(this);
            p1.Physics(this);
            p2.Physics(this);
            Separate();
            Clamp();
            if (fight || finish)
                CheckHits();
        }

        TickPhase();
        fx.Tick();
        SyncViews();
        hud.Tick(this);
    }

    private void SyncViews()
    {
        float mid = (p1.pos.x + p2.pos.x) * 0.5f;
        float limit = GameConfig.StageHalfWidth - GameConfig.HalfWidth;
        cameraX += (Mathf.Clamp(mid, -limit, limit) - cameraX) * 0.18f;
        display.SetCamera(new Vector2(cameraX, GameConfig.CameraY), fx.ShakeOffset());
        stage.Sync(display.CameraCentre.x);
        bool p1Front = p1.state == FighterState.Attack || p1.state == FighterState.Throwing;
        view1.Sync(p1Front);
        view2.Sync(!p1Front && (p2.state == FighterState.Attack || p2.state == FighterState.Throwing));
    }

    // ------------------------------------------------------------------ rounds

    private void StartRound()
    {
        p1.ResetForRound(-70f, 1);
        p2.ResetForRound(70f, -1);
        fx.ClearRound();
        hud.ClearBanners();
        hud.ResetTrails();
        timer = GameConfig.RoundSeconds;
        timerTick = 0;
        winner = loser = null;
        finishPending = false;
        cameraX = 0f;
        SetPhase(MatchPhase.Intro);
    }

    private void SetPhase(MatchPhase next)
    {
        phase = next;
        phaseTick = 0;
    }

    private bool FinalRound => p1.roundWins == GameConfig.RoundsToWin - 1 && p2.roundWins == GameConfig.RoundsToWin - 1;

    private void TickPhase()
    {
        phaseTick++;
        switch (phase)
        {
            case MatchPhase.Intro:
                if (phaseTick == 15)
                {
                    hud.ShowBanner(FinalRound ? "FINAL ROUND" : "ROUND " + round, "large", 70);
                    Sfx(FinalRound ? "announcer_final_round" : "announcer_round_" + Mathf.Min(round, 5));
                }
                if (phaseTick == 88)
                {
                    hud.ShowBanner("FIGHT!", "large", 45);
                    Sfx("announcer_fight");
                }
                if (phaseTick == 92)
                {
                    p1.ReturnToNeutral();
                    p2.ReturnToNeutral();
                    SetPhase(MatchPhase.Fight);
                }
                break;

            case MatchPhase.Fight:
                if (++timerTick >= 60)
                {
                    timerTick = 0;
                    timer--;
                    if (timer <= 0)
                        TimeUp();
                }
                break;

            case MatchPhase.KO:
                TickKnockout();
                break;

            case MatchPhase.FinishHim:
                if (phaseTick > GameConfig.FinishHimTicks && phase == MatchPhase.FinishHim)
                {
                    loser.SetState(FighterState.KO);
                    loser.PlayAnim("ko", true);
                    Sfx("body_fall");
                    EndMatchNormally();
                }
                break;

            case MatchPhase.Fatality:
                if (phaseTick == 85)
                {
                    hud.ShowBanner("FATALITY", "blood", 220, 22f, 1.5f);
                    Sfx("announcer_fatality");
                }
                if (phaseTick == 150)
                    Celebrate(winner);
                if (phaseTick == 175)
                    hud.ShowBanner(winner.def.Name + " WINS", "medium", 180, -8f);
                if (phaseTick >= 380)
                    SetPhase(MatchPhase.Done);
                break;

            case MatchPhase.RoundOver:
                if (phaseTick >= 170)
                {
                    if (p1.roundWins >= GameConfig.RoundsToWin || p2.roundWins >= GameConfig.RoundsToWin || round >= 5)
                    {
                        if (winner == null)
                            winner = p1.roundWins >= p2.roundWins ? p1 : p2;
                        SetPhase(MatchPhase.Done);
                    }
                    else
                    {
                        round++;
                        StartRound();
                    }
                }
                break;

            case MatchPhase.MatchOver:
                if (phaseTick >= 220)
                    SetPhase(MatchPhase.Done);
                break;
        }
    }

    private void TimeUp()
    {
        hud.ShowBanner("TIME OVER", "large", 90);
        Sfx("announcer_time");
        if (p1.health == p2.health)
        {
            hud.ShowBanner("DRAW", "medium", 120, -12f);
            SetPhase(MatchPhase.RoundOver);
            return;
        }
        winner = p1.health > p2.health ? p1 : p2;
        loser = winner.opponent;
        loser.SetState(FighterState.KO);
        loser.PlayAnim("ko", true);
        winner.roundWins++;
        Celebrate(winner);
        hud.ShowBanner(winner.def.Name + " WINS", "medium", 140, -12f);
        SetPhase(winner.roundWins >= GameConfig.RoundsToWin ? MatchPhase.MatchOver : MatchPhase.RoundOver);
    }

    public void OnKnockout(Fighter victim, Fighter killer)
    {
        if (phase != MatchPhase.Fight)
            return;
        loser = victim;
        winner = killer;
        finishPending = killer.roundWins + 1 >= GameConfig.RoundsToWin;
        SetPhase(MatchPhase.KO);
        slowTicks = 60;
        hud.ShowBanner("K.O.", "large", 100);
        hud.Flash(Color.white, 10);
        Sfx("announcer_ko");
    }

    private void TickKnockout()
    {
        bool down = loser.state == FighterState.Lying || loser.state == FighterState.KO;
        if (!down && phaseTick < 240)
            return;

        if (finishPending)
        {
            if (phaseTick < 80)
                return;
            // The loser staggers back up for the final blow.
            loser.SetState(FighterState.Dizzy);
            loser.PlayAnim("dizzy", true);
            loser.rotation = 0f;
            loser.pos.y = 0f;
            loser.vel = Vector2.zero;
            winner.ReturnToNeutral();
            hud.ShowBanner("FINISH HIM!", "blood", 120, 14f, 1f);
            Sfx("announcer_finish_him");
            SetPhase(MatchPhase.FinishHim);
            return;
        }

        if (phaseTick >= 110)
        {
            winner.roundWins++;
            Celebrate(winner);
            hud.ShowBanner(winner.def.Name + " WINS", "medium", 140, -12f);
            if (loser.health <= 0 && winner.health == winner.MaxHealth)
                hud.ShowBanner("PERFECT", "large", 140, 22f);
            Sfx("announcer_wins_" + winner.def.Id);
            SetPhase(MatchPhase.RoundOver);
        }
    }

    private void EndMatchNormally()
    {
        winner.roundWins++;
        Celebrate(winner);
        hud.ShowBanner(winner.def.Name + " WINS", "large", 200);
        Sfx("announcer_wins_" + winner.def.Id);
        SetPhase(MatchPhase.MatchOver);
    }

    private void Celebrate(Fighter fighter)
    {
        if (fighter.state == FighterState.Throwing && fighter.throwVictim != null)
            return;
        fighter.SetState(FighterState.Victory);
        fighter.move = null;
        fighter.vel.x = 0f;
        fighter.PlayAnim("victory", true);
    }

    // ------------------------------------------------------------------ fatality

    private void Fatality(Fighter killer, Fighter victim, MoveEntry move)
    {
        fatality = true;
        SetPhase(MatchPhase.Fatality);
        killer.roundWins++;
        hitstop = 22;
        fx.Shake(1.3f);
        hud.Flash(new Color(0.8f, 0f, 0f), 18);
        Sfx("gore_rip");
        Sfx("scream_" + victim.def.Id);

        bool decapitate = move != null && (move.id == "heavy" || move.id == "special" || move.id == "air")
                          && victim.def.HasAnim("headless");
        Color blood = victim.def.bloodColor;
        GoreEntry[] parts = victim.def.data.gore;
        if (decapitate)
        {
            victim.SetState(FighterState.Dead);
            victim.PlayAnim("headless", true);
            foreach (GoreEntry part in parts)
                if (part.part == "head")
                    fx.Gore(victim, part, new Vector2(killer.facing * 2.6f, 6.5f), killer.facing * -14f);
            Vector2 neck = victim.pos + new Vector2(victim.def.data.neckX * victim.facing, victim.def.data.neckY);
            fx.Blood(neck, Vector2.up, 70, blood, 3.5f);
            Fighter body = victim;
            int age = 0;
            fx.Fountain(neck, blood, 130, () =>
            {
                age++;
                float sink = Mathf.Clamp01((age - 30) / 50f);
                return body.pos + new Vector2(body.def.data.neckX * body.facing * (1f - sink * 2f),
                    Mathf.Lerp(body.def.data.neckY, 10f, sink));
            });
        }
        else
        {
            victim.SetState(FighterState.Exploded);
            victim.hidden = true;
            Vector2 centre = victim.pos + new Vector2(0f, 46f);
            foreach (GoreEntry part in parts)
            {
                float dir = part.x * victim.facing >= 0 ? 1f : -1f;
                Vector2 vel = new Vector2(dir * Random.Range(1.2f, 3.6f) + killer.facing * 1.2f, Random.Range(4f, 8f));
                fx.Gore(victim, part, vel, Random.Range(-18f, 18f));
            }
            fx.Burst(centre, 320, blood, 4.2f);
        }
    }

    // ------------------------------------------------------------------ hits

    private void CheckHits()
    {
        if (p1.IsActive)
            TryHit(p1, p2);
        if (p2.IsActive)
            TryHit(p2, p1);
    }

    private void TryHit(Fighter attacker, Fighter defender)
    {
        MoveEntry move = attacker.move;
        Rect hitbox = attacker.Hitbox();

        if (move.IsThrow)
        {
            if (!defender.CanBeThrown)
                return;
            Rect body = defender.def.data.hurtStand.ToWorld(defender.pos, defender.facing);
            if (!hitbox.Overlaps(body))
                return;
            attacker.moveConnected = true;
            attacker.BeginThrow(defender);
            Sfx(move.sfxHit);
            return;
        }

        Rect? hurt = defender.Hurtbox();
        if (hurt == null || !hitbox.Overlaps(hurt.Value))
            return;

        attacker.moveConnected = true;
        Vector2 contact = Contact(hitbox, hurt.Value);

        if (phase == MatchPhase.FinishHim)
        {
            if (attacker == winner && defender == loser)
            {
                fx.Spark(contact, "hit");
                Fatality(attacker, defender, move);
            }
            return;
        }

        if (defender.CanBlock(move.level, attacker.Airborne))
        {
            defender.EnterBlockstun(move.blockstun);
            PushApart(attacker, defender, move.pushback * 0.35f);
            fx.Spark(contact, "block");
            fx.Shake(0.08f);
            hitstop = Mathf.Max(4, move.hitstop - 4);
            Sfx("block");
            return;
        }

        ApplyDamage(attacker, defender, move, contact);
    }

    private void ApplyDamage(Fighter attacker, Fighter defender, MoveEntry move, Vector2 contact)
    {
        float scale = defender.comboTaken <= 1 ? 1f : Mathf.Max(0.45f, 1f - 0.15f * (defender.comboTaken - 1));
        int damage = Mathf.Max(1, Mathf.RoundToInt(move.damage * scale));
        defender.health = Mathf.Max(0, defender.health - damage);
        defender.comboTaken++;
        defender.flashTicks = 6;
        defender.flashColor = Color.white;

        bool lethal = defender.health <= 0;
        if (lethal || move.knockdown || defender.Airborne)
            defender.Launch(attacker.facing * (lethal ? 3.2f : 2.4f), lethal ? 6.2f : 5f);
        else
            defender.EnterHitstun(move.hitstun, move.level);
        PushApart(attacker, defender, move.pushback * 0.3f);

        bool heavy = move.damage >= 100;
        fx.Spark(contact, "hit");
        fx.Blood(contact, new Vector2(attacker.facing, 0.5f), move.blood + (lethal ? 30 : 0), defender.def.bloodColor,
            heavy ? 3.4f : 2.4f);
        fx.Shake(move.shake + (lethal ? 0.9f : 0f));
        hitstop = move.hitstop + (lethal ? 14 : 0);
        Sfx(move.sfxHit);
        if (heavy || lethal || Random.value < 0.35f)
            Sfx("pain_" + defender.def.Id, 0.8f);
        hud.Combo(attacker, defender.comboTaken);

        if (lethal)
            OnKnockout(defender, attacker);
    }

    private void PushApart(Fighter attacker, Fighter defender, float amount)
    {
        float wall = GameConfig.StageHalfWidth - GameConfig.WallMargin - 1f;
        if (Mathf.Abs(defender.pos.x) >= wall && Mathf.Sign(defender.pos.x) == attacker.facing)
            attacker.pushVel = -attacker.facing * amount;
        else
            defender.pushVel = attacker.facing * amount;
    }

    private static Vector2 Contact(Rect a, Rect b)
    {
        float xMin = Mathf.Max(a.xMin, b.xMin), xMax = Mathf.Min(a.xMax, b.xMax);
        float yMin = Mathf.Max(a.yMin, b.yMin), yMax = Mathf.Min(a.yMax, b.yMax);
        return new Vector2((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f);
    }

    // ------------------------------------------------------------------ throws

    public void PlaceThrowVictim(Fighter thrower, Fighter victim, int tick)
    {
        Vector2 anchor = thrower.def.HandAnchor(thrower.FrameAnim, thrower.FrameIndex);
        Vector2 hand = thrower.pos + new Vector2(anchor.x * thrower.facing, anchor.y);
        float lift = tick < 10 ? 0f : (tick - 10) / 14f;
        victim.rotation = -victim.facing * 100f * lift;
        victim.SetFrame(lift > 0f ? "knockdown" : "hurt", 0);
        Vector2 grip = Quaternion.Euler(0f, 0f, victim.rotation) * new Vector2(0f, 48f);
        victim.pos = hand - grip;
        victim.pos.y = Mathf.Max(0f, victim.pos.y);
        if (tick == 10)
            Sfx("grunt_" + thrower.def.Id, 0.8f);
    }

    public void ReleaseThrow(Fighter thrower, Fighter victim)
    {
        victim.rotation = 0f;
        if (phase == MatchPhase.FinishHim && victim == loser)
        {
            Fatality(thrower, victim, thrower.move);
            return;
        }
        MoveEntry move = thrower.move;
        victim.health = Mathf.Max(0, victim.health - (move != null ? move.damage : 120));
        victim.Launch(thrower.facing * 3.4f, 5.6f);
        victim.flashTicks = 6;
        fx.Shake(0.6f);
        fx.Blood(victim.pos + new Vector2(0f, 40f), new Vector2(thrower.facing, 1f), 10, victim.def.bloodColor);
        hitstop = 6;
        Sfx("throw");
        Sfx("pain_" + victim.def.Id, 0.8f);
        if (victim.health <= 0)
            OnKnockout(victim, thrower);
    }

    public void OnFighterLanded(Fighter fighter)
    {
        Sfx("body_fall");
        fx.Shake(0.25f);
        fx.Blood(fighter.pos + new Vector2(0f, 6f), Vector2.up, 4, fighter.def.bloodColor, 1.4f);
    }

    // ------------------------------------------------------------------ spacing

    private void Separate()
    {
        if (p1.state == FighterState.Thrown || p2.state == FighterState.Thrown || p1.hidden || p2.hidden)
            return;
        float dx = p2.pos.x - p1.pos.x;
        float minimum = (p1.def.Stats.pushWidth + p2.def.Stats.pushWidth) * 0.5f;
        if (Mathf.Abs(dx) >= minimum || Mathf.Abs(p1.pos.y - p2.pos.y) > 70f)
            return;
        float dir = Mathf.Abs(dx) < 0.01f ? -p1.facing : Mathf.Sign(dx);
        float overlap = minimum - Mathf.Abs(dx);
        p1.pos.x -= dir * overlap * 0.5f;
        p2.pos.x += dir * overlap * 0.5f;
    }

    private void Clamp()
    {
        float wall = GameConfig.StageHalfWidth - GameConfig.WallMargin;
        float mid = (p1.pos.x + p2.pos.x) * 0.5f;
        float half = GameConfig.MaxFighterSeparation * 0.5f;
        foreach (Fighter f in new[] { p1, p2 })
        {
            f.pos.x = Mathf.Clamp(f.pos.x, mid - half, mid + half);
            f.pos.x = Mathf.Clamp(f.pos.x, -wall, wall);
        }
    }

    public void Dispose()
    {
        Nodes.Destroy(worldRoot.gameObject);
        hud.Destroy();
    }
}
