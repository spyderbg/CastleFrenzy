// Direct level launch (editor "Play day", automation): -cf-level N -cf-day D -cf-difficulty 0..2 -cf-endless -cf-skip-cutscene
// plus the game's cheat switches -cf-all-weapons and -cf-no-cooldown.
// In the editor the Level Editor writes the same options to Temp/cf_launch.txt before entering Play mode.
using System;
using System.IO;
using UnityEngine;

namespace CastleFrenzy
{
    public static class LaunchOptions
    {
        public static int Level = -1, Day, Difficulty = 1;
        public static bool Endless, SkipCutscene, AutoContinue;
        /// <summary>Test hook: kills every walking unit (reaches the end-of-day screens quickly).</summary>
        public static bool AutoKill;
        /// <summary>Test hook: spell equipped at level start (-cf-spell boulder|lightning|ghost|tornado), -2 = level default.</summary>
        public static int Spell = -2;
        /// <summary>Test hook: log the day's result ("[CastleFrenzy] Result ...") and quit (-cf-quit-on-result).</summary>
        public static bool QuitOnResult;
        /// <summary>Test hook: run frames as fast as possible instead of 60 per second (-cf-fast).</summary>
        public static bool Fast;
        static int s_resultFrames = -1;

        public static bool Active { get { return Level >= 0 || Endless; } }

        public static string EditorRequestPath { get { return Path.Combine(Application.dataPath, "../Temp/cf_launch.txt"); } }

        public static void Parse(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i], next = i + 1 < args.Length ? args[i + 1] : null;
                if (a == "-cf-level" && next != null) { int.TryParse(next, out Level); i++; }
                else if (a == "-cf-day" && next != null) { int.TryParse(next, out Day); i++; }
                else if (a == "-cf-difficulty" && next != null) { int.TryParse(next, out Difficulty); i++; }
                else if (a == "-cf-endless") Endless = true;
                else if (a == "-cf-skip-cutscene") SkipCutscene = true;
                else if (a == "-cf-all-weapons") CGameConfig.AllWeaponsSold = true;
                else if (a == "-cf-no-cooldown") CGameConfig.NoWeaponCD = true;
                else if (a == "-cf-autokill") AutoKill = true;
                else if (a == "-cf-quit-on-result") QuitOnResult = true;
                else if (a == "-cf-autoplay") { AutoPlay.Active = true; AutoInput.Activate(); }
                else if (a == "-cf-fast") Fast = true;
                else if (a == "-cf-spell" && next != null)
                {
                    switch (next) { case "boulder": Spell = GE.ST_Boulder; break; case "lightning": Spell = GE.ST_Lightning; break; case "ghost": Spell = GE.ST_GhostArmy; break; case "tornado": Spell = GE.ST_Tornado; break; }
                    i++;
                }
                else if (a == "-cf-timescale" && next != null) { float ts; if (float.TryParse(next, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out ts)) GameMain.TestTimeScale = ts; i++; }
            }
            if (Active) AutoContinue = true;
        }

        public static void LoadEditorRequest()
        {
#if UNITY_EDITOR
            string path = EditorRequestPath;
            if (!File.Exists(path)) return;
            string[] args = File.ReadAllText(path).Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            File.Delete(path);
            Parse(args);
#endif
        }

        public static void ApplyTestHooks()
        {
            if (!AutoKill && !QuitOnResult) return;
            CPlayfield pf = CPlayfield.GetInstance();
            if (QuitOnResult && pf.IsLevelLoaded())
            {
                if (s_resultFrames < 0 && pf.IsMissionOver())
                {
                    Debug.Log("[CastleFrenzy] Result level " + pf.m_CurrentLevel + " day " + pf.m_CurrentDay + ": " +
                              (pf.IsMissionSuccess() ? "victory" : "defeat") + " gate " + pf.getGate().getHealth() +
                              (AutoPlay.Active ? " grabs " + AutoPlay.Grabs + " kills " + pf.m_kills : ""));
                    s_resultFrames = 30; // let the end-of-day menu open and draw before quitting
                }
                else if (s_resultFrames > 0 && --s_resultFrames == 0) GameMain.Quit();
            }
            if (!AutoKill || !pf.IsLevelLoaded() || pf.GetPlayState() != CPlayfield.PS_Play) return;
            foreach (CUnit u in new System.Collections.Generic.List<CUnit>(pf.getUnits()))
                if (u.getState() == GE.US_Walking || u.getState() == GE.US_Attacking) u.damageUnit(9999, GE.UDS_GroundDrag);
        }

        /// <summary>Called by GS_Splash once initialization is done: starts the requested level/day.</summary>
        public static bool TryStart()
        {
            if (!Active) return false;
            if (CStringTable.GetInstance().GetLanguage() == ELanguages.Language_None) CStringTable.GetInstance().SetLanguage(ELanguages.Language_English);
            CPlayfield pf = CPlayfield.GetInstance();
            pf.ReadProfileStats();
            pf.setDifficulty(Mathf.Clamp(Difficulty, 0, 2));
            pf.m_isEndless = Endless;
            int levels = LevelInfo.getNumberOfDaysPerLevel().Count;
            int level = Endless ? levels - 1 : Mathf.Clamp(Level, 0, levels - 1);
            pf.StartNewLevel(level);
            pf.m_CurrentDay = Mathf.Max(0, Day);
            if (!Endless) pf.m_CurrentDay = Mathf.Min(pf.m_CurrentDay, LevelInfo.getNumberOfDaysPerLevel()[level] - 1);
            Level = -1;
            Endless = false;
            return true;
        }
    }
}
