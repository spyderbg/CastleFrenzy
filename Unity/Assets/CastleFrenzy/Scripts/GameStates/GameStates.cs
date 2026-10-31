// GameStates/: GS_Splash, GS_Menu, GSLevel, GS_Shop and GS_Cheat.
using static CastleFrenzy.G;
using static CastleFrenzy.GE;
using static CastleFrenzy.Ids;

namespace CastleFrenzy
{
    public class GS_Splash : IStateBase
    {
        const int GSSplashState_Initializing = 0, GSSplashState_Loading = 1, GSSplashState_LoadingDone = 2;

        int m_state;
        int m_loadingStep;
        ASprite m_BGR;
        readonly CTouchElement m_TE = new CTouchElement();
        float m_loadingDoneTimer;
        VisualControlSlider m_LoadingBar;
        GS_Menu m_nextState;
        int m_loadingStepMin, m_loadingStepMax;

        public override void ctor()
        {
            m_BGR = new ASprite();
            m_BGR.Load("Menu/menu_bg");
            m_nextState = null;
            m_state = GSSplashState_Initializing;
            m_loadingStep = 0;
            m_loadingStepMin = 0;
            m_loadingStepMax = 9;
        }

        public override void dtor()
        {
            if (m_LoadingBar != null) { m_LoadingBar.Dispose(); m_LoadingBar = null; }
            m_TE.Dispose();
            m_BGR = null;
        }

        void InitializeControls()
        {
            m_LoadingBar = new VisualControlSlider(0);
            m_LoadingBar.SetArea(GAME_SCREEN_W2, GAME_SCREEN_H - 30, 276, 16);
            m_LoadingBar.SetPercent(0);
            m_LoadingBar.SetFrames(m_BGR, MENU_F_MENU_BG_SLIDER_EMPTY, MENU_F_MENU_BG_SLIDER_FILL, -1, -1);
            m_LoadingBar.SetSelected(false);
            m_loadingDoneTimer = 0;
        }

        public override void update(float dt)
        {
            CApplication app = CApplication.GetInstance();
            if (m_state == GSSplashState_Initializing)
            {
                app.InitializeProfile();
                app.InitializeStrings();
                app.InitializeAchievments();
                app.InitializeAudio();
                InitializeControls();
                m_state = GSSplashState_Loading;
            }
            else if (m_state == GSSplashState_Loading)
            {
                switch (m_loadingStep)
                {
                    case 2: app.InitializeTouchPad(); break;
                    case 3: app.InitializeOrientations(); break;
                    case 5: app.InitializeOthers(); break;
                    case 6:
                        if (LaunchOptions.TryStart()) return;
                        m_nextState = new GS_Menu();
                        break;
                    case 7: m_nextState.Load(); break;
                    case 8:
                        m_TE.SetTapDetectionEnabled(true);
                        m_TE.SetEnabled(true);
                        m_TE.SetScreenCoordinates(0, 0, GAME_SCREEN_W, GAME_SCREEN_H);
                        m_TE.DisableAllOtherElements(true);
                        break;
                    case 9: m_state = GSSplashState_LoadingDone; break;
                }
                int totalSteps = m_loadingStepMax - m_loadingStepMin;
                m_LoadingBar.SetPercent((int)((m_loadingStep - m_loadingStepMin + 1) * (100.0f / totalSteps + 100 % totalSteps)));
                m_loadingStep++;
            }
            else
            {
                m_loadingDoneTimer += dt / 1000;
                if (m_TE.IsActive() || IS_LANG_NONE())
                {
                    m_TE.DisableAllOtherElements(false);
                    __MGR_STATES().switchState(m_nextState);
                }
                else __MGR_SOUNDS().playIfNotPlaying(M_TITLE);
            }
        }

        public override void draw()
        {
            Gfx.beginScene2D();
            m_BGR.PaintFrame(MENU_F_MENU_BG_MENU_BG_LOGO, GAME_SCREEN_W2, GAME_SCREEN_H2);
            if (m_state == GSSplashState_Loading)
            {
                if (m_LoadingBar != null) m_LoadingBar.Draw();
                __FONT("LoadingText").DrawString(_TXT(STRING_IDS.MENU_LOADING), GAME_SCREEN_W2, GAME_SCREEN_H - 50, CFont.HVCENTER);
            }
            else if (m_state == GSSplashState_LoadingDone)
            {
                int alpha = CLAMP((int)(196 + 196 * sinf(-PI / 2 + 2 * PI * m_loadingDoneTimer / 1.0f)), 0, 255);
                __FONT("LoadingTitle").DrawString(_TXT(STRING_IDS.HELP_LOAD_CONTINUE), GAME_SCREEN_W2, GAME_SCREEN_H2 + 100, CFont.HVCENTER, alpha);
            }
            Gfx.endScene2D();
        }

