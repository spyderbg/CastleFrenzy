// Menus/Menu_Ingame: pause, victory, defeat and score screens shown over the playfield.
using static CastleFrenzy.G;
using static CastleFrenzy.GE;
using static CastleFrenzy.Ids;

namespace CastleFrenzy
{
    public class Menu_Ingame : MenuSimple
    {
        public const int IngameMenuPause = 0, IngameMenuVictory = 1, IngameMenuDefeated = 2, IngameMenuHighScore = 3, IngameMenuNotSpecified = 4;

        const int pause_action_continue = MB.MENU_NEWGAME_ACTION_BASE, pause_action_continue_trans = pause_action_continue + 1,
            pause_action_exit = pause_action_continue + 2, pause_action_sound_options = pause_action_continue + 3,
            pause_action_sound_options_trans = pause_action_continue + 4, pause_action_help_book = pause_action_continue + 5,
            pause_action_help_book_trans = pause_action_continue + 6, pause_action_back = pause_action_continue + 7,
            pause_action_back_trans = pause_action_continue + 8, victory_action_next_level = pause_action_continue + 9,
            victory_action_next_level_trans = pause_action_continue + 10, victory_action_shop = pause_action_continue + 11,
            victory_action_shop_trans = pause_action_continue + 12, victory_action_exit = pause_action_continue + 13,
            defeated_action_retry = pause_action_continue + 14, defeated_action_retry_trans = pause_action_continue + 15,
            defeated_action_exit = pause_action_continue + 16, action_toggle = pause_action_continue + 17,
            action_toggle_trans = pause_action_continue + 18, touched = pause_action_continue + 19;
        const int BASE_CONTROLS_ID = 100;

        readonly CPlayfield m_Playfield;
        bool m_menuShow;
        int m_menuType = IngameMenuNotSpecified;
        VisualControlText m_titleText;
        readonly Menu_Confirmation m_menuConfirmation;
        readonly Menu_Ingame_Help m_menuIngameHelp;
        readonly Menu_Help m_menuHelp;
        readonly Menu_GameSettings m_menuGameSettings;
        readonly Menu_SoundOptions m_menuSoundOptions;
        bool m_exit;
        ASprite m_highScoreIcons;
        float m_time;
        bool m_scoreEndless;
        int m_scoreKills, m_scoreDays, m_scoreGold, m_scoreSouls, m_scoreDifficulty;
        readonly CTouchElement m_touch = new CTouchElement();

        public Menu_Ingame(IMenu parent) : base(parent)
        {
            m_Playfield = CPlayfield.GetInstance();
            m_touch.SetScreenCoordinates(0, 0, 480, 320);
            m_touch.SetEnabled(true);
            m_menuConfirmation = new Menu_Confirmation(this);
            SubMenu_Register("Menu_Confirmation", m_menuConfirmation);
            m_menuIngameHelp = new Menu_Ingame_Help(this);
            SubMenu_Register("Menu_Ingame_Help", m_menuIngameHelp);
            m_menuHelp = new Menu_Help(this);
            SubMenu_Register("Menu_Help", m_menuHelp);
            m_menuGameSettings = new Menu_GameSettings(this);
            SubMenu_Register("Menu_GameSettings", m_menuGameSettings);
            m_menuSoundOptions = new Menu_SoundOptions(this);
            SubMenu_Register("Menu_SoundOptions", m_menuSoundOptions);
        }

        public Menu_Help GetHelpMenu() { return m_menuHelp; }
        public Menu_GameSettings GetGameSettings() { return m_menuGameSettings; }
        public Menu_SoundOptions GetSoundOptions() { return m_menuSoundOptions; }
        public VisualControlText GetTitleTextControl() { return m_titleText; }

        public void CallMenu(int type)
        {
            m_menuType = type;
            m_menuShow = true;
            DoMenuInitializations();
            m_time = 0;
        }

        protected override void LoadResources()
        {
            if (m_sprite == null) { m_sprite = new ASprite(); m_sprite.Load("Menu/menu"); }
            if (m_highScoreIcons == null) { m_highScoreIcons = new ASprite(); m_highScoreIcons.Load("Menu/high_score_icone"); }
        }

