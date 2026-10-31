// Scripted touch input for automated runs: -cf-script file.txt
// Lines: "<frame> down <x> <y> [touch]", "<frame> move <x> <y> [touch]", "<frame> up <x> <y> [touch]",
//        "<frame> tap <x> <y>" (down now, up 3 frames later), "<frame> shot <path>", "<frame> quit".
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace CastleFrenzy
{
    public static class AutoInput
    {
        struct Cmd { public int Frame; public string Op; public int X, Y, Touch; public string Arg; }

        static readonly List<Cmd> s_cmds = new List<Cmd>();
        static int s_next;
        public static bool Active { get; private set; }

        /// <summary>Deterministic automation without a script (used by the -cf-autoplay bot).</summary>
        public static void Activate() { Active = true; }

        public static void Load(string path)
        {
            if (!File.Exists(path)) { Debug.LogError("Missing input script " + path); return; }
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                string[] p = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 2) continue;
                Cmd c = new Cmd { Frame = int.Parse(p[0], CultureInfo.InvariantCulture), Op = p[1] };
                if (c.Op == "shot") c.Arg = p.Length > 2 ? p[2] : "shot.png";
                else if (p.Length >= 4)
                {
                    c.X = int.Parse(p[2], CultureInfo.InvariantCulture);
                    c.Y = int.Parse(p[3], CultureInfo.InvariantCulture);
                    c.Touch = p.Length > 4 ? int.Parse(p[4], CultureInfo.InvariantCulture) : 0;
                }
                if (c.Op == "tap")
                {
                    s_cmds.Add(new Cmd { Frame = c.Frame, Op = "down", X = c.X, Y = c.Y });
                    s_cmds.Add(new Cmd { Frame = c.Frame + 3, Op = "up", X = c.X, Y = c.Y });
                    continue;
                }
                s_cmds.Add(c);
            }
            s_cmds.Sort((a, b) => a.Frame.CompareTo(b.Frame));
            Active = true;
        }

        public static void Step(int frame)
        {
            while (s_next < s_cmds.Count && s_cmds[s_next].Frame <= frame)
            {
                Cmd c = s_cmds[s_next++];
                switch (c.Op)
                {
                    case "down": CTouchPad.EventTouchDown(c.Touch, c.X, c.Y); break;
                    case "move": CTouchPad.EventTouchMove(c.Touch, c.X, c.Y); break;
                    case "up": CTouchPad.EventTouchUp(c.Touch, c.X, c.Y); break;
                    case "shot": ScreenCapture.CaptureScreenshot(c.Arg); break;
                    case "quit": GameMain.Quit(); break;
                }
            }
            if (AutoPlay.Active) AutoPlay.Update();
        }
    }
}
