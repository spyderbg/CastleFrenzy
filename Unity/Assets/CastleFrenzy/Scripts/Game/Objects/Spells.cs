// Game/Objects/Weapons/Spells: the spell shield, gesture trail and the spell behaviours.
using System.Collections.Generic;
using static CastleFrenzy.G;
using static CastleFrenzy.GE;
using static CastleFrenzy.Ids;

namespace CastleFrenzy
{
    public abstract class ISpellBehaviour
    {
        public abstract void activate(p2i gestureEnd);
        public abstract bool update(float time);
        public virtual void draw2D() { }
        public virtual void draw3D() { }
        public virtual void adjustStats(IAttributes attr) { }
        public virtual void Dispose() { }

        /// <summary>Accelerometer pitch minus PI/2 (0 on desktop unless tilted with the arrow keys).</summary>
        protected static float TiltAngle() { return CApplication.GetInstance().GetAccelerometerPitch() - PI / 2; }

        public static v3f ProjectPointOnTheGround(p2i point2d)
        {
            line3df ray = __IRR_SCENE().getRayFromScreenCoordinates(point2d);
            CPlayfield pf = CPlayfield.GetInstance();
            plane3df flying = pf.getFlyingPlane(), ground = pf.getGroundPlane(), castle = pf.getCastlePlane();
            v3f v = v3f.Zero;
            flying.getIntersectionWithLine(ray.start, ray.getVector(), ref v);
            ground.getIntersectionWithLine(v, new v3f(0, 1, 0), ref v);
            if (castle.classifyPointRelation(v) == EIntersectionRelation3D.ISREL3D_BACK)
                castle.getIntersectionWithLine(v, castle.Normal, ref v);
            return v;
        }
    }

    public class CBoulderBehaviour : ISpellBehaviour
    {
        float m_radius, m_duration;
        readonly ColladaControl m_boulder = new ColladaControl();
        readonly ISceneNode m_shadow;
        bool m_falling, m_breaking;
        float m_speed, m_acceleration, m_time;
        int m_soundIndex = -1;

        public CBoulderBehaviour()
        {
            ISceneNode node = LoadColladaScene("Scenes/Spells/boulder.bdae");
            cleanNode(node);
            __IRR_SCENE().getRootSceneNode().addChild(node);
            m_boulder.setRoot(node);
            m_boulder.getRoot().setVisible(false);
            m_shadow = LoadColladaScene("Scenes/Goblin/shadow.bdae");
            cleanNode(m_shadow);
            __IRR_SCENE().getRootSceneNode().addChild(m_shadow);
            m_shadow.setVisible(false);
        }

        public override void Dispose()
        {
            m_boulder.getRoot().remove();
            m_shadow.remove();
        }

        public override void adjustStats(IAttributes attr)
        {
            m_radius = attr.getAttributeAsFloat("Radius");
            m_duration = attr.getAttributeAsFloat("Duration");
        }

        public override void activate(p2i gestureEnd)
        {
            CPlayfield playfield = CPlayfield.GetInstance();
            v3f pos = playfield.getPath().start + playfield.getPath().getVector() * 0.9f + new v3f(0, 30, 0);
            m_boulder.getRoot().setPosition(pos);
            m_boulder.setAnim("roll");
            m_boulder.getRoot().getSceneNodeFromName("boulder").setVisible(true);
            m_boulder.getRoot().getSceneNodeFromName("break").setVisible(false);
            m_boulder.getRoot().setVisible(true);
            m_shadow.setVisible(true);
            m_falling = true;
            m_breaking = false;
            m_time = 0;
            m_speed = 0;
            m_acceleration = 0;
            m_soundIndex = -1;
        }

