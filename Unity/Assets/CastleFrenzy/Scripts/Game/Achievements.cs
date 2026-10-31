// Game/Achievements and Game/Cheats ported from the C++ game.
using System;
using System.Collections.Generic;
using static CastleFrenzy.G;
using static CastleFrenzy.GE;
using static CastleFrenzy.Ids;

namespace CastleFrenzy
{
    public class ACHIEVEMENT
    {
        public string name;
        public uint achieve_value;
        public int anim, name_id, dcrp_id;
        public ACHIEVEMENT(string n, uint v, int a, int ni, int di) { name = n; achieve_value = v; anim = a; name_id = ni; dcrp_id = di; }
    }

    public class CAchievements
    {
        const string ACHIEVEMENTS_PREFIX = "achievement_";
        const int EMS_Wait = 0, EMS_MoveRight = 1, EMS_Visible = 2, EMS_MoveLeft = 3;

        static readonly uint g_UnitsType = (1u << UK_Unknown) - 1;
        static readonly uint g_UnitsTypePet = g_UnitsType & ~(1u << UK_Skeleton);
        static readonly uint g_AllUpgradesVal = (1u << (WT_Count * WL_Count)) - 1;
        static readonly uint g_ChallengesNum = (1u << CT_Count) - 1;
        const uint g_HourPlayInMili = 60 * 60 * 1000;

