// Menus/Menu_About (scrolling credits) and Menus/Menu_Loading (level loading screen with hints).
using System.Collections.Generic;
using static CastleFrenzy.G;
using static CastleFrenzy.GE;
using static CastleFrenzy.Ids;

namespace CastleFrenzy
{
    public class Menu_About : MenuSimple
    {
        const int action_back = MB.MENU_ABOUT_ACTION_BASE, action_back_trans = action_back + 1, action_vpane = action_back + 2;
        const int control_id_base = 10;
        const string ABOUT_DEFAULT_FONT = "MM_Normal";
        const uint ABOUT_DEFAULT_COLOR = 0xFFFFFFFF;
        const string ESCAPE_FONT_STR_B = "//<", ESCAPE_FONT_STR_E = ">", ESCAPE_COLOR_STR_B = "//(", ESCAPE_COLOR_STR_E = ")";
        public const string g_castle_frenzy_version_str = "1.1.0";

        VisualControlPane m_vpane;

        public Menu_About(IMenu parent) : base(parent) { }

        static int Begin { get { return STRING_IDS.ABOUT_ABOUT_LINE_1; } }
        static int End { get { return STRING_IDS.ABOUT_ABOUT_LINE_END; } }

        public override void Initialize()
        {
            LoadResources();
            SetMenuName(STRING_IDS.MENU_ABOUT_TITLE);
            PreprocessStrings();
            MB.AddBackButton(this, m_sprite, action_back);
            VisualControls_SetEnableTE(false);
            m_isInitialized = true;
        }

        public override void Destroy()
        {
            VisualControls_Free();
            m_isInitialized = false;
        }

        public override void Draw()
        {
            if (!m_isInitialized) return;
            VisualControls_Draw();
            ((MenuMain)GetParent()).GetTitle().Draw();
        }

        public override void Update(float dt)
        {
            if (!m_isInitialized) return;
            m_vpane.PushVertical(1.0f, dt / 1000.0f);
            base.Update(dt);
        }

        public override void GainFocus()
        {
            base.GainFocus();
            UpdateControlsOnFocus();
            m_vpane.ReturnToStartPos();
        }

        public override void VisualControlDoAction(IVisualControl vc, int action_id)
        {
            if (action_id == action_back) Transition_Start(transition_hide, action_back_trans);
            else if (action_id == action_back_trans)
            {
                CUserProfile profile = __PROFILE();
                bool goToMM = profile.GetInt("GameCompletedState") == 2;
                if (goToMM) profile.Set("GameCompletedState", 3);
                Go(goToMM ? "Menu_Engage" : "Menu_Info");
            }
        }

        void ParseLine(int i, ref string currentFontName, ref uint currentColor, out string text, out uint color)
        {
            string fontName = EscapeFont(i);
            string colorHexStr = EscapeColor(i);
            text = SetVersion(EscapeTextLine(i));
            if (fontName.Length != 0) currentFontName = fontName;
            if (colorHexStr.Length != 0) currentColor = ConvertColorFromString(colorHexStr);
            color = currentColor;
        }

        void UpdateControlsOnFocus()
        {
            string font = ABOUT_DEFAULT_FONT;
            uint color = ABOUT_DEFAULT_COLOR;
            for (int i = Begin; i < End; i++)
            {
                string text;
                uint useColor;
                ParseLine(i, ref font, ref color, out text, out useColor);
                VisualControlText tc = (VisualControlText)VisualControlGetById(control_id_base + (i - Begin));
                tc.SetText(text);
                tc.SetFont(font);
            }
        }

        void PreprocessStrings()
        {
            string font = ABOUT_DEFAULT_FONT;
            uint color = ABOUT_DEFAULT_COLOR;
            m_vpane = new VisualControlPane(action_vpane);
            m_vpane.SetArea(new r2i(0, 0, GAME_SCREEN_W, GAME_SCREEN_H));
            m_vpane.SetType(VisualControlPane.CPT_AllowContMove | VisualControlPane.CPT_Vertical | VisualControlPane.CPT_Cycle);
            VisualControlAdd(m_vpane);
            int lines_size_y = GAME_SCREEN_H + 30;
            for (int i = Begin; i < End; i++)
            {
                string text;
                uint useColor;
                ParseLine(i, ref font, ref color, out text, out useColor);
                VisualControlText tc = new VisualControlText(control_id_base + (i - Begin));
                tc.SetText(text);
                tc.SetFont(font);
                tc.SetFontColor(useColor);
                tc.SetPos(new p2i(GAME_SCREEN_W2, GAME_SCREEN_H + 30 + (i - Begin) * 30));
                tc.SetAlign(CFont.HVCENTER);
                lines_size_y += 30;
                m_vpane.AddControl(tc);
                VisualControlAdd(tc);
            }
            m_vpane.SetScrollSize(new d2i(GAME_SCREEN_W, lines_size_y));
        }

