// Game/Objects/Weapons/Fireball.
using System.Collections.Generic;
using static CastleFrenzy.G;
using static CastleFrenzy.GE;
using static CastleFrenzy.Ids;

namespace CastleFrenzy
{
    /// <summary>Empty scene node that registers as transparent and renders through a callback (CFireballNode & co).</summary>
    public class CCallbackSceneNode : ISceneNode
    {
        readonly System.Func<SMaterial> m_material;
        readonly System.Action m_render;

        public CCallbackSceneNode(System.Func<SMaterial> material, System.Action render) { m_material = material; m_render = render; }

        public override int getMaterialCount() { return 1; }
        public override SMaterial getMaterial(int i) { return m_material() ?? s_fallback; }

        public override void OnRegisterSceneNode()
        {
            if (!IsVisible) return;
            CSceneManager.Instance.registerNodeForRendering(this, 0, s_transparent);
            base.OnRegisterSceneNode();
        }

        public override void render(int buffer) { m_render(); }

        static readonly SMaterial s_fallback = new SMaterial();
        static readonly SMaterial s_transparent = new SMaterial { MaterialType = E_MATERIAL_TYPE.EMT_TRANSPARENT_ALPHA_CHANNEL };
    }

    public class CFireball : CWeaponObject
    {
        public const int FS_Respawning = 0, FS_RespawningToReadyTransition = 1, FS_Ready = 2, FS_Dragged = 3, FS_Dying = 4, FS_Shop = 5, FS_Flying = 6, FS_Disabled = 7;

        static readonly v3f k_gravity = new v3f(0.0f, -9.8f * 3, 0.0f);
        const float k_maxDamagePerUnit = 9999.0f;
        const float k_spriteScale = 1.5f;
        const int k_touch_x = 175, k_touch_y = 0, k_touch_w = 100, k_touch_h = 90;

        protected float m_stat_cooldown, m_stat_radius;
        protected int m_stat_maxDamage, m_stat_maxBurn, m_stat_bounce;

        int m_state;
        float m_stateTime;
        int m_bounce, m_bounceUnitsHit;
        v3f m_hit_normal, m_spawnPosition, m_velocity, m_smoothVelocity;
        readonly ISceneNode m_postament, m_shadow, m_light;
        readonly ColladaControl m_burnedGround = new ColladaControl();
        float m_burnedGroundTimer;

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

        public CFireball() : base(null)
        {
            m_Node = new CCallbackSceneNode(() => m_sprite != null ? m_sprite._material : null, draw);
            __IRR_SCENE().getRootSceneNode().addChild(m_Node);
            m_postament = LoadColladaScene("Scenes/Fireball/bowl_gold.bdae");
            cleanNode(m_postament);
            __IRR_SCENE().getRootSceneNode().addChild(m_postament);
            m_shadow = LoadColladaScene("Scenes/Goblin/shadow.bdae");
            cleanNode(m_shadow);
            __IRR_SCENE().getRootSceneNode().addChild(m_shadow);
            m_light = LoadColladaScene("Scenes/Fireball/fireball_light.bdae");
            cleanNode(m_light);
            __IRR_SCENE().getRootSceneNode().addChild(m_light);
            m_spriteGlow = new ASprite();
            m_spriteGlow.Load("Ingame/fireball_glow");
            m_spriteGlow._material.MaterialType = E_MATERIAL_TYPE.EMT_TRANSPARENT_ADD_COLOR;
            m_burnedGround.setRoot(LoadColladaScene("Scenes/Fireball/burned_ground.bdae"));
            cleanNode(m_burnedGround.getRoot());
            __IRR_SCENE().getRootSceneNode().addChild(m_burnedGround.getRoot());
            m_inst.StartAnim(m_sprite, 0, 0, 0);
            m_touch = new CTouchElement(CTouchElement.Type_Drag, 1000);
            m_touch.SetScreenCoordinates(k_touch_x, k_touch_y, k_touch_w, k_touch_h);
            setLevel(WL_None);
            reset();
        }