        public static readonly ACHIEVEMENT[] g_AchievementsList =
        {
            new ACHIEVEMENT("Lock & Load", g_AllUpgradesVal, MENU_A_ACHIEVEMENTS_LOCK_LOAD_NORMAL, STRING_IDS.ACHIEVEMENTS_TITLE_0, STRING_IDS.ACHIEVEMENTS_DESCRIPTION_0),
            new ACHIEVEMENT("Cash & Carry", 1000, MENU_A_ACHIEVEMENTS_CASH_CARRY_NORMAL, STRING_IDS.ACHIEVEMENTS_TITLE_1, STRING_IDS.ACHIEVEMENTS_DESCRIPTION_1),
            new ACHIEVEMENT("Picky Customer", 1, MENU_A_ACHIEVEMENTS_PICKY_CUSTOMER_NORMAL, STRING_IDS.ACHIEVEMENTS_TITLE_2, STRING_IDS.ACHIEVEMENTS_DESCRIPTION_2),
            new ACHIEVEMENT("Treat Pets Well", g_UnitsTypePet, MENU_A_ACHIEVEMENTS_TREAT_PETS_WELL_NORMAL, STRING_IDS.ACHIEVEMENTS_TITLE_3, STRING_IDS.ACHIEVEMENTS_DESCRIPTION_3),
            new ACHIEVEMENT("100 Goblins", 100, MENU_A_ACHIEVEMENTS_100_GOBLINS_NORMAL, STRING_IDS.ACHIEVEMENTS_TITLE_4, STRING_IDS.ACHIEVEMENTS_DESCRIPTION_4),
            new ACHIEVEMENT("100 Warlocks", 100, MENU_A_ACHIEVEMENTS_100_WARLOCKS_NORMAL, STRING_IDS.ACHIEVEMENTS_TITLE_5, STRING_IDS.ACHIEVEMENTS_DESCRIPTION_5),
            new ACHIEVEMENT("100 Boars", 100, MENU_A_ACHIEVEMENTS_100_BOARS_NORMAL, STRING_IDS.ACHIEVEMENTS_TITLE_6, STRING_IDS.ACHIEVEMENTS_DESCRIPTION_6),
            new ACHIEVEMENT("100 Trolls", 100, MENU_A_ACHIEVEMENTS_100_TROLLS_NORMAL, STRING_IDS.ACHIEVEMENTS_TITLE_7, STRING_IDS.ACHIEVEMENTS_DESCRIPTION_7),
            new ACHIEVEMENT("100 Dark elves", 100, MENU_A_ACHIEVEMENTS_100_DARK_ELF_NORMAL, STRING_IDS.ACHIEVEMENTS_TITLE_8, STRING_IDS.ACHIEVEMENTS_DESCRIPTION_8),
            new ACHIEVEMENT("100 Skeletons", 100, MENU_A_ACHIEVEMENTS_100_SKELETOMS_NORMAL, STRING_IDS.ACHIEVEMENTS_TITLE_9, STRING_IDS.ACHIEVEMENTS_DESCRIPTION_9),
            new ACHIEVEMENT("Riders on the storm", 20, MENU_A_ACHIEVEMENTS_RIDERS_ON_THE_STORM_NORMAL, STRING_IDS.ACHIEVEMENTS_TITLE_10, STRING_IDS.ACHIEVEMENTS_DESCRIPTION_10),
            new ACHIEVEMENT("Fireball Master", 1, MENU_A_ACHIEVEMENTS_FIREBALL_MASTER_NORMAL, STRING_IDS.ACHIEVEMENTS_TITLE_11, STRING_IDS.ACHIEVEMENTS_DESCRIPTION_11),
            new ACHIEVEMENT("Statue garden", 100, MENU_A_ACHIEVEMENTS_STATUE_GARDEN_NORMAL, STRING_IDS.ACHIEVEMENTS_TITLE_12, STRING_IDS.ACHIEVEMENTS_DESCRIPTION_12),
            new ACHIEVEMENT("Burden of the Troll", 50, MENU_A_ACHIEVEMENTS_TROLL_BURDEN_NORMAL, STRING_IDS.ACHIEVEMENTS_TITLE_13, STRING_IDS.ACHIEVEMENTS_DESCRIPTION_13),
            new ACHIEVEMENT("Ice ice baby", 50, MENU_A_ACHIEVEMENTS_ICE_ICE_BABY_NORMAL, STRING_IDS.ACHIEVEMENTS_TITLE_14, STRING_IDS.ACHIEVEMENTS_DESCRIPTION_14),
            new ACHIEVEMENT("No more Strangers", g_UnitsType, MENU_A_ACHIEVEMENTS_NO_MORE_STRANGERS_NORMAL, STRING_IDS.ACHIEVEMENTS_TITLE_15, STRING_IDS.ACHIEVEMENTS_DESCRIPTION_15),
            new ACHIEVEMENT("Free your Mind", 1, MENU_A_ACHIEVEMENTS_FREE_YOUR_MINDE_NORMAL, STRING_IDS.ACHIEVEMENTS_TITLE_16, STRING_IDS.ACHIEVEMENTS_DESCRIPTION_16),
            new ACHIEVEMENT("Recruit", 1, MENU_A_ACHIEVEMENTS_RECLUUIT_NORMAL, STRING_IDS.ACHIEVEMENTS_TITLE_17, STRING_IDS.ACHIEVEMENTS_DESCRIPTION_17),
            new ACHIEVEMENT("Soldier", 1, MENU_A_ACHIEVEMENTS_SOLDIER_NORMAL, STRING_IDS.ACHIEVEMENTS_TITLE_18, STRING_IDS.ACHIEVEMENTS_DESCRIPTION_18),
            new ACHIEVEMENT("True Defender", 1, MENU_A_ACHIEVEMENTS_TRUE_DEFENDER_NORMAL, STRING_IDS.ACHIEVEMENTS_TITLE_19, STRING_IDS.ACHIEVEMENTS_DESCRIPTION_19),
            new ACHIEVEMENT("Brick in the wall", 100, MENU_A_ACHIEVEMENTS_ANOTHER_BRICK_IN_THE_WALL_NORMAL, STRING_IDS.ACHIEVEMENTS_TITLE_20, STRING_IDS.ACHIEVEMENTS_DESCRIPTION_20),
            new ACHIEVEMENT("Challenger", g_ChallengesNum, MENU_A_ACHIEVEMENTS_CHALLENGER_NORMAL, STRING_IDS.ACHIEVEMENTS_TITLE_21, STRING_IDS.ACHIEVEMENTS_DESCRIPTION_21),
            new ACHIEVEMENT("Addicted", g_HourPlayInMili, MENU_A_ACHIEVEMENTS_ADDICTED_NORMAL, STRING_IDS.ACHIEVEMENTS_TITLE_22, STRING_IDS.ACHIEVEMENTS_DESCRIPTION_22),
        };

        static CAchievements s_instance;
        public static CAchievements GetInstance() { return s_instance ?? (s_instance = new CAchievements()); }

        string m_LastLoadedProfileName = "";
        readonly Dictionary<string, uint> m_Achievements = new Dictionary<string, uint>();
        ASprite m_sprite;
        readonly List<string> m_eventMsgList = new List<string>();
        string m_eventMsg = "";
        int m_eventMsgState = EMS_Wait;
        float m_eventMsgViewTime;
        int m_eventMsgBgOffset;

        public void reset() { m_Achievements.Clear(); m_LastLoadedProfileName = ""; }

