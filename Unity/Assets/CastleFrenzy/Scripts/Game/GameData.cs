// Game/Config (LevelStats, Scores, Upgrades), Game/LevelInfo, Game/PlayfieldParts/Challenge
// and Game/Objects/UnitParts/UnitKind ported from the C++ game.
using System;
using System.Collections.Generic;
using static CastleFrenzy.G;
using static CastleFrenzy.GE;
using static CastleFrenzy.Ids;

namespace CastleFrenzy
{
    public class CLevelStats
    {
        public int Gold, Souls, Kills;
        public int[] WeaponLevel = new int[WT_Count];
        public bool[] WeaponAvailable = new bool[WT_Count];
        public int SpellAvailable;

        public CLevelStats() { reset(); }

        public void reset()
        {
            Gold = 0; Souls = 0;
            for (int i = 0; i < WT_Count; i++) { WeaponLevel[i] = WL_None; WeaponAvailable[i] = true; }
            SpellAvailable = ST_None;
            Kills = 0;
        }
    }

    public class CampaignScore { public int gold, souls, difficulty; }
    public class EndlessScore { public int kills, days, gold, souls; }

    public static class Scores
    {
        public const int k_multiplier_kills = 5, k_multiplier_days = 1000, k_multiplier_gold = 2, k_multiplier_souls = 100;
        public static readonly int[] k_difficulty_bonus = { 1000, 10000, 100000 };
        static bool s_lastScoreWasHighScore;

        public static int GetScoreTotal(CampaignScore s) { return s.gold * k_multiplier_gold + s.souls * k_multiplier_souls + k_difficulty_bonus[s.difficulty]; }
        public static int GetScoreTotal(EndlessScore s) { return s.kills * k_multiplier_kills + s.days * k_multiplier_days + s.gold * k_multiplier_gold + s.souls * k_multiplier_souls; }
        public static bool LastScoreWasHighScore() { return s_lastScoreWasHighScore; }

        public static bool HasEndlessScore(CUserProfile p) { return p.GetInt("HighScoreExistsForEndless") != 0; }

        public static void GetEndlessScore(CUserProfile p, EndlessScore s)
        {
            s.kills = p.GetInt("HighScoreEndlessKills");
            s.days = p.GetInt("HighScoreEndlessDays");
            s.gold = p.GetInt("HighScoreEndlessGold");
            s.souls = p.GetInt("HighScoreEndlessSouls");
        }

        static void SetEndlessScore(CUserProfile p, EndlessScore s)
        {
            s_lastScoreWasHighScore = true;
            p.Set("HighScoreExistsForEndless", 1);
            p.Set("HighScoreEndlessKills", s.kills);
            p.Set("HighScoreEndlessDays", s.days);
            p.Set("HighScoreEndlessGold", s.gold);
            p.Set("HighScoreEndlessSouls", s.souls);
        }

        public static void NewEndlessScore(CUserProfile p, EndlessScore s)
        {
            s_lastScoreWasHighScore = false;
            if (HasEndlessScore(p))
            {
                EndlessScore old = new EndlessScore();
                GetEndlessScore(p, old);
                int oldScore = GetScoreTotal(old), newScore = GetScoreTotal(s);
                if (newScore > 0 && oldScore < newScore) SetEndlessScore(p, s);
            }
            else if (GetScoreTotal(s) > 0) SetEndlessScore(p, s);
        }

        public static bool HasCampaignScore(CUserProfile p, int level) { return p.GetInt("HighScoreExistsForLevel" + (level + 1)) != 0; }

        public static void GetCampaignScore(CUserProfile p, CampaignScore s, int level)
        {
            s.gold = p.GetInt("HighScoreLevelGold" + (level + 1));
            s.souls = p.GetInt("HighScoreLevelSouls" + (level + 1));
            s.difficulty = p.GetInt("HighScoreLevelDifficulty" + (level + 1));
        }

        static void SetCampaignScore(CUserProfile p, CampaignScore s, int level)
        {
            s_lastScoreWasHighScore = true;
            p.Set("HighScoreExistsForLevel" + (level + 1), 1);
            p.Set("HighScoreLevelGold" + (level + 1), s.gold);
            p.Set("HighScoreLevelSouls" + (level + 1), s.souls);
            p.Set("HighScoreLevelDifficulty" + (level + 1), s.difficulty);
        }

        public static void NewCampaignScore(CUserProfile p, CampaignScore s, int level)
        {
            s_lastScoreWasHighScore = false;
            if (HasCampaignScore(p, level))
            {
                CampaignScore old = new CampaignScore();
                GetCampaignScore(p, old, level);
                if (GetScoreTotal(old) < GetScoreTotal(s)) SetCampaignScore(p, s, level);
            }
            else SetCampaignScore(p, s, level);
        }
    }

    public class SUpgradeInfo
    {
        public int Name, Description;
        public string UpgradeType, UpgradeID;

        public SUpgradeInfo(int name, int desc, string type, string id) { Name = name; Description = desc; UpgradeType = type; UpgradeID = id; }

        public IAttributes getAttributes() { return CGameConfig.GetInstance().getAttributes(UpgradeType, UpgradeID); }

        public int getPriceGold()
        {
            string[] price = { "PriceGoldEasy", "PriceGoldMedium", "PriceGoldHard" };
            IAttributes a = getAttributes();
            return a != null ? a.getAttributeAsInt(price[CPlayfield.GetInstance().getDifficulty()]) : 0;
        }

