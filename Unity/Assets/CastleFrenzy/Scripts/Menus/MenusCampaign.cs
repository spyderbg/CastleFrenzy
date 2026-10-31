// Menus/Menu_Campaign (level select pane) and Menus/Menu_Info.
using static CastleFrenzy.G;
using static CastleFrenzy.GE;
using static CastleFrenzy.Ids;

namespace CastleFrenzy
{
    public class Menu_Campaign : MenuSimple
    {
        const int action_back = MB.MENU_CAMPAIGN_ACTION_BASE, action_back_trans = action_back + 1, action_vpane = action_back + 2,
            action_up = action_back + 3, action_down = action_back + 4, action_transition = action_back + 5;

        readonly Menu_Confirmation m_menuConfirmation;
        int m_selected = -1;
        VisualControlPane m_vpane;
        VisualControlButton m_arrowUp, m_arrowDown;

        public Menu_Campaign(IMenu parent) : base(parent)
        {
            m_menuConfirmation = new Menu_Confirmation(this);
            SubMenu_Register("Menu_Confirmation", m_menuConfirmation);
        }

        static int LevelCount { get { return LevelInfo.getNumberOfDaysPerLevel().Count - 1; } }

        public override void GainFocus()
        {
            base.GainFocus();
            CPlayfield pf = CPlayfield.GetInstance();
            for (int i = 0; i < LevelCount; i++)
                VisualControlGetByActionId(action_transition + 1 + i).SetEnabled(i <= pf.getUnlockLevel(pf.getDifficulty()) || CGameConfig.UnlockAllLevels);
            m_vpane.ReturnToStartPos();
            m_selected = -1;
        }

        public override void Initialize()
        {
            LoadResources();
            SetMenuName(STRING_IDS.MENU_CAMPAIGN_TITLE);
            CreateLevelButtons();
            MB.AddBackButton(this, m_sprite, action_back);
            VisualControls_SetEnableTE(false);
            SubMenus_Initialize();
            m_isInitialized = true;
        }

