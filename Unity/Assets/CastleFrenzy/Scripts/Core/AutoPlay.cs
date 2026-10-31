// Test bot for automated runs (-cf-autoplay). It plays through the real touch input, like a player: it grabs the
// unit closest to the gate, lifts it high and flings it, or smashes the units the in-game help says must be smashed
// (warlocks, dark elves, skeletons, mind-controlled boars) into the ground. Days are therefore won only through the
// game's own drag/throw/damage rules.
using System.Collections.Generic;

namespace CastleFrenzy
{
    public static class AutoPlay
    {
        public static bool Active;

        struct Step { public int X, Y; public bool Up; }

        static readonly Queue<Step> s_steps = new Queue<Step>();
        static bool s_down;
        static int s_cooldown;
        /// <summary>Units the bot has grabbed so far (reported in the -cf-quit-on-result line).</summary>
        public static int Grabs;

        public static void Update()
        {
            CPlayfield pf = CPlayfield.GetInstance();
            int state = pf.IsLevelLoaded() ? pf.GetPlayState() : CPlayfield.PS_None;
            if (state == CPlayfield.PS_IntroUnitCamera && s_steps.Count == 0 && s_cooldown == 0)
            {
                // new enemy tutorial: tap its back arrow
                for (int i = 0; i < 3; i++) s_steps.Enqueue(new Step { X = 70, Y = 65 });
                s_steps.Enqueue(new Step { X = 70, Y = 65, Up = true });
                s_cooldown = 30;
            }
            else if (state != CPlayfield.PS_Play && state != CPlayfield.PS_IntroUnitCamera)
            {
                Release(240, 160);
                s_steps.Clear();
                return;
            }
            if (s_steps.Count > 0)
            {
                Step s = s_steps.Dequeue();
                if (s.Up) Release(s.X, s.Y);
                else if (!s_down) { CTouchPad.EventTouchDown(0, s.X, s.Y); s_down = true; }
                else CTouchPad.EventTouchMove(0, s.X, s.Y);
                return;
            }
            if (s_cooldown > 0) { s_cooldown--; return; }
            if (state != CPlayfield.PS_Play) return;

            CUnit target = null;
            float best = float.MaxValue;
            v3f gate = pf.getGate().getAbsolutePosition();
            foreach (CUnit u in pf.getUnits())
            {
                if (!u.isDragable() || !IsTarget(u.getState())) continue;
                p2i sp = u.getScreenPosition();
                if (sp.X < 10 || sp.X > 470 || sp.Y < 30 || sp.Y > 310) continue;
                // nearest first, but units hurting the castle (attacking, climbing elves, bomblins) take priority
                float d = (u.getAbsolutePosition() - gate).getLength();
                if (u.isMindControllingShaman()) d -= 2000.0f; // freeing the slaves needs the warlock dead
                else if (u.getState() == GE.US_Attacking) d -= 1000.0f;
                else if (u.getState() == GE.US_ElfWallGrab) d -= 800.0f;
                else if (u.getUnitKind() == GE.UK_Bomblin) d -= 500.0f;
                if (d < best) { best = d; target = u; }
            }
            if (target == null) return;

            Grabs++;
            p2i p = target.getScreenPosition();
            int x = p.X - 10, y = p.Y + 10;
            s_steps.Enqueue(new Step { X = x, Y = y });
            // the in-game help: warlocks, dark elves and skeletons must be smashed into the ground
            int kind = target.getUnitKind();
            bool smash = kind == GE.UK_Shaman || kind == GE.UK_Elf || kind == GE.UK_Skeleton || target.isMindControlled();
            if (smash)
            {
                // lift well above the ground (elves and skeletons only take smash damage from more than 3 units up),
                // then slam down within a few frames; dark elves wriggle free after 0.8 s, so stay quick
                int top = 30;
                for (int i = 1; i <= 8; i++) s_steps.Enqueue(new Step { X = x, Y = y + (top - y) * i / 8 });
                s_steps.Enqueue(new Step { X = x, Y = 170 });
                s_steps.Enqueue(new Step { X = x, Y = 318 });
                s_steps.Enqueue(new Step { X = x, Y = 318, Up = true });
            }
            else
            {
                // lift, then fling upward and let go while moving: the extra height makes the fall lethal
                int top = 70;
                for (int i = 1; i <= 8; i++) s_steps.Enqueue(new Step { X = x, Y = y + (top - y) * i / 8 });
                for (int i = 1; i <= 3; i++) s_steps.Enqueue(new Step { X = x, Y = top - (top - 2) * i / 3 });
                s_steps.Enqueue(new Step { X = x, Y = 0, Up = true });
            }
            s_cooldown = 4;
        }

        static bool IsTarget(int state)
        {
            switch (state)
            {
                case GE.US_Walking: case GE.US_Attacking: case GE.US_Stunned: case GE.US_Riding: case GE.US_MindControlling:
                case GE.US_RunningAway: case GE.US_ElfWallGrab: case GE.US_SkeletonLand:
                    return true;
            }
            return false;
        }

        static void Release(int x, int y)
        {
            if (!s_down) return;
            CTouchPad.EventTouchUp(0, x, y);
            s_down = false;
        }
    }
}
