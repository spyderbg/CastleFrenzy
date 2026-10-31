// Game/Objects/Unit: every enemy kind (goblin, boar, rider, shaman, troll, elf, skeleton, bomblin).
using System.Collections.Generic;
using static CastleFrenzy.G;
using static CastleFrenzy.GE;
using static CastleFrenzy.Ids;

namespace CastleFrenzy
{
    public class CUnit : CGameObject
    {
        public int m_lane = -1, m_positionInList, m_id;

        protected bool m_harmlessFall, m_thrownByBoar;
        protected int m_unitKind;
        protected bool m_isIntroUnit;
        protected string m_SoundsPackName;
        protected readonly string[] m_animations = new string[UA_TotalCount];
        protected readonly float[] m_animationSpeeds = new float[UA_TotalCount];
        protected float m_statHealth, m_statDamage, m_statSpeed, m_statFallStun, m_statFallDie, m_statRange;
        protected int m_statSouls;
        protected bool m_statMustDragIntoGroundToKill, m_statNoDeathFromFastFall, m_statIsFreeBoar, m_statIsWildBoar, m_statIsMindControlled;
        protected float m_statDistanceBeforeFallAnim, m_statStumbleDistance;
        protected float m_statCharge, m_statChargeWalkMin, m_statChargeWalkMax, m_statChargeHealth;
        protected bool m_statFireImmunity, m_statIceImmunity;
        protected CUnit m_rider, m_mount;
        protected ISceneNode m_bip01;
        protected int m_state = -1;
        protected float m_stateTime;
        protected CProjectile m_projectile;
        protected int m_currentAnim;
        protected bool m_currentAnimLoop, m_brokenLink;
        protected bool m_dieStartScaleGo;
        protected v3f m_dieStartScaleScale, m_dieStartScalePos, m_dieStartScaleChestPos;
        protected ISceneNode m_chestDummy, m_dragDummy, m_energyBallDummy, m_shamanStaffDummy, m_headDummy;
        protected CBomb m_bomb;
        protected float m_maxFallSpeedY, m_dustTime, m_freezeDuration;
        protected line3df m_path;
        protected plane3df m_flyingPlane;
        protected float m_gravity;
        protected v3f m_acceleration, m_velocity, m_rotationVelocity, m_position;
        protected bool m_charge;
        protected float m_chargeMinWalk;
        protected p2i m_dragOffset;
        protected float m_health;
        protected int m_lastDamageSource = UDS_GroundFall;
        protected bool m_blood;
        protected CTouchElement m_touch;
        protected bool m_isAnimationOver, m_gaveMoney;
        protected quaternion m_modelRotation = quaternion.Identity, m_startRotation = quaternion.Identity;
        protected float m_maxY;
        protected ShamanAura m_shamanAura;
        protected CUnit m_mindControlMaster;
        protected readonly List<CUnit> m_mindControlSlaves = new List<CUnit>();
        protected readonly ASpriteInstance m_shamanStaff = new ASpriteInstance();
        protected readonly ASpriteInstance m_stunnedStars = new ASpriteInstance();
        protected readonly ColladaControl m_shamanWave = new ColladaControl();
        protected int m_unitsHit;
        protected readonly int[] m_unitsHitByKind = new int[UK_Unknown + 1];
        protected bool m_stoned;
        protected int m_prerenderedFrame = -1;
        protected int m_catchTries, m_catchCounter;
        protected float m_catchTime;
        protected bool m_ropePhysicsApplied;
        protected ISceneNode m_shadow;
        protected string m_arrowMesh;
        protected ISceneNode m_arrowDummy;

        static readonly v3f hydraFood = new v3f(-1.1784601f, 4.7320328f, 10.047342f);

        public CUnit(UnitParameters p, ISceneNode root = null) : base(null)
        {
            m_SoundsPackName = p.soundsPackName;
            m_stunnedStars.StartAnim(CPlayfield.GetInstance().getSpriteDizzy(), 0, 0, 0);
            m_unitKind = p.unitKind;
            int difficulty = CPlayfield.GetInstance().getDifficulty();
            m_statDamage = p.damageByDifficulty[difficulty];
            m_Node = LoadColladaScene(p.modelFile, true);
            if (m_unitKind == UK_Bomblin)
            {
                m_bomb = new CBomb(m_statDamage, root);
                m_bomb.getRoot().setScale(new v3f(1, 1, 1) * 1.4f);
                CPlayfield.GetInstance().addBomb(m_bomb);
            }
            OptimizedCleanUnit(p, m_Node);
            float scale = p.scale;
            m_Node.setScale(new v3f(scale, scale, scale));
            (root ?? m_SceneManager.getRootSceneNode()).addChild(m_Node);
            if (!string.IsNullOrEmpty(p.shamanDieWaveModel))
            {
                ISceneNode wave = LoadColladaScene(p.shamanDieWaveModel, true);
                cleanNode(wave);
                wave.setVisible(false);
                (root ?? m_SceneManager.getRootSceneNode()).addChild(wave);
                m_shamanWave.setRoot(wave);
            }
            if (!string.IsNullOrEmpty(p.arrowMeshModel))
            {
                m_arrowMesh = p.arrowMeshModel;
                m_arrowDummy = m_Node.getSceneNodeFromName(p.arrowMeshDummy);
            }
            m_modelRotation = p.rotation;
            for (int i = 0; i < UA_TotalCount; ++i)
            {
                m_animations[i] = p.animations[i];
                m_animationSpeeds[i] = p.animationSpeeds[i];
            }
            m_statDistanceBeforeFallAnim = p.statDistanceBeforeFallAnim;
            m_statStumbleDistance = p.statStumbleDistance;
            if (!string.IsNullOrEmpty(p.shadowModel))
            {
                m_shadow = LoadColladaScene(p.shadowModel, true);
                OptimizedCleanShadow(m_shadow);
                (root ?? m_SceneManager.getRootSceneNode()).addChild(m_shadow);
            }
            if (!string.IsNullOrEmpty(p.auraDummyNodeName))
            {
                m_shamanAura = new ShamanAura(p.auraDummyEffectName, p.auraDummyScale, m_Node.getSceneNodeFromName(p.auraDummyNodeName));
                m_shamanAura.show();
            }
            m_statSpeed = RandFloat(p.minSpeedByDifficulty[difficulty], p.maxSpeedByDifficulty[difficulty]);
            m_statRange = RandFloat(p.minRange, p.maxRange);
            m_statSouls = (int)p.souls;
            m_statMustDragIntoGroundToKill = p.mustDragIntoGroundToKill;
            m_statNoDeathFromFastFall = p.noDeathFromFastFall;
            m_statHealth = p.health;
            m_statFallStun = p.fallStun;
            m_statFallDie = p.fallDie;
            m_thrownByBoar = p.thrownByBoar;
            m_statIsFreeBoar = p.isFreeBoar;
            m_statIsWildBoar = p.isWildBoar;
            m_statIsMindControlled = p.isMindControlled;
            m_gravity = p.gravity;
            m_maxFallSpeedY = p.maxFallSpeedY;
            m_statCharge = p.charge;
            m_statChargeHealth = p.chargeHealth;
            m_statChargeWalkMin = p.chargeWalkMin;
            m_statChargeWalkMax = p.chargeWalkMax;
            m_statFireImmunity = p.fireImmunity;
            m_statIceImmunity = p.iceImmunity;
            m_animationSpeeds[UA_Walk_Normal] = m_statSpeed / m_animationSpeeds[UA_Walk_Normal];
            m_animationSpeeds[UA_Walk_Injured] = m_statSpeed / m_animationSpeeds[UA_Walk_Injured];
            m_animationSpeeds[UA_Walk_Victory] = m_statSpeed / m_animationSpeeds[UA_Walk_Victory];
            m_animationSpeeds[UA_Walk_Fast] = m_statSpeed / m_animationSpeeds[UA_Walk_Fast];
            m_animationSpeeds[UA_Walk_Charge] = m_statSpeed * m_statCharge / m_animationSpeeds[UA_Walk_Charge];
            setVisible(false);
            m_currentAnim = UA_Invalid;
            m_acceleration = new v3f(0, m_gravity, 0);
        }

        public override void Dispose()
        {
            if (m_Node != null) m_Node.remove();
            if (m_shadow != null) m_shadow.remove();
            if (m_shamanWave.getRoot() != null) m_shamanWave.getRoot().remove();
            if (m_shamanAura != null) { m_shamanAura.Dispose(); m_shamanAura = null; }
        }

        public line3df getPath() { return m_path; }
        public CUnit getRider() { return m_rider; }
        public void setRider(CUnit rider) { m_rider = rider; }
        public int getSouls() { return m_statSouls; }
        public ISceneNode getChestDummy() { return m_chestDummy; }
        public bool isFreeBoar() { return m_statIsFreeBoar; }
        public bool isWildBoar() { return m_statIsWildBoar; }
        public bool isRedSkeleton() { return m_unitKind == UK_Skeleton && m_statFireImmunity; }
        public bool isMindControllingShaman() { return m_unitKind == UK_Shaman && m_mindControlSlaves.Count > 0; }
        public bool isMindControlled() { return m_mindControlMaster != null; }
        public float getHealth() { return m_health; }
        public float getStatHealth() { return m_statHealth; }

        void shamanShoot()
        {
            m_projectile = new CProjectile(CPlayfield.GetInstance().getSpriteEffects(), INGAME_A_EFFECTS_SHAMAN_FIRE, "green_el_blast_new", 0.2f,
                m_energyBallDummy.getAbsolutePosition(), v3f.Zero, v3f.Zero, m_statDamage);
            CPlayfield.GetInstance().addProjectile(m_projectile);
        }

        public void setIntroUnit() { m_isIntroUnit = true; }

        public void showIntroUnitInfo()
        {
            m_isIntroUnit = false;
            CPlayfield.GetInstance().showIntroUnitInfo(this);
        }

