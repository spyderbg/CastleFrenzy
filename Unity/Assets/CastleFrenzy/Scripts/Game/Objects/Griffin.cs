// Game/Objects/Weapons/Griffin (CWeaponObject + ColladaControl; the control is a member here).
using System.Collections.Generic;
using static CastleFrenzy.G;
using static CastleFrenzy.GE;
using static CastleFrenzy.Ids;

namespace CastleFrenzy
{
    public class CGriffin : CWeaponObject
    {
        public const int GGS_Inactive = 0, GGS_Active = 1, GGS_Smashing = 2, GGS_GoingUp = 3, GGS_Up = 4, GGS_FlyingToSpawnPosition = 5, GGS_Shop = 6, GGS_Disabled = 7;

        protected float m_stat_cooldown, m_stat_radius, m_stat_petrify;
        protected int m_stat_hits;

        readonly ColladaControl m_ctrl = new ColladaControl();
        v3f m_spawnPosition;
        int m_state;
        float m_cooldown, m_smashTimer;
        readonly CTouchElement m_touch;
        readonly ColladaControl m_brokenGround = new ColladaControl(), m_petrifyWave = new ColladaControl();
        readonly ISceneNode m_shadow, m_scaleDummy, m_scaleBip01;
        v3f m_petrifyPosition;
        float m_petrifyCountdown;
        ParticleSystem m_petrifySparkles;
        float m_petrifySparklesTime;
        v3f m_deactivatingStartPosition;
        float m_deactivatingTimer;
        bool m_visible, m_visibleObject, m_visibleShadow, m_visibleEffect, m_visiblePetrify;

        public CGriffin() : base(null)
        {
            ISceneNode brokenGround = LoadColladaScene("Scenes/Griffin/broken_road.bdae");
            __IRR_SCENE().getRootSceneNode().addChild(brokenGround);
            cleanNode(brokenGround, false);
            m_brokenGround.setRoot(brokenGround);
            ISceneNode petrify = LoadColladaScene("Scenes/Griffin/griffin_wave.bdae");
            __IRR_SCENE().getRootSceneNode().addChild(petrify);
            cleanNode(petrify, false);
            m_petrifyWave.setRoot(petrify);
            m_Node = LoadColladaScene("Scenes/Griffin/griffin.bdae");
            __IRR_SCENE().getRootSceneNode().addChild(m_Node);
            cleanNode(m_Node);
            SColor e = CGameConfig.EmissiveColorForInteractiveObjects;
            setEmissiveColor(m_Node, new SColor(e.getAlpha() / 2, e.getRed() / 2, e.getGreen() / 2, e.getBlue() / 2));
            m_shadow = LoadColladaScene("Scenes/Goblin/shadow.bdae");
            cleanNode(m_shadow);
            __IRR_SCENE().getRootSceneNode().addChild(m_shadow);
            m_ctrl.setRoot(m_Node);
            m_scaleDummy = m_Node.getSceneNodeFromName("d_transform_bip01");
            m_scaleBip01 = m_Node.getSceneNodeFromUID("Bip01-node");
            m_touch = new CTouchElement(CTouchElement.Type_AnyTouch, 100);
            m_touch.SetScreenCoordinates(280, 32, 95, 140);
            setLevel(WL_None);
            reset();
            m_petrifyCountdown = 0.0f;
        }

        public override int getType() { return WT_Griffin; }
        public ColladaControl getControl() { return m_ctrl; }

        public void setSpawnPosition(v3f pos)
        {
            m_spawnPosition = pos;
            setPosition(pos);
        }

        public override void reset()
        {
            base.reset();
            if (m_level != WL_None)
            {
                setShouldBeVisible(true, false, false, false);
                setPosition(m_spawnPosition);
                m_ctrl.setAnim("active");
                setState(GGS_Active);
            }
            else
            {
                setState(GGS_Disabled);
                setShouldBeVisible(false, false, false, false);
            }
            setVisible(true);
            if (m_petrifySparkles != null) { ParticleManager.RemoveParticleSystem(m_petrifySparkles); m_petrifySparkles = null; }
            m_cooldown = 0.0f;
        }

        public override void Dispose()
        {
            m_Node.remove();
            m_shadow.remove();
            m_brokenGround.getRoot().remove();
            m_petrifyWave.getRoot().remove();
            if (m_petrifySparkles != null) { ParticleManager.RemoveParticleSystem(m_petrifySparkles); m_petrifySparkles = null; }
            m_touch.Dispose();
        }

