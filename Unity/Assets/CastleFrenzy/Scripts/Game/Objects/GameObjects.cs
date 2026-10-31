// Game/ColladaControl, Game/Objects/GameObject, Weapons/WeaponObject and Weapons/CastleGate.
using System.Collections.Generic;
using static CastleFrenzy.G;
using static CastleFrenzy.GE;
using static CastleFrenzy.Ids;

namespace CastleFrenzy
{
    /// <summary>Animation clip control with "anim_end"/"anim_next" and Max events for one Collada scene.</summary>
    public class ColladaControl
    {
        ISceneNode m_root;
        readonly List<string> m_events = new List<string>();
        int m_currentAnim = -1;
        int m_nextAnim = -1;
        float m_nextAnimSpeed;
        bool m_nextAnimLoop;

        CTimelineController Ctrl { get { return m_root.getFirstAnimator().getTimelineCtrl(); } }

        public void setRoot(ISceneNode root)
        {
            if (m_root != null && m_root.getFirstAnimator() != null)
            {
                Ctrl.setCallback(null, null);
                m_root.getFirstAnimator().setEventsCallback(null);
                m_root = null;
            }
            m_root = root;
            m_currentAnim = -1;
            if (m_root != null && m_root.getFirstAnimator() != null)
            {
                Ctrl.setCallback(AnimEndCallback, this);
                m_root.getFirstAnimator().setEventsCallback(EventsCallback, this);
            }
        }

        public void updateAtEndOfFrame(float time) { m_events.Clear(); }
        public ISceneNode getRoot() { return m_root; }

        public bool checkEvent(string name)
        {
            for (int i = 0; i < m_events.Count; ++i) if (m_events[i] == name) return true;
            return false;
        }

        public void setAnim(string name, bool loop = true, float speed = 1.0f) { setAnim(getAnimIndex(name), loop, speed); }

        public void setAnim(int index, bool loop = true, float speed = 1.0f)
        {
            CTimelineController ctrl = Ctrl;
            ctrl.setClip(index);
            ctrl.setLoop(loop);
            ctrl.setScale(speed);
            m_currentAnim = index;
            m_nextAnim = -1;
        }

        public void setNextAnim(string name, bool loop = true, float speed = 1.0f) { setNextAnim(getAnimIndex(name), loop, speed); }
        public void setNextAnim(int index, bool loop = true, float speed = 1.0f) { m_nextAnim = index; m_nextAnimLoop = loop; m_nextAnimSpeed = speed; }

        public void setAnimAnim(string n1, string n2, bool loop2 = true, float s1 = 1.0f, float s2 = 1.0f) { setAnimAnim(getAnimIndex(n1), getAnimIndex(n2), loop2, s1, s2); }

        public void setAnimAnim(int i1, int i2, bool loop2 = true, float s1 = 1.0f, float s2 = 1.0f)
        {
            setAnim(i1, false, s1);
            setNextAnim(i2, loop2, s2);
        }

        public string getCurrentAnimName() { return getAnimName(getCurrentAnimIndex()); }
        public int getCurrentAnimIndex() { return m_currentAnim; }
        public int getAnimIndex(string name) { return Ctrl.getClipIndex(name); }
        public string getAnimName(int index) { return Ctrl.getClipName(index) ?? ""; }

        static void EventsCallback(STriggeredEvent ev, object self) { ((ColladaControl)self).m_events.Add(ev.name); }

        static void AnimEndCallback(CTimelineController ctrl, object self)
        {
            ColladaControl me = (ColladaControl)self;
            if (me.m_nextAnim != -1)
            {
                me.setAnim(me.m_nextAnim, me.m_nextAnimLoop, me.m_nextAnimSpeed);
                me.m_events.Add("anim_next");
            }
            else me.m_events.Add("anim_end");
        }
    }

    public class CGameObject
    {
        protected ISceneNode m_Node;
        protected CSceneManager m_SceneManager;
        protected bool m_animFrozen;
        protected float m_animOldScale;

