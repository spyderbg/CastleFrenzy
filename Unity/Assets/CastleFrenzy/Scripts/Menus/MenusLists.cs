// Menus/Menu_Achievements, Menu_LanguageSelection, Menu_HighScore and Menu_HighScoresLevelList.
using static CastleFrenzy.G;
using static CastleFrenzy.GE;
using static CastleFrenzy.Ids;

namespace CastleFrenzy
{
    public struct SingleScore
    {
        public int frame;
        public string text;
        public int value, multiplier;
        public SingleScore(int f, string t, int v, int m) { frame = f; text = t; value = v; multiplier = m; }
    }

    public static class ScoreDraw
    {
        public static void DrawScores(ASprite sprite, int x, ref int y, r2i backgroundRect, ref int total, SingleScore[] scores, int alpha)
        {
            int scoresCount = scores.Length;
            int yStep = 40 + (5 - (scoresCount + 1)) * 40 / scoresCount;
            for (int i = 0; i < scoresCount; ++i)
            {
                sprite.PaintFrame(scores[i].frame, x + 38, y, alpha);
                __FONT("Arial").DrawString(scores[i].text, x + 60, y, CFont.VCENTER | CFont.LEFT, alpha);
                int mul = scores[i].multiplier == 0 ? 1 : scores[i].multiplier;
                if (scores[i].multiplier != 0)
                {
                    __FONT("SingleHighScore").DrawString(FormatNumber(scores[i].value), x + 130, y, CFont.VCENTER | CFont.LEFT, alpha);
                    __FONT("SingleHighScore").DrawString("x" + FormatNumber(scores[i].multiplier), x + 220, y, CFont.VCENTER | CFont.LEFT, alpha);
                }
                __FONT("SingleHighScore").DrawString(FormatNumber(scores[i].value * mul), x + backgroundRect.getWidth() - 20, y, CFont.VCENTER | CFont.RIGHT, alpha);
                total += scores[i].value * mul;
                y += yStep;
            }
        }
    }

    /// <summary>Shared vertical list-of-buttons pane with optional scroll arrows.</summary>
    public abstract class MenuPaneList : MenuSimple
    {
        protected VisualControlPane m_vpane;
        protected VisualControlButton m_arrowUp, m_arrowDown;

        protected MenuPaneList(IMenu parent) : base(parent) { }

        protected void AddArrows(int actionUp, int actionDown)
        {
            m_arrowUp = new VisualControlButton(actionUp, -1, MB.k_back_button_sfx);
            m_arrowUp.SetAnimations(m_sprite, MENU_A_MENU_ARROW_UP_NORMAL, MENU_A_MENU_ARROW_UP_PRESSED, MENU_A_MENU_ARROW_UP_BLINK, MENU_A_MENU_ARROW_UP_NORMAL);
            m_arrowUp.SetArea(428, 85, m_sprite.GetFrameWidth(MENU_F_MENU_BIG_ARROW_UP), m_sprite.GetFrameHeight(MENU_F_MENU_BIG_ARROW_UP));
            VisualControlAdd(m_arrowUp);
            m_arrowDown = new VisualControlButton(actionDown, -1, MB.k_back_button_sfx);
            m_arrowDown.SetAnimations(m_sprite, MENU_A_MENU_ARROW_DOWN_NORMAL, MENU_A_MENU_ARROW_DOWN_PRESSED, MENU_A_MENU_ARROW_DOWN_BLINK, MENU_A_MENU_ARROW_DOWN_NORMAL);
            m_arrowDown.SetArea(428, 270, m_sprite.GetFrameWidth(MENU_F_MENU_BIG_ARROW_DOWN), m_sprite.GetFrameHeight(MENU_F_MENU_BIG_ARROW_DOWN));
            VisualControlAdd(m_arrowDown);
        }

        protected VisualControlButton AddListButton(int action, int controlId, int y, int textId, int sfx = SFX_MENU_SELECT)
        {
            VisualControlButton b = new VisualControlButton(action, controlId, sfx);
            b.SetAnimations(m_sprite, MENU_A_MENU_BASE_BUTTON_NORMAL, MENU_A_MENU_BASE_BUTTON_PRESSED, MENU_A_MENU_BASE_BUTTON_BLINK);
            b.SetArea(GAME_SCREEN_W2 - 70, y, m_sprite.GetFrameWidth(MENU_F_MENU_BASE_BUTTON), m_sprite.GetFrameHeight(MENU_F_MENU_BASE_BUTTON));
            b.SetText1(textId, "MM_Normal", "MM_Pressed");
            m_vpane.AddControl(b);
            VisualControlAdd(b);
            return b;
        }