        public int getPriceSouls()
        {
            string[] price = { "PriceSoulsEasy", "PriceSoulsMedium", "PriceSoulsHard" };
            IAttributes a = getAttributes();
            return a != null ? a.getAttributeAsInt(price[CPlayfield.GetInstance().getDifficulty()]) : 0;
        }

        static readonly SUpgradeInfo[,] WEAPON_LEVEL_INFO =
        {
            { new SUpgradeInfo(STRING_IDS.WEAPON_DOOR_NAME_L1, STRING_IDS.WEAPON_DOOR_DESC_L1, "gate", "Gate02"),
              new SUpgradeInfo(STRING_IDS.WEAPON_DOOR_NAME_L2, STRING_IDS.WEAPON_DOOR_DESC_L2, "gate", "Gate03"),
              new SUpgradeInfo(STRING_IDS.WEAPON_DOOR_NAME_L3, STRING_IDS.WEAPON_DOOR_DESC_L3, "gate", "Gate04") },
            { new SUpgradeInfo(STRING_IDS.WEAPON_HYDRA_NAME_L1, STRING_IDS.WEAPON_HYDRA_DESC_L1, "weapon", "Hydra_1"),
              new SUpgradeInfo(STRING_IDS.WEAPON_HYDRA_NAME_L2, STRING_IDS.WEAPON_HYDRA_DESC_L2, "weapon", "Hydra_2"),
              new SUpgradeInfo(STRING_IDS.WEAPON_HYDRA_NAME_L3, STRING_IDS.WEAPON_HYDRA_DESC_L3, "weapon", "Hydra_3") },
            { new SUpgradeInfo(STRING_IDS.WEAPON_FIREBALL_NAME_L1, STRING_IDS.WEAPON_FIREBALL_DESC_L1, "weapon", "Fireball_1"),
              new SUpgradeInfo(STRING_IDS.WEAPON_FIREBALL_NAME_L2, STRING_IDS.WEAPON_FIREBALL_DESC_L2, "weapon", "Fireball_2"),
              new SUpgradeInfo(STRING_IDS.WEAPON_FIREBALL_NAME_L3, STRING_IDS.WEAPON_FIREBALL_DESC_L3, "weapon", "Fireball_3") },
            { new SUpgradeInfo(STRING_IDS.WEAPON_ICEBALL_NAME_L1, STRING_IDS.WEAPON_ICEBALL_DESC_L1, "weapon", "Iceball_1"),
              new SUpgradeInfo(STRING_IDS.WEAPON_ICEBALL_NAME_L2, STRING_IDS.WEAPON_ICEBALL_DESC_L2, "weapon", "Iceball_2"),
              new SUpgradeInfo(STRING_IDS.WEAPON_ICEBALL_NAME_L3, STRING_IDS.WEAPON_ICEBALL_DESC_L3, "weapon", "Iceball_3") },
            { new SUpgradeInfo(STRING_IDS.WEAPON_GRIFFIN_NAME_L1, STRING_IDS.WEAPON_GRIFFIN_DESC_L1, "weapon", "Griffin_1"),
              new SUpgradeInfo(STRING_IDS.WEAPON_GRIFFIN_NAME_L2, STRING_IDS.WEAPON_GRIFFIN_DESC_L2, "weapon", "Griffin_2"),
              new SUpgradeInfo(STRING_IDS.WEAPON_GRIFFIN_NAME_L3, STRING_IDS.WEAPON_GRIFFIN_DESC_L3, "weapon", "Griffin_3") },
        };

        static readonly SUpgradeInfo[] SPELL_INFO =
        {
            new SUpgradeInfo(STRING_IDS.WEAPON_BOULDER_NAME, STRING_IDS.WEAPON_BOULDER_DESC, "spell", "None"),
            new SUpgradeInfo(STRING_IDS.WEAPON_BOULDER_NAME, STRING_IDS.WEAPON_BOULDER_DESC, "spell", "Boulder"),
            new SUpgradeInfo(STRING_IDS.WEAPON_LIGHTNING_NAME, STRING_IDS.WEAPON_LIGHTNING_DESC, "spell", "Lightning"),
            new SUpgradeInfo(STRING_IDS.WEAPON_GHOST_NAME, STRING_IDS.WEAPON_GHOST_DESC, "spell", "GhostArmy"),
            new SUpgradeInfo(STRING_IDS.WEAPON_TORNADO_NAME, STRING_IDS.WEAPON_TORNADO_DESC, "spell", "Tornado"),
        };

        public static SUpgradeInfo GetWeaponInfo(int weapon, int level)
        {
            if (weapon == WT_Spell) return SPELL_INFO[level + 1];
            return WEAPON_LEVEL_INFO[weapon, level];
        }
    }

    // ------------------------------------------------------------------------ challenge

    public class Challenge
    {
        const int k_goblinWallSmashGoal = 5, k_shamanWallSmashGoal = 5, k_boarRampageCount = 5;
        const float k_scrollingTime = 3.0f, k_scrollingTransition = 0.5f;

