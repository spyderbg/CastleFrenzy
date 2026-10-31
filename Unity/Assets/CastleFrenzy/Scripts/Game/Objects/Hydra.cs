// Game/Objects/Weapons/Hydra.
using System.Collections.Generic;
using static CastleFrenzy.G;
using static CastleFrenzy.GE;
using static CastleFrenzy.Ids;

namespace CastleFrenzy
{
    public class CHydraHead : ColladaControl
    {
        public float Cooldown;
        public int State;
    }

    public class CHydra : CWeaponObject
    {
        public const int HS_Idle = 0, HS_EatFromHand = 1, HS_WaitForFallingUnit = 2, HS_EatFromRoad = 3, HS_WaitForHelp = 4, HS_EatUnit = 5,
            HS_Transition = 6, HS_Shop = 7, HS_Disabled = 8;
        public const int HHS_Ready = 0, HHS_Aiming = 1, HHS_Eating = 2, HHS_Cooldown = 3, HHS_Transition = 4;

        protected float m_stat_cooldown;
        protected int m_stat_heads;

        int m_transitionHead;
        float m_transitionTimer;
        int m_state = -1;
        CUnit m_victim;
        ISceneNode m_foodDummy;
        readonly ISceneNode m_victimDummy;
        readonly ISceneNode[] m_hydraDummy = new ISceneNode[3];
        bool m_visible, m_aiming;
        readonly CTouchElement m_touch;
        float m_angle;
        v3f m_ptToFace;
        float m_refuseTimer;
        readonly float[] m_animOldScales = new float[3];
        public readonly CHydraHead[] Head = { new CHydraHead(), new CHydraHead(), new CHydraHead() };

        public CHydra() : base(null)
        {
            const float scale = 0.9f;
            m_Node = __IRR_SCENE().addEmptySceneNode();
            m_Node.setPosition(new v3f(0, -1.5f, 0));
            SColor e = CGameConfig.EmissiveColorForInteractiveObjects;
            SColor halfEmissive = new SColor(e.getAlpha() / 2, e.getRed() / 2, e.getGreen() / 2, e.getBlue() / 2);
            ISceneNode node = null;
            for (int i = 0; i < 3; i++)
            {
                node = LoadColladaScene("Scenes/Hydra/hydra.bdae");
                cleanNode(node, false, true);
                setEmissiveColor(node, halfEmissive);
                Head[i].setRoot(node);
            }
            m_hydraDummy[0] = node.getSceneNodeFromName("d_hydra_c");
            m_hydraDummy[1] = node.getSceneNodeFromName("d_hydra_l");
            m_hydraDummy[2] = node.getSceneNodeFromName("d_hydra_r");
            for (int i = 0; i < 3; i++) m_Node.addChild(Head[i].getRoot());
            for (int i = 0; i < 3; i++) Head[i].getRoot().setPosition(m_hydraDummy[i].getPosition());
            m_Node.setScale(new v3f(1, 1, 1) * scale);
            for (int i = 0; i < 3; i++)
            {
                ISceneNode bone = Head[i].getRoot().getSceneNodeFromName("Bone01");
                if (bone != null) bone.setScale(new v3f(1, 1, 1) * scale);
            }
            m_victimDummy = new CEmptySceneNode();
            m_touch = new CTouchElement(CTouchElement.Type_Drag, 100);
            m_touch.SetScreenCoordinates(350, 90, 100, 100);
            setLevel(WL_None);
            reset();
        }

        public override int getType() { return WT_Hydra; }

        public override void reset()
        {
            base.reset();
            if (m_level != WL_None)
            {
                setState(HS_Idle);
                Head[0].State = HHS_Ready;
                Head[0].setAnim("idle", true, RandFloat(0.8f, 1.1f));
                Head[1].State = HHS_Ready;
                Head[1].setAnim("idle_low", true, RandFloat(0.8f, 1.1f));
                Head[2].State = HHS_Ready;
                Head[2].setAnim("idle_low", true, RandFloat(0.8f, 1.1f));
            }
            else
            {
                setState(HS_Disabled);
                m_stat_heads = 0;
            }
            setVisible(true);
            if (m_victim != null)
            {
                m_victim.setState(US_Dead);
                m_victim = null;
            }
            m_aiming = false;
            m_angle = 0.0f;
            m_refuseTimer = 0.0f;
            for (int i = 0; i < m_stat_heads; i++) Head[i].Cooldown = 0.0f;
        }