        public void draw()
        {
            if (m_sprite != null && m_eventMsgList.Count > 0) eventMsgDraw();
        }

        public void update(float timestamp)
        {
            if (m_sprite != null && m_eventMsgList.Count > 0) eventMsgUpdate(timestamp);
        }

        public void init()
        {
            release();
            m_eventMsgState = EMS_Wait;
            m_eventMsgViewTime = 0;
            m_eventMsgBgOffset = 0;
            m_sprite = new ASprite();
            if (!m_sprite.Load("Menu/menu")) { Logs.error("Could not load achievements sprite: Menu/menu"); m_sprite = null; return; }
            eventMsgUpdatePos(0.0f);
        }

        public void release() { m_sprite = null; m_eventMsgList.Clear(); }
        public void eventMsgPush(string text) { m_eventMsgList.Add(text); }
        void eventMsgErase() { if (m_eventMsgList.Count > 0) m_eventMsgList.RemoveAt(0); }

        void eventMsgUpdatePos(float percent)
        {
            int h = m_sprite.GetFrameHeight(MENU_F_MENU_ACHIEVEMENTS_BG);
            m_eventMsgBgOffset = (int)(percent * h) - h;
        }

        void eventMsgUpdate(float dt)
        {
            const int view_time_message = 2000, move_left_time = 300, move_right_time = 300;
            m_eventMsgViewTime += dt;
            if (!CPlayfield.GetInstance().isChallengeVisible())
            {
                if (m_eventMsgState == EMS_Wait)
                {
                    if (m_eventMsgList.Count > 0)
                    {
                        m_eventMsgState = EMS_MoveRight;
                        m_eventMsgViewTime = 0;
                        m_eventMsg = m_eventMsgList[0];
                        __MGR_SOUNDS().play(SFX_CHALLENGE_COMPLETED);
                    }
                    else eventMsgUpdatePos(-1.0f);
                }
                if (m_eventMsgState == EMS_MoveRight)
                {
                    if (m_eventMsgViewTime >= move_right_time) { m_eventMsgState = EMS_Visible; m_eventMsgViewTime = 0; }
                    else eventMsgUpdatePos(m_eventMsgViewTime / move_right_time);
                }
                if (m_eventMsgState == EMS_Visible)
                {
                    if (m_eventMsgViewTime >= view_time_message)
                    {
                        if (m_eventMsgList.Count > 1)
                        {
                            eventMsgErase();
                            m_eventMsgViewTime = 0;
                            __MGR_SOUNDS().play(SFX_CHALLENGE_COMPLETED);
                        }
                        else { m_eventMsgState = EMS_MoveLeft; m_eventMsgViewTime = 0; }
                    }
                    else eventMsgUpdatePos(1.0f);
                }
                if (m_eventMsgState == EMS_MoveLeft)
                {
                    if (m_eventMsgViewTime >= move_left_time)
                    {
                        m_eventMsgState = EMS_Wait;
                        m_eventMsgViewTime = 0;
                        if (m_eventMsgList.Count > 0) eventMsgErase();
                    }
                    else
                    {
                        eventMsgUpdatePos(1.0f - m_eventMsgViewTime / move_left_time);
                        if (m_eventMsgList.Count > 0)
                        {
                            string msg = m_eventMsgList[0];
                            if (m_eventMsg == msg)
                            {
                                if (m_eventMsgList.Count > 1)
                                {
                                    m_eventMsgState = EMS_MoveRight;
                                    m_eventMsgViewTime = 0;
                                    eventMsgErase();
                                    m_eventMsg = m_eventMsgList[0];
                                }
                            }
                            else
                            {
                                m_eventMsgState = EMS_MoveRight;
                                m_eventMsgViewTime = 0;
                                m_eventMsg = msg;
                            }
                        }
                    }
                }
            }
            else
            {
                m_eventMsgState = EMS_Wait;
                eventMsgUpdatePos(-1.0f);
            }
        }

        void eventMsgDraw()
        {
            m_sprite.PaintFrame(MENU_F_MENU_ACHIEVEMENTS_BG, GAME_SCREEN_W2, m_eventMsgBgOffset);
            r2i rect = m_sprite.GetFrameRect(MENU_F_MENU_ACHIEVEMENTS_BG);
            string str = _TXT(STRING_IDS.ACHIEVEMENTS_ACHIEVEMENT_UNLOCKED) + "\n" + m_eventMsg;
            __FONT("Achievements").DrawWrappedText(str, GAME_SCREEN_W2, m_eventMsgBgOffset + rect.getCenter().Y, rect.getWidth() - 10, CFont.HVCENTER, 255);
        }