        /// <summary>irr::core::stringw::subString (clamped).</summary>
        static string Sub(string s, int begin, int length)
        {
            if (begin < 0) begin = 0;
            if (begin >= s.Length || length <= 0) return "";
            return s.Substring(begin, System.Math.Min(length, s.Length - begin));
        }

        static string SetVersion(string cline)
        {
            const string strV = "<version>";
            int found = cline.IndexOf(strV, System.StringComparison.Ordinal);
            if (found == -1) return cline;
            return Sub(cline, 0, found) + g_castle_frenzy_version_str + Sub(cline, found + strV.Length + 1, cline.Length - found - strV.Length);
        }

        static string Escape(int sid, string b, string e)
        {
            string cline = _TXT_INT(sid) ?? "";
            int foundBegin = cline.IndexOf(b, System.StringComparison.Ordinal);
            int foundEnd = cline.IndexOf(e, System.StringComparison.Ordinal);
            if (foundBegin == -1 || foundEnd == -1) return "";
            return Sub(cline, foundBegin + 3, foundEnd - 3 - foundBegin);
        }

        static string EscapeFont(int sid) { return Escape(sid, ESCAPE_FONT_STR_B, ESCAPE_FONT_STR_E); }
        static string EscapeColor(int sid) { return Escape(sid, ESCAPE_COLOR_STR_B, ESCAPE_COLOR_STR_E); }

        static string EscapeTextLine(int sid)
        {
            string cline = _TXT_INT(sid) ?? "";
            int fontBegin = cline.IndexOf(ESCAPE_FONT_STR_B, System.StringComparison.Ordinal);
            int fontEnd = cline.IndexOf(ESCAPE_FONT_STR_E, System.StringComparison.Ordinal) + 1;
            if (fontBegin == -1) fontEnd = -1;
            int colorBegin = cline.IndexOf(ESCAPE_COLOR_STR_B, System.StringComparison.Ordinal);
            int colorEnd = cline.IndexOf(ESCAPE_COLOR_STR_E, System.StringComparison.Ordinal) + 1;
            if (colorBegin == -1) colorEnd = -1;
            if (colorEnd == -1 && fontEnd == -1) return cline;
            int maxPos = System.Math.Max(fontEnd, colorEnd);
            return Sub(cline, maxPos, cline.Length - maxPos);
        }

        static uint ConvertColorFromString(string color)
        {
            uint x;
            uint.TryParse(color.Trim(), System.Globalization.NumberStyles.HexNumber, null, out x);
            return x;
        }
    }

    public class MenuLoading : IMenu
    {
        const int LH_1 = 0, LH_3 = 2, LH_6 = 5, LH_7 = 6, LH_9 = 8, LH_10 = 9, LH_11 = 10, LH_12 = 11, LH_13 = 12, LH_14 = 13, LH_15 = 14,
            LH_Boulder = 15, LH_Lightning = 16, LH_GhostArmy = 17, LH_Tornado = 18, LH_Count = 19;

        static readonly int[] HINTS_TEXT =
        {
            STRING_IDS.HELP_LOAD_1, STRING_IDS.HELP_LOAD_2, STRING_IDS.HELP_LOAD_3, STRING_IDS.HELP_LOAD_4, STRING_IDS.HELP_LOAD_5,
            STRING_IDS.HELP_LOAD_6, STRING_IDS.HELP_LOAD_7, STRING_IDS.HELP_LOAD_8, STRING_IDS.HELP_LOAD_9, STRING_IDS.HELP_LOAD_10,
            STRING_IDS.HELP_LOAD_11, STRING_IDS.HELP_LOAD_12, STRING_IDS.HELP_LOAD_13, STRING_IDS.HELP_LOAD_14, STRING_IDS.HELP_LOAD_15,
            STRING_IDS.HELP_BOULDER_DESC, STRING_IDS.HELP_LIGHTNING_DESC, STRING_IDS.HELP_GHOST_DESC, STRING_IDS.HELP_TORNADO_DESC,
        };

