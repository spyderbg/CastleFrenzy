// Menus/Menu_NewGame, Menu_Options, Menu_Difficulty, Menu_Confirmation, Menu_SoundOptions and Menu_GameSettings.
using static CastleFrenzy.G;
using static CastleFrenzy.GE;
using static CastleFrenzy.Ids;

namespace CastleFrenzy
{
    /// <summary>The Destroy/Draw/Update boilerplate shared by most sub-menus.</summary>
    public abstract class MenuSimple : IMenu
    {
        protected MenuSimple(IMenu parent) : base(parent) { }

        public override void Destroy()
        {
            if (m_music_id != -1) __MGR_SOUNDS().stop(m_music_id);
            VisualControls_Free();
            m_isInitialized = false;
            SubMenus_Destroy();
            FreeResources();
        }

        public override void Draw()
        {
            if (!m_isInitialized) return;
            VisualControls_Draw();
        }

        public override void Update(float dt)
        {
            if (!m_isInitialized) return;
            base.Update(dt);
        }

        protected void Go(string submenu) { GetParent().SubMenu_Change(submenu); }

        /// <summary>The bordered dark panel used by the options menus.</summary>
        protected void DrawPanel(int x1, int y1, int x2, int y2, int verticalFrame = MENU_F_MENU_VERTICAL_LINE_MAIN_MENU)
        {
            Gfx.draw2DRectangle(new SColor(200, 0, 0, 0), new r2i(x1, y1, x2, y2));
            m_sprite.PaintFrame(MENU_F_MENU_HORIZONTAL_LINE, (x1 + x2) / 2, y1);
            m_sprite.PaintFrame(MENU_F_MENU_HORIZONTAL_LINE, (x1 + x2) / 2, y2);
            m_sprite.PaintFrame(verticalFrame, x1, (y1 + y2) / 2);
            m_sprite.PaintFrame(verticalFrame, x2, (y1 + y2) / 2);
            m_sprite.PaintFrame(MENU_F_MENU_UPPER_LEFT_ARROW, x1, y1);
            m_sprite.PaintFrame(MENU_F_MENU_UPPER_RIGHT_ARROW, x2, y1);
            m_sprite.PaintFrame(MENU_F_MENU_BOTTOM_RIGHT_ARROW, x2, y2);
            m_sprite.PaintFrame(MENU_F_MENU_BOTTOM_LEFT_ARROW, x1, y2);
        }
    }

    public class Menu_NewGame : MenuSimple
    {
        const int action_newgame = MB.MENU_NEWGAME_ACTION_BASE, action_newgame_trans = action_newgame + 1, action_endless = action_newgame + 2,
            action_endless_trans = action_newgame + 3, action_back = action_newgame + 4, action_back_trans = action_newgame + 5;

        public Menu_NewGame(IMenu parent) : base(parent) { }

        public override void Initialize()
        {
            LoadResources();
            SetMenuName(STRING_IDS.MENU_NEW_GAME_TITLE);
            MB.AddBaseButton(this, m_sprite, action_newgame, GAME_SCREEN_W2, GAME_SCREEN_H2 - 40, STRING_IDS.MENU_NEW_GAME_CAMPAIGN);
            MB.AddBaseButton(this, m_sprite, action_endless, GAME_SCREEN_W2, GAME_SCREEN_H2 + 40, STRING_IDS.MENU_NEW_GAME_ENDLESS);
            MB.AddBackButton(this, m_sprite, action_back);
            VisualControls_SetEnableTE(false);
            m_isInitialized = true;
        }

        public override void VisualControlDoAction(IVisualControl vc, int action_id)
        {
            switch (action_id)
            {
                case action_back: Transition_Start(transition_hide, action_back_trans); break;
                case action_back_trans: Go("Menu_Engage"); break;
                case action_newgame: Transition_Start(transition_hide, action_newgame_trans); break;
                case action_newgame_trans:
                    CPlayfield.GetInstance().m_isEndless = false;
                    Go("Menu_Difficulty");
                    break;
                case action_endless: Transition_Start(transition_hide, action_endless_trans); break;
                case action_endless_trans:
                    CPlayfield.GetInstance().m_isEndless = true;
                    CPlayfield.GetInstance().StartNewLevel(LevelInfo.getNumberOfDaysPerLevel().Count - 1);
                    break;
            }
        }
    }