        protected override void FreeResources() { m_sprite = null; m_highScoreIcons = null; }

        public override void Initialize()
        {
            LoadResources();
            m_menuConfirmation.Initialize();
            m_menuIngameHelp.Initialize();
            m_menuHelp.Initialize();
            m_menuGameSettings.SetIsMenuIngame(true);
            m_menuGameSettings.Initialize();
            m_menuSoundOptions.Initialize();
            m_titleText = new VisualControlText(BASE_CONTROLS_ID + 5);
            m_titleText.SetText(STRING_IDS.MENU_INGAME_PAUSE_TITLE);
            m_titleText.SetPos(new p2i(GAME_SCREEN_W2, 40));
            m_titleText.SetFont("MM_Normal");
            m_titleText.SetAlign(CFont.HVCENTER);
            VisualControls_SetEnableTE(true);
            m_isInitialized = true;
        }

        public override void Destroy()
        {
            if (m_music_id != -1) __MGR_SOUNDS().stop(m_music_id);
            VisualControls_Free();
            if (m_titleText != null) { m_titleText.Dispose(); m_titleText = null; }
            m_isInitialized = false;
            SubMenus_Destroy();
            FreeResources();
            m_touch.Dispose();
        }

        public override void Draw()
        {
            if (!m_isInitialized || !m_menuShow) return;
            DrawBackground();
            if (m_menuType == IngameMenuHighScore) DrawHighScore();
            VisualControls_Draw();
            if (m_pCurrentSubMenu != null) m_pCurrentSubMenu.Draw();
        }

        public override void GainFocus()
        {
            if (!m_isInitialized || m_isOnFocus) return;
            VisualControls_SetEnableTE(true);
            m_isOnFocus = true;
            if (m_pCurrentSubMenu != null) m_pCurrentSubMenu.LoseFocus();
            Transition_Start(transition_show);
        }

        public void SetEndlessScores(int kills, int days, int gold, int souls)
        {
            m_scoreKills = kills;
            m_scoreDays = days;
            m_scoreGold = gold;
            m_scoreSouls = souls;
            m_scoreEndless = true;
            Scores.NewEndlessScore(__PROFILE(), new EndlessScore { kills = kills, days = days, gold = gold, souls = souls });
        }

        public void SetLevelScores(int gold, int souls, int difficulty)
        {
            m_scoreGold = gold;
            m_scoreSouls = souls;
            m_scoreDifficulty = difficulty;
            m_scoreEndless = false;
            Scores.NewCampaignScore(__PROFILE(), new CampaignScore { gold = gold, souls = souls, difficulty = difficulty }, CPlayfield.GetInstance().m_CurrentLevel);
        }

        public override void LoseFocus()
        {
            if (!m_isInitialized) return;
            VisualControls_SetEnableTE(false);
            m_isOnFocus = false;
            Transition_Start(transition_show);
        }

        public override void Update(float dt)
        {
            if (!m_menuShow) return;
            m_time += dt;
            base.Update(dt);
            if (m_menuConfirmation.IsOnFocus() && m_menuConfirmation.GetDialogResult() != Menu_Confirmation.RESULT_NONE)
            {
                int result = m_menuConfirmation.GetDialogResult();
                SubMenu_Remove();
                if (result == Menu_Confirmation.RESULT_YES) m_exit = true;
            }
            if ((m_menuSoundOptions.IsOnFocus() && m_menuSoundOptions.isBack()) || (m_menuIngameHelp.IsOnFocus() && m_menuIngameHelp.isBack()))
            {
                SubMenu_Remove();
                switch (m_menuType)
                {
                    case IngameMenuPause: GetTitleTextControl().SetText(STRING_IDS.MENU_INGAME_PAUSE_TITLE); break;
                    case IngameMenuVictory: GetTitleTextControl().SetText(STRING_IDS.MENU_INGAME_VICTORY_TITLE); break;
                    case IngameMenuDefeated: GetTitleTextControl().SetText(STRING_IDS.MENU_INGAME_DEFEATED_TITLE); break;
                }
            }
            UpdateCheats();
            bool goToHighScore = false, goToSelectLevel = false;
            if (m_menuType == IngameMenuHighScore && m_time > 1.0f && m_touch.IsActive())
            {
                m_exit = true;
                goToHighScore = CPlayfield.GetInstance().m_isEndless && Scores.LastScoreWasHighScore();
                goToSelectLevel = !CPlayfield.GetInstance().m_isEndless;
            }
            if (m_exit)
            {
                m_exit = false;
                CAchievements.GetInstance().saveAchievementsByProfile("Player1");
                GS_Menu menu = new GS_Menu();
                if (goToHighScore) menu.GoToHighScore();
                else if (goToSelectLevel) menu.GoToSelectLevel();
                __MGR_STATES().switchState(menu);
            }
        }