        public override bool update(float time)
        {
            CPlayfield playfield = CPlayfield.GetInstance();
            m_time += time;
            bool spellDone = false;
            float damage_radius = m_radius * 1.3f;
            float boulder_scale = m_radius / 0.75f;
            float shadow_scale = boulder_scale * 3.5f;
            const float max_speed = 5.0f, max_acceleration = 10.0f;
            plane3df ground = playfield.getGroundPlane();
            ground.D -= m_radius;
            plane3df castle = playfield.getCastlePlane();
            castle.D -= m_radius;
            v3f pos = m_boulder.getRoot().getPosition();
            if (!m_breaking)
            {
                if (m_falling)
                {
                    pos += new v3f(0, -50, 0) * time;
                    if (ground.classifyPointRelation(pos) != EIntersectionRelation3D.ISREL3D_FRONT)
                    {
                        ground.getIntersectionWithLine(pos, new v3f(0, 1, 0), ref pos);
                        m_falling = false;
                        playfield.SetCameraShake(0.7f, 1.0f);
                        m_acceleration = -max_acceleration;
                        __MGR_SOUNDS().play(SFX_SPELL_FALLING_BOULDER_IMPACT);
                    }
                }
                if (!m_falling)
                {
                    int[] unitsHitByKind = new int[UK_Unknown + 1];
                    foreach (CUnit unit in new List<CUnit>(playfield.getUnits()))
                    {
                        if ((unit.getAbsolutePosition() - (pos - new v3f(0, m_radius, 0))).getLength() < damage_radius)
                        {
                            if (unit.isPetrified()) unit.unpetrify(true);
                            else if (unit.canBeKilledByLightning())
                            {
                                unit.damageUnit(9999, UDS_Lightning);
                                ++unitsHitByKind[unit.getUnitKind()];
                            }
                        }
                    }
                    playfield.getChallenge().unitsWereKilledByWeapon(UDS_Lightning, unitsHitByKind);
                    float angle = TiltAngle();
                    if (fabsf(angle) >= PI / 18)
                        m_acceleration = CLAMP(max_acceleration * angle / (PI / 6), -max_acceleration, max_acceleration);
                }
                m_speed = CLAMP(m_speed + m_acceleration * time, -max_speed, max_speed);
                if (m_speed != 0.0f && (m_soundIndex == -1 || !__MGR_SOUNDS().isSoundPlaying(SFX_SPELL_ROLLING_BOULDER, m_soundIndex)))
                    m_soundIndex = __MGR_SOUNDS().play(SFX_SPELL_ROLLING_BOULDER, true);
                float distance = m_speed * time;
                pos = pos + playfield.getPath().getVector().normalize() * distance;
                if (castle.classifyPointRelation(pos) != EIntersectionRelation3D.ISREL3D_FRONT)
                {
                    castle.getIntersectionWithLine(pos, playfield.getPath().getVector(), ref pos);
                    m_speed = -m_speed;
                }
                m_boulder.getRoot().setPosition(pos);
                m_boulder.getRoot().setScale(new v3f(1, 1, 1) * boulder_scale);
                v3f shadowPos = v3f.Zero;
                playfield.getGroundPlane().getIntersectionWithLine(pos, new v3f(0, 1, 0), ref shadowPos);
                m_shadow.setPosition(shadowPos);
                float distanceFromGround = playfield.getGroundPlane().getDistanceTo(pos);
                float scale = CLAMP(shadow_scale - 0.2f * distanceFromGround, 0.4f, shadow_scale);
                m_shadow.setScale(new v3f(1, 1, 1) * scale);
                if (m_time > m_duration)
                {
                    m_breaking = true;
                    m_boulder.setAnim("break", false);
                    m_boulder.getRoot().getSceneNodeFromName("boulder").setVisible(false);
                    m_boulder.getRoot().getSceneNodeFromName("break").setVisible(true);
                    m_boulder.getRoot().setRotation(new quaternion(0, RandFloat(0, 2 * PI), 0));
                }
                if (playfield.getPath().start.X > pos.X) spellDone = true;
            }
            else
            {
                if (m_boulder.checkEvent("anim_end")) spellDone = true;
                StopRolling();
            }
            if (spellDone)
            {
                m_boulder.getRoot().setVisible(false);
                m_shadow.setVisible(false);
                StopRolling();
                return false;
            }
            if (!m_breaking)
            {
                float p = 2 * PI * m_radius;
                float rot = 2 * PI * pos.X / p;
                m_boulder.getRoot().setRotation(new quaternion(0, 0, rot));
            }
            m_boulder.updateAtEndOfFrame(time);
            return true;
        }