    public class Menu_Options : MenuSimple
    {
        const int action_game_settings = MB.MENU_OPTIONS_ACTION_BASE, action_game_settings_trans = action_game_settings + 1,
            action_sound_options = action_game_settings + 2, action_sound_options_trans = action_game_settings + 3,
            action_back = action_game_settings + 4, action_back_trans = action_game_settings + 5;

        public Menu_Options(IMenu parent) : base(parent) { }

        public override void Initialize()
        {
            LoadResources();
            SetMenuName(STRING_IDS.MENU_OPTIONS_TITLE);
            MB.AddBaseButton(this, m_sprite, action_sound_options, GAME_SCREEN_W2, GAME_SCREEN_H2 - 35, STRING_IDS.MENU_OPTIONS_SOUND);
            MB.AddBaseButton(this, m_sprite, action_game_settings, GAME_SCREEN_W2, GAME_SCREEN_H2 + 35, STRING_IDS.MENU_OPTIONS_GAME);
            MB.AddBackButton(this, m_sprite, action_back);
            VisualControls_SetEnableTE(false);
            m_isInitialized = true;
        }

        public override void VisualControlDoAction(IVisualControl vc, int action_id)
        {
            switch (action_id)
            {
                case action_game_settings: Transition_Start(transition_hide, action_game_settings_trans); break;
                case action_game_settings_trans: Go("Menu_GameSettings"); break;
                case action_sound_options: Transition_Start(transition_hide, action_sound_options_trans); break;
                case action_sound_options_trans: Go("Menu_SoundOptions"); break;
                case action_back: Transition_Start(transition_hide, action_back_trans); break;
                case action_back_trans: Go("Menu_Engage"); break;
            }
        }
    }

    public class Menu_Difficulty : MenuSimple
    {
        const int action_easy = MB.MENU_DIFFICULTY_ACTION_BASE, action_easy_trans = action_easy + 1, action_normal = action_easy + 2,
            action_normal_trans = action_easy + 3, action_hard = action_easy + 4, action_hard_trans = action_easy + 5,
            action_back = action_easy + 6, action_back_trans = action_easy + 7;

        public Menu_Difficulty(IMenu parent) : base(parent) { }

        public override void Initialize()
        {
            LoadResources();
            SetMenuName(STRING_IDS.MENU_CAMPAIGN_TITLE);
            MB.AddBaseButton(this, m_sprite, action_easy, GAME_SCREEN_W2, GAME_SCREEN_H2 - 65, STRING_IDS.MENU_DIFFICULTY_EASY);
            MB.AddBaseButton(this, m_sprite, action_normal, GAME_SCREEN_W2, GAME_SCREEN_H2, STRING_IDS.MENU_DIFFICULTY_NORMAL);
            MB.AddBaseButton(this, m_sprite, action_hard, GAME_SCREEN_W2, GAME_SCREEN_H2 + 65, STRING_IDS.MENU_DIFFICULTY_HARD);
            MB.AddBackButton(this, m_sprite, action_back);
            VisualControls_SetEnableTE(false);
            m_isInitialized = true;
        }

        public override void VisualControlDoAction(IVisualControl vc, int action_id)
        {
            switch (action_id)
            {
                case action_easy: Transition_Start(transition_hide, action_easy_trans); CPlayfield.GetInstance().setDifficulty(GD_Easy); break;
                case action_normal: Transition_Start(transition_hide, action_normal_trans); CPlayfield.GetInstance().setDifficulty(GD_Normal); break;
                case action_hard: Transition_Start(transition_hide, action_hard_trans); CPlayfield.GetInstance().setDifficulty(GD_Hard); break;
                case action_easy_trans:
                case action_normal_trans:
                case action_hard_trans:
                    if (CPlayfield.GetInstance().m_isEndless) CPlayfield.GetInstance().StartNewLevel(LevelInfo.getNumberOfDaysPerLevel().Count - 1);
                    else Go("Menu_Campaign");
                    break;
                case action_back: Transition_Start(transition_hide, action_back_trans); break;
                case action_back_trans: Go("Menu_NewGame"); break;
            }
        }
    }