        const int ET_Door = 0, ET_Fireball = 1, ET_Iceball = 2, ET_Griffin = 3;

        static int elfChooseTarget(int fs, int ist, int gs)
        {
            int[] targets = new int[3];
            int count = 0;
            if (!CProjectile.s_antiFireballArrowShot && fs == CFireball.FS_Ready) targets[count++] = ET_Fireball;
            if (!CProjectile.s_antiGriffinArrowShot && gs == CGriffin.GGS_Active) targets[count++] = ET_Griffin;
            if (!CProjectile.s_antiIceballArrowShot && ist == CIceball.IS_Ready) targets[count++] = ET_Iceball;
            return count > 0 ? targets[RandInt(0, count - 1)] : ET_Door;
        }

        void elfShoot()
        {
            m_projectile = new CProjectile(m_arrowMesh, m_statDamage, "petrify_smoke", 1.5f);
            CPlayfield.GetInstance().addProjectile(m_projectile);
            v3f scale = m_Node.getScale();
            m_projectile.getSceneNode().setScale(new v3f(1.0f / scale.X, 1.0f / scale.Y, 1.0f / scale.Z));
            m_arrowDummy.addChild(m_projectile.getSceneNode());
            m_projectile.getSceneNode().updateAbsolutePosition();
            m_projectile.getSceneNode().setRotation(new quaternion(0.5f * PI, 0, 0));
            CPlayfield pf = CPlayfield.GetInstance();
            switch (elfChooseTarget(pf.getFireball().getState(), pf.getIceball().getState(), pf.getGriffin().getState()))
            {
                case ET_Door:
                    m_Node.setRotation(new quaternion().rotationFromTo(m_path.getVector(), new v3f(0, 0, 1)));
                    break;
                case ET_Fireball:
                {
                    v3f target = pf.getFireball().getSceneNode().getAbsolutePosition();
                    m_projectile.setAntiFireball(target);
                    v3f dir = target - getPosition();
                    dir.Y = 0;
                    m_Node.setRotation(new quaternion().rotationFromTo(dir, new v3f(0, 0, 1)));
                    break;
                }
                case ET_Iceball:
                {
                    v3f target = pf.getIceball().getSceneNode().getAbsolutePosition();
                    m_projectile.setAntiIceball(target);
                    v3f dir = target - getPosition();
                    dir.Y = 0;
                    m_Node.setRotation(new quaternion().rotationFromTo(dir, new v3f(0, 0, 1)));
                    break;
                }
                case ET_Griffin:
                {
                    v3f target = pf.getGriffin().getSceneNode().getAbsolutePosition() + new v3f(0, 4, 0);
                    m_projectile.setAntiGriffin(target);
                    v3f dir = target - getPosition();
                    dir.Y = 0;
                    m_Node.setRotation(new quaternion().rotationFromTo(dir, new v3f(0, 0, 1)));
                    break;
                }
            }
        }

        public void setState(int state)
        {
            CPlayfield pf = CPlayfield.GetInstance();
            if (m_mount != null && m_state == US_Riding && state != US_Riding && state != US_Petrified)
            {
                m_position = m_mount.getPosition() + getPosition();
                __IRR_SCENE().getRootSceneNode().addChild(m_Node);
                setPosition(m_position);
                m_Node.setRotation(m_startRotation);
                m_mount.freeBoar();
                m_mount = null;
            }
            if (m_state == US_Attacking && m_unitKind == UK_Elf) m_Node.setRotation(m_startRotation);
            if (m_state == US_Falling) m_Node.setRotation(m_startRotation);
            bool wasPetrified = m_state == US_Petrified;
            m_state = state;
            m_stateTime = 0;
            if (m_bomb != null && m_state == US_None) { }
            else if (m_bomb != null && m_state != US_Walking)
            {
                CBomb bomb = m_bomb;
                m_bomb = null;
                if (m_state == US_Dragged || m_state == US_Falling || m_state == US_ToBeEeatenByHydra || m_state == US_Petrified || m_state == US_Tornado)
                    bomb.dropOnGround(bomb.getRoot().getScale() * m_Node.getScale().X);
                else
                    bomb.explode(m_state == US_Attacking);
            }
            if (m_state == US_Dragged && m_catchTries < 5)
            {
                float time = pf.getTime();
                float delta = time - m_catchTime;
                m_catchTime = time;
                bool levitate = m_unitKind == UK_Shaman || m_mindControlMaster != null || (m_unitKind == UK_Skeleton && pf.findShaman() != null);
                if (!levitate && (m_catchCounter == 0 || delta > 0.50f))
                {
                    m_catchCounter++;
                    if (m_catchCounter >= 2 && m_catchCounter <= 6)
                    {
                        int bonus = 2 + m_catchCounter * 2;
                        string text = null;
                        if (m_catchCounter == 5)
                        {
                            pf.getChallenge().comboAchieved(m_unitKind);
                            text = _TXT(STRING_IDS.INGAME_GREAT);
                        }
                        pf.addMoney(0, bonus, getAbsolutePosition(), text);
                    }
                }
            }
            else if (m_state != US_Falling && m_state != US_FallingInWater)
            {
                if (m_catchCounter != 0) ++m_catchTries;
                m_catchCounter = 0;
            }

            switch (m_state)
            {
                case US_Walking:
                    m_flyingPlane.getIntersectionWithLine(m_position, m_flyingPlane.Normal, ref m_position);
                    if ((m_statIsFreeBoar && m_rider == null) || (m_statIsMindControlled && m_mindControlMaster == null))
                        setState(US_TurningAroundToRunAway);
                    else if (m_unitKind == UK_Bomblin && m_bomb == null)
                        setState(US_TurningAroundToRunAway);
                    else
                    {
                        setAnimation(m_health == m_statHealth ? UA_Walk_Normal : UA_Walk_Injured, true);
                        if (m_rider != null) m_rider.setAnimation(UA_Riding_Walk, true);
                        if (m_mindControlSlaves.Count > 0) setAnimation(UA_MindControl, true);
                    }
                    m_charge = false;
                    m_chargeMinWalk = RandFloat(m_statChargeWalkMin, m_statChargeWalkMax);
                    break;
                case US_Earthquake: setAnimation(UA_Earthquake, false); break;
                case US_Tornado: setAnimation(UA_Flying, true); break;
                case US_SkeletonAppear:
                    setAnimation(UA_SkeletonAppear, false);
                    setPosition(m_position = m_path.end - m_path.getVector().setLength(RandFloat(2, 6)));
                    break;
                case US_SkeletonLand: setAnimation(UA_SkeletonLand, false); break;
                case US_ElfWallGrab:
                    setAnimation(UA_ElfWallGrab, false);
                    m_velocity = v3f.Zero;
                    break;
                case US_ElfWallFall:
                    setState(US_Falling);
                    m_harmlessFall = true;
                    setAnimation(UA_ElfWallFall, true);
                    break;
                case US_FallingOnHydra: setAnimation(UA_Falling, true); break;
                case US_TurningAroundToRunAway: setAnimation(UA_TurnAround, false); break;
                case US_RunningAway:
                case US_RunningAwayThrowingStuffAround:
                    setAnimation(UA_Walk_Fast, true);
                    m_Node.setRotation(new quaternion(0, PI, 0) * m_startRotation);
                    break;
                case US_Dragged:
                    if (m_shamanAura != null) m_shamanAura.hide();
                    m_maxY = 0;
                    m_velocity = v3f.Zero;
                    setAnimation(m_mindControlSlaves.Count > 0 ? UA_DraggedMindControl : UA_Dragged, true);
                    if (m_rider != null) m_rider.setAnimation(UA_Riding_Dragged, true);
                    break;
                case US_Riding:
                    setAnimation(UA_Walk_Normal, true);
                    m_Node.setRotation(m_modelRotation);
                    break;
                case US_Falling:
                {
                    m_maxY = 0.0f;
                    if (m_shamanAura != null) m_shamanAura.hide();
                    if (m_currentAnim != UA_Die_WallHit)
                    {
                        setAnimation(UA_Flying, true);
                        PlayUnitSound("Throw");
                    }
                    if (pf.getHydra().isReadyToEat(this) && !m_harmlessFall && m_lastDamageSource == UDS_Fireball)
                    {
                        m_acceleration = new v3f(0, m_gravity, 0);
                        line3df path = pf.getPath();
                        float d = m_velocity.Y * m_velocity.Y - 2.0f * m_acceleration.Y * (m_position.Y - path.start.Y);
                        if (d >= 0)
                        {
                            float t = (1 / m_acceleration.Y) * (-m_velocity.Y - sqrtf(d));
                            v3f hit = (m_acceleration * (0.5f * t) + m_velocity) * t + m_position;
                            float topT = -m_velocity.Y / m_acceleration.Y;
                            v3f top = (m_acceleration * (0.5f * topT) + m_velocity) * topT + m_position;
                            float distGroundToTop = top.Y - m_path.start.Y;
                            p2i hitScreen = screenFrom3DPos(hit);
                            if (hitScreen.X > 330 && hitScreen.X < 480 && distGroundToTop > 5.0f && m_statRange == 0)
                            {
                                float d2 = m_velocity.Y * m_velocity.Y - 2.0f * m_acceleration.Y * (m_position.Y - hydraFood.Y);
                                if (d2 >= 0)
                                {
                                    float timeToFallOnHydra = (1 / m_acceleration.Y) * (-m_velocity.Y - sqrtf(d2));
                                    v3f toHydraVel = (hydraFood - m_position) / timeToFallOnHydra;
                                    m_velocity.X = toHydraVel.X;
                                    m_velocity.Z = toHydraVel.Z;
                                    m_state = US_FallingOnHydra;
                                    pf.getHydra().waitForUnit(this);
                                }
                            }
                        }
                    }
                    break;
                }
                case US_BeingEatenByHydra: pf.getHydra().eatUnitStart(this); break;
                case US_Petrified: freeAllSlavesFromMindControl(); break;
                case US_Stunned:
                    setAnimation(UA_Stunned, false);
                    if (m_shamanAura != null) m_shamanAura.show();
                    if (m_rider != null) m_rider.setAnimation(UA_Riding_Stunned, true);
                    break;
                case US_Stumble: setAnimation(UA_Stumble, false); break;
                case US_ToBeEeatenByHydra:
                    if (m_shamanAura != null) m_shamanAura.hide();
                    freeFromMindControl();
                    freeAllSlavesFromMindControl();
                    break;
                case US_Attacking:
                    if (m_unitKind == UK_Shaman) shamanShoot();
                    if (m_unitKind == UK_Elf) elfShoot();
                    setAnimation(UA_Attack, true);
                    if (m_rider != null) m_rider.setAnimation(UA_Riding_Attack, true);
                    break;
                case US_MindControlling: setAnimation(UA_MindControl, true); break;
                case US_Dying:
                {
                    AddFrameEvent(FET_UnitDying, m_unitKind);
                    m_dieStartScaleScale = m_Node.getScale();
                    freeFromMindControl();
                    freeAllSlavesFromMindControl();
                    m_dieStartScaleGo = false;
                    int amount = 10, bonus = 0, anim;
                    switch (m_lastDamageSource)
                    {
                        case UDS_GroundFall: anim = UA_Die_GroundFall; amount = 10; break;
                        case UDS_GroundDrag: amount = 15; anim = UA_Die_GroundSmash; break;
                        case UDS_GriffinSmash: amount = 10; anim = UA_Die_GroundSmash; break;
                        case UDS_WallHit: amount = 15; anim = UA_Die_WallHit; break;
                        case UDS_Fireball: amount = 10; anim = UA_Die_GroundFall; break;
                        // UDS_TrollSmash falls through to default in the original (missing break).
                        default: amount = 10; anim = UA_Die_GroundFall; if (m_lastDamageSource == UDS_TrollSmash) bonus = 4; break;
                    }
                    if (!m_gaveMoney)
                    {
                        m_gaveMoney = true;
                        int comboMoney;
                        string comboText;
                        bool combo = pf.checkCombo(m_lastDamageSource, m_unitKind, out comboMoney, out comboText);
                        bonus += comboMoney;
                        if (combo) pf.getChallenge().comboAchieved(pf.m_comboUnitKind);
                        pf.addMoney(amount, bonus, getAbsolutePosition(), comboText);
                    }
                    if (anim != m_currentAnim) setAnimation(anim, false);
                    if (wasPetrified)
                    {
                        setState(US_Dead);
                        setVisible(false);
                        ParticleSystem ps = ParticleManager.AddParticleSystem(m_stoned ? "petrify_breaks" : "ice_breaks");
                        ps.SetScale(0.08f);
                        ps.SetRotation(new quaternion(0.5f * PI, 0.0f, 0.0f));
                        ps.SetPosition(getAbsolutePosition());
                    }
                    break;
                }
                case US_Dead:
                {
                    AddFrameEvent(FET_UnitDead, m_unitKind);
                    CAchievements ach = CAchievements.GetInstance();
                    switch (getUnitKind())
                    {
                        case UK_Goblin: ach.addValue("100 Goblins", 1); break;
                        case UK_Boar: ach.addValue("100 Boars", 1); break;
                        case UK_BoarRider: ach.addValue("100 Goblins", 1); break;
                        case UK_Shaman: ach.addValue("100 Warlocks", 1); break;
                        case UK_Troll: ach.addValue("100 Trolls", 1); break;
                        case UK_Elf: ach.addValue("100 Dark elves", 1); break;
                        case UK_Skeleton: ach.addValue("100 Skeletons", 1); break;
                    }
                    if (m_lastDamageSource == UDS_WallHit) ach.addValue("Brick in the wall", 1);
                    ++pf.m_kills;
                    break;
                }
                case US_Victory:
                    setAnimation(RandInt(0, 1) == 0 ? UA_Walk_Victory : UA_Walk_Normal, true);
                    if (m_rider != null) m_rider.setAnimation(UA_Riding_Victory, true);
                    break;
            }
            if (m_state != US_Attacking && m_projectile != null)
            {
                m_projectile.setDead();
                m_projectile = null;
            }
        }

