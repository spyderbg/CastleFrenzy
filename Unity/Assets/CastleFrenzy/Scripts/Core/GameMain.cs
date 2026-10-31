// Unity entry point: owns the camera, feeds mouse/touch/keyboard input to the touch pad
// emulation and drives Application.Update/Draw once per frame.
using System;
using System.IO;
using UnityEngine;

namespace CastleFrenzy
{
    public class GameMain : MonoBehaviour
    {
        public static GameMain Instance { get; private set; }

        Camera m_camera;
        float m_msAccumulator;
        public static float TimeSpeed = 1.0f;
        /// <summary>Extra multiplier for automated runs (-cf-timescale); independent of the game's own timer speed.</summary>
        public static float TestTimeScale = 1.0f;

        // Command line automation (used for smoke tests and screenshots)
        int m_frameLimit;
        int m_frameCount;
        string m_screenshotPath;
        int m_screenshotFrame;
        string m_testMode;
        RendererSelfTest m_selfTest;
        readonly bool[] m_down = new bool[2];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindAnyObjectByType<GameMain>() != null) return;
            GameObject go = new GameObject("CastleFrenzy");
            go.AddComponent<GameMain>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Input.multiTouchEnabled = true;
            ParseCommandLine();
            // -1 means uncapped on desktop but the platform default (30) on phones, so ask phones for their refresh rate
            if (LaunchOptions.Fast)
                Application.targetFrameRate = Application.isMobilePlatform ? Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value) : -1;
            GameDataInstaller.EnsureInstalled();

            m_camera = GetComponent<Camera>();
            if (m_camera == null) m_camera = gameObject.AddComponent<Camera>();
            foreach (Camera c in Camera.allCameras) if (c != m_camera) c.enabled = false;
            m_camera.clearFlags = CameraClearFlags.SolidColor;
            m_camera.backgroundColor = Color.black;
            m_camera.cullingMask = 0;
            m_camera.orthographic = true;
            m_camera.depth = 100;
            Gfx.Init(m_camera);
            CSceneManager.Create();

            if (m_testMode == "renderer") m_selfTest = new RendererSelfTest();
            else CApplication.GetInstance().Init();
        }

        void ParseCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                string next = i + 1 < args.Length ? args[i + 1] : null;
                if (a == "-cf-frames" && next != null) { int.TryParse(next, out m_frameLimit); i++; }
                else if (a == "-cf-screenshot" && next != null) { m_screenshotPath = next; i++; }
                else if (a == "-cf-screenshot-frame" && next != null) { int.TryParse(next, out m_screenshotFrame); i++; }
                else if (a == "-cf-test" && next != null) { m_testMode = next; i++; }
                else if (a == "-cf-data" && next != null) { GameFiles.Root = next; i++; }
                else if (a == "-cf-script" && next != null) { AutoInput.Load(next); i++; }
                else if (a == "-cf-save-dir" && next != null) { GameFiles.SavePathOverride = next; i++; }
            }
            LaunchOptions.Parse(args);
            LaunchOptions.LoadEditorRequest();
            if (m_screenshotPath != null && m_screenshotFrame == 0) m_screenshotFrame = Math.Max(1, m_frameLimit - 1);
        }

        /// <summary>Converts window pixels to the 480x320 game coordinates.</summary>
        public static p2i ToGame(Vector2 screen)
        {
            Rect r = Gfx.ComputeGameRect(Screen.width, Screen.height);
            float x = (screen.x - r.x) * Gfx.SCREEN_W / r.width;
            float y = (r.yMax - screen.y) * Gfx.SCREEN_H / r.height;
            return new p2i((int)x, (int)y);
        }

        void HandleInput()
        {
            if (AutoInput.Active) { AutoInput.Step(m_frameCount); return; }
            if (Input.touchSupported && Input.touchCount > 0)
            {
                foreach (Touch t in Input.touches)
                {
                    if (t.fingerId >= CTouchPad.k_max_touches) continue;
                    p2i p = ToGame(t.position);
                    switch (t.phase)
                    {
                        case TouchPhase.Began: CTouchPad.EventTouchDown(t.fingerId, p.X, p.Y); break;
                        case TouchPhase.Moved:
                        case TouchPhase.Stationary: CTouchPad.EventTouchMove(t.fingerId, p.X, p.Y); break;
                        case TouchPhase.Ended:
                        case TouchPhase.Canceled: CTouchPad.EventTouchUp(t.fingerId, p.X, p.Y); break;
                    }
                }
                return;
            }
            p2i m = ToGame(Input.mousePosition);
            for (int b = 0; b < 2; b++)
            {
                if (Input.GetMouseButtonDown(b)) { m_down[b] = true; CTouchPad.EventTouchDown(b, m.X, m.Y); }
                else if (Input.GetMouseButtonUp(b) && m_down[b]) { m_down[b] = false; CTouchPad.EventTouchUp(b, m.X, m.Y); }
                else if (m_down[b]) CTouchPad.EventTouchMove(b, m.X, m.Y);
            }
            if (Input.GetKeyDown(KeyCode.F2)) TimeSpeed *= 0.5f;
            if (Input.GetKeyDown(KeyCode.F3)) TimeSpeed *= 2.0f;
            if (Input.GetKeyDown(KeyCode.F11)) Screen.fullScreen = !Screen.fullScreen;
        }

        void Update()
        {
            m_frameCount++;
            float ms = Time.unscaledDeltaTime * 1000f * TimeSpeed * TestTimeScale;
            if (m_frameLimit > 0 || AutoInput.Active) ms = 1000f / 60f * TimeSpeed * TestTimeScale; // deterministic automation
            m_msAccumulator += ms;
            int ticks = (int)m_msAccumulator;
            m_msAccumulator -= ticks;
            ticks = Mathf.Clamp(ticks, 1, 100);

            HandleInput();
            Gfx.BeginFrame();
            if (m_selfTest != null)
            {
                m_selfTest.Update(ticks);
                m_selfTest.Draw();
            }
            else
            {
                CApplication app = CApplication.GetInstance();
                if (!app.isPaused())
                {
                    app.Update(ticks);
                    LaunchOptions.ApplyTestHooks();
                    app.Draw();
                }
            }
            Gfx.EndFrame();
            CUserProfile.Flush();

            if (m_screenshotPath != null && m_frameCount == m_screenshotFrame)
                ScreenCapture.CaptureScreenshot(m_screenshotPath);
            if (m_frameLimit > 0 && m_frameCount >= m_frameLimit + 2) Quit();
        }

        void OnApplicationPause(bool pause)
        {
            if (m_selfTest != null) return;
            if (pause) CApplication.GetInstance().Pause();
            else CApplication.GetInstance().Resume();
        }

        void OnApplicationQuit()
        {
            if (m_selfTest == null) CApplication.GetInstance().Quit();
            CUserProfile.Flush();
        }

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