        static readonly string[] s_names =
        {
            "None", "PerfectHealth", "GoblinSmasher", "ShamanSmasher", "BoarRampage", "GriffinFireballMaster", "GriffinFireballSuperMaster",
            "FlickMaster", "OneHitOneKill", "ManyEnemiesWithSpecial", "ThatWasClose", "WallSmasher", "GroundSmasher", "MenuGoblins",
            "MenuTrolls", "MenuShamans", "ShamanCombo", "TrollCombo", "ComboMaster"
        };

        public static int StringToChallengeType(string text)
        {
            for (int i = 1; i < s_names.Length; i++) if (s_names[i] == text) return i;
            return CT_None;
        }

        public static string ChallengeTypeToString(int type) { return type > 0 && type < s_names.Length ? s_names[type] : "None"; }

        int m_type;
        string m_scrollingTitle;
        float m_scrollingTimer;
        int m_wallSmashKind, m_wallSmashCount, m_wallSmashGoal;
        bool m_specialWeaponUsed, m_challengeComplete, m_failed;
        int m_enemiesKilledBySpecial, m_enemiesKilledOnWall, m_enemiesKilledOnGround, m_combos, m_enemiesEaten;

        public Challenge() { setType(CT_None); }

        public int getType() { return m_type; }

        public void setType(int type)
        {
            m_type = type;
            m_scrollingTitle = null;
            m_scrollingTimer = 0.0f;
            m_challengeComplete = false;
            m_specialWeaponUsed = false;
            m_enemiesKilledBySpecial = 0;
            m_enemiesKilledOnWall = 0;
            m_enemiesKilledOnGround = 0;
            m_combos = 0;
            m_enemiesEaten = 0;
            m_wallSmashKind = UK_Unknown;
            m_wallSmashCount = 0;
            if (type == CT_GoblinWallSmash) { m_wallSmashKind = UK_Goblin; m_wallSmashGoal = k_goblinWallSmashGoal; }
            if (type == CT_ShamanWallSmash) { m_wallSmashKind = UK_Shaman; m_wallSmashGoal = k_shamanWallSmashGoal; }
        }

        public bool isChallengeCompleted() { return m_challengeComplete && !m_failed; }
        public bool isVisible() { return m_scrollingTitle != null; }

        public void update(float time)
        {
            if (m_scrollingTitle != null)
            {
                m_scrollingTimer += time;
                if (m_scrollingTimer > k_scrollingTime) m_scrollingTitle = null;
            }
        }

        public void draw()
        {
            if (m_scrollingTitle == null) return;
            float x = LinearUpFlatDown(m_scrollingTimer, 0, k_scrollingTransition, k_scrollingTime - k_scrollingTransition, k_scrollingTime);
            ASprite spr = CPlayfield.GetInstance().getSpriteIngame();
            spr.PaintFrame(MENU_F_MENU_CHALLENGE_BG, (int)(170 * x - 170), 30);
            r2i rect = spr.GetFrameRect(MENU_F_MENU_CHALLENGE_BG);
            string str = (m_failed ? _TXT(STRING_IDS.CHALLENGES_FAILED) : _TXT(STRING_IDS.CHALLENGES_COMPLETED)) + "\n" + m_scrollingTitle;
            string challengeFont = m_failed ? "ChallengeFailed" : "Challenge";
            float scaleFont = 0.85f;
            CFont f = __FONT(challengeFont);
            float sx = f.GetScaleX(), sy = f.GetScaleY();
            f.SetScale(sx * scaleFont, sy * scaleFont);
            f.DrawWrappedText(str, 170 * x - 170 + 82, 30 + 33, rect.getWidth() - 5, CFont.HVCENTER, 255);
            f.SetScale(sx, sy);
        }

        public void gateDamaged() { if (m_type == CT_PerfectHealth) failChallenge(); }

        public void unitDied(int damageSource, int kind)
        {
            switch (m_type)
            {
                case CT_GoblinWallSmash:
                case CT_ShamanWallSmash:
                    if (kind == m_wallSmashKind && m_wallSmashCount < m_wallSmashGoal && damageSource == UDS_WallHit)
                    {
                        ++m_wallSmashCount;
                        if (m_wallSmashCount == m_wallSmashGoal) completeChallenge();
                    }
                    break;
                case CT_WallSmasher:
                    if (damageSource == UDS_WallHit && ++m_enemiesKilledOnWall >= 15) completeChallenge();
                    break;
                case CT_GroundSmasher:
                    if (damageSource == UDS_GroundDrag && ++m_enemiesKilledOnGround >= 15) completeChallenge();
                    break;
            }
        }

        public void levelEnded(bool maxHealth, bool criticalHealth, bool win)
        {
            if (win)
            {
                if (m_type == CT_PerfectHealth && maxHealth) completeChallenge();
                if (m_type == CT_FlickMaster && !m_specialWeaponUsed) completeChallenge();
                if (m_type == CT_ThatWasClose && criticalHealth) completeChallenge();
            }
            if (!m_challengeComplete && m_type != CT_None) failChallenge();
        }

        public void unitWasHitByBoar(int[] unitsByKind)
        {
            if (m_type == CT_BoarRampage && !m_challengeComplete && unitsByKind[UK_Goblin] + unitsByKind[UK_BoarRider] >= k_boarRampageCount)
                completeChallenge();
        }