        void DrawBackground()
        {
            int left_x = 76, right_x = 405, left_y = 55, right_y = 280;
            int titleFrame = MENU_F_MENU_INGAME_TITLE;
            if (m_menuType == IngameMenuHighScore)
            {
                left_x -= 43;
                right_x += 43;
                titleFrame = MENU_F_MENU_INGAME_TITLE_SCORE;
            }
            Gfx.draw2DRectangle(new SColor(90, 0, 0, 0), new r2i(0, 0, GAME_SCREEN_W, GAME_SCREEN_H));
            if (!m_menuHelp.IsOnFocus()) Gfx.draw2DRectangle(new SColor(150, 0, 0, 0), new r2i(left_x, left_y, right_x, right_y));
            m_sprite.PaintFrame(titleFrame, GAME_SCREEN_W2, 40);
            if (!m_menuHelp.IsOnFocus())
            {
                m_sprite.PaintFrame(MENU_F_MENU_BOTTOM_RIGHT_ARROW, right_x, right_y);
                m_sprite.PaintFrame(MENU_F_MENU_BOTTOM_LEFT_ARROW, left_x, right_y);
            }
            m_titleText.Draw();
        }

        static float Pulse(float t, float maxT, int pulses) { return 0.5f - 0.5f * cosf(t * 2.0f * PI * pulses / maxT); }
        static float CLAMPED(float v, float lo, float hi) { return CLAMP(v, lo, hi); }