        void StopRolling()
        {
            if (m_soundIndex == -1) return;
            __MGR_SOUNDS().stop(SFX_SPELL_ROLLING_BOULDER, m_soundIndex, 500);
            m_soundIndex = -1;
        }
    }

    public class CLightningBehaviour : ISpellBehaviour
    {
        readonly ASpriteInstance m_sprite = new ASpriteInstance();
        readonly ColladaControl m_wave = new ColladaControl();

        public CLightningBehaviour()
        {
            ASprite spr = new ASprite();
            spr.Load("Ingame/lightning");
            m_sprite.SetASprite(spr);
            ISceneNode wave = LoadColladaScene("Scenes/Spells/strike_plane.bdae");
            cleanNode(wave);
            __IRR_SCENE().getRootSceneNode().addChild(wave);
            wave.setVisible(false);
            m_wave.setRoot(wave);
        }

        public override void Dispose() { m_wave.getRoot().remove(); }

        public override void activate(p2i gestureEnd)
        {
            v3f v = ProjectPointOnTheGround(gestureEnd);
            p2i p = screenFrom3DPos(v);
            m_wave.getRoot().setPosition(v + new v3f(0, 0.1f, 0));
            m_wave.getRoot().setVisible(true);
            m_wave.setAnim(0, false);
            m_sprite.SetAnim(0, 0);
            m_sprite.SetPosition(p.X, p.Y);
            int[] unitsHitByKind = new int[UK_Unknown + 1];
            foreach (CUnit unit in new List<CUnit>(CPlayfield.GetInstance().getUnits()))
            {
                if ((unit.getAbsolutePosition() - v).getLength() >= 4.0f) continue;
                if (unit.isPetrified()) unit.unpetrify(true);
                else if (unit.canBeKilledByLightning())
                {
                    unit.damageUnit(9999, UDS_Lightning);
                    ++unitsHitByKind[unit.getUnitKind()];
                }
            }
            CPlayfield.GetInstance().getChallenge().unitsWereKilledByWeapon(UDS_Lightning, unitsHitByKind);
            __MGR_SOUNDS().play(SFX_SPELL_LIGHTNING);
        }

        public override bool update(float time)
        {
            bool continueSpell = true;
            if (m_wave.checkEvent("anim_end"))
            {
                m_wave.getRoot().setVisible(false);
                continueSpell = false;
            }
            m_sprite.Update(time);
            m_wave.updateAtEndOfFrame(time);
            return continueSpell;
        }

        public override void draw2D() { m_sprite.Draw(); }
    }

    public class CTornadoBehaviour : ISpellBehaviour
    {
        readonly CTouchElement m_touch = new CTouchElement();
        readonly ColladaControl m_tornado = new ColladaControl();
        float m_time;
        ParticleSystem m_dust;
        readonly ISceneNode m_bottom;
        float m_speed, m_radius, m_maxTime;
        int m_soundIndex = -1;

        public CTornadoBehaviour()
        {
            m_touch.SetEnabled(false);
            m_touch.m_zLevel = 0;
            ISceneNode tornado = LoadColladaScene("Scenes/Tornado/tornado.bdae");
            cleanNode(tornado);
            ISceneNode t = tornado.getSceneNodeFromName("tornato");
            if (t != null && t.Children.Count > 0 && t.Children[0].getMaterialCount() > 0)
            {
                t.Children[0].getMaterial(0).MaterialType = E_MATERIAL_TYPE.EMT_TRANSPARENT_TEXTURE_VERTEX_ALPHA;
                t.Children[0].getMaterial(0).Lighting = false;
            }
            tornado.setScale(new v3f(1.4f, 1.0f, 1.4f) * 0.20f);
            __IRR_SCENE().getRootSceneNode().addChild(tornado);
            tornado.setVisible(false);
            m_tornado.setRoot(tornado);
            m_bottom = m_tornado.getRoot().getSceneNodeFromName("Point01");
        }

        public override void Dispose()
        {
            m_tornado.getRoot().remove();
            m_touch.Dispose();
            if (m_dust != null) { ParticleManager.RemoveParticleSystem(m_dust); m_dust = null; }
        }