        public int getState() { return m_state; }

        public void setAnimation(int anim, bool loop = false)
        {
            if (m_currentAnim == anim && m_currentAnimLoop && loop) return;
            m_currentAnim = anim;
            m_currentAnimLoop = loop;
            ISceneNodeAnimator animator = m_Node.getFirstAnimator();
            if (animator == null) return;
            animator.setEventsCallback(eventsCallback, this);
            CTimelineController ctrl = animator.getTimelineCtrl();
            ctrl.setClip(m_animations[anim]);
            ctrl.setScale(m_animationSpeeds[anim] * RandFloat(0.90f, 1.10f));
            ctrl.setLoop(loop);
            ctrl.setCallback(timelineCtrlCallback, this);
            m_isAnimationOver = false;
        }

        static void timelineCtrlCallback(CTimelineController ctrl, object self) { ((CUnit)self).m_isAnimationOver = true; }

        static void eventsCallback(STriggeredEvent ev, object self)
        {
            CPlayfield playfield = CPlayfield.GetInstance();
            CUnit unit = (CUnit)self;
            switch (ev.name)
            {
                case "hit":
                    playfield.addSpark(unit.getAbsolutePosition() + new v3f(0, 2, 0) + randomVectorInSphere(0.1f));
                    playfield.getGate().damageGate(unit.m_statDamage);
                    playfield.m_damage += unit.m_statDamage;
                    unit.PlayUnitSound("Attack");
                    break;
                case "throw":
                    if (unit.m_projectile != null)
                    {
                        unit.m_projectile.setActive();
                        unit.m_projectile.m_velocity = unit.m_path.getVector().normalize() * 4.0f;
                        unit.m_projectile.getTouch().SetEnabled(true);
                        unit.m_projectile = null;
                        unit.PlayUnitSound("Attack");
                    }
                    break;
                case "shoot":
                    if (unit.m_projectile != null)
                    {
                        unit.m_projectile.goElf();
                        unit.m_projectile = null;
                        unit.PlayUnitSound("Attack");
                    }
                    break;
                case "troll_stun":
                    unit.trollStun();
                    break;
            }
        }

        public bool isAnimationOver() { return m_isAnimationOver; }

        public bool isDragable()
        {
            if (m_isIntroUnit || (m_mount != null && m_mount.m_isIntroUnit)) return false;
            if (m_state == US_Dying || m_state == US_Dead || m_state == US_Stumble || m_state == US_Petrified) return false;
            if (m_state == US_FallingOnHydra || m_state == US_RunningAwayThrowingStuffAround || m_state == US_TurningAroundToRunAway || m_state == US_ToBeEeatenByHydra) return false;
            if (m_health <= 0.0f) return false;
            return true;
        }

        public void startDrag(CTouchElement touch)
        {
            PlayUnitSound("Pickup");
            m_touch = touch;
            setState(US_Dragged);
            int tx, ty;
            m_touch.GetTouchPos_Current(out tx, out ty);
            m_dragOffset = getScreenPosition() - new p2i(tx, ty);
            m_dragOffset -= new p2i(0, 2);
        }

        public p2i getScreenPosition()
        {
            return m_SceneManager.getScreenCoordinatesFrom3DPosition(getAbsolutePosition() + new v3f(0, 1, 0));
        }

        public void setPath(line3df path)
        {
            m_path = path;
            m_flyingPlane = new plane3df(m_path.start, m_path.end, m_path.end + new v3f(0, 1, 0));
        }

        public void start()
        {
            m_health = m_statHealth;
            quaternion rot = new quaternion().rotationFromTo(new v3f(0, 0, -1), m_path.getVector());
            m_Node.setRotation(rot * m_modelRotation);
            m_startRotation = m_Node.getRotation();
            m_position = m_path.start;
            setPosition(m_position);
            setVisible(true);
            setState(US_Walking);
            if (m_unitKind == UK_Elf)
            {
                throwUnitOnForTime(m_path.end - m_path.getVector().setLength(m_statRange), RandFloat(0.8f, 1.0f));
                m_harmlessFall = true;
                setAnimation(UA_ElfJump);
            }
            if (m_unitKind == UK_Skeleton)
            {
                setState(US_SkeletonAppear);
                PlayUnitSound("Appear");
            }
        }