        protected override void adjustStats(IAttributes attributes)
        {
            string[] cooldown = { "CooldownEasy", "CooldownMedium", "CooldownHard" };
            int difficulty = CPlayfield.GetInstance().getDifficulty();
            m_stat_cooldown = attributes.getAttributeAsFloat(cooldown[difficulty]);
            m_stat_radius = attributes.getAttributeAsFloat("DamageRadius");
            m_stat_hits = attributes.getAttributeAsInt("Hits");
            m_stat_petrify = attributes.getAttributeAsFloat("PetrifyTime");
            int lvl = m_level == WL_2 ? 1 : (m_level == WL_3 ? 2 : (m_level == WL_1 ? 0 : -1));
            if (lvl < 0) return;
            ITexture tex = TextureCache.getTexture("Scenes/Griffin/griffin_0" + lvl + ".png");
            ISceneNode griffin = m_Node.getSceneNodeFromName("griffin");
            if (griffin != null && griffin.Children.Count > 0 && griffin.Children[0].getMaterialCount() > 0)
                griffin.Children[0].getMaterial(0).Textures[0] = tex;
            for (int i = 0; i < 3; i++)
            {
                ISceneNode b = m_Node.getSceneNodeFromName("base0" + i);
                if (b != null) b.setVisible(i == lvl);
            }
        }

        static v3f Interpolate(float t, v3f startPos, v3f startVel, v3f endPos, v3f endVel)
        {
            v3f d = startPos;
            v3f c = startVel;
            v3f b = (endPos - startPos) * 3.0f - startVel * 2.0f - endVel;
            v3f a = (startPos - endPos) * 2.0f + startVel + endVel;
            return d + (c + (b + a * t) * t) * t;
        }

        public override void update(float time)
        {
            base.update(time);
            if (m_scaleDummy != null && m_scaleBip01 != null) m_scaleBip01.setScale(m_scaleDummy.getScale());
            CPlayfield pf = CPlayfield.GetInstance();
            switch (m_state)
            {
                case GGS_Inactive:
                    if (m_cooldown >= 0.0f)
                    {
                        m_cooldown -= time;
                        if (m_cooldown <= 0.0f || CGameConfig.NoWeaponCD)
                        {
                            m_cooldown = 0.0f;
                            activate();
                        }
                    }
                    break;
                case GGS_Active:
                    if (m_ctrl.checkEvent("scream")) pf.PlaySfx(SFX_DEFENSE_GRIFFIN_MOVING);
                    if (m_touch.IsActive())
                    {
                        setState(GGS_GoingUp);
                        setShouldBeVisible(true, true, m_visibleEffect, m_visiblePetrify);
                        m_ctrl.setAnimAnim("fly_off", "fly");
                        pf.StopSfx(SFX_DEFENSE_GRIFFIN_MOVING);
                    }
                    break;
                case GGS_Up:
                case GGS_GoingUp:
                    if (m_touch.IsActive()) updateFlying(time, pf);
                    else
                    {
                        pf.StopSfx(SFX_DEFENSE_GRIFFIN_FLAP);
                        if (m_brokenGround.getCurrentAnimIndex() == 0) m_brokenGround.setAnim(1, false);
                        flyToSpawnPosition();
                    }
                    break;
                case GGS_Smashing:
                    m_smashTimer += time;
                    if (m_smashTimer > 0.7f)
                    {
                        if (m_cooldown < m_stat_cooldown && m_touch.IsActive()) setState(GGS_GoingUp);
                        else
                        {
                            if (m_brokenGround.getCurrentAnimIndex() == 0) m_brokenGround.setAnim(1, false);
                            flyToSpawnPosition();
                        }
                    }
                    break;
                case GGS_FlyingToSpawnPosition:
                {
                    m_deactivatingTimer += time;
                    float t = m_deactivatingTimer / 0.6f;
                    if (t > 1.0f)
                    {
                        setPosition(m_spawnPosition);
                        if (m_cooldown > 0.0f) deactivate();
                        else
                        {
                            setState(GGS_Active);
                            m_ctrl.setAnim("active");
                            setShouldBeVisible(true, false, m_visibleEffect, m_visiblePetrify);
                        }
                    }
                    else setPosition(Interpolate(t, m_deactivatingStartPosition, v3f.Zero, m_spawnPosition, new v3f(0, -20, 0)));
                    break;
                }
            }
            if (m_state == GGS_GoingUp || m_state == GGS_Up || m_state == GGS_Smashing || m_state == GGS_FlyingToSpawnPosition)
            {
                v3f shadowPos = v3f.Zero;
                pf.getGroundPlane().getIntersectionWithLine(getPosition(), new v3f(0, 1, 0), ref shadowPos);
                m_shadow.setPosition(shadowPos);
                float distanceFromGround = pf.getGroundPlane().getDistanceTo(getPosition());
                float scale = CLAMP(5.0f - 0.1f * distanceFromGround, 0.4f, 5.0f);
                m_shadow.setScale(new v3f(scale, scale, scale));
            }
            updatePetrify(time);
            m_brokenGround.updateAtEndOfFrame(time);
            m_petrifyWave.updateAtEndOfFrame(time);
            m_ctrl.updateAtEndOfFrame(time);
        }