        protected void ScrollArrows()
        {
            if (m_arrowUp != null && m_arrowUp.IsPressed()) m_vpane.ScrollVertical(-1);
            if (m_arrowDown != null && m_arrowDown.IsPressed()) m_vpane.ScrollVertical(1);
        }
    }

    public class Menu_Achievements : MenuSimple
    {
        const int action_back = MB.MENU_ACHIEVEMENTS_ACTION_BASE, action_back_trans = action_back + 1, action_vpane = action_back + 2, action_transition = action_back + 3;

        VisualControlPane m_vpane;
        ASprite m_Icons;
        int m_LastDescription;

        public Menu_Achievements(IMenu parent) : base(parent) { }

        static ACHIEVEMENT[] List { get { return CAchievements.g_AchievementsList; } }

        protected override void LoadResources()
        {
            base.LoadResources();
            if (m_Icons == null)
            {
                m_Icons = new ASprite();
                m_Icons.Load("Menu/achievements");
            }
        }

        protected override void FreeResources() { m_Icons = null; }

        public override void GainFocus()
        {
            base.GainFocus();
            SetButtonsAimations();
            m_LastDescription = 0;
            for (int i = 0; i < List.Length; i++) VisualControlGetByActionId(action_transition + 1 + i).SetSelected(i == m_LastDescription);
            m_vpane.ReturnToStartPos();
        }

        public override void Initialize()
        {
            LoadResources();
            SetMenuName(STRING_IDS.MENU_ACHIEVEMENTS_TITLE);
            CreateButtons();
            MB.AddBackButton(this, m_sprite, action_back);
            VisualControls_SetEnableTE(false);
            m_isInitialized = true;
            m_LastDescription = 0;
        }

        void CreateButtons()
        {
            int bw = m_sprite.GetFrameWidth(MENU_F_MENU_BASE_BUTTON), bh = m_sprite.GetFrameHeight(MENU_F_MENU_BASE_BUTTON);
            m_vpane = new VisualControlPane(action_vpane);
            m_vpane.SetArea(new r2i(195, 40, GAME_SCREEN_W, GAME_SCREEN_H));
            m_vpane.SetScrollSize(new d2i(bw, 56 * List.Length));
            m_vpane.SetRows(List.Length);
            for (int i = 0; i < List.Length; i++)
            {
                VisualControlButton b = new VisualControlButton(action_transition + 1 + i, 10 + i);
                b.SetArea(GAME_SCREEN_W2 - 195 + 94, 56 / 2 + i * 56, bw, bh);
                b.SetText1(List[i].name_id, "MM_Achievements", "MM_Achievements_Pressed");
                b.SetEnabled(true);
                m_vpane.AddControl(b);
                VisualControlAdd(b);
            }
            VisualControlAdd(m_vpane);
            SetButtonsAimations();
        }

        void SetButtonsAimations()
        {
            for (int i = 0; i < List.Length; i++)
            {
                ACHIEVEMENT a = List[i];
                VisualControlButton b = (VisualControlButton)VisualControlGetByActionId(action_transition + 1 + i);
                int baseAnim = CAchievements.GetInstance().isAchieved(a.name) ? a.anim : a.anim + 3;
                b.SetAnimations(m_Icons, baseAnim, baseAnim + 1, baseAnim + 2);
            }
        }

        public override void Draw()
        {
            if (!m_isInitialized) return;
            m_sprite.PaintFrame(MENU_F_MENU_ACHIEV_WINDOW, 100, 205);
            __FONT("MM_Achievements").DrawWrappedText(_TXT_INT(List[m_LastDescription].dcrp_id), 100, 205,
                m_sprite.GetFrameWidth(MENU_F_MENU_ACHIEV_WINDOW) - 45, CFont.HCENTER | CFont.VCENTER);
            VisualControls_Draw();
            ((MenuMain)GetParent()).GetTitle().Draw();
        }

