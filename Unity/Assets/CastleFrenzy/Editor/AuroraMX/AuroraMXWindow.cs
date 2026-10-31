// AuroraMX for Unity: the Castle Frenzy sprite editor (Tools/AuroraMX) as an EditorWindow.
// Edits sprite YAML (modules, frames, animations, raw source) with previews drawn by the game's own sprite renderer.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CastleFrenzy.EditorTools
{
    public class AuroraMXWindow : EditorWindow
    {
        enum Tab { Modules, Frames, Animations, Source }

        [SerializeField] string m_relPath = "";
        [SerializeField] string m_savedText = "";
        [SerializeField] Tab m_tab = Tab.Frames;
        [SerializeField] int m_module = -1, m_frame = -1, m_part = -1, m_rect = -1, m_anim = -1, m_step = -1;
        [SerializeField] float m_zoom = 3f, m_atlasZoom = 1f;
        [SerializeField] Vector2 m_pan, m_atlasScroll;
        [SerializeField] bool m_showGrid = true, m_showRects = true, m_onion, m_loop = true, m_drawModuleMode;

        SpriteDocument m_doc;
        string m_sourceText = "";
        bool m_sourceDirty;
        readonly List<string> m_undo = new List<string>(), m_redo = new List<string>();
        string m_lastSnapshot;

        ASprite m_sprite;
        bool m_spriteDirty = true, m_atlasDirty = true;
        string m_compileError;
        EditorGameRenderer m_renderer;

        Vector2 m_listScroll, m_detailScroll, m_sourceScroll;
        bool m_playing;
        double m_lastTime;
        float m_ticks;
        int m_playFrame;

        // mouse interaction
        bool m_dragging;
        Vector2 m_dragStart;
        int m_dragStartX, m_dragStartY;
        Rect m_drawRect;

        string[] m_files = new string[0];

        [MenuItem("Castle Frenzy/AuroraMX Sprite Editor", priority = 10)]
        public static void Open() { GetWindow<AuroraMXWindow>("AuroraMX").Show(); }

        public static void OpenFile(string relPath)
        {
            AuroraMXWindow w = GetWindow<AuroraMXWindow>("AuroraMX");
            w.Load(relPath);
            w.Show();
        }

        /// <summary>Selects a tab (0 modules, 1 frames, 2 animations, 3 source) and an item; used by tests.</summary>
        public void Select(int tab, int item)
        {
            m_tab = (Tab)tab;
            if (m_doc == null) return;
            if (m_tab == Tab.Modules) m_module = Mathf.Clamp(item, -1, m_doc.modules.Count - 1);
            else if (m_tab == Tab.Frames) m_frame = Mathf.Clamp(item, -1, m_doc.frames.Count - 1);
            else if (m_tab == Tab.Animations) { m_anim = Mathf.Clamp(item, -1, m_doc.animations.Count - 1); m_playFrame = 0; m_ticks = 0; }
            FitFrame();
            Repaint();
        }

        void OnEnable()
        {
            wantsMouseMove = true;
            titleContent = new GUIContent(string.IsNullOrEmpty(m_relPath) ? "AuroraMX" : "AuroraMX - " + Path.GetFileName(m_relPath));
            RefreshFileList();
            if (!string.IsNullOrEmpty(m_relPath))
            {
                try
                {
                    m_doc = SpriteDocument.Parse(!string.IsNullOrEmpty(m_savedText) ? m_savedText : File.ReadAllText(GameDataTools.UnityPath(m_relPath)));
                    m_sourceText = m_doc.Serialize();
                }
                catch (Exception) { m_doc = null; }
            }
            EditorApplication.update += OnEditorUpdate;
        }

        void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            if (m_renderer != null) { m_renderer.Dispose(); m_renderer = null; }
        }

        void OnEditorUpdate()
        {
            if (!m_playing || m_doc == null || m_anim < 0) return;
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - m_lastTime);
            m_lastTime = now;
            m_ticks += dt * 25f;
            AdvanceAnimation();
            Repaint();
        }

        void RefreshFileList()
        {
            string root = GameDataTools.UnityPath("Sprites");
            if (!Directory.Exists(root)) { m_files = new string[0]; return; }
            m_files = Directory.GetFiles(root, "*.yaml", SearchOption.AllDirectories)
                .Select(f => "Sprites/" + f.Substring(root.Length + 1).Replace('\\', '/'))
                .OrderBy(f => f).ToArray();
        }

        bool Dirty { get { return m_doc != null && (m_lastSnapshot != m_savedText || m_sourceDirty); } }

        void Load(string relPath)
        {
            string path = GameDataTools.UnityPath(relPath);
            if (!File.Exists(path)) { ShowNotification(new GUIContent("Missing " + relPath)); return; }
            string text = File.ReadAllText(path);
            try { m_doc = SpriteDocument.Parse(text); }
            catch (Exception e) { EditorUtility.DisplayDialog("AuroraMX", "Could not parse " + relPath + ":\n" + e.Message, "OK"); return; }
            m_relPath = relPath;
            m_savedText = m_doc.Serialize();
            m_sourceText = text;
            m_sourceDirty = false;
            m_undo.Clear();
            m_redo.Clear();
            m_lastSnapshot = m_savedText;
            m_module = m_doc.modules.Count > 0 ? 0 : -1;
            m_frame = m_doc.frames.Count > 0 ? 0 : -1;
            m_anim = m_doc.animations.Count > 0 ? 0 : -1;
            m_part = m_rect = m_step = -1;
            m_spriteDirty = m_atlasDirty = true;
            m_pan = Vector2.zero;
            titleContent = new GUIContent("AuroraMX - " + Path.GetFileName(relPath));
        }

        void Save()
        {
            if (m_doc == null) return;
            if (m_sourceDirty && !ApplySource()) return;
            List<string> errors = m_doc.Validate();
            if (errors.Count > 0 && !EditorUtility.DisplayDialog("AuroraMX", "The sprite has problems the game will reject:\n\n" + string.Join("\n", errors.Take(12)) + "\n\nSave anyway?", "Save", "Cancel")) return;
            string text = m_doc.Serialize();
            List<string> written = GameDataTools.WriteGameFile(m_relPath, System.Text.Encoding.UTF8.GetBytes(text));
            m_savedText = text;
            m_lastSnapshot = text;
            m_sourceText = text;
            string ids = GameDataTools.RegenerateSpriteIds();
            AssetDatabase.Refresh();
            ShowNotification(new GUIContent("Saved " + written.Count + " cop" + (written.Count == 1 ? "y" : "ies")));
            Debug.Log("[AuroraMX] Saved " + string.Join(", ", written) + ". " + ids);
        }

        // ------------------------------------------------------------------ undo

        /// <summary>m_lastSnapshot always holds the last committed document; edits are committed by EndEdit.</summary>
        void BeginEdit() { if (m_lastSnapshot == null) m_lastSnapshot = m_doc.Serialize(); }

        void EndEdit()
        {
            string now = m_doc.Serialize();
            if (now == m_lastSnapshot) return;
            m_undo.Add(m_lastSnapshot);
            if (m_undo.Count > 200) m_undo.RemoveAt(0);
            m_redo.Clear();
            m_lastSnapshot = now;
            m_spriteDirty = true;
            m_sourceText = now;
            m_sourceDirty = false;
        }

        void Undo()
        {
            if (m_undo.Count == 0) return;
            m_redo.Add(m_doc.Serialize());
            RestoreSnapshot(m_undo[m_undo.Count - 1]);
            m_undo.RemoveAt(m_undo.Count - 1);
        }

        void Redo()
        {
            if (m_redo.Count == 0) return;
            m_undo.Add(m_doc.Serialize());
            RestoreSnapshot(m_redo[m_redo.Count - 1]);
            m_redo.RemoveAt(m_redo.Count - 1);
        }

        void RestoreSnapshot(string text)
        {
            m_doc = SpriteDocument.Parse(text);
            m_lastSnapshot = text;
            m_sourceText = text;
            m_spriteDirty = m_atlasDirty = true;
            ClampSelection();
        }

        void ClampSelection()
        {
            m_module = Mathf.Min(m_module, m_doc.modules.Count - 1);
            m_frame = Mathf.Min(m_frame, m_doc.frames.Count - 1);
            m_anim = Mathf.Min(m_anim, m_doc.animations.Count - 1);
            m_part = m_frame >= 0 ? Mathf.Min(m_part, m_doc.frames[m_frame].parts.Count - 1) : -1;
            m_rect = m_frame >= 0 ? Mathf.Min(m_rect, m_doc.frames[m_frame].rects.Count - 1) : -1;
            m_step = m_anim >= 0 ? Mathf.Min(m_step, m_doc.animations[m_anim].frames.Count - 1) : -1;
        }

        bool ApplySource()
        {
            try
            {
                SpriteDocument d = SpriteDocument.Parse(m_sourceText);
                BeginEdit();
                m_doc = d;
                EndEdit();
                m_sourceDirty = false;
                m_atlasDirty = true;
                ClampSelection();
                return true;
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("AuroraMX", "Source does not parse:\n" + e.Message, "OK");
                return false;
            }
        }

        // ------------------------------------------------------------------ sprite compilation (game renderer)

        ASprite Sprite
        {
            get
            {
                // The texture cache is shared with the running game, so nothing is compiled during Play mode.
                if (m_doc == null || !EditorGameRenderer.Available) return null;
                if (m_spriteDirty || m_sprite == null)
                {
                    m_spriteDirty = false;
                    ASprite s = new ASprite();
                    try
                    {
                        if (s.LoadYamlText(m_doc.Serialize(), m_relPath, !m_atlasDirty)) { m_sprite = s; m_compileError = null; }
                        else m_compileError = "The game could not compile this sprite (see Console).";
                    }
                    catch (Exception e) { m_compileError = e.Message; }
                    m_atlasDirty = false;
                }
                return m_sprite;
            }
        }

        Texture2D AtlasTexture
        {
            get
            {
                ASprite s = Sprite;
                return s != null && s._modules_image != null ? s._modules_image.Tex : null;
            }
        }

        // ------------------------------------------------------------------ GUI

        void OnGUI()
        {
            HandleShortcuts();
            DrawToolbar();
            if (m_doc == null)
            {
                EditorGUILayout.HelpBox("Open a sprite YAML from StreamingAssets/GameData/Sprites (File ▸ Open). Saving also updates Build/bin/Sprites and Data/Sprites.", MessageType.Info);
                return;
            }
            Rect area = new Rect(0, EditorStyles.toolbar.fixedHeight, position.width, position.height - EditorStyles.toolbar.fixedHeight);
            float left = Mathf.Clamp(position.width * 0.38f, 300, 520);
            GUILayout.BeginArea(new Rect(area.x, area.y, left, area.height));
            m_tab = (Tab)GUILayout.Toolbar((int)m_tab, new[] { "Modules", "Frames", "Animations", "Source" });
            switch (m_tab)
            {
                case Tab.Modules: ModulesPanel(); break;
                case Tab.Frames: FramesPanel(); break;
                case Tab.Animations: AnimationsPanel(); break;
                case Tab.Source: SourcePanel(); break;
            }
            GUILayout.EndArea();
            Rect preview = new Rect(left + 4, area.y + 22, area.width - left - 8, area.height - 26);
            GUILayout.BeginArea(new Rect(left + 4, area.y, area.width - left - 8, 22));
            PreviewToolbar();
            GUILayout.EndArea();
            if (m_tab == Tab.Modules) AtlasView(preview);
            else GamePreview(preview);
            if (GUI.changed && !m_dragging) EndEdit();
        }

        void HandleShortcuts()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown) return;
            bool ctrl = e.control || e.command;
            bool inText = EditorGUIUtility.editingTextField;
            if (ctrl && e.keyCode == KeyCode.S) { Save(); e.Use(); }
            else if (ctrl && e.keyCode == KeyCode.Z && !inText) { Undo(); e.Use(); }
            else if (ctrl && e.keyCode == KeyCode.Y && !inText) { Redo(); e.Use(); }
            else if (e.keyCode == KeyCode.Space && !inText && m_tab == Tab.Animations) { TogglePlay(); e.Use(); }
        }

        void DrawToolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (EditorGUILayout.DropdownButton(new GUIContent("File"), FocusType.Passive, EditorStyles.toolbarDropDown, GUILayout.Width(50)))
            {
                GenericMenu menu = new GenericMenu();
                RefreshFileList();
                foreach (string f in m_files)
                {
                    string file = f;
                    menu.AddItem(new GUIContent("Open/" + f.Substring("Sprites/".Length)), file == m_relPath, () => { if (!Dirty || EditorUtility.DisplayDialog("AuroraMX", "Discard unsaved changes?", "Discard", "Cancel")) Load(file); });
                }
                menu.AddSeparator("");
                if (m_doc != null)
                {
                    menu.AddItem(new GUIContent("Save  (Ctrl+S)"), false, Save);
                    menu.AddItem(new GUIContent("Reload"), false, () => Load(m_relPath));
                    menu.AddItem(new GUIContent("Reveal in File Browser"), false, () => EditorUtility.RevealInFinder(GameDataTools.UnityPath(m_relPath)));
                }
                menu.AddItem(new GUIContent("Regenerate Sprite IDs"), false, GameDataTools.RegenerateSpriteIdsMenu);
                menu.ShowAsContext();
            }
            GUI.enabled = m_doc != null;
            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(44))) Save();
            GUI.enabled = m_undo.Count > 0;
            if (GUILayout.Button("Undo", EditorStyles.toolbarButton, GUILayout.Width(44))) Undo();
            GUI.enabled = m_redo.Count > 0;
            if (GUILayout.Button("Redo", EditorStyles.toolbarButton, GUILayout.Width(44))) Redo();
            GUI.enabled = m_doc != null;
            if (GUILayout.Button("Validate", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                List<string> errors = m_doc.Validate();
                EditorUtility.DisplayDialog("AuroraMX", errors.Count == 0 ? "No problems found." : string.Join("\n", errors.Take(30)), "OK");
            }
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            if (m_doc != null)
            {
                GUILayout.Label(m_relPath + (Dirty ? " *" : ""), EditorStyles.miniLabel);
                GUILayout.Label(m_doc.modules.Count + " modules · " + m_doc.frames.Count + " frames · " + m_doc.animations.Count + " anims", EditorStyles.miniLabel);
            }
            GUILayout.EndHorizontal();
        }

        static bool ListButtons(ref int sel, int count, out bool add, out bool clone, out bool del, out int move)
        {
            GUILayout.BeginHorizontal();
            add = GUILayout.Button("Add", EditorStyles.miniButtonLeft);
            GUI.enabled = sel >= 0;
            clone = GUILayout.Button("Clone", EditorStyles.miniButtonMid);
            del = GUILayout.Button("Delete", EditorStyles.miniButtonMid);
            move = 0;
            GUI.enabled = sel > 0;
            if (GUILayout.Button("▲", EditorStyles.miniButtonMid, GUILayout.Width(24))) move = -1;
            GUI.enabled = sel >= 0 && sel < count - 1;
            if (GUILayout.Button("▼", EditorStyles.miniButtonRight, GUILayout.Width(24))) move = 1;
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            return add || clone || del || move != 0;
        }

        static void Move<T>(List<T> list, ref int sel, int dir)
        {
            int to = sel + dir;
            if (sel < 0 || to < 0 || to >= list.Count) return;
            T t = list[sel];
            list[sel] = list[to];
            list[to] = t;
            sel = to;
        }

        int SelectList(int sel, int count, Func<int, string> label, float height)
        {
            m_listScroll = EditorGUILayout.BeginScrollView(m_listScroll, GUILayout.Height(height));
            for (int i = 0; i < count; i++)
            {
                GUIStyle st = i == sel ? EditorStyles.selectionRect : EditorStyles.label;
                Rect r = GUILayoutUtility.GetRect(new GUIContent(label(i)), EditorStyles.label);
                if (i == sel) EditorGUI.DrawRect(r, new Color(0.24f, 0.48f, 0.9f, 0.45f));
                GUI.Label(r, label(i));
                if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
                {
                    sel = i;
                    GUI.FocusControl(null);
                    Event.current.Use();
                }
            }
            EditorGUILayout.EndScrollView();
            return sel;
        }

        // ------------------------------------------------------------------ modules

        void ModulesPanel()
        {
            int sel = m_module;
            m_module = SelectList(m_module, m_doc.modules.Count, i =>
            {
                SpriteModuleDef m = m_doc.modules[i];
                return i + ": " + m.id + "   (" + m.x + ", " + m.y + ")  " + m.w + "×" + m.h;
            }, position.height * 0.45f);
            if (sel != m_module) Repaint();
            bool add, clone, del;
            int move;
            BeginEdit();
            if (ListButtons(ref m_module, m_doc.modules.Count, out add, out clone, out del, out move))
            {
                if (add)
                {
                    SpriteModuleDef nm = new SpriteModuleDef { id = m_doc.NextId(m_doc.modules, x => x.id, 0x1000), image = m_doc.images.Count > 0 ? m_doc.images[0].id : "0x0000" };
                    m_doc.modules.Insert(m_module + 1, nm);
                    m_module++;
                }
                else if (clone && m_module >= 0)
                {
                    SpriteModuleDef c = m_doc.modules[m_module].Clone();
                    c.id = m_doc.NextId(m_doc.modules, x => x.id, 0x1000);
                    m_doc.modules.Insert(m_module + 1, c);
                    m_module++;
                }
                else if (del && m_module >= 0)
                {
                    string id = m_doc.modules[m_module].id;
                    int uses = m_doc.frames.Sum(f => f.parts.Count(p => !p.hyper && p.module == id));
                    if (uses == 0 || EditorUtility.DisplayDialog("AuroraMX", "Module " + id + " is used by " + uses + " frame part(s). Delete it and those parts?", "Delete", "Cancel"))
                    {
                        foreach (SpriteFrameDef f in m_doc.frames) f.parts.RemoveAll(p => !p.hyper && p.module == id);
                        m_doc.modules.RemoveAt(m_module);
                        m_module = Mathf.Min(m_module, m_doc.modules.Count - 1);
                    }
                }
                else if (move != 0) Move(m_doc.modules, ref m_module, move);
            }
            EditorGUILayout.Space();
            m_detailScroll = EditorGUILayout.BeginScrollView(m_detailScroll);
            if (m_module >= 0)
            {
                SpriteModuleDef m = m_doc.modules[m_module];
                EditorGUI.BeginChangeCheck();
                m.id = EditorGUILayout.TextField("ID", m.id);
                m.image = EditorGUILayout.TextField("Image", m.image);
                m.x = EditorGUILayout.IntField("X", m.x);
                m.y = EditorGUILayout.IntField("Y", m.y);
                m.w = EditorGUILayout.IntField("Width", m.w);
                m.h = EditorGUILayout.IntField("Height", m.h);
                if (EditorGUI.EndChangeCheck()) { }
                int uses = m_doc.frames.Count(f => f.parts.Any(p => !p.hyper && p.module == m.id));
                EditorGUILayout.LabelField("Used by", uses + " frame(s)");
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Images", EditorStyles.boldLabel);
            foreach (SpriteImageDef img in m_doc.images)
            {
                EditorGUI.BeginChangeCheck();
                img.id = EditorGUILayout.TextField("ID", img.id);
                img.file = EditorGUILayout.TextField("File", img.file);
                img.transparent = EditorGUILayout.TextField("Transparent key", img.transparent);
                img.alphaFile = EditorGUILayout.TextField("Alpha mask", img.alphaFile);
                if (EditorGUI.EndChangeCheck()) m_atlasDirty = true;
            }
            m_drawModuleMode = EditorGUILayout.ToggleLeft("Draw module on atlas (drag a new rectangle)", m_drawModuleMode);
            EditorGUILayout.HelpBox("Click a module on the atlas to select it, drag to move it, or enable draw mode to create one. Wheel zooms.", MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        void AtlasView(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.16f));
            Texture2D tex = AtlasTexture;
            if (tex == null) { GUI.Label(rect, m_compileError ?? "No atlas image.", EditorStyles.centeredGreyMiniLabel); return; }
            Event e = Event.current;
            if (e.type == EventType.ScrollWheel && rect.Contains(e.mousePosition))
            {
                m_atlasZoom = Mathf.Clamp(m_atlasZoom * (e.delta.y > 0 ? 0.87f : 1.15f), 0.25f, 16f);
                e.Use();
            }
            Rect content = new Rect(0, 0, tex.width * m_atlasZoom, tex.height * m_atlasZoom);
            m_atlasScroll = GUI.BeginScrollView(rect, m_atlasScroll, content);
            DrawChecker(content);
            GUI.DrawTexture(content, tex, ScaleMode.StretchToFill, true);
            for (int i = 0; i < m_doc.modules.Count; i++)
            {
                SpriteModuleDef m = m_doc.modules[i];
                Rect r = new Rect(m.x * m_atlasZoom, m.y * m_atlasZoom, m.w * m_atlasZoom, m.h * m_atlasZoom);
                Outline(r, i == m_module ? Color.yellow : new Color(0.3f, 0.9f, 1f, 0.55f), i == m_module ? 2 : 1);
            }
            Vector2 atlasPos = e.mousePosition / m_atlasZoom;
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (m_drawModuleMode)
                {
                    m_dragging = true;
                    m_dragStart = atlasPos;
                    m_drawRect = new Rect(atlasPos, Vector2.zero);
                }
                else
                {
                    int hit = -1;
                    for (int i = m_doc.modules.Count - 1; i >= 0; i--)
                    {
                        SpriteModuleDef m = m_doc.modules[i];
                        if (new Rect(m.x, m.y, m.w, m.h).Contains(atlasPos)) { hit = i; break; }
                    }
                    if (hit >= 0)
                    {
                        m_module = hit;
                        BeginEdit();
                        m_dragging = true;
                        m_dragStart = atlasPos;
                        m_dragStartX = m_doc.modules[hit].x;
                        m_dragStartY = m_doc.modules[hit].y;
                    }
                }
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && m_dragging)
            {
                if (m_drawModuleMode) m_drawRect = Rect.MinMaxRect(Mathf.Min(m_dragStart.x, atlasPos.x), Mathf.Min(m_dragStart.y, atlasPos.y), Mathf.Max(m_dragStart.x, atlasPos.x), Mathf.Max(m_dragStart.y, atlasPos.y));
                else if (m_module >= 0)
                {
                    m_doc.modules[m_module].x = m_dragStartX + Mathf.RoundToInt(atlasPos.x - m_dragStart.x);
                    m_doc.modules[m_module].y = m_dragStartY + Mathf.RoundToInt(atlasPos.y - m_dragStart.y);
                    m_spriteDirty = true;
                }
                e.Use();
            }
            else if (e.type == EventType.MouseUp && m_dragging)
            {
                m_dragging = false;
                if (m_drawModuleMode)
                {
                    if (m_drawRect.width >= 1 && m_drawRect.height >= 1)
                    {
                        BeginEdit();
                        SpriteModuleDef nm = new SpriteModuleDef
                        {
                            id = m_doc.NextId(m_doc.modules, x => x.id, 0x1000),
                            image = m_doc.images.Count > 0 ? m_doc.images[0].id : "0x0000",
                            x = Mathf.FloorToInt(m_drawRect.xMin), y = Mathf.FloorToInt(m_drawRect.yMin),
                            w = Mathf.Max(1, Mathf.RoundToInt(m_drawRect.width)), h = Mathf.Max(1, Mathf.RoundToInt(m_drawRect.height)),
                        };
                        m_doc.modules.Add(nm);
                        m_module = m_doc.modules.Count - 1;
                        EndEdit();
                    }
                }
                else EndEdit();
                e.Use();
            }
            if (m_dragging && m_drawModuleMode)
                Outline(new Rect(m_drawRect.x * m_atlasZoom, m_drawRect.y * m_atlasZoom, m_drawRect.width * m_atlasZoom, m_drawRect.height * m_atlasZoom), Color.green, 2);
            GUI.EndScrollView();
            GUI.Label(new Rect(rect.x + 4, rect.yMax - 18, 300, 18), tex.width + "×" + tex.height + "  zoom " + m_atlasZoom.ToString("0.##") + "×", EditorStyles.whiteMiniLabel);
        }

        // ------------------------------------------------------------------ frames

        void FramesPanel()
        {
            m_frame = SelectList(m_frame, m_doc.frames.Count, i =>
            {
                SpriteFrameDef f = m_doc.frames[i];
                return i + ": " + f.id + "  " + (string.IsNullOrEmpty(f.name) ? "" : f.name) + "  [" + f.parts.Count + "]";
            }, position.height * 0.3f);
            bool add, clone, del;
            int move;
            BeginEdit();
            if (ListButtons(ref m_frame, m_doc.frames.Count, out add, out clone, out del, out move))
            {
                if (add)
                {
                    m_doc.frames.Insert(m_frame + 1, new SpriteFrameDef { id = m_doc.NextId(m_doc.frames, x => x.id, 0x2000) });
                    m_frame++;
                }
                else if (clone && m_frame >= 0)
                {
                    SpriteFrameDef c = m_doc.frames[m_frame].Clone();
                    c.id = m_doc.NextId(m_doc.frames, x => x.id, 0x2000);
                    m_doc.frames.Insert(m_frame + 1, c);
                    m_frame++;
                }
                else if (del && m_frame >= 0)
                {
                    string id = m_doc.frames[m_frame].id;
                    int uses = m_doc.animations.Sum(a => a.frames.Count(s => s.frame == id));
                    if (uses == 0 || EditorUtility.DisplayDialog("AuroraMX", "Frame " + id + " is used by " + uses + " animation step(s). Delete it and those steps?", "Delete", "Cancel"))
                    {
                        foreach (SpriteAnimDef a in m_doc.animations) a.frames.RemoveAll(s => s.frame == id);
                        m_doc.frames.RemoveAt(m_frame);
                        m_frame = Mathf.Min(m_frame, m_doc.frames.Count - 1);
                    }
                }
                else if (move != 0) Move(m_doc.frames, ref m_frame, move);
                m_part = m_rect = -1;
            }
            m_detailScroll = EditorGUILayout.BeginScrollView(m_detailScroll);
            if (m_frame >= 0)
            {
                SpriteFrameDef f = m_doc.frames[m_frame];
                f.id = EditorGUILayout.TextField("ID", f.id);
                f.name = EditorGUILayout.TextField("Name", f.name);
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Parts (" + f.parts.Count + ")", EditorStyles.boldLabel);
                string[] moduleIds = m_doc.modules.Select(m => m.id).ToArray();
                string[] frameIds = m_doc.frames.Select(x => x.id + " " + x.name).ToArray();
                for (int i = 0; i < f.parts.Count; i++)
                {
                    SpritePartDef p = f.parts[i];
                    GUILayout.BeginHorizontal(i == m_part ? "SelectionRect" : GUIStyle.none);
                    if (GUILayout.Toggle(i == m_part, "", GUILayout.Width(16)) && m_part != i) { m_part = i; GUI.FocusControl(null); }
                    if (p.hyper)
                    {
                        int fi = Mathf.Max(0, m_doc.FrameIndex(p.module));
                        int nf = EditorGUILayout.Popup(fi, frameIds, GUILayout.Width(110));
                        if (nf >= 0 && nf < m_doc.frames.Count) p.module = m_doc.frames[nf].id;
                    }
                    else
                    {
                        int mi = Mathf.Max(0, m_doc.ModuleIndex(p.module));
                        int nm = EditorGUILayout.Popup(mi, moduleIds, GUILayout.Width(110));
                        if (nm >= 0 && nm < m_doc.modules.Count) p.module = m_doc.modules[nm].id;
                    }
                    p.x = EditorGUILayout.IntField(p.x, GUILayout.Width(44));
                    p.y = EditorGUILayout.IntField(p.y, GUILayout.Width(44));
                    p.flipX = GUILayout.Toggle(p.flipX, "FX", EditorStyles.miniButtonLeft, GUILayout.Width(26));
                    p.flipY = GUILayout.Toggle(p.flipY, "FY", EditorStyles.miniButtonMid, GUILayout.Width(26));
                    p.rot90 = GUILayout.Toggle(p.rot90, "R", EditorStyles.miniButtonMid, GUILayout.Width(22));
                    p.hyper = GUILayout.Toggle(p.hyper, "H", EditorStyles.miniButtonRight, GUILayout.Width(22));
                    GUILayout.EndHorizontal();
                }
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Add part", EditorStyles.miniButtonLeft))
                {
                    string mod = m_module >= 0 && m_module < m_doc.modules.Count ? m_doc.modules[m_module].id : (m_doc.modules.Count > 0 ? m_doc.modules[0].id : "0x1000");
                    f.parts.Add(new SpritePartDef { module = mod });
                    m_part = f.parts.Count - 1;
                }
                GUI.enabled = m_part >= 0;
                if (GUILayout.Button("Delete", EditorStyles.miniButtonMid)) { f.parts.RemoveAt(m_part); m_part = Mathf.Min(m_part, f.parts.Count - 1); }
                if (GUILayout.Button("▲ layer", EditorStyles.miniButtonMid) && m_part > 0) Move(f.parts, ref m_part, -1);
                if (GUILayout.Button("▼ layer", EditorStyles.miniButtonRight) && m_part >= 0 && m_part < f.parts.Count - 1) Move(f.parts, ref m_part, 1);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Collision rectangles (" + f.rects.Count + ")", EditorStyles.boldLabel);
                for (int i = 0; i < f.rects.Count; i++)
                {
                    SpriteRectDef r = f.rects[i];
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Toggle(i == m_rect, "", GUILayout.Width(16))) m_rect = i;
                    r.x1 = EditorGUILayout.IntField(r.x1);
                    r.y1 = EditorGUILayout.IntField(r.y1);
                    r.x2 = EditorGUILayout.IntField(r.x2);
                    r.y2 = EditorGUILayout.IntField(r.y2);
                    GUILayout.EndHorizontal();
                }
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Add rect", EditorStyles.miniButtonLeft)) { f.rects.Add(new SpriteRectDef { x1 = -8, y1 = -8, x2 = 8, y2 = 8 }); m_rect = f.rects.Count - 1; }
                GUI.enabled = m_rect >= 0;
                if (GUILayout.Button("Delete rect", EditorStyles.miniButtonRight)) { f.rects.RemoveAt(m_rect); m_rect = Mathf.Min(m_rect, f.rects.Count - 1); }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                EditorGUILayout.HelpBox("Drag parts in the preview to move them (the selected part, or the one under the cursor). Wheel zooms, middle/Alt-drag pans.", MessageType.None);
            }
            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------ animations

        void AnimationsPanel()
        {
            int before = m_anim;
            m_anim = SelectList(m_anim, m_doc.animations.Count, i =>
            {
                SpriteAnimDef a = m_doc.animations[i];
                return i + ": " + a.id + "  " + a.name + "  [" + a.frames.Count + "]";
            }, position.height * 0.3f);
            if (before != m_anim) { m_ticks = 0; m_playFrame = 0; m_step = -1; }
            bool add, clone, del;
            int move;
            BeginEdit();
            if (ListButtons(ref m_anim, m_doc.animations.Count, out add, out clone, out del, out move))
            {
                if (add)
                {
                    m_doc.animations.Insert(m_anim + 1, new SpriteAnimDef { id = m_doc.NextId(m_doc.animations, x => x.id, 0x3000) });
                    m_anim++;
                }
                else if (clone && m_anim >= 0)
                {
                    SpriteAnimDef c = m_doc.animations[m_anim].Clone();
                    c.id = m_doc.NextId(m_doc.animations, x => x.id, 0x3000);
                    m_doc.animations.Insert(m_anim + 1, c);
                    m_anim++;
                }
                else if (del && m_anim >= 0)
                {
                    m_doc.animations.RemoveAt(m_anim);
                    m_anim = Mathf.Min(m_anim, m_doc.animations.Count - 1);
                }
                else if (move != 0) Move(m_doc.animations, ref m_anim, move);
                m_step = -1;
            }
            m_detailScroll = EditorGUILayout.BeginScrollView(m_detailScroll);
            if (m_anim >= 0)
            {
                SpriteAnimDef a = m_doc.animations[m_anim];
                a.id = EditorGUILayout.TextField("ID", a.id);
                a.name = EditorGUILayout.TextField("Name", a.name);
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Steps (" + a.frames.Count + ")  —  time in ticks, 25 per second; 0 holds", EditorStyles.boldLabel);
                string[] frameIds = m_doc.frames.Select(x => x.id + " " + x.name).ToArray();
                for (int i = 0; i < a.frames.Count; i++)
                {
                    SpriteStepDef s = a.frames[i];
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Toggle(i == m_step, i.ToString(), GUILayout.Width(30)) && m_step != i) { m_step = i; m_playFrame = i; m_playing = false; }
                    int fi = Mathf.Max(0, m_doc.FrameIndex(s.frame));
                    int nf = EditorGUILayout.Popup(fi, frameIds, GUILayout.MinWidth(80));
                    if (nf >= 0 && nf < m_doc.frames.Count) s.frame = m_doc.frames[nf].id;
                    s.time = EditorGUILayout.IntField(s.time, GUILayout.Width(32));
                    s.x = EditorGUILayout.IntField(s.x, GUILayout.Width(36));
                    s.y = EditorGUILayout.IntField(s.y, GUILayout.Width(36));
                    s.flipX = GUILayout.Toggle(s.flipX, "FX", EditorStyles.miniButtonLeft, GUILayout.Width(26));
                    s.flipY = GUILayout.Toggle(s.flipY, "FY", EditorStyles.miniButtonMid, GUILayout.Width(26));
                    s.rot90 = GUILayout.Toggle(s.rot90, "R", EditorStyles.miniButtonRight, GUILayout.Width(22));
                    GUILayout.EndHorizontal();
                }
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Add step", EditorStyles.miniButtonLeft))
                {
                    string fr = m_frame >= 0 && m_frame < m_doc.frames.Count ? m_doc.frames[m_frame].id : (m_doc.frames.Count > 0 ? m_doc.frames[0].id : "0x2000");
                    a.frames.Insert(m_step + 1, new SpriteStepDef { frame = fr, time = 2 });
                    m_step++;
                }
                GUI.enabled = m_step >= 0;
                if (GUILayout.Button("Delete", EditorStyles.miniButtonMid)) { a.frames.RemoveAt(m_step); m_step = Mathf.Min(m_step, a.frames.Count - 1); }
                if (GUILayout.Button("▲", EditorStyles.miniButtonMid) && m_step > 0) Move(a.frames, ref m_step, -1);
                if (GUILayout.Button("▼", EditorStyles.miniButtonRight) && m_step >= 0 && m_step < a.frames.Count - 1) Move(a.frames, ref m_step, 1);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                int total = a.frames.Sum(s => s.time);
                EditorGUILayout.LabelField("Length", total + " ticks (" + (total / 25f).ToString("0.00") + " s)");
            }
            EditorGUILayout.EndScrollView();
        }

        void TogglePlay()
        {
            m_playing = !m_playing;
            m_lastTime = EditorApplication.timeSinceStartup;
        }

        void AdvanceAnimation()
        {
            SpriteAnimDef a = m_doc.animations[m_anim];
            if (a.frames.Count == 0) return;
            m_playFrame = Mathf.Clamp(m_playFrame, 0, a.frames.Count - 1);
            int guard = 0;
            while (guard++ < 1000)
            {
                int t = a.frames[m_playFrame].time;
                if (t == 0 || m_ticks < t) break;
                m_ticks -= t;
                if (m_playFrame + 1 >= a.frames.Count)
                {
                    if (!m_loop) { m_playing = false; m_ticks = 0; break; }
                    m_playFrame = 0;
                }
                else m_playFrame++;
            }
        }

        // ------------------------------------------------------------------ source

        void SourcePanel()
        {
            EditorGUILayout.HelpBox("Edit the YAML directly, then Apply (or Save). Comments at the top of the file are kept.", MessageType.None);
            m_sourceScroll = EditorGUILayout.BeginScrollView(m_sourceScroll, GUILayout.ExpandHeight(true));
            EditorGUI.BeginChangeCheck();
            GUIStyle mono = new GUIStyle(EditorStyles.textArea) { font = EditorStyles.standardFont, wordWrap = false };
            m_sourceText = EditorGUILayout.TextArea(m_sourceText, mono, GUILayout.ExpandHeight(true));
            if (EditorGUI.EndChangeCheck()) m_sourceDirty = true;
            EditorGUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            GUI.enabled = m_sourceDirty;
            if (GUILayout.Button("Apply source")) ApplySource();
            if (GUILayout.Button("Revert")) { m_sourceText = m_doc.Serialize(); m_sourceDirty = false; GUI.FocusControl(null); }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        // ------------------------------------------------------------------ game preview (frames & animations)

        void PreviewToolbar()
        {
            GUILayout.BeginHorizontal();
            if (m_tab == Tab.Modules)
            {
                GUILayout.Label("Atlas", EditorStyles.boldLabel);
                m_atlasZoom = GUILayout.HorizontalSlider(m_atlasZoom, 0.25f, 8f, GUILayout.Width(120));
            }
            else
            {
                GUILayout.Label("Zoom", GUILayout.Width(36));
                m_zoom = GUILayout.HorizontalSlider(m_zoom, 0.5f, 10f, GUILayout.Width(110));
                if (GUILayout.Button("Fit", EditorStyles.miniButton, GUILayout.Width(32))) FitFrame();
                m_showGrid = GUILayout.Toggle(m_showGrid, "Origin", EditorStyles.miniButton, GUILayout.Width(48));
                m_showRects = GUILayout.Toggle(m_showRects, "Rects", EditorStyles.miniButton, GUILayout.Width(44));
                if (m_tab == Tab.Animations)
                {
                    if (GUILayout.Button(m_playing ? "❚❚" : "▶", EditorStyles.miniButton, GUILayout.Width(28))) TogglePlay();
                    if (GUILayout.Button("|◀", EditorStyles.miniButton, GUILayout.Width(28))) { m_playFrame = 0; m_ticks = 0; }
                    if (GUILayout.Button("▶|", EditorStyles.miniButton, GUILayout.Width(28)) && m_anim >= 0 && m_doc.animations[m_anim].frames.Count > 0)
                    {
                        m_playFrame = (m_playFrame + 1) % m_doc.animations[m_anim].frames.Count;
                        m_ticks = 0;
                    }
                    m_loop = GUILayout.Toggle(m_loop, "Loop", EditorStyles.miniButton, GUILayout.Width(40));
                    m_onion = GUILayout.Toggle(m_onion, "Onion", EditorStyles.miniButton, GUILayout.Width(46));
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        int PreviewFrameIndex()
        {
            if (m_tab == Tab.Frames) return m_frame;
            if (m_tab == Tab.Animations && m_anim >= 0)
            {
                SpriteAnimDef a = m_doc.animations[m_anim];
                if (a.frames.Count == 0) return -1;
                return m_doc.FrameIndex(a.frames[Mathf.Clamp(m_playFrame, 0, a.frames.Count - 1)].frame);
            }
            return -1;
        }

        void FitFrame()
        {
            ASprite s = Sprite;
            int f = PreviewFrameIndex();
            m_pan = Vector2.zero;
            if (s == null || f < 0 || f >= s.GetFramesNum()) { m_zoom = 3; return; }
            int w = Mathf.Max(8, s.GetFrameWidth(f)), h = Mathf.Max(8, s.GetFrameHeight(f));
            m_zoom = Mathf.Clamp(Mathf.Min(400f / w, 260f / h), 0.5f, 10f);
            m_pan = new Vector2(-(s.GetFrameLeft(f) + w * 0.5f) * m_zoom, -(s.GetFrameTop(f) + h * 0.5f) * m_zoom);
        }

        // Game-space (480x320) <-> GUI conversions for the preview rectangle.
        Rect GameRectIn(Rect r) { Rect g = Gfx.ComputeGameRect((int)r.width, (int)r.height); g.x += r.x; g.y += r.y; return g; }

        Vector2 GuiToSprite(Rect r, Vector2 gui)
        {
            Rect g = GameRectIn(r);
            float gx = (gui.x - g.x) * Gfx.SCREEN_W / g.width, gy = (gui.y - g.y) * Gfx.SCREEN_H / g.height;
            return new Vector2((gx - Gfx.SCREEN_W * 0.5f - m_pan.x) / m_zoom, (gy - Gfx.SCREEN_H * 0.5f - m_pan.y) / m_zoom);
        }

        void GamePreview(Rect rect)
        {
            EditorGUI.DrawRect(rect, Color.black);
            if (!EditorGameRenderer.Available) { GUI.Label(rect, "Preview is paused while the game runs in Play mode.", EditorStyles.centeredGreyMiniLabel); return; }
            ASprite sprite = Sprite;
            if (sprite == null) { GUI.Label(rect, m_compileError ?? "Sprite does not compile.", EditorStyles.centeredGreyMiniLabel); return; }
            HandlePreviewMouse(rect, sprite);
            if (Event.current.type != EventType.Repaint) return;
            if (m_renderer == null) m_renderer = new EditorGameRenderer();
            int frame = PreviewFrameIndex();
            float scale = EditorGUIUtility.pixelsPerPoint;
            m_renderer.Render((int)(rect.width * scale), (int)(rect.height * scale), () => DrawPreviewScene(sprite, frame));
            if (m_renderer.Texture != null) GUI.DrawTexture(rect, m_renderer.Texture, ScaleMode.StretchToFill, false);
            string info = frame >= 0 && frame < sprite.GetFramesNum() ? "frame " + frame + "  bounds " + sprite.GetFrameWidth(frame) + "×" + sprite.GetFrameHeight(frame) : "no frame";
            if (m_tab == Tab.Animations && m_anim >= 0) info += "   step " + m_playFrame;
            GUI.Label(new Rect(rect.x + 4, rect.yMax - 18, rect.width, 18), info + "   zoom " + m_zoom.ToString("0.0") + "×", EditorStyles.whiteMiniLabel);
        }

        void DrawPreviewScene(ASprite sprite, int frame)
        {
            Gfx.beginScene2D();
            float cx = Gfx.SCREEN_W * 0.5f + m_pan.x, cy = Gfx.SCREEN_H * 0.5f + m_pan.y;
            if (m_showGrid)
            {
                Gfx.draw2DRectangle(new SColor(90, 255, 255, 255), new r2i((int)cx, 0, (int)cx + 1, Gfx.SCREEN_H));
                Gfx.draw2DRectangle(new SColor(90, 255, 255, 255), new r2i(0, (int)cy, Gfx.SCREEN_W, (int)cy + 1));
            }
            matrix4 m = matrix4.Identity.setTranslation(new v3f(cx, cy, 0)) * matrix4.Identity.setScale(new v3f(m_zoom, m_zoom, 1));
            ASprite.PushTransform(m);
            if (m_tab == Tab.Animations && m_anim >= 0 && m_anim < sprite.GetAnimsNum())
            {
                int naf = sprite.GetAFrames(m_anim);
                if (m_onion && naf > 1)
                {
                    int prev = (m_playFrame + naf - 1) % naf;
                    sprite.PaintAFrame(m_anim, prev, 0, 0, 0, 0, 0, 80);
                }
                if (naf > 0) sprite.PaintAFrame(m_anim, Mathf.Clamp(m_playFrame, 0, naf - 1), 0, 0, 0, 0, 0);
            }
            else if (frame >= 0 && frame < sprite.GetFramesNum())
            {
                sprite.PaintFrame(frame, 0, 0, 0, 0, 0);
                if (m_showRects && m_tab == Tab.Frames && m_frame >= 0)
                {
                    SpriteFrameDef f = m_doc.frames[m_frame];
                    for (int i = 0; i < f.rects.Count; i++)
                    {
                        SpriteRectDef r = f.rects[i];
                        SColor c = i == m_rect ? new SColor(120, 255, 220, 0) : new SColor(90, 255, 60, 60);
                        Gfx.draw2DRectangle(c, new r2i(Mathf.Min(r.x1, r.x2), Mathf.Min(r.y1, r.y2), Mathf.Max(r.x1, r.x2), Mathf.Max(r.y1, r.y2)));
                    }
                    if (m_part >= 0 && m_part < f.parts.Count)
                    {
                        r2i pr = PartRect(f.parts[m_part]);
                        Gfx.draw2DRectangle(new SColor(70, 80, 200, 255), pr);
                    }
                }
            }
            ASprite.PopTransform();
            Gfx.endScene2D();
        }

        r2i PartRect(SpritePartDef p)
        {
            if (p.hyper)
            {
                int fi = m_doc.FrameIndex(p.module);
                ASprite s = Sprite;
                if (s == null || fi < 0) return new r2i(p.x, p.y, p.x + 4, p.y + 4);
                return new r2i(p.x + s.GetFrameLeft(fi), p.y + s.GetFrameTop(fi), p.x + s.GetFrameLeft(fi) + s.GetFrameWidth(fi), p.y + s.GetFrameTop(fi) + s.GetFrameHeight(fi));
            }
            int mi = m_doc.ModuleIndex(p.module);
            if (mi < 0) return new r2i(p.x, p.y, p.x + 4, p.y + 4);
            SpriteModuleDef md = m_doc.modules[mi];
            int w = p.rot90 ? md.h : md.w, h = p.rot90 ? md.w : md.h;
            return new r2i(p.x, p.y, p.x + w, p.y + h);
        }

        void HandlePreviewMouse(Rect rect, ASprite sprite)
        {
            Event e = Event.current;
            if (!rect.Contains(e.mousePosition) && !m_dragging) return;
            if (e.type == EventType.ScrollWheel)
            {
                m_zoom = Mathf.Clamp(m_zoom * (e.delta.y > 0 ? 0.87f : 1.15f), 0.5f, 12f);
                e.Use();
                Repaint();
                return;
            }
            bool pan = e.button == 2 || (e.button == 0 && e.alt);
            if (e.type == EventType.MouseDrag && pan)
            {
                Rect g = GameRectIn(rect);
                m_pan += e.delta * (Gfx.SCREEN_W / g.width);
                e.Use();
                Repaint();
                return;
            }
            if (m_tab != Tab.Frames || m_frame < 0) return;
            SpriteFrameDef f = m_doc.frames[m_frame];
            Vector2 sp = GuiToSprite(rect, e.mousePosition);
            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
            {
                int hit = -1;
                for (int i = f.parts.Count - 1; i >= 0; i--)
                {
                    r2i r = PartRect(f.parts[i]);
                    if (sp.x >= r.UpperLeftCorner.X && sp.x < r.LowerRightCorner.X && sp.y >= r.UpperLeftCorner.Y && sp.y < r.LowerRightCorner.Y) { hit = i; break; }
                }
                if (hit < 0 && m_part >= 0) hit = m_part;
                if (hit >= 0)
                {
                    m_part = hit;
                    BeginEdit();
                    m_dragging = true;
                    m_dragStart = sp;
                    m_dragStartX = f.parts[hit].x;
                    m_dragStartY = f.parts[hit].y;
                }
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.MouseDrag && m_dragging && m_part >= 0 && m_part < f.parts.Count)
            {
                f.parts[m_part].x = m_dragStartX + Mathf.RoundToInt(sp.x - m_dragStart.x);
                f.parts[m_part].y = m_dragStartY + Mathf.RoundToInt(sp.y - m_dragStart.y);
                m_spriteDirty = true;
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.MouseUp && m_dragging)
            {
                m_dragging = false;
                EndEdit();
                e.Use();
            }
        }

        // ------------------------------------------------------------------ drawing helpers

        static void Outline(Rect r, Color c, float t)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, t), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - t, r.width, t), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, t, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        static void DrawChecker(Rect r)
        {
            EditorGUI.DrawRect(r, new Color(0.32f, 0.32f, 0.32f));
            const float s = 16;
            Color c = new Color(0.4f, 0.4f, 0.4f);
            for (float y = r.y; y < r.yMax; y += s)
                for (float x = r.x + (((int)((y - r.y) / s)) % 2) * s; x < r.xMax; x += 2 * s)
                    EditorGUI.DrawRect(new Rect(x, y, Mathf.Min(s, r.xMax - x), Mathf.Min(s, r.yMax - y)), c);
        }
    }
}