        void updateFlying(float time, CPlayfield pf)
        {
            if (!__MGR_SOUNDS().isSoundPlaying(SFX_DEFENSE_GRIFFIN_FLAP)) pf.PlaySfx(SFX_DEFENSE_GRIFFIN_FLAP, true);
            plane3df pl = pf.getFlyingPlane();
            int tx, ty;
            m_touch.GetTouchPos_Current(out tx, out ty);
            v3f newPos = v3f.Zero;
            line3df ray = __IRR_SCENE().getRayFromScreenCoordinates(new p2i(tx, ty));
            pl.getIntersectionWithLine(ray.start, ray.getVector(), ref newPos);
            plane3df plCastle = pf.getCastlePlane();
            plCastle.D -= 2.0f;
            if (plCastle.classifyPointRelation(newPos) != EIntersectionRelation3D.ISREL3D_FRONT)
                plCastle.getIntersectionWithLine(newPos, pf.getPath().getVector(), ref newPos);
            v3f upPos = v3f.Zero;
            plane3df plGround = pf.getGroundPlane();
            plGround.D -= 4.0f;
            plGround.getIntersectionWithLine(newPos, new v3f(0, 1, 0), ref upPos);
            int dtx, dty;
            m_touch.GetTouchPos_DT(out dtx, out dty);
            if (m_state == GGS_GoingUp)
            {
                v3f dir = upPos - getPosition();
                if (dir.getLength() < 1.0f)
                {
                    setState(GGS_Up);
                    setPosition(upPos);
                    if (m_brokenGround.getCurrentAnimIndex() == 0) m_brokenGround.setAnim(1, false);
                }
                else
                {
                    dir /= time;
                    if (dir.getLength() > 40.0f) dir.setLength(40.0f);
                    setPosition(getPosition() + dir * time);
                }
                return;
            }
            bool boom = (float)dty / time > 600.0f;
            if (newPos.Y < upPos.Y) upPos.Y += 0.2f * (newPos.Y - upPos.Y);
            setPosition(upPos);
            if (!boom) return;
            setState(GGS_Smashing);
            pf.getChallenge().specialWeaponUsed();
            pf.StopSfx(SFX_DEFENSE_GRIFFIN_FLAP);
            pf.PlaySfx(SFX_DEFENSE_GRIFFIN_SMASH);
            pf.getGroundPlane().getIntersectionWithLine(newPos, new v3f(0, 1, 0), ref newPos);
            setPosition(newPos);
            m_cooldown += m_stat_cooldown / m_stat_hits;
            if (m_stat_petrify > 0)
            {
                setShouldBeVisible(m_visibleObject, m_visibleShadow, m_visibleEffect, true);
                m_petrifyWave.setAnim(0, false);
                m_petrifyWave.getRoot().setPosition(getPosition());
            }
            ParticleSystem ps = ParticleManager.AddParticleSystem("grifon_fall");
            ps.SetScale(0.17f);
            ps.SetPosition(getPosition() - new v3f(0, 0.5f, 0));
            ps.SetRotation(new quaternion(0.5f * PI, 0.0f, 0.0f));
            int[] unitsHitByKind = new int[UK_Unknown + 1];
            foreach (CUnit unit in new List<CUnit>(pf.getUnits()))
            {
                v3f vec = unit.getAbsolutePosition() - getPosition();
                vec.Y = 0;
                if (vec.getLength() <= m_stat_radius)
                {
                    if (unit.isPetrified()) unit.unpetrify(true);
                    else if (unit.getUnitKind() == UK_Elf && RandFloat(0, 1) < 0.9f - 0.1f * m_level) unit.elfJump(1.0f);
                    else unit.damageUnit(9999, UDS_GriffinSmash);
                    ++unitsHitByKind[unit.getUnitKind()];
                }
            }
            m_smashTimer = 0.0f;
            pf.getChallenge().unitsWereKilledByWeapon(UDS_GriffinSmash, unitsHitByKind);
            pf.SetCameraShake(0.7f, 1.0f);
            m_brokenGround.setAnim(0, false);
            m_brokenGround.getRoot().setPosition(getPosition() + new v3f(0, 1.7f, 0));
            setShouldBeVisible(true, true, true, m_stat_petrify > 0.0f);
            if (m_stat_petrify > 0.0f) petrify();
        }

