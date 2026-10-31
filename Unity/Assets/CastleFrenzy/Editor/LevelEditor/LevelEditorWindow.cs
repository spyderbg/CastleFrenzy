// Level Editor for Unity: the Castle Frenzy level editor (Tools/LevelEditor) as an EditorWindow.
// Waves/days/challenges with a timeline, per-level weapon loadouts, 3D model previews drawn by the game's
// renderer, spawn previews (and the live spawn list while playing), and "Play day" straight into the game.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static CastleFrenzy.GE;

namespace CastleFrenzy.EditorTools
{
    public class LevelEditorWindow : EditorWindow
    {
        enum Tab { Waves, Weapons, Models, Spawns }

        class LevelData
        {
            public Level info = new Level();
            public List<Day> days = new List<Day>();

            public LevelData Clone()
            {
                LevelData c = new LevelData();
                Array.Copy(info.weaponStartingLevel, c.info.weaponStartingLevel, info.weaponStartingLevel.Length);
                Array.Copy(info.weaponAvailableInShop, c.info.weaponAvailableInShop, info.weaponAvailableInShop.Length);
                c.info.spellAvailableInShop = info.spellAvailableInShop;
                c.info.spellBought = info.spellBought;
                foreach (Day d in days) c.days.Add(LevelInfo.cloneDay(d));
                return c;
            }
        }

        static readonly string[] k_weaponNames = { "Gate", "Hydra", "Fireball", "Iceball", "Griffin" };
        static readonly string[] k_weaponLevels = { "None", "Level 1", "Level 2", "Level 3" };
        static readonly string[] k_spells = { "None", "Boulder", "Lightning", "Ghost army", "Tornado" };
        static readonly string[] k_difficulties = { "Easy", "Medium", "Hard" };

        List<LevelData> m_levels;
        List<List<LevelData>> m_undo = new List<List<LevelData>>(), m_redo = new List<List<LevelData>>();
        string m_committed;
        string m_savedState;
        [SerializeField] Tab m_tab;
        [SerializeField] int m_level, m_day, m_wave = -1, m_playDifficulty = 1;
        [SerializeField] bool m_skipCutscene = true;
        [SerializeField] float m_timelineZoom = 1f;
        Day m_clipboard;
        string[] m_unitNames = new string[0], m_challengeNames = new string[0];
        Vector2 m_levelScroll, m_dayScroll, m_waveScroll, m_detailScroll, m_spawnScroll, m_modelScroll;
        int m_spawnSeed = 1;

        // timeline drag
        int m_dragWave = -1, m_dragButton, m_dragStartValue;
        float m_dragStartX;

        // models
        EditorGameRenderer m_renderer;
        string[] m_modelIds = new string[0];
        [SerializeField] int m_model;
        ISceneNode m_modelNode;
        string m_modelLoaded;
        CCameraSceneNode m_modelCam;
        CLightSceneNode m_modelLight;
        float m_yaw = 200, m_pitch = 15, m_distance = 6, m_speed = 1;
        v3f m_target = new v3f(0, 1, 0);
        bool m_animPlaying = true;
        int m_clip;
        double m_lastTime;

        [MenuItem("Castle Frenzy/Level Editor", priority = 11)]
        public static void Open() { GetWindow<LevelEditorWindow>("Level Editor").Show(); }

        /// <summary>Selects a tab (0 waves, 1 weapons, 2 models, 3 spawns), level, day and wave; used by tests.</summary>
        public void Select(int tab, int level, int day, int wave)
        {
            m_tab = (Tab)tab;
            m_level = level;
            m_day = day;
            m_wave = wave;
            ClampSelection();
            Repaint();
        }

        void OnEnable()
        {
            titleContent = new GUIContent("Level Editor");
            wantsMouseMove = true;
            EditorApplication.update += OnEditorUpdate;
            Reload();
        }

        void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            DisposeModel();
            if (m_renderer != null) { m_renderer.Dispose(); m_renderer = null; }
        }