        public override void activate(p2i gestureEnd)
        {
            v3f v = ProjectPointOnTheGround(gestureEnd);
            m_tornado.getRoot().setPosition(v);
            m_tornado.getRoot().setVisible(true);
            m_tornado.setAnimAnim("tornado_down", "tornado_loop");
            m_touch.SetEnabled(true);
            m_time = 0;
            m_soundIndex = -1;
        }

        public override bool update(float time)
        {
            m_time += time;
            bool continueSpell = m_time < m_maxTime;
            v3f pos3D = m_tornado.getRoot().getPosition();
            p2i pos2D = screenFrom3DPos(pos3D);
            m_touch.SetScreenCoordinates(pos2D.X - 40, 0, 80, 320);
            if (m_tornado.checkEvent("on_ground"))
            {
                m_dust = ParticleManager.AddParticleSystem("tornado_dust");
                m_dust.SetScale(0.07f);
                m_dust.SetRotation(new quaternion(0, 0.5f * PI, 0.0f) * new quaternion(0.5f * PI, 0.0f, 0.0f));
            }
            v3f target = pos3D;
            float angle = TiltAngle();
            line3df path = CPlayfield.GetInstance().getPath();
            if (angle > PI / 18.0f) target = path.end;
            if (angle < -PI / 18.0f) target = path.start;
            if (m_touch.IsActive())
            {
                int tx, ty;
                m_touch.GetTouchPos_Current(out tx, out ty);
                target = ProjectPointOnTheGround(new p2i(tx, ty));
            }
            v3f velocity = (target - pos3D) / time;
            if (velocity.getLength() > m_speed) velocity.setLength(m_speed);
            pos3D += velocity * time;
            m_tornado.getRoot().setPosition(pos3D);
            CCameraSceneNode cam = __IRR_SCENE().getActiveCamera();
            v3f dir = cam.getAbsolutePosition() - m_tornado.getRoot().getPosition();
            dir.Y = 0;
            m_tornado.getRoot().setRotation(new quaternion().rotationFromTo(dir, new v3f(0, 0, 1)));
            if (m_dust != null && m_bottom != null)
            {
                v3f p1 = m_bottom.getAbsolutePosition();
                v3f d = p1 - cam.getAbsolutePosition();
                d.setLength(2.0f);
                m_dust.SetPosition(p1 - d);
            }
            if (!continueSpell)
            {
                m_tornado.setAnim("tornado_up", false);
                m_touch.SetEnabled(false);
                if (m_dust != null)
                {
                    ParticleManager.RemoveParticleSystem(m_dust);
                    m_dust = null;
                    if (m_soundIndex != -1)
                    {
                        __MGR_SOUNDS().stop(SFX_SPELL_TORNADO, m_soundIndex, 500);
                        m_soundIndex = -1;
                    }
                }
            }
            int[] unitsHitByKind = new int[UK_Unknown + 1];
            foreach (CUnit unit in new List<CUnit>(CPlayfield.GetInstance().getUnits())) affectUnit(time, unit, continueSpell, unitsHitByKind);
            foreach (CBomb bomb in new List<CBomb>(CPlayfield.GetInstance().getBombs())) affectBomb(time, bomb, continueSpell);
            CPlayfield.GetInstance().getChallenge().unitsWereKilledByWeapon(UDS_Lightning, unitsHitByKind);
            m_tornado.updateAtEndOfFrame(time);
            if (continueSpell && (m_soundIndex == -1 || !__MGR_SOUNDS().isSoundPlaying(SFX_SPELL_TORNADO, m_soundIndex)))
                m_soundIndex = __MGR_SOUNDS().play(SFX_SPELL_TORNADO);
            return continueSpell;
        }

        v3f Pull(v3f objPos, v3f myPos, float height, float time, out float distanceY)
        {
            v3f direction = objPos - myPos;
            direction.Y = 0.0f;
            direction.rotateXZBy(-15);
            direction.Y = 0.0f;
            direction.setLength(1.5f);
            v3f target = myPos + new v3f(0, height, 0) + direction;
            v3f velocityXZ = (target - objPos) / time; velocityXZ.Y = 0;
            v3f velocityY = (target - objPos) / time; velocityY.X = velocityY.Z = 0;
            distanceY = target.Y - objPos.Y;
            if (velocityXZ.getLength() > 25.0f - 2.5f * distanceY) velocityXZ.setLength(25.0f - 2.5f * distanceY);
            if (velocityY.getLength() > 1.5f) velocityY.setLength(1.5f);
            return velocityXZ + velocityY;
        }