        public override void update(float time)
        {
            m_stateTime += time;
            m_shamanStaff.Update(time);
            m_stunnedStars.Update(time);
            CPlayfield playfield = CPlayfield.GetInstance();
            if (playfield.getGate().isDestroyed() && (m_state == US_Walking || m_state == US_Attacking)) setState(US_Victory);
            const float dustInterval = 0.8f;
            if (m_shamanAura != null) m_shamanAura.update(time);
            switch (m_state)
            {
                case US_Victory:
                case US_Walking:
                {
                    if (m_isIntroUnit && (m_position - m_path.end).getLength() < 10.0f) showIntroUnitInfo();
                    m_dustTime += time;
                    if (m_dustTime > dustInterval)
                    {
                        m_dustTime -= dustInterval;
                        playfield.addWalkDust(getAbsolutePosition());
                    }
                    float speed = m_statSpeed;
                    if (m_charge) speed *= m_statCharge;
                    if (playfield.getIceball().isGroundSlippery(m_position))
                    {
                        if (canBeSlowedByIceball()) speed *= 0.33f;
                        if (m_unitKind == UK_Elf && m_currentAnim != UA_ElfSlide) setAnimation(UA_ElfSlide, true);
                    }
                    else if (m_unitKind == UK_Elf && m_currentAnim == UA_ElfSlide) setAnimation(UA_Walk_Normal, true);
                    m_velocity = m_path.getVector().normalize() * speed;
                    m_position += m_velocity * time;
                    setPosition(m_position);
                    if (m_state == US_Victory) break;
                    float distanceToFortress = m_path.getVector().getLength() - (m_position - m_path.start).getLength();
                    if (distanceToFortress > 2.5f && distanceToFortress < 11) m_chargeMinWalk -= speed * time;
                    if (distanceToFortress <= m_statRange)
                    {
                        if (distanceToFortress < 0.0f) m_position = m_path.end;
                        setPosition(m_position);
                        onFortressReached();
                    }
                    if (!m_charge && m_statCharge > 0.0f && m_health >= m_statChargeHealth && m_chargeMinWalk <= 0.0f && m_rider == null &&
                        (m_brokenLink || m_mindControlMaster == null))
                    {
                        m_charge = true;
                        setAnimation(UA_Walk_Charge, true);
                    }
                    break;
                }
                case US_SkeletonAppear:
                case US_SkeletonLand:
                    if (isAnimationOver()) setState(US_Walking);
                    break;
                case US_ElfWallGrab:
                    if (isAnimationOver() && m_currentAnim == UA_ElfWallGrab)
                        setPosition(m_position -= m_path.getVector().setLength(1.0f));
                    if (isAnimationOver() || (m_touch != null && m_touch.IsNowReleased() && m_currentAnim != UA_ElfWallGrab))
                    {
                        setState(US_ElfWallFall);
                        PlayUnitSound("PickUpGetAway");
                    }
                    break;
                case US_TurningAroundToRunAway:
                    if (isAnimationOver())
                    {
                        m_isAnimationOver = false;
                        if (m_unitKind == UK_Bomblin) setState(US_RunningAway);
                        else
                        {
                            setState(US_RunningAwayThrowingStuffAround);
                            m_position -= m_path.getVector().setLength(1.6f);
                        }
                        setPosition(m_position);
                    }
                    break;
                case US_RunningAway:
                {
                    v3f dir = m_path.getVector().normalize();
                    m_velocity = dir * (-2.0f * m_statSpeed);
                    m_position += m_velocity * time;
                    setPosition(m_position);
                    if (dir.dotProduct(m_position - m_path.start) <= 0.0f) setState(US_Dead);
                    break;
                }
                case US_RunningAwayThrowingStuffAround:
                {
                    m_dustTime += 3.0f * time;
                    if (m_dustTime > dustInterval)
                    {
                        m_dustTime -= dustInterval;
                        playfield.addWalkDust(getAbsolutePosition());
                    }
                    v3f dir = m_path.getVector().normalize();
                    m_velocity = dir * (-3.0f * m_statSpeed);
                    if ((m_position - m_path.end).getLength() < 11.5f)
                    {
                        foreach (CUnit unit in new List<CUnit>(playfield.getUnits()))
                        {
                            if (unit.canBeThrownByFreedBoar())
                            {
                                v3f unitPos = unit.getAbsolutePosition();
                                float dist = -dir.dotProduct(unitPos - m_position);
                                if (1.0f <= dist && dist <= 1.0f + 2.0f * m_velocity.getLength() * time)
                                {
                                    unit.damageUnit(1, UDS_Fireball);
                                    unit.throwUnit(new v3f(0, RandFloat(25, 30), 0) - unit.getPath().getVector().setLength(10.0f), false);
                                    ++m_unitsHit;
                                    ++m_unitsHitByKind[unit.getUnitKind()];
                                    int bonus = m_unitsHit * 2;
                                    string text = m_unitsHit == 5 ? _TXT(STRING_IDS.INGAME_GREAT) : null;
                                    playfield.addMoney(10, bonus, unit.getAbsolutePosition(), text);
                                    playfield.getChallenge().unitWasHitByBoar(m_unitsHitByKind);
                                    PlayUnitSound("BoarHit");
                                }
                            }
                            else if (unit.m_state == US_Walking || unit.m_state == US_Stunned || unit.m_state == US_Attacking)
                            {
                                v3f unitPos = unit.getAbsolutePosition();
                                float dist = -dir.dotProduct(unitPos - m_position);
                                if (1.0f <= dist && dist <= 1.0f + 2.0f * m_velocity.getLength() * time)
                                {
                                    if (m_unitKind == UK_Elf) elfJump(0.8f);
                                    else unit.throwUnit(new v3f(0, RandFloat(18, 19), 0) - unit.getPath().getVector().setLength(2.0f), true);
                                }
                            }
                        }
                    }
                    m_position += m_velocity * time;
                    setPosition(m_position);
                    if (dir.dotProduct(m_position - m_path.start) <= 0.0f)
                    {
                        CAchievements.GetInstance().addValue("Riders on the storm", 1);
                        setState(US_Dead);
                    }
                    break;
                }
                case US_Dragged: updateDragged(time, playfield); break;
                case US_Stumble:
                    if (isAnimationOver())
                    {
                        m_position += m_path.getVector().normalize() * m_statStumbleDistance;
                        setPosition(m_position);
                        setState(US_Walking);
                    }
                    break;
                case US_FallingOnHydra:
                    m_velocity += m_acceleration * time;
                    m_position += m_velocity * time;
                    setPosition(m_position);
                    if (m_position.Y < hydraFood.Y && m_velocity.Y < 0.0f)
                    {
                        m_position.Y = hydraFood.Y;
                        setState(US_BeingEatenByHydra);
                    }
                    break;
                case US_BeingEatenByHydra:
                    if (m_shamanWave.checkEvent("anim_end")) m_shamanWave.getRoot().setVisible(false);
                    if (!m_blood) { m_blood = true; bleed(0.016f); }
                    break;
                case US_FallingInWater:
                {
                    m_velocity += m_acceleration * time;
                    m_position += m_velocity * time;
                    quaternion q = new quaternion(m_rotationVelocity * time);
                    m_Node.setRotation(q * m_Node.getRotation());
                    if (playfield.getGroundPlane().classifyPointRelation(m_position + new v3f(0, 2, 0)) == EIntersectionRelation3D.ISREL3D_BACK)
                    {
                        playfield.getGroundPlane().getIntersectionWithLine(m_position, new v3f(0, 1, 0), ref m_position);
                        ParticleSystem ps = ParticleManager.AddParticleSystem("water_impact_new");
                        ps.SetPosition(m_position - new v3f(0, 2, 0));
                        ps.SetScale(0.05f);
                        ps.SetRotation(new quaternion(0.5f * PI, 0.0f, 0.0f));
                        ps.Restart();
                        PlayUnitSound("FallInWater");
                        setState(US_Dead);
                    }
                    setPosition(m_position);
                    break;
                }
                case US_Falling: updateFalling(time, playfield); break;
                case US_Stunned:
                    if (isAnimationOver()) { m_isAnimationOver = false; setState(US_Walking); }
                    break;
                case US_Petrified:
                    if (m_stateTime > m_freezeDuration) unpetrify(false);
                    break;
                case US_MindControlling:
                    if (m_mindControlSlaves.Count == 0) setState(US_Walking);
                    break;
                case US_Attacking:
                    if (m_projectile != null && m_unitKind == UK_Shaman)
                    {
                        m_projectile.setPosition(m_energyBallDummy.getAbsolutePosition() + new v3f(0, 0.3f, 0) + m_path.getVector().setLength(0.4f));
                        if (m_projectile.m_scale < 2.0f)
                        {
                            m_projectile.m_scale += 4.0f * time;
                            if (m_projectile.m_scale > 2.0f) m_projectile.m_scale = 2.0f;
                        }
                    }
                    if (isAnimationOver())
                    {
                        m_isAnimationOver = false;
                        if (m_unitKind == UK_Shaman) shamanShoot();
                        if (m_unitKind == UK_Elf)
                        {
                            if (RandFloat(0, 100) < 35) elfJump(RandFloat(0.8f, 0.9f));
                            else elfShoot();
                        }
                    }
                    break;
                case US_Dying: updateDying(playfield); break;
                case US_ToBeEeatenByHydra:
                    if (m_shamanWave.checkEvent("anim_end")) m_shamanWave.getRoot().setVisible(false);
                    break;
            }
            if ((!m_Node.isVisible() || m_Node.getParent() == null || !m_Node.getParent().isVisible()) && m_stateTime > 15.0f)
            {
                Logs.warning("!!!! This should not happen !!!!");
                setState(US_Dead);
            }
            if (m_shadow != null) UpdateShadow(time);
            m_shamanWave.updateAtEndOfFrame(time);
        }

