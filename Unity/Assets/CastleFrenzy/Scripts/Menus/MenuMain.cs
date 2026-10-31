// Menus/MenuMain (root of the front-end menus) and Menus/Menu_Engage.
using static CastleFrenzy.G;
using static CastleFrenzy.GE;
using static CastleFrenzy.Ids;

namespace CastleFrenzy
{
    /// <summary>Shared menu constants from MenuMain.h.</summary>
    public static class MB
    {
        public const int MENU_ENGAGE_ACTION_BASE = 10, MENU_OPTIONS_ACTION_BASE = 40, MENU_NEWGAME_ACTION_BASE = 60, MENU_SOUNDOPTIONS_ACTION_BASE = 90,
            MENU_CAMPAIGN_ACTION_BASE = 150, MENU_DAY_SELECT_ACTION_BASE = 250, MENU_SHOP_ACTION_BASE = 300, MENU_INFO_ACTION_BASE = 350,
            MENU_GAME_SETTINGS_ACTION_BASE = 400, MENU_ACHIEVEMENTS_ACTION_BASE = 450, MENU_CONFIRMATION_ACTION_BASE = 500,
            MENU_DIFFICULTY_ACTION_BASE = 550, MENU_HELP_ACTION_BASE = 600, MENU_INGAME_HELP_ACTION_BASE = 700, MENU_LANGUAGE_SELECT = 750,
            MENU_HIGHSCORES_BASE = 850, MENU_HIGHSCORESLIST_BASE = 900, MENU_ABOUT_ACTION_BASE = 1000;

        public const int k_button_back_frame = MENU_F_MENU_BACK_BUTTON;
        public const int k_button_back_game_x = 41, k_button_back_game_y = 70;
        public const int k_button_back_game_anim_normal = MENU_A_MENU_BACK_BUTTON_NORMAL;
        public const int k_button_back_game_anim_pressed = MENU_A_MENU_BACK_BUTTON_PRESSED;
        public const int k_button_back_game_anim_blink = MENU_A_MENU_BACK_BUTTON_BLINK;
        public const string k_button_back_text = "", k_button_back_font1 = "MM_Normal", k_button_back_font2 = "MM_Pressed";
        public const int k_button_main_frame = MENU_F_MENU_BASE_BUTTON;
        public const int k_button_main_title_x = GAME_SCREEN_W2, k_button_main_title_y = 16;
        public const string k_button_main_font_title = "MM_Title", k_button_main_font2 = "MM_Normal";
        public const int k_button_main_textFlag = CFont.LEFT | CFont.VCENTER;
        public const int k_back_button_sfx = SFX_MENU_BACK;

        /// <summary>Adds the standard back button (MENU_F_MENU_BACK_BUTTON at 41,70) used by most sub-menus.</summary>
        public static VisualControlButton AddBackButton(IMenu menu, ASprite sprite, int action, int x = k_button_back_game_x, int y = k_button_back_game_y)
        {
            VisualControlButton b = new VisualControlButton(action, -1, k_back_button_sfx);
            b.SetAnimations(sprite, k_button_back_game_anim_normal, k_button_back_game_anim_pressed, k_button_back_game_anim_blink);
            b.SetArea(x, y, sprite.GetFrameWidth(k_button_back_frame), sprite.GetFrameHeight(k_button_back_frame));
            menu.VisualControlAdd(b);
            return b;
        }

        /// <summary>Adds a MENU_F_MENU_BASE_BUTTON button with a string id.</summary>
        public static VisualControlButton AddBaseButton(IMenu menu, ASprite sprite, int action, int x, int y, int textId, int lockAnim = -1)
        {
            VisualControlButton b = new VisualControlButton(action);
            if (lockAnim >= 0) b.SetAnimations(sprite, MENU_A_MENU_BASE_BUTTON_NORMAL, MENU_A_MENU_BASE_BUTTON_PRESSED, MENU_A_MENU_BASE_BUTTON_BLINK, lockAnim);
            else b.SetAnimations(sprite, MENU_A_MENU_BASE_BUTTON_NORMAL, MENU_A_MENU_BASE_BUTTON_PRESSED, MENU_A_MENU_BASE_BUTTON_BLINK);
            b.SetArea(x, y, sprite.GetFrameWidth(MENU_F_MENU_BASE_BUTTON), sprite.GetFrameHeight(MENU_F_MENU_BASE_BUTTON));
            b.SetText1(textId, "MM_Normal", "MM_Pressed");
            menu.VisualControlAdd(b);
            return b;
        }
    }