        public override void Dispose()
        {
            for (int i = 0; i < 3; i++) Head[i].getRoot().remove();
            m_victimDummy.remove();
            m_Node.remove();
            m_touch.Dispose();
        }

        protected override void adjustStats(IAttributes attributes)
        {
            string[] cooldown = { "CooldownEasy", "CooldownMedium", "CooldownHard" };
            int difficulty = CPlayfield.GetInstance().getDifficulty();
            m_stat_cooldown = attributes.getAttributeAsFloat(cooldown[difficulty]);
            m_stat_heads = attributes.getAttributeAsInt("Heads");
        }

        public bool isReadyToEat()
        {
            if (m_state != HS_Idle && m_state != HS_EatFromRoad) return false;
            return Head[0].State == HHS_Ready || Head[0].State == HHS_Aiming;
        }

        public bool isReadyToEat(CUnit unit)
        {
            if (!isReadyToEat()) return false;
            int readyHeads = 1;
            if (m_stat_heads >= 2 && Head[1].State == HHS_Ready) readyHeads++;
            if (m_stat_heads >= 3 && Head[2].State == HHS_Ready) readyHeads++;
            return unit.getSouls() > 0 && readyHeads >= unit.getSouls();
        }

        void attachVictimToHydra()
        {
            CPlayfield.GetInstance().PlaySfx(SFX_DEFENSE_HYDRA_EATING);
            m_victim.setAnimation(m_victim.getUnitKind() == UK_BoarRider ? UA_Stunned : UA_Falling);
            m_foodDummy = Head[0].getRoot().getSceneNodeFromName(m_victim.getUnitKind() == UK_Boar ? "d_food_boar" : "d_food");
            m_foodDummy.addChild(m_victimDummy);
            m_victimDummy.addChild(m_victim.getSceneNode());
            m_victim.setPosition(m_victim.getAbsolutePosition() - m_victim.getChestDummy().getAbsolutePosition());
            m_victim.getSceneNode().setRotation(new quaternion(0, 0, 0));
            if (m_victim.getUnitKind() == UK_Boar) m_victimDummy.setRotation(new quaternion(PI / 2, 0, PI / 2));
            else m_victimDummy.setRotation(new quaternion(0, -PI / 2, PI));
            m_victim.bleed(0.030f);
            if (m_victim.getUnitKind() == UK_Shaman) m_victim.shamanExplode();
        }

        void claimHelpers(int heads, string start, string idle)
        {
            if (heads > 0 && Head[1].State == HHS_Ready) { Head[1].State = HHS_Eating; Head[1].setAnimAnim(start, idle); heads--; }
            if (heads > 0 && Head[2].State == HHS_Ready) { Head[2].State = HHS_Eating; Head[2].setAnimAnim(start, idle); heads--; }
        }

        public void waitForUnit(CUnit unit)
        {
            setState(HS_WaitForFallingUnit);
            int heads = unit.getSouls();
            Head[0].State = HHS_Eating;
            Head[0].setAnimAnim("feed_start", "feed_idle");
            heads--;
            claimHelpers(heads, "feed_help_start", "feed_help_idle");
        }

        public void eatUnitStart(CUnit unit)
        {
            if (m_victim == null)
            {
                m_victim = unit;
                attachVictimToHydra();
            }
            int heads = unit.getSouls();
            if (heads == 1)
            {
                eatUnitContinue();
                return;
            }
            setState(HS_WaitForHelp);
            Head[0].setAnim("feed_idle");
            heads--;
            if (heads > 0 && Head[1].State == HHS_Eating) { Head[1].setAnim("feed_help_end", false); heads--; }
            if (heads > 0 && Head[2].State == HHS_Eating) { Head[2].setAnim("feed_help_end", false); heads--; }
        }

        void eatUnitContinue()
        {
            setState(HS_EatUnit);
            Head[0].State = HHS_Eating;
            Head[0].setAnim(m_stat_heads == 1 ? "feed_hand_end_side" : "feed_need_help_end", false);
            CPlayfield pf = CPlayfield.GetInstance();
            pf.addMoney(10, 0, m_foodDummy.getAbsolutePosition());
            pf.addSouls(m_victim.getSouls(), m_foodDummy.getAbsolutePosition());
            pf.getChallenge().specialWeaponUsed();
            pf.getChallenge().hydraHasEatenUnit(m_victim.getUnitKind());
            CAchievements.GetInstance().setValue("Treat Pets Well", 1 << m_victim.getUnitKind());
        }

