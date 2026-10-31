// Game/Objects/Weapons/Iceball.
using System.Collections.Generic;
using static CastleFrenzy.G;
using static CastleFrenzy.GE;
using static CastleFrenzy.Ids;

namespace CastleFrenzy
{
    public class CIceball : CWeaponObject
    {
        public const int IS_Respawning = 0, IS_RespawningToReadyTransition = 1, IS_Ready = 2, IS_Dragged = 3, IS_Dying = 4, IS_Shop = 5, IS_Intro = 6,
            IS_Flying = 7, IS_Disabled = 8;

        static readonly v3f k_gravity = new v3f(0.0f, -9.8f * 3, 0.0f);
        const float k_spriteScale = 1.5f;
        const int k_touch_x = 0, k_touch_y = 65, k_touch_w = 80, k_touch_h = 120;

        protected float m_stat_cooldown, m_stat_radius, m_stat_groundIceRadius;
        protected int m_stat_maxIcedUnits;

        int m_state, m_oldState;
        float m_stateTime;
        v3f m_spawnPosition, m_velocity, m_smoothVelocity;
        readonly ISceneNode m_postament, m_shadow, m_light, m_groundIce;
        float m_groundIceTimer;

        struct PosScale
        {
            public v3f pos;
            public float scale;
            public PosScale(v3f p, float s) { pos = p; scale = s; }
        }

        readonly List<PosScale> m_drawItems = new List<PosScale>();
        p2i m_dragOffset;
        ASprite m_sprite;
        readonly ASprite m_spriteGlow;
        readonly ASpriteInstance m_inst = new ASpriteInstance();
        readonly CTouchElement m_touch;
        bool m_visible, m_visibleObject, m_visibleShadow, m_visibleEffect;

        public CIceball() : base(null)
        {
            m_Node = new CCallbackSceneNode(() => m_sprite != null ? m_sprite._material : null, draw);
            __IRR_SCENE().getRootSceneNode().addChild(m_Node);
            m_postament = LoadColladaScene("Scenes/Fireball/bowl_ice.bdae");
            cleanNode(m_postament);
            __IRR_SCENE().getRootSceneNode().addChild(m_postament);
            m_shadow = LoadColladaScene("Scenes/Goblin/shadow.bdae");
            cleanNode(m_shadow);
            __IRR_SCENE().getRootSceneNode().addChild(m_shadow);
            m_groundIce = LoadColladaScene("Scenes/Fireball/ice.bdae");
            cleanNode(m_groundIce);
            __IRR_SCENE().getRootSceneNode().addChild(m_groundIce);
            m_light = LoadColladaScene("Scenes/Fireball/fireball_light.bdae");
            cleanNode(m_light);
            __IRR_SCENE().getRootSceneNode().addChild(m_light);
            m_spriteGlow = new ASprite();
            m_spriteGlow.Load("Ingame/iceball_glow");
            m_spriteGlow._material.MaterialType = E_MATERIAL_TYPE.EMT_TRANSPARENT_ADD_COLOR;
            m_inst.StartAnim(m_sprite, 0, 0, 0);
            m_touch = new CTouchElement(CTouchElement.Type_Drag, 1000);
            m_touch.SetScreenCoordinates(k_touch_x, k_touch_y, k_touch_w, k_touch_h);
            setLevel(WL_None);
            reset();
        }

        public override int getType() { return WT_Iceball; }

        public void setSpawnPosition(ISceneNode node)
        {
            node.addChild(m_postament);
            m_spawnPosition = node.getAbsolutePosition() + new v3f(0, 2, 0);
            setPosition(m_spawnPosition);
        }

        public override void reset()
        {
            base.reset();
            if (m_level != WL_None)
            {
                m_sprite._material.ZBuffer = false;
                m_spriteGlow._material.ZBuffer = false;
                setShouldBeVisible(true, false, false);
                setPosition(m_spawnPosition);
                setState(IS_Ready);
            }
            else
            {
                setState(IS_Disabled);
                setShouldBeVisible(false, false, false);
            }
            setVisible(true);
        }

        public override void Dispose()
        {
            m_Node.remove();
            m_postament.remove();
            m_shadow.remove();
            m_light.remove();
            m_groundIce.remove();
            m_touch.Dispose();
        }