    public class MenuMain : IMenu
    {
        readonly Menu_Help m_menuHelp;
        IMenu m_LastSubMenuPtr;
        VisualControlButton m_title;
        ASprite m_BGR;

        public MenuMain(IMenu parent) : base(parent)
        {
            SubMenu_Register("Menu_Engage", new Menu_Engage(this));
            SubMenu_Register("Menu_NewGame", new Menu_NewGame(this));
            SubMenu_Register("Menu_Options", new Menu_Options(this));
            SubMenu_Register("Menu_SoundOptions", new Menu_SoundOptions(this));
            SubMenu_Register("Menu_Campaign", new Menu_Campaign(this));
            SubMenu_Register("Menu_Info", new Menu_Info(this));
            SubMenu_Register("Menu_GameSettings", new Menu_GameSettings(this));
            SubMenu_Register("Menu_Achievements", new Menu_Achievements(this));
            SubMenu_Register("Menu_Difficulty", new Menu_Difficulty(this));
            SubMenu_Register("Menu_About", new Menu_About(this));
            m_menuHelp = new Menu_Help(this);
            SubMenu_Register("Menu_Help", m_menuHelp);
            SubMenu_Register("Menu_LanguageSelection", new Menu_LanguageSelection(this));
            SubMenu_Register("Menu_HighScoresLevelList", new Menu_HighScoresLevelList(this));
            SubMenu_Register("Menu_HighScore", new Menu_HighScore(this));
        }

        public VisualControlButton GetTitle() { return m_title; }
        public Menu_Help GetHelpMenu() { return m_menuHelp; }

        protected override void LoadResources()
        {
            m_sprite = new ASprite();
            m_sprite.Load("Menu/menu");
            m_BGR = new ASprite();
            m_BGR.Load("Menu/menu_bg");
            SubMenus_LoadResources();
        }

        protected override void FreeResources()
        {
            SubMenus_FreeResources();
            m_sprite = null;
            m_BGR = null;
        }

        public override void Initialize()
        {
            LoadResources();
            m_LastSubMenuPtr = null;
            VisualControlButton vc = new VisualControlButton(-1);
            vc.SetAnimations(m_sprite, MENU_A_MENU_MAIN, MENU_A_MENU_MAIN, MENU_A_MENU_MAIN);
            vc.SetArea(MB.k_button_main_title_x, MB.k_button_main_title_y, m_sprite.GetFrameWidth(MB.k_button_main_frame), m_sprite.GetFrameHeight(MB.k_button_main_frame));
            vc.SetText1("", "MM_Normal", "MM_Normal", new vcRect(0, 0, 0, 0), MB.k_button_main_textFlag);
            vc.SetEnabled(false);
            vc.SetDisabledTextAlpha(255);
            VisualControlAdd(vc);
            m_title = vc;
            SubMenus_Initialize();
            m_isInitialized = true;
        }

        public override void Destroy()
        {
            if (m_music_id >= 0) __MGR_SOUNDS().stop(m_music_id);
            FreeResources();
            VisualControls_Free();
            SubMenus_Destroy();
            m_isInitialized = false;
        }

        public override void GainFocus()
        {
            if (!m_isInitialized || m_isOnFocus) return;
            VisualControls_SetEnableTE(true);
            m_isOnFocus = true;
            if (m_pCurrentSubMenu != null) m_pCurrentSubMenu.GainFocus();
            Transition_Start(transition_show);
        }

        public override void LoseFocus()
        {
            if (!m_isInitialized) return;
            if (m_pCurrentSubMenu != null) m_pCurrentSubMenu.LoseFocus();
            VisualControls_SetEnableTE(false);
            m_isOnFocus = false;
        }

        public override void Draw()
        {
            m_BGR.PaintFrame(MENU_F_MENU_BG_MENU_BG, GAME_SCREEN_W2, GAME_SCREEN_H2);
            Gfx.draw2DRectangle(new SColor(0x50000000), new r2i(0, 0, GAME_SCREEN_W, GAME_SCREEN_H));
            base.Draw();
        }

        public override void Update(float dt)
        {
            if (!m_isInitialized) return;
            base.Update(dt);
            if (m_pCurrentSubMenu != m_LastSubMenuPtr)
            {
                m_LastSubMenuPtr = m_pCurrentSubMenu;
                if (m_pCurrentSubMenu != null)
                    m_title.SetText1(m_pCurrentSubMenu.GetMenuName(), MB.k_button_main_font_title, MB.k_button_main_font2, new vcRect(0, 0, 0, 0), MB.k_button_main_textFlag);
            }
            UpdateMenuMusic();
        }