        public override int getType() { return WT_Fireball; }

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
                m_sprite._material.ZBuffer = true;
                m_spriteGlow._material.ZBuffer = true;
                setShouldBeVisible(true, false, false);
                setPosition(m_spawnPosition);
                setState(FS_Ready);
            }
            else
            {
                setState(FS_Disabled);
                setShouldBeVisible(false, false, false);
            }
            setVisible(true);
            m_bounce = 0;
            m_bounceUnitsHit = 0;
        }

        public override void Dispose()
        {
            m_Node.remove();
            m_postament.remove();
            m_shadow.remove();
            m_light.remove();
            m_burnedGround.getRoot().remove();
            m_touch.Dispose();
        }

        protected override void adjustStats(IAttributes attributes)
        {
            string[] cooldown = { "CooldownEasy", "CooldownMedium", "CooldownHard" };
            int difficulty = CPlayfield.GetInstance().getDifficulty();
            m_stat_cooldown = attributes.getAttributeAsFloat(cooldown[difficulty]);
            m_stat_radius = attributes.getAttributeAsFloat("DamageRadius");
            m_stat_maxDamage = attributes.getAttributeAsInt("MaxDamage");
            m_stat_maxBurn = attributes.getAttributeAsInt("MaxBurn");
            m_stat_bounce = attributes.getAttributeAsInt("Bounce");
            if (m_sprite == null)
            {
                m_sprite = new ASprite();
                m_sprite.Load("Ingame/fireball");
                m_inst.SetASprite(m_sprite);
            }
            switch (m_level)
            {
                case WL_1: m_sprite.LoadTexture("Sprites/Ingame/fireball_small.png"); break;
                case WL_2: m_sprite.LoadTexture("Sprites/Ingame/fireball_medium.png"); break;
                case WL_3: m_sprite.LoadTexture("Sprites/Ingame/fireball_big.png"); break;
            }
        }

        public override void update(float time)
        {
            base.update(time);
            m_drawItems.Clear();
            m_stateTime += time;
            m_inst.Update(time);
            if (m_burnedGroundTimer > 0.0f)
            {
                m_burnedGroundTimer -= time;
                if (m_burnedGroundTimer < 0.0f) m_burnedGround.setAnim(1, false);
            }
            m_touch.SetEnabled(isDragable());
            CPlayfield playfield = CPlayfield.GetInstance();
            line3df path = playfield.getPath();
            switch (m_state)
            {
                case FS_Shop:
                case FS_Ready:
                    m_sprite._material.ZBuffer = true;
                    m_spriteGlow._material.ZBuffer = true;
                    m_drawItems.Add(new PosScale(getPosition(), k_spriteScale));
                    if (m_touch.IsActive())
                    {
                        PlaySfx(SFX_DEFENSE_FIREBALL_AIM);
                        startDrag(m_touch);
                        playfield.getChallenge().specialWeaponUsed();
                        m_velocity = v3f.Zero;
                        m_smoothVelocity = v3f.Zero;
                    }
                    break;
                case FS_Dragged:
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
                        if (playfield.getGroundPlane().getDistanceTo(newPos) < 1.5f)
                        {
                            playfield.getGroundPlane().getIntersectionWithLine(newPos, new v3f(0, 1, 0), ref newPos);
                            newPos += new v3f(0, 1.5f, 0);
                        }
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
                        setState(FS_Flying);
                        PlaySfx(SFX_DEFENSE_FIREBALL_FLY);
                        calculateTail(v3f.Zero, m_smoothVelocity);
                    }
                    break;
                }
                case FS_Flying:
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
                        m_hit_normal = playfield.getCastlePlane().Normal.normalized();
                        hit = true;
                    }
                    if (playfield.getGroundPlane().classifyPointRelation(newPos) == EIntersectionRelation3D.ISREL3D_BACK)
                    {
                        playfield.getGroundPlane().getIntersectionWithLine(oldPos, newPos - oldPos, ref newPos);
                        setPosition(newPos);
                        m_hit_normal = playfield.getGroundPlane().Normal.normalized();
                        hit = true;
                    }
                    if (hit)
                    {
                        explode();
                        PlaySfx(SFX_DEFENSE_FIREBALL_IMPACT);
                    }
                    break;
                }
                case FS_Dying:
                    setShouldBeVisible(true, false, true);
                    m_velocity += k_gravity * time;
                    setPosition(getPosition() + m_velocity * time);
                    calculateTail(k_gravity, m_velocity);
                    if (m_drawItems.Count == 0)
                    {
                        setShouldBeVisible(false, false, m_visibleEffect);
                        setState(FS_Respawning);
                    }
                    break;
                case FS_Respawning:
                    if (m_stateTime > m_stat_cooldown || CGameConfig.NoWeaponCD)
                    {
                        m_sprite._material.ZBuffer = true;
                        m_spriteGlow._material.ZBuffer = true;
                        setShouldBeVisible(true, false, false);
                        setPosition(m_spawnPosition);
                        setState(FS_RespawningToReadyTransition);
                    }
                    break;
                case FS_RespawningToReadyTransition:
                    m_drawItems.Add(new PosScale(getPosition() - new v3f(0, 2.0f, 0) * (0.4f - m_stateTime), k_spriteScale * (m_stateTime / 0.4f)));
                    if (m_stateTime > 0.4f) setState(FS_Ready);
                    break;
            }
            if (m_state == FS_Dragged || m_state == FS_Flying)
            {
                v3f pos = getPosition();
                p2i screen = screenFrom3DPos(pos);
                line3df ray = m_SceneManager.getRayFromScreenCoordinates(screen);
                if (playfield.getFlyingPlane().classifyPointRelation(pos) == EIntersectionRelation3D.ISREL3D_FRONT)
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
            m_burnedGround.updateAtEndOfFrame(time);
        }

        public void elfArrowHit()
        {
            if (m_state != FS_Ready) return;
            setShouldBeVisible(false, false, m_visibleEffect);
            setState(FS_Respawning);
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

        public static void FireballExplosion(v3f position, float radius, float maxDamage, float elfSurvivalChance, int[] unitsHitByKind, ref int curBounceUnitsHit, float scale = 1.0f)
        {
            CPlayfield pf = CPlayfield.GetInstance();
            v3f explosionPos = position + new v3f(0, -5, 0);
            v3f[] waterDummies =
            {
                pf.getDummy("d_water_01").getAbsolutePosition(),
                pf.getDummy("d_water_02").getAbsolutePosition(),
                pf.getDummy("d_water_03").getAbsolutePosition(),
            };
            // std::random_shuffle(indices, indices + 2) only shuffles the first two entries.
            int[] indices = { 0, 1, 2 };
            if (RandInt(0, 1) == 1) { indices[0] = 1; indices[1] = 0; }
            int waterIndex = 0;
            float damageDone = 0.0f;
            foreach (CUnit unit in new List<CUnit>(pf.getUnits()))
            {
                float dist = (unit.getAbsolutePosition() - position).getLength();
                if (dist < radius && unit.isPetrified()) unit.unpetrify(true);
                else if (dist < radius && unit.canBeDamagedByFireball())
                {
                    v3f vel = unit.getAbsolutePosition() - explosionPos;
                    const float throwPower = 34;
                    vel.setLength(throwPower * (radius - dist) / radius);
                    bool harmless = damageDone >= maxDamage;
                    if (!harmless && unit.getUnitKind() == UK_Elf && RandFloat(0, 1) < elfSurvivalChance) unit.elfJump(1.0f);
                    else if (harmless) unit.throwUnit(unit.projectVectorToFlyingPlane(vel), true);
                    else
                    {
                        v3f hitPos = unit.getFallPosition(vel);
                        if (!unit.isPositionOnPath(hitPos)) vel = unit.projectVectorToFlyingPlane(vel);
                        damageDone += unit.damageUnit(k_maxDamagePerUnit, UDS_Fireball);
                        unit.throwUnit(vel, false);
                        if (unit.getState() != US_FallingOnHydra)
                        {
                            int rnd = RandInt(0, 100);
                            if (waterIndex < 3 && rnd < 50)
                            {
                                unit.damageUnit(k_maxDamagePerUnit, UDS_Fireball);
                                unit.throwUnitInWater(waterDummies[indices[waterIndex++]]);
                            }
                            else if (rnd < 70)
                            {
                                damageDone += unit.damageUnit(k_maxDamagePerUnit, UDS_Fireball);
                                unit.throwUnitOnForTime(unit.getAbsolutePosition() - new v3f(0, 0, 10), 0.6f);
                            }
                            else if (rnd < 101 && unit.getVelocity().X > 0)
                            {
                                unit.healUnit(1.0f);
                                unit.throwUnit(new v3f(35, RandFloat(20.0f, 30.0f), 0), false);
                            }
                            ++unitsHitByKind[unit.getUnitKind()];
                        }
                        curBounceUnitsHit++;
                        v3f uvel = unit.getVelocity();
                        float angle = atan2f(-uvel.X, uvel.Z) + 0.5f * PI;
                        unit.getSceneNode().setRotation(new quaternion(0, angle, 0) * unit.getSceneNode().getRotation());
                    }
                }
            }
            if (pf.getIceball().isGroundSlippery(position)) pf.getIceball().removeIce();
            ParticleSystem ps = ParticleManager.AddParticleSystem("explode_new");
            ps.SetZBuffer(false);
            ps.SetPosition(position + new v3f(0, 1.0f, 0));
            ps.SetScale(0.075f * 0.75f * scale);
            ps.Restart();
        }

        void explode()
        {
            int[] unitsHitByKind = new int[UK_Unknown + 1];
            int curBounceUnitsHit = 0;
            CPlayfield pf = CPlayfield.GetInstance();
            int difficulty = pf.getDifficulty();
            FireballExplosion(getPosition(), m_stat_radius, m_stat_maxDamage, 0.8f - 0.1f * m_level - 0.2f * (2 - difficulty), unitsHitByKind, ref curBounceUnitsHit);
            pf.getChallenge().unitsWereKilledByWeapon(UDS_Fireball, unitsHitByKind);
            float height = pf.getGroundPlane().getDistanceTo(getPosition());
            if (height < 2.0f)
            {
                m_burnedGroundTimer = 1.5f;
                m_burnedGround.getRoot().setPosition(getPosition() + new v3f(0, -height, 0));
                m_burnedGround.setAnim(0, false);
            }
            bool die = true;
            if (m_bounce < m_stat_bounce)
            {
                if (curBounceUnitsHit > 0) m_bounceUnitsHit++;
                m_bounce++;
                float speed = m_velocity.getLength() * 0.7f;
                const float speedMin = 10.0f;
                if (height > 2.0f || speed >= speedMin)
                {
                    m_velocity.setLength(speed);
                    m_velocity = -m_velocity;
                    quaternion q = new quaternion();
                    q.rotationFromTo(m_velocity, m_hit_normal);
                    q.slerp(quaternion.Identity, q, -1.4f);
                    m_velocity = q * m_velocity;
                    die = false;
                }
            }
            else if (m_bounceUnitsHit == WL_3 && curBounceUnitsHit > 0)
            {
                pf.addMoney(0, 50, getPosition(), _TXT(STRING_IDS.INGAME_GREAT));
                CAchievements.GetInstance().setAchieved("Fireball Master");
            }
            if (die) setState(FS_Dying);
        }

        public override void draw()
        {
            if (!m_Node.isVisible() || m_sprite == null) return;
            float bounceScale = 1.0f - 0.15f * m_bounce;
            float baseScale = m_baseScale * m_scale * bounceScale;
            v3f campos = __IRR_SCENE().getActiveCamera().getAbsolutePosition();
            for (int i = m_drawItems.Count - 1; i >= 0; --i)
            {
                float scale = baseScale * m_drawItems[i].scale;
                v3f pos = m_drawItems[i].pos + (campos - m_drawItems[i].pos).setLength(1.5f);
                if (i == 0) m_spriteGlow.PaintFrame3D(0, pos, 0, 0, 0, scale * ASprite.SPRITE_SCALE_3D, scale * ASprite.SPRITE_SCALE_3D);
                if (i != 0 && m_drawItems.Count > 1)
                    m_inst.GetASprite().PaintFrame3D(i + INGAME_F_FIREBALL_FIRST_TAIL + (8 - m_drawItems.Count), pos, 0, 0, 0,
                        scale * ASprite.SPRITE_SCALE_3D, scale * ASprite.SPRITE_SCALE_3D);
                else m_inst.Draw3D(pos, scale);
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
            if (m_state != FS_Respawning) return 0.0f;
            return MAX(0.0f, 1.0f - m_stateTime / m_stat_cooldown);
        }

        public void setState(int state)
        {
            m_state = state;
            m_stateTime = 0;
            if (m_state == FS_Respawning) { m_bounce = 0; m_bounceUnitsHit = 0; }
        }

        public int getState() { return m_state; }
        public bool isDragable() { return m_state == FS_Ready || m_state == FS_Dragged; }

        public void startDrag(CTouchElement touch)
        {
            setState(FS_Dragged);
            m_dragOffset = new p2i(0, 0);
            setShouldBeVisible(true, true, false);
        }

        public p2i getScreenPosition() { return screenFrom3DPos(getPosition()); }
        public CTouchElement getTouch() { return m_touch; }

        public override void setShopState()
        {
            setState(FS_Shop);
            m_touch.SetEnabled(false);
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

        public int PlaySfx(int soundId, bool loop = false, int fadeInTime = 0)
        {
            return WeaponSfx.Play(soundId, getScreenPosition(), loop, fadeInTime);
        }
    }

    /// <summary>The weapons' PlaySfx: non-obligatory sounds are muted when the weapon is off screen.</summary>
    public static class WeaponSfx
    {
        public static int Play(int soundId, p2i screenPos, bool loop, int fadeInTime)
        {
            string soundName = __MGR_SOUNDS().GetSoundNameFromIndex(soundId);
            IAttributes configAttr = soundName != null ? CGameConfig.GetInstance().getAttributes("soundsconfig", soundName) : null;
            if (configAttr != null && configAttr.getAttributeAsInt("Obligatory") == 0)
            {
                r2i viewport = new r2i(0, 0, GAME_SCREEN_W, GAME_SCREEN_H);
                if (!viewport.isPointInside(screenPos)) return CPlayfield.GetInstance().PlaySfx(soundId, loop, fadeInTime, 100);
            }
            return CPlayfield.GetInstance().PlaySfx(soundId, loop, fadeInTime);
        }
    }
}