        protected override void adjustStats(IAttributes attributes)
        {
            string[] cooldown = { "CooldownEasy", "CooldownMedium", "CooldownHard" };
            int difficulty = CPlayfield.GetInstance().getDifficulty();
            m_stat_cooldown = attributes.getAttributeAsFloat(cooldown[difficulty]);
            m_stat_radius = attributes.getAttributeAsFloat("DamageRadius");
            m_stat_maxIcedUnits = attributes.getAttributeAsInt("MaxIcedUnits");
            m_stat_groundIceRadius = attributes.getAttributeAsFloat("GroundIceRadius");
            if (m_sprite == null)
            {
                m_sprite = new ASprite();
                m_sprite.Load("Ingame/iceball");
                m_inst.SetASprite(m_sprite);
            }
            switch (m_level)
            {
                case WL_1: m_sprite.LoadTexture("Sprites/Ingame/iceball_small.png"); break;
                case WL_2: m_sprite.LoadTexture("Sprites/Ingame/iceball_medium.png"); break;
                case WL_3: m_sprite.LoadTexture("Sprites/Ingame/iceball_big.png"); break;
            }
        }

        public bool isGroundSlippery(v3f pos)
        {
            return m_visibleEffect && (m_groundIce.getAbsolutePosition() - pos).getLength() < 0.5f * m_groundIce.getScale().X;
        }

        public override void update(float time)
        {
            base.update(time);
            m_drawItems.Clear();
            m_stateTime += time;
            m_inst.Update(time);
            m_touch.SetEnabled(isDragable());
            CPlayfield playfield = CPlayfield.GetInstance();
            line3df path = playfield.getPath();
            if (m_groundIceTimer > 0.0f)
            {
                m_groundIceTimer -= time;
                if (m_groundIceTimer < 1.0f) m_groundIce.setScale(m_groundIce.getScale() * powf(0.001f, time));
                if (m_groundIceTimer < 0.0f) setShouldBeVisible(m_visibleObject, m_visibleShadow, false);
            }
            switch (m_state)
            {
                case IS_Shop:
                case IS_Intro:
                case IS_Ready:
                    if (m_state != IS_Ready && m_sprite != null)
                    {
                        m_sprite._material.ZBuffer = true;
                        m_spriteGlow._material.ZBuffer = true;
                    }
                    m_drawItems.Add(new PosScale(getPosition(), k_spriteScale));
                    if (m_touch.IsActive())
                    {
                        PlaySfx(SFX_DEFENSE_ICEBALL_AIM);
                        playfield.getChallenge().specialWeaponUsed();
                        startDrag(m_touch);
                        m_velocity = v3f.Zero;
                        m_smoothVelocity = v3f.Zero;
                    }
                    break;
                case IS_Dragged:
                {
                    m_sprite._material.ZBuffer = false;
                    m_spriteGlow._material.ZBuffer = false;
                    int tx, ty;
                    m_touch.GetTouchPos_Current(out tx, out ty);
                    p2i touch = new p2i(tx, ty) + m_dragOffset;
                    if (m_touch.IsActive())
                    {
                        line3df ray = m_SceneManager.getRayFromScreenCoordinates(touch);
                        v3f newPos = v3f.Zero;
                        playfield.getFlyingPlane().getIntersectionWithLine(ray.start, ray.getVector(), ref newPos);
                        if (playfield.getCastlePlane().classifyPointRelation(newPos) != EIntersectionRelation3D.ISREL3D_FRONT)
                            playfield.getCastlePlane().getIntersectionWithLine(newPos, path.getVector(), ref newPos);
                        if (playfield.getGroundPlane().classifyPointRelation(newPos) != EIntersectionRelation3D.ISREL3D_FRONT)
                            playfield.getGroundPlane().getIntersectionWithLine(newPos, new v3f(0, 1, 0), ref newPos);
                        v3f newVelocity = (newPos - getPosition()) / time;
                        if (newVelocity.getLength() > 25.0f) newVelocity.setLength(25.0f);
                        m_velocity = newVelocity;
                        float k = powf(0.001f, time);
                        m_smoothVelocity = m_smoothVelocity * k + m_velocity * (1 - k);
                        setPosition(getPosition() + m_velocity * time);
                        calculateTail(v3f.Zero, m_smoothVelocity);
                    }
                    else
                    {
                        setState(IS_Flying);
                        calculateTail(v3f.Zero, m_smoothVelocity);
                    }
                    break;
                }
                case IS_Flying:
                {
                    m_velocity += k_gravity * time;
                    v3f oldPos = getPosition();
                    v3f newPos = getPosition() + m_velocity * time;
                    setPosition(newPos);
                    calculateTail(k_gravity, m_velocity);
                    bool hit = false;
                    if (playfield.getCastlePlane().classifyPointRelation(newPos) == EIntersectionRelation3D.ISREL3D_BACK)
                    {
                        playfield.getCastlePlane().getIntersectionWithLine(oldPos, newPos - oldPos, ref newPos);
                        setPosition(newPos);
                        hit = true;
                    }
                    if (playfield.getGroundPlane().classifyPointRelation(newPos) == EIntersectionRelation3D.ISREL3D_BACK)
                    {
                        playfield.getGroundPlane().getIntersectionWithLine(oldPos, newPos - oldPos, ref newPos);
                        setPosition(newPos);
                        hit = true;
                    }
                    if (hit)
                    {
                        explode();
                        PlaySfx(SFX_DEFENSE_ICEBALL_IMPACT);
                    }
                    break;
                }
                case IS_Dying:
                    setShouldBeVisible(true, false, true);
                    m_velocity += k_gravity * time;
                    setPosition(getPosition() + m_velocity * time);
                    calculateTail(k_gravity, m_velocity);
                    if (m_drawItems.Count == 0)
                    {
                        setShouldBeVisible(false, false, m_visibleEffect);
                        setState(IS_Respawning);
                    }
                    break;
                case IS_Respawning:
                    if (m_stateTime > m_stat_cooldown || CGameConfig.NoWeaponCD)
                    {
                        m_sprite._material.ZBuffer = false;
                        m_spriteGlow._material.ZBuffer = false;
                        setShouldBeVisible(true, false, false);
                        setPosition(m_spawnPosition);
                        setState(IS_RespawningToReadyTransition);
                    }
                    break;
                case IS_RespawningToReadyTransition:
                    m_drawItems.Add(new PosScale(getPosition() - new v3f(0, 2.0f, 0) * (0.4f - m_stateTime), k_spriteScale * (m_stateTime / 0.4f)));
                    if (m_stateTime > 0.4f) setState(IS_Ready);
                    break;
            }
            if (m_state == IS_Dragged || m_state == IS_Flying)
            {
                v3f pos = getPosition();
                p2i screen = screenFrom3DPos(pos);
                line3df ray = m_SceneManager.getRayFromScreenCoordinates(screen);
                if (playfield.getFlyingPlane().classifyPointRelation(pos) == EIntersectionRelation3D.ISREL3D_BACK)
                {
                    playfield.getFlyingPlane().getIntersectionWithLine(ray.start, ray.getVector(), ref pos);
                    setPosition(pos);
                }
                v3f shadowPos = v3f.Zero;
                playfield.getGroundPlane().getIntersectionWithLine(getPosition(), new v3f(0, 1, 0), ref shadowPos);
                m_shadow.setPosition(shadowPos);
                m_light.setPosition(shadowPos);
                float distanceFromGround = playfield.getGroundPlane().getDistanceTo(getPosition());
                float scale = CLAMP(2.0f - 0.1f * distanceFromGround, 0.4f, 2.0f);
                m_shadow.setScale(new v3f(scale, scale, scale));
                m_light.setScale(new v3f(scale, scale, scale) * 2.0f);
            }
        }