        void affectUnit(float time, CUnit unit, bool continueSpell, int[] unitsHitByKind)
        {
            v3f unitPos = unit.getAbsolutePosition();
            v3f myPos = m_tornado.getRoot().getPosition();
            v3f direction = unitPos - myPos;
            direction.Y = 0.0f;
            if (unit.getState() == US_Tornado)
            {
                float distanceY;
                v3f vel = Pull(unitPos, myPos, 3.5f + 0.4f * unit.m_lane, time, out distanceY);
                unitPos += vel * time;
                unit.setMPosition(unitPos);
                unit.setVelocity(vel);
                if (fabsf(distanceY) < 0.4f || !continueSpell)
                {
                    unit.damageUnit(9999, UDS_Tornado);
                    unit.setState(US_Falling);
                    ++unitsHitByKind[unit.getUnitKind()];
                }
            }
            else if (direction.getLength() < m_radius && unit.canBeTakenByTornado()) unit.setState(US_Tornado);
        }

        void affectBomb(float time, CBomb bomb, bool continueSpell)
        {
            v3f bombPos = bomb.getRoot().getAbsolutePosition();
            v3f myPos = m_tornado.getRoot().getPosition();
            v3f direction = bombPos - myPos;
            direction.Y = 0.0f;
            if (!bomb.isFree() || direction.getLength() >= m_radius) return;
            float distanceY;
            v3f vel = Pull(bombPos, myPos, 3.5f, time, out distanceY);
            bombPos += vel * time;
            bomb.getRoot().setPosition(bombPos);
            bomb.setVelocity(vel);
        }

        public override void adjustStats(IAttributes attr)
        {
            m_speed = attr.getAttributeAsFloat("Speed");
            m_radius = attr.getAttributeAsFloat("Radius");
            m_maxTime = attr.getAttributeAsFloat("Time");
        }
    }

    public class CGhostArmyBehaviour : ISpellBehaviour
    {
        const int k_ghosts = 7;
        readonly v3f[] m_positions = new v3f[k_ghosts], m_velocities = new v3f[k_ghosts];
        readonly ASpriteInstance[] m_sprites = new ASpriteInstance[k_ghosts];
        float m_radius;
        int m_kills;
        int m_soundIndex = -1;

        public CGhostArmyBehaviour()
        {
            ASprite spr = new ASprite();
            spr.Load("Ingame/ghost_army");
            for (int i = 0; i < k_ghosts; ++i)
            {
                m_sprites[i] = new ASpriteInstance();
                m_sprites[i].SetASprite(spr);
            }
        }

        public override void activate(p2i gestureEnd)
        {
            line3df path = CPlayfield.GetInstance().getPath();
            v3f[] poss = new v3f[k_ghosts];
            for (int i = 0; i < k_ghosts; ++i)
            {
                m_velocities[i] = path.getVector().setLength(-RandFloat(4.5f, 5.5f));
                poss[i] = path.end + new v3f(2.4f + RandFloat(-2.3f, 2.3f), 0.2f, RandFloat(-1.3f, 1.3f));
                m_sprites[i].SetAnim(0);
            }
            // GetSortedIndices: ascending Z, stored back to front.
            int[] indices = new int[k_ghosts];
            for (int i = 0; i < k_ghosts; ++i) indices[i] = i;
            System.Array.Sort(indices, (a, b) => poss[a].Z.CompareTo(poss[b].Z));
            for (int i = 0; i < k_ghosts; ++i) m_positions[k_ghosts - i - 1] = poss[indices[i]];
            m_kills = 0;
            m_soundIndex = -1;
        }

