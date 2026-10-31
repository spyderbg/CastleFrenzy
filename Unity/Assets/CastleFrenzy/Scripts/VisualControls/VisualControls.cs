// VisualControls/: IVisualControl and the Button, Sprite, Text, Container, Slider, Switch, Mesh and Pane controls.
using System.Collections.Generic;
using static CastleFrenzy.G;
using static CastleFrenzy.GE;
using static CastleFrenzy.Ids;

namespace CastleFrenzy
{
    public struct vcRect
    {
        public int x, y, w, h;
        public vcRect(int x, int y, int w, int h) { this.x = x; this.y = y; this.w = w; this.h = h; }
        public void set(int x1, int y1, int w1, int h1) { x = x1; y = y1; w = w1; h = h1; }
        public r2i toIrrRect() { return new r2i(x, y, x + w, y + h); }
    }

    public class vcTextInfo
    {
        public int m_textId = -1;
        public string m_text = "";
        public int m_textFlags;
        public string m_font_normal_id = "", m_font_pressed_id = "";
        public vcRect m_text_pos;

        public void reset()
        {
            m_textId = -1;
            m_text = "";
            m_font_normal_id = "";
            m_font_pressed_id = "";
            m_textFlags = 0;
        }
    }

    public abstract class IVisualControl
    {
        protected readonly vcTextInfo m_text1_info = new vcTextInfo(), m_text2_info = new vcTextInfo();
        protected p2i m_pos;
        protected d2i m_size;
        protected r2i m_clip;
        protected int m_frame_counter;
        protected int m_click_sfx_id, m_control_id, m_action_id, m_value;
        protected bool m_bVisible, m_bEnabled, m_bEnabledTE, m_is_selected;
        protected readonly CTouchElement m_TE = new CTouchElement();
        protected ASprite m_sprite;
        protected CSpriteInstance m_spriteInst;
        protected int m_transition_type = transition_none;
        protected bool m_transition_is_finished;
        protected int m_transition_alpha = 255;
        protected float m_transition_off_x, m_transition_off_max, m_transition_step;
        protected int m_transition_orig_x, m_transition_orig_y;

        protected IVisualControl(int action_id, int control_id, int click_sfx)
        {
            m_action_id = action_id;
            m_control_id = control_id;
            m_click_sfx_id = click_sfx;
            m_value = -1;
            m_bVisible = true;
            m_text1_info.reset();
            m_text2_info.reset();
            SetClipRect(new r2i(0, 0, OS_SCREEN_W, OS_SCREEN_H));
            m_TE.SetTapDetectionEnabled(true);
            // C++ calls the IVisualControl versions here (virtual dispatch is not active in a constructor);
            // C# would call the derived overrides, so call the base behaviour explicitly.
            m_is_selected = false;
            m_bEnabledTE = true;
            m_bEnabled = true;
            m_TE.SetEnabled(true);
        }

        /// <summary>The C++ destructor: unregisters the touch element.</summary>
        public virtual void Dispose()
        {
            m_TE.SetEnabled(false);
            m_TE.Dispose();
        }

        public abstract void Draw();
        public abstract int Update(float dt);
        public abstract void Reset();

        public virtual void Transition_Start(int type, int param1 = 0, int param2 = 0)
        {
            m_frame_counter = 0;
            m_transition_type = type;
            switch (type)
            {
                case transition_show: m_transition_alpha = 0; m_transition_is_finished = false; break;
                case transition_hide: m_transition_alpha = 255; m_transition_is_finished = false; break;
                case transition_showLeft:
                case transition_showRight:
                {
                    m_transition_orig_x = m_pos.X;
                    m_transition_off_x = param1;
                    m_transition_off_max = param1;
                    m_transition_step = param2;
                    float off = m_transition_type == transition_showLeft ? -m_transition_off_x : m_transition_off_x;
                    MoveControl((int)(m_transition_orig_x + off), 0);
                    m_transition_is_finished = false;
                    break;
                }
                case transition_hideLeft:
                case transition_hideRight:
                    m_transition_orig_x = m_pos.X;
                    m_transition_off_x = 0;
                    m_transition_off_max = param1;
                    m_transition_step = param2;
                    m_transition_is_finished = false;
                    break;
                case transition_stretch_show:
                case transition_stretch_hide:
                    m_transition_is_finished = false;
                    break;
                default:
                    m_transition_type = transition_none;
                    break;
            }
        }

        public virtual void Transition_Update()
        {
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
                case transition_showLeft:
                case transition_showRight:
                {
                    if (m_transition_off_x < 0) m_transition_off_x++;
                    else m_transition_off_x -= m_transition_step;
                    if (m_transition_off_x < -5) m_transition_off_x = -5;
                    if (m_transition_off_x == 0) m_transition_is_finished = true;
                    m_transition_alpha = CLAMP((int)((m_transition_off_max - m_transition_off_x) * 255 / m_transition_off_max), 0, 255);
                    float off = m_transition_type == transition_showLeft ? -m_transition_off_x : m_transition_off_x;
                    MoveControl((int)(m_transition_orig_x + off), 0);
                    break;
                }
                case transition_hideLeft:
                case transition_hideRight:
                {
                    if (m_frame_counter <= 3) break;
                    m_transition_off_x += m_transition_step;
                    if (m_transition_off_x >= m_transition_off_max)
                    {
                        m_transition_off_x = m_transition_off_max;
                        m_transition_is_finished = true;
                    }
                    m_transition_alpha = CLAMP((int)((m_transition_off_max - m_transition_off_x) * 255 / m_transition_off_max), 0, 255);
                    float off = m_transition_type == transition_hideLeft ? -m_transition_off_x : m_transition_off_x;
                    MoveControl((int)(m_transition_orig_x + off), 0);
                    break;
                }
                case transition_stretch_show:
                    m_transition_alpha = CLAMP(m_frame_counter * 255 / 8 + 100, 0, 255);
                    if (m_frame_counter == 8) m_transition_is_finished = true;
                    break;
                case transition_stretch_hide:
                    m_transition_alpha = CLAMP((4 - m_frame_counter) * 255 / 4 + 100, 0, 255);
                    if (m_frame_counter == 4) m_transition_is_finished = true;
                    break;
            }
        }

