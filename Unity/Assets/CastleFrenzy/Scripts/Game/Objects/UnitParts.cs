// Game/Objects/UnitParts/ShamanAura, UnitParts/Bomb and Objects/Projectile.
using System.Collections.Generic;
using static CastleFrenzy.G;
using static CastleFrenzy.GE;
using static CastleFrenzy.Ids;

namespace CastleFrenzy
{
    public class ShamanAura
    {
        readonly string m_effectName;
        readonly float m_effectScale;
        bool m_hiding;
        float m_currentScale = 1.0f;
        ParticleSystem m_particleSystem;
        readonly ISceneNode m_dummy;

        public ShamanAura(string name, float scale, ISceneNode dummy) { m_effectName = name; m_effectScale = scale; m_dummy = dummy; }

        public void Dispose()
        {
            if (m_particleSystem != null) { ParticleManager.RemoveParticleSystem(m_particleSystem); m_particleSystem = null; }
        }

        public void update(float secs)
        {
            if (!m_hiding) return;
            float newScale = m_currentScale - 1.5f * secs;
            if (newScale > 0.0f)
            {
                m_particleSystem.SetScale(newScale * m_effectScale);
                m_currentScale = newScale;
            }
            else
            {
                m_hiding = false;
                ParticleManager.RemoveParticleSystem(m_particleSystem);
                m_particleSystem = null;
            }
        }

        public void draw() { }

        public void hide()
        {
            if (!m_hiding && m_particleSystem != null) { m_hiding = true; m_currentScale = 1.0f; }
        }

        public void hideImmediately()
        {
            if (m_particleSystem != null) { ParticleManager.RemoveParticleSystem(m_particleSystem); m_particleSystem = null; m_hiding = false; }
        }

        public void show()
        {
            if (m_particleSystem != null && m_currentScale == 1.0f) return;
            if (m_particleSystem != null) ParticleManager.RemoveParticleSystem(m_particleSystem);
            m_particleSystem = ParticleManager.AddParticleSystem(m_effectName);
            m_particleSystem.SetScale(m_effectScale);
            m_particleSystem.SetRotation(new quaternion(0.5f * PI, 0.0f, 0.0f));
            m_currentScale = 1.0f;
            if (m_dummy != null) m_dummy.addChild(m_particleSystem.getNode());
            m_hiding = false;
        }
    }

    public class CBomb : ColladaControl
    {
        readonly ISceneNode m_sceneroot;
        bool m_free, m_dead;
        readonly float m_damage;
        float m_time;
        readonly CTouchElement m_touch = new CTouchElement();
        v3f m_velocity;
        readonly bool m_isForHelpMenu;

        public CBomb(float damage, ISceneNode rootobj = null)
        {
            m_sceneroot = rootobj;
            ISceneNode modelRoot = LoadColladaScene("Scenes/Goblin_bomb/bomb.bdae", true);
            cleanNode(modelRoot, false);
            (m_sceneroot ?? __IRR_SCENE().getRootSceneNode()).addChild(modelRoot);
            setRoot(modelRoot);
            modelRoot.setRotation(new quaternion(0, PI, 0));
            setAnim(0, true);
            m_touch.m_zLevel = 0;
            m_touch.SetEnabled(false);
            m_damage = damage;
            m_isForHelpMenu = m_sceneroot != null;
        }

        public void Dispose()
        {
            if (getRoot() != null) getRoot().remove();
            m_touch.Dispose();
        }