        public override void wakeup() { m_TE.DisableAllOtherElements(true); }
    }

    public class GS_Menu : IStateBase
    {
        IMenu m_pMenuMain;
        readonly CTouchElement m_TE = new CTouchElement();
        bool m_goToHighScore, m_goToSelectLevel;

        public void GoToHighScore()
        {
            m_goToHighScore = true;
            m_goToSelectLevel = false;
            Menu_HighScore.SetEndless();
        }

        public void GoToSelectLevel()
        {
            m_goToHighScore = false;
            m_goToSelectLevel = true;
        }

        public override void ctor() { if (m_pMenuMain == null) Load(); }

        public override void dtor()
        {
            m_pMenuMain.Destroy();
            m_pMenuMain.VisualControls_Free();
            m_pMenuMain = null;
            m_TE.Dispose();
        }

        public void Load()
        {
            m_pMenuMain = new MenuMain(null);
            m_pMenuMain.Initialize();
            CUserProfile profile = __PROFILE();
            bool goToAbout = profile.GetInt("GameCompletedState") == 1;
            if (goToAbout) profile.Set("GameCompletedState", 2);
            if (IS_LANG_NONE()) m_pMenuMain.SubMenu_Change("Menu_LanguageSelection");
            else if (goToAbout) m_pMenuMain.SubMenu_Change("Menu_About");
            else if (m_goToHighScore) m_pMenuMain.SubMenu_Change("Menu_HighScore");
            else if (m_goToSelectLevel) m_pMenuMain.SubMenu_Change("Menu_Campaign");
            else m_pMenuMain.SubMenu_Change("Menu_Engage");
            m_TE.SetTapDetectionEnabled(true);
            m_TE.SetEnabled(true);
            m_TE.SetScreenCoordinates(0, 0, GAME_SCREEN_W, GAME_SCREEN_H);
            m_pMenuMain.SetMusicId(M_TITLE);
        }

        public override void sleep() { if (m_pMenuMain != null) m_pMenuMain.VisualControls_Reset(); }

        public override void update(float dt)
        {
            m_pMenuMain.Update(dt);
            if (!m_TE.IsActive()) return;
            int x, y;
            m_TE.GetTouchPos_Current(out x, out y);
            m_TE.ResetInput();
            CCheats.GetInstance().Update(x < 240 ? 1 : 2);
            if (CCheats.GetInstance().IsVisible()) __MGR_STATES().pushState(new GS_Cheat());
        }

        public override void draw()
        {
            Gfx.beginScene2D();
            m_pMenuMain.Draw();
            Gfx.endScene2D();
        }
    }

    public class GSLevel : IStateBase
    {
        public static GSLevel g_level;

        MenuLoading m_pLoadingMenu;
        Menu_Ingame m_pMenuIngame;
        Menu_Help m_pMenuHelp;
        ISceneNode m_dummy;
        CUnit m_rider;

        public GSLevel() { g_level = this; }

        public Menu_Ingame GetInGameMenu() { return m_pMenuIngame; }

        public override void ctor()
        {
            __MGR_SOUNDS().stopAllMusics();
            m_pLoadingMenu = new MenuLoading(null);
            m_pLoadingMenu.Initialize();
            m_pLoadingMenu.GainFocus();
            CleanColladaCache(true);
            m_dummy = new CEmptySceneNode();
            m_rider = new CUnit(UnitParameters.GetUnitParameters("GoblinRider"), m_dummy);
            m_pMenuIngame = new Menu_Ingame(null);
            m_pMenuIngame.Initialize();
            m_pMenuHelp = m_pMenuIngame.GetHelpMenu();
        }

        public override void dtor()
        {
            if (m_rider != null) { m_rider.Dispose(); m_rider = null; }
            CPlayfield.GetInstance().ReleaseLevel();
            m_pMenuIngame.Destroy();
            m_pMenuIngame.VisualControls_Free();
            m_pMenuIngame = null;
            if (m_pLoadingMenu != null) { m_pLoadingMenu.Destroy(); m_pLoadingMenu = null; }
            if (g_level == this) g_level = null;
        }