        public CGameObject(IAttributes attributes) { m_SceneManager = CSceneManager.Instance; }

        public virtual void update(float time) { }
        public virtual void draw() { }
        public virtual void setVisible(bool visible) { m_Node.setVisible(visible); }
        public virtual bool isVisible() { return m_Node.isVisible(); }
        public ISceneNode getSceneNode() { return m_Node; }
        public void setPosition(v3f pos) { m_Node.setPosition(pos); }
        public v3f getPosition() { return m_Node.getPosition(); }
        public v3f getAbsolutePosition() { return m_Node.getAbsolutePosition(); }
        public float distanceTo(CGameObject target) { return (m_Node.getAbsolutePosition() - target.m_Node.getAbsolutePosition()).getLength(); }

        public float distanceToXZ(CGameObject target)
        {
            v3f diff = m_Node.getAbsolutePosition() - target.m_Node.getAbsolutePosition();
            diff.Y = 0.0f;
            return diff.getLength();
        }

        public virtual void freezeAnimation(bool freeze)
        {
            if (freeze == m_animFrozen) return;
            m_animFrozen = freeze;
            if (m_Node == null || m_Node.getAnimators().Count == 0) return;
            CTimelineController t = m_Node.getFirstAnimator().getTimelineCtrl();
            if (freeze) { m_animOldScale = t.getScale(); t.setScale(0.0f); }
            else t.setScale(m_animOldScale);
        }

        /// <summary>Releases scene nodes and touch elements (the C++ destructor).</summary>
        public virtual void Dispose() { }
    }

    public abstract class CWeaponObject : CGameObject
    {
        protected int m_level;
        protected float m_scale = 1.0f, m_baseScale = 1.0f;
        protected float m_readyTime, m_cooldownTime;

        protected CWeaponObject(IAttributes attributes) : base(attributes) { }

        public abstract int getType();
        public abstract void setShopState();
        protected abstract void adjustStats(IAttributes attributes);

        public virtual void reset() { m_readyTime = 1000.0f; m_cooldownTime = 0.0f; }
        public virtual float getCooldown() { return 0; }

        public void setLevel(int level)
        {
            m_level = level;
            IAttributes attr;
            if (getType() == WT_Gate && level == WL_None) attr = CGameConfig.GetInstance().getAttributes("gate", "Gate01");
            else
            {
                if (level == WL_None && getType() != WT_Spell) level = WL_1;
                attr = SUpgradeInfo.GetWeaponInfo(getType(), level).getAttributes();
            }
            if (attr == null) attr = new IAttributes();
            setBaseScale(GetFloatWithDefault(attr, "Scale", 1.0f));
            adjustStats(attr);
        }

        public int getLevel() { return m_level; }

        public void setScale(float scale)
        {
            m_scale = scale;
            getSceneNode().setScale(new v3f(1, 1, 1) * m_scale * m_baseScale);
        }

        protected void setBaseScale(float scale) { m_baseScale = scale; setScale(m_scale); }

        public override void update(float time)
        {
            if (getCooldown() == 0.0f) { m_readyTime += time; m_cooldownTime = 0.0f; }
            else { m_readyTime = 0.0f; m_cooldownTime += time; }
        }

        public void drawCooldown(p2i pos)
        {
            const float blinkDuration = 1.0f, fadeDuration = 0.5f, blinkCount = 4.0f;
            float cooldown = getCooldown();
            if (cooldown == 0.0f && m_readyTime >= blinkDuration + fadeDuration) return;
            ASprite sprite = CPlayfield.GetInstance().getSpriteIngame();
            r2i clip;
            sprite.GetFrameRect(out clip, MENU_F_MENU_COOLDOWN_FILL, 0);
            clip = clip + pos;
            int alpha;
            if (cooldown == 0.0f) alpha = (int)(255 * (1.0f - (m_readyTime - blinkDuration) / fadeDuration));
            else alpha = (int)(255 * m_cooldownTime / fadeDuration);
            alpha = CLAMP(alpha, 0, 255);
            if (cooldown == 0.0f && m_readyTime < blinkDuration)
            {
                int blink = (int)(blinkCount * m_readyTime / blinkDuration);
                if ((blink & 1) != 0) cooldown = 1.0f;
            }
            clip.LowerRightCorner.X = clip.UpperLeftCorner.X + (int)(clip.getWidth() * (1.0f - cooldown));
            sprite.PaintFrame(MENU_F_MENU_COOLDOWN_BG, pos.X, pos.Y, alpha);
            ASprite.PushClipRect(clip);
            sprite.PaintFrame(MENU_F_MENU_COOLDOWN_FILL, pos.X, pos.Y, alpha);
            ASprite.PopClipRect();
        }