        static ACHIEVEMENT Find(string name)
        {
            foreach (ACHIEVEMENT a in g_AchievementsList) if (a.name == name) return a;
            return null;
        }

        uint getAchieveValue(string name) { ACHIEVEMENT a = Find(name); return a != null ? a.achieve_value : 0; }

        public bool resetAchievementsByProfile(string profileName)
        {
            reset();
            CUserProfile profile = __PROFILE();
            foreach (ACHIEVEMENT a in g_AchievementsList)
            {
                int val = profile.GetInt(ACHIEVEMENTS_PREFIX + a.name);
                if (val < 0) val = 0;
                m_Achievements[a.name] = (uint)val;
            }
            m_LastLoadedProfileName = profileName;
            if (!isAchieved("Addicted")) setValue("Addicted", 0);
            return true;
        }

        public bool loadAchievementsByProfile(string profileName)
        {
            if (m_LastLoadedProfileName.Length > 0) saveAchievementsByProfile(m_LastLoadedProfileName);
            return resetAchievementsByProfile(profileName);
        }

        public bool saveAchievementsByProfile(string profileName)
        {
            if (m_LastLoadedProfileName.Length == 0) return false;
            CUserProfile profile = __PROFILE();
            foreach (ACHIEVEMENT a in g_AchievementsList)
            {
                uint v;
                m_Achievements.TryGetValue(a.name, out v);
                profile.Set(ACHIEVEMENTS_PREFIX + a.name, (int)v);
            }
            return true;
        }

        public bool isAchieved(string name)
        {
            uint val;
            if (!m_Achievements.TryGetValue(name, out val)) return false;
            return val == getAchieveValue(name);
        }

        public uint getValue(string name)
        {
            uint val;
            return m_Achievements.TryGetValue(name, out val) ? val : 0;
        }

        public void setValue(string name, uint value)
        {
            bool wasAchieved = isAchieved(name);
            uint val;
            if (!m_Achievements.TryGetValue(name, out val)) return;
            uint aval = getAchieveValue(name);
            if (name == "Cash & Carry") { if (val != aval && value > aval) m_Achievements[name] = aval; }
            else if (name == "Treat Pets Well" || name == "No more Strangers" || name == "Challenger") m_Achievements[name] = val | value;
            else m_Achievements[name] = value;
            if (!wasAchieved && isAchieved(name)) unlockAchievement(name);
        }

        public void setValue(string name, int value) { setValue(name, (uint)value); }

        public void addValue(string name, uint value)
        {
            bool wasAchieved = isAchieved(name);
            uint val;
            if (!m_Achievements.TryGetValue(name, out val)) return;
            uint aval = getAchieveValue(name);
            if (name != "Treat Pets Well") m_Achievements[name] = Math.Min(val + value, aval);
            if (!wasAchieved && isAchieved(name)) unlockAchievement(name);
        }

        public void addValue(string name, int value) { addValue(name, (uint)value); }

        public void setAchieved(string name)
        {
            if (!m_Achievements.ContainsKey(name)) return;
            if (!isAchieved(name)) unlockAchievement(name);
            m_Achievements[name] = getAchieveValue(name);
        }

        void unlockAchievement(string name)
        {
            ACHIEVEMENT a = Find(name);
            if (a != null) eventMsgPush(_TXT(a.name_id));
        }
    }

    public class CCheats
    {
        const int CHEAT_START_POS = 50, CHEAT_LINE_HEIGHT = 40;
        static readonly int[] CHEAT_BUFFER = { 1, 1, 1, 2, 2, 2, 1, 1, 1 };

        class Field { public bool Switch; public string text; public r2i rect; }

        static CCheats s_instance;
        public static CCheats GetInstance() { return s_instance ?? (s_instance = new CCheats()); }

        public bool m_Enabled, m_IsVisible;
        readonly int[] m_CheatBuffer = new int[CHEAT_BUFFER.Length];
        readonly SortedDictionary<string, Field> m_CheatsList = new SortedDictionary<string, Field>(StringComparer.Ordinal);

        void AddNewCheat(string key, string text, bool sw)
        {
            int y = CHEAT_START_POS + CHEAT_LINE_HEIGHT * m_CheatsList.Count;
            m_CheatsList[key] = new Field { text = text, Switch = sw, rect = new r2i(0, y, 480, y + CHEAT_LINE_HEIGHT) };
        }