        void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - m_lastTime);
            m_lastTime = now;
            if (m_tab == Tab.Models && m_animPlaying && m_modelNode != null && EditorGameRenderer.Available)
            {
                CSceneManager.Instance.update(Mathf.Min(dt, 0.1f) * 1000f * m_speed);
                Repaint();
            }
            else if (m_tab == Tab.Spawns && Application.isPlaying) Repaint();
        }

        // ------------------------------------------------------------------ load / save

        void Reload()
        {
            try
            {
                LevelInfo.LevelsRoot = null;
                // While the game runs it owns the shared config; only (re)import it in edit mode.
                if (!Application.isPlaying) CGameConfig.GetInstance().clear();
                if (!Application.isPlaying || LevelInfo.g_unitTypes.Count == 0) LevelInfo.initUnitTypes();
                m_unitNames = LevelInfo.g_unitTypes.Select(u => u.name).ToArray();
                m_challengeNames = Enumerable.Range(0, CT_Count).Select(i => Challenge.ChallengeTypeToString(i)).ToArray();
                m_modelIds = CGameConfig.GetInstance().getAttributesMap("model").Keys.ToArray();
                List<int> days = LevelInfo.loadNumberOfDaysPerLevel();
                m_levels = new List<LevelData>();
                for (int l = 0; l < days.Count; l++)
                {
                    LevelData ld = new LevelData { info = LevelInfo.loadLevelInfo(LevelInfo.getLevelPath(l)) };
                    for (int d = 0; d < days[l]; d++) ld.days.Add(LevelInfo.loadDayInfo(LevelInfo.getDayPath(l, d)) ?? NewDay());
                    m_levels.Add(ld);
                }
                m_undo.Clear();
                m_redo.Clear();
                m_committed = m_savedState = Fingerprint();
                ClampSelection();
            }
            catch (Exception e)
            {
                m_levels = null;
                Debug.LogException(e);
            }
        }

        static Day NewDay() { return new Day { dayEndTime = 60, challenge = CT_None, difficulty = 0 }; }

        string Fingerprint()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (LevelData l in m_levels)
            {
                sb.Append('L').Append(string.Join(",", l.info.weaponStartingLevel)).Append('|').Append(string.Join(",", l.info.weaponAvailableInShop))
                  .Append('|').Append(l.info.spellAvailableInShop).Append(l.info.spellBought);
                foreach (Day d in l.days)
                {
                    sb.Append('D').Append(d.dayEndTime).Append(',').Append(d.challenge).Append(',').Append(d.difficulty);
                    foreach (Wave w in d.waves)
                    {
                        sb.Append('W').Append(w.name).Append(',').Append(w.timeStart).Append(',').Append(w.timeInterval).Append(',').Append(w.repeatInterval).Append(',').Append(w.repeatCount).Append(w.packed);
                        foreach (Variant v in w.variants)
                        {
                            sb.Append('V').Append(v.intro);
                            foreach (EnemyType e in v.enemyTypes) sb.Append('E').Append(e.unitType).Append(',').Append(e.minCount).Append(',').Append(e.maxCount);
                        }
                    }
                }
            }
            return sb.ToString();
        }

        bool Dirty { get { return m_levels != null && m_committed != m_savedState; } }

        void Commit()
        {
            string now = Fingerprint();
            if (now == m_committed) return;
            m_committed = now;
            m_redo.Clear();
        }

        /// <summary>Call before a structural change: records an undo snapshot.</summary>
        void Record()
        {
            m_undo.Add(m_levels.Select(l => l.Clone()).ToList());
            if (m_undo.Count > 100) m_undo.RemoveAt(0);
            m_redo.Clear();
        }

        void Undo()
        {
            if (m_undo.Count == 0) return;
            m_redo.Add(m_levels.Select(l => l.Clone()).ToList());
            m_levels = m_undo[m_undo.Count - 1];
            m_undo.RemoveAt(m_undo.Count - 1);
            m_committed = Fingerprint();
            ClampSelection();
        }

        void Redo()
        {
            if (m_redo.Count == 0) return;
            m_undo.Add(m_levels.Select(l => l.Clone()).ToList());
            m_levels = m_redo[m_redo.Count - 1];
            m_redo.RemoveAt(m_redo.Count - 1);
            m_committed = Fingerprint();
            ClampSelection();
        }

        List<string> Validate()
        {
            List<string> errors = new List<string>();
            if (m_levels.Count < 2) errors.Add("The game needs at least one campaign level and the Endless level.");
            for (int l = 0; l < m_levels.Count; l++)
            {
                if (m_levels[l].days.Count == 0) errors.Add("Level " + (l + 1) + " has no days.");
                for (int d = 0; d < m_levels[l].days.Count; d++)
                {
                    Day day = m_levels[l].days[d];
                    string where = "Level " + (l + 1) + " day " + (d + 1);
                    if (day.dayEndTime <= 0) errors.Add(where + ": day end time must be positive.");
                    if (day.waves.Count == 0) errors.Add(where + ": no waves.");
                    foreach (Wave w in day.waves)
                    {
                        if (w.repeatCount < 1) errors.Add(where + ", wave '" + w.name + "': repeat count must be at least 1.");
                        if (w.variants.Count == 0) errors.Add(where + ", wave '" + w.name + "': no variants (the wave never spawns).");
                        foreach (Variant v in w.variants)
                            foreach (EnemyType e in v.enemyTypes)
                                if (e.minCount > e.maxCount || e.minCount < 0) errors.Add(where + ", wave '" + w.name + "': min/max counts are inverted.");
                    }
                }
            }
            return errors;
        }

        void Save()
        {
            List<string> errors = Validate();
            if (errors.Count > 0 && !EditorUtility.DisplayDialog("Level Editor", "Problems found:\n\n" + string.Join("\n", errors.Take(12)) + "\n\nSave anyway?", "Save", "Cancel")) return;
            string levelsDir = GameDataTools.UnityPath("Levels");
            Dictionary<string, byte[]> before = Directory.GetFiles(levelsDir, "*.xml").ToDictionary(f => Path.GetFileName(f), f => File.ReadAllBytes(f));
            for (int l = 0; l < m_levels.Count; l++)
            {
                LevelInfo.saveLevelInfo(LevelInfo.getLevelPath(l), m_levels[l].info);
                for (int d = 0; d < m_levels[l].days.Count; d++) LevelInfo.saveDayInfo(LevelInfo.getDayPath(l, d), m_levels[l].days[d]);
            }
            LevelInfo.saveNumberOfDaysPerLevel(m_levels.Select(l => l.days.Count).ToList());
            HashSet<string> keep = new HashSet<string> { "info.xml" };
            for (int l = 0; l < m_levels.Count; l++)
            {
                keep.Add(Path.GetFileName(LevelInfo.getLevelPath(l)));
                for (int d = 0; d < m_levels[l].days.Count; d++) keep.Add(Path.GetFileName(LevelInfo.getDayPath(l, d)));
            }
            int changed = 0, removed = 0;
            foreach (string f in Directory.GetFiles(levelsDir, "level*.xml"))
                if (!keep.Contains(Path.GetFileName(f))) { File.Delete(f); if (File.Exists(f + ".meta")) File.Delete(f + ".meta"); removed++; }
            // Mirror the changed files into Build/bin/Levels so the C++ game and the original tools agree.
            string mirror = GameDataTools.BuildBin + "/Levels";
            foreach (string f in Directory.GetFiles(levelsDir, "*.xml"))
            {
                string name = Path.GetFileName(f);
                byte[] now = File.ReadAllBytes(f);
                byte[] old;
                bool fileChanged = !before.TryGetValue(name, out old) || !old.SequenceEqual(now);
                if (fileChanged) changed++;
                if (Directory.Exists(mirror) && (fileChanged || !File.Exists(mirror + "/" + name))) File.WriteAllBytes(mirror + "/" + name, now);
            }
            if (Directory.Exists(mirror))
                foreach (string f in Directory.GetFiles(mirror, "level*.xml"))
                    if (!keep.Contains(Path.GetFileName(f))) File.Delete(f);
            LevelInfo.ReloadNumberOfDaysPerLevel();
            m_savedState = m_committed = Fingerprint();
            AssetDatabase.Refresh();
            ShowNotification(new GUIContent("Saved (" + changed + " changed, " + removed + " removed)"));
        }

        void ClampSelection()
        {
            if (m_levels == null || m_levels.Count == 0) { m_level = m_day = 0; m_wave = -1; return; }
            m_level = Mathf.Clamp(m_level, 0, m_levels.Count - 1);
            m_day = Mathf.Clamp(m_day, 0, Mathf.Max(0, m_levels[m_level].days.Count - 1));
            Day day = CurrentDay;
            m_wave = day == null ? -1 : Mathf.Clamp(m_wave, -1, day.waves.Count - 1);
        }

        LevelData CurrentLevel { get { return m_levels != null && m_level < m_levels.Count ? m_levels[m_level] : null; } }
        Day CurrentDay { get { LevelData l = CurrentLevel; return l != null && m_day < l.days.Count ? l.days[m_day] : null; } }
        bool IsEndless(int level) { return level == m_levels.Count - 1; }

        // ------------------------------------------------------------------ GUI

        void OnGUI()
        {
            HandleShortcuts();
            Toolbar();
            if (m_levels == null)
            {
                EditorGUILayout.HelpBox("Could not load StreamingAssets/GameData/Levels (see Console).", MessageType.Error);
                if (GUILayout.Button("Retry")) Reload();
                return;
            }
            GUILayout.BeginHorizontal();
            LevelAndDayColumn();
            GUILayout.BeginVertical();
            m_tab = (Tab)GUILayout.Toolbar((int)m_tab, new[] { "Waves", "Weapons", "Models", "Spawns" });
            switch (m_tab)
            {
                case Tab.Waves: WavesTab(); break;
                case Tab.Weapons: WeaponsTab(); break;
                case Tab.Models: ModelsTab(); break;
                case Tab.Spawns: SpawnsTab(); break;
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            if (GUI.changed) Commit();
        }

        void HandleShortcuts()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown) return;
            bool ctrl = e.control || e.command;
            if (ctrl && e.keyCode == KeyCode.S) { Save(); e.Use(); }
            else if (ctrl && e.keyCode == KeyCode.Z && !EditorGUIUtility.editingTextField) { Undo(); e.Use(); }
            else if (ctrl && e.keyCode == KeyCode.Y && !EditorGUIUtility.editingTextField) { Redo(); e.Use(); }
        }

        void Toolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUI.enabled = m_levels != null;
            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(44))) Save();
            GUI.enabled = true;
            if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(52)))
                if (!Dirty || EditorUtility.DisplayDialog("Level Editor", "Discard unsaved changes?", "Discard", "Cancel")) Reload();
            GUI.enabled = m_undo.Count > 0;
            if (GUILayout.Button("Undo", EditorStyles.toolbarButton, GUILayout.Width(44))) Undo();
            GUI.enabled = m_redo.Count > 0;
            if (GUILayout.Button("Redo", EditorStyles.toolbarButton, GUILayout.Width(44))) Redo();
            GUI.enabled = m_levels != null;
            GUILayout.Space(12);
            GUILayout.Label("Difficulty", EditorStyles.miniLabel, GUILayout.Width(52));
            m_playDifficulty = EditorGUILayout.Popup(m_playDifficulty, k_difficulties, EditorStyles.toolbarPopup, GUILayout.Width(70));
            m_skipCutscene = GUILayout.Toggle(m_skipCutscene, "Skip cutscene", EditorStyles.toolbarButton, GUILayout.Width(90));
            if (GUILayout.Button("▶ Play day", EditorStyles.toolbarButton, GUILayout.Width(74)))
            {
                if (Dirty) Save();
                GameDataTools.PlayLevel(m_level, m_day, m_playDifficulty, IsEndless(m_level), m_skipCutscene);
            }
            if (GUILayout.Button("▶ Play level", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                if (Dirty) Save();
                GameDataTools.PlayLevel(m_level, 0, m_playDifficulty, IsEndless(m_level), m_skipCutscene);
            }
            GUI.enabled = Application.isPlaying;
            if (GUILayout.Button("■ Stop", EditorStyles.toolbarButton, GUILayout.Width(50))) EditorApplication.isPlaying = false;
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            if (m_levels != null) GUILayout.Label((Dirty ? "● unsaved  " : "") + m_levels.Count + " levels · " + m_levels.Sum(l => l.days.Count) + " days", EditorStyles.miniLabel);
            GUILayout.EndHorizontal();
        }

        // ------------------------------------------------------------------ levels & days

        void LevelAndDayColumn()
        {
            GUILayout.BeginVertical(GUILayout.Width(210));
            EditorGUILayout.LabelField("Levels", EditorStyles.boldLabel);
            m_levelScroll = EditorGUILayout.BeginScrollView(m_levelScroll, GUILayout.Height(Mathf.Max(120, position.height * 0.36f)));
            for (int l = 0; l < m_levels.Count; l++)
            {
                string label = (IsEndless(l) ? "Endless" : "Level " + (l + 1)) + "  (" + m_levels[l].days.Count + " days)";
                if (GUILayout.Toggle(l == m_level, label, "Button") && l != m_level) { m_level = l; m_day = 0; m_wave = -1; GUI.FocusControl(null); }
            }
            EditorGUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("New", EditorStyles.miniButtonLeft))
            {
                Record();
                LevelData nl = new LevelData();
                for (int i = 0; i < WT_Count - 1; i++) { nl.info.weaponStartingLevel[i] = WL_None; nl.info.weaponAvailableInShop[i] = true; }
                nl.days.Add(NewDay());
                int at = Mathf.Max(0, m_levels.Count - 1);
                m_levels.Insert(at, nl);
                m_level = at;
                m_day = 0;
            }
            GUI.enabled = !IsEndless(m_level) && m_levels.Count > 2;
            if (GUILayout.Button("Delete", EditorStyles.miniButtonMid) && EditorUtility.DisplayDialog("Level Editor", "Delete level " + (m_level + 1) + " and its days?", "Delete", "Cancel"))
            {
                Record();
                m_levels.RemoveAt(m_level);
                ClampSelection();
            }
            GUI.enabled = m_level > 0;
            if (GUILayout.Button("▲", EditorStyles.miniButtonMid)) { Record(); Swap(m_levels, m_level, m_level - 1); m_level--; }
            GUI.enabled = m_level < m_levels.Count - 1;
            if (GUILayout.Button("▼", EditorStyles.miniButtonRight)) { Record(); Swap(m_levels, m_level, m_level + 1); m_level++; }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("The last level is always Endless (its days are the random segments).", MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(IsEndless(m_level) ? "Endless segments" : "Days", EditorStyles.boldLabel);
            LevelData lvl = CurrentLevel;
            m_dayScroll = EditorGUILayout.BeginScrollView(m_dayScroll);
            for (int d = 0; d < lvl.days.Count; d++)
            {
                Day day = lvl.days[d];
                string label = "Day " + (d + 1) + "  ·  " + day.waves.Count + " waves" + (day.challenge != CT_None ? "  ★" : "") + (IsEndless(m_level) ? "  (" + k_difficulties[Mathf.Clamp(day.difficulty, 0, 2)] + ")" : "");
                if (GUILayout.Toggle(d == m_day, label, "Button") && d != m_day) { m_day = d; m_wave = -1; GUI.FocusControl(null); }
            }
            EditorGUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("New", EditorStyles.miniButtonLeft)) { Record(); lvl.days.Insert(m_day + 1, NewDay()); m_day++; m_wave = -1; }
            if (GUILayout.Button("Copy", EditorStyles.miniButtonMid) && CurrentDay != null) m_clipboard = LevelInfo.cloneDay(CurrentDay);
            GUI.enabled = m_clipboard != null;
            if (GUILayout.Button("Paste", EditorStyles.miniButtonMid)) { Record(); lvl.days.Insert(m_day + 1, LevelInfo.cloneDay(m_clipboard)); m_day++; }
            GUI.enabled = lvl.days.Count > 1;
            if (GUILayout.Button("Cut", EditorStyles.miniButtonMid)) { Record(); m_clipboard = LevelInfo.cloneDay(CurrentDay); lvl.days.RemoveAt(m_day); ClampSelection(); }
            GUI.enabled = m_day > 0;
            if (GUILayout.Button("▲", EditorStyles.miniButtonMid)) { Record(); Swap(lvl.days, m_day, m_day - 1); m_day--; }
            GUI.enabled = m_day < lvl.days.Count - 1;
            if (GUILayout.Button("▼", EditorStyles.miniButtonRight)) { Record(); Swap(lvl.days, m_day, m_day + 1); m_day++; }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        static void Swap<T>(List<T> list, int a, int b) { T t = list[a]; list[a] = list[b]; list[b] = t; }

        // ------------------------------------------------------------------ waves

        void WavesTab()
        {
            Day day = CurrentDay;
            if (day == null) return;
            GUILayout.BeginHorizontal();
            int end = EditorGUILayout.IntField("Day end time (s)", day.dayEndTime, GUILayout.Width(260));
            if (end != day.dayEndTime) { Record(); day.dayEndTime = Mathf.Max(1, end); }
            int ch = EditorGUILayout.Popup("Challenge", Mathf.Clamp(day.challenge, 0, m_challengeNames.Length - 1), m_challengeNames, GUILayout.Width(340));
            if (ch != day.challenge) { Record(); day.challenge = ch; }
            if (IsEndless(m_level))
            {
                int diff = EditorGUILayout.Popup(Mathf.Clamp(day.difficulty, 0, 2), k_difficulties, GUILayout.Width(80));
                if (diff != day.difficulty) { Record(); day.difficulty = diff; }
            }
            GUILayout.EndHorizontal();
            if (day.challenge != CT_None) EditorGUILayout.LabelField("Challenge text", _ChallengeText(day.challenge), EditorStyles.wordWrappedMiniLabel);

            Rect tl = GUILayoutUtility.GetRect(10, 10, Mathf.Max(80, day.waves.Count * 18 + 26), Mathf.Max(80, day.waves.Count * 18 + 26), GUILayout.ExpandWidth(true));
            Timeline(tl, day);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Zoom", GUILayout.Width(36));
            m_timelineZoom = GUILayout.HorizontalSlider(m_timelineZoom, 0.5f, 6f, GUILayout.Width(120));
            GUILayout.Label("Left-drag: start · Right-drag: duration · Middle-drag: repeat spacing · Wheel: repeat count", EditorStyles.miniLabel);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(230));
            EditorGUILayout.LabelField("Waves", EditorStyles.boldLabel);
            m_waveScroll = EditorGUILayout.BeginScrollView(m_waveScroll);
            for (int i = 0; i < day.waves.Count; i++)
            {
                Wave w = day.waves[i];
                string label = (string.IsNullOrEmpty(w.name) ? "(unnamed)" : w.name) + "  @" + w.timeStart + "s ×" + w.repeatCount;
                if (GUILayout.Toggle(i == m_wave, label, "Button") && i != m_wave) { m_wave = i; GUI.FocusControl(null); }
            }
            EditorGUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("New", EditorStyles.miniButtonLeft))
            {
                Record();
                Wave nw = new Wave { name = "wave" + (day.waves.Count + 1), timeStart = 0, timeInterval = 5, repeatInterval = 10, repeatCount = 1 };
                nw.variants.Add(new Variant { enemyTypes = { new EnemyType { unitType = Mathf.Max(0, Array.IndexOf(m_unitNames, "Goblin")), minCount = 1, maxCount = 2 } } });
                day.waves.Insert(m_wave + 1, nw);
                m_wave++;
            }
            GUI.enabled = m_wave >= 0;
            if (GUILayout.Button("Clone", EditorStyles.miniButtonMid)) { Record(); day.waves.Insert(m_wave + 1, LevelInfo.cloneWave(day.waves[m_wave])); m_wave++; }
            if (GUILayout.Button("Delete", EditorStyles.miniButtonMid)) { Record(); day.waves.RemoveAt(m_wave); m_wave = Mathf.Min(m_wave, day.waves.Count - 1); }
            GUI.enabled = m_wave > 0;
            if (GUILayout.Button("▲", EditorStyles.miniButtonMid)) { Record(); Swap(day.waves, m_wave, m_wave - 1); m_wave--; }
            GUI.enabled = m_wave >= 0 && m_wave < day.waves.Count - 1;
            if (GUILayout.Button("▼", EditorStyles.miniButtonRight)) { Record(); Swap(day.waves, m_wave, m_wave + 1); m_wave++; }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            m_detailScroll = EditorGUILayout.BeginScrollView(m_detailScroll);
            if (m_wave >= 0 && m_wave < day.waves.Count) WaveDetails(day.waves[m_wave]);
            else EditorGUILayout.HelpBox("Select a wave.", MessageType.None);
            EditorGUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        static string _ChallengeText(int type)
        {
            try { return CStringTable.GetInstance().GetText(STRING_IDS.CHALLENGES_DESCRIPTION_0 + type) ?? ""; }
            catch (Exception) { return ""; }
        }

        void WaveDetails(Wave w)
        {
            EditorGUI.BeginChangeCheck();
            string name = EditorGUILayout.TextField("Name", w.name);
            bool packed = EditorGUILayout.Toggle(new GUIContent("Packed", "All enemies of a variant spawn together instead of spread over the duration"), w.packed);
            int start = EditorGUILayout.IntField("Start (s)", w.timeStart);
            int dur = EditorGUILayout.IntField("Duration (s)", w.timeInterval);
            int rep = EditorGUILayout.IntField("Repeat interval (s)", w.repeatInterval);
            int cnt = EditorGUILayout.IntField("Repeat count", w.repeatCount);
            if (EditorGUI.EndChangeCheck())
            {
                Record();
                w.name = name; w.packed = packed;
                w.timeStart = Mathf.Max(0, start); w.timeInterval = Mathf.Max(0, dur);
                w.repeatInterval = Mathf.Max(0, rep); w.repeatCount = Mathf.Max(1, cnt);
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Variants (one is picked at random for each repeat)", EditorStyles.boldLabel);
            for (int vi = 0; vi < w.variants.Count; vi++)
            {
                Variant v = w.variants[vi];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Variant " + (vi + 1), EditorStyles.boldLabel, GUILayout.Width(70));
                bool intro = GUILayout.Toggle(v.intro, new GUIContent("Intro camera", "Show the unit introduction when it reaches the castle"));
                if (intro != v.intro) { Record(); v.intro = intro; }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Remove variant", EditorStyles.miniButton)) { Record(); w.variants.RemoveAt(vi); GUILayout.EndHorizontal(); EditorGUILayout.EndVertical(); break; }
                GUILayout.EndHorizontal();
                for (int ei = 0; ei < v.enemyTypes.Count; ei++)
                {
                    EnemyType e = v.enemyTypes[ei];
                    GUILayout.BeginHorizontal();
                    EditorGUI.BeginChangeCheck();
                    int ut = EditorGUILayout.Popup(Mathf.Clamp(e.unitType, 0, m_unitNames.Length - 1), m_unitNames, GUILayout.MinWidth(120));
                    GUILayout.Label("min", GUILayout.Width(26));
                    int mn = EditorGUILayout.IntField(e.minCount, GUILayout.Width(36));
                    GUILayout.Label("max", GUILayout.Width(28));
                    int mx = EditorGUILayout.IntField(e.maxCount, GUILayout.Width(36));
                    if (EditorGUI.EndChangeCheck()) { Record(); e.unitType = ut; e.minCount = Mathf.Max(0, mn); e.maxCount = Mathf.Max(0, mx); }
                    if (UnitHas(e.unitType, "MindControl")) GUILayout.Label(new GUIContent("mind control", "Count is the number of controlled slaves"), EditorStyles.miniLabel, GUILayout.Width(70));
                    else if (UnitHas(e.unitType, "Rider")) GUILayout.Label("+ rider", EditorStyles.miniLabel, GUILayout.Width(70));
                    if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22))) { Record(); v.enemyTypes.RemoveAt(ei); GUILayout.EndHorizontal(); break; }
                    GUILayout.EndHorizontal();
                }
                if (GUILayout.Button("Add enemy type", EditorStyles.miniButton)) { Record(); v.enemyTypes.Add(new EnemyType { unitType = 0, minCount = 1, maxCount = 1 }); }
                EditorGUILayout.EndVertical();
            }
            if (GUILayout.Button("Add variant")) { Record(); w.variants.Add(new Variant()); }
        }

        bool UnitHas(int unitType, string attr)
        {
            return unitType >= 0 && unitType < LevelInfo.g_unitTypes.Count && LevelInfo.g_unitTypes[unitType].attributes.existsAttribute(attr);
        }

        void Timeline(Rect r, Day day)
        {
            EditorGUI.DrawRect(r, new Color(0.13f, 0.13f, 0.13f));
            int maxTime = Mathf.Max(day.dayEndTime, LevelInfo.getDayMaxTime(day)) + 5;
            float scale = (r.width - 8) / maxTime * m_timelineZoom;
            float x0 = r.x + 4;
            for (int t = 0; t <= maxTime; t += 10)
            {
                float x = x0 + t * scale;
                if (x > r.xMax) break;
                EditorGUI.DrawRect(new Rect(x, r.y, 1, r.height), new Color(1, 1, 1, 0.06f));
                GUI.Label(new Rect(x + 2, r.y, 40, 14), t + "s", EditorStyles.miniLabel);
            }
            float endX = x0 + day.dayEndTime * scale;
            if (endX < r.xMax) EditorGUI.DrawRect(new Rect(endX, r.y, 2, r.height), new Color(1f, 0.35f, 0.3f, 0.9f));
            Event e = Event.current;
            for (int i = 0; i < day.waves.Count; i++)
            {
                Wave w = day.waves[i];
                float y = r.y + 16 + i * 18;
                Rect row = new Rect(r.x, y, r.width, 16);
                Color c = i == m_wave ? new Color(1f, 0.75f, 0.2f) : new Color(0.35f, 0.65f, 1f);
                for (int k = 0; k < Mathf.Max(1, w.repeatCount); k++)
                {
                    float s = x0 + (w.timeStart + k * w.repeatInterval) * scale;
                    float len = Mathf.Max(3, w.timeInterval * scale);
                    if (s > r.xMax) break;
                    EditorGUI.DrawRect(new Rect(s, y + 2, Mathf.Min(len, r.xMax - s), 12), (w.timeStart + k * w.repeatInterval) < day.dayEndTime ? c : c * 0.45f);
                }
                GUI.Label(new Rect(x0 + w.timeStart * scale + 3, y, 200, 16), w.name, EditorStyles.whiteMiniLabel);
                if (e.type == EventType.MouseDown && row.Contains(e.mousePosition))
                {
                    m_wave = i;
                    Record();
                    m_dragWave = i;
                    m_dragButton = e.button;
                    m_dragStartX = e.mousePosition.x;
                    m_dragStartValue = e.button == 1 ? w.timeInterval : e.button == 2 ? w.repeatInterval : w.timeStart;
                    e.Use();
                }
                else if (e.type == EventType.ScrollWheel && row.Contains(e.mousePosition))
                {
                    Record();
                    w.repeatCount = Mathf.Max(1, w.repeatCount + (e.delta.y < 0 ? 1 : -1));
                    m_wave = i;
                    Commit();
                    e.Use();
                }
            }
            if (m_dragWave >= 0 && m_dragWave < day.waves.Count)
            {
                if (e.type == EventType.MouseDrag)
                {
                    Wave w = day.waves[m_dragWave];
                    int v = Mathf.Max(0, m_dragStartValue + Mathf.RoundToInt((e.mousePosition.x - m_dragStartX) / scale));
                    if (m_dragButton == 1) w.timeInterval = v;
                    else if (m_dragButton == 2) w.repeatInterval = v;
                    else w.timeStart = v;
                    e.Use();
                    Repaint();
                }
                else if (e.type == EventType.MouseUp)
                {
                    m_dragWave = -1;
                    Commit();
                    e.Use();
                }
            }
        }

        // ------------------------------------------------------------------ weapons

        void WeaponsTab()
        {
            LevelData lvl = CurrentLevel;
            if (lvl == null) return;
            EditorGUILayout.LabelField((IsEndless(m_level) ? "Endless" : "Level " + (m_level + 1)) + " loadout", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Starting level is what the player owns on day 1; shop availability decides whether it can be bought. UI levels None/1/2/3 are stored as -1/0/1/2, like the game.", MessageType.None);
            for (int i = 0; i < WT_Count - 1; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(k_weaponNames[i], GUILayout.Width(80));
                int sl = EditorGUILayout.Popup(lvl.info.weaponStartingLevel[i] + 1, k_weaponLevels, GUILayout.Width(100)) - 1;
                bool av = GUILayout.Toggle(lvl.info.weaponAvailableInShop[i], "Available in shop", GUILayout.Width(130));
                if (sl != lvl.info.weaponStartingLevel[i] || av != lvl.info.weaponAvailableInShop[i])
                {
                    Record();
                    lvl.info.weaponStartingLevel[i] = sl;
                    lvl.info.weaponAvailableInShop[i] = av;
                }
                GUILayout.EndHorizontal();
            }
            EditorGUILayout.Space();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Spell", GUILayout.Width(80));
            int sp = EditorGUILayout.Popup(lvl.info.spellAvailableInShop + 1, k_spells, GUILayout.Width(100)) - 1;
            bool bought = GUILayout.Toggle(lvl.info.spellBought, "Spell bought", GUILayout.Width(130));
            if (sp != lvl.info.spellAvailableInShop || bought != lvl.info.spellBought) { Record(); lvl.info.spellAvailableInShop = sp; lvl.info.spellBought = bought; }
            GUILayout.EndHorizontal();
        }

        // ------------------------------------------------------------------ models (game renderer)

        void DisposeModel()
        {
            if (m_modelNode != null) { m_modelNode.remove(); m_modelNode = null; }
            if (m_modelCam != null) { m_modelCam.remove(); m_modelCam = null; }
            if (m_modelLight != null) { m_modelLight.remove(); m_modelLight = null; }
            m_modelLoaded = null;
        }

        void EnsureModelLoaded()
        {
            if (m_modelIds.Length == 0) return;
            m_model = Mathf.Clamp(m_model, 0, m_modelIds.Length - 1);
            string id = m_modelIds[m_model];
            if (m_modelLoaded == id && m_modelNode != null) return;
            DisposeModel();
            EditorGameRenderer.EnsureRuntime();
            CSceneManager scene = CSceneManager.Instance;
            IAttributes a = CGameConfig.GetInstance().getAttributes("model", id);
            string file = a != null ? a.getAttributeAsString("File") : "";
            if (string.IsNullOrEmpty(file)) return;
            m_modelNode = G.LoadColladaScene(file, true);
            if (m_modelNode == null) return;
            G.cleanNode(m_modelNode, true);
            float scale = a.existsAttribute("Scale") ? a.getAttributeAsFloat("Scale") : 1f;
            m_modelNode.setScale(new v3f(scale, scale, scale));
            scene.getRootSceneNode().addChild(m_modelNode);
            m_modelLight = scene.addLightSceneNode();
            m_modelLight.getLightData().AmbientColor = new SColorf(1, 1, 1, 1);
            m_modelLight.getLightData().DiffuseColor = new SColorf(0, 0, 0, 1);
            m_modelCam = scene.addCameraSceneNode();
            m_modelCam.setAspectRatio(480f / 320f);
            m_modelLoaded = id;
            m_clip = 0;
            CTimelineController tc = m_modelNode.getFirstAnimator() != null ? G.Timeline(m_modelNode) : null;
            if (tc != null && tc.getClipCount() > 0) { tc.setClip(0); tc.setLoop(true); }
            FitModel();
        }

        void FitModel()
        {
            if (m_modelNode == null) return;
            m_modelNode.updateAbsolutePosition(true);
            aabbox3df box = new aabbox3df(v3f.Zero);
            bool any = false;
            Action<ISceneNode> visit = null;
            visit = n =>
            {
                if (n.getMaterialCount() > 0)
                {
                    aabbox3df b = n.getTransformedBoundingBox();
                    if (!any) { box = b; any = true; }
                    else { box.addInternalPoint(b.MinEdge); box.addInternalPoint(b.MaxEdge); }
                }
                foreach (ISceneNode c in n.getChildren()) visit(c);
            };
            visit(m_modelNode);
            if (!any) { m_target = new v3f(0, 1, 0); m_distance = 6; return; }
            m_target = box.getCenter();
            m_distance = Mathf.Max(1.5f, box.getExtent().getLength() * 1.4f);
        }

        void ModelsTab()
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(170));
            m_modelScroll = EditorGUILayout.BeginScrollView(m_modelScroll);
            for (int i = 0; i < m_modelIds.Length; i++)
                if (GUILayout.Toggle(i == m_model, m_modelIds[i], "Button") && i != m_model) { m_model = i; m_modelLoaded = null; }
            EditorGUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            if (!EditorGameRenderer.Available)
            {
                EditorGUILayout.HelpBox("Model previews use the game renderer and pause while the game runs in Play mode.", MessageType.Info);
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
                return;
            }
            EnsureModelLoaded();
            CTimelineController tc = m_modelNode != null && m_modelNode.getFirstAnimator() != null ? G.Timeline(m_modelNode) : null;
            GUILayout.BeginHorizontal();
            if (tc != null && tc.getClipCount() > 0)
            {
                string[] clips = Enumerable.Range(0, tc.getClipCount()).Select(i => tc.getClipName(i) ?? ("clip " + i)).ToArray();
                int c = EditorGUILayout.Popup(Mathf.Clamp(m_clip, 0, clips.Length - 1), clips, GUILayout.Width(200));
                if (c != m_clip) { m_clip = c; tc.setClip(c); tc.setLoop(true); }
            }
            m_animPlaying = GUILayout.Toggle(m_animPlaying, m_animPlaying ? "❚❚ Pause" : "▶ Play", EditorStyles.miniButton, GUILayout.Width(70));
            GUILayout.Label("Speed", GUILayout.Width(40));
            m_speed = GUILayout.HorizontalSlider(m_speed, 0f, 3f, GUILayout.Width(90));
            if (GUILayout.Button("Fit", EditorStyles.miniButton, GUILayout.Width(36))) FitModel();
            GUILayout.EndHorizontal();
            if (tc != null && tc.getClipCount() > 0)
            {
                int len = Mathf.Max(1, tc.getCurrentClipLength());
                float t = (tc.getCtrlTime() - tc.getCurrentClipStart()) / (float)len;
                float nt = EditorGUILayout.Slider("Time", Mathf.Clamp01(t), 0, 1);
                if (Mathf.Abs(nt - t) > 0.001f) { tc.jumpTo(tc.getCurrentClipStart() + (int)(nt * len)); CSceneManager.Instance.update(0); }
            }
            Rect r = GUILayoutUtility.GetRect(10, 10, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            ModelViewport(r);
            IAttributes a = m_modelIds.Length > 0 ? CGameConfig.GetInstance().getAttributes("model", m_modelIds[m_model]) : null;
            if (a != null) EditorGUILayout.LabelField("File", a.getAttributeAsString("File") + "   scale " + a.getAttributeAsString("Scale"), EditorStyles.miniLabel);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        void ModelViewport(Rect r)
        {
            EditorGUI.DrawRect(r, Color.black);
            Event e = Event.current;
            if (r.Contains(e.mousePosition))
            {
                if (e.type == EventType.MouseDrag && e.button == 0) { m_yaw += e.delta.x * 0.5f; m_pitch = Mathf.Clamp(m_pitch + e.delta.y * 0.5f, -85, 85); e.Use(); Repaint(); }
                else if (e.type == EventType.MouseDrag && e.button == 1)
                {
                    v3f right = new v3f(Mathf.Cos(m_yaw * Mathf.Deg2Rad), 0, -Mathf.Sin(m_yaw * Mathf.Deg2Rad));
                    m_target = m_target - right * (e.delta.x * m_distance * 0.002f) + new v3f(0, e.delta.y * m_distance * 0.002f, 0);
                    e.Use(); Repaint();
                }
                else if (e.type == EventType.ScrollWheel) { m_distance = Mathf.Clamp(m_distance * (e.delta.y > 0 ? 1.1f : 0.9f), 0.3f, 500f); e.Use(); Repaint(); }
            }
            if (e.type != EventType.Repaint || m_modelNode == null) return;
            if (m_renderer == null) m_renderer = new EditorGameRenderer();
            float yaw = m_yaw * Mathf.Deg2Rad, pitch = m_pitch * Mathf.Deg2Rad;
            v3f offset = new v3f(Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch)) * m_distance;
            m_modelCam.setPosition(m_target + offset);
            m_modelCam.setTarget(m_target);
            m_modelCam.setNearValue(Mathf.Max(0.01f, m_distance * 0.01f));
            m_modelCam.setFarValue(m_distance * 50f);
            m_modelCam.updateAbsolutePosition();
            float scale = EditorGUIUtility.pixelsPerPoint;
            m_renderer.Render((int)(r.width * scale), (int)(r.height * scale), () =>
            {
                CSceneManager.Instance.setActiveCamera(m_modelCam);
                CSceneManager.Instance.drawAll();
            });
            if (m_renderer.Texture != null) GUI.DrawTexture(r, m_renderer.Texture, ScaleMode.StretchToFill, false);
            GUI.Label(new Rect(r.x + 4, r.yMax - 18, r.width, 18), "Left-drag orbit · Right-drag pan · Wheel zoom", EditorStyles.whiteMiniLabel);
        }

        // ------------------------------------------------------------------ spawns

        struct Spawn { public float time; public int type, slaves, riders; public bool intro; }

        /// <summary>The spawn list CPlayfield.loadWaves produces for this day (seeded so it can be re-rolled).</summary>
        List<Spawn> SimulateSpawns(Day day, int difficulty, int seed)
        {
            System.Random rng = new System.Random(seed);
            Func<int, int, int> randInt = (min, max) => min + rng.Next(0, 0x7fffffff) % (max - min + 1);
            Func<float, float, float> randFloat = (min, max) => (rng.Next(0, 0x7fffffff) / (float)0x7fffffff) * (max - min) + min;
            List<Spawn> result = new List<Spawn>();
            foreach (Wave wave in day.waves)
            {
                if (wave.variants.Count == 0) continue;
                float time = 0;
                for (int rep = 0; rep < wave.repeatCount && time < day.dayEndTime; ++rep)
                {
                    int timeStart = wave.timeStart + wave.repeatInterval * rep;
                    Variant v = wave.variants[randInt(0, wave.variants.Count - 1)];
                    time = randFloat(timeStart, timeStart + wave.timeInterval);
                    foreach (EnemyType enemy in v.enemyTypes)
                    {
                        int mn = enemy.minCount, mx = enemy.maxCount;
                        int count = difficulty == GD_Easy ? randInt(mn, (2 * mn + 4 * mx + 3) / 6)
                                  : difficulty == GD_Normal ? randInt((4 * mn + 2 * mx + 3) / 6, (2 * mn + 4 * mx + 3) / 6)
                                  : randInt((2 * mn + 4 * mx + 3) / 6, mx);
                        if (UnitHas(enemy.unitType, "MindControl")) result.Add(new Spawn { type = enemy.unitType, time = time, slaves = count, intro = v.intro });
                        else
                            for (int k = 0; k < count; ++k)
                                result.Add(new Spawn { type = enemy.unitType, time = wave.packed ? time : timeStart + k * wave.timeInterval / (float)count, riders = UnitHas(enemy.unitType, "Rider") ? 1 : 0, intro = v.intro });
                    }
                }
            }
            result.Sort((a, b) => a.time.CompareTo(b.time));
            return result;
        }

        string UnitName(int t) { return t >= 0 && t < m_unitNames.Length ? m_unitNames[t] : "?"; }

        void SpawnsTab()
        {
            if (Application.isPlaying && CPlayfield.GetInstance().IsLevelLoaded())
            {
                CPlayfield pf = CPlayfield.GetInstance();
                EditorGUILayout.LabelField("Live spawns — " + (pf.m_isEndless ? "Endless" : "Level " + (pf.m_CurrentLevel + 1)) + " day " + (pf.m_CurrentDay + 1), EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Time " + pf.getTime().ToString("0.0") + " s   ·   alive " + pf.getUnits().Count + "   ·   spawned " + pf.getWavePos() + "/" + pf.getWaves().Count + "   ·   gate " + (pf.getGate() != null ? pf.getGate().getHealth().ToString("0") : "-"));
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Pause / resume", GUILayout.Width(120)))
                {
                    if (pf.GetPlayState() == CPlayfield.PS_Play) pf.SetPlayState(CPlayfield.PS_EnterIGM);
                    else if (pf.isPaused()) pf.SetPlayState(CPlayfield.PS_ExitIGM);
                }
                if (GUILayout.Button("DIE! (kill all units)", GUILayout.Width(140)))
                    foreach (CUnit u in new List<CUnit>(pf.getUnits())) u.damageUnit(9999, UDS_Lightning);
                GUILayout.EndHorizontal();
                m_spawnScroll = EditorGUILayout.BeginScrollView(m_spawnScroll);
                List<SUnitAtTime> waves = pf.getWaves();
                for (int i = 0; i < waves.Count; i++)
                {
                    SUnitAtTime s = waves[i];
                    string state = i < pf.getWavePos() ? "spawned" : i == pf.getWavePos() ? "next" : "future";
                    GUI.color = i < pf.getWavePos() ? Color.gray : i == pf.getWavePos() ? Color.yellow : Color.white;
                    EditorGUILayout.LabelField(s.time.ToString("0.0") + " s", UnitName(s.type) + (s.slaves > 0 ? " + " + s.slaves + " slaves" : "") + (s.riders > 0 ? " + rider" : "") + (s.intro ? "  [intro]" : "") + "   " + state);
                }
                GUI.color = Color.white;
                foreach (UnitTypeAndId x in pf.m_extraSpawns) EditorGUILayout.LabelField(x.time.ToString("0.0") + " s", "extra: " + UnitName(x.unitType));
                EditorGUILayout.EndScrollView();
                return;
            }
            Day day = CurrentDay;
            if (day == null) return;
            if (IsEndless(m_level))
            {
                EditorGUILayout.HelpBox("Endless days are assembled at run time from these segments, weighted by the day number (easy → medium → hard). Play the level to see the live spawn list.", MessageType.Info);
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label("Preview for", GUILayout.Width(70));
            m_playDifficulty = EditorGUILayout.Popup(m_playDifficulty, k_difficulties, GUILayout.Width(80));
            if (GUILayout.Button("Re-roll", GUILayout.Width(60))) m_spawnSeed++;
            GUILayout.EndHorizontal();
            List<Spawn> spawns = SimulateSpawns(day, m_playDifficulty, m_spawnSeed);
            int total = spawns.Sum(s => 1 + s.slaves + s.riders);
            EditorGUILayout.LabelField(spawns.Count + " spawn events, " + total + " units (the game keeps at most 8 alive; later spawns wait).");
            m_spawnScroll = EditorGUILayout.BeginScrollView(m_spawnScroll);
            foreach (Spawn s in spawns)
                EditorGUILayout.LabelField(s.time.ToString("0.0") + " s", UnitName(s.type) + (s.slaves > 0 ? " + " + s.slaves + " slaves" : "") + (s.riders > 0 ? " + rider" : "") + (s.intro ? "  [intro]" : ""));
            EditorGUILayout.EndScrollView();
        }
    }
}