        public void unitsWereKilledByWeapon(int damageSource, int[] unitsHitByKind)
        {
            if (m_challengeComplete) return;
            int differentTypes = 0, totalKilled = 0;
            for (int i = 0; i < UK_Unknown; ++i)
            {
                if (unitsHitByKind[i] > 0) ++differentTypes;
                totalKilled += unitsHitByKind[i];
            }
            m_enemiesKilledBySpecial += totalKilled;
            bool fireGriffin = damageSource == UDS_Fireball || damageSource == UDS_GriffinSmash;
            if (m_type == CT_GriffinFireballMaster && differentTypes >= 3 && fireGriffin) completeChallenge();
            if (m_type == CT_GriffinFireballSuperMaster && totalKilled >= 5 && fireGriffin) completeChallenge();
            if (m_type == CT_ManyEnemiesWithSpecial && m_enemiesKilledBySpecial >= 30) completeChallenge();
        }

        public void specialWeaponUsed()
        {
            m_specialWeaponUsed = true;
            if (m_type == CT_FlickMaster) failChallenge();
        }

        public void hydraHasEatenUnit(int kind)
        {
            if ((m_type == CT_MenuGoblins && kind == UK_Goblin) || (m_type == CT_MenuShamans && kind == UK_Shaman) || (m_type == CT_MenuTrolls && kind == UK_Troll))
            {
                ++m_enemiesEaten;
                if (m_enemiesEaten >= 5) completeChallenge();
            }
            int[] unitsHitByKind = new int[UK_Unknown + 1];
            unitsHitByKind[kind] = 1;
            unitsWereKilledByWeapon(UDS_Petrify, unitsHitByKind);
        }

        public void comboAchieved(int kind)
        {
            if ((m_type == CT_ShamanCombo && kind == UK_Shaman) || (m_type == CT_TrollCombo && kind == UK_Troll)) completeChallenge();
            if (m_type == CT_ComboMaster && ++m_combos >= 5) completeChallenge();
        }

        void completeChallenge()
        {
            if (m_challengeComplete) return;
            m_failed = false;
            m_scrollingTitle = _TXT(STRING_IDS.CHALLENGES_TITLE_0 + m_type);
            m_scrollingTimer = 0.0f;
            m_challengeComplete = true;
            int reward = 100;
            IAttributes attr = __GAME_CONFIG().getAttributes("challenge", ChallengeTypeToString(m_type));
            if (attr != null && attr.existsAttribute("Reward")) reward = attr.getAttributeAsInt("Reward");
            CPlayfield.GetInstance().addMoney(0, reward, new v2f(240, 90), _TXT(STRING_IDS.INGAME_BONUS), 3.0f);
            CAchievements.GetInstance().setValue("Challenger", 1 << m_type);
        }

        void failChallenge()
        {
            if (m_challengeComplete) return;
            m_failed = true;
            m_scrollingTitle = _TXT(STRING_IDS.CHALLENGES_TITLE_0 + m_type);
            m_scrollingTimer = 0.0f;
            m_challengeComplete = true;
        }

        public string getDescription() { return _TXT(STRING_IDS.CHALLENGES_DESCRIPTION_0 + m_type); }
    }

    // ------------------------------------------------------------------------ unit kinds & parameters

    public class UnitParameters
    {
        public string soundsPackName;
        public int unitKind;
        public string modelId;
        public IAttributes modelAttr;
        public string chestDummy, dragDummy, energyBallDummy, modelFile;
        public float scale;
        public string shamanDieWaveModel, arrowMeshModel, arrowMeshDummy;
        public quaternion rotation = quaternion.Identity;
        public string[] animations = new string[UA_TotalCount];
        public float[] animationSpeeds = new float[UA_TotalCount];
        public float statDistanceBeforeFallAnim, statStumbleDistance;
        public string shadowModel, auraDummyNodeName, auraDummyEffectName;
        public float auraDummyScale;
        public List<string> textureObjects = new List<string>(), textureFiles = new List<string>();
        public float[] damageByDifficulty = new float[3], minSpeedByDifficulty = new float[3], maxSpeedByDifficulty = new float[3];
        public float minRange, maxRange, souls;
        public bool mustDragIntoGroundToKill, noDeathFromFastFall;
        public float health, chargeHealth, fallStun, fallDie;
        public bool thrownByBoar, isFreeBoar, isWildBoar, isMindControlled;
        public float gravity, maxFallSpeedY, charge, chargeWalkMin, chargeWalkMax;
        public bool fireImmunity, iceImmunity;
        public string mindControl, rider;

        static readonly string[] s_kinds = { "Goblin", "Boar", "BoarRider", "Shaman", "Troll", "Elf", "Skeleton", "Bomblin" };

        public static int StringToUnitKind(string s)
        {
            int i = Array.IndexOf(s_kinds, s);
            return i >= 0 ? i : UK_Unknown;
        }

        public static string UnitKindToString(int kind) { return kind >= 0 && kind < s_kinds.Length ? s_kinds[kind] : "Unknown"; }