        public void update(float time)
        {
            p2i pos = screenFrom3DPos(getRoot().getAbsolutePosition());
            m_touch.SetScreenCoordinates(pos.X - 20, pos.Y - 20, 40, 40);
            if (checkEvent("anim_end") && getCurrentAnimName() == "explode") explode(false);
            if (m_time > 0.0f && getCurrentAnimName() == "pulse")
            {
                m_time -= time;
                if (m_time <= 0.0f) explodeLater();
            }
            if (m_touch.IsActive())
            {
                int tx, ty;
                m_touch.GetTouchPos_Current(out tx, out ty);
                plane3df targetPlane = CPlayfield.GetInstance().getFlyingPlane();
                line3df ray = __IRR_SCENE().getRayFromScreenCoordinates(new p2i(tx, ty));
                v3f targetPos = v3f.Zero;
                targetPlane.getIntersectionWithLine(ray.start, ray.getVector(), ref targetPos);
                v3f targetDir = (targetPos - getRoot().getPosition()) / time;
                const float maxVelocity = 25.0f;
                if (targetDir.getLength() > maxVelocity) targetDir.setLength(maxVelocity);
                m_velocity = targetDir;
            }
            else m_velocity += new v3f(0, -9.8f * 4.0f * 0.9f, 0) * time;

            if (getCurrentAnimIndex() != 0)
            {
                plane3df ground = CPlayfield.GetInstance().getGroundPlane();
                ISceneNode r = getRoot();
                if (ground.classifyPointRelation(r.getPosition()) == EIntersectionRelation3D.ISREL3D_FRONT)
                {
                    r.setPosition(r.getPosition() + m_velocity * time);
                    if (ground.classifyPointRelation(r.getPosition()) == EIntersectionRelation3D.ISREL3D_FRONT)
                    {
                        r.setPosition(r.getPosition() + m_velocity * time);
                        if (ground.classifyPointRelation(r.getPosition()) != EIntersectionRelation3D.ISREL3D_FRONT)
                            __MGR_SOUNDS().play(SFX_ATTACK_DROP_BOMB_IMPACT);
                        r.setPosition(r.getPosition() - m_velocity * time);
                    }
                    r.setPosition(r.getPosition() - m_velocity * time);
                }
                r.setPosition(r.getPosition() + m_velocity * time);
                if (ground.classifyPointRelation(r.getPosition()) != EIntersectionRelation3D.ISREL3D_FRONT)
                {
                    v3f p = v3f.Zero;
                    ground.getIntersectionWithLine(r.getPosition(), m_velocity, ref p);
                    r.setPosition(p);
                    m_velocity.Y = -m_velocity.Y;
                    m_velocity *= 0.5f;
                }
                plane3df castle = CPlayfield.GetInstance().getCastlePlane();
                if (castle.classifyPointRelation(r.getPosition()) != EIntersectionRelation3D.ISREL3D_FRONT)
                {
                    v3f p = v3f.Zero;
                    castle.getIntersectionWithLine(r.getPosition(), m_velocity, ref p);
                    r.setPosition(p);
                    m_velocity.X = -m_velocity.X;
                    m_velocity *= 0.5f;
                }
            }
            updateAtEndOfFrame(time);
        }

        public void draw2D() { }
        public void draw3D() { }
        public bool isDead() { return m_dead; }

        public void explode(bool forceCastleDamage)
        {
            if (m_dead) return;
            int[] unitsHitByKind = new int[UK_Unknown + 1];
            int cb = 0;
            int difficulty = CPlayfield.GetInstance().getDifficulty();
            CFireball.FireballExplosion(getRoot().getAbsolutePosition(), 3.0f, 300.0f, 0.9f, unitsHitByKind, ref cb, 0.5f - 0.2f * (2 - difficulty));
            m_dead = true;
            __MGR_SOUNDS().play(SFX_ATTACK_EXPLOSIVE);
            if (forceCastleDamage || fabsf(CPlayfield.GetInstance().getCastlePlane().getDistanceTo(getRoot().getAbsolutePosition())) < 2.0f)
                CPlayfield.GetInstance().getGate().damageGate(m_damage);
        }

        public void explodeLater()
        {
            if (!m_dead && getCurrentAnimName() != "explode") setAnim("explode");
        }

        public void dropOnGround(v3f scale)
        {
            m_touch.SetEnabled(true);
            m_velocity = v3f.Zero;
            m_free = true;
            v3f pos = getRoot().getAbsolutePosition();
            (m_sceneroot ?? __IRR_SCENE().getRootSceneNode()).addChild(getRoot());
            getRoot().setPosition(pos);
            getRoot().setScale(scale);
            setAnimAnim("drop", "pulse");
            m_time = 1.0f;
            __MGR_SOUNDS().play(SFX_ATTACK_DROP_BOMB);
        }

        public bool isFree() { return m_free; }
        public v3f getVelocity() { return m_velocity; }
        public void setVelocity(v3f v) { m_velocity = v; }
        public bool isForHelpMenu() { return m_isForHelpMenu; }
    }