        void updateDragged(float time, CPlayfield playfield)
        {
            bool dragged = playfield.isActiveTouch();
            bool collided = false;
            int dtx, dty;
            m_touch.GetTouchPos_DT(out dtx, out dty);
            float d = playfield.getGroundPlane().getDistanceTo(m_position);
            float distToCastle = playfield.getCastlePlane().getDistanceTo(m_position);
            if (dragged || System.Math.Abs(dty) > 20)
            {
                m_maxY = MAX(d, m_maxY);
                if (dragged && d > 0.7 && m_stateTime > 0.2f)
                {
                    p2i pos = getScreenPosition() - screenFrom3DPos(m_dragDummy.getAbsolutePosition());
                    v2f v1 = new v2f(m_dragOffset.X, m_dragOffset.Y);
                    v2f v2 = new v2f(pos.X, pos.Y);
                    v1 = v1 + (v2 - v1) * MIN((d - 0.7f) / 4, 0.3f);
                    m_dragOffset.X = (int)v1.X;
                    m_dragOffset.Y = (int)v1.Y;
                }
                int tx, ty;
                m_touch.GetTouchPos_Current(out tx, out ty);
                p2i touch = new p2i(tx, ty) + m_dragOffset;
                line3df ray = m_SceneManager.getRayFromScreenCoordinates(touch);
                v3f oldPos = m_position;
                m_flyingPlane.getIntersectionWithLine(ray.start, ray.getVector(), ref m_position);
                m_position -= new v3f(0, 0.9f, 0);
                m_velocity = (m_position - oldPos) / time;
                plane3df ground = playfield.getGroundPlane(), castle = playfield.getCastlePlane();
                bool groundTest = ground.classifyPointRelation(m_position) == EIntersectionRelation3D.ISREL3D_BACK;
                bool castleTest = castle.classifyPointRelation(m_position) == EIntersectionRelation3D.ISREL3D_BACK;
                int groundPass = -1, castlePass = -1;
                if (groundTest && castleTest)
                {
                    v3f groundPoint = v3f.Zero, castlePoint = v3f.Zero;
                    ground.getIntersectionWithLine(m_position, m_velocity, ref groundPoint);
                    castle.getIntersectionWithLine(m_position, m_velocity, ref castlePoint);
                    groundPass = castle.classifyPointRelation(groundPoint) == EIntersectionRelation3D.ISREL3D_FRONT ? 1 : 0;
                    castlePass = 1 - groundPass;
                }
                else if (groundTest) groundPass = 0;
                else if (castleTest) castlePass = 0;
                for (int i = 0; i < 2; i++)
                {
                    if (i == groundPass)
                    {
                        ground.getIntersectionWithLine(m_position, i == 0 ? m_velocity : new v3f(0, 1, 0), ref m_position);
                        setPosition(m_position);
                        float speed = -m_velocity.Y;
                        if (!collided)
                        {
                            int neededY = (m_unitKind == UK_Elf || m_unitKind == UK_Skeleton) ? 3 : 1;
                            onGroundHit(m_maxY > neededY ? speed : 0.0f, true, m_maxY > 0.0f);
                            collided = true;
                        }
                    }
                    if (i == castlePass)
                    {
                        if (castle.classifyPointRelation(oldPos) != EIntersectionRelation3D.ISREL3D_FRONT)
                            castle.getIntersectionWithLine(m_position, m_path.getVector(), ref m_position);
                        else
                        {
                            castle.getIntersectionWithLine(m_position, i == 0 ? m_velocity : m_path.getVector(), ref m_position);
                            setPosition(m_position);
                            float speed = m_velocity.X;
                            if (!collided && speed >= m_statFallDie)
                            {
                                onWallHit(speed, true);
                                collided = true;
                            }
                        }
                    }
                }
                setPosition(m_position);
                for (int i = 0; i < m_mindControlSlaves.Count; ++i) ropePhysics(m_mindControlSlaves[i]);
                if (m_mindControlMaster != null) ropePhysics(m_mindControlMaster);
                const float timetime = 1.5f;
                if (m_stateTime >= timetime && m_mindControlSlaves.Count > 0)
                    for (int i = 0; i < m_mindControlSlaves.Count; ++i) m_mindControlSlaves[i].m_brokenLink = true;
                if (m_stateTime >= timetime && m_mindControlMaster != null)
                    for (int i = 0; i < m_mindControlMaster.m_mindControlSlaves.Count; ++i) m_mindControlMaster.m_mindControlSlaves[i].m_brokenLink = true;
            }
            if (!dragged && !collided)
            {
                const float MAX_SPEED = 23.0f;
                m_velocity.Y *= 2;
                if (m_velocity.getLength() > MAX_SPEED) m_velocity.setLength(MAX_SPEED);
                if (m_stateTime < 0.2f && m_velocity.getLength() < 5.0f && d < 1.0f && distToCastle > m_statStumbleDistance)
                {
                    if (m_statStumbleDistance > 0.0f)
                    {
                        playfield.getGroundPlane().getIntersectionWithLine(m_position, new v3f(0, 1, 0), ref m_position);
                        setPosition(m_position);
                        setState(US_Stumble);
                    }
                    else if (m_unitKind == UK_Elf) elfJump(1.0f);
                }
                else
                {
                    float topTime = -m_velocity.Y / m_gravity;
                    m_rotationVelocity = new v3f(0, 0, PI / topTime);
                    setState(US_Falling);
                }
            }
            if (dragged) playfield.checkHydraEatUnitFromHand(time, this);
            if (dragged && m_stateTime > 0.8f && m_unitKind == UK_Elf)
            {
                float dist = playfield.getGroundPlane().getDistanceTo(m_position);
                setState(US_ElfWallGrab);
                setAnimation(dist > 3.0f ? UA_ElfUnhangHigh : UA_ElfUnhangLow);
            }
        }

        void updateFalling(float time, CPlayfield playfield)
        {
            float d = playfield.getGroundPlane().getDistanceTo(m_position);
            m_maxY = MAX(d, m_maxY);
            m_acceleration = new v3f(0, m_gravity, 0);
            if (m_currentAnim == UA_Die_WallHit && !m_blood) { m_blood = true; bleed(0.016f); }
            if (m_currentAnim == UA_Flying && m_velocity.Y < 0.0f)
            {
                setAnimation(UA_FlyingToFalling, true);
                if (m_unitKind == UK_Shaman && m_lastDamageSource == UDS_Fireball) PlayUnitSound("AlternativeFalling");
                else if (m_unitKind != UK_Shaman || playfield.getGroundPlane().getDistanceTo(m_position) > 2.0f) PlayUnitSound("Falling");
            }
            if (m_currentAnim == UA_FlyingToFalling && isAnimationOver()) setAnimation(UA_Falling, true);
            if (m_shamanWave.checkEvent("anim_end")) m_shamanWave.getRoot().setVisible(false);
            if (m_currentAnim == UA_Die_WallHit && m_stateTime < 2.0f) m_acceleration = m_acceleration * m_stateTime / 2.0f;
            m_velocity += m_acceleration * time;
            float maxFallSpeedY = (m_mindControlMaster != null && !m_brokenLink) ? m_mindControlMaster.m_maxFallSpeedY : m_maxFallSpeedY;
            if (m_unitKind == UK_Skeleton && m_health > 0.0f)
            {
                CUnit shaman = playfield.findShaman();
                if (shaman != null) maxFallSpeedY = shaman.m_maxFallSpeedY;
            }
            if (m_health == 0.0f) maxFallSpeedY = 9999.0f;
            if (m_velocity.Y < -maxFallSpeedY)
            {
                m_velocity *= -maxFallSpeedY / m_velocity.Y;
                if (m_shamanAura != null) m_shamanAura.show();
            }
            m_position += m_velocity * time;
            plane3df castle = playfield.getCastlePlane();
            if (castle.classifyPointRelation(m_position) == EIntersectionRelation3D.ISREL3D_BACK)
            {
                castle.getIntersectionWithLine(m_position, m_path.getVector(), ref m_position);
                setPosition(m_position);
                float speed = m_velocity.dotProduct(-castle.Normal);
                if (speed >= m_statFallDie) onWallHit(speed, true);
                else
                {
                    m_velocity.rotateXZBy(180);
                    m_velocity.X *= 0.5f;
                    m_velocity.Z *= 0.5f;
                }
            }
            v3f offset = m_health > 2.0f && !m_ropePhysicsApplied ? new v3f(0, m_statDistanceBeforeFallAnim, 0) : v3f.Zero;
            m_ropePhysicsApplied = false;
            plane3df ground = playfield.getGroundPlane();
            if (m_velocity.Y < 0.0f && ground.classifyPointRelation(m_position - offset) == EIntersectionRelation3D.ISREL3D_BACK)
            {
                ground.getIntersectionWithLine(m_position, new v3f(0, 1, 0), ref m_position);
                float speed = -m_velocity.Y;
                setPosition(m_position);
                m_Node.updateAbsolutePosition();
                if (m_statNoDeathFromFastFall && speed >= m_statFallDie) speed = m_statFallStun;
                onGroundHit(speed, false, m_maxY > 1.0f);
            }
            setPosition(m_position);
            for (int i = 0; i < m_mindControlSlaves.Count; ++i)
                if (m_mindControlSlaves[i].getState() != US_Dragged) ropePhysics(m_mindControlSlaves[i]);
            if (!m_ropePhysicsApplied && m_mindControlMaster != null) m_mindControlMaster.ropePhysics(this);
        }

        void updateDying(CPlayfield playfield)
        {
            if (!m_blood && (m_lastDamageSource == UDS_GroundFall || m_lastDamageSource == UDS_GriffinSmash || m_lastDamageSource == UDS_GroundDrag))
            {
                m_blood = true;
                bleed(m_lastDamageSource == UDS_GroundFall ? 0.02f : 0.04f);
            }
            if (m_shamanWave.checkEvent("anim_end")) m_shamanWave.getRoot().setVisible(false);
            if (m_rider != null && m_lastDamageSource != UDS_WallHit && m_lastDamageSource != UDS_Fireball && m_lastDamageSource != UDS_Petrify)
            {
                m_rider.damageUnit(m_rider.m_statHealth, m_lastDamageSource);
                if (m_rider != null)
                {
                    m_rider.setState(US_Falling);
                    m_rider.m_velocity = m_velocity;
                }
            }
            if (isAnimationOver())
            {
                m_isAnimationOver = false;
                m_dieStartScaleGo = true;
                m_stateTime = 0.0f;
                m_dieStartScaleScale = m_Node.getScale();
                m_dieStartScalePos = m_Node.getAbsolutePosition();
                m_dieStartScaleChestPos = m_chestDummy.getAbsolutePosition();
                if (m_unitKind != UK_Skeleton) playfield.addDie(m_chestDummy.getAbsolutePosition());
                if (m_unitKind == UK_Shaman) shamanExplode();
            }
            if (!m_dieStartScaleGo && m_statMustDragIntoGroundToKill && m_currentAnim == UA_Die_GroundSmash)
            {
                float scale = 1.0f - m_stateTime / (m_Node.getFirstAnimator().getTimelineCtrl().getCurrentClipLength() / 1000.0f);
                float scaleY = scale * scale * scale;
                float scaleXZ = 1.5f - 0.5f * scaleY;
                m_Node.setScale(new v3f(m_dieStartScaleScale.X * scaleXZ, m_dieStartScaleScale.Y * scaleY, m_dieStartScaleScale.Z * scaleXZ));
            }
            if (m_dieStartScaleGo)
            {
                float scale = 1.0f - m_stateTime / 0.9f;
                scale = scale * scale * scale;
                m_Node.setScale(m_dieStartScaleScale * scale);
                m_Node.setPosition(m_dieStartScaleChestPos + (m_dieStartScalePos - m_dieStartScaleChestPos) * scale);
                if (m_stateTime > 0.9f) setState(US_Dead);
            }
        }