        void DrawHighScore()
        {
            int total = 0;
            r2i backgroundRect = new r2i(76 - 40, 55, 405 + 40, 280);
            int x = backgroundRect.UpperLeftCorner.X;
            int y = backgroundRect.UpperLeftCorner.Y + 30;
            float endTime;
            if (m_scoreEndless)
            {
                SingleScore[] scores =
                {
                    new SingleScore(MENU_F_HIGH_SCORE_ICONE_KILLS, _TXT(STRING_IDS.MENU_SCORE_KILLS), (int)(CLAMPED((m_time - 100) / 500, 0, 1) * m_scoreKills), Scores.k_multiplier_kills),
                    new SingleScore(MENU_F_HIGH_SCORE_ICONE_DAYS, _TXT(STRING_IDS.MENU_SCORE_DAYS), (int)(CLAMPED((m_time - 600) / 500, 0, 1) * m_scoreDays), Scores.k_multiplier_days),
                    new SingleScore(MENU_F_HIGH_SCORE_ICONE_GOLD, _TXT(STRING_IDS.MENU_SCORE_GOLD), (int)(CLAMPED((m_time - 1100) / 500, 0, 1) * m_scoreGold), Scores.k_multiplier_gold),
                    new SingleScore(MENU_F_HIGH_SCORE_ICONE_SOULS, _TXT(STRING_IDS.MENU_SCORE_SOULS), (int)(CLAMPED((m_time - 1600) / 500, 0, 1) * m_scoreSouls), Scores.k_multiplier_souls),
                };
                endTime = 2000.0f;
                ScoreDraw.DrawScores(m_highScoreIcons, x, ref y, backgroundRect, ref total, scores, 255);
            }
            else
            {
                int[] difficulty_icons = { MENU_F_HIGH_SCORE_ICONE_EASY, MENU_F_HIGH_SCORE_ICONE_MEDIUM, MENU_F_HIGH_SCORE_ICONE_HARD };
                string[] difficulty_texts = { _TXT(STRING_IDS.MENU_SCORE_EASY), _TXT(STRING_IDS.MENU_SCORE_MEDIUM), _TXT(STRING_IDS.MENU_SCORE_HARD) };
                int d = CLAMP(m_scoreDifficulty, 0, 2);
                string str = FormatString("%s: %s", _TXT(STRING_IDS.MENU_SELECT_DIFFICULTY_2), difficulty_texts[d]);
                SingleScore[] scores =
                {
                    new SingleScore(MENU_F_HIGH_SCORE_ICONE_GOLD, _TXT(STRING_IDS.MENU_SCORE_GOLD), (int)(CLAMPED((m_time - 100) / 500, 0, 1) * m_scoreGold), Scores.k_multiplier_gold),
                    new SingleScore(MENU_F_HIGH_SCORE_ICONE_SOULS, _TXT(STRING_IDS.MENU_SCORE_SOULS), (int)(CLAMPED((m_time - 600) / 500, 0, 1) * m_scoreSouls), Scores.k_multiplier_souls),
                    new SingleScore(difficulty_icons[d], str, (int)(CLAMPED((m_time - 1100) / 500, 0, 1) * Scores.k_difficulty_bonus[d]), 0),
                };
                endTime = 1700.0f;
                ScoreDraw.DrawScores(m_highScoreIcons, x, ref y, backgroundRect, ref total, scores, 255);
            }
            const float maxTime = 4000.0f;
            CFont font = __FONT("TotalHighScore");
            if (Scores.LastScoreWasHighScore() && endTime < m_time && m_time < endTime + maxTime)
            {
                float s1 = 1.0f + 0.2f * Pulse(m_time - endTime, maxTime, 8);
                float s2 = Pulse(m_time - endTime, maxTime, 4);
                d2f sz = font.GetStringSize(FormatNumber(total));
                ASprite.PushTransform(new v3f(x + backgroundRect.getWidth() - 20 - sz.Width / 2.0f, y + 7, 0.0f), new v3f(1, 1, 0) * s1, v3f.Zero);
                SColor color1 = font.GetColor1();
                font.SetColor1(color1.getInterpolated(new SColor(0xffff7070), 1.0f - s2));
                font.DrawString(FormatNumber(total), x + backgroundRect.getWidth() - 20, y + 7, CFont.VCENTER | CFont.RIGHT);
                font.SetColor1(color1);
                ASprite.PopTransform();
            }
            else font.DrawString(FormatNumber(total), x + backgroundRect.getWidth() - 20, y + 7, CFont.VCENTER | CFont.RIGHT);
            font.DrawString(_TXT(STRING_IDS.INGAME_TOTAL), x + 20, y + 7, CFont.VCENTER | CFont.LEFT);
            int alpha = CLAMP((int)(196 + 196 * sinf(-PI / 2 + 2 * PI * m_time / 1000.0f)), 0, 255);
            __FONT("Arial").DrawString(_TXT(STRING_IDS.HELP_LOAD_CONTINUE), 240, 310, CFont.HCENTER | CFont.BOTTOM, alpha);
        }

        VisualControlButton AddButton(int action, int id, int y, int textId)
        {
            VisualControlButton b = new VisualControlButton(action, id);
            b.SetAnimations(m_sprite, MENU_A_MENU_BASE_BUTTON_NORMAL, MENU_A_MENU_BASE_BUTTON_PRESSED, MENU_A_MENU_BASE_BUTTON_BLINK);
            b.SetArea(GAME_SCREEN_W2, y, m_sprite.GetFrameWidth(MENU_F_MENU_BASE_BUTTON), m_sprite.GetFrameHeight(MENU_F_MENU_BASE_BUTTON));
            b.SetText1(textId, "MM_Normal", "MM_Pressed");
            VisualControlAdd(b);
            return b;
        }