    public class CProjectile : CGameObject
    {
        public static bool s_antiFireballArrowShot, s_antiIceballArrowShot, s_antiGriffinArrowShot;
        public v3f m_velocity, m_acceleration;
        public float m_scale;
        readonly string m_diePs;
        ParticleSystem m_ps;
        readonly ASpriteInstance m_inst = new ASpriteInstance();
        readonly float m_damage;
        bool m_dead;
        bool m_antiFireball, m_antiIceball, m_antiGriffin;
        v3f m_target;
        bool m_active;
        v3f m_startPos;
        readonly CTouchElement m_touch;
        bool m_isElfArrow;

        public CProjectile(ASprite sprite, int anim, string diePs, float scale, v3f pos, v3f vel, v3f accel, float damage) : base(null)
        {
            m_Node = __IRR_SCENE().addEmptySceneNode(__IRR_SCENE().getRootSceneNode());
            setPosition(pos);
            m_velocity = vel; m_acceleration = accel; m_damage = damage;
            m_inst.StartAnim(sprite, anim, 0, 0);
            m_scale = scale;
            m_diePs = diePs;
            m_touch = new CTouchElement(CTouchElement.Type_AnyTouch, 40);
            m_touch.SetEnabled(false);
        }

        public CProjectile(string ps, string diePs, float scale, v3f pos, v3f vel, v3f accel, float damage) : base(null)
        {
            m_Node = __IRR_SCENE().addEmptySceneNode(__IRR_SCENE().getRootSceneNode());
            setPosition(pos);
            m_velocity = vel; m_acceleration = accel; m_damage = damage;
            m_scale = scale;
            m_diePs = diePs;
            m_touch = new CTouchElement(CTouchElement.Type_AnyTouch, 40);
            m_touch.SetEnabled(false);
            m_ps = ParticleManager.AddParticleSystem(ps);
            m_ps.SetScale(m_scale);
        }

        public CProjectile(string mesh, float damage, string diePs, float diePsScale) : base(null)
        {
            m_Node = LoadColladaScene(mesh, true);
            cleanNode(m_Node);
            m_damage = damage;
            m_scale = diePsScale;
            m_diePs = diePs;
            m_touch = new CTouchElement(CTouchElement.Type_AnyTouch, 40);
            m_touch.SetEnabled(false);
        }

        public override void Dispose()
        {
            m_Node.remove();
            m_touch.Dispose();
            if (m_ps != null) { ParticleManager.RemoveParticleSystem(m_ps); m_ps = null; }
        }

        public void setActive() { m_active = true; }

        public override void update(float time)
        {
            if (m_dead || !m_active) return;
            m_inst.Update(time);
            m_velocity += m_acceleration * time;
            setPosition(getPosition() + m_velocity * time);
            p2i screenPos = screenFrom3DPos(getAbsolutePosition());
            m_touch.SetScreenCoordinates(screenPos.X - 20, screenPos.Y - 20, 40, 40);
            if (m_touch.IsActive())
            {
                int tx, ty;
                m_touch.GetTouchPos_Current(out tx, out ty);
                plane3df targetPlane = CPlayfield.GetInstance().getFlyingPlane();
                line3df ray = __IRR_SCENE().getRayFromScreenCoordinates(new p2i(tx, ty));
                v3f targetPos = v3f.Zero;
                targetPlane.getIntersectionWithLine(ray.start, ray.getVector(), ref targetPos);
                v3f targetDir = (targetPos - getPosition()) / time;
                const float maxVelocity = 25.0f;
                if (targetDir.getLength() > maxVelocity) targetDir.setLength(maxVelocity);
                m_velocity = targetDir;
                m_acceleration = v3f.Zero;
            }
            if (m_touch.IsNowReleased()) m_acceleration = new v3f(0, -9.8f * 2.0f, 0.0f);
            if (m_antiFireball || m_antiGriffin || m_antiIceball)
            {
                if ((m_target - getPosition()).dotProduct(m_velocity) < 0.0f)
                {
                    setPosition(m_target);
                    onTargetHit();
                }
            }
            else
            {
                plane3df ground = CPlayfield.GetInstance().getGroundPlane();
                if (ground.classifyPointRelation(getAbsolutePosition()) != EIntersectionRelation3D.ISREL3D_FRONT)
                {
                    v3f p = v3f.Zero;
                    ground.getIntersectionWithLine(getPosition(), m_velocity, ref p);
                    setPosition(p);
                    onGroundHit();
                }
                plane3df castle = CPlayfield.GetInstance().getCastlePlane();
                if (castle.classifyPointRelation(getAbsolutePosition()) != EIntersectionRelation3D.ISREL3D_FRONT)
                {
                    v3f p = v3f.Zero;
                    castle.getIntersectionWithLine(getPosition(), m_velocity, ref p);
                    setPosition(p);
                    onCastleHit();
                }
            }
            if (m_ps != null) m_ps.SetPosition(getPosition());
        }