    public class Menu_Confirmation : MenuSimple
    {
        public const int RESULT_NONE = 0, RESULT_YES = 1, RESULT_NO = 2;
        const int action_yes = MB.MENU_CONFIRMATION_ACTION_BASE, action_no = action_yes + 1;

        int m_bgrFrame;
        int m_textID;
        VisualControlText m_text;
        int m_result = RESULT_NONE;

        public Menu_Confirmation(IMenu parent) : base(parent) { SetBackGroundFrame(MENU_F_MENU_CONFIRMATION_BKG); }

        public void SetConfirmationText(int id) { m_textID = id; UpdateTextFrame(); }
        public int GetDialogResult() { return m_result; }

        public override void Initialize()
        {
            LoadResources();
            SetMenuName(STRING_IDS.MENU_OPTIONS_INFO);
            m_text = new VisualControlText();
            m_text.SetPos(new p2i(GAME_SCREEN_W2, GAME_SCREEN_H2 - 25));
            m_text.SetFont("MS_Text");
            m_text.SetAlign(CFont.HVCENTER);
            m_text.SetRegion(360, 200);
            VisualControlAdd(m_text);
            int w = m_sprite.GetFrameWidth(MENU_F_MENU_BASE_BUTTON_SMALL), h = m_sprite.GetFrameHeight(MENU_F_MENU_BASE_BUTTON_SMALL);
            VisualControlButton yes = new VisualControlButton(action_yes, -1, SFX_MENU_VALIDATE);
            yes.SetText1(STRING_IDS.COMMAND_YES, "MM_Normal", "MM_Pressed", new vcRect(0, 0, 0, 0));
            yes.SetAnimations(m_sprite, MENU_A_MENU_CONFIRMATION_BUTTON_NORMAL, MENU_A_MENU_CONFIRMATION_BUTTON_PRESSED, MENU_A_MENU_CONFIRMATION_BUTTON_BLINK);
            yes.SetArea(GAME_SCREEN_W2 - 65, 190, w, h);
            VisualControlAdd(yes);
            VisualControlButton no = new VisualControlButton(action_no, -1, SFX_MENU_BACK);
            no.SetText1(STRING_IDS.COMMAND_NO, "MM_Normal", "MM_Pressed", new vcRect(0, 0, 0, 0));
            no.SetAnimations(m_sprite, MENU_A_MENU_CONFIRMATION_BUTTON_NORMAL, MENU_A_MENU_CONFIRMATION_BUTTON_PRESSED, MENU_A_MENU_CONFIRMATION_BUTTON_BLINK);
            no.SetArea(GAME_SCREEN_W2 + 65, 190, w, h);
            VisualControlAdd(no);
            VisualControls_SetEnableTE(false);
            m_isInitialized = true;
        }

        public void SetBackGroundFrame(int bgrFrame)
        {
            m_bgrFrame = bgrFrame;
            UpdateTextFrame();
        }

        public void UpdateTextFrame()
        {
            if (m_sprite == null || m_text == null) return;
            r2i frameRect = m_sprite.GetFrameRect(m_bgrFrame);
            m_text.SetRegion(frameRect.getWidth() - 20, frameRect.getHeight() - 15);
        }

        public override void Draw()
        {
            if (!m_isInitialized) return;
            m_sprite.PaintFrame(m_bgrFrame, GAME_SCREEN_W2, GAME_SCREEN_H2);
            VisualControls_Draw();
        }

        public override void GainFocus()
        {
            base.GainFocus();
            m_text.SetText(m_textID);
            m_result = RESULT_NONE;
            GetParent().VisualControls_SetEnableTE(false);
        }

        public override void LoseFocus()
        {
            base.LoseFocus();
            GetParent().VisualControls_SetEnableTE(true);
        }