        void CreatePauseMenu()
        {
            SetMenuName(STRING_IDS.MENU_INGAME_PAUSE_TITLE);
            AddButton(pause_action_continue, BASE_CONTROLS_ID, GAME_SCREEN_H2 - 60 - 13, STRING_IDS.MENU_INGAME_CONTINUE);
            AddButton(pause_action_sound_options, BASE_CONTROLS_ID + 1, GAME_SCREEN_H2 - 24, STRING_IDS.MENU_OPTIONS_SOUND);
            AddButton(pause_action_help_book, BASE_CONTROLS_ID + 2, GAME_SCREEN_H2 + 28, STRING_IDS.MENU_INFO_HELP_BOOK);
            AddButton(pause_action_exit, BASE_CONTROLS_ID + 3, GAME_SCREEN_H2 + 60 + 18, STRING_IDS.MENU_INGAME_EXIT);
            VisualControlButton back = new VisualControlButton(pause_action_back, BASE_CONTROLS_ID + 4);
            back.SetAnimations(m_sprite, MENU_A_MENU_PAUSE_PLAY, MENU_A_MENU_PLAY, MENU_A_MENU_PAUSE_PLAY);
            back.SetArea(20, 300, 50, 50);
            VisualControlAdd(back);
            m_titleText.SetText(STRING_IDS.MENU_INGAME_PAUSE_TITLE);
        }

        void CreateVictoryMenu()
        {
            bool isLevelOver = CPlayfield.GetInstance().IsLevelOver();
            SetMenuName(STRING_IDS.MENU_INGAME_VICTORY_TITLE);
            if (!isLevelOver)
            {
                AddButton(victory_action_next_level, BASE_CONTROLS_ID + 6, GAME_SCREEN_H2 - 60, STRING_IDS.MENU_INGAME_CONTINUE);
                AddButton(victory_action_shop, BASE_CONTROLS_ID + 7, GAME_SCREEN_H2, STRING_IDS.MENU_INGAME_SHOP);
            }
            VisualControlButton exit = AddButton(victory_action_exit, BASE_CONTROLS_ID + 8, GAME_SCREEN_H2 + 60, STRING_IDS.MENU_INGAME_EXIT);
            if (isLevelOver) exit.MoveArea(0, -60);
            m_titleText.SetText(STRING_IDS.MENU_INGAME_VICTORY_TITLE);
        }

        void CreateDefeatedMenu()
        {
            SetMenuName(STRING_IDS.MENU_INGAME_DEFEATED_TITLE);
            bool endless = CPlayfield.GetInstance().m_isEndless;
            if (!endless) AddButton(defeated_action_retry, BASE_CONTROLS_ID + 9, GAME_SCREEN_H2 - 60, STRING_IDS.MENU_INGAME_RETRY);
            AddButton(defeated_action_exit, BASE_CONTROLS_ID + 10, GAME_SCREEN_H2 + 60, STRING_IDS.MENU_INGAME_EXIT);
            int offset = 10;
            if (!endless)
            {
                IVisualControl retry = VisualControlGetById(BASE_CONTROLS_ID + 9);
                retry.SetEnabled(true);
                retry.SetEnabledTE(true);
                retry.SetVisible(true);
                retry.MoveArea(0, offset);
            }
            else offset += 60;
            IVisualControl ex = VisualControlGetById(BASE_CONTROLS_ID + 10);
            ex.SetEnabled(true);
            ex.SetEnabledTE(true);
            ex.SetVisible(true);
            ex.MoveArea(0, -offset);
            m_titleText.SetText(STRING_IDS.MENU_INGAME_DEFEATED_TITLE);
        }

        void CreateHighScoreMenu()
        {
            SetMenuName(STRING_IDS.INGAME_SCORE);
            m_titleText.SetText(Scores.LastScoreWasHighScore() ? STRING_IDS.INGAME_HIGH_SCORE : STRING_IDS.INGAME_SCORE);
        }

        public override void VisualControlDoAction(IVisualControl vc, int action_id)
        {
            switch (m_menuType)
            {
                case IngameMenuPause: DoActionPause(action_id); break;
                case IngameMenuVictory: DoActionVictory(action_id); break;
                case IngameMenuDefeated: DoActionDefeated(action_id); break;
                case IngameMenuHighScore: if (action_id == touched) m_exit = true; break;
            }
        }

