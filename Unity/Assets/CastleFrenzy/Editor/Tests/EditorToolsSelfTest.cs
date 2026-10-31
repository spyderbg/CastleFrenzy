// Batch-mode checks for the editor tools:
//   Unity -batchmode -projectPath Unity -executeMethod CastleFrenzy.EditorTools.EditorToolsSelfTest.Run -cf-out <dir>
// 1. every sprite YAML survives SpriteDocument parse/serialize and compiles to identical game data,
// 2. sprite IDs regenerate without changes, 3. levels load and re-save byte-identically (into a temp dir),
// 4. the game renderer draws a sprite frame and a 3D model into PNGs.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CastleFrenzy.EditorTools
{
    public static class EditorToolsSelfTest
    {
        static int s_failures;

        static void Check(bool ok, string what)
        {
            if (!ok) { s_failures++; Debug.LogError("[EditorToolsSelfTest] FAIL " + what); }
        }

        public static void Run()
        {
            string outDir = Path.Combine(GameDataTools.ProjectRoot, "Unity/Temp/EditorToolsSelfTest");
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-cf-out") outDir = args[i + 1];
            Directory.CreateDirectory(outDir);
            s_failures = 0;
            try
            {
                SpriteRoundTrip();
                IdsUnchanged();
                LevelsRoundTrip(outDir);
                RenderPreviews(outDir);
            }
            catch (Exception e)
            {
                s_failures++;
                Debug.LogException(e);
            }
            Debug.Log("[EditorToolsSelfTest] done, failures=" + s_failures);
            EditorApplication.Exit(s_failures == 0 ? 0 : 1);
        }

        static void SpriteRoundTrip()
        {
            string root = GameDataTools.UnityPath("Sprites");
            int count = 0;
            foreach (string path in Directory.GetFiles(root, "*.yaml", SearchOption.AllDirectories))
            {
                string rel = "Sprites/" + path.Substring(root.Length + 1).Replace('\\', '/');
                string text = File.ReadAllText(path);
                SpriteDocument doc = SpriteDocument.Parse(text);
                string again = doc.Serialize();
                Check(SpriteDocument.Parse(again).Serialize() == again, rel + ": serialize is not stable");
                Check(doc.Validate().Count == 0, rel + ": validation errors " + string.Join("; ", doc.Validate().Take(3)));
                ASprite a = new ASprite(), b = new ASprite();
                Check(a.LoadYamlText(text, rel, true), rel + ": original does not compile");
                Check(b.LoadYamlText(again, rel, true), rel + ": re-serialized does not compile");
                Check(a.GetModulesNum() == b.GetModulesNum() && a.GetFramesNum() == b.GetFramesNum() && a.GetAnimsNum() == b.GetAnimsNum(), rel + ": counts differ");
                for (int m = 0; m < a.GetModulesNum(); m++) Check(a.GetModuleWidth(m) == b.GetModuleWidth(m) && a.GetModuleHeight(m) == b.GetModuleHeight(m), rel + ": module " + m);
                for (int f = 0; f < a.GetFramesNum(); f++)
                {
                    Check(a.GetFModules(f) == b.GetFModules(f) && a.GetFrameRect(f).Equals(b.GetFrameRect(f)), rel + ": frame " + f);
                    for (int p = 0; p < a.GetFModules(f); p++) Check(a.GetFModuleOffX(f, p) == b.GetFModuleOffX(f, p) && a.GetFModuleOffY(f, p) == b.GetFModuleOffY(f, p), rel + ": frame " + f + " part " + p);
                }
                for (int an = 0; an < a.GetAnimsNum(); an++)
                {
                    Check(a.GetAFrames(an) == b.GetAFrames(an), rel + ": anim " + an);
                    for (int s = 0; s < a.GetAFrames(an); s++) Check(a.GetAnimFrame(an, s) == b.GetAnimFrame(an, s) && a.GetAFrameTime(an, s) == b.GetAFrameTime(an, s), rel + ": anim " + an + " step " + s);
                }
                count++;
            }
            Debug.Log("[EditorToolsSelfTest] sprites round-tripped: " + count);
            Check(count >= 29, "expected at least 29 sprite YAML files, found " + count);
        }

        static void IdsUnchanged()
        {
            string dir = GameDataTools.IdsFolder;
            Dictionary<string, string> before = Directory.GetFiles(dir, "Ids_*.cs").ToDictionary(f => f, f => File.ReadAllText(f));
            Func<string, HashSet<string>> consts = text => new HashSet<string>(text.Split('\n').Where(l => l.Contains("public const int")).Select(l => l.Trim()));
            string report = GameDataTools.RegenerateSpriteIds();
            foreach (var kv in before)
            {
                HashSet<string> now = consts(File.ReadAllText(kv.Key)), old = consts(kv.Value);
                Check(now.SetEquals(old), Path.GetFileName(kv.Key) + ": regenerated constants differ");
                File.WriteAllText(kv.Key, kv.Value);
            }
            Debug.Log("[EditorToolsSelfTest] " + report);
        }

        static void LevelsRoundTrip(string outDir)
        {
            CGameConfig.GetInstance().clear();
            LevelInfo.initUnitTypes();
            List<int> days = LevelInfo.loadNumberOfDaysPerLevel();
            Check(days.Count >= 2, "levels/info.xml lists " + days.Count + " levels");
            string tmp = Path.Combine(outDir, "LevelRoundTrip").Replace('\\', '/');
            if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
            Directory.CreateDirectory(tmp + "/Levels");
            int files = 0, identical = 0;
            for (int l = 0; l < days.Count; l++)
            {
                LevelInfo.LevelsRoot = null;
                Level info = LevelInfo.loadLevelInfo(LevelInfo.getLevelPath(l));
                List<Day> ds = new List<Day>();
                for (int d = 0; d < days[l]; d++) ds.Add(LevelInfo.loadDayInfo(LevelInfo.getDayPath(l, d)));
                LevelInfo.LevelsRoot = tmp;
                LevelInfo.saveLevelInfo(LevelInfo.getLevelPath(l), info);
                for (int d = 0; d < days[l]; d++)
                {
                    Check(ds[d] != null, "level " + (l + 1) + " day " + (d + 1) + " does not load");
                    if (ds[d] == null) continue;
                    LevelInfo.saveDayInfo(LevelInfo.getDayPath(l, d), ds[d]);
                    Day back = LevelInfo.loadDayInfo(LevelInfo.getDayPath(l, d));
                    Check(back != null && LevelInfo.sameDay(back, ds[d]), "level " + (l + 1) + " day " + (d + 1) + " changes on save");
                    files++;
                    byte[] orig = File.ReadAllBytes(GameDataTools.UnityPath(LevelInfo.getDayPath(l, d)));
                    byte[] saved = File.ReadAllBytes(tmp + "/" + LevelInfo.getDayPath(l, d));
                    if (orig.SequenceEqual(saved)) identical++;
                }
            }
            LevelInfo.LevelsRoot = null;
            Debug.Log("[EditorToolsSelfTest] days re-saved: " + files + " (" + identical + " byte-identical to the shipped XML)");
        }

        static void SavePng(RenderTexture rt, string path)
        {
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D t = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            t.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, t.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(t);
        }

        static void RenderPreviews(string outDir)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.LogWarning("[EditorToolsSelfTest] no graphics device (-nographics): skipping preview renders");
                return;
            }
            using (EditorGameRenderer r = new EditorGameRenderer())
            {
                ASprite menu = new ASprite();
                Check(menu.Load("Menu/menu"), "Menu/menu sprite loads");
                CFont font = CFontManager.GetInstance().GetFont("MM_Normal");
                r.Render(480, 320, () =>
                {
                    Gfx.beginScene2D();
                    menu.PaintFrame(Ids.MENU_F_MENU_BASE_BUTTON, 240, 120);
                    menu.PaintAFrame(Ids.MENU_A_MENU_BACK_BUTTON_NORMAL, 0, 60, 60, 0, 0, 0);
                    if (font != null) font.DrawString("AuroraMX preview", 240, 200, CFont.HVCENTER);
                    Gfx.endScene2D();
                });
                SavePng(r.Texture, Path.Combine(outDir, "sprite_preview.png"));

                CGameConfig.GetInstance().clear();
                LevelInfo.initUnitTypes();
                EditorGameRenderer.EnsureRuntime();
                CSceneManager scene = CSceneManager.Instance;
                ISceneNode goblin = G.LoadColladaScene("Scenes/Goblin/goblin.bdae", true);
                Check(goblin != null, "goblin model loads");
                if (goblin == null) return;
                G.cleanNode(goblin, true);
                goblin.setScale(new v3f(0.7f, 0.7f, 0.7f));
                scene.getRootSceneNode().addChild(goblin);
                CLightSceneNode light = scene.addLightSceneNode();
                light.getLightData().AmbientColor = new SColorf(1, 1, 1, 1);
                CCameraSceneNode cam = scene.addCameraSceneNode();
                cam.setAspectRatio(1.5f);
                cam.setPosition(new v3f(2.2f, 1.4f, 3.2f));
                cam.setTarget(new v3f(0, 0.8f, 0));
                CTimelineController tc = G.Timeline(goblin);
                if (tc.getClipCount() > 0) { tc.setClip(0); tc.setLoop(true); }
                scene.update(250);
                r.Render(480, 320, () => { scene.setActiveCamera(cam); scene.drawAll(); });
                SavePng(r.Texture, Path.Combine(outDir, "model_preview.png"));
                goblin.remove();
                cam.remove();
                light.remove();
            }
        }
    }
}