        static readonly List<int> s_hints = new List<int>();
        ASprite m_BackGround;
        VisualControlSlider m_LoadingBar;
        float m_loadingDoneTimer;
        int m_hint;

        public MenuLoading(IMenu parent) : base(parent)
        {
            if (s_hints.Count != 0) return;
            s_hints.AddRange(new[] { LH_Boulder, LH_Lightning, LH_GhostArmy, LH_Tornado });
            List<int> rest = new List<int>();
            for (int i = 4; i < LH_Count; i++) rest.Add(i - 4);
            for (int i = rest.Count - 1; i > 0; i--)
            {
                int j = RandInt(0, i);
                int t = rest[i]; rest[i] = rest[j]; rest[j] = t;
            }
            s_hints.AddRange(rest);
        }

        protected override void LoadResources()
        {
            if (m_sprite == null) { m_sprite = new ASprite(); m_sprite.Load("Menu/menu"); }
            if (m_BackGround == null) { m_BackGround = new ASprite(); m_BackGround.Load("Menu/menu_bg"); }
        }

        protected override void FreeResources() { m_sprite = null; m_BackGround = null; }

        public override void Initialize()
        {
            SelectLoadingHint();
            LoadResources();
            m_LoadingBar = new VisualControlSlider(0);
            m_LoadingBar.SetArea(GAME_SCREEN_W2, GAME_SCREEN_H - 30, 276, 16);
            m_LoadingBar.SetPercent(0);
            m_LoadingBar.SetFrames(m_sprite, MENU_F_MENU_SLIDER_EMPTY, MENU_F_MENU_SLIDER_FILL, -1, -1);
            VisualControlAdd(m_LoadingBar);
            m_LoadingBar.SetSelected(false);
            m_isInitialized = true;
            Transition_Start(transition_show);
        }

        public override void Destroy()
        {
            if (m_music_id >= 0) __MGR_SOUNDS().stop(m_music_id);
            VisualControls_Free();
            FreeResources();
            SubMenus_Destroy();
            m_isInitialized = false;
        }

        public override void GainFocus()
        {
            if (!m_isInitialized || m_isOnFocus) return;
            VisualControls_SetEnableTE(true);
            m_isOnFocus = true;
            Transition_Start(transition_show);
        }

        public override void LoseFocus()
        {
            if (!m_isInitialized) return;
            VisualControls_SetEnableTE(false);
            m_isOnFocus = false;
        }

        public override void Draw()
        {
            if (!m_isInitialized) return;
            m_BackGround.PaintFrame(MENU_F_MENU_BG_MENU_BG, GAME_SCREEN_W2, GAME_SCREEN_H2);
            Gfx.draw2DRectangle(new SColor(0x50000000), new r2i(0, 0, GAME_SCREEN_W, GAME_SCREEN_H));
            DrawHint();
            DrawLoading();
            VisualControls_Draw();
        }

        public override void Update(float dt)
        {
            if (!m_isInitialized) return;
            base.Update(dt);
            if (LoadingDone()) m_loadingDoneTimer += dt / 1000;
            m_LoadingBar.SetSelected(false);
            m_LoadingBar.SetEnabled(true);
            m_LoadingBar.Reset();
        }