        public override void update(float dt_ms)
        {
            CPlayfield playfield = CPlayfield.GetInstance();
            if (!playfield.IsLevelLoaded())
            {
                playfield.LoadLevel(dt_ms);
                m_pLoadingMenu.SetProgress(playfield.GetProgress() + 2);
                m_pLoadingMenu.Update(dt_ms);
                if (playfield.IsLevelLoaded())
                {
                    m_pLoadingMenu.Destroy();
                    m_pLoadingMenu = null;
                }
            }
            if (playfield.IsLevelLoaded())
            {
                playfield.update(dt_ms * 0.001f);
                if (playfield.isPaused() || playfield.IsMissionOver())
                {
                    switch (playfield.GetPlayState())
                    {
                        case CPlayfield.PS_EnterIGM: m_pMenuIngame.CallMenu(Menu_Ingame.IngameMenuPause); break;
                        case CPlayfield.PS_IGM:
                        case CPlayfield.PS_Pause: break;
                        case CPlayfield.PS_ExitIGM: m_pMenuIngame.LoseFocus(); break;
                        default:
                            if (playfield.IsMissionOver() && !m_pMenuIngame.IsOnFocus())
                            {
                                if (playfield.IsMissionSuccess())
                                    m_pMenuIngame.CallMenu(playfield.IsLevelOver() ? Menu_Ingame.IngameMenuHighScore : Menu_Ingame.IngameMenuVictory);
                                else if (playfield.m_isEndless)
                                {
                                    playfield.SetEndlessScores(m_pMenuIngame);
                                    m_pMenuIngame.CallMenu(Menu_Ingame.IngameMenuHighScore);
                                }
                                else m_pMenuIngame.CallMenu(Menu_Ingame.IngameMenuDefeated);
                            }
                            break;
                    }
                    m_pMenuIngame.Update(dt_ms);
                }
            }
            CAchievements.GetInstance().addValue("Addicted", (uint)dt_ms);
        }

        public override void draw()
        {
            CPlayfield playfield = CPlayfield.GetInstance();
            if (playfield.IsLevelLoaded())
            {
                if (m_pMenuHelp != null && m_pMenuHelp.IsOnFocus()) m_pMenuHelp.ResetGraphics();
                playfield.draw();
                playfield.draw2D();
                if (playfield.isPaused() || playfield.IsMissionOver())
                {
                    Gfx.beginScene2D();
                    m_pMenuIngame.Draw();
                    Gfx.endScene2D();
                }
            }
            else
            {
                Gfx.beginScene2D();
                if (m_pLoadingMenu != null) m_pLoadingMenu.Draw();
                playfield.DrawFade();
                Gfx.endScene2D();
            }
        }

        public override void sleep()
        {
            CPlayfield.GetInstance().pause();
            if (m_pLoadingMenu != null) m_pLoadingMenu.VisualControls_Reset();
            if (m_pMenuIngame != null) m_pMenuIngame.VisualControls_Reset();
            if (m_pMenuHelp != null) m_pMenuHelp.VisualControls_Reset();
        }

        public override void wakeup() { CPlayfield.GetInstance().resume(); }
    }

    public class GS_Shop : IStateBase
    {
        IMenu m_pMenuShop;

        public override void ctor()
        {
            m_pMenuShop = new Menu_Shop(null);
            m_pMenuShop.Initialize();
            m_pMenuShop.GainFocus();
            m_pMenuShop.SetMusicId(M_SHOP);
        }

        public override void dtor()
        {
            m_pMenuShop.Destroy();
            m_pMenuShop.VisualControls_Free();
            m_pMenuShop = null;
        }

        public override void sleep() { if (m_pMenuShop != null) m_pMenuShop.VisualControls_Reset(); }
        public override void update(float dt) { m_pMenuShop.Update(dt); }

        public override void draw()
        {
            Gfx.beginScene2D();
            m_pMenuShop.Draw();
            Gfx.endScene2D();
        }
    }

    public class GS_Cheat : IStateBase
    {
        readonly CTouchElement m_TE = new CTouchElement();

        public override void ctor()
        {
            m_TE.SetTapDetectionEnabled(true);
            m_TE.SetEnabled(true);
            m_TE.SetScreenCoordinates(0, 0, 480, 320);
        }

        public override void dtor() { m_TE.Dispose(); }

        public override void update(float dt)
        {
            if (!m_TE.IsNowActivated()) return;
            int x, y;
            m_TE.GetTouchPos_Current(out x, out y);
            CCheats cheats = CCheats.GetInstance();
            if (cheats.Activate(x, y))
            {
                cheats.Hide();
                cheats.Clear();
                __MGR_STATES().popState();
            }
        }

        public override void draw()
        {
            Gfx.beginScene2D();
            CCheats.GetInstance().Draw();
            Gfx.endScene2D();
        }
    }
}