        public override void VisualControlDoAction(IVisualControl vc, int action_id)
        {
            switch (action_id)
            {
                case button_action_lose_focus:
                {
                    int a = vc.GetActionId();
                    if (a >= action_transition + 1 && a < action_transition + 1 + List.Length) vc.GetTouchElement().ReassignTouchTo(m_vpane.GetTouchElement());
                    break;
                }
                case action_back: Transition_Start(transition_hide, action_back_trans); break;
                case action_back_trans: Go("Menu_Info"); break;
                case action_vpane: break;
                default:
                    if (action_id >= action_transition + 1 && action_id < action_transition + 1 + List.Length)
                    {
                        m_LastDescription = action_id - (action_transition + 1);
                        for (int i = 0; i < List.Length; i++) VisualControlGetByActionId(action_transition + 1 + i).SetSelected(i == m_LastDescription);
                    }
                    break;
            }
        }
    }

    public class Menu_LanguageSelection : MenuPaneList
    {
        const int action_back = MB.MENU_LANGUAGE_SELECT, action_back_trans = action_back + 1, action_vpane = action_back + 2,
            action_up = action_back + 3, action_down = action_back + 4, action_transition = action_back + 5;
        const int Language_Count = 6;
        static readonly int[] LanguageStringIDs =
        {
            STRING_IDS.MENU_LANGUAGE_ENGLISH, STRING_IDS.MENU_LANGUAGE_FRENCH, STRING_IDS.MENU_LANGUAGE_SPANISH,
            STRING_IDS.MENU_LANGUAGE_DEUTCH, STRING_IDS.MENU_LANGUAGE_ITALIAN, STRING_IDS.MENU_LANGUAGE_JAPANESE,
        };

        VisualControlButton m_backButton;

        public Menu_LanguageSelection(IMenu parent) : base(parent) { }

        public override void GainFocus()
        {
            base.GainFocus();
            m_vpane.ReturnToStartPos();
            m_backButton.SetVisible(!IS_LANG_NONE());
        }

        public override void Initialize()
        {
            LoadResources();
            SetMenuName(STRING_IDS.MENU_LANGUAGE_TITLE);
            m_vpane = new VisualControlPane(action_vpane);
            m_vpane.SetArea(new r2i(70, 40, GAME_SCREEN_W - 70, GAME_SCREEN_H));
            m_vpane.SetScrollSize(new d2i(m_sprite.GetFrameWidth(MENU_F_MENU_BASE_BUTTON), 46 * Language_Count));
            m_vpane.SetRows(Language_Count);
            m_vpane.SetType(0);
            for (int i = 0; i < Language_Count; i++) AddListButton(action_transition + 1 + i, 10 + i, 46 / 2 + i * 46, LanguageStringIDs[i]);
            VisualControlAdd(m_vpane);
            m_backButton = MB.AddBackButton(this, m_sprite, action_back);
            m_backButton.SetVisible(!IS_LANG_NONE());
            VisualControls_SetEnableTE(false);
            m_isInitialized = true;
        }

        public override void Draw()
        {
            if (!m_isInitialized) return;
            VisualControls_Draw();
            ((MenuMain)GetParent()).GetTitle().Draw();
        }

        public override void VisualControlDoAction(IVisualControl vc, int action_id)
        {
            switch (action_id)
            {
                case button_action_lose_focus: break;
                case action_back: Transition_Start(transition_hide, action_back_trans); break;
                case action_back_trans: Go("Menu_GameSettings"); break;
                case action_transition:
                case action_vpane:
                case action_up:
                case action_down: break;
                default:
                    if (action_id >= action_transition + 1 && action_id < action_transition + 1 + Language_Count)
                    {
                        bool first_call = IS_LANG_NONE();
                        __MGR_STRINGS().SetLanguage((ELanguages)(action_id - (action_transition + 1)));
                        if (first_call) Go("Menu_Engage");
                    }
                    break;
            }
        }
    }

    public class Menu_HighScore : MenuSimple
    {
        const int action_back = MB.MENU_HIGHSCORES_BASE, action_back_trans = action_back + 1;

        static bool m_endless;
        static int m_level;
        static readonly CampaignScore m_campaignScore = new CampaignScore();
        static readonly EndlessScore m_endlessScore = new EndlessScore();
        ASprite m_highScoreIcons;

        public Menu_HighScore(IMenu parent) : base(parent) { }

        public override void Initialize()
        {
            LoadResources();
            if (m_highScoreIcons == null)
            {
                m_highScoreIcons = new ASprite();
                m_highScoreIcons.Load("Menu/high_score_icone");
            }
            SetMenuName(STRING_IDS.MENU_HIGH_SCORE_BUTTON);
            MB.AddBackButton(this, m_sprite, action_back);
            VisualControls_SetEnableTE(false);
            m_isInitialized = true;
        }