        void eatUnitEnd()
        {
            setState(HS_Idle);
            if (m_victim != null)
            {
                m_victim.setState(US_Dead);
                m_victim = null;
            }
            for (int i = 0; i < 3; i++)
            {
                if (Head[i].State != HHS_Eating) continue;
                Head[i].State = HHS_Cooldown;
                Head[i].Cooldown = CGameConfig.NoWeaponCD ? 0.0f : m_stat_cooldown;
            }
        }

        public void eatFromHandStart(CUnit unit)
        {
            if (!isReadyToEat(unit))
            {
                if (m_refuseTimer == 0.0f)
                {
                    m_refuseTimer = 2.0f;
                    Head[0].setAnimAnim("refuse", "idle");
                }
                return;
            }
            m_victim = unit;
            m_victim.setState(US_ToBeEeatenByHydra);
            setState(HS_EatFromHand);
            Head[0].State = HHS_Eating;
            Head[0].setAnim("feed_hand_attack", false);
        }

        void eatFromHandFail()
        {
            Head[0].State = HHS_Eating;
            Head[0].setAnim("feed_hand_fail", false);
        }

        void eatFromHandSuccess()
        {
            attachVictimToHydra();
            int heads = m_victim.getSouls();
            Head[0].State = HHS_Eating;
            Head[0].setAnim("feed_hand_need_help", false);
            heads--;
            if (heads > 0 && Head[1].State == HHS_Ready)
            {
                Head[1].State = HHS_Eating;
                Head[1].setAnimAnim("feed_help_start", "feed_help_idle");
                heads--;
                if (m_victim.getUnitKind() == UK_Boar) m_victim.setPosition(m_victim.getPosition() + new v3f(0, 0, 0.5f));
            }
            if (heads > 0 && Head[2].State == HHS_Ready)
            {
                Head[2].State = HHS_Eating;
                Head[2].setAnimAnim("feed_help_start", "feed_help_idle");
                heads--;
                if (m_victim.getUnitKind() == UK_Boar) m_victim.setPosition(m_victim.getPosition() - new v3f(0, 0, 0.5f));
            }
        }

        void eatFromRoadStart()
        {
            setState(HS_EatFromRoad);
            m_aiming = true;
            Head[0].State = HHS_Aiming;
            Head[0].setAnimAnim("idle_to_attack_idle", "attack_idle");
        }

        public void eatFromRoadCancel()
        {
            setState(HS_Idle);
            m_aiming = false;
            Head[0].State = HHS_Ready;
            Head[0].setAnimAnim("attack_idle_to_idle", "idle", true, 1.0f, RandFloat(0.8f, 1.1f));
        }

        void eatFromRoadFail()
        {
            m_aiming = false;
            Head[0].setAnim("attack_fail", false);
        }

        void eatFromRoadSuccess(CUnit unit)
        {
            m_aiming = false;
            m_victim = unit;
            m_victim.setState(US_ToBeEeatenByHydra);
            m_victim.setAnimation(UA_Stunned);
            int heads = m_victim.getSouls();
            Head[0].State = HHS_Eating;
            Head[0].setAnim("attack_success", false);
            heads--;
            claimHelpers(heads, "feed_help_start", "feed_help_idle");
        }

        void eatFromRoadRotateHead()
        {
            int tx, ty;
            m_touch.GetTouchPos_Current(out tx, out ty);
            line3df ray = __IRR_SCENE().getRayFromScreenCoordinates(new p2i(tx, ty));
            v3f normal = ray.getVector().crossProduct(new v3f(0, 1, 0)).normalized();
            plane3df pl = new plane3df(ray.start, normal);
            line3df path = CPlayfield.GetInstance().getPath();
            if (pl.getIntersectionWithLine(path.start, path.getVector(), ref m_ptToFace))
            {
                v3f dir = m_ptToFace - getAbsolutePosition();
                dir.Y = 0;
                quaternion invRot = new quaternion(0, PI, 0);
                invRot.makeInverse();
                dir = invRot * dir;
                m_angle = CLAMP(atan2f(-dir.X, dir.Z), -30.0f * PI / 180.0f, 30.0f * PI / 180.0f);
            }
        }