        public void Init(string name, IAttributes attributes)
        {
            soundsPackName = attributes.getAttributeAsString("Sounds");
            unitKind = StringToUnitKind(attributes.getAttributeAsString("Kind"));
            modelId = attributes.getAttributeAsString("Model");
            modelAttr = CGameConfig.GetInstance().getAttributes("model", modelId) ?? new IAttributes();
            modelFile = modelAttr.getAttributeAsString("File");
            chestDummy = modelAttr.getAttributeAsString("ChestDummy");
            dragDummy = modelAttr.getAttributeAsString("DragDummy");
            energyBallDummy = modelAttr.getAttributeAsString("EnergyBallDummy");
            scale = modelAttr.getAttributeAsFloat("Scale");
            shamanDieWaveModel = modelAttr.getAttributeAsString("ShamanDieWave");
            arrowMeshModel = modelAttr.getAttributeAsString("ArrowMesh");
            arrowMeshDummy = modelAttr.getAttributeAsString("ArrowDummy");
            rotation = quaternion.Identity;
            string[] animAttr =
            {
                "Anim_Walk_Normal", "Anim_Walk_Injured", "Anim_Walk_Victory", "Anim_Walk_Fast", "Anim_Walk_Charge", "Anim_Dragged",
                "Anim_DraggedMindControl", "Anim_Falling", "Anim_Flying", "Anim_FlyingToFalling", "Anim_Stunned", "Anim_Stumble", "Anim_Attack",
                "Anim_Die_GroundFall", "Anim_Die_GroundSmash", "Anim_Die_WallHit", "Anim_Riding_Walk", "Anim_Riding_Attack", "Anim_Riding_Dragged",
                "Anim_Riding_Stunned", "Anim_Riding_Victory", "Anim_Turning", "Anim_MindControl", "Anim_Elf_WallGrab", "Anim_Elf_WallFall",
                "Anim_Elf_WallLand", "Anim_Elf_Jump", "Anim_Elf_UnhangHigh", "Anim_Elf_UnhangLow", "Anim_Elf_Slide", "Anim_Skeleton_Appear",
                "Anim_Skeleton_Land", "Anim_Earthquake"
            };
            for (int i = 0; i < UA_TotalCount; ++i) animations[i] = modelAttr.getAttributeAsString(animAttr[i]);
            string[] speedAttr =
            {
                "Speed_Walk", "Speed_Walk", "Speed_Walk", "Speed_WalkFast", "Speed_Charge", "Speed_Dragged", "Speed_Dragged", "Speed_Falling",
                "Speed_Flying", "Speed_FlyingToFalling", "Speed_Stunned", "Speed_Stumble", "Speed_Attack", "Speed_GroundFall", "Speed_Die", "Speed_Die",
                "Speed_Walk", "Speed_Riding_Attack", "Speed_Dragged", "Speed_Stunned", "Speed_Die", "Speed_Die", "Speed_MindControl",
                "Speed_Elf_WallGrab", "Speed_Elf_WallFall", "Speed_Elf_WallLand", "Speed_Elf_Jump", "Speed_Elf_UnhangHigh", "Speed_Elf_UnhangLow",
                "Speed_Elf_Slide", "Speed_Skeleton_Appear", "Speed_Skeleton_Land", "Speed_Earthquake"
            };
            for (int i = 0; i < UA_TotalCount; ++i) animationSpeeds[i] = GetFloatWithDefault(modelAttr, speedAttr[i], 1.0f);
            statDistanceBeforeFallAnim = GetFloatWithDefault(modelAttr, "DistanceBeforeFallAnim", 0.0f);
            statStumbleDistance = GetFloatWithDefault(modelAttr, "StumbleDistance", 0.0f);
            shadowModel = modelAttr.getAttributeAsString("Shadow");
            auraDummyNodeName = modelAttr.getAttributeAsString("AuraDummy");
            auraDummyEffectName = modelAttr.getAttributeAsString("AuraEffect");
            auraDummyScale = modelAttr.getAttributeAsFloat("AuraScale");
            for (int i = 1; i < 100; ++i)
            {
                string file = "TextureFile" + i, obj = "TextureObject" + i;
                if (!modelAttr.existsAttribute(file)) break;
                textureObjects.Add(modelAttr.getAttributeAsString(obj));
                textureFiles.Add(modelAttr.getAttributeAsString(file));
            }
            string[] damages = { "EasyDamage", "MediumDamage", "HardDamage" };
            string[] minSpeeds = { "EasyMinSpeed", "MediumMinSpeed", "HardMinSpeed" };
            string[] maxSpeeds = { "EasyMaxSpeed", "MediumMaxSpeed", "HardMaxSpeed" };
            for (int d = 0; d < 3; ++d)
            {
                damageByDifficulty[d] = attributes.getAttributeAsFloat(damages[d]);
                minSpeedByDifficulty[d] = attributes.getAttributeAsFloat(minSpeeds[d]);
                maxSpeedByDifficulty[d] = attributes.getAttributeAsFloat(maxSpeeds[d]);
            }
            minRange = attributes.getAttributeAsFloat("RangeMin");
            maxRange = attributes.getAttributeAsFloat("RangeMax");
            souls = attributes.getAttributeAsFloat("Souls");
            mustDragIntoGroundToKill = attributes.getAttributeAsBool("MustDragIntoGroundToKill");
            noDeathFromFastFall = attributes.getAttributeAsBool("NoDeathFromFastFall");
            health = attributes.getAttributeAsFloat("Health");
            chargeHealth = attributes.getAttributeAsFloat("ChargeHealth");
            fallStun = attributes.getAttributeAsFloat("FallStun");
            fallDie = attributes.getAttributeAsFloat("FallDie");
            thrownByBoar = !attributes.getAttributeAsBool("NotThrownByBoar");
            isFreeBoar = attributes.getAttributeAsBool("IsFreeBoar");
            isWildBoar = attributes.getAttributeAsBool("IsWildBoar");
            isMindControlled = attributes.getAttributeAsBool("IsMindControlled");
            gravity = GetFloatWithDefault(attributes, "Gravity", -9.8f * 8.0f * 0.9f);
            maxFallSpeedY = GetFloatWithDefault(attributes, "MaxFallSpeedY", 100.0f);
            charge = attributes.getAttributeAsFloat("Charge");
            chargeWalkMin = attributes.getAttributeAsFloat("ChargeWalkMin");
            chargeWalkMax = attributes.getAttributeAsFloat("ChargeWalkMax");
            fireImmunity = attributes.getAttributeAsBool("FireImmunity");
            iceImmunity = attributes.getAttributeAsBool("IceImmunity");
            mindControl = attributes.getAttributeAsString("MindControl");
            rider = attributes.getAttributeAsString("Rider");
        }