        public void Init()
        {
            m_CheatsList.Clear();
            AddNewCheat("Immortality", "Immortality", true);
            AddNewCheat("Money", "Money: +10000", false);
            AddNewCheat("Souls", "Souls: +100", false);
            AddNewCheat("FPS", "Show FPS", true);
            AddNewCheat("Weapons", "Unlock all weapons", true);
            AddNewCheat("UnlockLevels", "Unlock all levels", true);
            Clear();
            m_Enabled = true;
        }

        public void Clear() { for (int i = 0; i < m_CheatBuffer.Length; i++) m_CheatBuffer[i] = -CHEAT_BUFFER.Length; }
        public bool IsVisible() { return m_IsVisible; }
        public void Show() { m_IsVisible = true; }
        public void Hide() { m_IsVisible = false; }

        public void Draw()
        {
            if (!m_IsVisible || !m_Enabled) return;
            SColor line = new SColor(0xFFF0F0F0);
            foreach (var kv in m_CheatsList)
            {
                Field f = kv.Value;
                string str = f.text;
                if (f.Switch) str += IsActive(kv.Key) ? ": On" : ": Off";
                r2i r = f.rect;
                Gfx.draw2DLine(new p2i(r.UpperLeftCorner.X, r.LowerRightCorner.Y), new p2i(r.LowerRightCorner.X, r.LowerRightCorner.Y), line);
                Gfx.draw2DLine(new p2i(r.UpperLeftCorner.X, r.UpperLeftCorner.Y), new p2i(r.LowerRightCorner.X, r.UpperLeftCorner.Y), line);
                __FONT("MS_Title").DrawString(str, 240, (r.UpperLeftCorner.Y + r.LowerRightCorner.Y) / 2, CFont.HVCENTER);
            }
        }

        public void Update(int sector)
        {
            if (m_IsVisible || !m_Enabled) { Clear(); return; }
            int n = CHEAT_BUFFER.Length;
            if (m_CheatBuffer[n - 1] < 0)
            {
                int i = 0;
                while (m_CheatBuffer[i] > 0) i++;
                m_CheatBuffer[i] = sector;
            }
            else
            {
                for (int i = 0; i < n - 1; i++) m_CheatBuffer[i] = m_CheatBuffer[i + 1];
                m_CheatBuffer[n - 1] = sector;
            }
            bool same = true;
            for (int i = 0; i < n; i++) if (CHEAT_BUFFER[i] != m_CheatBuffer[i]) { same = false; break; }
            if (same) { m_IsVisible = true; Clear(); }
        }

        public bool IsActive(string key)
        {
            switch (key)
            {
                case "Immortality": return CGameConfig.Invulnerable;
                case "FPS": return CGameConfig.DrawFPS;
                case "Weapons": return CGameConfig.AllWeaponsSold;
                case "UnlockLevels": return CGameConfig.UnlockAllLevels;
            }
            return false;
        }

        public bool Activate(int x, int y)
        {
            string key = "";
            foreach (var kv in m_CheatsList) if (kv.Value.rect.isPointInside(new p2i(x, y))) { key = kv.Key; break; }
            CPlayfield pf = CPlayfield.GetInstance();
            switch (key)
            {
                case "Immortality": CGameConfig.Invulnerable = !CGameConfig.Invulnerable; break;
                case "Money": pf.GetLevelStats().Gold += 10000; break;
                case "Souls": pf.GetLevelStats().Souls += 100; break;
                case "FPS": CGameConfig.DrawFPS = !CGameConfig.DrawFPS; break;
                case "Weapons":
                    CGameConfig.AllWeaponsSold = !CGameConfig.AllWeaponsSold;
                    if (pf.IsLevelLoaded())
                    {
                        pf.getHydra().setLevel(WL_3); pf.getHydra().reset();
                        pf.getGriffin().setLevel(WL_3); pf.getGriffin().reset();
                        pf.getFireball().setLevel(WL_3); pf.getFireball().reset();
                        pf.getIceball().setLevel(WL_3); pf.getIceball().reset();
                        pf.getGate().setLevel(WL_3); pf.getGate().reset();
                    }
                    break;
                case "UnlockLevels": CGameConfig.UnlockAllLevels = !CGameConfig.UnlockAllLevels; break;
            }
            return true;
        }
    }
}