        CUnit eatFromRoadGetTarget()
        {
            v3f hydraPos = getAbsolutePosition();
            plane3df hydraLookingPlane = new plane3df(m_ptToFace, hydraPos, m_ptToFace + new v3f(0, 1, 0));
            CUnit bestUnit = null;
            float bestDistance = 10000.0f;
            foreach (CUnit u in CPlayfield.GetInstance().getUnits())
            {
                float distanceToPlane = fabsf(hydraLookingPlane.getDistanceTo(u.getAbsolutePosition()));
                v3f dir = u.getAbsolutePosition() - hydraPos;
                dir.Y = 0.0f;
                float distanceToHydra = dir.getLength();
                if (u.canBeEatenByHydra() && isReadyToEat(u) && distanceToPlane < 1.5f && distanceToHydra < 6.0f &&
                    (bestUnit == null || distanceToPlane < bestDistance))
                {
                    bestUnit = u;
                    bestDistance = distanceToPlane;
                }
            }
            return bestUnit;
        }

        public void setState(int state) { m_state = state; }

        public override void update(float time)
        {
            base.update(time);
            m_refuseTimer = MAX(0.0f, m_refuseTimer - time);
            for (int i = 0; i < 3; i++)
                if (Head[i].State == HHS_Cooldown) Head[i].Cooldown = MAX(0.0f, Head[i].Cooldown - time);
            switch (m_state)
            {
                case HS_Idle:
                    for (int i = 0; i < m_stat_heads - 1; i++)
                        for (int j = i + 1; j < m_stat_heads; j++)
                            if (Head[i].State == HHS_Cooldown && Head[j].State == HHS_Cooldown && Head[i].Cooldown > Head[j].Cooldown)
                            {
                                float t = Head[i].Cooldown; Head[i].Cooldown = Head[j].Cooldown; Head[j].Cooldown = t;
                            }
                    for (int i = 0; i < m_stat_heads; i++)
                    {
                        if (Head[i].State == HHS_Cooldown && Head[i].Cooldown == 0.0f)
                        {
                            Head[i].State = HHS_Ready;
                            if (i == 0) Head[i].setAnimAnim("up", "idle", true, 1.0f, RandFloat(0.8f, 1.1f));
                            else Head[i].setAnimAnim("up_low", "idle_low", true, 1.0f, RandFloat(0.8f, 1.1f));
                        }
                    }
                    if (Head[0].State == HHS_Ready && m_touch.IsNowActivated()) eatFromRoadStart();
                    if (Head[0].State == HHS_Cooldown)
                    {
                        if (m_stat_heads >= 3 && Head[2].State == HHS_Ready) startHeadTransition(2);
                        else if (m_stat_heads >= 2 && Head[1].State == HHS_Ready) startHeadTransition(1);
                    }
                    break;
                case HS_EatFromHand:
                    if (Head[0].checkEvent("anim_end"))
                    {
                        string anim = Head[0].getCurrentAnimName();
                        if (anim == "feed_hand_attack")
                        {
                            if (m_stat_heads < m_victim.getSouls()) eatFromHandFail();
                            else eatFromHandSuccess();
                        }
                        else if (anim == "feed_hand_fail")
                        {
                            setState(HS_Idle);
                            Head[0].State = HHS_Ready;
                            Head[0].setAnim("idle", true, RandFloat(0.8f, 1.1f));
                        }
                        else if (anim == "feed_hand_need_help") eatUnitStart(m_victim);
                    }
                    break;
                case HS_EatFromRoad:
                    if (m_aiming)
                    {
                        eatFromRoadRotateHead();
                        if (m_touch.IsNowReleased())
                        {
                            CUnit target = eatFromRoadGetTarget();
                            if (target != null) eatFromRoadSuccess(target);
                            else eatFromRoadFail();
                        }
                    }
                    else
                    {
                        if (Head[0].checkEvent("attack")) attachVictimToHydra();
                        if (Head[0].checkEvent("anim_end"))
                        {
                            string anim = Head[0].getCurrentAnimName();
                            if (anim == "attack_fail")
                            {
                                setState(HS_Idle);
                                Head[0].State = HHS_Ready;
                                Head[0].setAnimAnim("attack_idle_to_idle", "idle", true, 1.0f, RandFloat(0.8f, 1.1f));
                            }
                            else if (anim == "attack_success") eatUnitStart(m_victim);
                        }
                    }
                    break;
                case HS_WaitForHelp:
                    if (Head[1].checkEvent("help_done") || Head[2].checkEvent("help_done"))
                    {
                        m_victim.bleed(0.020f);
                        eatUnitContinue();
                    }
                    break;
                case HS_EatUnit:
                    if (Head[0].checkEvent("anim_end")) eatUnitEnd();
                    break;
                case HS_Transition:
                {
                    const float duration = 1.0f;
                    if (m_transitionTimer == 0.0f) Head[m_transitionHead].setAnim("idle_low_to_idle", false);
                    m_transitionTimer += time;
                    if (m_transitionTimer < duration)
                    {
                        float k = m_transitionTimer / duration;
                        Head[m_transitionHead].getRoot().setPosition(m_hydraDummy[0].getPosition().getInterpolated(m_hydraDummy[m_transitionHead].getPosition(), k));
                    }
                    else
                    {
                        Head[m_transitionHead].State = HHS_Cooldown;
                        Head[m_transitionHead].Cooldown = Head[0].Cooldown;
                        Head[m_transitionHead].getRoot().setPosition(m_hydraDummy[m_transitionHead].getPosition());
                        Head[0].State = HHS_Ready;
                        Head[0].Cooldown = 0.0f;
                        Head[0].setAnim("idle", true, RandFloat(0.8f, 1.1f));
                        Head[0].getRoot().setPosition(m_hydraDummy[0].getPosition());
                        setState(HS_Idle);
                    }
                    break;
                }
            }
            if (!m_aiming && m_state != HS_EatFromRoad)
            {
                const float speed = 1.0f;
                if (m_angle > 0.0f) { m_angle -= speed * time; if (m_angle < 0.0f) m_angle = 0.0f; }
                else if (m_angle < 0.0f) { m_angle += speed * time; if (m_angle > 0.0f) m_angle = 0.0f; }
            }
            string[] dummies = { "d_hydra_c", "d_hydra_l", "d_hydra_r" };
            for (int i = 0; i < 3; i++)
            {
                ISceneNode d = Head[i].getRoot().getSceneNodeFromName(dummies[i]);
                if (d != null) Head[i].getRoot().setRotation(d.getRotation());
            }
            quaternion newRot = new quaternion();
            newRot.fromAngleAxis(m_angle, new v3f(0, 1, 0));
            Head[0].getRoot().setRotation(newRot * Head[0].getRoot().getRotation());
            if (Head[0].checkEvent("eat_lightball")) CPlayfield.GetInstance().m_lightball = false;
            setVisible(m_visible);
            for (int i = 0; i < 3; i++) Head[i].updateAtEndOfFrame(time);
        }

