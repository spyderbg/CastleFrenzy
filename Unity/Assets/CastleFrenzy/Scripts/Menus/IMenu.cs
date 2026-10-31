// Entities/IMenu: menu base with visual controls, sub-menus (sorted by key like irr::core::map) and transitions.
using System;
using System.Collections.Generic;
using static CastleFrenzy.G;
using static CastleFrenzy.GE;

namespace CastleFrenzy
{
    public abstract class IMenu
    {
        const int MAX_VISUAL_CONTROLS = 1000;

        readonly List<IVisualControl> m_visual_controls = new List<IVisualControl>();
        int m_menu_name;
        protected IMenu m_pParentMenu, m_pCurrentSubMenu;
        protected readonly SortedDictionary<string, IMenu> m_subMenus = new SortedDictionary<string, IMenu>(StringComparer.Ordinal);
        protected ASprite m_sprite;
        protected int m_frame_counter;
        protected bool m_isInitialized, m_isOnFocus;
        protected int m_music_id = -1;
        protected bool m_is_music_enable = true, m_is_music_loop;
        protected int m_transition_type = transition_none;
        protected int m_transition_action = -1;
        protected IVisualControl m_transition_control;
        protected int m_transition_alpha = 255;
        protected bool m_transition_is_finished;
        protected float m_transition_scale, m_transition_offset_y, m_transition_offset_x, m_transition_offset_max, m_transition_step;
        protected int m_transition_FramesCount, m_transition_val;

        protected IMenu(IMenu parent) { SetParent(parent); }

        protected virtual void LoadResources() { m_sprite = GetParent().GetSprite(); }
        protected virtual void FreeResources() { }

        public abstract void Initialize();
        public abstract void Destroy();
        public bool IsInitialized() { return m_isInitialized; }

        public virtual void GainFocus()
        {
            if (!m_isInitialized || m_isOnFocus) return;
            if (m_pCurrentSubMenu != null) m_pCurrentSubMenu.GainFocus();
            VisualControls_SetEnableTE(true);
            m_isOnFocus = true;
            Transition_Start(transition_show);
        }

        public virtual void LoseFocus()
        {
            if (!m_isInitialized) return;
            SubMenus_LoseFocus();
            VisualControls_SetEnableTE(false);
            m_isOnFocus = false;
        }

        public virtual bool IsOnFocus() { return m_isOnFocus; }
        public void SetMusicEnable(bool enable) { m_is_music_enable = enable; }

        public void SetMusicId(int music_id, bool bLoop = true)
        {
            m_music_id = music_id;
            m_is_music_loop = bLoop;
            PlayMusic();
        }

        public int GetMusicId() { return m_music_id; }

        public void PlayMusic()
        {
            if (!m_is_music_enable || m_music_id == -1) return;
            __MGR_SOUNDS().stopAllMusics();
            __MGR_SOUNDS().play(m_music_id, true);
        }

        public void UpdateMenuMusic()
        {
            if (m_is_music_enable && m_music_id != -1) __MGR_SOUNDS().playIfNotPlaying(m_music_id);
        }

        public void SetSprite(ASprite sprite) { m_sprite = sprite; }
        public ASprite GetSprite() { return m_sprite; }

        public virtual void Draw()
        {
            if (!m_isInitialized) return;
            VisualControls_Draw();
            if (m_pCurrentSubMenu != null) m_pCurrentSubMenu.Draw();
        }

        public virtual void Update(float dt)
        {
            m_frame_counter++;
            if (!m_isInitialized) return;
            Transition_Update();
            VisualControls_Update(dt);
            if (m_pCurrentSubMenu != null) m_pCurrentSubMenu.Update(dt);
        }

        public abstract void VisualControlDoAction(IVisualControl visual_control, int action_id);

        public void VisualControlAdd(IVisualControl visual_control)
        {
            if (m_visual_controls.Count <= MAX_VISUAL_CONTROLS) m_visual_controls.Add(visual_control);
        }

        public void VisualControlAction_NotifyMenus(IMenu this_menu, IVisualControl visual_control, int action_id)
        {
            foreach (IMenu sub in new List<IMenu>(this_menu.m_subMenus.Values)) sub.VisualControlAction_NotifyMenus(sub, visual_control, action_id);
            this_menu.VisualControlDoAction(visual_control, action_id);
        }

        public IVisualControl VisualControlGetByActionId(int action_id) { return m_visual_controls.Find(c => c.GetActionId() == action_id); }
        public IVisualControl VisualControlGetById(int control_id) { return m_visual_controls.Find(c => c.GetControlId() == control_id); }
        public void VisualControls_SetEnable(bool e) { foreach (IVisualControl c in m_visual_controls) c.SetEnabled(e); }
        public void VisualControls_SetEnableTE(bool e) { foreach (IVisualControl c in m_visual_controls) c.SetEnabledTE(e); }
        public void VisualControls_SetSelected(bool s) { foreach (IVisualControl c in m_visual_controls) c.SetSelected(s); }
        public void SetVisibleFlagToControls(bool flag) { foreach (IVisualControl c in m_visual_controls) c.SetVisible(flag); }