        public override void VisualControlDoAction(IVisualControl vc, int action_id)
        {
            if (action_id == action_yes) m_result = RESULT_YES;
            else if (action_id == action_no) m_result = RESULT_NO;
        }
    }

    public class Menu_SoundOptions : MenuSimple
    {
        const int action_sound_options_back = MB.MENU_SOUNDOPTIONS_ACTION_BASE, action_sound_options_back_trans = action_sound_options_back + 1,
            action_slider_sfx = action_sound_options_back + 2, action_slider_music = action_sound_options_back + 3,
            action_back = action_sound_options_back + 4, action_back_trans = action_sound_options_back + 5;

        bool m_isIngame, m_backClicked;
        VisualControlText m_sfxText, m_musicText;
        VisualControlSlider m_musicSlider, m_sfxSlider;

        public Menu_SoundOptions(IMenu parent) : base(parent) { }

        public void SetIsMenuIngame(bool is_ingame) { m_isIngame = is_ingame; }
        public bool isBack() { return m_backClicked; }

        public override void GainFocus()
        {
            base.GainFocus();
            m_musicSlider.SetPercent((int)(__MGR_SOUNDS().getMusicVolume() * 100.0f));
            m_sfxSlider.SetPercent((int)(__MGR_SOUNDS().getSFxVolume() * 100.0f));
            if (m_isIngame)
            {
                GetParent().VisualControls_SetEnableTE(false);
                ((Menu_Ingame)GetParent()).GainFocus();
                ((Menu_Ingame)GetParent()).GetTitleTextControl().SetText(STRING_IDS.MENU_SOUND_OPTIONS_TITLE);
                m_backClicked = false;
                SetMusicId(M_TITLE);
                __MGR_SOUNDS().play(m_music_id);
            }
        }

        public override void LoseFocus()
        {
            base.LoseFocus();
            __MGR_SOUNDS().setMusicVolume(m_musicSlider.GetValue() / 100.0f);
            __MGR_SOUNDS().setSFxVolume(m_sfxSlider.GetValue() / 100.0f);
            if (m_isIngame)
            {
                GetParent().VisualControls_SetEnableTE(true);
                __MGR_SOUNDS().stopAllMusics();
            }
        }

        public override void Initialize()
        {
            LoadResources();
            SetMenuName(STRING_IDS.MENU_SOUND_OPTIONS_TITLE);
            int sw = m_sprite.GetFrameWidth(MENU_F_MENU_SLIDER_EMPTY), sh = m_sprite.GetFrameHeight(MENU_F_MENU_SLIDER_EMPTY);
            m_sfxSlider = new VisualControlSlider(action_slider_sfx, SFX_ATTACK_SWORD);
            m_sfxSlider.SetFrames(m_sprite, MENU_F_MENU_SLIDER_EMPTY, MENU_F_MENU_SLIDER_FILL, MENU_F_MENU_SLIDER_SFX_NORMAL, MENU_F_MENU_SLIDER_SFX_PRESSED);
            m_sfxSlider.SetArea(242, 144, sw, sh);
            VisualControlAdd(m_sfxSlider);
            m_sfxSlider.SetValue(50);
            m_musicSlider = new VisualControlSlider(action_slider_music, -1);
            m_musicSlider.SetFrames(m_sprite, MENU_F_MENU_SLIDER_EMPTY, MENU_F_MENU_SLIDER_FILL, MENU_F_MENU_SLIDER_MUSIC_NORMAL, MENU_F_MENU_SLIDER_MUSIC_PRESSED);
            m_musicSlider.SetArea(241, 236, sw, sh);
            VisualControlAdd(m_musicSlider);
            m_musicSlider.SetValue(50);
            MB.AddBackButton(this, m_sprite, action_back);
            m_sfxText = new VisualControlText();
            m_sfxText.SetText("");
            m_sfxText.SetPos(new p2i(110, 95));
            m_sfxText.SetFont("MM_Normal");
            VisualControlAdd(m_sfxText);
            m_musicText = new VisualControlText();
            m_musicText.SetText("");
            m_musicText.SetPos(new p2i(110, 188));
            m_musicText.SetFont("MM_Normal");
            VisualControlAdd(m_musicText);
            VisualControls_SetEnableTE(false);
            m_isInitialized = true;
        }