        public void elfArrowHit()
        {
            if (m_state != IS_Ready) return;
            setShouldBeVisible(false, false, false);
            setState(IS_Respawning);
            PlaySfx(SFX_ATTACK_SABOTAGE_WEAPON);
        }

        public ASprite getSprite() { return m_sprite; }
        public ASprite getSpriteGlow() { return m_spriteGlow; }
        public ISceneNode getPostament() { return m_postament; }

        void calculateTail(v3f accel, v3f velocity)
        {
            m_drawItems.Clear();
            v3f vel = velocity;
            const float timestep = 0.02f;
            v3f pos = getPosition();
            for (int i = 0; i < 8; ++i)
            {
                m_drawItems.Add(new PosScale(pos, k_spriteScale - (i / 8.0f) * 0.7f + (i > 0 ? 0.3f : 0.0f)));
                pos -= vel * timestep;
                vel -= accel * timestep;
            }
            CPlayfield pf = CPlayfield.GetInstance();
            while (m_drawItems.Count > 0 &&
                (pf.getGroundPlane().classifyPointRelation(m_drawItems[0].pos) == EIntersectionRelation3D.ISREL3D_BACK ||
                 pf.getCastlePlane().classifyPointRelation(m_drawItems[0].pos) == EIntersectionRelation3D.ISREL3D_BACK))
                m_drawItems.RemoveAt(0);
        }