        public void VisualControls_Draw()
        {
            for (int i = 0; i < m_visual_controls.Count; i++) m_visual_controls[i].Draw();
        }

        public void VisualControls_Update(float dt)
        {
            for (int i = 0; i < m_visual_controls.Count; i++)
            {
                IVisualControl c = m_visual_controls[i];
                int action = c.Update(dt);
                if (action != -1 && !Transition_IsGoing()) VisualControlDoAction(c, action);
            }
        }

        public void VisualControls_Free()
        {
            foreach (IVisualControl c in m_visual_controls) c.Dispose();
            m_visual_controls.Clear();
        }

        public void VisualControls_Reset()
        {
            foreach (IMenu sub in m_subMenus.Values) sub.VisualControls_Reset();
            foreach (IVisualControl c in m_visual_controls) c.Reset();
        }

        public void VisualControls_StartTransition(int type, int param1 = 0, int param2 = 0)
        {
            foreach (IVisualControl c in m_visual_controls)
                if (c.GetValue() != -2) c.Transition_Start(type, param1, param2);
        }

        public virtual void Transition_Start(int type, int action_id = -1, int param1 = 0, int param2 = 0)
        {
            m_transition_type = type;
            m_transition_action = action_id;
            m_frame_counter = 0;
            VisualControls_StartTransition(type, param1, param2);
            switch (type)
            {
                case transition_show: m_transition_alpha = 80; m_transition_is_finished = false; break;
                case transition_hide: m_transition_alpha = 255; m_transition_is_finished = false; break;
                case transition_showLeft:
                case transition_showRight:
                    m_transition_step = param2;
                    m_transition_offset_x = param1;
                    m_transition_offset_max = param1;
                    m_transition_is_finished = false;
                    break;
                case transition_hideLeft:
                case transition_hideRight:
                    m_transition_step = param2;
                    m_transition_offset_x = 0;
                    m_transition_offset_max = param1;
                    m_transition_is_finished = false;
                    break;
                case transition_showWithZoom: m_transition_scale = 0; break;
                case transition_hideWithZoom: m_transition_scale = 100; break;
                case transition_stretch_show:
                case transition_stretch_hide: m_transition_FramesCount = param1; break;
            }
        }

        public virtual void Transition_Update()
        {
            if (m_transition_type == transition_finishing) m_transition_type = transition_none;
            if (m_transition_is_finished)
            {
                Transition_Finish();
                m_transition_is_finished = false;
            }
            switch (m_transition_type)
            {
                case transition_show:
                    m_transition_alpha += (255 - m_transition_alpha) / 3;
                    if (m_transition_alpha >= 255 - 3) m_transition_alpha = 255;
                    if (m_transition_alpha == 255) m_transition_is_finished = true;
                    break;
                case transition_hide:
                    m_transition_alpha -= m_transition_alpha / 3;
                    if (m_transition_alpha <= 55) m_transition_alpha = 0;
                    if (m_transition_alpha == 0) m_transition_is_finished = true;
                    break;
                case transition_showRight:
                    m_transition_offset_x -= m_transition_offset_x / 2;
                    if (m_transition_offset_x <= 2) m_transition_offset_x = 0;
                    if (m_transition_offset_x == 0) m_transition_is_finished = true;
                    break;
                case transition_showWithZoom:
                    m_transition_scale += (100 - m_transition_scale) / 2;
                    if (m_transition_scale >= 99) m_transition_scale = 100;
                    if (m_transition_scale == 100) m_transition_is_finished = true;
                    break;
                case transition_hideWithZoom:
                    m_transition_scale -= m_transition_scale / 2;
                    if (m_transition_scale <= 1) m_transition_scale = 0;
                    if (m_transition_scale == 0) m_transition_is_finished = true;
                    break;
                case transition_stretch_hide:
                    if (m_frame_counter == 1) m_transition_val = 1;
                    else if (m_frame_counter == 2 || m_frame_counter == 3 || m_frame_counter == 4) m_transition_val = 2;
                    if (m_frame_counter == 4) m_transition_is_finished = true;
                    m_transition_alpha = CLAMP((4 - m_frame_counter) * 255 / 4 + 100, 0, 255);
                    break;
                case transition_stretch_show:
                    if (m_frame_counter == 2 || m_frame_counter == 3 || m_frame_counter == 6 || m_frame_counter == 7) m_transition_val = 1;
                    else if (m_frame_counter == 1) m_transition_val = 2;
                    else if (m_frame_counter == 4 || m_frame_counter == 5 || m_frame_counter == 8) m_transition_val = 0;
                    if (m_frame_counter == 8)
                    {
                        m_transition_val = 0;
                        m_transition_is_finished = true;
                    }
                    m_transition_alpha = CLAMP(m_frame_counter * 255 / 8 + 100, 0, 255);
                    break;
            }
        }