        public void ropePhysics(CUnit slave)
        {
            if (m_brokenLink || slave.m_brokenLink || isPetrified() || slave.isPetrified()) return;
            slave.m_ropePhysicsApplied = true;
            const float maxDistance = 4.0f;
            plane3df slaveFlyingPlane = new plane3df(slave.getPath().start, slave.getPath().end, slave.getPath().end + new v3f(0, 1, 0));
            slave.m_Node.updateAbsolutePosition();
            v3f slavePos = slave.getAbsolutePosition();
            v3f myPos = m_position - slaveFlyingPlane.Normal * slaveFlyingPlane.getDistanceTo(m_position);
            v3f dir = slavePos - myPos;
            float distance = dir.getLength();
            float extraDistance = slave.getState() != US_Falling ? 1.5f : 0.0f;
            if (distance > maxDistance + extraDistance)
            {
                if (slave.getState() != US_Falling) slave.setState(US_Falling);
                dir.normalize();
                v3f target = myPos + dir * maxDistance;
                v3f toMove = (target - slave.m_position) * 0.2f;
                slave.setPosition(slave.m_position += toMove);
                slave.m_Node.updateAbsolutePosition();
                v3f slaveVel = slave.m_velocity;
                v3f perpDir = new quaternion().fromAngleAxis(0.5f * PI, slaveFlyingPlane.Normal) * dir;
                slave.m_velocity = perpDir * (RandFloat(0.92f, 1.08f) * perpDir.dotProduct(slaveVel));
            }
        }

        public void throwUnit(v3f velocity, bool harmless)
        {
            m_harmlessFall = harmless;
            m_velocity = velocity;
            setState(US_Falling);
            if (!harmless && m_rider != null) m_rider.throwUnit(velocity * 1.3f, false);
        }

        public void elfJump(float power)
        {
            v3f n = m_path.getVector().normalize();
            if ((getAbsolutePosition() - m_path.end).getLength() > 3.0f && RandFloat(0, 1) < 0.5f)
                throwUnit(n * (power * 8.0f) + new v3f(0, power * 25, 0), true);
            else
                throwUnit(n * (power * -8.0f) + new v3f(0, power * 25, 0), true);
            setAnimation(UA_ElfJump);
        }

        public float damageUnit(float damage, int source)
        {
            if (m_state == US_Dying || m_state == US_Dead) return 0.0f;
            float unitDamage = MIN(damage, m_health);
            if (source == UDS_GroundFall && m_statMustDragIntoGroundToKill) unitDamage = 0.0f;
            if (unitDamage > 0.0f)
            {
                m_lastDamageSource = source;
                m_health -= unitDamage;
                if (m_health <= 0.0f) setState(US_Dying);
            }
            return unitDamage;
        }

        protected virtual void onGroundHit(float speed, bool dragged, bool fallfromhigh)
        {
            int src = dragged ? UDS_GroundDrag : UDS_GroundFall;
            bool elfFallOffWall = m_currentAnim == UA_ElfWallFall;
            bool wasDamaged = false;
            if (speed >= m_statFallStun && !m_harmlessFall)
            {
                if (m_unitKind != UK_Elf || dragged)
                {
                    damageUnit(speed >= m_statFallDie ? m_health : m_statHealth * 0.5f, src);
                    wasDamaged = true;
                }
                if (m_unitKind != UK_Shaman) CPlayfield.GetInstance().addFallDust(getPosition());
            }
            bool trollShouldSmash = false, boarShouldStun = false, boarShouldSmash = false;
            if (m_health > 0.0f)
            {
                if (m_unitKind != UK_Shaman || fallfromhigh) PlayUnitSound("GetAway");
                if ((!dragged && m_statMustDragIntoGroundToKill) || m_harmlessFall)
                {
                    trollShouldSmash = true;
                    boarShouldStun = true;
                    setState(US_Stunned);
                }
                else if (!wasDamaged) setState(m_unitKind == UK_Skeleton ? US_SkeletonLand : US_Walking);
                else
                {
                    setState(US_Stunned);
                    trollShouldSmash = true;
                    boarShouldStun = true;
                }
                if (m_unitKind == UK_Elf && !dragged)
                {
                    if (elfFallOffWall) { setState(US_Stunned); setAnimation(UA_ElfWallLand); }
                    else if (speed >= m_statFallDie) { setState(US_Stunned); setAnimation(UA_Stunned); }
                    else if (speed >= m_statFallStun) { setState(US_Stunned); setAnimation(UA_Stumble); }
                }
            }
            else
            {
                trollShouldSmash = true;
                boarShouldStun = true;
                boarShouldSmash = dragged;
                PlayUnitSound(src == UDS_GroundDrag ? "SmashGround" : "DieFall");
                setState(US_Dying);
            }
            if (m_unitKind == UK_Troll && trollShouldSmash) trollStun(true);
            if (m_unitKind == UK_Boar && (boarShouldStun || boarShouldSmash)) boarStun(boarShouldSmash);
            m_harmlessFall = false;
            m_catchCounter = 0;
        }

        void genericUnitSmash(int maxUnitsToKill, float range, bool shake, int source)
        {
            int unitsKilled = 0;
            v3f center = m_chestDummy.getAbsolutePosition();
            List<CUnit> unitsToKill = new List<CUnit>();
            foreach (CUnit unit in new List<CUnit>(CPlayfield.GetInstance().getUnits()))
            {
                v3f dist = center - unit.getAbsolutePosition();
                dist.Y = 0;
                if (unit != this && dist.getLength() < range)
                {
                    if (unit.isPetrified()) unit.unpetrify(true);
                    else if (unitsKilled < maxUnitsToKill && unit.canBeSmashedBy(source)) { unitsToKill.Add(unit); ++unitsKilled; }
                    else if (unit.canBeStunnedBy(source)) unit.setState(US_Stunned);
                }
            }
            foreach (CUnit u in unitsToKill)
            {
                u.damageUnit(9999, source);
                if (source == UDS_TrollSmash && u.getUnitKind() == UK_Goblin) CAchievements.GetInstance().addValue("Burden of the Troll", 1);
            }
            if (shake) CPlayfield.GetInstance().SetCameraShake(0.2f, 0.4f);
        }

        public void trollStun(bool canKill = false) { genericUnitSmash(canKill ? 3 : 0, 1.5f, true, UDS_TrollSmash); }
        public void boarStun(bool canKill = false) { genericUnitSmash(canKill ? 2 : 0, 1.5f, false, UDS_BoarSmash); }

        protected virtual void onWallHit(float speed, bool dragged)
        {
            PlayUnitSound("SmashCastle");
            if (speed >= m_statFallStun && !m_harmlessFall)
            {
                if (m_unitKind != UK_Elf) damageUnit(speed >= m_statFallDie ? m_health : m_statHealth * 0.5f, UDS_WallHit);
                if (m_unitKind != UK_Shaman) CPlayfield.GetInstance().addFallDust(m_position + new v3f(0, 1, 0));
            }
            if (m_health == 0.0f && m_rider != null)
            {
                CUnit rider = m_rider;
                rider.damageUnit(m_rider.m_statHealth, UDS_WallHit);
                rider.setState(US_Falling);
                rider.m_velocity = v3f.Zero;
            }
            if (m_unitKind == UK_Elf) setState(US_ElfWallGrab);
            else
            {
                setState(US_Falling);
                m_velocity = v3f.Zero;
                m_harmlessFall = false;
            }
        }

        protected virtual void onFortressReached() { setState(m_mindControlSlaves.Count == 0 ? US_Attacking : US_MindControlling); }

        bool shouldShowStars()
        {
            return (m_state == US_Stunned && m_unitKind != UK_Elf && m_unitKind != UK_Shaman) || m_state == US_Earthquake;
        }

        void drawLightning(v3f start, v3f origin, bool falling)
        {
            SColor color = new SColor(falling ? 0xff105050 : 0xffd01010);
            float shake = falling ? 0.55f : 0.15f;
            float thickness = falling ? 4.0f : 3.0f;
            v3f prevPoint = start;
            for (int j = 1; j < 5; ++j)
            {
                v3f curPoint = start + (origin - start) * (j / 5.0f) + randomVectorInSphere(shake);
                Gfx.draw3DLine(prevPoint, curPoint, color, E_MATERIAL_TYPE.EMT_TRANSPARENT_ADD_COLOR, thickness);
                prevPoint = curPoint;
            }
            Gfx.draw3DLine(prevPoint, origin, color, E_MATERIAL_TYPE.EMT_TRANSPARENT_ADD_COLOR, thickness);
        }