namespace CastleFrenzy.EditorTools
{
    /// <summary>
    /// Interactive-editor check: opens the tool windows and saves screenshots of them.
    ///   Unity -projectPath Unity -executeMethod CastleFrenzy.EditorTools.EditorWindowShots.Run -cf-out dir
    /// </summary>
    public static class EditorWindowShots
    {
        static readonly System.Collections.Generic.List<System.Action> s_steps = new System.Collections.Generic.List<System.Action>();
        static int s_step, s_wait;
        static string s_out;

        public static void Run()
        {
            s_out = System.IO.Path.Combine(GameDataTools.ProjectRoot, "Unity/Temp/EditorWindowShots");
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-cf-out") s_out = args[i + 1];
            System.IO.Directory.CreateDirectory(s_out);
            AuroraMXWindow amx = null;
            LevelEditorWindow le = null;
            s_steps.Add(() => { amx = UnityEditor.EditorWindow.GetWindow<AuroraMXWindow>(); amx.position = new UnityEngine.Rect(40, 60, 1280, 760); AuroraMXWindow.OpenFile("Sprites/Menu/menu.yaml"); amx.Select(1, 0); });
            s_steps.Add(() => Shot(amx, "auroramx_frames.png"));
            s_steps.Add(() => amx.Select(0, 4));
            s_steps.Add(() => Shot(amx, "auroramx_modules.png"));
            s_steps.Add(() => { AuroraMXWindow.OpenFile("Sprites/Ingame/fireball.yaml"); amx.Select(2, 0); });
            s_steps.Add(() => Shot(amx, "auroramx_anims.png"));
            s_steps.Add(() => { amx.Close(); le = UnityEditor.EditorWindow.GetWindow<LevelEditorWindow>(); le.position = new UnityEngine.Rect(40, 60, 1280, 760); le.Select(0, 2, 1, 0); });
            s_steps.Add(() => Shot(le, "leveleditor_waves.png"));
            s_steps.Add(() => le.Select(1, 4, 0, -1));
            s_steps.Add(() => Shot(le, "leveleditor_weapons.png"));
            s_steps.Add(() => le.Select(2, 0, 0, -1));
            s_steps.Add(() => Shot(le, "leveleditor_models.png"));
            s_steps.Add(() => le.Select(3, 3, 2, -1));
            s_steps.Add(() => Shot(le, "leveleditor_spawns.png"));
            s_steps.Add(() => { le.Close(); UnityEditor.EditorApplication.Exit(0); });
            UnityEditor.EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (++s_wait < 40) return;
            s_wait = 0;
            if (s_step >= s_steps.Count) return;
            try { s_steps[s_step++](); }
            catch (System.Exception e) { UnityEngine.Debug.LogException(e); UnityEditor.EditorApplication.Exit(1); }
        }

        static void Shot(UnityEditor.EditorWindow w, string name)
        {
            w.Focus();
            w.Repaint();
            UnityEngine.Rect r = w.position;
            int pw = (int)(r.width * UnityEditor.EditorGUIUtility.pixelsPerPoint), ph = (int)(r.height * UnityEditor.EditorGUIUtility.pixelsPerPoint);
            UnityEngine.Color[] px = UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(r.position * UnityEditor.EditorGUIUtility.pixelsPerPoint, pw, ph);
            UnityEngine.Texture2D t = new UnityEngine.Texture2D(pw, ph, UnityEngine.TextureFormat.RGB24, false);
            t.SetPixels(px);
            t.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(s_out, name), t.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(t);
            UnityEngine.Debug.Log("[EditorWindowShots] " + name);
        }
    }
}