        public virtual void Transition_Finish() { m_transition_type = transition_none; }
        public bool Transition_IsGoing() { return m_transition_type != transition_none; }

        void SetText(vcTextInfo info, int textId, string text, string normal, string pressed, vcRect? clip, int flags)
        {
            info.m_textId = textId;
            if (text != null) info.m_text = text;
            info.m_textFlags = flags;
            info.m_font_normal_id = normal;
            info.m_font_pressed_id = pressed;
            info.m_text_pos = clip ?? new vcRect(0, 0, m_size.Width, m_size.Height);
        }

        public void SetText1(int textId, string normal, string pressed, vcRect? clip = null, int flags = CFont.HVCENTER) { SetText(m_text1_info, textId, null, normal, pressed, clip, flags); }
        public void SetText1(string text, string normal, string pressed, vcRect? clip = null, int flags = CFont.HVCENTER) { SetText(m_text1_info, -1, text, normal, pressed, clip, flags); }
        public void SetText2(int textId, string normal, string pressed, vcRect? clip = null, int flags = CFont.HVCENTER) { SetText(m_text2_info, textId, null, normal, pressed, clip, flags); }
        public void SetText2(string text, string normal, string pressed, vcRect? clip = null, int flags = CFont.HVCENTER) { SetText(m_text2_info, -1, text, normal, pressed, clip, flags); }

        public virtual void SetPos(p2i pos)
        {
            m_pos = pos;
            m_TE.SetScreenCoordinates(m_pos.X - m_size.Width / 2, m_pos.Y - m_size.Height / 2, m_size.Width, m_size.Height);
        }

        public p2i GetPos() { return m_pos; }

        public void SetSize(d2i size)
        {
            m_size = size;
            m_TE.SetScreenCoordinates(m_pos.X - m_size.Width / 2, m_pos.Y - m_size.Height / 2, m_size.Width, m_size.Height);
        }

        public d2i GetSize() { return m_size; }
        public void SetClipRect(r2i rect) { m_clip = rect; }
        public r2i GetClipRect() { return m_clip; }
        public virtual void SetArea(p2i pos, d2i size) { SetPos(pos); SetSize(size); }
        public virtual void SetArea(int x, int y, int w, int h) { SetPos(new p2i(x, y)); SetSize(new d2i(w, h)); }
        public virtual void SetArea(r2i area) { SetPos(area.getCenter()); SetSize(area.getSize()); }
        public r2i GetArea() { return new r2i(GetPos() - new p2i(GetSize().Width / 2, GetSize().Height / 2), GetSize()); }
        public void MoveArea(int xOff, int yOff) { SetPos(new p2i(m_pos.X + xOff, m_pos.Y + yOff)); }
        public void MoveControl(int x, int y) { SetPos(GetPos() + new p2i(x, y)); }

        public virtual void SetEnabledTE(bool is_enabled)
        {
            m_bEnabledTE = is_enabled;
            m_TE.SetEnabled(m_bEnabled && m_bEnabledTE);
        }

        public virtual void SetEnabled(bool is_enabled)
        {
            m_bEnabled = is_enabled;
            m_TE.SetEnabled(m_bEnabled && m_bEnabledTE);
        }