        void onTargetHit()
        {
            setDead();
            if (m_antiFireball) CPlayfield.GetInstance().getFireball().elfArrowHit();
            if (m_antiIceball) CPlayfield.GetInstance().getIceball().elfArrowHit();
            if (m_antiGriffin) CPlayfield.GetInstance().getGriffin().elfArrowHit();
        }

        void onGroundHit()
        {
            setDead();
            foreach (CUnit unit in CPlayfield.GetInstance().getUnits())
            {
                float dist = (unit.getAbsolutePosition() - getPosition()).getLength();
                if (dist < 3.0f && unit.canBeDamagedByShamanBall())
                {
                    unit.damageUnit(1000.0f, UDS_Fireball);
                    break;
                }
            }
        }

        void onCastleHit()
        {
            setDead();
            CPlayfield.GetInstance().getGate().damageGate(m_damage);
            CPlayfield.GetInstance().m_damage += m_damage;
            if (!m_isElfArrow) __MGR_SOUNDS().play(SFX_ATTACK_SPELL_IMPACT);
        }

        public bool isDead() { return m_dead; }

        public void setDead()
        {
            if (m_dead) return;
            m_dead = true;
            if (m_ps != null) { ParticleManager.RemoveParticleSystem(m_ps); m_ps = null; }
            if (m_diePs != null)
            {
                ParticleSystem die = ParticleManager.AddParticleSystem(m_diePs);
                die.SetPosition(getPosition());
                die.SetScale(m_scale * 0.02f);
            }
            if (m_antiFireball) s_antiFireballArrowShot = false;
            if (m_antiIceball) s_antiIceballArrowShot = false;
            if (m_antiGriffin) s_antiGriffinArrowShot = false;
        }

        public static void reset() { s_antiFireballArrowShot = false; s_antiIceballArrowShot = false; s_antiGriffinArrowShot = false; }

        public override void draw()
        {
            if (m_dead) return;
            m_inst.Draw3D(getPosition(), m_scale);
            if (m_active && m_inst.GetASprite() == null)
            {
                Gfx.setTransform(E_TRANSFORMATION_STATE.ETS_WORLD, matrix4.Identity);
                Gfx.draw3DLine(m_startPos, getAbsolutePosition(), new SColor(0x80707070), E_MATERIAL_TYPE.EMT_TRANSPARENT_ADD_COLOR);
            }
        }

        public void setAntiFireball(v3f target) { s_antiFireballArrowShot = true; m_antiFireball = true; m_target = target; }
        public void setAntiIceball(v3f target) { s_antiIceballArrowShot = true; m_antiIceball = true; m_target = target; }
        public void setAntiGriffin(v3f target) { s_antiGriffinArrowShot = true; m_antiGriffin = true; m_target = target; }

        public void goElf()
        {
            v3f pos = getAbsolutePosition();
            __IRR_SCENE().getRootSceneNode().addChild(getSceneNode());
            setPosition(pos);
            v3f scale = getSceneNode().getScale();
            getSceneNode().setScale(new v3f(1.0f / scale.X, 1.0f / scale.Y, 1.0f / scale.Z));
            setActive();
            v3f dir = (m_antiFireball || m_antiGriffin || m_antiIceball) ? (m_target - pos).normalized() : new v3f(1, 0, 0);
            m_velocity = dir * 40.0f;
            getSceneNode().setRotation(new quaternion().rotationFromTo(dir, new v3f(0, 0, 1)));
            m_startPos = pos;
            m_isElfArrow = true;
        }

        public CTouchElement getTouch() { return m_touch; }
    }
}