        public int getState() { return m_state; }
        public CTouchElement getTouch() { return m_touch; }
        public override void draw() { }

        public override void setVisible(bool visible)
        {
            m_visible = visible;
            m_Node.setVisible(m_visible);
            for (int i = 0; i < 3; i++) Head[i].getRoot().setVisible(m_visible && m_stat_heads >= i + 1 && Head[i].State != HHS_Cooldown);
        }

        public override bool isVisible() { return m_visible; }

        public override float getCooldown()
        {
            if (m_state != HS_Idle) return 0.0f;
            for (int i = 0; i < m_stat_heads; i++) if (Head[i].Cooldown == 0.0f) return 0.0f;
            return Head[0].Cooldown / m_stat_cooldown;
        }

        public override void setShopState()
        {
            Head[0].setAnim("idle", true, RandFloat(0.8f, 1.1f));
            Head[1].setAnim("idle_low", true, RandFloat(0.8f, 1.1f));
            Head[2].setAnim("idle_low", true, RandFloat(0.8f, 1.1f));
            setState(HS_Shop);
        }

        void startHeadTransition(int head)
        {
            m_transitionHead = head;
            m_transitionTimer = 0.0f;
            setState(HS_Transition);
        }

        public override void freezeAnimation(bool freeze)
        {
            if (freeze == m_animFrozen) return;
            m_animFrozen = freeze;
            for (int i = 0; i < 3; i++)
            {
                CTimelineController tc = Head[i].getRoot().getFirstAnimator().getTimelineCtrl();
                if (freeze) { m_animOldScales[i] = tc.getScale(); tc.setScale(0.0f); }
                else tc.setScale(m_animOldScales[i]);
            }
        }
    }
}