        public virtual void SetSelected(bool is_selected) { m_is_selected = is_selected; }
        public bool GetSelected() { return m_is_selected; }
        public void SetVisible(bool is_visible, bool is_enabled = true) { m_bVisible = is_visible; }
        public bool GetVisible() { return m_bVisible; }
        public void SetValue(int value) { m_value = value; }
        public int GetValue() { return m_value; }
        public int GetActionId() { return m_action_id; }
        public int GetControlId() { return m_control_id; }
        public CTouchElement GetTouchElement() { return m_TE; }
    }

    public class VisualControlButton : IVisualControl
    {
        public const int button_anim_normal = 0, button_anim_pressed = 1, button_anim_blink = 2, button_anim_disabled = 3, button_anim_number = 4;

        protected readonly int[] m_animations = new int[button_anim_number];
        protected int m_curr_anim = button_anim_normal;
        protected bool m_TE_resetInputOnDragOut;
        protected int m_disabledTextAlpha = 128;
        protected bool m_keepSelection;

        public VisualControlButton(int action_id, int control_id = -1, int click_sfx = SFX_MENU_SELECT) : base(action_id, control_id, click_sfx) { }

        public void SetResetTEWhenDragOut(bool enabled) { m_TE_resetInputOnDragOut = enabled; }
        public void SetDisabledTextAlpha(int alpha) { m_disabledTextAlpha = alpha; }
        public void SetKeepSelection(bool keep) { m_keepSelection = keep; }
        public bool IsPressed() { return m_curr_anim == button_anim_pressed; }

        void DrawText(vcTextInfo info, ASprite sprite, int frame_idx, int rectIdx)
        {
            string text = info.m_textId != -1 ? _TXT_INT(info.m_textId) : info.m_text;
            if (string.IsNullOrEmpty(text)) return;
            int alpha = m_transition_alpha;
            if (!m_bEnabled) alpha = (alpha * m_disabledTextAlpha) >> 8;
            alpha = CLAMP(alpha, 0, 255);
            CFont font = __FONT(m_curr_anim == button_anim_pressed ? info.m_font_pressed_id : info.m_font_normal_id);
            int x = m_pos.X + info.m_text_pos.x;
            int y = m_pos.Y + info.m_text_pos.y;
            int w = info.m_text_pos.w != 0 ? info.m_text_pos.w : m_size.Width;
            if (frame_idx >= 0 && sprite.GetFrameRectsNum(frame_idx) > rectIdx)
            {
                r2i rect;
                sprite.GetFrameRect(out rect, frame_idx, rectIdx);
                if ((info.m_textFlags & CFont.LEFT) != 0) x = m_pos.X + rect.UpperLeftCorner.X;
                else if ((info.m_textFlags & CFont.RIGHT) != 0) x = m_pos.X + rect.LowerRightCorner.X;
                else x = m_pos.X + (rect.UpperLeftCorner.X + rect.LowerRightCorner.X) / 2;
                y = m_pos.Y + (rect.UpperLeftCorner.Y + rect.LowerRightCorner.Y) / 2;
                w = rect.LowerRightCorner.X - rect.UpperLeftCorner.X;
            }
            if (font != null) font.DrawWrappedText(text, x, y, w, info.m_textFlags, alpha);
        }

        public override void Draw()
        {
            if (!m_bVisible || m_spriteInst == null) return;
            r2i visibleRect = new r2i(0, 0, GAME_SCREEN_W, GAME_SCREEN_H);
            visibleRect.clipAgainst(GetArea());
            if (visibleRect.getWidth() == 0 || visibleRect.getHeight() == 0) return;
            ASprite.PushClipRect(m_clip);
            m_spriteInst.SetPosition(m_pos.X, m_pos.Y, 0);
            m_spriteInst.SetAlpha(m_transition_alpha);
            m_spriteInst.Render();
            ASprite sprite = m_spriteInst.GetSprite();
            int frame_idx = sprite.GetAnimFrame(m_spriteInst.GetCurrAnimIdx(), m_spriteInst.GetLastPlayedFrameIdx());
            DrawText(m_text1_info, sprite, frame_idx, 0);
            DrawText(m_text2_info, sprite, frame_idx, 1);
            ASprite.PopClipRect();
        }

        public override int Update(float dt)
        {
            m_frame_counter++;
            Transition_Update();
            if (!m_bVisible || m_spriteInst == null) return -1;
            m_spriteInst.update(dt);
            if (Transition_IsGoing())
            {
                SetCurrentAnim(m_bEnabled ? (m_is_selected ? button_anim_pressed : button_anim_normal) : button_anim_disabled);
                m_TE.ResetInput();
                return -1;
            }
            if (m_curr_anim == button_anim_blink)
            {
                if (!m_spriteInst.IsPlaying()) return -1;
                m_TE.DisableAllOtherElements(false);
                m_TE.ResetInput();
                if (m_keepSelection) { SetCurrentAnim(button_anim_pressed); m_is_selected = true; }
                else { SetCurrentAnim(button_anim_normal); m_is_selected = false; }
                return m_action_id;
            }
            if (m_TE.IsNowReleased())
            {
                if (!m_is_selected && m_curr_anim == button_anim_pressed)
                {
                    SetCurrentAnim(button_anim_blink, false);
                    if (m_click_sfx_id != -1) __MGR_SOUNDS().play(m_click_sfx_id);
                }
                else
                {
                    m_TE.DisableAllOtherElements(false);
                    m_TE.ResetInput();
                }
            }
            else if (m_TE.IsActive())
            {
                if (m_TE.IsNowActivated())
                {
                    if (!m_is_selected)
                    {
                        m_TE.DisableAllOtherElements(true);
                        SetCurrentAnim(button_anim_pressed, false);
                    }
                    return button_action_gain_focus;
                }
                if (!m_TE.IsTouchInsideElement())
                {
                    if (!m_is_selected)
                    {
                        if (m_TE_resetInputOnDragOut)
                        {
                            m_TE.DisableAllOtherElements(false);
                            m_TE.ResetInput();
                        }
                        SetCurrentAnim(button_anim_normal, false);
                    }
                    return button_action_lose_focus;
                }
                if (!m_is_selected) SetCurrentAnim(button_anim_pressed, false);
                return button_action_gain_focus;
            }
            return -1;
        }

        public override void SetEnabled(bool is_enabled)
        {
            SetCurrentAnim(is_enabled ? button_anim_normal : button_anim_disabled);
            base.SetEnabled(is_enabled);
        }

        public override void SetSelected(bool is_selected)
        {
            SetCurrentAnim(is_selected ? button_anim_pressed : button_anim_normal);
            m_is_selected = is_selected;
        }

        public override void Reset()
        {
            if (m_TE.IsActive() && m_curr_anim == button_anim_pressed)
            {
                m_TE.ResetInput();
                if (!m_is_selected) SetCurrentAnim(button_anim_normal);
                m_is_selected = false;
            }
        }

        public void SetAnimations(ASprite sprite, params int[] anims)
        {
            m_spriteInst = new CSpriteInstance(sprite);
            for (int i = 0; i < anims.Length && i < button_anim_number; i++) m_animations[i] = anims[i];
            SetCurrentAnim(button_anim_normal);
        }

        public bool SetCurrentAnim(int anim_type, bool loop = true)
        {
            if (m_spriteInst == null) return false;
            if (anim_type == button_anim_disabled && m_animations[button_anim_disabled] == 0) anim_type = button_anim_normal;
            m_curr_anim = anim_type;
            m_spriteInst.StartAnimation(m_animations[m_curr_anim]);
            return true;
        }
    }

    public class VisualControlSprite : IVisualControl
    {
        protected CSpriteInstance m_spriteInstance;

        public VisualControlSprite(int control_id = -1) : base(-1, control_id, -1)
        {
            base.SetEnabledTE(false);
            SetSelected(false);
            m_bVisible = false;
        }

        public void SetAnimation(ASprite sprite, int animId)
        {
            if (m_spriteInstance != null) return;
            m_spriteInstance = new CSpriteInstance(sprite);
            m_spriteInstance.StartAnimation(animId);
        }

        public override void Draw()
        {
            if (!m_bVisible || m_spriteInstance == null) return;
            ASprite.PushClipRect(m_clip);
            m_spriteInstance.SetPosition(m_pos.X, m_pos.Y, -10);
            m_spriteInstance.Render();
            ASprite.PopClipRect();
        }

        public override int Update(float dt)
        {
            m_frame_counter++;
            Transition_Update();
            if (!m_bVisible || Transition_IsGoing()) return -1;
            if (m_spriteInstance != null) m_spriteInstance.update(dt);
            return -1;
        }

        public override void SetEnabledTE(bool is_enabled) { }
        public override void SetEnabled(bool is_enabled) { }
        public override void Reset() { }
    }

    public class VisualControlText : IVisualControl
    {
        protected bool m_useForcedColor;
        protected uint m_forcedTextColor = 0xFFFFFFFF;
        protected string m_text = "";
        protected int m_textId = -1;
        protected string m_font = "";
        protected int m_align, m_disabledTextAlpha;
        protected int m_max_width = 480, m_max_height = 320;

        public VisualControlText(int control_id = -1) : base(-1, control_id, -1)
        {
            base.SetEnabledTE(false);
            SetSelected(false);
        }

        public override void Draw()
        {
            if (!m_bVisible) return;
            string text = m_textId != -1 ? _TXT_INT(m_textId) : m_text;
            if (string.IsNullOrEmpty(text)) return;
            r2i visibleRect = new r2i(0, 0, GAME_SCREEN_W, GAME_SCREEN_H);
            visibleRect.clipAgainst(GetTextArea());
            if (visibleRect.getWidth() == 0 || visibleRect.getHeight() == 0) return;
            int alpha = m_transition_alpha;
            if (!m_bEnabled) alpha = (alpha * m_disabledTextAlpha) >> 8;
            alpha = CLAMP(alpha, 0, 255);
            CFont font = __FONT(m_font);
            if (font == null) return;
            ASprite.PushClipRect(m_clip);
            if (!m_useForcedColor) font.DrawWrappedText(text, m_pos.X, m_pos.Y, m_max_width, m_align, alpha);
            else
            {
                SColor oldColor = font.GetColor1();
                font.SetColor1(new SColor(m_forcedTextColor));
                font.DrawWrappedText(text, m_pos.X, m_pos.Y, m_max_width, m_align, alpha);
                font.SetColor1(oldColor);
            }
            ASprite.PopClipRect();
        }

        public override int Update(float dt)
        {
            m_frame_counter++;
            Transition_Update();
            return -1;
        }

        public override void SetEnabledTE(bool is_enabled) { }
        public override void SetEnabled(bool is_enabled) { }
        public override void Reset() { }

        public void RecalculateSize()
        {
            string text = m_textId != -1 ? _TXT_INT(m_textId) : m_text;
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(m_font)) SetSize(new d2i(0, 0));
            else
            {
                d2f size = __FONT(m_font).GetStringSize(text);
                SetSize(new d2i((int)size.Width, (int)size.Height));
            }
        }

        public void SetText(string text) { m_text = text ?? ""; RecalculateSize(); }
        public void SetText(int textId) { m_textId = textId; RecalculateSize(); }
        public string GetText() { return m_textId != -1 ? _TXT_INT(m_textId) : m_text; }
        public void SetFont(string fontName) { m_font = fontName; RecalculateSize(); }
        public void SetRegion(int mwidth, int mheight) { m_max_width = mwidth; m_max_height = mheight; RecalculateSize(); }
        public void SetFontColor(uint forced_color) { m_useForcedColor = true; m_forcedTextColor = forced_color; }
        public void SetAlign(int align) { m_align = align; }

        public r2i GetTextArea()
        {
            p2i pos = GetPos();
            d2i size = GetSize();
            if ((m_align & CFont.HCENTER) != 0) pos.X -= size.Width / 2;
            if ((m_align & CFont.VCENTER) != 0) pos.Y -= size.Height / 2;
            if ((m_align & CFont.RIGHT) != 0) pos.X -= size.Width;
            if ((m_align & CFont.BOTTOM) != 0) pos.Y -= size.Height;
            return new r2i(pos, size);
        }
    }

    public class VisualControlContainer : IVisualControl
    {
        protected readonly List<IVisualControl> m_controls = new List<IVisualControl>();
        protected readonly List<p2i> m_startPos = new List<p2i>();

        public VisualControlContainer() : base(-1, -1, -1)
        {
            base.SetEnabledTE(false);
            SetSelected(false);
        }

        public override void Dispose() { ReleaseControls(); base.Dispose(); }

        public void ReleaseControls()
        {
            foreach (IVisualControl c in m_controls) c.Dispose();
            m_controls.Clear();
            m_startPos.Clear();
        }

        public void AddControl(IVisualControl ptr)
        {
            m_controls.Add(ptr);
            m_startPos.Add(ptr.GetPos());
        }

        public void RemoveControl(IVisualControl ptr)
        {
            int i = m_controls.IndexOf(ptr);
            if (i < 0) return;
            ptr.Dispose();
            m_controls.RemoveAt(i);
            m_startPos.RemoveAt(i);
        }

        public override void Draw()
        {
            if (!m_bVisible) return;
            foreach (IVisualControl c in m_controls) c.Draw();
        }

        public override int Update(float dt)
        {
            if (!m_bVisible) return -1;
            foreach (IVisualControl c in m_controls) c.Update(dt);
            return -1;
        }

        public override void Reset() { foreach (IVisualControl c in m_controls) c.Reset(); }

        public override void SetPos(p2i pos)
        {
            base.SetPos(pos);
            for (int i = 0; i < m_controls.Count; i++) m_controls[i].SetPos(pos + m_startPos[i]);
        }

        public override void SetEnabledTE(bool is_enabled) { }
        public override void SetEnabled(bool is_enabled) { foreach (IVisualControl c in m_controls) c.SetEnabled(is_enabled); }
    }

    public class VisualControlSlider : IVisualControl
    {
        public const int slider_frame_empty = 0, slider_frame_fill = 1, slider_frame_btn = 2, slider_frame_btn_glow = 3, slider_frame_number = 4;
        protected readonly int[] m_frames = new int[slider_frame_number];
        protected int m_offset_x, m_min_x, m_max_x;

        public VisualControlSlider(int action_id, int click_sfx = -1) : base(action_id, -1, click_sfx) { m_value = 0; }

        public override void Draw()
        {
            if (!m_bVisible) return;
            m_sprite.PaintFrame(m_frames[slider_frame_empty], m_pos.X, m_pos.Y, m_transition_alpha);
            ASprite.PushClipRect(new r2i(m_pos.X + m_min_x, 0, m_pos.X + m_offset_x, 480));
            m_sprite.PaintFrame(m_frames[slider_frame_fill], m_pos.X, m_pos.Y, m_transition_alpha);
            ASprite.PopClipRect();
            int frame = m_is_selected ? m_frames[slider_frame_btn_glow] : m_frames[slider_frame_btn];
            if (frame >= 0 && frame < m_sprite.GetFrames()) m_sprite.PaintFrame(frame, m_pos.X + m_offset_x, m_pos.Y, m_transition_alpha);
        }

        public override int Update(float dt)
        {
            m_frame_counter++;
            Transition_Update();
            if (!m_bVisible || Transition_IsGoing()) return -1;
            if (m_TE.IsNowReleased())
            {
                m_TE.DisableAllOtherElements(false);
                m_is_selected = false;
                if (m_click_sfx_id != -1) __MGR_SOUNDS().play(m_click_sfx_id);
            }
            else if (m_TE.IsActive())
            {
                if (m_TE.IsNowActivated())
                {
                    m_TE.DisableAllOtherElements(true);
                    m_is_selected = true;
                }
                int px, py;
                m_TE.GetTouchPos_Current(out px, out py);
                m_offset_x = CLAMP(px - m_pos.X, m_min_x, m_max_x);
                UpdatePercent();
            }
            return m_is_selected ? m_action_id : -1;
        }

        public void SetFrames(ASprite sprite, params int[] frames)
        {
            m_sprite = sprite;
            for (int i = 0; i < frames.Length; i++) ChangeFrame(i, frames[i]);
            SetSliderFillSize();
        }

        public void ChangeFrame(int frame_id, int frame_value) { m_frames[frame_id] = frame_value; }
        void UpdatePercent() { m_value = IntRemap1(m_offset_x, m_min_x, m_max_x, 0, 100); }

        public void SetPercent(int percent)
        {
            percent = CLAMP(percent, 0, 100);
            m_value = percent;
            m_offset_x = IntRemap1(percent, 0, 100, m_min_x, m_max_x);
        }

        public int GetPercent() { return m_value; }

        public override void Reset()
        {
            if (m_TE.IsActive() && m_is_selected)
            {
                m_TE.ResetInput();
                m_is_selected = false;
            }
        }

        public override void SetEnabled(bool is_enabled) { }

        void SetSliderFillSize()
        {
            r2i fillRect;
            m_sprite.GetFrameRect(out fillRect, m_frames[slider_frame_fill], 0);
            m_min_x = fillRect.UpperLeftCorner.X;
            m_max_x = fillRect.LowerRightCorner.X;
            SetPercent(m_value);
        }
    }

    public class VisualControlSwitch : IVisualControl
    {
        public const int switch_frame_background = 0, switch_frame_background_top = 1, switch_frame_button = 2, switch_frame_number = 3;
        const int btn_gap = 5, btn_w = 64, btn_total_w = btn_w + btn_gap;
        protected readonly int[] m_frames = new int[switch_frame_number];
        protected int m_btn_offset_x, m_btn_offset_x_target;

        public VisualControlSwitch(int action_id, int click_sfx = -1) : base(action_id, -1, click_sfx)
        {
            m_is_selected = false;
            m_value = 0;
        }

        int MaxOffset { get { return m_size.Width - btn_total_w; } }

        public override void Draw()
        {
            if (!m_bVisible) return;
            m_sprite.PaintFrame(m_frames[switch_frame_background], m_pos.X, m_pos.Y, m_transition_alpha);
            m_sprite.PaintFrame(m_frames[switch_frame_button], m_pos.X + m_btn_offset_x, m_pos.Y, m_transition_alpha);
            m_sprite.PaintFrame(m_frames[switch_frame_background_top], m_pos.X, m_pos.Y, m_transition_alpha);
        }

        public override int Update(float dt)
        {
            m_frame_counter++;
            Transition_Update();
            if (!m_bVisible || Transition_IsGoing()) return -1;
            if (m_TE.IsNowReleased())
            {
                m_TE.DisableAllOtherElements(false);
                if (!m_is_selected && m_btn_offset_x != 0) m_btn_offset_x_target = MaxOffset;
                else if (m_is_selected && m_btn_offset_x != MaxOffset) m_btn_offset_x_target = 0;
                else
                {
                    m_value = m_is_selected ? 1 : 0;
                    return m_action_id;
                }
            }
            else if (m_TE.IsActive())
            {
                if (m_TE.IsNowActivated())
                {
                    m_TE.DisableAllOtherElements(true);
                    int px, py;
                    m_TE.GetTouchPos_Current(out px, out py);
                    m_btn_offset_x = px - m_pos.X < m_size.Width / 2 ? 0 : MaxOffset;
                    UpdateSwitchState();
                }
                else if (m_TE.GetCurrentTouchType() == CTouchPad.Type_Drag)
                {
                    int dx, dy;
                    m_TE.GetTouchPos_DT(out dx, out dy);
                    m_btn_offset_x = CLAMP(m_btn_offset_x + dx, 0, MaxOffset);
                    UpdateSwitchState();
                }
            }
            else if (UpdateSwitchButtonPos())
            {
                m_value = m_is_selected ? 1 : 0;
                return m_action_id;
            }
            return -1;
        }

        bool UpdateSwitchButtonPos()
        {
            if (m_btn_offset_x_target - m_btn_offset_x == 0) return false;
            int step = MaxOffset / 2;
            m_btn_offset_x = CLAMP(m_btn_offset_x + step * (m_btn_offset_x_target - m_btn_offset_x > 0 ? 1 : -1), 0, MaxOffset);
            return UpdateSwitchState();
        }

        bool UpdateSwitchState()
        {
            if (m_btn_offset_x != 0 && m_btn_offset_x != MaxOffset) return false;
            m_btn_offset_x_target = m_btn_offset_x;
            m_is_selected = m_btn_offset_x == MaxOffset;
            return true;
        }

        public override void SetSelected(bool is_selected)
        {
            m_value = is_selected ? 1 : 0;
            m_is_selected = is_selected;
            m_btn_offset_x = m_btn_offset_x_target = m_is_selected ? MaxOffset : 0;
        }

        public override void Reset()
        {
            if (!m_TE.IsActive()) return;
            m_TE.ResetInput();
            SetSelected(m_value != 0);
        }

        public override void SetEnabled(bool is_enabled) { }

        public void SetFrames(ASprite sprite, params int[] frames)
        {
            m_sprite = sprite;
            for (int i = 0; i < frames.Length; i++) m_frames[i] = frames[i];
        }
    }

    public class VisualControlMesh : IVisualControl
    {
        protected bool m_enabled;
        protected float m_sScale;
        protected ISceneNode m_node;
        protected v3f m_position, m_rotation;

        public VisualControlMesh(int control_id = -1) : base(-1, control_id, -1)
        {
            base.SetEnabledTE(false);
            SetSelected(false);
        }

        public void SetMesh(ISceneNode node) { m_node = node; }
        public void SetScale(float scalar) { m_sScale = scalar; }
        public void SetRotationRad(v3f rot) { m_rotation = rot; }
        public void SetRotationDeg(v3f rot) { m_rotation = rot * DEGTORAD; }

        public override void Draw()
        {
            if (m_node == null) return;
            if (!m_enabled) m_node.setVisible(false);
            else
            {
                m_node.setVisible(true);
                m_position = new v3f(-m_pos.X, m_pos.Y, -10);
                m_node.setPosition(m_position);
            }
        }

        public override void SetPos(p2i pos)
        {
            base.SetPos(pos);
            m_position = new v3f(-m_pos.X, m_pos.Y, -10);
            if (m_node != null) m_node.setPosition(m_position);
        }

        public override int Update(float dt)
        {
            if (!m_enabled || m_node == null) return -1;
            m_frame_counter++;
            Transition_Update();
            m_position = new v3f(-m_pos.X, m_pos.Y, -10);
            m_node.setPosition(m_position);
            m_node.setRotation(new quaternion(m_rotation));
            m_node.setScale(new v3f(m_sScale, m_sScale, m_sScale));
            m_node.updateAbsolutePosition();
            return -1;
        }

        public override void SetEnabledTE(bool is_enabled) { }

        public override void SetEnabled(bool is_enabled)
        {
            m_enabled = is_enabled;
            if (m_node != null) m_node.setVisible(is_enabled);
        }

        public override void Reset() { }
    }

    public class VisualControlPane : IVisualControl
    {
        public const int CPT_AllowContMove = 1, CPT_AlignControls = 1 << 1, CPT_Vertical = 1 << 2, CPT_Horizontal = 1 << 3, CPT_Cycle = 1 << 4;
        const int CPS_WaitTouch = 0, CPS_Moving = 1, CPS_Sliding = 2;

        int m_state = CPS_WaitTouch;
        protected readonly List<p2i> m_startPos = new List<p2i>();
        protected readonly List<IVisualControl> m_controls = new List<IVisualControl>();
        int m_type;
        d2i m_scrollSize;
        v2f m_minOffset, m_maxOffset, m_offset, m_velocity, m_acceleration, m_snapOffset;
        int m_rows = 1, m_cols = 1;

        static bool MASK_CHECK(int x, int mask) { return (x & mask) == mask; }

        public VisualControlPane(int action_id, int click_sfx = -1) : base(action_id, -1, click_sfx)
        {
            SetArea(0, 0, GAME_SCREEN_W, GAME_SCREEN_H);
            SetType(CPT_AllowContMove | CPT_Vertical | CPT_AlignControls);
        }

        public override void Draw() { }
        public override void Reset() { }
        public bool CanChangeType() { return m_state == CPS_WaitTouch; }
        public void SetType(int type) { if (CanChangeType()) m_type = type; }

        public void AddControl(IVisualControl ptr)
        {
            m_startPos.Add(ptr.GetPos());
            m_controls.Add(ptr);
        }

        public void RemoveControl(IVisualControl ptr)
        {
            int i = m_controls.IndexOf(ptr);
            if (i < 0) return;
            m_controls.RemoveAt(i);
            m_startPos.RemoveAt(i);
        }

        public void RemoveAllControls() { m_controls.Clear(); m_startPos.Clear(); }
        public List<IVisualControl> GetControls() { return m_controls; }

        public void ReturnToStartPos()
        {
            m_offset = new v2f(0, 0);
            m_snapOffset = m_offset;
            m_velocity = new v2f(0, 0);
            SetControlsPosition();
        }

        public void SetScrollSize(d2i size)
        {
            m_scrollSize = size;
            r2i screenArea = GetArea();
            m_minOffset.X = -MAX(0, m_scrollSize.Width - screenArea.getWidth());
            m_maxOffset.X = 0;
            m_minOffset.Y = -MAX(0, m_scrollSize.Height - screenArea.getHeight());
            m_maxOffset.Y = 0;
        }

        void SetControlsPosition(bool check_bounds = true)
        {
            if (check_bounds)
            {
                if (m_offset.X <= m_minOffset.X) m_offset.X = m_minOffset.X;
                if (m_offset.X >= m_maxOffset.X) m_offset.X = m_maxOffset.X;
                if (m_offset.Y <= m_minOffset.Y) m_offset.Y = m_minOffset.Y;
                if (m_offset.Y >= m_maxOffset.Y) m_offset.Y = m_maxOffset.Y;
            }
            r2i screenArea = GetArea();
            p2i offset = new p2i((int)m_offset.X, (int)m_offset.Y);
            for (int i = 0; i < m_controls.Count; i++)
            {
                p2i p = m_startPos[i] + offset;
                if (MASK_CHECK(m_type, CPT_Cycle))
                {
                    if (p.Y < -m_controls[i].GetArea().getHeight()) p.Y += m_scrollSize.Height;
                    if (p.X < -m_controls[i].GetArea().getWidth()) p.X += m_scrollSize.Width;
                }
                m_controls[i].SetPos(screenArea.UpperLeftCorner + p);
            }
        }

        public override int Update(float dt)
        {
            float time = dt / 1000;
            m_frame_counter++;
            Transition_Update();
            if (!m_bVisible || Transition_IsGoing()) return -1;
            if (m_TE.IsActive())
            {
                if (m_TE.IsNowActivated()) m_TE.DisableAllOtherElements(true);
                m_state = CPS_Moving;
            }
            else if (m_TE.IsNowReleased())
            {
                StartSliding();
                m_TE.DisableAllOtherElements(false);
                m_TE.ResetInput();
            }
            if (m_state == CPS_Moving) UpdateMoving(time);
            else if (m_state == CPS_Sliding) UpdateSliding(time);
            normalizeOffset();
            return m_is_selected ? m_action_id : -1;
        }

        void UpdateMoving(float time)
        {
            int dx, dy;
            m_TE.GetTouchPos_DT(out dx, out dy);
            v2f offset = new v2f(dx, dy);
            constraintOffset(ref offset);
            m_offset += offset;
            m_snapOffset = m_offset;
            if (time > 0) m_velocity = offset / time;
            SetControlsPosition(!MASK_CHECK(m_type, CPT_AllowContMove));
        }

        void StartSliding()
        {
            constraintVelocity(2 * GAME_SCREEN_H);
            m_snapOffset = m_offset + m_velocity / 3;
            if (!MASK_CHECK(m_type, CPT_Cycle))
            {
                m_snapOffset.X = CLAMP(m_snapOffset.X, m_minOffset.X, m_maxOffset.X);
                m_snapOffset.Y = CLAMP(m_snapOffset.Y, m_minOffset.Y, m_maxOffset.Y);
            }
            if (MASK_CHECK(m_type, CPT_AlignControls))
            {
                float colWidth = (float)m_scrollSize.Width / m_cols;
                float rowHeight = (float)m_scrollSize.Height / m_rows;
                if (colWidth > 0) m_snapOffset.X = colWidth * floorf(m_snapOffset.X / colWidth + 0.5f);
                if (rowHeight > 0) m_snapOffset.Y = rowHeight * floorf(m_snapOffset.Y / rowHeight + 0.5f);
            }
            m_state = CPS_Sliding;
        }

        void UpdateSliding(float time)
        {
            const float d = 10.0f, k = 30.0f;
            v2f dist = m_offset - m_snapOffset;
            m_acceleration = dist * -k - m_velocity * d;
            m_velocity += m_acceleration * time;
            m_offset += m_velocity * time;
            if (dist.getLength() <= 1.0f && m_velocity.getLength() < 30.0f)
            {
                m_offset = m_snapOffset;
                m_state = CPS_WaitTouch;
            }
            SetControlsPosition(false);
        }

        void constraintOffset(ref v2f offset)
        {
            if (MASK_CHECK(m_type, CPT_Horizontal)) offset.Y = 0;
            if (MASK_CHECK(m_type, CPT_Vertical)) offset.X = 0;
            if (MASK_CHECK(m_type, CPT_Cycle)) return;
            if (m_offset.Y <= m_minOffset.Y || m_offset.Y >= m_maxOffset.Y) offset.Y *= 0.5f;
            else if (m_offset.X <= m_minOffset.X || m_offset.X >= m_maxOffset.X) offset.X *= 0.5f;
        }

        void constraintVelocity(float max)
        {
            float len = m_velocity.getLength();
            if (len > max) m_velocity *= max / len;
        }

        void normalizeOffset()
        {
            if (!MASK_CHECK(m_type, CPT_Cycle)) return;
            float width = m_scrollSize.Width, height = m_scrollSize.Height;
            if (width > 0.0f)
            {
                while (m_offset.X < -width) { m_offset.X += width; m_snapOffset.X += width; }
                while (m_offset.X > 0) { m_offset.X -= width; m_snapOffset.X -= width; }
            }
            if (height > 0.0f)
            {
                while (m_offset.Y < -height) { m_offset.Y += height; m_snapOffset.Y += height; }
                while (m_offset.Y > 0) { m_offset.Y -= height; m_snapOffset.Y -= height; }
            }
        }

        public void SetRows(int rows) { m_rows = rows; }
        public void SetCols(int cols) { m_cols = cols; }
        public int GetRows() { return m_rows; }
        public int GetCols() { return m_cols; }
        public int GetCurrentRow() { float h = (float)m_scrollSize.Height / m_rows; return (int)-floorf(m_snapOffset.Y / h + 0.5f); }
        public int GetCurrentCol() { float w = (float)m_scrollSize.Width / m_cols; return (int)-floorf(m_snapOffset.X / w + 0.5f); }

        public void PushVertical(float rows, float dt)
        {
            float rowHeight = MASK_CHECK(m_type, CPT_AlignControls) ? m_scrollSize.Height / m_rows : 30;
            float accel = -12.0f * rows * rowHeight - 4.0f * m_velocity.Y;
            m_velocity.Y += accel * dt;
            StartSliding();
        }

        public void PushHorizontal(float cols, float dt)
        {
            float colWidth = MASK_CHECK(m_type, CPT_AlignControls) ? m_scrollSize.Width / m_cols : 30;
            float accel = -12.0f * cols * colWidth - 4.0f * m_velocity.X;
            m_velocity.X += accel * dt;
            StartSliding();
        }

        public void ScrollVertical(float rows)
        {
            m_velocity.Y = MASK_CHECK(m_type, CPT_AlignControls) ? -3.0f * rows * m_scrollSize.Height / m_rows : -3.0f * 30 * rows;
            StartSliding();
        }

        public void ScrollHorizontal(float cols)
        {
            m_velocity.X = MASK_CHECK(m_type, CPT_AlignControls) ? -3.0f * cols * m_scrollSize.Width / m_cols : -3.0f * 30 * cols;
            StartSliding();
        }

        public void GoToRow(int row, bool instant = false)
        {
            float rowHeight = (float)m_scrollSize.Height / m_rows;
            if (!instant) { m_snapOffset.Y = rowHeight * -row; m_state = CPS_Sliding; }
            else { m_offset.Y = rowHeight * -row; m_state = CPS_WaitTouch; }
        }

        public void GoToCol(int col, bool instant = false)
        {
            float colWidth = (float)m_scrollSize.Width / m_cols;
            if (!instant) { m_snapOffset.X = colWidth * -col; m_state = CPS_Sliding; }
            else { m_offset.X = colWidth * -col; m_state = CPS_WaitTouch; }
        }
    }
}