        public override bool update(float time)
        {
            v3f pos = v3f.Zero;
            for (int i = 0; i < k_ghosts; ++i) pos += m_positions[i];
            pos /= k_ghosts;
            CPlayfield pf = CPlayfield.GetInstance();
            line3df path = pf.getPath();
            bool continueSpell = path.getVector().dotProduct(pos - path.start) >= 0.0f;
            for (int i = 0; i < k_ghosts; ++i) m_positions[i] += m_velocities[i] * time;
            int[] unitsHitByKind = new int[UK_Unknown + 1];
            foreach (CUnit unit in new List<CUnit>(pf.getUnits()))
            {
                v3f dir = unit.getAbsolutePosition() - pos;
                dir.Z = 0.0f;
                if (!unit.canBeKilledByGhostArmy() || dir.getLength() >= m_radius) continue;
                if (unit.isPetrified()) unit.unpetrify(true);
                else unit.damageUnit(9999, UDS_GhostArmy);
                if (unit.getSouls() > 0)
                {
                    ++m_kills;
                    pf.addSouls(unit.getSouls() * (m_kills % 2), unit.getChestDummy().getAbsolutePosition());
                }
                ++unitsHitByKind[unit.getUnitKind()];
            }
            pf.getChallenge().unitsWereKilledByWeapon(UDS_Lightning, unitsHitByKind);
            for (int i = 0; i < k_ghosts; ++i) m_sprites[i].Update(time * (0.95f + 0.05f * i));
            if (continueSpell)
            {
                if (m_soundIndex == -1 || !__MGR_SOUNDS().isSoundPlaying(SFX_SPELL_RUNNING_GHOSTS, m_soundIndex))
                    m_soundIndex = __MGR_SOUNDS().play(SFX_SPELL_RUNNING_GHOSTS);
            }
            else if (m_soundIndex != -1)
            {
                __MGR_SOUNDS().stop(SFX_SPELL_RUNNING_GHOSTS, m_soundIndex, 500);
                m_soundIndex = -1;
            }
            return continueSpell;
        }

        public override void draw3D()
        {
            for (int i = 0; i < k_ghosts; ++i) m_sprites[0].Draw3D(m_positions[i], 1.2f);
        }

        public override void adjustStats(IAttributes attr) { m_radius = 1.5f; }
    }

    public class CEarthquakeBehaviour : ISpellBehaviour
    {
        float m_time, m_maxTime, m_power;

        public override void activate(p2i gestureEnd) { m_time = 0.0f; }

        public override bool update(float time)
        {
            bool continueSpell = m_time < m_maxTime;
            float t = m_time / m_maxTime;
            t = 0.8f * powf(1.0f - t, 10.0f) + 0.2f;
            m_time += time;
            CPlayfield pf = CPlayfield.GetInstance();
            pf.SetCameraShake(1.0f, t * m_power);
            foreach (CUnit unit in new List<CUnit>(pf.getUnits()))
            {
                if (unit.canBeStunnedByEarthquake())
                {
                    if (unit.isPetrified()) unit.unpetrify(true);
                    unit.freeAllSlavesFromMindControl();
                    unit.setState(US_Earthquake);
                }
                if (unit.getState() == US_Earthquake)
                {
                    line3df path = pf.getPath();
                    v3f pos = unit.getPosition();
                    v3f dir = new v3f(0, 0, RandFloat(0.8f, 1.2f));
                    dir.rotateXZBy(RandFloat(-5, 5));
                    unit.setMPosition(pos + dir * time);
                    if (pos.Z - path.end.Z > 1.8f) unit.throwUnitInWater(pos + new v3f(0, -2, 1.5f), 0.5f);
                }
                if (!continueSpell && unit.getState() == US_Earthquake) unit.setState(US_Walking);
            }
            return continueSpell;
        }

        public override void adjustStats(IAttributes attr)
        {
            m_maxTime = attr.getAttributeAsFloat("Time");
            m_power = attr.getAttributeAsFloat("ShakePower");
        }
    }

    public class CSpell : CWeaponObject
    {
        public const int SS_Disabled = 0, SS_Ready = 1, SS_Casting = 2, SS_Active = 3, SS_Cooldown = 4;

        protected float m_stat_cooldown;
        ISpellBehaviour m_behaviour;
        int m_state;
        readonly ColladaControl m_shield = new ColladaControl();
        CTouchElement m_touch;
        readonly ITexture m_fingerTrailTexture;
        float m_fadeAlpha;
        readonly List<v3f> m_gesture = new List<v3f>();
        S3DVertex[] m_vertices = new S3DVertex[0];
        int[] m_indices = new int[0];
        float m_cooldown;
        bool m_visible;