        public override void draw()
        {
            if (shouldShowStars())
                m_stunnedStars.Draw3D(m_headDummy.getAbsolutePosition() + (m_unitKind == UK_Boar ? new v3f(0.3f, 1.0f, 0.0f) : new v3f(0.0f, 0.5f, 0.0f)), 0.8f);
            Gfx.setTransform(E_TRANSFORMATION_STATE.ETS_WORLD, matrix4.Identity);
            if (m_unitKind == UK_Skeleton && m_state == US_Falling && m_velocity.Y < 0.0f && m_health > 0.0f)
            {
                CUnit shaman = CPlayfield.GetInstance().findShaman();
                if (shaman != null) drawLightning(shaman.m_shamanStaffDummy.getAbsolutePosition(), getAbsolutePosition() + new v3f(0, 1.0f, 0), true);
            }
            if (m_mindControlSlaves.Count > 0)
            {
                v3f start = m_shamanStaffDummy.getAbsolutePosition();
                bool hadFalling = false;
                foreach (CUnit slave in m_mindControlSlaves)
                {
                    if (slave.m_brokenLink || slave.isPetrified()) continue;
                    bool falling = slave.getState() == US_Falling && slave.m_velocity.Y < 0.0f;
                    hadFalling = hadFalling || falling;
                    drawLightning(start, slave.getAbsolutePosition() + new v3f(0, 1.0f, 0), falling);
                }
                m_shamanStaff.Draw3D(start, hadFalling ? 3.0f : 2.0f);
            }
            if (m_shamanAura != null) m_shamanAura.draw();
            if (m_state == US_Petrified && m_prerenderedFrame != -1)
            {
                ASprite sprite = m_stoned ? CPlayfield.GetInstance().getSpriteStonedUnits() : CPlayfield.GetInstance().getSpriteIcedUnits();
                const float scale = ASprite.SPRITE_SCALE_3D * 1.15f;
                sprite.PaintFrame3D(m_prerenderedFrame, getPosition(), 0, 0, 0, scale, scale);
            }
        }

        public virtual void UpdateShadow(float dt)
        {
            if (m_shadow == null) return;
            CPlayfield playfield = CPlayfield.GetInstance();
            v3f shadowPos = v3f.Zero;
            v3f pos = m_bip01.getAbsolutePosition();
            playfield.getGroundPlane().getIntersectionWithLine(pos, new v3f(0, 1, 0), ref shadowPos);
            m_shadow.setPosition(shadowPos);
            float distanceFromGround = playfield.getGroundPlane().getDistanceTo(m_position);
            float scale = CLAMP(1.5f - 0.3f * distanceFromGround, 0.4f, 1.5f);
            if (m_state == US_Dying) scale = CLAMP(1.0f - 3.0f * m_stateTime, 0.0f, 1.0f);
            m_shadow.setScale(new v3f(scale, scale, scale));
        }

        public void throwOnHydra()
        {
            v3f pos = getAbsolutePosition();
            __IRR_SCENE().getRootSceneNode().addChild(m_Node);
            m_Node.setRotation(new quaternion().rotationFromTo(new v3f(0, 0, -1), m_path.getVector()));
            m_position = pos;
            setPosition(m_position);
            m_acceleration = new v3f(0, m_gravity, 0);
            m_velocity = new v3f(0, 25, 0);
            float d = m_velocity.Y * m_velocity.Y - 2.0f * m_acceleration.Y * (pos.Y - m_path.start.Y);
            if (d >= 0)
            {
                float d2 = m_velocity.Y * m_velocity.Y - 2.0f * m_acceleration.Y * (pos.Y - hydraFood.Y);
                if (d2 >= 0)
                {
                    float timeToFallOnHydra = (1 / m_acceleration.Y) * (-m_velocity.Y - sqrtf(d2));
                    v3f toHydraVel = (hydraFood - pos) / timeToFallOnHydra;
                    m_velocity.X = toHydraVel.X;
                    m_velocity.Z = toHydraVel.Z;
                    setState(US_FallingOnHydra);
                }
            }
        }

        public void throwUnitOnForTime(v3f pos, float timeToFly)
        {
            m_harmlessFall = true;
            m_velocity = (pos - m_position) / timeToFly - m_acceleration * (0.5f * timeToFly);
            setState(US_Falling);
            m_harmlessFall = false;
        }

        public void throwUnitInWater(v3f position, float time = 0.75f)
        {
            throwUnitOnForTime(position, time);
            setState(US_FallingInWater);
            m_rotationVelocity = new v3f(RandFloat(0, 4.0f * PI), RandFloat(0, 4.0f * PI), RandFloat(0, 4.0f * PI));
        }

        public void bleed(float scale)
        {
            if (m_unitKind == UK_Shaman || m_unitKind == UK_Skeleton) return;
            ParticleSystem ps = ParticleManager.AddParticleSystem("bloody_blood2");
            ps.SetScale(scale);
            ps.SetRotation(new quaternion(0.5f * PI, 0.0f, 0.0f));
            ps.SetPosition(v3f.Zero);
            m_chestDummy.addChild(ps.getNode());
        }

        public void shamanExplode()
        {
            ParticleSystem ps = ParticleManager.AddParticleSystem("Shaman_death");
            ps.SetScale(0.004f);
            ps.SetPosition(v3f.Zero);
            m_chestDummy.addChild(ps.getNode());
            m_shamanWave.getRoot().setPosition(getAbsolutePosition());
            m_shamanWave.getRoot().setVisible(true);
            m_shamanWave.setAnim(0, false);
            foreach (CUnit unit in new List<CUnit>(CPlayfield.GetInstance().getUnits()))
            {
                if (!unit.canBeStunnedByShaman()) continue;
                v3f myPos = getAbsolutePosition(), unitPos = unit.getAbsolutePosition();
                myPos.Y = 0.0f;
                unitPos.Y = 0.0f;
                if ((myPos - unitPos).getLength() < 3.0f) unit.setState(US_Stunned);
            }
        }

        public v3f getFallPosition(v3f velocity)
        {
            float d = velocity.Y * velocity.Y - 2.0f * m_acceleration.Y * (m_position.Y - m_path.start.Y);
            float t = (1 / m_acceleration.Y) * (-velocity.Y - sqrtf(d));
            return (m_acceleration * (0.5f * t) + velocity) * t + m_position;
        }

        public v3f projectVectorToFlyingPlane(v3f vec)
        {
            plane3df flyingPlane = new plane3df(m_path.start, m_path.end, m_path.end + new v3f(0, 1, 0));
            v3f proj = v3f.Zero;
            flyingPlane.getIntersectionWithLine(m_path.start + vec, flyingPlane.Normal, ref proj);
            return proj - m_path.start;
        }

        public bool isPositionOnPath(v3f pos, float tolerance = 1.0f)
        {
            v3f posProjOnPath = m_path.start + projectVectorToFlyingPlane(pos - m_path.start);
            return (posProjOnPath - pos).getLength() < tolerance;
        }

        public bool canBeSmashedBy(int source)
        {
            if (source == UDS_TrollSmash)
            {
                if (m_unitKind == UK_Boar && m_rider == null) return true;
                return (m_unitKind == UK_Goblin || m_unitKind == UK_BoarRider || m_unitKind == UK_Shaman) &&
                    (m_state == US_Walking || m_state == US_Stunned || m_state == US_Attacking || m_state == US_Riding || m_state == US_MindControlling);
            }
            if (source == UDS_BoarSmash)
                return (m_unitKind == UK_Goblin || m_unitKind == UK_BoarRider) && (m_state == US_Walking || m_state == US_Stunned || m_state == US_Attacking);
            return false;
        }

        public bool canBeStunnedBy(int source)
        {
            return m_unitKind != UK_Shaman && m_unitKind != UK_Troll && (m_state == US_Walking || m_state == US_Attacking);
        }

        public bool canBeFrozenByIceball()
        {
            if (m_health <= 0.0f || m_statIceImmunity || m_isIntroUnit) return false;
            if (m_mount != null && m_mount.m_isIntroUnit) return false;
            return m_state == US_Walking || m_state == US_Stunned || m_state == US_Attacking || m_state == US_MindControlling;
        }

        public bool canBeDamagedByFireball()
        {
            if (m_statFireImmunity || m_isIntroUnit) return false;
            if (m_mount != null && m_mount.m_isIntroUnit) return false;
            return m_state == US_Walking || m_state == US_Stunned || m_state == US_Attacking || m_state == US_MindControlling ||
                m_state == US_TurningAroundToRunAway || m_state == US_RunningAway;
        }

        public bool canBeDamagedByShamanBall()
        {
            return (m_state == US_Walking || m_state == US_Stunned || m_state == US_Attacking || m_state == US_Riding) &&
                (m_unitKind == UK_Goblin || m_unitKind == UK_BoarRider);
        }

        public bool canBeSlowedByIceball() { return m_unitKind != UK_Shaman && m_unitKind != UK_Skeleton && m_unitKind != UK_Elf; }

        public bool canBeThrownByFreedBoar()
        {
            if (m_isIntroUnit) return false;
            if (m_mount != null && m_mount.m_isIntroUnit) return false;
            return m_thrownByBoar && (m_state == US_Walking || m_state == US_Stunned || m_state == US_Attacking) && m_unitKind != UK_Elf;
        }

        public bool canBePetrified()
        {
            if (m_health <= 0.0f) return false;
            return m_state == US_Walking || m_state == US_Stunned || m_state == US_Attacking || m_state == US_MindControlling;
        }

        public bool isPetrified() { return m_state == US_Petrified; }

        public bool canBeKilledByLightning()
        {
            return m_state == US_Walking || m_state == US_Stunned || m_state == US_Attacking || m_state == US_Riding || m_state == US_Falling ||
                m_state == US_MindControlling || m_state == US_Stumble || m_state == US_TurningAroundToRunAway || m_state == US_RunningAwayThrowingStuffAround;
        }

        public bool canBeTakenByTornado()
        {
            return m_health > 0.0f && (m_state == US_Walking || m_state == US_Stunned || m_state == US_Attacking || m_state == US_Riding ||
                m_state == US_Falling || m_state == US_MindControlling || m_state == US_Stumble || m_state == US_TurningAroundToRunAway ||
                m_state == US_RunningAwayThrowingStuffAround || m_state == US_Dragged);
        }

        public bool canBeKilledByGhostArmy()
        {
            return m_health > 0.0f && (m_state == US_Walking || m_state == US_Stunned || m_state == US_Attacking || m_state == US_Riding ||
                m_state == US_Falling || m_state == US_MindControlling || m_state == US_Stumble || m_state == US_TurningAroundToRunAway ||
                m_state == US_Petrified || m_state == US_RunningAwayThrowingStuffAround);
        }