        static readonly Dictionary<string, UnitParameters> s_unitParameters = new Dictionary<string, UnitParameters>();

        public static void InitUnitParameters()
        {
            s_unitParameters.Clear();
            foreach (UnitType t in LevelInfo.g_unitTypes)
            {
                UnitParameters p = new UnitParameters();
                p.Init(t.name, t.attributes);
                s_unitParameters[t.name] = p;
            }
        }

        public static UnitParameters GetUnitParameters(string id)
        {
            UnitParameters p;
            return s_unitParameters.TryGetValue(id, out p) ? p : null;
        }
    }

    // ------------------------------------------------------------------------ level info

    public class EnemyType { public int unitType, minCount, maxCount; }
    public class Variant { public List<EnemyType> enemyTypes = new List<EnemyType>(); public bool intro; }

    public class Wave
    {
        public string name = "";
        public int timeStart, timeInterval, repeatInterval, repeatCount;
        public bool packed;
        public List<Variant> variants = new List<Variant>();
    }

    public class Day
    {
        public int dayEndTime;
        public int challenge;
        public int difficulty;
        public List<Wave> waves = new List<Wave>();
    }

    public class Level
    {
        public int[] weaponStartingLevel = new int[WT_Count - 1];
        public bool[] weaponAvailableInShop = new bool[WT_Count - 1];
        public int spellAvailableInShop = ST_None;
        public bool spellBought;
    }

    public class UnitType { public string name; public IAttributes attributes; }

    public static class LevelInfo
    {
        public static readonly List<UnitType> g_unitTypes = new List<UnitType>();
        static List<int> s_numberOfDaysPerLevel = new List<int>();

        /// <summary>Directory holding Levels/ (edited by the Level Editor tool).</summary>
        public static string LevelsRoot = null;

        static string LevelFile(string relative) { return LevelsRoot != null ? LevelsRoot + "/" + relative : relative; }

        // --- sequential reader helpers (same structure as the C++ readers)
        class Reader
        {
            readonly IXMLReader r;
            readonly Stack<string> stack = new Stack<string>();
            public Reader(IXMLReader reader) { r = reader; }

            public void BeginElement(string element)
            {
                stack.Push(element);
                while (r.read()) if (r.getNodeType() == EXML_NODE.EXN_ELEMENT && r.getNodeName() == element) break;
            }

            public bool BeginElementOrEndParent(string element)
            {
                while (r.read())
                {
                    if (r.getNodeType() == EXML_NODE.EXN_ELEMENT && r.getNodeName() == element) { stack.Push(element); return true; }
                    if (r.getNodeType() == EXML_NODE.EXN_ELEMENT_END && stack.Count > 0 && r.getNodeName() == stack.Peek()) { stack.Pop(); return false; }
                }
                return false;
            }

            void Seek(string element) { while (r.read()) if (r.getNodeType() == EXML_NODE.EXN_ELEMENT && r.getNodeName() == element) break; }
            public int Int(string element) { Seek(element); return r.getAttributeValueAsInt("value"); }
            public string Str(string element) { Seek(element); return r.getAttributeValueSafe("value"); }
            public bool Bool(string element) { Seek(element); return r.getAttributeValueSafe("value") == "true"; }

            public void EndElement()
            {
                string top = stack.Count > 0 ? stack.Peek() : null;
                while (r.read()) if (r.getNodeType() == EXML_NODE.EXN_ELEMENT_END && r.getNodeName() == top) break;
                if (stack.Count > 0) stack.Pop();
            }
        }

        class Writer
        {
            public readonly IXMLWriter w;
            readonly Stack<string> stack = new Stack<string>();
            public Writer(string path) { w = new IXMLWriter(path, true); w.writeXMLHeader(); w.writeLineBreak(); }
            void Indent() { for (int i = 0; i < stack.Count; i++) w.writeText("\t"); }
            public void Begin(string e) { Indent(); stack.Push(e); w.writeElement(e); w.writeLineBreak(); }
            public void Simple(string e, int v) { Indent(); w.writeElement(e, true, "value", v.ToString()); w.writeLineBreak(); }
            public void Simple(string e, string v) { Indent(); w.writeElement(e, true, "value", v); w.writeLineBreak(); }
            public void Simple(string e, bool v) { Indent(); w.writeElement(e, true, "value", v ? "true" : "false"); w.writeLineBreak(); }
            public void End() { string e = stack.Pop(); Indent(); w.writeClosingTag(e); w.writeLineBreak(); }
            public bool Close() { return w.close(); }
        }