        void explode()
        {
            int icedUnits = 0;
            foreach (CUnit unit in new List<CUnit>(CPlayfield.GetInstance().getUnits()))
            {
                float dist = (unit.getAbsolutePosition() - getPosition()).getLength();
                if (dist < m_stat_radius && unit.canBeFrozenByIceball())
                {
                    unit.petrify(5.0f, false);
                    ++icedUnits;
                    if (icedUnits == m_stat_maxIcedUnits) break;
                }
            }
            ParticleSystem ps = ParticleManager.AddParticleSystem("ice_explode");
            ps.SetZBuffer(false);
            ps.SetPosition(getPosition() + new v3f(0, 1.0f, 0));
            ps.SetScale(0.075f * 0.75f);
            ps.Restart();
            if (CPlayfield.GetInstance().getGroundPlane().getDistanceTo(getPosition()) < 0.1f)
            {
                m_groundIce.setPosition(getPosition());
                m_groundIce.setScale(new v3f(m_stat_groundIceRadius, 4.0f, 4.0f));
                m_groundIceTimer = m_stat_cooldown;
                setShouldBeVisible(m_visibleObject, m_visibleShadow, true);
            }
            setState(IS_Dying);
        }

        public override void draw()
        {
            if (!m_Node.isVisible() || m_sprite == null) return;
            float baseScale = m_baseScale * m_scale;
            for (int i = m_drawItems.Count - 1; i >= 0; --i)
            {
                float scale = baseScale * m_drawItems[i].scale;
                if (i != 0 && m_drawItems.Count > 1)
                    m_inst.GetASprite().PaintFrame3D(i + INGAME_F_ICEBALL_FIRST_TAIL + (8 - m_drawItems.Count), m_drawItems[i].pos, 0, 0, 0,
                        scale * ASprite.SPRITE_SCALE_3D, scale * ASprite.SPRITE_SCALE_3D);
                else m_inst.Draw3D(m_drawItems[i].pos, scale);
            }
        }

        public override void setVisible(bool visible)
        {
            m_visible = visible;
            m_Node.setVisible(m_visible && m_visibleObject);
            bool isNight = CPlayfield.GetInstance().GetDayTime() >= 0.85f;
            m_shadow.setVisible(m_visible && m_visibleShadow && !isNight);
            m_light.setVisible(m_visible && m_visibleShadow && isNight);
            m_postament.setVisible(m_visible && m_level != WL_None);
            m_groundIce.setVisible(m_visible && m_visibleEffect);
        }

        public override bool isVisible() { return m_visible; }

        void setShouldBeVisible(bool obj, bool shadow, bool effect)
        {
            m_visibleObject = obj;
            m_visibleShadow = shadow;
            m_visibleEffect = effect;
            setVisible(m_visible);
        }

        public override float getCooldown()
        {
            if (m_state != IS_Respawning) return 0.0f;
            return MAX(0.0f, 1.0f - m_stateTime / m_stat_cooldown);
        }

        public void setState(int state) { m_state = state; m_stateTime = 0; }
        public int getState() { return m_state; }
        public bool isDragable() { return m_state == IS_Ready || m_state == IS_Dragged; }

        public void startDrag(CTouchElement touch)
        {
            setState(IS_Dragged);
            m_dragOffset = new p2i(0, 0);
            setShouldBeVisible(true, true, false);
        }

        public p2i getScreenPosition() { return screenFrom3DPos(getPosition()); }
        public CTouchElement getTouch() { return m_touch; }

        public override void setShopState()
        {
            setState(IS_Shop);
            m_touch.SetEnabled(false);
        }

        public void setIntroState()
        {
            m_oldState = m_state;
            setState(IS_Intro);
            m_touch.SetEnabled(false);
        }

        public void setOldState()
        {
            setState(m_oldState);
            m_touch.SetEnabled(true);
        }

        public override void setShopHighlight(float power)
        {
            SColor emissive = new SColor(0xAAAAAAAA).getInterpolated(new SColor(0x00000000), power);
            setEmissiveColor(getPostament(), emissive);
            getSprite()._material.Lighting = true;
            getSprite()._material.EmissiveColor = emissive;
            getSpriteGlow()._material.Lighting = true;
            getSpriteGlow()._material.EmissiveColor = emissive;
        }

        public void removeIce() { if (m_groundIceTimer > 1.0f) m_groundIceTimer = 1.0f; }

        public int PlaySfx(int soundId, bool loop = false, int fadeInTime = 0) { return WeaponSfx.Play(soundId, getScreenPosition(), loop, fadeInTime); }
    }
}