        public override void Draw()
        {
            if (!m_isInitialized) return;
            if (!m_isIngame) DrawPanel(74, 55, 405, 287);
            VisualControls_Draw();
        }

        public override void Update(float dt)
        {
            if (!m_isInitialized) return;
            base.Update(dt);
            m_sfxText.SetText(_TXT_INT(STRING_IDS.MENU_SOUND_OPTIONS_SFX_VOL) + " " + m_sfxSlider.GetValue() + "%");
            m_musicText.SetText(_TXT_INT(STRING_IDS.MENU_SOUND_OPTIONS_MUSIC_VOL) + " " + m_musicSlider.GetValue() + "%");
        }

        public override void VisualControlDoAction(IVisualControl vc, int action_id)
        {
            switch (action_id)
            {
                case action_back: Transition_Start(transition_hide, action_back_trans); break;
                case action_back_trans:
                    if (m_isIngame)
                    {
                        GetParent().Transition_Start(transition_show);
                        m_backClicked = true;
                    }
                    else Go("Menu_Options");
                    break;
                case action_slider_sfx: __MGR_SOUNDS().setSFxVolume(m_sfxSlider.GetValue() / 100.0f); break;
                case action_slider_music: __MGR_SOUNDS().setMusicVolume(m_musicSlider.GetValue() / 100.0f); break;
            }
        }
    }

    public class Menu_GameSettings : MenuSimple
    {
        const int action_select_language = MB.MENU_GAME_SETTINGS_ACTION_BASE, action_select_language_trans = action_select_language + 1,
            action_reset_profile = action_select_language + 2, action_reset_profile_trans = action_select_language + 3,
            action_handed_mode_bg = action_select_language + 4, action_handed_mode = action_select_language + 5,
            action_screen_orientation_lock = action_select_language + 6, action_back = action_select_language + 7, action_back_trans = action_select_language + 8;

        bool m_isIngame;
        VisualControlButton m_buttonLock;
        readonly Menu_Confirmation m_menuConfirmation;

        public Menu_GameSettings(IMenu parent) : base(parent)
        {
            m_menuConfirmation = new Menu_Confirmation(this);
            SubMenu_Register("Menu_Confirmation", m_menuConfirmation);
        }

        public void SetIsMenuIngame(bool is_ingame) { m_isIngame = is_ingame; }

        static bool Locked { get { return CApplication.GetInstance().m_deviceOrientationLocked; } }

        void UpdateLockButton()
        {
            if (Locked) m_buttonLock.SetAnimations(m_sprite, MENU_A_MENU_LOCK_BUTTON_ON_NORMAL, MENU_A_MENU_LOCK_BUTTON_ON_PRESSED, MENU_A_MENU_LOCK_BUTTON_ON_BLINK);
            else m_buttonLock.SetAnimations(m_sprite, MENU_A_MENU_LOCK_BUTTON_OFF_NORMAL, MENU_A_MENU_LOCK_BUTTON_OFF_PRESSED, MENU_A_MENU_LOCK_BUTTON_OFF_BLINK);
            m_buttonLock.SetText2(Locked ? _TXT(STRING_IDS.MENU_SWITCH_ON) : _TXT(STRING_IDS.MENU_SWITCH_OFF), "MM_Normal", "MM_Pressed");
        }