        void DrawHint()
        {
            p2i p = new p2i(GAME_SCREEN_W2, GAME_SCREEN_H2 - 15);
            d2i d = new d2i(330, 200);
            Gfx.draw2DRectangle(new SColor(0xC8000000), new r2i(p - new p2i(d.Width / 2, d.Height / 2), d));
            m_sprite.PaintFrame(MENU_F_MENU_HORIZONTAL_LINE, p.X, p.Y - d.Height / 2);
            m_sprite.PaintFrame(MENU_F_MENU_HORIZONTAL_LINE, p.X, p.Y + d.Height / 2);
            m_sprite.PaintFrame(MENU_F_MENU_VERTICAL_LINE_INGAME_MENU, p.X - d.Width / 2, p.Y);
            m_sprite.PaintFrame(MENU_F_MENU_VERTICAL_LINE_INGAME_MENU, p.X + d.Width / 2, p.Y);
            m_sprite.PaintFrame(MENU_F_MENU_UPPER_LEFT_ARROW, p.X - d.Width / 2, p.Y - d.Height / 2);
            m_sprite.PaintFrame(MENU_F_MENU_UPPER_RIGHT_ARROW, p.X + d.Width / 2, p.Y - d.Height / 2);
            m_sprite.PaintFrame(MENU_F_MENU_BOTTOM_RIGHT_ARROW, p.X + d.Width / 2, p.Y + d.Height / 2);
            m_sprite.PaintFrame(MENU_F_MENU_BOTTOM_LEFT_ARROW, p.X - d.Width / 2, p.Y + d.Height / 2);
            CPlayfield pf = CPlayfield.GetInstance();
            string str = pf.m_isEndless ? _TXT(STRING_IDS.MENU_NEW_GAME_ENDLESS) : _TXT(STRING_IDS.MENU_CAMPAIGN_LABEL_1 + pf.m_CurrentLevel);
            __FONT("LoadingTitle").DrawString(str, GAME_SCREEN_W2, GAME_SCREEN_H2 - 15 - 200 / 2 + 30, CFont.HVCENTER);
            __FONT("LoadingText").DrawWrappedText(_TXT_INT(HINTS_TEXT[m_hint]), GAME_SCREEN_W2, GAME_SCREEN_H2 - 15 + 20, 330 - 20 * 2, CFont.HVCENTER);
        }

        void DrawLoading()
        {
            if (!LoadingDone())
            {
                m_LoadingBar.SetVisible(true);
                __FONT("LoadingText").DrawString(_TXT(STRING_IDS.MENU_LOADING), GAME_SCREEN_W2, GAME_SCREEN_H - 50, CFont.HVCENTER);
            }
            else
            {
                m_LoadingBar.SetVisible(false);
                if (!CPlayfield.GetInstance().isFading())
                {
                    int alpha = CLAMP((int)(196 + 196 * sinf(-PI / 2 + 2 * PI * m_loadingDoneTimer / 1.0f)), 0, 255);
                    __FONT("LoadingTitle").DrawString(_TXT(STRING_IDS.HELP_LOAD_CONTINUE), GAME_SCREEN_W2, GAME_SCREEN_H - 40, CFont.HVCENTER, alpha);
                }
            }
        }

        public override void VisualControlDoAction(IVisualControl vc, int action_id) { }
        public void SetProgress(int progress) { m_LoadingBar.SetPercent(progress); }
        public bool LoadingDone() { return m_LoadingBar.GetPercent() == 100; }

        void SelectLoadingHint()
        {
            for (int i = 0; i < LH_Count; i++)
            {
                if (!IsHintAppropriate(s_hints[i])) continue;
                m_hint = s_hints[i];
                s_hints.RemoveAt(i);
                s_hints.Add(m_hint);
                break;
            }
        }

        static bool IsHintAppropriate(int hint)
        {
            CPlayfield pf = CPlayfield.GetInstance();
            int level = pf.m_CurrentLevel + 1, day = pf.m_CurrentDay + 1;
            if (level == 1 && day == 1) return hint == LH_13;
            if (hint == LH_13) return false;
            if (level == 10 && day == 1) return hint == LH_14;
            if (hint == LH_14) return false;
            if (level == 11 && day == 1) return hint == LH_15;
            if (hint == LH_15) return false;
            if (pf.getChallenge().getType() != CT_None) return hint == LH_7;
            if (hint == LH_7) return false;
            if (hint == LH_3) return level != 2;
            if (hint == LH_6 || hint == LH_10 || hint == LH_11) return level != 1;
            if (hint == LH_12) return level != 1 && level != 2 && level != 3;
            CLevelStats stats = pf.GetLevelStats();
            if (hint == LH_9) return stats.WeaponLevel[WT_Hydra] == WL_1;
            if (hint == LH_Boulder) return stats.WeaponLevel[WT_Spell] == ST_Boulder;
            if (hint == LH_Lightning) return stats.WeaponLevel[WT_Spell] == ST_Lightning;
            if (hint == LH_GhostArmy) return stats.WeaponLevel[WT_Spell] == ST_GhostArmy;
            if (hint == LH_Tornado) return stats.WeaponLevel[WT_Spell] == ST_Tornado;
            return true;
        }
    }
}