        public virtual void setShopHighlight(float power)
        {
            SColor emissive = new SColor(0xAAAAAAAA).getInterpolated(new SColor(0x00000000), power);
            setEmissiveColor(m_Node, emissive);
        }
    }

    public class CCastleGate : CWeaponObject
    {
        float m_maxHealth, m_health, m_slowHealth, m_slowHealthVelocity;
        readonly ColladaControl m_gate = new ColladaControl();

        public CCastleGate() : base(null)
        {
            m_Node = CSceneManager.Instance.addEmptySceneNode();
            setLevel(WL_None);
            reset();
        }

        public override void Dispose()
        {
            m_Node.remove();
            if (m_gate.getRoot() != null) m_gate.getRoot().remove();
        }

        public override int getType() { return WT_Gate; }
        public float getHealth() { return m_health; }
        public float getSlowHealth() { return m_slowHealth; }
        public float getMaxHealth() { return m_maxHealth; }
        public float getHealthRatio() { return m_health / m_maxHealth; }
        public float getSlowHealthRatio() { return m_slowHealth / m_maxHealth; }
        public bool isDestroyed() { return m_health <= 0.0f && !CGameConfig.Invulnerable; }

        public override void update(float time)
        {
            base.update(time);
            if (m_slowHealth > m_health)
            {
                const float acceleration = -1300.0f;
                m_slowHealthVelocity += acceleration * time;
                m_slowHealth += (m_slowHealthVelocity < -900.0f ? m_slowHealthVelocity : 0.0f) * time;
                if (m_slowHealth < m_health) { m_slowHealth = m_health; m_slowHealthVelocity = 0.0f; }
            }
            m_gate.updateAtEndOfFrame(time);
        }

        public override void reset()
        {
            base.reset();
            m_health = m_maxHealth;
            m_slowHealth = m_maxHealth;
            m_slowHealthVelocity = 0.0f;
            m_gate.setAnim("stand", false);
            m_gate.getRoot().setVisible(true);
        }

        public void damageGate(float damage)
        {
            float oldHealth = m_health;
            damage = MIN(damage, m_health);
            if (damage <= 0.0f) return;
            m_health -= damage;
            CPlayfield.GetInstance().getChallenge().gateDamaged();
            if (m_health == 0.0f)
            {
                m_gate.setAnim("fall", false);
                CPlayfield.GetInstance().PlaySfx(SFX_CASTLE_DESTROY_GATE);
            }
            else if (oldHealth > m_maxHealth / 2 && m_health <= m_maxHealth / 2) m_gate.setAnim("deform", false);
            else if (m_health >= m_maxHealth / 2) m_gate.setAnim("stand_hit", false, 1.5f);
            else m_gate.setAnim("deform_hit", false, 1.5f);
        }

        protected override void adjustStats(IAttributes attributes)
        {
            m_maxHealth = attributes.getAttributeAsFloat("Health");
            ISceneNode toBeDeleted = m_gate.getRoot();
            ISceneNode node = LoadColladaScene(attributes.getAttributeAsString("File"));
            cleanNode(node);
            m_Node.addChild(node);
            m_gate.setRoot(node);
            m_gate.setAnim("stand");
            if (toBeDeleted != null) toBeDeleted.remove();
        }

        public override void setShopState() { m_gate.setAnim("stand"); }
    }
}