        public bool canBeStunnedByEarthquake()
        {
            return m_health > 0.0f && (m_state == US_Walking || m_state == US_Stunned || m_state == US_Attacking || m_state == US_MindControlling ||
                m_state == US_Stumble || m_state == US_TurningAroundToRunAway || m_state == US_Petrified || m_state == US_RunningAwayThrowingStuffAround);
        }

        public void petrify(float duration, bool stoned)
        {
            m_stoned = stoned;
            setVisible(false);
            switch (m_unitKind)
            {
                case UK_Bomblin: m_prerenderedFrame = INGAME_F_STONED_UNITS_BOMBLIN; break;
                case UK_Goblin: m_prerenderedFrame = INGAME_F_STONED_UNITS_GOBLIN; break;
                case UK_Boar:
                    if (m_rider != null) m_prerenderedFrame = m_statIsWildBoar ? INGAME_F_STONED_UNITS_BOAR_AND_RIDER_02 : INGAME_F_STONED_UNITS_BOAR_AND_RIDER_01;
                    else m_prerenderedFrame = m_statIsWildBoar ? INGAME_F_STONED_UNITS_BOAR_02 : INGAME_F_STONED_UNITS_BOAR_01;
                    break;
                case UK_BoarRider: m_prerenderedFrame = INGAME_F_STONED_UNITS_RIDER; break;
                case UK_Shaman: m_prerenderedFrame = INGAME_F_STONED_UNITS_SHAMAN; break;
                case UK_Troll: m_prerenderedFrame = INGAME_F_STONED_UNITS_TROLL; break;
                case UK_Elf: m_prerenderedFrame = INGAME_F_STONED_UNITS_ELF; break;
                case UK_Skeleton: m_prerenderedFrame = INGAME_F_STONED_UNITS_SKELETON; break;
                default: m_prerenderedFrame = -1; break;
            }
            if (m_prerenderedFrame != -1) m_prerenderedFrame += rand() % 2;
            m_freezeDuration = duration;
            setState(US_Petrified);
            if (m_rider != null)
            {
                m_rider.m_prerenderedFrame = -1;
                m_rider.m_freezeDuration = duration;
                m_rider.setState(US_Petrified);
            }
            CAchievements.GetInstance().addValue(m_stoned ? "Statue garden" : "Ice ice baby", 1);
        }

        public void unpetrify(bool kill)
        {
            if (m_state != US_Petrified) return;
            setVisible(true);
            if (kill)
            {
                damageUnit(9999, UDS_Petrify);
                int[] unitsHitByKind = new int[UK_Unknown + 1];
                unitsHitByKind[m_unitKind] = 1;
                CPlayfield.GetInstance().getChallenge().unitsWereKilledByWeapon(UDS_Petrify, unitsHitByKind);
                PlayUnitSound(m_stoned ? "StoneBreak" : "IceBreak");
            }
            else setState(m_mount != null ? US_Riding : US_Walking);
            if (m_rider != null) m_rider.unpetrify(kill);
            if (m_mount != null) m_mount.unpetrify(kill);
        }

        public void setMount(CUnit mount)
        {
            setPosition(m_position = v3f.Zero);
            m_mount = mount;
        }

        public void freeBoar()
        {
            m_rider = null;
            int s = getState();
            if (m_statIsFreeBoar && s != US_Falling && s != US_Dying && s != US_Dead && s != US_Dragged && s != US_FallingOnHydra)
                setState(US_TurningAroundToRunAway);
        }

        public bool canBeEatenByHydra()
        {
            if (m_isIntroUnit) return false;
            if (m_mount != null && m_mount.m_isIntroUnit) return false;
            return m_rider == null && (m_state == US_Walking || m_state == US_Stunned || m_state == US_Attacking || m_state == US_Riding ||
                m_state == US_Dragged || m_state == US_MindControlling);
        }

        public void setMindControlMaster(CUnit master)
        {
            if (m_mindControlMaster != null) return;
            m_mindControlMaster = master;
            master.m_mindControlSlaves.Add(this);
            if (master.m_shamanStaff.GetASprite() == null)
                master.m_shamanStaff.StartAnim(CPlayfield.GetInstance().getSpriteEffects(), INGAME_A_EFFECTS_SHAMAN_FIRE, 0, 0);
        }

        public void freeFromMindControl()
        {
            if (m_mindControlMaster == null) return;
            int idx = m_mindControlMaster.m_mindControlSlaves.LastIndexOf(this);
            if (idx >= 0) m_mindControlMaster.m_mindControlSlaves.RemoveAt(idx);
            m_mindControlMaster = null;
            onFreeFromMindControl();
        }

        public void freeAllSlavesFromMindControl()
        {
            if (m_mindControlSlaves.Count > 0 && getUnitKind() == UK_Shaman) CAchievements.GetInstance().setAchieved("Free your Mind");
            while (m_mindControlSlaves.Count > 0) m_mindControlSlaves[m_mindControlSlaves.Count - 1].freeFromMindControl();
        }

        protected virtual void onFreeFromMindControl()
        {
            if (m_state == US_Walking || m_state == US_Attacking || m_state == US_Stunned) setState(US_TurningAroundToRunAway);
        }

        public v3f getVelocity() { return m_velocity; }
        public void setMPosition(v3f position) { setPosition(m_position = position); }
        public void setVelocity(v3f velocity) { m_velocity = velocity; }
        public void healUnit(float health) { m_health += health; }
        bool canBeStunnedByShaman() { return !m_statMustDragIntoGroundToKill && (m_state == US_Walking || m_state == US_Attacking); }
        public int getUnitKind() { return m_unitKind; }

        protected virtual bool PlayUnitSound(string soundName, bool loop = false)
        {
            CSoundManager sm = __MGR_SOUNDS();
            IAttributes attr = CGameConfig.GetInstance().getAttributes("sounds", m_SoundsPackName);
            if (attr == null) return false;
            int num = 0;
            string str;
            do
            {
                str = num == 0 ? soundName : soundName + "_" + num;
                num++;
            } while (attr.getAttributeAsString(str).Length > 0);
            num--;
            if (num < 1) return false;
            int r = rand() % num;
            str = attr.getAttributeAsString(r > 0 ? soundName + "_" + r : soundName);
            int idx = sm.GetSoundIndexFromName(str);
            if (idx < 0) return false;
            IAttributes configAttr = CGameConfig.GetInstance().getAttributes("soundsconfig", str);
            if (configAttr != null && configAttr.getAttributeAsInt("Obligatory") == 0)
            {
                p2i pos = getScreenPosition();
                r2i viewport = new r2i(0, 0, GAME_SCREEN_W, GAME_SCREEN_H);
                if (!viewport.isPointInside(pos)) return CPlayfield.GetInstance().PlaySfx(idx, loop, 0, 100) != 0;
            }
            return CPlayfield.GetInstance().PlaySfx(idx, loop) != 0;
        }

        public int getDragPriority()
        {
            if (isPetrified()) return 100;
            if (m_unitKind == UK_Elf) return 80;
            if (m_unitKind == UK_Shaman) return 60;
            if (m_unitKind == UK_Troll) return 50;
            if (m_unitKind == UK_Skeleton) return 40;
            if (m_unitKind == UK_Goblin && m_statSpeed > 3.0f) return 20;
            return 0;
        }

        public void hideAura() { if (m_shamanAura != null) m_shamanAura.hideImmediately(); }

        void OptimizedCleanUnit(UnitParameters p, ISceneNode node)
        {
            foreach (ISceneNode child in new List<ISceneNode>(node.Children)) OptimizedCleanUnit(p, child);
            if (node.getType() == ESCENE_NODE_TYPE.ESNT_LIGHT || node.getType() == ESCENE_NODE_TYPE.ESNT_COLLADA_CAMERA)
            {
                node.remove();
                return;
            }
            string name = node.getName();
            if (m_bip01 == null && name == "Bip01") m_bip01 = node;
            if (m_chestDummy == null && name == p.chestDummy) m_chestDummy = node;
            if (m_dragDummy == null && name == p.dragDummy) m_dragDummy = node;
            if (m_energyBallDummy == null && name == p.energyBallDummy) m_energyBallDummy = node;
            if (m_headDummy == null && name == "Bip01_Head") m_headDummy = node;
            if (m_shamanStaffDummy == null && name == "d_magic") m_shamanStaffDummy = node;
            if (m_unitKind == UK_Bomblin && name == "Bip01_Prop1") node.addChild(m_bomb.getRoot());
            for (int i = 0; i < p.textureObjects.Count; ++i)
            {
                if (name == p.textureObjects[i] && node.Children.Count > 0 && node.Children[0].getMaterialCount() > 0)
                    node.Children[0].getMaterial(0).Textures[0] = TextureCache.getTexture(p.textureFiles[i]);
            }
            for (int i = 0; i < node.getMaterialCount(); ++i)
            {
                SMaterial material = node.getMaterial(i);
                material.Lighting = true;
                material.EmissiveColor = CGameConfig.EmissiveColorForInteractiveObjects;
                material.Shininess = 0.0f;
                material.MaterialType = E_MATERIAL_TYPE.EMT_TRANSPARENT_ALPHA_CHANNEL_REF;
                material.BackfaceCulling = true;
            }
        }

        void OptimizedCleanShadow(ISceneNode node)
        {
            foreach (ISceneNode child in new List<ISceneNode>(node.Children)) OptimizedCleanShadow(child);
            if (node.getType() == ESCENE_NODE_TYPE.ESNT_LIGHT || node.getType() == ESCENE_NODE_TYPE.ESNT_COLLADA_CAMERA)
            {
                node.remove();
                return;
            }
            for (int i = 0; i < node.getMaterialCount(); ++i)
            {
                SMaterial material = node.getMaterial(i);
                material.Lighting = true;
                material.EmissiveColor = new SColor(0x00000000);
                material.Shininess = 0.0f;
            }
        }
    }
}