        public static void initUnitTypes()
        {
            CGameConfig gc = CGameConfig.GetInstance();
            foreach (string f in new[] { "models", "units", "sounds", "weapons", "gates", "spells", "sounds_config", "challenge_rewards" })
                gc.importFile("config/" + f + ".xml");
            g_unitTypes.Clear();
            foreach (var kv in gc.getAttributesMap("unit")) g_unitTypes.Add(new UnitType { name = kv.Key, attributes = kv.Value });
            UnitParameters.InitUnitParameters();
        }

        public static int stringToUnitType(string str)
        {
            for (int i = 0; i < g_unitTypes.Count; ++i) if (g_unitTypes[i].name == str) return i;
            return -1;
        }

        public static string unitTypeToString(int t) { return g_unitTypes[t].name; }
        public static string getDayPath(int level, int day) { return string.Format("Levels/level{0:00}-day{1:00}.xml", level + 1, day + 1); }
        public static string getLevelPath(int level) { return string.Format("Levels/level{0:00}-info.xml", level + 1); }

        public static Day loadDayInfo(string path)
        {
            IXMLReader xml = IXMLReader.FromFile(LevelFile(path));
            if (xml == null) return null;
            Reader r = new Reader(xml);
            Day level = new Day();
            r.BeginElement("level");
            level.dayEndTime = r.Int("dayEndTime");
            level.challenge = Challenge.StringToChallengeType(r.Str("challenge"));
            level.difficulty = r.Int("difficulty");
            r.BeginElement("waves");
            while (r.BeginElementOrEndParent("wave"))
            {
                Wave wave = new Wave();
                wave.name = r.Str("name");
                wave.timeStart = r.Int("timeStart");
                wave.timeInterval = r.Int("timeInterval");
                wave.repeatInterval = r.Int("repeatInterval");
                wave.repeatCount = r.Int("repeatCount");
                wave.packed = r.Bool("packed");
                while (r.BeginElementOrEndParent("variant"))
                {
                    Variant var = new Variant();
                    var.intro = r.Bool("intro");
                    while (r.BeginElementOrEndParent("enemy"))
                    {
                        EnemyType enemy = new EnemyType();
                        string type = r.Str("type");
                        for (int i = 0; i < g_unitTypes.Count; ++i) if (g_unitTypes[i].name == type) { enemy.unitType = i; break; }
                        enemy.minCount = r.Int("min");
                        enemy.maxCount = r.Int("max");
                        var.enemyTypes.Add(enemy);
                        r.EndElement();
                    }
                    wave.variants.Add(var);
                }
                level.waves.Add(wave);
            }
            r.EndElement();
            return level;
        }

        public static bool sameDay(Day a, Day b)
        {
            if (a.dayEndTime != b.dayEndTime || a.challenge != b.challenge || a.difficulty != b.difficulty || a.waves.Count != b.waves.Count) return false;
            for (int i = 0; i < a.waves.Count; ++i)
            {
                Wave x = a.waves[i], y = b.waves[i];
                if (x.name != y.name || x.timeStart != y.timeStart || x.timeInterval != y.timeInterval || x.repeatInterval != y.repeatInterval ||
                    x.repeatCount != y.repeatCount || x.packed != y.packed || x.variants.Count != y.variants.Count) return false;
                for (int j = 0; j < x.variants.Count; ++j)
                {
                    Variant v = x.variants[j], w = y.variants[j];
                    if (v.intro != w.intro || v.enemyTypes.Count != w.enemyTypes.Count) return false;
                    for (int k = 0; k < v.enemyTypes.Count; ++k)
                    {
                        EnemyType e = v.enemyTypes[k], f = w.enemyTypes[k];
                        if (e.unitType != f.unitType || e.minCount != f.minCount || e.maxCount != f.maxCount) return false;
                    }
                }
            }
            return true;
        }

        public static void saveDayInfo(string path, Day level)
        {
            Day saved = loadDayInfo(path);
            if (saved != null && sameDay(saved, level)) return;
            Writer w = new Writer(GameFiles.Full(LevelFile(path)));
            w.Begin("level");
            w.Simple("dayEndTime", level.dayEndTime);
            w.Simple("challenge", Challenge.ChallengeTypeToString(level.challenge));
            w.Simple("difficulty", level.difficulty);
            w.Begin("waves");
            foreach (Wave wave in level.waves)
            {
                w.Begin("wave");
                w.Simple("name", wave.name);
                w.Simple("timeStart", wave.timeStart);
                w.Simple("timeInterval", wave.timeInterval);
                w.Simple("repeatInterval", wave.repeatInterval);
                w.Simple("repeatCount", wave.repeatCount);
                w.Simple("packed", wave.packed);
                foreach (Variant var in wave.variants)
                {
                    w.Begin("variant");
                    w.Simple("intro", var.intro);
                    foreach (EnemyType enemy in var.enemyTypes)
                    {
                        w.Begin("enemy");
                        w.Simple("type", g_unitTypes[enemy.unitType].name);
                        w.Simple("min", enemy.minCount);
                        w.Simple("max", enemy.maxCount);
                        w.End();
                    }
                    w.End();
                }
                w.End();
            }
            w.End();
            w.End();
            w.Close();
        }

