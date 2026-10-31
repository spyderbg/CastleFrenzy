// Core/Application: initialization steps, update/draw order and shutdown of the game.
using UnityEngine;
using static CastleFrenzy.G;

namespace CastleFrenzy
{
    /// <summary>os::Timer speed (slow motion while casting a spell, F2/F3 debug keys).</summary>
    public static class GameTimer
    {
        public static float Speed { get { return GameMain.TimeSpeed; } set { GameMain.TimeSpeed = value; } }
    }

    public class CApplication
    {
        static CApplication s_instance;
        public static CApplication GetInstance() { return s_instance ?? (s_instance = new CApplication()); }

        bool m_paused;
        public bool m_deviceOrientationLocked;

        public void Init()
        {
            srand(AutoInput.Active ? 12345 : System.Environment.TickCount);
            GameTimer.Speed = 1.0f;
            __MGR_STATES().switchState(new GS_Splash());
        }

        public void InitializeAudio()
        {
            SoundFileManager files = SoundFileManager.GetInstance();
            files.AddPack_Stream("audio_Music.pack");
            files.AddPack_Memory("audio_SFX.pack");
            __MGR_SOUNDS().init("Player1");
        }

        public void InitializeTouchPad() { CTouchPad.InitTouchPad(); }

        public void InitializeStrings()
        {
            int lang = __PROFILE().GetInt("Language");
            ELanguages profileLang = (ELanguages)lang;
            ELanguages deviceLang = GetDeviceLanguage();
            if (profileLang != ELanguages.Language_None) __MGR_STRINGS().SetLanguage(profileLang);
            else if (deviceLang != ELanguages.Language_None) __MGR_STRINGS().SetLanguage(deviceLang);
            else __MGR_STRINGS().SetLanguage(ELanguages.Language_None);
        }

        public void InitializeProfile() { Profile.Get(); }

        public void InitializeAchievments()
        {
            if (CGameConfig.EnableCheats) CCheats.GetInstance().Init();
            CAchievements.GetInstance().init();
            CAchievements.GetInstance().loadAchievementsByProfile("Player1");
        }

        public void InitializeOrientations()
        {
            m_deviceOrientationLocked = __PROFILE().GetInt("ScreenOrientationLock") != 0;
        }

        public void InitializeOthers()
        {
            ParticleManager.LoadParticlePresets();
            LevelInfo.initUnitTypes();
            UnitParameters.InitUnitParameters();
        }

        /// <summary>The original Linux build always reported Language_None (DeviceLanguage was stubbed).</summary>
        public ELanguages GetDeviceLanguage() { return ELanguages.Language_None; }

        /// <summary>
        /// AccelerometerBase::getPitch. The desktop build reported a constant -PI/2 (boulder rolls toward the enemies);
        /// the arrow keys (or a phone's tilt) lean it like the iPhone accelerometer did.
        /// </summary>
        public float GetAccelerometerPitch()
        {
            float pitch = -PI / 2;
            if (Input.GetKey(KeyCode.LeftArrow)) pitch = PI / 2 - PI / 6;
            else if (Input.GetKey(KeyCode.RightArrow)) pitch = PI / 2 + PI / 6;
            else if (UnityEngine.SystemInfo.supportsAccelerometer && Input.acceleration.sqrMagnitude > 0.01f)
                pitch = PI / 2 + CLAMP(Input.acceleration.y * PI / 2, -PI / 2, PI / 2);
            return pitch;
        }

        public bool isPaused() { return m_paused; }

        public void Pause()
        {
            m_paused = true;
            CStateMachine.GetInstance().sleep();
            CTouchElement.ResetElementsInput();
        }

        public void Resume()
        {
            m_paused = false;
            CStateMachine.GetInstance().wakeup();
        }

        public void Update(int ticksPerFrame)
        {
            CTouchPad.UpdateCounters(ticksPerFrame);
            CTouchElement.UpdateElements();
            __MGR_STATES().update(ticksPerFrame);
            CAchievements.GetInstance().update(ticksPerFrame);
            __MGR_SOUNDS().update(ticksPerFrame);
            __IRR_SCENE().update(ticksPerFrame);
            FrameEnded(ticksPerFrame);
        }

        public void Draw()
        {
            __MGR_STATES().draw();
            Gfx.beginScene2D();
            CAchievements.GetInstance().draw();
            Gfx.endScene2D();
        }

        public void Quit()
        {
            CUserProfile profile = __PROFILE();
            __MGR_SOUNDS().stopAllSounds();
            profile.Set("sfx_volume", __MGR_SOUNDS().getSFxVolume() * 100);
            profile.Set("music_volume", __MGR_SOUNDS().getMusicVolume() * 100);
            int completed = profile.GetInt("GameCompletedState");
            if (completed == 1 || completed == 2) profile.Set("GameCompletedState", 3);
            profile.Set("Language", (int)__MGR_STRINGS().GetLanguage());
            CAchievements.GetInstance().saveAchievementsByProfile("Player1");
        }
    }
}