        public override void Initialize()
        {
            LoadResources();
            SetMenuName(STRING_IDS.MENU_GAME_SETTINGS_TITLE);
            VisualControlButton lang = MB.AddBaseButton(this, m_sprite, action_select_language, GAME_SCREEN_W2, m_isIngame ? GAME_SCREEN_H2 - 60 : 95, STRING_IDS.MENU_SELECT_LANGUAGE);
            MB.AddBaseButton(this, m_sprite, action_reset_profile, GAME_SCREEN_W2, 150, STRING_IDS.MENU_RESET_PROFILE);
            VisualControlButton vc = new VisualControlButton(action_screen_orientation_lock);
            vc.SetAnimations(m_sprite, MENU_A_MENU_LOCK_BUTTON_ON_NORMAL, MENU_A_MENU_LOCK_BUTTON_ON_PRESSED, MENU_A_MENU_LOCK_BUTTON_ON_BLINK);
            vc.SetArea(GAME_SCREEN_W2, 238, m_sprite.GetFrameWidth(MENU_F_MENU_LOCK_BUTTON_ON), m_sprite.GetFrameHeight(MENU_F_MENU_LOCK_BUTTON_ON));
            vc.SetText1(STRING_IDS.MENU_SCREEN_ORIENTATION, "MM_Normal", "MM_Pressed");
            vc.SetText2(Locked ? _TXT(STRING_IDS.MENU_SWITCH_ON) : _TXT(STRING_IDS.MENU_SWITCH_OFF), "MM_Normal", "MM_Pressed");
            VisualControlAdd(vc);
            m_buttonLock = vc;
            MB.AddBackButton(this, m_sprite, action_back);
            VisualControls_SetEnableTE(false);
            SubMenus_Initialize();
            m_isInitialized = true;
        }

        public override void Destroy()
        {
            VisualControls_Free();
            SubMenus_Destroy();
            m_isInitialized = false;
        }

        public override void Draw()
        {
            if (!m_isInitialized) return;
            VisualControls_Draw();
            if (m_pCurrentSubMenu != null) m_pCurrentSubMenu.Draw();
        }

        public override void GainFocus()
        {
            base.GainFocus();
            if (m_isIngame)
            {
                GetParent().VisualControls_SetEnableTE(false);
                ((Menu_Ingame)GetParent()).GetTitleTextControl().SetText(STRING_IDS.MENU_GAME_SETTINGS_TITLE);
            }
            UpdateLockButton();
        }

        public override void LoseFocus()
        {
            base.LoseFocus();
            if (m_isIngame) GetParent().VisualControls_SetEnableTE(true);
        }

        public override void Update(float dt)
        {
            if (!m_isInitialized) return;
            base.Update(dt);
            if (Transition_IsGoing()) return;
            if (!m_menuConfirmation.IsOnFocus() || m_menuConfirmation.GetDialogResult() == Menu_Confirmation.RESULT_NONE) return;
            m_menuConfirmation.SetBackGroundFrame(MENU_F_MENU_CONFIRMATION_BKG);
            int result = m_menuConfirmation.GetDialogResult();
            SubMenu_Remove();
            if (result != Menu_Confirmation.RESULT_YES) return;
            CUserProfile profile = __PROFILE();
            int oldLanguage = profile.GetInt("Language");
            int orientation = profile.GetInt("ScreenOrientationLock");
            profile.ResetToDefault();
            profile.Set("Language", oldLanguage);
            profile.Set("ScreenOrientationLock", orientation);
            __MGR_SOUNDS().setSFxVolume(profile.GetFloat("sfx_volume") / 100);
            __MGR_SOUNDS().setMusicVolume(profile.GetFloat("music_volume") / 100);
            CAchievements.GetInstance().resetAchievementsByProfile("Player1");
        }

        public override void VisualControlDoAction(IVisualControl vc, int action_id)
        {
            switch (action_id)
            {
                case action_screen_orientation_lock:
                    CApplication.GetInstance().m_deviceOrientationLocked = !Locked;
                    __PROFILE().Set("ScreenOrientationLock", Locked ? 1 : 0);
                    UpdateLockButton();
                    break;
                case action_select_language: Transition_Start(transition_hide, action_select_language_trans); break;
                case action_select_language_trans: Go("Menu_LanguageSelection"); break;
                case action_reset_profile:
                    m_menuConfirmation.SetConfirmationText(STRING_IDS.MENU_RESET_PROFILE_CONFIRMATION);
                    SubMenu_Change("Menu_Confirmation");
                    break;
                case action_back: Transition_Start(transition_hide, action_back_trans); break;
                case action_back_trans: Go(m_isIngame ? "Menu_Ingame_Options" : "Menu_Options"); break;
            }
        }
    }
}