        public override void VisualControlDoAction(IVisualControl visual_control, int action_id) { }
    }

    public class Menu_Engage : IMenu
    {
        const int action_new_game = MB.MENU_ENGAGE_ACTION_BASE, action_new_game_trans = action_new_game + 1, action_continue = action_new_game + 2,
            action_continue_trans = action_new_game + 3, action_options = action_new_game + 4, action_options_trans = action_new_game + 5,
            action_info = action_new_game + 6, action_info_trans = action_new_game + 7;
        const int k_button_main_base_offset_y = 87, k_button_main_offset_y = 51, k_buttons_offset = 65;

        public Menu_Engage(IMenu parent) : base(parent) { }

        public override void Initialize()
        {
            LoadResources();
            SetMenuName(STRING_IDS.MENU_ENGAGE_TITLE);
            MB.AddBaseButton(this, m_sprite, action_new_game, GAME_SCREEN_W2, k_button_main_base_offset_y, STRING_IDS.MENU_ENGAGE_NEW);
            MB.AddBaseButton(this, m_sprite, action_continue, GAME_SCREEN_W2, k_button_main_base_offset_y + k_button_main_offset_y, STRING_IDS.MENU_ENGAGE_CONTINUE, MENU_A_MENU_BASE_BUTTON_LOCK);
            MB.AddBaseButton(this, m_sprite, action_options, GAME_SCREEN_W2, k_button_main_base_offset_y + 2 * k_button_main_offset_y, STRING_IDS.MENU_ENGAGE_OPTIONS);
            VisualControlButton info = new VisualControlButton(action_info);
            info.SetAnimations(m_sprite, MENU_A_MENU_INFO_BUTTON_NORMAL, MENU_A_MENU_INFO_BUTTON_PRESSED, MENU_A_MENU_INFO_BUTTON_BLINK);
            info.SetArea(40, 292, m_sprite.GetFrameWidth(MENU_F_MENU_BUTTON_INFO), m_sprite.GetFrameHeight(MENU_F_MENU_BUTTON_INFO));
            VisualControlAdd(info);
            VisualControls_SetEnableTE(false);
            m_isInitialized = true;
        }

        public override void Destroy()
        {
            VisualControls_Free();
            FreeResources();
            m_isInitialized = false;
        }

        public override void GainFocus()
        {
            base.GainFocus();
            CPlayfield playfield = CPlayfield.GetInstance();
            playfield.ReadProfileStats();
            IVisualControl cont = VisualControlGetByActionId(action_continue);
            bool noSave = playfield.m_CurrentLevel == -1 || playfield.m_CurrentDay == -1;
            cont.SetEnabled(!noSave);
            cont.SetVisible(!noSave);
            VisualControlGetByActionId(action_new_game).SetPos(new p2i(GAME_SCREEN_W2, noSave ? GAME_SCREEN_H2 - k_buttons_offset : k_button_main_base_offset_y));
            VisualControlGetByActionId(action_options).SetPos(new p2i(GAME_SCREEN_W2, noSave ? GAME_SCREEN_H2 : k_button_main_base_offset_y + 2 * k_button_main_offset_y));
        }

        public override void VisualControlDoAction(IVisualControl visual_control, int action_id)
        {
            switch (action_id)
            {
                case action_new_game: Transition_Start(transition_hide, action_new_game_trans); break;
                case action_new_game_trans: GetParent().SubMenu_Change("Menu_NewGame"); break;
                case action_continue: Transition_Start(transition_hide, action_continue_trans); break;
                case action_continue_trans:
                {
                    CPlayfield playfield = CPlayfield.GetInstance();
                    playfield.ReadProfileStats();
                    playfield.m_isEndless = false;
                    if (playfield.m_CurrentDay == 0) playfield.StartNewLevel(playfield.m_CurrentLevel);
                    else if (playfield.m_EnterInShop) __MGR_STATES().switchState(new GS_Shop());
                    else __MGR_STATES().switchState(new GSLevel());
                    break;
                }
                case action_info: Transition_Start(transition_hide, action_info_trans); break;
                case action_info_trans: GetParent().SubMenu_Change("Menu_Info"); break;
                case action_options: Transition_Start(transition_hide, action_options_trans); break;
                case action_options_trans: GetParent().SubMenu_Change("Menu_Options"); break;
            }
        }
    }
}