        public override void Destroy()
        {
            VisualControls_Free();
            m_highScoreIcons = null;
            m_isInitialized = false;
        }

        public override void Draw()
        {
            if (!m_isInitialized) return;
            int total = 0;
            r2i backgroundRect = new r2i(46, 67, 445, 270);
            int x = backgroundRect.UpperLeftCorner.X;
            int y = backgroundRect.UpperLeftCorner.Y + 30;
            Gfx.draw2DRectangle(new SColor(m_transition_alpha * 192 / 255, 0, 0, 0), new r2i(0, 0, 480, 320));
            if (m_endless)
            {
                SingleScore[] scores =
                {
                    new SingleScore(MENU_F_HIGH_SCORE_ICONE_KILLS, _TXT(STRING_IDS.MENU_SCORE_KILLS), m_endlessScore.kills, Scores.k_multiplier_kills),
                    new SingleScore(MENU_F_HIGH_SCORE_ICONE_DAYS, _TXT(STRING_IDS.MENU_SCORE_DAYS), m_endlessScore.days, Scores.k_multiplier_days),
                    new SingleScore(MENU_F_HIGH_SCORE_ICONE_GOLD, _TXT(STRING_IDS.MENU_SCORE_GOLD), m_endlessScore.gold, Scores.k_multiplier_gold),
                    new SingleScore(MENU_F_HIGH_SCORE_ICONE_SOULS, _TXT(STRING_IDS.MENU_SCORE_SOULS), m_endlessScore.souls, Scores.k_multiplier_souls),
                };
                ScoreDraw.DrawScores(m_highScoreIcons, x, ref y, backgroundRect, ref total, scores, m_transition_alpha);
                __FONT("MM_Normal").DrawString(_TXT(STRING_IDS.MENU_NEW_GAME_ENDLESS), 250, 55, CFont.HCENTER | CFont.VCENTER, m_transition_alpha);
            }
            else
            {
                int[] difficulty_icons = { MENU_F_HIGH_SCORE_ICONE_EASY, MENU_F_HIGH_SCORE_ICONE_MEDIUM, MENU_F_HIGH_SCORE_ICONE_HARD };
                string[] difficulty_texts = { _TXT(STRING_IDS.MENU_SCORE_EASY), _TXT(STRING_IDS.MENU_SCORE_MEDIUM), _TXT(STRING_IDS.MENU_SCORE_HARD) };
                int d = CLAMP(m_campaignScore.difficulty, 0, 2);
                string str = FormatString("%s: %s", _TXT(STRING_IDS.MENU_SELECT_DIFFICULTY_2), difficulty_texts[d]);
                SingleScore[] scores =
                {
                    new SingleScore(MENU_F_HIGH_SCORE_ICONE_GOLD, _TXT(STRING_IDS.MENU_SCORE_GOLD), m_campaignScore.gold, Scores.k_multiplier_gold),
                    new SingleScore(MENU_F_HIGH_SCORE_ICONE_SOULS, _TXT(STRING_IDS.MENU_SCORE_SOULS), m_campaignScore.souls, Scores.k_multiplier_souls),
                    new SingleScore(difficulty_icons[d], str, Scores.k_difficulty_bonus[d], 0),
                };
                ScoreDraw.DrawScores(m_highScoreIcons, x, ref y, backgroundRect, ref total, scores, m_transition_alpha);
                __FONT("MM_Normal").DrawString(_TXT(STRING_IDS.MENU_CAMPAIGN_LABEL_1 + m_level), 250, 55, CFont.HCENTER | CFont.VCENTER, m_transition_alpha);
            }
            int offset_y = m_endless ? y - 5 : y + 15;
            __FONT("TotalHighScore").DrawString(FormatNumber(total), x + backgroundRect.getWidth() - 100, offset_y, CFont.VCENTER | CFont.RIGHT, m_transition_alpha);
            __FONT("TotalHighScore").DrawString(_TXT(STRING_IDS.INGAME_TOTAL), x + 80, offset_y, CFont.VCENTER | CFont.LEFT, m_transition_alpha);
            VisualControls_Draw();
            ((MenuMain)GetParent()).GetTitle().Draw();
        }