        public virtual void Transition_Finish()
        {
            m_transition_type = transition_finishing;
            if (m_transition_action != -1) VisualControlDoAction(m_transition_control, m_transition_action);
        }

        public bool Transition_IsGoing() { return m_transition_type != transition_none; }

        public int Transition_GetPercent()
        {
            switch (m_transition_type)
            {
                case transition_show:
                case transition_hide: return m_transition_alpha * 100 / 255;
                case transition_showWithZoom:
                case transition_hideWithZoom: return (int)m_transition_scale;
                default: return 100;
            }
        }

        public IMenu GetParent() { return m_pParentMenu; }
        public void SetParent(IMenu parent) { m_pParentMenu = parent; }

        public IMenu GetRootMenu()
        {
            IMenu cur = this;
            while (cur.GetParent() != null) cur = cur.GetParent();
            return cur;
        }

        public void SubMenu_Register(string key, IMenu sub_menu)
        {
            if (m_subMenus.ContainsKey(key)) { Logs.warning("Try to register already registred submenu " + key); return; }
            m_subMenus[key] = sub_menu;
        }

        public void SubMenu_GainFocus(IMenu sub_menu, bool trans_parent = false, bool trans_this = false)
        {
            m_pCurrentSubMenu = sub_menu;
            LoseFocus();
            if (m_pCurrentSubMenu != null) m_pCurrentSubMenu.GainFocus();
        }

        public void SubMenu_LoseFocus()
        {
            if (m_pCurrentSubMenu != null)
            {
                m_pCurrentSubMenu.LoseFocus();
                m_pCurrentSubMenu = null;
            }
            if (!IsOnFocus()) GainFocus();
        }

        public void SubMenu_Change(string key, bool gain_focus = true)
        {
            IMenu menu;
            if (!m_subMenus.TryGetValue(key, out menu) || menu == null || m_pCurrentSubMenu == menu) return;
            if (m_pCurrentSubMenu != null) m_pCurrentSubMenu.LoseFocus();
            m_pCurrentSubMenu = menu;
            if (gain_focus) m_pCurrentSubMenu.GainFocus();
        }

        public void SubMenu_Remove()
        {
            if (m_pCurrentSubMenu != null) m_pCurrentSubMenu.LoseFocus();
            m_pCurrentSubMenu = null;
        }

        public void SubMenu_Draw()
        {
            if (m_pCurrentSubMenu == null || !m_pCurrentSubMenu.IsInitialized()) return;
            m_pCurrentSubMenu.Draw();
        }

        public void SubMenu_Update(float dt)
        {
            if (m_pCurrentSubMenu == null || !m_pCurrentSubMenu.IsInitialized()) return;
            m_pCurrentSubMenu.Update(dt);
        }

        public bool SubMenu_Exists() { return m_pCurrentSubMenu != null; }

        public bool SubMenu_Is(string key)
        {
            IMenu m;
            return m_subMenus.TryGetValue(key, out m) && m == m_pCurrentSubMenu;
        }

        public int SubMenus_GetCount() { return m_subMenus.Count; }
        public IMenu SubMenu_Get(string key) { IMenu m; return m_subMenus.TryGetValue(key, out m) ? m : null; }

        public void SubMenus_LoadResources() { foreach (IMenu s in m_subMenus.Values) s.SubMenus_LoadResources(); }
        public void SubMenus_FreeResources() { foreach (IMenu s in m_subMenus.Values) s.SubMenus_FreeResources(); }

        public void SubMenus_LoseFocus()
        {
            if (m_subMenus.Count == 0) return;
            foreach (IMenu s in m_subMenus.Values) s.SubMenus_LoseFocus();
            m_pCurrentSubMenu = null;
        }

        public void SubMenus_Initialize() { foreach (IMenu s in new List<IMenu>(m_subMenus.Values)) s.Initialize(); }

        public void SubMenus_Destroy()
        {
            if (m_subMenus.Count == 0) return;
            List<IMenu> subs = new List<IMenu>(m_subMenus.Values);
            foreach (IMenu s in subs) s.Destroy();
            foreach (IMenu s in subs) s.VisualControls_Free();
        }

        public void SetMenuName(int text_id) { m_menu_name = text_id; }
        public int GetMenuName() { return m_menu_name; }
    }
}
