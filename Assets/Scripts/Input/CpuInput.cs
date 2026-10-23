using UnityEngine;

// Arcade CPU opponent. Decides every few ticks (reaction time), holds directions between decisions and taps buttons.
public class CpuInput : IInputSource
{
    private Fighter self, foe;
    private Match match;
    private readonly float skill;
    private readonly System.Random random;
    private int wait;
    private int holdX;
    private bool holdDown;
    private int holdTicks;
    private InputFrame tap;

    public CpuInput(float skill = 0.6f, int seed = 7)
    {
        this.skill = Mathf.Clamp01(skill);
        random = new System.Random(seed);
    }

    public void Bind(Fighter self, Fighter foe, Match match)
    {
        this.self = self;
        this.foe = foe;
        this.match = match;
    }

    public void Sample()
    {
    }

    private float Roll() => (float)random.NextDouble();

    public InputFrame Consume()
    {
        InputFrame frame = default;
        if (self == null || foe == null)
            return frame;

        if (--wait <= 0)
            Decide();
        if (holdTicks-- <= 0)
        {
            holdX = 0;
            holdDown = false;
        }

        frame = tap;
        frame.x = holdX;
        frame.down = holdDown;
        tap = default;
        return frame;
    }

    private void Decide()
    {
        float dx = foe.pos.x - self.pos.x;
        float distance = Mathf.Abs(dx);
        int toward = dx >= 0f ? 1 : -1;
        int reaction = Mathf.RoundToInt(Mathf.Lerp(16f, 5f, skill));
        wait = reaction + random.Next(0, 6);

        if (match != null && match.phase == MatchPhase.FinishHim)
        {
            if (distance > 46f)
                Hold(toward, false, wait);
            else
            {
                // Pick a fatality style: heavy decapitates, kick cuts to pieces, light blows the body apart.
                float pick = Roll();
                tap.heavy = pick < 0.34f;
                tap.kick = pick >= 0.34f && pick < 0.67f;
                tap.light = pick >= 0.67f;
            }
            return;
        }

        // Continue a connected attack into its chain follow-up.
        if (self.state == FighterState.Attack && self.moveConnected && self.move != null && self.move.chains != null
            && self.move.chains.Length > 0 && Roll() < 0.35f + skill * 0.5f)
        {
            string next = self.move.chains[random.Next(self.move.chains.Length)].input;
            tap.light = next == "light";
            tap.heavy = next == "heavy";
            tap.kick = next == "kick";
            wait = 3;
            return;
        }

        if (self.Airborne)
        {
            if (self.state == FighterState.Jump && self.vel.y < 1f && distance < 70f)
                tap.kick = true;
            return;
        }

        // Guard against an incoming attack.
        if (foe.IsThreat && distance < 110f && Roll() < 0.25f + skill * 0.55f)
        {
            bool low = foe.move != null && foe.move.level == "low";
            Hold(-toward, low, 14);
            wait = 6;
            return;
        }

        // Punish a whiffed attack or a falling opponent getting up.
        if (foe.state == FighterState.Attack && foe.moveTick > foe.move.startup + foe.move.active && distance < 70f)
        {
            tap.heavy = Roll() < 0.5f;
            tap.kick = !tap.heavy;
            return;
        }

        float r = Roll();
        if (distance > 120f)
        {
            if (r < 0.12f)
            {
                tap.heavy = true;
                Hold(toward, false, 3);
            }
            else if (r < 0.22f)
                JumpAt(toward);
            else
                Hold(toward, false, wait + 4);
        }
        else if (distance < 36f && r < 0.22f)
        {
            tap.grab = true;
        }
        else if (distance < 64f)
        {
            if (r < 0.28f)
                tap.light = true;
            else if (r < 0.40f)
                tap.kick = true;
            else if (r < 0.46f)
            {
                tap.kick = true;
                Hold(-toward, false, 2);
            }
            else if (r < 0.58f)
                tap.heavy = true;
            else if (r < 0.72f)
            {
                Hold(0, true, 8);
                tap.light = true;
            }
            else if (r < 0.84f)
                Hold(-toward, false, 12);
            else
                JumpAt(toward);
        }
        else
        {
            if (r < 0.55f)
                Hold(toward, false, wait + 6);
            else if (r < 0.7f)
                JumpAt(toward);
            else if (r < 0.8f)
            {
                tap.heavy = true;
                Hold(toward, false, 2);
            }
            else
                Hold(-toward, false, 10);
        }
    }

    private void Hold(int x, bool down, int ticks)
    {
        holdX = x;
        holdDown = down;
        holdTicks = ticks;
    }

    private void JumpAt(int toward)
    {
        tap.up = true;
        Hold(toward, false, 2);
    }
}