        public CSpell() : base(null)
        {
            m_Node = LoadColladaScene("Scenes/Spells/spell_shield.bdae");
            __IRR_SCENE().getRootSceneNode().addChild(m_Node);
            cleanNode(m_Node);
            SColor e = CGameConfig.EmissiveColorForInteractiveObjects;
            setEmissiveColor(m_Node, new SColor(e.getAlpha() / 2, e.getRed() / 2, e.getGreen() / 2, e.getBlue() / 2));
            m_shield.setRoot(m_Node);
            setLevel(ST_None);
            reset();
            m_fingerTrailTexture = TextureCache.getTexture("Scenes/Spells/finger_trail.png");
        }

        public override int getType() { return WT_Spell; }

        public override void reset()
        {
            base.reset();
            setLevel(m_level);
            setState(m_level == ST_None ? SS_Disabled : SS_Ready);
            m_cooldown = 0.0f;
            setVisible(true);
        }

        public override void Dispose()
        {
            m_Node.remove();
            if (m_behaviour != null) { m_behaviour.Dispose(); m_behaviour = null; }
        }

        protected override void adjustStats(IAttributes attributes)
        {
            string[] cooldown = { "CooldownEasy", "CooldownMedium", "CooldownHard" };
            int difficulty = CPlayfield.GetInstance().getDifficulty();
            m_stat_cooldown = attributes.getAttributeAsFloat(cooldown[difficulty]);
            if (CGameConfig.NoWeaponCD) m_stat_cooldown = 0.5f;
            if (m_behaviour != null) { m_behaviour.Dispose(); m_behaviour = null; }
            ITexture tex = null;
            switch (m_level)
            {
                case ST_Boulder: tex = TextureCache.getTexture("Scenes/Spells/shield_boulder.png"); m_behaviour = new CBoulderBehaviour(); break;
                case ST_Lightning: tex = TextureCache.getTexture("Scenes/Spells/shield_lightning.png"); m_behaviour = new CLightningBehaviour(); break;
                case ST_Tornado: tex = TextureCache.getTexture("Scenes/Spells/shield_tornado.png"); m_behaviour = new CTornadoBehaviour(); break;
                case ST_GhostArmy: tex = TextureCache.getTexture("Scenes/Spells/shield_ghost_army.png"); m_behaviour = new CGhostArmyBehaviour(); break;
            }
            ISceneNode shield = m_shield.getRoot().getSceneNodeFromName("shield");
            if (shield != null && shield.Children.Count > 0 && shield.Children[0].getMaterialCount() > 0)
                shield.Children[0].getMaterial(0).Textures[0] = tex;
            if (m_behaviour != null) m_behaviour.adjustStats(attributes);
        }

        public bool canStartCast() { return m_state == SS_Ready; }
        public bool isCasting() { return m_state == SS_Casting; }

        public void startCast(CTouchElement touch)
        {
            m_touch = touch;
            setState(SS_Casting);
            CGesture.GetInstance().Reset();
        }

        public override void update(float time)
        {
            base.update(time);
            m_fadeAlpha -= time;
            switch (m_state)
            {
                case SS_Casting:
                {
                    int px, py;
                    m_touch.GetTouchPos_Current(out px, out py);
                    p2i p = new p2i(px, py);
                    CGesture g = CGesture.GetInstance();
                    if (m_touch.IsActive() && g.GetInput().Count < 100)
                    {
                        g.Update(p, time);
                        m_fadeAlpha = 1.0f;
                        m_gesture.Clear();
                        foreach (CGesture.SKey k in g.GetInput()) m_gesture.Add(new v3f(k.Point.X, k.Point.Y, 0.0f));
                    }
                    else if (g.MatchGesture() == m_level)
                    {
                        CPlayfield.GetInstance().getChallenge().specialWeaponUsed();
                        m_behaviour.activate(p);
                        setState(SS_Active);
                    }
                    else setState(SS_Ready);
                    break;
                }
                case SS_Active:
                    if (!m_behaviour.update(time)) setState(SS_Cooldown);
                    break;
                case SS_Cooldown:
                    m_cooldown += time;
                    if (m_cooldown >= m_stat_cooldown)
                    {
                        m_cooldown = 0;
                        setState(SS_Ready);
                    }
                    break;
            }
            m_shield.updateAtEndOfFrame(time);
        }