        public override void VisualControlDoAction(IVisualControl vc, int action_id)
        {
            if (action_id == action_back) Transition_Start(transition_hide, action_back_trans);
            else if (action_id == action_back_trans) Go("Menu_HighScoresLevelList");
        }

        public static void SetLevel(int level)
        {
            m_endless = false;
            m_level = level;
            Scores.GetCampaignScore(__PROFILE(), m_campaignScore, level);
        }

        public static void SetEndless()
        {
            m_endless = true;
            Scores.GetEndlessScore(__PROFILE(), m_endlessScore);
        }
    }

    public class Menu_HighScoresLevelList : MenuPaneList
    {
        const int action_back = MB.MENU_HIGHSCORESLIST_BASE, action_back_trans = action_back + 1, action_vpane = action_back + 2,
            action_up = action_back + 3, action_down = action_back + 4, action_transition = action_back + 5;

        int m_selected = -1;

        public Menu_HighScoresLevelList(IMenu parent) : base(parent) { }

        static int LevelCount { get { return LevelInfo.getNumberOfDaysPerLevel().Count - 1; } }

        public override void GainFocus()
        {
            base.GainFocus();
            CUserProfile profile = __PROFILE();
            for (int i = 0; i < LevelCount; i++) VisualControlGetByActionId(action_transition + 1 + i).SetEnabled(Scores.HasCampaignScore(profile, i));
            VisualControlGetByActionId(action_transition + 1 + LevelCount).SetEnabled(Scores.HasEndlessScore(profile));
            m_vpane.ReturnToStartPos();
            m_selected = -1;
        }

        public override void Initialize()
        {
            LoadResources();
            SetMenuName(STRING_IDS.MENU_HIGH_SCORE_BUTTON);
            int levelCount = LevelCount;
            m_vpane = new VisualControlPane(action_vpane);
            m_vpane.SetArea(new r2i(70, 40, GAME_SCREEN_W - 70, GAME_SCREEN_H));
            m_vpane.SetScrollSize(new d2i(m_sprite.GetFrameWidth(MENU_F_MENU_BASE_BUTTON), 46 * (levelCount + 1)));
            m_vpane.SetRows(levelCount + 1);
            for (int i = 0; i < levelCount; i++) AddListButton(action_transition + 1 + i, 10 + i, 46 / 2 + (i + 1) * 46, STRING_IDS.MENU_CAMPAIGN_LABEL_1 + i, SFX_MENU_VALIDATE);
            VisualControlAdd(m_vpane);
            AddListButton(action_transition + 1 + levelCount, 10 + levelCount, 46 / 2, STRING_IDS.MENU_NEW_GAME_ENDLESS, SFX_MENU_VALIDATE);
            AddArrows(action_up, action_down);
            MB.AddBackButton(this, m_sprite, action_back);
            VisualControls_SetEnableTE(false);
            SubMenus_Initialize();
            m_isInitialized = true;
        }

        public override void Draw()
        {
            if (!m_isInitialized) return;
            VisualControls_Draw();
            ((MenuMain)GetParent()).GetTitle().Draw();
            if (m_pCurrentSubMenu != null) m_pCurrentSubMenu.Draw();
        }

        public override void Update(float dt)
        {
            if (!m_isInitialized) return;
            ScrollArrows();
            base.Update(dt);
            if (Transition_IsGoing()) return;
            if (m_selected != -1)
            {
                m_selected = -1;
                Transition_Start(transition_hide, action_transition);
            }
        }

        public override void VisualControlDoAction(IVisualControl vc, int action_id)
        {
            int levelCount = LevelCount;
            switch (action_id)
            {
                case button_action_lose_focus:
                {
                    int a = vc.GetActionId();
                    if (a >= action_transition + 1 && a < action_transition + 1 + levelCount) vc.GetTouchElement().ReassignTouchTo(m_vpane.GetTouchElement());
                    break;
                }
                case action_back: Transition_Start(transition_hide, action_back_trans); break;
                case action_back_trans: Go("Menu_Info"); break;
                case action_transition: Go("Menu_HighScore"); return;
            }
            for (int i = 0; i < levelCount + 1; i++)
            {
                if (i + action_transition + 1 != action_id) continue;
                m_selected = action_id - (action_transition + 1);
                if (m_selected < levelCount) Menu_HighScore.SetLevel(m_selected);
                else Menu_HighScore.SetEndless();
                break;
            }
        }
    }
}
