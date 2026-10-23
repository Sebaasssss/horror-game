using UnityEngine;

public enum MatchPhase { Intro, Fight, KO, RoundOver, FinishHim, Fatality, MatchOver, Done }

public enum FatalityKind { Decapitation, Pieces, Explosion }

// Best-of-three fight: rounds, timer, hit resolution, dismemberment, throws, KO / FINISH HIM / FATALITY flow.
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
    private readonly System.Random random = new System.Random(4242);

    public MatchPhase phase = MatchPhase.Intro;
    public int phaseTick;
    public int round = 1;
    public int timer = GameConfig.RoundSeconds;
    public Fighter winner, loser;
    public bool fatality;
    public FatalityKind fatalityKind;
    public int dismemberments;
    private int timerTick;
    private int hitstop;
    private int slowTicks;
    private int frame;
    private float cameraX;

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

    private float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

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
            if (fight)
            {
                Bleed(p1);
                Bleed(p2);
            }
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

    // Mutilated fighters slowly bleed out (never to death on their own).
    private void Bleed(Fighter fighter)
    {
        if (fighter.limbsLost == 0 || frame % 24 != 0 || fighter.health <= 1)
            return;
        fighter.health = Mathf.Max(1, fighter.health - fighter.limbsLost);
    }

    // ------------------------------------------------------------------ rounds

    private void StartRound()
    {
        p1.ResetForRound(-70f, 1);
        p2.ResetForRound(70f, -1);
        fx.ClearRound();
        hud.ClearBanners();
        hud.ClearSplatter();
        hud.ResetTrails();
        timer = GameConfig.RoundSeconds;
        timerTick = 0;
        winner = loser = null;
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
                if (loser.state != FighterState.Dizzy && loser.state != FighterState.Thrown)
                {
                    loser.SetState(FighterState.Dizzy);
                    loser.PlayAnim("dizzy", true);
                }
                if (phaseTick > GameConfig.FinishHimTicks)
                {
                    loser.SetState(FighterState.KO);
                    loser.PlayAnim("ko", true);
                    fx.Pool(loser.pos.x, 60f, loser.def.bloodColor);
                    Sfx("body_fall");
                    EndMatchNormally();
                }
                break;

            case MatchPhase.Fatality:
                if (phaseTick == 85)
                {
                    hud.ShowBanner("FATALITY", "blood", 220, 22f, 1.5f);
                    Sfx("announcer_fatality");
                    Sfx("fatality_sting");
                }
                if (phaseTick == 150)
                    Celebrate(winner);
                if (phaseTick == 175)
                    hud.ShowBanner(winner.def.Name + " WINS", "medium", 180, -8f);
                if (phaseTick == 180)
                    Sfx("announcer_wins_" + winner.def.Id);
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
        if (winner.roundWins + 1 >= GameConfig.RoundsToWin)
        {
            StartFinishHim();
            return;
        }
        loser.SetState(FighterState.KO);
        loser.PlayAnim("ko", true);
        winner.roundWins++;
        Celebrate(winner);
        hud.ShowBanner(winner.def.Name + " WINS", "medium", 140, -12f);
        SetPhase(MatchPhase.RoundOver);
    }

    public void OnKnockout(Fighter victim, Fighter killer)
    {
        if (phase != MatchPhase.Fight)
            return;
        loser = victim;
        winner = killer;
        // Winning the match goes straight to FINISH HIM: no knockdown, no waiting.
        if (killer.roundWins + 1 >= GameConfig.RoundsToWin)
        {
            StartFinishHim();
            return;
        }
        SetPhase(MatchPhase.KO);
        slowTicks = 60;
        hud.ShowBanner("K.O.", "large", 100);
        hud.Flash(Color.white, 10);
        Sfx("announcer_ko");
    }

    private void StartFinishHim()
    {
        loser.health = 0;
        loser.rotation = 0f;
        loser.vel = Vector2.zero;
        loser.pushVel = 0f;
        loser.pos.y = 0f;
        loser.move = null;
        loser.SetState(FighterState.Dizzy);
        loser.PlayAnim("dizzy", true);
        if (winner.state == FighterState.Throwing || winner.state == FighterState.Victory)
            winner.ReturnToNeutral();
        hitstop = 18;
        fx.Shake(0.6f);
        hud.Flash(new Color(0.7f, 0f, 0f), 12);
        hud.ShowBanner("FINISH HIM!", "blood", 140, 14f, 1f);
        Sfx("announcer_finish_him");
        SetPhase(MatchPhase.FinishHim);
    }

    private void TickKnockout()
    {
        bool down = loser.state == FighterState.Lying || loser.state == FighterState.KO;
        if (!down && phaseTick < 240)
            return;
        if (phaseTick == 1 || (down && phaseTick % 200 == 0))
            fx.Pool(loser.pos.x, 46f, loser.def.bloodColor);

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

    public static FatalityKind KindFor(MoveEntry move)
    {
        string id = move != null ? move.id : "";
        if (id == "heavy" || id == "special" || id == "slash_side" || id == "air" || id == "uppercut")
            return FatalityKind.Decapitation;
        if (id == "kick" || id == "roundhouse" || id == "sweep" || id == "air_kick" || id == "crouch_light")
            return FatalityKind.Pieces;
        return FatalityKind.Explosion;
    }

    private void Fatality(Fighter killer, Fighter victim, MoveEntry move)
    {
        fatality = true;
        fatalityKind = KindFor(move);
        hud.ClearBanners();
        SetPhase(MatchPhase.Fatality);
        killer.roundWins++;
        hitstop = 24;
        fx.Shake(1.5f);
        hud.Flash(new Color(0.8f, 0f, 0f), 18);
        hud.Splatter(26, victim.def.bloodColor);
        Sfx("gore_rip");
        Sfx("fatality_gore");

        Color blood = victim.def.bloodColor;
        float away = killer.facing;
        if (fatalityKind == FatalityKind.Decapitation && victim.def.HasAnim("headless"))
        {
            victim.SetState(FighterState.Dead);
            victim.PlayAnim("headless", true);
            GoreEntry head = victim.def.Gore("head");
            if (head != null)
                fx.Gore(victim, head, new Vector2(away * 2.8f, 7f), away * -16f);
            Vector2 neck = victim.NeckPoint;
            fx.Blood(neck, Vector2.up, 160, blood, 4f);
            fx.Gibs(neck, 6, away, blood);
            Fighter body = victim;
            int age = 0;
            fx.Fountain(neck, blood, 170, () =>
            {
                age++;
                float sink = Mathf.Clamp01((age - 30) / 60f);
                return body.pos + new Vector2(body.def.data.neckX * body.facing * (1f - sink * 2f),
                    Mathf.Lerp(body.def.data.neckY, 10f, sink));
            });
            fx.Pool(victim.pos.x, 90f, blood);
            return;
        }

        Vector2 centre = victim.pos + new Vector2(0f, 46f);
        GoreEntry[] parts = victim.def.data.gore ?? new GoreEntry[0];
        if (fatalityKind == FatalityKind.Pieces)
        {
            // Cut into chunks: the big parts fly, guts spill, blood everywhere.
            if (victim.def.HasAnim("bisected"))
            {
                victim.SetState(FighterState.Dead);
                victim.PlayAnim("bisected", true);
                foreach (GoreEntry part in parts)
                    if (part.part == "guts" || part.part == "spine" || part.part == "arm_back")
                        fx.Gore(victim, part, new Vector2(away * Range(1f, 3f), Range(3f, 6f)), Range(-14f, 14f));
            }
            else
            {
                victim.SetState(FighterState.Exploded);
                victim.hidden = true;
                foreach (GoreEntry part in parts)
                    fx.Gore(victim, part, new Vector2(away * Range(0.4f, 2.6f) + Range(-1f, 1f), Range(3f, 6.5f)),
                        Range(-12f, 12f));
            }
            fx.Burst(centre, 420, blood, 3.6f);
            fx.Gibs(centre, 14, away, blood);
            fx.Pool(victim.pos.x, 110f, blood);
            return;
        }

        // A thousand pieces: every part, a storm of gibs and a wall of blood.
        victim.SetState(FighterState.Exploded);
        victim.hidden = true;
        foreach (GoreEntry part in parts)
        {
            float dir = part.x * victim.facing >= 0 ? 1f : -1f;
            fx.Gore(victim, part, new Vector2(dir * Range(1.5f, 4.5f) + away, Range(5f, 10f)), Range(-24f, 24f));
        }
        fx.Gibs(centre, 46, 0f, blood);
        fx.Burst(centre, 900, blood, 5.2f);
        fx.Pool(victim.pos.x, 140f, blood);
        hud.Splatter(30, blood);
        Sfx("explosion_gore");
    }

    // ------------------------------------------------------------------ dismemberment

    // Limbs come off as health drops: the first one below 65% health, the second below 35%. Heavy hits and combos
    // make it near certain once the threshold is crossed; otherwise there is still a small random chance.
    private void TryDismember(Fighter attacker, Fighter defender, MoveEntry move, int damage, bool counter)
    {
        if (defender.limbsLost >= 2 || defender.health <= 0 || phase != MatchPhase.Fight)
            return;
        float ratio = defender.health / (float)defender.MaxHealth;
        int allowed = ratio < 0.35f ? 2 : ratio < 0.65f ? 1 : 0;
        float chance;
        if (defender.limbsLost < allowed)
        {
            chance = 0.35f + move.dismember;
            if (damage >= 90 || defender.comboTaken >= 3 || counter || attacker.chainCount >= 2)
                chance += 0.45f;
        }
        else
            chance = defender.limbsLost == 0 && defender.comboTaken >= 4 ? 0.08f + move.dismember * 0.5f : 0f;
        if (Range(0f, 1f) >= Mathf.Min(0.95f, chance))
            return;

        string current = defender.kit.Name;
        string next;
        if (current == "full")
        {
            bool armFirst = random.NextDouble() < 0.5;
            next = armFirst ? "one_arm" : "one_leg";
            if (defender.def.Variant(next) == null)
                next = armFirst ? "one_leg" : "one_arm";
        }
        else
            next = current == "one_arm" ? "no_arms" : current == "one_leg" ? "no_legs" : null;
        VariantDef variant = defender.def.Variant(next);
        if (variant == null)
            return;

        string part = next == "one_arm" ? "arm_back" : next == "no_arms" ? "arm_front"
            : next == "one_leg" ? "leg_back" : "leg_front";
        defender.SetVariant(variant);
        dismemberments++;
        GoreEntry limb = defender.def.Gore(part);
        Color blood = defender.def.bloodColor;
        if (limb != null)
            fx.Gore(defender, limb, new Vector2(attacker.facing * Range(2f, 3.5f), Range(4.5f, 7f)), Range(-20f, 20f));
        Vector2 stump = defender.StumpPoint;
        fx.Blood(stump, new Vector2(attacker.facing, 0.8f), 140, blood, 4f);
        fx.Gibs(stump, 5, attacker.facing, blood);
        Fighter victim = defender;
        VariantDef state = variant;
        fx.Fountain(stump, blood, 60, () => victim.StumpPoint);
        fx.Drip(() => victim.kit == state && victim.health > 0 ? victim.StumpPoint : (Vector2?)null, blood, 3);
        hud.Splatter(12, blood);
        hud.ShowBanner("DISMEMBERED!", "blood", 70, 30f, 1f);
        hud.Flash(new Color(0.7f, 0f, 0f), 8);
        hitstop = 20;
        fx.Shake(1f);
        Sfx("gore_rip");
        Sfx("dismember");
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
            if (!defender.CanBeThrown || !hitbox.Overlaps(defender.StandBox))
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
        bool counter = defender.InStartup;
        float scale = defender.comboTaken <= 1 ? 1f : Mathf.Max(0.45f, 1f - 0.12f * (defender.comboTaken - 1));
        float variance = Range(0.88f, 1.12f);
        bool critical = Range(0f, 1f) < 0.06f;
        float total = move.damage * scale * variance * attacker.kit.data.damageScale * (counter ? 1.3f : 1f)
                      * (critical ? 1.5f : 1f) * (defender.Mutilated ? 1.1f : 1f);
        int damage = Mathf.Max(1, Mathf.RoundToInt(total));
        defender.health = Mathf.Max(0, defender.health - damage);
        defender.comboTaken++;
        defender.flashTicks = 6;
        defender.flashColor = critical ? new Color(1f, 0.3f, 0.3f) : Color.white;

        bool lethal = defender.health <= 0;
        bool finishesMatch = lethal && attacker.roundWins + 1 >= GameConfig.RoundsToWin;
        if (!finishesMatch)
        {
            if (lethal || move.knockdown || move.launcher || defender.Airborne)
                defender.Launch(attacker.facing * (lethal ? 3.2f : 2.4f), lethal ? 6.2f : move.launcher ? 7f : 5f);
            else
                defender.EnterHitstun(move.hitstun, move.level);
            PushApart(attacker, defender, move.pushback * 0.3f);
        }

        bool heavy = move.damage >= 100 || critical;
        Color blood = defender.def.bloodColor;
        fx.Spark(contact, "hit");
        fx.Spark(contact, "blood");
        int drops = move.blood * 3 + (heavy ? 40 : 10) + (lethal ? 80 : 0) + defender.comboTaken * 6;
        fx.Blood(contact, new Vector2(attacker.facing, 0.6f), drops, blood, heavy ? 3.8f : 2.8f);
        if (heavy)
        {
            fx.Gibs(contact, critical ? 4 : 2, attacker.facing, blood);
            hud.Splatter(critical ? 8 : 3, blood);
        }
        fx.Shake(move.shake + (lethal ? 0.9f : 0f) + (critical ? 0.4f : 0f));
        hitstop = move.hitstop + (lethal ? 14 : 0) + (critical ? 6 : 0);
        Sfx(move.sfxHit);
        Sfx("blood_splat", 0.6f);
        hud.Combo(attacker, defender.comboTaken);
        if (critical)
            hud.Callout(attacker, "CRITICAL!");
        else if (counter)
            hud.Callout(attacker, "COUNTER!");

        if (lethal)
            OnKnockout(defender, attacker);
        else
            TryDismember(attacker, defender, move, damage, counter);
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
        Vector2 anchor = thrower.HandAnchor();
        Vector2 hand = thrower.pos + new Vector2(anchor.x * thrower.facing, anchor.y);
        float lift = tick < 6 ? 0f : Mathf.Clamp01((tick - 6) / 18f);
        if (victim.kit.HasAnim("thrown"))
        {
            // Drawn "thrown" frames already show the pose; only position the body under the hand.
            victim.rotation = 0f;
            victim.SetFrame("thrown", lift <= 0f ? 0 : lift < 0.6f ? 1 : 2);
            victim.pos = hand - new Vector2(0f, lift < 0.6f ? 56f : 40f);
        }
        else
        {
            victim.rotation = -victim.facing * 100f * lift;
            victim.SetFrame(lift > 0f ? "knockdown" : "hurt", 0);
            Vector2 grip = Quaternion.Euler(0f, 0f, victim.rotation) * new Vector2(0f, 48f);
            victim.pos = hand - grip;
        }
        victim.pos.y = Mathf.Max(0f, victim.pos.y);
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
        int damage = Mathf.RoundToInt((move != null ? move.damage : 120) * Range(0.9f, 1.1f));
        victim.health = Mathf.Max(0, victim.health - damage);
        bool finishesMatch = victim.health <= 0 && thrower.roundWins + 1 >= GameConfig.RoundsToWin;
        if (!finishesMatch)
            victim.Launch(thrower.facing * 3.4f, 5.6f);
        victim.flashTicks = 6;
        fx.Shake(0.7f);
        fx.Blood(victim.pos + new Vector2(0f, 40f), new Vector2(thrower.facing, 1f), 40, victim.def.bloodColor, 3f);
        hitstop = 6;
        Sfx("throw");
        if (victim.health <= 0)
            OnKnockout(victim, thrower);
    }

    public void OnHopLanded(Fighter fighter)
    {
        Sfx("land", 0.45f);
        fx.Blood(fighter.StumpPoint, Vector2.down, 6, fighter.def.bloodColor, 1.2f);
    }

    public void OnFighterLanded(Fighter fighter)
    {
        Sfx("body_fall");
        fx.Shake(0.25f);
        fx.Blood(fighter.pos + new Vector2(0f, 6f), Vector2.up, 14, fighter.def.bloodColor, 1.6f);
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