        public static Level loadLevelInfo(string path)
        {
            Level result = new Level();
            for (int i = 0; i < WT_Count - 1; ++i) { result.weaponAvailableInShop[i] = true; result.weaponStartingLevel[i] = WL_None; }
            IXMLReader xml = IXMLReader.FromFile(LevelFile(path));
            if (xml == null) return result;
            Reader r = new Reader(xml);
            r.BeginElement("level");
            for (int i = 0; i < WT_Count - 1; ++i)
            {
                result.weaponStartingLevel[i] = r.Int("weapon-" + (i + 1) + "-starting-level");
                result.weaponAvailableInShop[i] = r.Bool("weapon-" + (i + 1) + "-available-in-shop");
            }
            result.spellAvailableInShop = r.Int("spell-available");
            result.spellBought = r.Bool("spell-bought");
            r.EndElement();
            return result;
        }

        public static bool sameLevel(Level a, Level b)
        {
            if (a.spellAvailableInShop != b.spellAvailableInShop || a.spellBought != b.spellBought) return false;
            for (int i = 0; i < WT_Count - 1; ++i)
                if (a.weaponStartingLevel[i] != b.weaponStartingLevel[i] || a.weaponAvailableInShop[i] != b.weaponAvailableInShop[i]) return false;
            return true;
        }

        public static void saveLevelInfo(string path, Level level)
        {
            if (GameFiles.Exists(LevelFile(path)) && sameLevel(loadLevelInfo(path), level)) return;
            Writer w = new Writer(GameFiles.Full(LevelFile(path)));
            w.Begin("level");
            for (int i = 0; i < WT_Count - 1; ++i)
            {
                w.Simple("weapon-" + (i + 1) + "-starting-level", level.weaponStartingLevel[i]);
                w.Simple("weapon-" + (i + 1) + "-available-in-shop", level.weaponAvailableInShop[i]);
            }
            w.Simple("spell-available", level.spellAvailableInShop);
            w.Simple("spell-bought", level.spellBought);
            w.End();
            w.Close();
        }

        public static List<int> loadNumberOfDaysPerLevel()
        {
            List<int> result = new List<int>();
            IXMLReader xml = IXMLReader.FromFile(LevelFile("Levels/info.xml"));
            if (xml == null) return result;
            Reader r = new Reader(xml);
            r.BeginElement("levels");
            while (r.BeginElementOrEndParent("level"))
            {
                result.Add(r.Int("days"));
                r.EndElement();
            }
            return result;
        }

        public static void saveNumberOfDaysPerLevel(List<int> arr)
        {
            s_numberOfDaysPerLevel = new List<int>(arr);
            List<int> saved = loadNumberOfDaysPerLevel();
            bool unchanged = arr.Count == saved.Count;
            for (int i = 0; unchanged && i < arr.Count; ++i) unchanged = arr[i] == saved[i];
            if (unchanged && GameFiles.Exists(LevelFile("Levels/info.xml"))) return;
            Writer w = new Writer(GameFiles.Full(LevelFile("Levels/info.xml")));
            w.Begin("levels");
            foreach (int d in arr) { w.Begin("level"); w.Simple("days", d); w.End(); }
            w.End();
            w.Close();
        }

        public static List<int> getNumberOfDaysPerLevel()
        {
            if (s_numberOfDaysPerLevel.Count == 0) s_numberOfDaysPerLevel = loadNumberOfDaysPerLevel();
            return s_numberOfDaysPerLevel;
        }

        public static void ReloadNumberOfDaysPerLevel() { s_numberOfDaysPerLevel = loadNumberOfDaysPerLevel(); }

        public static Wave cloneWave(Wave wave)
        {
            Wave c = new Wave { name = wave.name, timeStart = wave.timeStart, timeInterval = wave.timeInterval, repeatInterval = wave.repeatInterval, repeatCount = wave.repeatCount, packed = wave.packed };
            foreach (Variant v in wave.variants)
            {
                Variant nv = new Variant { intro = v.intro };
                foreach (EnemyType e in v.enemyTypes) nv.enemyTypes.Add(new EnemyType { unitType = e.unitType, minCount = e.minCount, maxCount = e.maxCount });
                c.variants.Add(nv);
            }
            return c;
        }

        public static Day cloneDay(Day level)
        {
            Day r = new Day { dayEndTime = level.dayEndTime, challenge = level.challenge, difficulty = level.difficulty };
            foreach (Wave w in level.waves) r.waves.Add(cloneWave(w));
            return r;
        }

        public static int appendDayToDay(Day inOut, int timeOffset, Day levelToAppend)
        {
            foreach (Wave w in levelToAppend.waves)
            {
                Wave wave = cloneWave(w);
                wave.timeStart += timeOffset;
                inOut.waves.Add(wave);
            }
            return timeOffset + getDayMaxTime(levelToAppend);
        }

        public static int getDayMaxTime(Day level)
        {
            int time = 0;
            foreach (Wave wave in level.waves)
                for (int j = 0; j < wave.variants.Count; ++j)
                    time = Math.Max(time, wave.timeStart + wave.timeInterval + wave.repeatInterval * (wave.repeatCount - 1));
            return time + 1;
        }
    }
}