        void CreateLevelButtons()
        {
            int levelCount = LevelCount;
            int bw = m_sprite.GetFrameWidth(MENU_F_MENU_BASE_BUTTON), bh = m_sprite.GetFrameHeight(MENU_F_MENU_BASE_BUTTON);
            m_vpane = new VisualControlPane(action_vpane);
            m_vpane.SetArea(new r2i(70, 40, GAME_SCREEN_W - 70, GAME_SCREEN_H));
            m_vpane.SetScrollSize(new d2i(bw, 46 * levelCount));
            m_vpane.SetRows(levelCount);
            for (int i = 0; i < levelCount; i++)
            {
                VisualControlButton b = new VisualControlButton(action_transition + 1 + i, 10 + i, SFX_MENU_VALIDATE);
                b.SetAnimations(m_sprite, MENU_A_MENU_BASE_BUTTON_NORMAL, MENU_A_MENU_BASE_BUTTON_PRESSED, MENU_A_MENU_BASE_BUTTON_BLINK);
                b.SetArea(GAME_SCREEN_W2 - 70, 46 / 2 + i * 46, bw, bh);
                b.SetText1(STRING_IDS.MENU_CAMPAIGN_LABEL_1 + i, "MM_Normal", "MM_Pressed");
                m_vpane.AddControl(b);
                VisualControlAdd(b);
            }
            VisualControlAdd(m_vpane);
            m_arrowUp = new VisualControlButton(action_up, -1, MB.k_back_button_sfx);
            m_arrowUp.SetAnimations(m_sprite, MENU_A_MENU_ARROW_UP_NORMAL, MENU_A_MENU_ARROW_UP_PRESSED, MENU_A_MENU_ARROW_UP_BLINK, MENU_A_MENU_ARROW_UP_NORMAL);
            m_arrowUp.SetArea(428, 85, m_sprite.GetFrameWidth(MENU_F_MENU_BIG_ARROW_UP), m_sprite.GetFrameHeight(MENU_F_MENU_BIG_ARROW_UP));
            VisualControlAdd(m_arrowUp);
            m_arrowDown = new VisualControlButton(action_down, -1, MB.k_back_button_sfx);
            m_arrowDown.SetAnimations(m_sprite, MENU_A_MENU_ARROW_DOWN_NORMAL, MENU_A_MENU_ARROW_DOWN_PRESSED, MENU_A_MENU_ARROW_DOWN_BLINK, MENU_A_MENU_ARROW_DOWN_NORMAL);
            m_arrowDown.SetArea(428, 270, m_sprite.GetFrameWidth(MENU_F_MENU_BIG_ARROW_DOWN), m_sprite.GetFrameHeight(MENU_F_MENU_BIG_ARROW_DOWN));
            VisualControlAdd(m_arrowDown);
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
            if (m_arrowUp.IsPressed()) m_vpane.ScrollVertical(-1);
            if (m_arrowDown.IsPressed()) m_vpane.ScrollVertical(1);
            base.Update(dt);
            if (Transition_IsGoing()) return;
            int select = -1;
            if (m_selected != -1)
            {
                if (m_menuConfirmation.IsOnFocus())
                {
                    if (m_menuConfirmation.GetDialogResult() != Menu_Confirmation.RESULT_NONE)
                    {
                        m_menuConfirmation.SetBackGroundFrame(MENU_F_MENU_CONFIRMATION_BKG);
                        int result = m_menuConfirmation.GetDialogResult();
                        SubMenu_Remove();
                        if (result == Menu_Confirmation.RESULT_YES) select = m_selected;
                        m_selected = -1;
                    }
                }
                else select = m_selected;
            }
            if (select != -1)
            {
                m_selected = -1;
                CPlayfield.GetInstance().m_CurrentLevel = select;
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
                    int actionId = vc.GetActionId();
                    if (actionId >= action_transition + 1 && actionId < action_transition + 1 + levelCount)
                        vc.GetTouchElement().ReassignTouchTo(m_vpane.GetTouchElement());
                    break;
                }
                case action_back: Transition_Start(transition_hide, action_back_trans); break;
                case action_back_trans: Go("Menu_NewGame"); break;
                case action_transition:
                    CPlayfield.GetInstance().StartNewLevel(CPlayfield.GetInstance().m_CurrentLevel);
                    return;
            }
            for (int i = 0; i < levelCount; i++)
            {
                if (i + action_transition + 1 != action_id) continue;
                if (CPlayfield.GetInstance().m_CurrentDay > 0)
                {
                    m_menuConfirmation.SetConfirmationText(STRING_IDS.MENU_NEW_GAME_WARNING);
                    m_menuConfirmation.SetBackGroundFrame(MENU_F_MENU_DIFF_CONFIRMATION_BKG);
                    SubMenu_Change("Menu_Confirmation");
                }
                m_selected = action_id - (action_transition + 1);
                break;
            }
        }
    }

    public class Menu_Info : MenuSimple
    {
        const int action_enemies = MB.MENU_INFO_ACTION_BASE, action_enemies_trans = action_enemies + 1, action_spells = action_enemies + 2,
            action_spells_trans = action_enemies + 3, action_credits = action_enemies + 4, action_credits_trans = action_enemies + 5,
            action_scores = action_enemies + 6, action_scores_trans = action_enemies + 7, action_achievements = action_enemies + 8,
            action_achievements_trans = action_enemies + 9, action_back = action_enemies + 10, action_back_trans = action_enemies + 11;

        public Menu_Info(IMenu parent) : base(parent) { }

        public override void Initialize()
        {
            LoadResources();
            SetMenuName(STRING_IDS.MENU_INFO_TITLE);
            MB.AddBaseButton(this, m_sprite, action_enemies, GAME_SCREEN_W2, 58, STRING_IDS.MENU_INFO_HELP_ENEMIES);
            MB.AddBaseButton(this, m_sprite, action_spells, GAME_SCREEN_W2, 58 + 47, STRING_IDS.MENU_INFO_HELP_SPELLS);
            MB.AddBaseButton(this, m_sprite, action_achievements, GAME_SCREEN_W2, 58 + 2 * 47, STRING_IDS.MENU_INFO_ACHIEVEMENTS);
            MB.AddBaseButton(this, m_sprite, action_credits, GAME_SCREEN_W2, 58 + 3 * 47, STRING_IDS.MENU_INFO_CREDITS);
            MB.AddBaseButton(this, m_sprite, action_scores, GAME_SCREEN_W2, 58 + 4 * 47, STRING_IDS.MENU_HIGH_SCORE_BUTTON);
            MB.AddBackButton(this, m_sprite, action_back);
            VisualControls_SetEnableTE(false);
            m_isInitialized = true;
        }

        public override void VisualControlDoAction(IVisualControl vc, int action_id)
        {
            MenuMain main = GetParent() as MenuMain;
            switch (action_id)
            {
                case action_enemies: Transition_Start(transition_hide, action_enemies_trans); break;
                case action_enemies_trans:
                    main.GetHelpMenu().SetHelpBookType(Menu_Help.MenuHelp_EnemiesBook);
                    main.GetHelpMenu().SetIsMenuIngame(false);
                    Go("Menu_Help");
                    break;
                case action_spells: Transition_Start(transition_hide, action_spells_trans); break;
                case action_spells_trans:
                    main.GetHelpMenu().SetHelpBookType(Menu_Help.MenuHelp_SpellsBook);
                    main.GetHelpMenu().SetIsMenuIngame(false);
                    Go("Menu_Help");
                    break;
                case action_credits: Transition_Start(transition_hide, action_credits_trans); break;
                case action_credits_trans: Go("Menu_About"); break;
                case action_scores: Transition_Start(transition_hide, action_scores_trans); break;
                case action_scores_trans: Go("Menu_HighScoresLevelList"); break;
                case action_achievements: Transition_Start(transition_hide, action_achievements_trans); break;
                case action_achievements_trans: Go("Menu_Achievements"); break;
                case action_back: Transition_Start(transition_hide, action_back_trans); break;
                case action_back_trans: Go("Menu_Engage"); break;
            }
        }
    }
}