        void DoActionPause(int action_id)
        {
            switch (action_id)
            {
                case pause_action_continue: Transition_Start(transition_hide, pause_action_continue_trans); break;
                case pause_action_continue_trans:
                case pause_action_back_trans:
                    ReturnControlToGame();
                    m_Playfield.SetPlayState(CPlayfield.PS_ExitIGM);
                    break;
                case pause_action_sound_options: Transition_Start(transition_hide, pause_action_sound_options_trans); break;
                case pause_action_sound_options_trans:
                    GetSoundOptions().SetIsMenuIngame(true);
                    SubMenu_Change("Menu_SoundOptions");
                    break;
                case pause_action_help_book: Transition_Start(transition_hide, pause_action_help_book_trans); break;
                case pause_action_help_book_trans:
                    GetSoundOptions().SetIsMenuIngame(true);
                    SubMenu_Change("Menu_Ingame_Help");
                    break;
                case pause_action_exit:
                    m_menuConfirmation.SetConfirmationText(STRING_IDS.MENU_INGAME_EXIT_CONFIRM);
                    SubMenu_Change("Menu_Confirmation");
                    break;
                case action_toggle: Transition_Start(transition_hide, action_toggle_trans); break;
                case pause_action_back: Transition_Start(transition_hide, pause_action_back_trans); break;
            }
        }

        void DoActionVictory(int action_id)
        {
            switch (action_id)
            {
                case victory_action_next_level: Transition_Start(transition_hide, victory_action_next_level_trans); break;
                case victory_action_next_level_trans:
                    CPlayfield.GetInstance().ResumeMusic();
                    CPlayfield.GetInstance().SetPlayState(CPlayfield.PS_Restart);
                    LoseFocus();
                    CPlayfield.GetInstance().EnableIngameTouchInput();
                    break;
                case victory_action_shop: Transition_Start(transition_hide, victory_action_shop_trans); break;
                case victory_action_shop_trans:
                    CPlayfield.GetInstance().EnableIngameTouchInput();
                    __MGR_STATES().switchState(new GS_Shop());
                    break;
                case victory_action_exit:
                    if (!CPlayfield.GetInstance().IsLevelOver())
                    {
                        m_menuConfirmation.SetConfirmationText(STRING_IDS.MENU_INGAME_EXIT_CONFIRM);
                        SubMenu_Change("Menu_Confirmation");
                    }
                    else m_exit = true;
                    break;
            }
        }

        void DoActionDefeated(int action_id)
        {
            switch (action_id)
            {
                case defeated_action_retry: Transition_Start(transition_hide, defeated_action_retry_trans); break;
                case defeated_action_retry_trans:
                    ReturnControlToGame();
                    m_Playfield.SetPlayState(CPlayfield.PS_Restart);
                    break;
                case defeated_action_exit:
                    m_menuConfirmation.SetConfirmationText(STRING_IDS.MENU_INGAME_EXIT_CONFIRM);
                    SubMenu_Change("Menu_Confirmation");
                    break;
            }
        }

        void DoMenuInitializations()
        {
            CPlayfield.GetInstance().DisableIngameTouchInput();
            GainFocus();
            MakeControls();
        }

        void MakeControls()
        {
            VisualControls_Free();
            switch (m_menuType)
            {
                case IngameMenuPause: CreatePauseMenu(); break;
                case IngameMenuVictory: CreateVictoryMenu(); break;
                case IngameMenuDefeated: CreateDefeatedMenu(); break;
                case IngameMenuHighScore: CreateHighScoreMenu(); break;
            }
        }

        void ReturnControlToGame()
        {
            LoseFocus();
            CPlayfield.GetInstance().EnableIngameTouchInput();
            m_menuType = IngameMenuNotSpecified;
            VisualControls_Free();
            m_menuShow = false;
        }

        public override bool IsOnFocus() { return m_isOnFocus || m_menuConfirmation.IsOnFocus(); }

        void UpdateCheats()
        {
            if (!m_touch.IsNowActivated()) return;
            int x, y;
            m_touch.GetTouchPos_Current(out x, out y);
            CCheats.GetInstance().Update(x < 240 ? 1 : 2);
            if (CCheats.GetInstance().IsVisible()) __MGR_STATES().pushState(new GS_Cheat());
        }
    }
}