        public override void draw() { }

        public override void setVisible(bool visible)
        {
            m_visible = visible;
            m_Node.setVisible(m_visible && m_visibleObject);
            m_shadow.setVisible(m_visible && m_visibleShadow);
            m_brokenGround.getRoot().setVisible(visible && m_visibleEffect);
            m_petrifyWave.getRoot().setVisible(visible && m_visiblePetrify);
        }

        public override bool isVisible() { return m_visible; }

        void setShouldBeVisible(bool obj, bool shadow, bool effect, bool petrify)
        {
            m_visibleObject = obj;
            m_visibleShadow = shadow;
            m_visibleEffect = effect;
            m_visiblePetrify = petrify;
            setVisible(m_visible);
        }

        void activate()
        {
            m_ctrl.setAnimAnim(m_ctrl.getCurrentAnimName() == "freeze" ? "freeze_to_idle" : "activate", "active");
            setState(GGS_Active);
        }

        void deactivate()
        {
            m_ctrl.setAnimAnim("unactivate", "unactive");
            setState(GGS_Inactive);
            setPosition(m_spawnPosition);
            setShouldBeVisible(true, false, m_visibleEffect, m_visiblePetrify);
        }

        public override float getCooldown() { return m_state != GGS_Inactive ? 0.0f : m_cooldown / m_stat_cooldown; }
        public void setState(int state) { m_state = state; }
        public int getState() { return m_state; }
        public CTouchElement getTouch() { return m_touch; }

        public void elfArrowHit()
        {
            if (m_state != GGS_Active) return;
            m_cooldown += m_stat_cooldown;
            deactivate();
            m_ctrl.setAnim("freeze", false);
            CPlayfield.GetInstance().PlaySfx(SFX_ATTACK_SABOTAGE_WEAPON);
        }

        public override void setShopState()
        {
            setShouldBeVisible(true, false, false, false);
            m_touch.SetEnabled(false);
            m_ctrl.setAnim("idle");
            setState(GGS_Shop);
        }

        void flyToSpawnPosition()
        {
            if (m_cooldown > 0.0f) m_cooldown = m_stat_cooldown;
            m_deactivatingTimer = 0.0f;
            m_deactivatingStartPosition = getPosition();
            setState(GGS_FlyingToSpawnPosition);
        }

        void petrify()
        {
            m_petrifyCountdown = 0.25f;
            m_petrifyPosition = getPosition();
        }

        void updatePetrify(float time)
        {
            if (m_petrifyWave.checkEvent("anim_end")) setShouldBeVisible(m_visibleObject, m_visibleShadow, m_visibleEffect, false);
            if (m_petrifyCountdown > 0.0f)
            {
                m_petrifyCountdown -= time;
                if (m_petrifyCountdown <= 0.0f) petrifyBlast();
            }
            if (m_petrifySparklesTime > 0.0f)
            {
                m_petrifySparklesTime -= time;
                if (m_petrifySparklesTime <= 0.0f)
                {
                    ParticleManager.RemoveParticleSystem(m_petrifySparkles);
                    m_petrifySparkles = null;
                }
            }
        }

        void petrifyBlast()
        {
            CPlayfield pf = CPlayfield.GetInstance();
            if (m_petrifySparkles == null)
            {
                m_petrifySparkles = ParticleManager.AddParticleSystem("sparkles");
                line3df path = pf.getPath();
                m_petrifySparkles.SetPosition(path.start + path.getVector() * 0.6f);
                m_petrifySparkles.SetRotation(new quaternion(0.5f * PI, 0.0f, 0.0f));
                m_petrifySparkles.SetScale(0.17f);
            }
            m_petrifySparklesTime = m_stat_petrify;
            bool petrifiedUnit = false;
            foreach (CUnit unit in new List<CUnit>(pf.getUnits()))
            {
                v3f vec = unit.getAbsolutePosition() - getPosition();
                vec.Y = 0;
                if (unit.canBePetrified() && vec.getLength() < 7.0f)
                {
                    petrifiedUnit = true;
                    unit.petrify(m_stat_petrify, true);
                }
            }
            if (petrifiedUnit) pf.PlaySfx(SFX_DEFENCE_GRIFFIN_PETRIFICATION);
        }
    }
}