        public override void draw()
        {
            drawGesture();
            if (m_state == SS_Active) m_behaviour.draw2D();
        }

        public void draw3D() { if (m_state == SS_Active) m_behaviour.draw3D(); }

        public override void setVisible(bool visible)
        {
            m_visible = visible;
            m_Node.setVisible(m_visible && m_level != ST_None);
        }

        public override bool isVisible() { return m_visible; }

        public void setState(int state)
        {
            m_state = state;
            switch (m_state)
            {
                case SS_Ready: GameTimer.Speed = 1.0f; m_shield.setAnim("spell_off"); break;
                case SS_Casting: GameTimer.Speed = 0.5f; m_shield.setAnim("spell_on"); break;
                case SS_Active: GameTimer.Speed = 1.0f; m_shield.setAnim("cooldown"); break;
                case SS_Cooldown: m_shield.setAnim("cooldown"); break;
            }
        }

        public override float getCooldown()
        {
            if (m_state != SS_Cooldown) return 0.0f;
            return MAX(0.0f, 1.0f - m_cooldown / m_stat_cooldown);
        }

        public int getState() { return m_state; }
        public override void setShopState() { m_shield.setAnim("spell_off"); }

        void drawGesture()
        {
            if (m_fadeAlpha <= 0.0f || m_gesture.Count <= 3) return;
            int size = m_gesture.Count - 1;
            if (m_vertices.Length < 4 * size) { m_vertices = new S3DVertex[4 * size]; m_indices = new int[6 * size]; }
            SColor color = new SColor((int)(127 * m_fadeAlpha), 255, 255, 255);
            const float lineWidth = 32.0f;
            for (int i = 0; i < size; ++i)
            {
                m_indices[6 * i + 0] = 4 * i + 0;
                m_indices[6 * i + 1] = 4 * i + 1;
                m_indices[6 * i + 2] = 4 * i + 2;
                m_indices[6 * i + 3] = 4 * i + 2;
                m_indices[6 * i + 4] = 4 * i + 1;
                m_indices[6 * i + 5] = 4 * i + 3;
            }
            for (int i = 0; i < size; ++i)
            {
                v3f p1 = m_gesture[i], p2 = m_gesture[i + 1];
                v3f parr12 = p2 - p1;
                v3f perp12 = new v3f(-parr12.Y, parr12.X, 0.0f);
                v3f perp;
                if (i == 0) perp = perp12;
                else
                {
                    v3f parr01 = p1 - m_gesture[i - 1];
                    perp = (new v3f(-parr01.Y, parr01.X, 0.0f) + perp12) * 0.5f;
                }
                perp *= 0.5f * lineWidth / perp.getLength();
                m_vertices[4 * i + 0] = new S3DVertex(p1 + perp, color, new v2f(0.0f, 0.0f));
                m_vertices[4 * i + 1] = new S3DVertex(p1 - perp, color, new v2f(0.0f, 1.0f));
                if (i == size - 1) perp = perp12;
                else
                {
                    v3f parr23 = m_gesture[i + 2] - p2;
                    perp = (new v3f(-parr23.Y, parr23.X, 0.0f) + perp12) * 0.5f;
                }
                perp *= 0.5f * lineWidth / perp.getLength();
                m_vertices[4 * i + 2] = new S3DVertex(p2 + perp, color, new v2f(1.0f, 0.0f));
                m_vertices[4 * i + 3] = new S3DVertex(p2 - perp, color, new v2f(1.0f, 1.0f));
            }
            SMaterial m = new SMaterial { MaterialType = E_MATERIAL_TYPE.EMT_TRANSPARENT_ALPHA_CHANNEL, Lighting = false };
            m.Textures[0] = m_fingerTrailTexture;
            Gfx.drawVertexPrimitiveList(m_vertices, size * 4, m_indices, size * 6, m);
        }
    }
}
