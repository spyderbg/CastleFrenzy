// Game/Playfield: the in-game world (castle, waves, units, weapons, HUD, music/sfx arbitration, cutscenes).
using System.Collections.Generic;
using static CastleFrenzy.G;
using static CastleFrenzy.GE;
using static CastleFrenzy.Ids;
using static CastleFrenzy.LevelInfo;

namespace CastleFrenzy
{
    public class SEffect { public ASpriteInstance inst; public v3f pos; }
    public class SMoney { public int amount, bonus; public v2f position; public string text; public float time; }
    public class SSouls { public int amount; public v3f start; public float time, duration; public ASpriteInstance inst; }

    public struct SUnitAtTime
    {
        public int type;
        public float time;
        public int slaves, riders;
        public bool intro;
    }

    public struct UnitTypeAndId
    {
        public int unitType;
        public float time;
        public int id;
    }

    public class CPlayfield
    {
        static CPlayfield s_instance;
        public static CPlayfield GetInstance() { return s_instance ?? (s_instance = new CPlayfield()); }

        public const int PS_None = 0, PS_CutScene = 1, PS_IntroUnitCamera = 2, PS_Begin = 3, PS_Play = 4, PS_Pause = 5, PS_Restart = 6,
            PS_MissionOverTimer = 7, PS_MissionOver = 8, PS_Shop = 9, PS_AfterInterrupt = 10, PS_EnterIGM = 11, PS_IGM = 12, PS_ExitIGM = 13, PS_Num = 14;
        public const int CS_Intro = 0, CS_Hydra = 1, CS_Hydra3Heads = 2, CS_Griffin = 3, CS_HydraGriffin = 4, CS_Count = 5, CS_Invalid = -1;

        const float k_dayTextEndTime = 3.0f;
        const float k_dayChallengeTextEndTime = 6.0f;
        const int k_maxPlayingUnitSounds = 8;
        const int k_defaultSfxPriority = 3;
        const int k_defaultSfxOverlap = 2;
        const float k_defaultSfxVolume = 1.0f;
        const int k_sfxFadeoutTimer = 500;

        float _time;
        public readonly List<UnitTypeAndId> m_extraSpawns = new List<UnitTypeAndId>();
        public float m_damage;
        int m_difficulty;
        public int m_kills;
        public int m_comboUnitKind;
        readonly int[] m_unitsInLane = new int[8];
        float m_introTime;
        CUnit m_introUnit;
        v3f m_introUnitStartPos;
        quaternion m_introUnitStartRot;
        CTouchElement m_backTouch;
        bool m_introGoingBack;
        bool m_unitHoveredOverHydra;
        float m_unitHoveredTimer;
        readonly List<CUnit> m_units = new List<CUnit>();
        line3df m_path;
        plane3df m_castlePlane, m_flyingPlane, m_groundPlane, m_outOfScreenPlane;
        int m_lastDamageSource;
        int m_currentBonus;
        bool m_playChallengePopupSound = true;
        readonly Challenge m_challenge = new Challenge();
        bool m_unitDragged;
        float m_cameraShakeTimer, m_cameraShakePower;
        int m_cameraShakeX, m_cameraShakeY;
        CCameraSceneNode m_camera;
        ISceneNode m_castle, m_cameraScene, m_castleBackground, m_castleForeground;
        CCastleGate m_gate;
        CHydra m_hydra;
        CFireball m_fireball;
        CIceball m_iceball;
        CGriffin m_griffin;
        CSpell m_spell;
        ASprite m_spriteSparks, m_spriteDust, m_spriteDie;
        readonly List<SMoney> m_flyingMoney = new List<SMoney>();
        readonly List<SSouls> m_flyingSouls = new List<SSouls>();
        readonly List<SEffect> m_effects = new List<SEffect>();
        readonly List<CProjectile> m_projectiles = new List<CProjectile>();
        readonly List<CBomb> m_bombs = new List<CBomb>();
        ASprite m_sprIngame, m_sprStonedUnits, m_sprIcedUnits, m_sprEffects, m_sprDizzy;
        ASpriteInstance m_playPauseIcon;
        int m_gold;
        float m_slowGold;
        int m_souls;
        float m_slowSouls;
        float m_hudAlphaTarget, m_hudAlpha;
        float m_time;
        readonly List<SUnitAtTime> m_waves = new List<SUnitAtTime>();
        int m_wavePos;
        float m_missionOverCountdown;
        readonly CLevelStats m_levelStats = new CLevelStats();
        float m_dayTextTime;
        int m_fadeDir;
        float m_fadeDuration, m_fadeTimer;
        int m_fadeAlpha;
        public bool m_lightball;
        int m_nextCutscene = CS_Intro;
        bool m_MusicPause;
        int m_CurrentMusic = -1, m_PrevMusic = -1, m_NextMusic = -1;
        bool m_NextMusicLoop;
        public int m_unlockLevelEasy, m_unlockLevelNormal, m_unlockLevelHard;
        public int m_CurrentLevel, m_CurrentDay;
        public bool m_EnterInShop;
        public bool m_isEndless;
        int m_LoadingProgress;
        int m_MaxUnits;
        float m_DayTime = -1.0f;
        CLightSceneNode m_SceneLight;
        int m_PlayState = PS_None;
        bool m_MissionSuccess, m_isLevelOver;

        class SSfxInfo
        {
            public int id, numPlayingInstances, priority, maxPlayingInstances;
            public readonly List<int> soundIndices = new List<int>();
            public float volume;
        }

        readonly SortedDictionary<int, SSfxInfo> m_sfxList = new SortedDictionary<int, SSfxInfo>();
        int m_totalPlayingInstances;
        int m_gameNumber;
        CTouchElement m_touch;
        bool m_firstTouch, m_secondTouch, m_isNowActivated, m_isNowReleased;
        v3f m_cameraPosition;

        CPlayfield()
        {
            m_LoadingProgress = 0;
            fadeIn(0);
        }

        // ------------------------------------------------------------------ accessors

        public bool isValid() { return m_castle != null; }
        public plane3df getCastlePlane() { return m_castlePlane; }
        public plane3df getFlyingPlane() { return m_flyingPlane; }
        public plane3df getGroundPlane() { return m_groundPlane; }
        public plane3df getOutOfScreenPlane() { return m_outOfScreenPlane; }
        public line3df getPath() { return m_path; }
        public List<CUnit> getUnits() { return m_units; }
        public CLevelStats GetLevelStats() { return m_levelStats; }
        public CCastleGate getGate() { return m_gate; }
        public CHydra getHydra() { return m_hydra; }
        public CFireball getFireball() { return m_fireball; }
        public CIceball getIceball() { return m_iceball; }
        public CGriffin getGriffin() { return m_griffin; }
        public CSpell getSpell() { return m_spell; }
        public CCameraSceneNode getCamera() { return m_camera; }
        public Challenge getChallenge() { return m_challenge; }
        public ASprite getSpriteIngame() { return m_sprIngame; }
        public ASprite getSpriteStonedUnits() { return m_sprStonedUnits; }
        public ASprite getSpriteIcedUnits() { return m_sprIcedUnits; }
        public ASprite getSpriteEffects() { return m_sprEffects; }
        public ASprite getSpriteDizzy() { return m_sprDizzy; }
        public int getDifficulty() { return m_isEndless ? GD_Hard : m_difficulty; }
        public void setDifficulty(int difficulty) { m_difficulty = difficulty; }
        public bool isChallengeVisible() { return m_challenge.isVisible(); }
        public bool isFading() { return m_fadeDir != 0; }
        public int GetProgress() { return m_LoadingProgress; }
        public bool IsLevelLoaded() { return GetProgress() >= 102; }
        public float GetDayTime() { return m_DayTime; }
        public CLightSceneNode GetSunLight() { return m_SceneLight; }
        public bool isPaused() { int s = GetPlayState(); return s == PS_Pause || s == PS_EnterIGM || s == PS_IGM || s == PS_ExitIGM; }
        public bool IsMissionOver() { return GetPlayState() > PS_MissionOverTimer; }
        public bool IsMissionSuccess() { return m_MissionSuccess; }
        public bool IsLevelOver() { return m_isLevelOver; }
        public int GetPlayState() { return m_PlayState; }
        public bool isFirstGame() { return m_gameNumber == 0; }
        public float getTime() { return m_time; }
        public List<SUnitAtTime> getWaves() { return m_waves; }
        public int getWavePos() { return m_wavePos; }
        public List<CBomb> getBombs() { return m_bombs; }
        public int GetMaxUnits() { return m_MaxUnits; }
        public void SetNextCutscene(int cutScene) { m_nextCutscene = cutScene; }

        public v3f GetSunPos() { return m_SceneLight == null ? v3f.Zero : m_SceneLight.getPosition(); }

        // ------------------------------------------------------------------ projectiles / combos

        public void ClearProjectiles(bool exit_level = false)
        {
            foreach (CProjectile p in m_projectiles) p.Dispose();
            m_projectiles.Clear();
            List<CBomb> temp = new List<CBomb>();
            foreach (CBomb b in m_bombs)
            {
                if (!b.isForHelpMenu() || exit_level) b.Dispose();
                else temp.Add(b);
            }
            m_bombs.Clear();
            m_bombs.AddRange(temp);
        }

        public bool checkCombo(int damageSource, int kind, out int money, out string text)
        {
            m_challenge.unitDied(damageSource, kind);
            bool combo = false;
            money = 0;
            text = null;
            if ((m_lastDamageSource == UDS_GroundDrag && damageSource == UDS_WallHit) || (m_lastDamageSource == UDS_WallHit && damageSource == UDS_GroundDrag))
            {
                if (m_comboUnitKind != kind) m_comboUnitKind = UK_Unknown;
                m_currentBonus += 2;
                money = m_currentBonus;
                if (m_currentBonus >= 8)
                {
                    combo = true;
                    text = _TXT(STRING_IDS.INGAME_COMBO);
                    m_currentBonus = 0;
                }
            }
            else
            {
                m_currentBonus = 0;
                if (damageSource == UDS_GroundDrag || damageSource == UDS_WallHit) m_comboUnitKind = kind;
            }
            m_lastDamageSource = damageSource;
            return combo;
        }

        public void SetCameraShake(float time, float power)
        {
            m_cameraShakeTimer = time;
            m_cameraShakePower = power;
        }

        public void pauseUnits()
        {
            foreach (CUnit u in m_units) u.freezeAnimation(true);
            Timeline(m_castle).setScale(0.0f);
            m_hydra.freezeAnimation(true);
            m_fireball.freezeAnimation(true);
            m_iceball.freezeAnimation(true);
            m_griffin.freezeAnimation(true);
            m_spell.freezeAnimation(true);
            foreach (ParticleSystem p in ParticleManager.m_particleSystemList) p.Pause();
            m_playPauseIcon.SetAnim(MENU_A_MENU_PAUSE_PLAY);
        }

        public void resumeUnits()
        {
            foreach (CUnit u in m_units) u.freezeAnimation(false);
            Timeline(m_castle).setScale(1.0f);
            m_hydra.freezeAnimation(false);
            m_fireball.freezeAnimation(false);
            m_iceball.freezeAnimation(false);
            m_griffin.freezeAnimation(false);
            m_spell.freezeAnimation(false);
            foreach (ParticleSystem p in ParticleManager.m_particleSystemList) p.UnPause();
            m_playPauseIcon.SetAnim(MENU_A_MENU_PLAY);
        }

        public void pause() { PauseMusic(); }

        public void resume()
        {
            if (GetPlayState() == PS_Play) SetPlayState(PS_AfterInterrupt);
            else if (GetPlayState() != PS_IGM || (GSLevel.g_level != null && GSLevel.g_level.GetInGameMenu().SubMenu_Is("Menu_SoundOptions"))) ResumeMusic();
        }

        bool isPauseButtonPressed()
        {
            if (!isNowActivatedTouch()) return false;
            int px, py;
            getTouch().GetTouchPos_Current(out px, out py);
            if (new r2i(new p2i(0, 320 - 50), new d2i(50, 50)).isPointInside(new p2i(px, py)))
            {
                getTouch().ResetInput();
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ mission flow

        void SuccessMission()
        {
            m_MissionSuccess = true;
            CGameConfig.RenderToTexture = false;
            m_challenge.levelEnded(m_gate.getHealthRatio() >= 1.0f, m_gate.getHealthRatio() <= 0.05f, true);
            m_missionOverCountdown = 3.0f;
            List<int> daysPerLevel = getNumberOfDaysPerLevel();
            if (m_CurrentLevel == daysPerLevel.Count - 2 && m_CurrentDay == daysPerLevel[m_CurrentLevel] - 1)
            {
                CTimelineController cameraControl = Timeline(m_cameraScene);
                cameraControl.setClip("victory_outro");
                cameraControl.setLoop(false);
            }
            if (m_CurrentDay == daysPerLevel[m_CurrentLevel] - 1) SetLevelScores(GSLevel.g_level.GetInGameMenu());
            SetPlayState(PS_MissionOverTimer);
        }

        void FaildMission()
        {
            m_MissionSuccess = false;
            CGameConfig.RenderToTexture = false;
            m_challenge.levelEnded(m_gate.getHealthRatio() >= 1.0f, m_gate.getHealthRatio() <= 0.05f, false);
            m_missionOverCountdown = 3.0f;
            CTimelineController cameraControl = Timeline(m_cameraScene);
            cameraControl.setClip("defeat_outro");
            cameraControl.setLoop(false);
            SetPlayState(PS_MissionOverTimer);
        }

        void Update_MissionOverCheck()
        {
            if (m_gate.isDestroyed()) FaildMission();
            else if (m_wavePos >= m_waves.Count && m_units.Count == 0) SuccessMission();
        }

        void Update_MissionOverTimer()
        {
            m_missionOverCountdown -= _time;
            if (m_missionOverCountdown > 0) return;
            SetPlayState(PS_MissionOver);
            if (!IsMissionSuccess()) return;
            CAchievements.GetInstance().setValue("Cash & Carry", m_gold);
            m_levelStats.Gold += m_gold;
            m_levelStats.Souls += m_souls;
            m_gold = 0;
            m_souls = 0;
            m_slowGold = 0.0f;
            m_slowSouls = 0.0f;
            List<int> daysPerLevel = getNumberOfDaysPerLevel();
            int levelsNum = daysPerLevel.Count - 1;
            m_CurrentDay++;
            if (!m_isEndless && m_CurrentDay >= daysPerLevel[m_CurrentLevel])
            {
                m_CurrentLevel++;
                if (!CGameConfig.UnlockAllLevels)
                {
                    if (m_CurrentLevel >= levelsNum)
                    {
                        SetUnlockLevel(m_difficulty, levelsNum);
                        if (m_difficulty == GD_Easy) CAchievements.GetInstance().setAchieved("Recruit");
                        if (m_difficulty == GD_Normal) CAchievements.GetInstance().setAchieved("Soldier");
                        if (m_difficulty == GD_Hard) CAchievements.GetInstance().setAchieved("True Defender");
                    }
                    else if (m_CurrentLevel > getUnlockLevel(m_difficulty)) SetUnlockLevel(m_difficulty, m_CurrentLevel);
                }
                m_isLevelOver = true;
                if (m_CurrentLevel >= levelsNum)
                {
                    m_CurrentLevel = -1;
                    m_CurrentDay = -1;
                    CUserProfile profile = __PROFILE();
                    if (profile.GetInt("GameCompletedState") == 0) profile.Set("GameCompletedState", 1);
                }
                else m_CurrentDay = 0;
                WriteProfileStats();
            }
            WriteProfileStats();
        }

        void Update_Shop()
        {
            SetPlayState(IsMissionOver() ? PS_MissionOver : PS_Play);
        }

        // ------------------------------------------------------------------ update

        void _gameUpdate(float time)
        {
            if (isPaused()) return;
            m_time += time;
            if (m_time >= -k_dayChallengeTextEndTime) m_dayTextTime += time;
            m_challenge.update(time);
            UpdateDayTime();
            if (m_cameraShakeTimer > 0.0f)
            {
                m_cameraShakeTimer -= time;
                m_camera.setPosition(m_cameraPosition + randomVectorInSphere(m_cameraShakePower));
                m_cameraShakeX = RandInt(-20, 20);
                m_cameraShakeY = RandInt(-20, 20);
            }
            else
            {
                m_cameraShakeTimer = 0.0f;
                m_camera.setPosition(m_cameraPosition);
                m_cameraShakeX = 0;
                m_cameraShakeY = 0;
            }
            for (int i = 0; i < m_projectiles.Count; ++i) m_projectiles[i].update(time);
            for (int i = m_projectiles.Count - 1; i >= 0; --i)
                if (m_projectiles[i].isDead()) { m_projectiles[i].Dispose(); m_projectiles.RemoveAt(i); }
            for (int i = 0; i < m_bombs.Count; ++i) m_bombs[i].update(time);
            for (int i = m_bombs.Count - 1; i >= 0; --i)
                if (m_bombs[i].isDead()) { m_bombs[i].Dispose(); m_bombs.RemoveAt(i); }
            updateEffects();
            updateWaves();
            if (isNowActivatedTouch() && m_spell.canStartCast())
            {
                int px, py;
                getTouch().GetTouchPos_Current(out px, out py);
                if (new r2i(90, 25, 170, 110).isPointInside(new p2i(px, py))) m_spell.startCast(getTouch());
            }
            if (!isActiveTouch()) m_unitDragged = false;
            CUnit unitDragged = null;
            for (int i = 0; i < m_units.Count;)
            {
                CUnit unit = m_units[i];
                if ((unitDragged == null || unit.getDragPriority() > unitDragged.getDragPriority()) &&
                    isActiveTouch() && !IsMissionOver() && !m_spell.isCasting() && checkUnitTouch(unit) &&
                    ((unit.isDragable() && getTouch().GetCurrentTouchDuration() < 1000) || unit.isPetrified()))
                    unitDragged = unit;
                unit.update(time);
                if (unit.getState() == US_Dead)
                {
                    if (unit.m_lane >= 0) freeLane(unit.m_lane);
                    unit.Dispose();
                    if (unitDragged == unit) unitDragged = null;
                    int idx = m_units.IndexOf(unit);
                    if (idx >= 0) m_units.RemoveAt(idx);
                    if (idx >= 0 && idx < i) i--;
                }
                else ++i;
            }
            if (unitDragged != null && !m_unitDragged)
            {
                if (unitDragged.isDragable() && getTouch().GetCurrentTouchDuration() < 1000)
                {
                    unitDragged.startDrag(getTouch());
                    m_unitDragged = true;
                }
                else if (unitDragged.isPetrified())
                {
                    unitDragged.unpetrify(true);
                    m_unitDragged = true;
                }
            }
            if (!m_unitHoveredOverHydra) m_unitHoveredTimer = 0.0f;
            m_unitHoveredOverHydra = false;
            if (!m_unitDragged && !m_spell.isCasting() && isNowActivatedTouch() && m_PlayState == PS_Play && m_time >= 0)
            {
                int px, py;
                getTouch().GetTouchPos_Current(out px, out py);
                p2i pos = new p2i(px, py);
                if (m_hydra.getTouch().IsPointInside(pos)) reassignTouchTo(m_hydra.getTouch());
                if (m_fireball.getTouch().IsPointInside(pos)) reassignTouchTo(m_fireball.getTouch());
                else if (m_iceball.getTouch().IsPointInside(pos)) reassignTouchTo(m_iceball.getTouch());
                else if (m_griffin.getTouch().IsPointInside(pos)) reassignTouchTo(m_griffin.getTouch());
            }
            m_griffin.update(time);
            m_fireball.update(time);
            m_iceball.update(time);
            m_hydra.update(time);
            m_gate.update(time);
            m_spell.update(time);
            ParticleManager.UpdateParticleSystem(time);
            if (m_slowGold < m_gold)
            {
                float speed = MAX(3.0f * (m_gold - m_slowGold), 15.0f);
                m_slowGold += speed * time;
                if (m_slowGold > m_gold) m_slowGold = m_gold;
            }
            if (m_slowSouls < m_souls)
            {
                float speed = MAX(m_souls - m_slowSouls, 3.0f);
                m_slowSouls += speed * time;
                if (m_slowSouls > m_souls) m_slowSouls = m_souls;
            }
        }

        static void MoveToTarget(ref float value, float target, float speed, float dt)
        {
            if (value < target) { value += speed * dt; if (value > target) value = target; }
            else if (value > target) { value -= speed * dt; if (value < target) value = target; }
        }

        public void update(float time)
        {
            _time = time;
            MoveToTarget(ref m_hudAlpha, m_hudAlphaTarget, 2.0f, time);
            __IRR_SCENE().setActiveCamera(m_camera);
            getTouch().SetScreenCoordinates(0, 0, GAME_SCREEN_W, GAME_SCREEN_H);
            updateTouch();
            m_playPauseIcon.Update(_time);
            switch (m_PlayState)
            {
                case PS_Begin:
                    m_hydra.reset();
                    m_fireball.reset();
                    m_iceball.reset();
                    m_griffin.reset();
                    m_spell.reset();
                    StartCutscene();
                    fadeIn(1.0f);
                    m_playChallengePopupSound = true;
                    break;
                case PS_Restart:
                    _gameUpdate(time);
                    if (!isFading()) SetPlayState(PS_Begin);
                    break;
                case PS_CutScene: _gameUpdate(time); break;
                case PS_IntroUnitCamera: updateIntroUnitCamera(time); break;
                case PS_Play:
                    _gameUpdate(time);
                    if (isPauseButtonPressed()) SetPlayState(PS_EnterIGM);
                    else Update_MissionOverCheck();
                    break;
                case PS_MissionOverTimer:
                    _gameUpdate(time);
                    Update_MissionOverTimer();
                    break;
                case PS_AfterInterrupt: SetPlayState(PS_EnterIGM); break;
                case PS_EnterIGM: SetPlayState(PS_IGM); break;
                case PS_ExitIGM: SetPlayState(PS_Play); break;
                case PS_MissionOver: _gameUpdate(time); break;
                case PS_Shop: Update_Shop(); break;
            }
            UpdateFade(time);
            if (m_PlayState != PS_Shop) UpdateMusic();
            UpdateSfx();
        }

        bool checkUnitTouch(CUnit unit)
        {
            r2i r = new r2i(-45, -25, 25, 45);
            if (unit.getUnitKind() == UK_Shaman) r = new r2i(-35, -45, 35, 45);
            else if (unit.getUnitKind() == UK_Elf || unit.getUnitKind() == UK_Troll) r = new r2i(-45, -45, 25, 45);
            else if (unit.getUnitKind() == UK_Boar) r = new r2i(-45, -25, 35, 45);
            r = r + unit.getScreenPosition();
            int x2, y2;
            getTouch().GetTouchPos_Current(out x2, out y2);
            p2i p2 = new p2i(x2, y2);
            if (isNowActivatedTouch()) return r.isPointInside(p2);
            int x1, y1;
            getTouch().GetTouchPos_Prev(out x1, out y1);
            p2i p1 = new p2i(x1, y1);
            if (r.isPointInside(p1) || r.isPointInside(p2)) return true;
            return SegmentsIntersect(p1, p2, r.UpperLeftCorner, r.LowerRightCorner) ||
                   SegmentsIntersect(p1, p2, new p2i(r.UpperLeftCorner.X, r.LowerRightCorner.Y), new p2i(r.LowerRightCorner.X, r.UpperLeftCorner.Y));
        }

        /// <summary>line2di::intersectWith for two segments.</summary>
        static bool SegmentsIntersect(p2i a1, p2i a2, p2i b1, p2i b2)
        {
            float commonDenominator = (float)(b2.Y - b1.Y) * (a2.X - a1.X) - (float)(b2.X - b1.X) * (a2.Y - a1.Y);
            float numeratorA = (float)(b2.X - b1.X) * (a1.Y - b1.Y) - (float)(b2.Y - b1.Y) * (a1.X - b1.X);
            float numeratorB = (float)(a2.X - a1.X) * (a1.Y - b1.Y) - (float)(a2.Y - a1.Y) * (a1.X - b1.X);
            if (commonDenominator == 0.0f) return numeratorA == 0.0f && numeratorB == 0.0f &&
                (System.Math.Min(a1.X, a2.X) <= System.Math.Max(b1.X, b2.X) && System.Math.Min(b1.X, b2.X) <= System.Math.Max(a1.X, a2.X));
            float uA = numeratorA / commonDenominator;
            float uB = numeratorB / commonDenominator;
            return uA >= 0.0f && uA <= 1.0f && uB >= 0.0f && uB <= 1.0f;
        }

        public void addProjectile(CProjectile proj) { m_projectiles.Add(proj); }

        static void DrawHealthBar(ASprite sprite, p2i pos, int frame, float fill, float alpha)
        {
            ASprite.PushClipRect(pos + sprite.GetFramePos(frame), new d2i((int)(fill * sprite.GetFrameWidth(frame)), sprite.GetFrameHeight(frame)));
            sprite.PaintFrame(frame, pos.X, pos.Y, 0, 0, 0, (int)(255 * alpha));
            ASprite.PopClipRect();
        }

        public void prerenderBackground() { }

        // ------------------------------------------------------------------ draw

        public void draw()
        {
            showForeground();
            showBackground();
            __IRR_SCENE().drawAll();
            m_griffin.draw();
            foreach (SEffect e in m_effects) e.inst.Draw3D(e.pos);
            foreach (CProjectile p in m_projectiles) p.draw();
            m_spell.draw3D();
            line3df ray = __IRR_SCENE().getRayFromScreenCoordinates(new p2i(48, 300));
            v3f dest = ray.start + ray.getVector().setLength(5.0f);
            foreach (SSouls s in m_flyingSouls)
            {
                float k = CLAMP(powf(s.time / s.duration, 2.0f), 0.0f, 1.0f);
                v3f pos = dest.getInterpolated(s.start, k);
                s.inst.Draw3D(pos, 1.1f - 1.0f * k);
            }
            foreach (CUnit unit in m_units) unit.draw();
            if (m_lightball)
            {
                ISceneNode lb = m_hydra.getSceneNode().getSceneNodeFromName("d_lightball");
                if (lb != null)
                {
                    const float scale = 1.0f * ASprite.SPRITE_SCALE_3D;
                    getSpriteEffects().PaintAFrame3D(INGAME_A_EFFECTS_HYDRA_FLY, 0, lb.getAbsolutePosition(), 0, 0, 0, scale, scale);
                }
            }
        }

        public void draw2D()
        {
            Gfx.beginScene2D();
            CFont font1 = __FONT("Money"), font2 = __FONT("Bonus");
            foreach (SMoney m in m_flyingMoney)
            {
                p2i pos = new p2i((int)m.position.X, (int)m.position.Y);
                pos.X = CLAMP(pos.X, 40, GAME_SCREEN_W - 40);
                pos.Y = CLAMP(pos.Y, 20, GAME_SCREEN_H - 20);
                int a = CLAMP((int)(255 * m.time), 0, 255);
                if (m.bonus == 0) font1.DrawString("+" + FormatNumber(m.amount), pos.X - 4, pos.Y, CFont.HVCENTER, a);
                else
                {
                    font2.DrawString("+" + FormatNumber(m.amount + m.bonus), pos.X - 4, pos.Y, CFont.HVCENTER, a);
                    if (m.text != null) font1.DrawString(m.text, pos.X, pos.Y - 15, CFont.HVCENTER, a);
                }
            }
            m_spell.draw();
            float alphaf = m_hudAlpha;
            int alpha = (int)(255 * m_hudAlpha);
            p2i hp = new p2i(49 + 6, 6 + 8);
            m_sprIngame.PaintFrame(MENU_F_MENU_HEALTH_BAR_BACKGROUND, hp.X, hp.Y, 0, 0, 0, alpha);
            float fill = m_gate.getHealthRatio();
            float slow = m_gate.getSlowHealthRatio();
            DrawHealthBar(m_sprIngame, hp, MENU_F_MENU_BAR_SLOW, slow, 0.5f * alphaf);
            if (fill <= 0.2f && fmodf(m_time, 0.5f) < 0.25f) DrawHealthBar(m_sprIngame, hp, MENU_F_MENU_BAR_WHITE, fill, 1.0f * alphaf);
            else DrawHealthBar(m_sprIngame, hp, MENU_F_MENU_BAR_RED, fill, 1.0f * alphaf);
            DrawHealthBar(m_sprIngame, hp, MENU_F_MENU_BAR_YELLOW, fill, LinearStep(fill, 0.00f, 0.50f) * alphaf);
            DrawHealthBar(m_sprIngame, hp, MENU_F_MENU_BAR_GREEN, fill, LinearStep(fill, 0.50f, 1.00f) * alphaf);
            __FONT("Health").DrawString(FormatNumber((int)m_gate.getHealth()), hp.X, hp.Y, CFont.HVCENTER, alpha);
            p2i cp = new p2i(123, 300);
            m_sprIngame.PaintAFrame(MENU_A_MENU_COIN_BAR, 0, cp.X, cp.Y, 0, 0, 0, alpha);
            __FONT(m_slowGold >= m_gold ? "Money" : "BigMoney").DrawString(FormatNumber((int)(m_levelStats.Gold + m_slowGold)), cp.X + 99, cp.Y - 1, CFont.HVCENTER, alpha);
            __FONT(m_slowSouls >= m_souls ? "Money" : "BigMoney").DrawString(FormatNumber((int)(m_levelStats.Souls + m_slowSouls)), cp.X - 20, cp.Y - 1, CFont.HVCENTER, alpha);
            m_playPauseIcon.Draw(alpha);
            if (m_PlayState == PS_Play || isPaused())
            {
                m_fireball.drawCooldown(new p2i(226, 41));
                m_iceball.drawCooldown(new p2i(31, 123));
                m_griffin.drawCooldown(new p2i(325, 94));
                m_hydra.drawCooldown(new p2i(402, 162));
                m_spell.drawCooldown(new p2i(134, 32));
            }
            const float transition = 0.4f;
            float end1 = m_challenge.getType() == CT_None ? k_dayTextEndTime : 0.5f * k_dayChallengeTextEndTime + 0.5f * transition;
            float end2 = m_challenge.getType() == CT_None ? k_dayTextEndTime : k_dayChallengeTextEndTime;
            const int y = 160, h = 70;
            if (m_dayTextTime < end2 && m_time >= -end2)
            {
                float backgroundAlpha = LinearUpFlatDown(m_dayTextTime, 0, transition, end2 - transition, end2);
                float dayAlpha = LinearUpFlatDown(m_dayTextTime, 0, transition, end1 - transition, end1);
                float dayX = LinearUpFlatUp(m_dayTextTime, 0, transition, end1 - transition, end1);
                Gfx.draw2DRectangle(new SColor((int)(backgroundAlpha * 150), 0, 0, 0), new r2i(0, y - h / 2, 480, y + h / 2));
                List<int> daysPerLevel = getNumberOfDaysPerLevel();
                string result = m_isEndless ? FormatString(_TXT(STRING_IDS.INGAME_DAY_X), m_CurrentDay + 1)
                                            : FormatString(_TXT(STRING_IDS.INGAME_DAY_X_OF_Y), m_CurrentDay + 1, daysPerLevel[m_CurrentLevel]);
                __FONT("DayBlue").DrawString(result, 240 + 300 * -dayX, y, CFont.HVCENTER, (int)(dayAlpha * 255));
                if (m_challenge.getType() != CT_None)
                {
                    float challengeAlpha = LinearUpFlatDown(m_dayTextTime, end1 - transition, end1, end2 - transition, end2);
                    float challengeX = LinearUpFlatUp(m_dayTextTime, end1 - transition, end1, end2 - transition, end2);
                    string text = _TXT(STRING_IDS.CHALLENGES_HEADER) + "\n" + m_challenge.getDescription();
                    __FONT("Challenge").DrawWrappedText(text, 240 + 300 * -challengeX, y, OS_SCREEN_W * 3 / 4, CFont.HVCENTER, (int)(challengeAlpha * 255));
                    if (m_playChallengePopupSound && m_dayTextTime > end1 - transition && m_dayTextTime < end2)
                    {
                        __MGR_SOUNDS().play(SFX_CHALLENGE_POPUP);
                        m_playChallengePopupSound = false;
                    }
                }
            }
            else if (m_dayTextTime - _time < end2 && m_time >= -end2) __MGR_SOUNDS().play(SFX_FOGHORN, false);
            if (m_PlayState == PS_MissionOverTimer)
            {
                float t = 3.0f - m_missionOverCountdown;
                float backgroundAlpha = LinearUpFlatDown(t, 0.0f, 0.5f, 2.5f, 3.0f);
                float dayAlpha = LinearUpFlatDown(t, 0.0f, 0.5f, 2.5f, 3.0f);
                float dayX = LinearUpFlatUp(t, 0, 0.5f, 2.5f, 3.0f);
                if (!IsMissionSuccess())
                {
                    Gfx.draw2DRectangle(new SColor((int)(backgroundAlpha * 150), 0, 0, 0), new r2i(0, y - h / 2, 480, y + h / 2));
                    __FONT("DayRed").DrawString(_TXT(STRING_IDS.INGAME_DEFEAT), 240 + 300 * -dayX, y, CFont.HVCENTER, (int)(dayAlpha * 255));
                }
                else
                {
                    List<int> daysPerLevel = getNumberOfDaysPerLevel();
                    if (m_CurrentLevel == daysPerLevel.Count - 2 && m_CurrentDay == daysPerLevel[m_CurrentLevel] - 1)
                    {
                        Gfx.draw2DRectangle(new SColor((int)(backgroundAlpha * 150), 0, 0, 0), new r2i(0, y - h / 2, 480, y + h / 2));
                        __FONT("DayBlue").DrawString(_TXT(STRING_IDS.INGAME_CONGRATULATIONS), 240 + 300 * -dayX, y, CFont.HVCENTER, (int)(dayAlpha * 255));
                    }
                }
            }
            m_challenge.draw();
            DrawFade();
            if (m_PlayState == PS_IntroUnitCamera) DrawIntroUnitCamera();
            Gfx.endScene2D();
        }

        // ------------------------------------------------------------------ money / effects

        public void addMoney(int amount, int bonus, v3f pos, string text = null, float time = 2.0f)
        {
            addMoney(amount, bonus, vector2DFrom3DPos(pos + new v3f(0, 1, 0)), text, time);
        }

        public void addMoney(int amount, int bonus, v2f pos, string text = null, float time = 2.0f)
        {
            m_flyingMoney.Add(new SMoney { amount = amount, bonus = bonus, position = pos, text = text, time = time });
            m_gold += amount + bonus;
        }

        public void addSouls(int amount, v3f pos)
        {
            SSouls souls = new SSouls { amount = amount, start = pos, time = 0.0f, duration = 1.0f, inst = new ASpriteInstance() };
            souls.inst.StartAnim(m_spriteDie, INGAME_A_DEAD01_HYDRA_TAKE_SOUL, 0, 0, 0);
            m_flyingSouls.Add(souls);
        }

        public CUnit spawnUnit(string id, int slaves, float time, int unitId, bool intro)
        {
            if (CGameConfig.DontSpawnUnits) return null;
            const float offset_min = -1.25f, offset_max = 1.25f;
            const float offset_len = offset_max - offset_min;
            const int lanes = 8;
            int lane = findFreeLane();
            allocateLane(lane);
            float offset = offset_min + offset_len * lane / (lanes - 1);
            UnitParameters p = UnitParameters.GetUnitParameters(id);
            CUnit unit = new CUnit(p);
            unit.m_lane = lane;
            AddFrameEvent(FET_SpawnUnit, unit.getUnitKind());
            v3f dir = m_path.getVector();
            v3f right = dir.crossProduct(new v3f(0, 1, 0)).normalize() * offset;
            line3df path = m_path;
            path.start += right;
            path.end += right * 0.5f;
            unit.setPath(path);
            if (!string.IsNullOrEmpty(p.mindControl) && slaves > 0)
            {
                path.start -= path.getVector().normalize() * 2.0f;
                for (int i = 0; i < slaves; ++i)
                {
                    int slaveId = unitId + i + 1;
                    CUnit slave = spawnUnit(p.mindControl, 0, time, slaveId, false);
                    slave.setMindControlMaster(unit);
                    slave.setState(US_Walking);
                    m_extraSpawns.Add(new UnitTypeAndId { unitType = stringToUnitType(p.mindControl), time = time + 0.001f, id = slaveId });
                }
            }
            if (!string.IsNullOrEmpty(p.rider))
            {
                int riderId = unitId + 1;
                unit.setRider(spawnUnit(p.rider, 0, time, riderId, false));
                unit.getRider().setMount(unit);
                unit.getRider().setState(US_Riding);
                ISceneNode riderDummy = unit.getSceneNode().getSceneNodeFromName("d_boar_rider");
                riderDummy.addChild(unit.getRider().getSceneNode());
                float scale = unit.getSceneNode().getScale().X;
                riderDummy.setScale(new v3f(1.0f / scale, 1.0f / scale, 1.0f / scale));
                m_extraSpawns.Add(new UnitTypeAndId { unitType = stringToUnitType(p.rider), time = time + 0.001f, id = riderId });
                freeLane(unit.getRider().m_lane);
                unit.getRider().m_lane = unit.m_lane;
                allocateLane(unit.getRider().m_lane);
                unit.getRider().setPath(unit.getPath());
            }
            unit.start();
            m_units.Add(unit);
            unit.m_positionInList = -1;
            unit.m_id = unitId;
            CAchievements.GetInstance().setValue("No more Strangers", 1 << unit.getUnitKind());
            if (intro) unit.setIntroUnit();
            return unit;
        }

        void addEffect(ASprite sprite, int anim, v3f pos)
        {
            SEffect e = new SEffect { inst = new ASpriteInstance(), pos = pos };
            e.inst.StartAnim(sprite, anim, 0, 0, 0);
            m_effects.Add(e);
        }

        public void addSpark(v3f pos) { addEffect(m_spriteSparks, 0, pos); }
        public void addWalkDust(v3f pos) { addEffect(m_spriteDust, 0, pos); }
        public void addDie(v3f pos) { addEffect(m_spriteDie, INGAME_A_DEAD01_UNIT_DIE, pos); }

        public void addFallDust(v3f pos)
        {
            ParticleSystem ps = ParticleManager.AddParticleSystem("smoke_explode");
            ps.SetPosition(pos);
            ps.SetScale(0.02f);
        }

        void updateEffects()
        {
            for (int i = 0; i < m_effects.Count; ++i)
            {
                if (m_effects[i].inst.IsAnimEnded()) { m_effects.RemoveAt(i); --i; continue; }
                m_effects[i].inst.Update(_time);
            }
            for (int i = 0; i < m_flyingMoney.Count; ++i)
            {
                if (m_flyingMoney[i].time <= 0.0f) { m_flyingMoney.RemoveAt(i); --i; continue; }
                m_flyingMoney[i].time -= _time;
                m_flyingMoney[i].position += new v2f(0, -20) * _time;
            }
            for (int i = 0; i < m_flyingSouls.Count; i++)
            {
                if (m_flyingSouls[i].time >= m_flyingSouls[i].duration)
                {
                    m_souls += m_flyingSouls[i].amount;
                    m_flyingSouls.RemoveAt(i);
                    --i;
                    continue;
                }
                m_flyingSouls[i].time += _time;
                m_flyingSouls[i].inst.Update(_time);
            }
        }

        void calc3D()
        {
            m_path.start = __IRR_SCENE().getSceneNodeFromName("d_path_start", m_castle).getPosition();
            m_path.end = __IRR_SCENE().getSceneNodeFromName("d_path_end", m_castle).getPosition();
            v3f dir = m_path.getVector();
            v3f up = new v3f(0, 1, 0);
            v3f right = dir.crossProduct(up).normalize();
            v3f middle = m_path.start.getInterpolated(m_path.end, 0.74f);
            m_groundPlane.setPlane(m_path.start, m_path.end, m_path.end - right);
            m_flyingPlane.setPlane(m_path.start, m_path.end, m_path.end + up);
            m_castlePlane.setPlane(m_path.end, m_path.end + right, m_path.end + up);
            m_outOfScreenPlane.setPlane(middle, middle + up, middle + right);
        }

        static int RandomEnemyCountByDifficulty(int difficulty, int min, int max)
        {
            switch (difficulty)
            {
                case GD_Easy: return RandInt(min, (2 * min + 4 * max + 3) / 6);
                case GD_Normal: return RandInt((4 * min + 2 * max + 3) / 6, (2 * min + 4 * max + 3) / 6);
                case GD_Hard: return RandInt((2 * min + 4 * max + 3) / 6, max);
                default: return RandInt(min, max);
            }
        }

        void loadWaves()
        {
            for (int i = 0; i < 8; ++i) m_unitsInLane[i] = 0;
            m_waves.Clear();
            m_extraSpawns.Clear();
            m_dayTextTime = 0.0f;
            CProjectile.reset();
            List<int> daysPerLevel = getNumberOfDaysPerLevel();
            bool isEndless = m_CurrentLevel == daysPerLevel.Count - 1;
            Day level;
            if (isEndless)
            {
                List<Day>[] segmentsByDifficulty = { new List<Day>(), new List<Day>(), new List<Day>() };
                for (int i = 0; i < daysPerLevel[daysPerLevel.Count - 1]; ++i)
                {
                    Day seg = loadDayInfo(getDayPath(m_CurrentLevel, i));
                    if (seg != null) segmentsByDifficulty[seg.difficulty].Add(seg);
                }
                const int dayMaxTime = 90;
                float[] odds = getEndlessOdds(m_CurrentDay);
                float totalOdds = odds[0] + odds[1] + odds[2];
                level = new Day { challenge = CT_None, difficulty = 0, dayEndTime = 0 };
                while (level.dayEndTime < dayMaxTime)
                {
                    float r = RandFloat(0.0f, totalOdds);
                    int difficulty = r < odds[0] ? 0 : r < odds[0] + odds[1] ? 1 : 2;
                    if (segmentsByDifficulty[difficulty].Count == 0) break;
                    int levelToLoad = RandInt(0, segmentsByDifficulty[difficulty].Count - 1);
                    level.dayEndTime = appendDayToDay(level, level.dayEndTime, segmentsByDifficulty[difficulty][levelToLoad]);
                }
            }
            else level = loadDayInfo(getDayPath(m_CurrentLevel, m_CurrentDay));
            if (level != null)
            {
                m_challenge.setType(level.challenge);
                foreach (Wave wave in level.waves)
                {
                    if (wave.variants.Count == 0) continue;
                    float time = 0.0f;
                    for (int rep = 0; rep < wave.repeatCount && time < level.dayEndTime; ++rep)
                    {
                        int timeStart = wave.timeStart + wave.repeatInterval * rep;
                        Variant v = wave.variants[RandInt(0, wave.variants.Count - 1)];
                        time = RandFloat(timeStart, timeStart + wave.timeInterval);
                        foreach (EnemyType enemy in v.enemyTypes)
                        {
                            int count = RandomEnemyCountByDifficulty(getDifficulty(), enemy.minCount, enemy.maxCount);
                            IAttributes attrs = g_unitTypes[enemy.unitType].attributes;
                            if (attrs.existsAttribute("MindControl"))
                                m_waves.Add(new SUnitAtTime { type = enemy.unitType, time = time, slaves = count, riders = 0, intro = v.intro });
                            else
                            {
                                for (int k = 0; k < count; ++k)
                                    m_waves.Add(new SUnitAtTime
                                    {
                                        type = enemy.unitType,
                                        time = wave.packed ? time : (timeStart + k * wave.timeInterval / (float)count),
                                        slaves = 0,
                                        riders = attrs.existsAttribute("Rider") ? 1 : 0,
                                        intro = v.intro
                                    });
                            }
                        }
                    }
                }
                m_waves.Sort((a, b) => a.time.CompareTo(b.time));
            }
            m_MaxUnits = m_waves.Count;
        }

        bool canSpawnMoreUnits(int count)
        {
            if (CGameConfig.DontSpawnUnits) return false;
            return !IsMissionOver() && m_units.Count + count <= 8;
        }

        void updateWaves()
        {
            while (m_wavePos < m_waves.Count && canSpawnMoreUnits(1 + m_waves[m_wavePos].slaves + m_waves[m_wavePos].riders) && m_time >= m_waves[m_wavePos].time)
            {
                SUnitAtTime w = m_waves[m_wavePos];
                spawnUnit(g_unitTypes[w.type].name, w.slaves, w.time, m_wavePos << 8, w.intro);
                ++m_wavePos;
            }
        }

        static void cameraClipEndCallback(CTimelineController tc, object ptr)
        {
            CPlayfield playfield = GetInstance();
            if (playfield.m_PlayState == PS_CutScene) playfield.EndCutscene();
        }

        void UpdateDayTime()
        {
            float max_time = m_waves.Count > 0 ? m_waves[m_waves.Count - 1].time + 10.0f : 0.0f;
            if (max_time == 0) SetDayTime(0);
            else SetDayTime(CLAMP(m_time / max_time, 0.0f, 1.0f));
        }

        static readonly SColorf[] s_skyColor =
        {
            new SColorf(1.00f, 1.00f, 1.00f, 1.00f), new SColorf(1.00f, 1.00f, 1.00f, 1.00f), new SColorf(1.00f, 1.00f, 1.00f, 1.00f),
            new SColorf(1.00f, 0.80f, 0.60f, 1.00f), new SColorf(0.45f, 0.65f, 0.75f, 1.00f),
        };
        static readonly float[] s_skyTime = { 0.00f, 0.15f, 0.70f, 0.85f, 1.00f };

        void SetDayTime(float day_time)
        {
            if (day_time == m_DayTime) return;
            m_DayTime = day_time;
            if (m_SceneLight == null) return;
            SColorf skyColor = new SColorf(1, 1, 1, 1);
            for (int i = 0; i < s_skyTime.Length; i++)
            {
                if (m_DayTime > s_skyTime[i]) continue;
                if (i == 0) skyColor = s_skyColor[0];
                else skyColor = s_skyColor[i - 1].getInterpolated(s_skyColor[i], (s_skyTime[i] - m_DayTime) / (s_skyTime[i] - s_skyTime[i - 1]));
                break;
            }
            m_SceneLight.getLightData().AmbientColor = skyColor;
        }

        // DisableRTT: the background/foreground split is only needed for the prerendered path.
        public void hideBackground() { }
        public void showBackground() { }
        public void hideForeground() { }
        public void showForeground() { }

        public ISceneNode getDummy(string name) { return __IRR_SCENE().getSceneNodeFromName(name, m_castle); }

        public void checkHydraEatUnitFromHand(float time, CUnit unit)
        {
            if (unit.getRider() != null) unit = unit.getRider();
            if (!m_hydra.isReadyToEat() || !unit.canBeEatenByHydra()) return;
            r2i hoverRect = new r2i(340, 70, 440, 190);
            p2i pos = screenFrom3DPos(unit.getChestDummy().getAbsolutePosition());
            if (!hoverRect.isPointInside(pos)) return;
            m_unitHoveredTimer += _time;
            if (m_unitHoveredTimer > 0.50f)
            {
                if (unit.getUnitKind() == UK_Elf && RandFloat(0.0f, 1.0f) < 0.5f)
                {
                    m_hydra.Head[0].setAnimAnim("attack_fail", "idle");
                    unit.elfJump(0.6f);
                }
                else m_hydra.eatFromHandStart(unit);
            }
            else m_unitHoveredOverHydra = true;
        }

        void initCamera() { m_cameraPosition = m_camera.getPosition(); }

        int findFreeLane()
        {
            int[] bestLanes = new int[8];
            int bestLanesCount = 0, bestLanesValue = 0;
            for (int i = 0; i < 8; ++i)
            {
                if (m_unitsInLane[i] != 0) continue;
                int j;
                for (j = 1; j < 8; ++j)
                {
                    if (i - j >= 0 && m_unitsInLane[i - j] > 0) break;
                    if (i + j < 8 && m_unitsInLane[i + j] > 0) break;
                }
                if (j > bestLanesValue) { bestLanesCount = 0; bestLanesValue = j; }
                if (j == bestLanesValue) bestLanes[bestLanesCount++] = i;
            }
            if (bestLanesCount == 0) return RandInt(0, 7);
            return bestLanes[RandInt(0, bestLanesCount - 1)];
        }

        void allocateLane(int lane) { ++m_unitsInLane[lane]; }
        void freeLane(int lane) { --m_unitsInLane[lane]; }

        public CUnit findShaman()
        {
            foreach (CUnit u in m_units) if (u.getUnitKind() == UK_Shaman && !u.isPetrified()) return u;
            return null;
        }

        float[] getEndlessOdds(int day)
        {
            return new[] { LinearUpFlatDown(day, -1, 0, 2, 4), LinearUpFlatDown(day, 2, 4, 6, 8), LinearUpFlatDown(day, 6, 8, 100000, 100001) };
        }

        public void addBomb(CBomb bomb) { m_bombs.Add(bomb); }

        // ------------------------------------------------------------------ music

        public void PauseMusic()
        {
            m_MusicPause = true;
            if (m_CurrentMusic >= 0) __MGR_SOUNDS().stop(m_CurrentMusic);
        }

        public void ResumeMusic()
        {
            m_MusicPause = false;
            if (m_CurrentMusic >= 0) __MGR_SOUNDS().play(m_CurrentMusic);
        }

        public int GetMusic() { return m_CurrentMusic; }

        public void SetMusic(int musicId, bool loop = false)
        {
            CSoundManager sm = __MGR_SOUNDS();
            if (musicId == m_CurrentMusic)
            {
                if (m_CurrentMusic >= 0 && !sm.isSoundPlaying(m_CurrentMusic) && !m_MusicPause) sm.play(m_CurrentMusic, loop);
            }
            else
            {
                if (m_CurrentMusic >= 0) sm.stop(m_CurrentMusic);
                m_PrevMusic = m_CurrentMusic;
                m_CurrentMusic = musicId;
                if (!m_MusicPause && m_CurrentMusic >= 0)
                {
                    sm.play(m_CurrentMusic, loop);
                    if (m_CurrentMusic == M_LEVEL_A_FAST) sm.play(M_LEVEL_A_FAST_INTRO, false);
                }
            }
        }

        public void SetNextMusic(int nextMusicId, bool loop = false)
        {
            if (nextMusicId < 0) return;
            m_NextMusic = nextMusicId;
            m_NextMusicLoop = loop;
            if (GetMusic() < 0)
            {
                m_NextMusic = -1;
                m_NextMusicLoop = false;
                SetMusic(m_NextMusic, m_NextMusicLoop);
            }
        }

        void UpdateMusic()
        {
            CSoundManager sm = __MGR_SOUNDS();
            if (GetPlayState() == PS_CutScene) SetMusic(M_LEVEL_A_SLOW);
            if (GetPlayState() == PS_Play) SetMusic(m_gate.getHealthRatio() <= 0.30f ? M_LEVEL_A_FAST : M_LEVEL_A_SLOW, true);
            if (GetPlayState() == PS_MissionOver)
            {
                int idx = GetMusic();
                if (idx < 0 || !sm.isSoundPlaying(idx))
                {
                    SetMusic(-1);
                    if (idx == M_DEBRIEFING) SetMusic(idx, true);
                }
            }
            if (GetMusic() < 0) SetMusic(m_NextMusic, m_NextMusicLoop);
        }

        // ------------------------------------------------------------------ level lifetime

        public void ResetLevel()
        {
            foreach (CUnit u in m_units) u.Dispose();
            m_units.Clear();
            ClearProjectiles();
            ParticleManager.ClearParticleSystem();
            CGameConfig.RenderToTexture = true;
            loadWaves();
            m_lastDamageSource = -1;
            m_currentBonus = 0;
            m_comboUnitKind = UK_Unknown;
            m_wavePos = 0;
            m_time = 0;
            m_gold = 0;
            m_souls = 0;
            m_slowGold = 0;
            m_slowSouls = 0;
            if (m_gate != null)
            {
                m_gate.reset();
                if (m_isEndless) m_gate.damageGate(m_damage);
            }
        }

        public void LoadLevel(float time)
        {
            AddFrameEvent(FET_Loading, m_LoadingProgress);
            m_LoadingProgress++;
            switch (m_LoadingProgress)
            {
                case 1:
                    __IRR_SCENE().getRootSceneNode().setVisible(false);
                    SetPlayState(PS_Begin);
                    break;
                case 3:
                    m_MusicPause = false;
                    m_CurrentMusic = -1;
                    m_PrevMusic = -1;
                    m_NextMusic = -1;
                    m_NextMusicLoop = false;
                    break;
                case 5: m_sprIngame = new ASprite(); m_sprIngame.Load("Menu/menu"); break;
                case 6: m_sprStonedUnits = new ASprite(); m_sprStonedUnits.Load("Ingame/stoned_units"); break;
                case 8: m_sprIcedUnits = new ASprite(); m_sprIcedUnits.Load("Ingame/iced_units"); break;
                case 12: m_sprEffects = new ASprite(); m_sprEffects.Load("Ingame/effects"); break;
                case 15: m_sprDizzy = new ASprite(); m_sprDizzy.Load("Ingame/dizzy_effect"); break;
                case 17:
                    m_playPauseIcon = new ASpriteInstance();
                    m_lastDamageSource = -1;
                    m_currentBonus = 0;
                    break;
                case 26: m_spriteSparks = new ASprite(); m_spriteSparks.Load("Ingame/hit"); break;
                case 28: m_spriteDust = new ASprite(); m_spriteDust.Load("Ingame/walk_dust"); break;
                case 32:
                    m_spriteDie = new ASprite();
                    m_spriteDie.Load("Ingame/dead01");
                    m_spriteDie._material.MaterialType = E_MATERIAL_TYPE.EMT_TRANSPARENT_ADD_COLOR;
                    break;
                case 33:
                    m_touch = new CTouchElement(CTouchElement.Type_AnyTouch);
                    m_touch.SetScreenCoordinates(0, 0, GAME_SCREEN_W, GAME_SCREEN_H);
                    m_touch.SetEnabled(true);
                    m_unitDragged = false;
                    break;
                case 40:
                {
                    m_castle = LoadColladaScene("Scenes/Castle/castle.bdae");
                    __IRR_SCENE().getRootSceneNode().addChild(m_castle);
                    CTimelineController castleControl = Timeline(m_castle);
                    castleControl.setClip("sceneloop");
                    castleControl.setLoop(true);
                    m_castleBackground = m_castle.getSceneNodeFromName("back");
                    m_castleForeground = m_castle.getSceneNodeFromName("front");
                    break;
                }
                case 45:
                    m_gate = new CCastleGate();
                    __IRR_SCENE().getRootSceneNode().addChild(m_gate.getSceneNode());
                    __IRR_SCENE().getSceneNodeFromName("d_gate", m_castle).addChild(m_gate.getSceneNode());
                    break;
                case 51:
                    m_griffin = new CGriffin();
                    m_griffin.setSpawnPosition(__IRR_SCENE().getSceneNodeFromName("d_griffin", m_castle).getPosition());
                    break;
                case 60:
                    foreach (string f in new[] { "Challenge", "ChallengeFailed", "Health", "Money", "BigMoney", "Bonus", "DayBlue", "DayRed" }) __FONT(f);
                    break;
                case 65:
                {
                    // Spawn points need world positions; the castle animator hasn't run yet.
                    m_castle.updateAbsolutePosition(true);
                    m_fireball = new CFireball();
                    m_fireball.setSpawnPosition(__IRR_SCENE().getSceneNodeFromName("d_fireball", m_castle));
                    ISceneNode dummy = __IRR_SCENE().getSceneNodeFromName("d_iceball", m_castle);
                    dummy.updateAbsolutePosition();
                    m_iceball = new CIceball();
                    m_iceball.setSpawnPosition(dummy);
                    break;
                }
                case 70:
                    cleanNode(m_castle);
                    calc3D();
                    break;
                case 75:
                    m_hydra = new CHydra();
                    __IRR_SCENE().getSceneNodeFromName("d_hydra", m_castle).addChild(m_hydra.getSceneNode());
                    break;
                case 80:
                    m_spell = new CSpell();
                    __IRR_SCENE().getSceneNodeFromName("d_shield", m_castle).addChild(m_spell.getSceneNode());
                    break;
                case 85:
                    m_wavePos = 0;
                    m_playPauseIcon.StartAnim(m_sprIngame, MENU_A_MENU_PLAY, 20, 300);
                    break;
                case 90:
                {
                    m_cameraScene = LoadColladaScene("Scenes/Castle/cameras.bdae");
                    __IRR_SCENE().getRootSceneNode().addChild(m_cameraScene);
                    ISceneNodeAnimator animator = m_cameraScene.getFirstAnimator();
                    animator.getTimelineCtrl().setCallback(cameraClipEndCallback, null);
                    m_camera = (CCameraSceneNode)m_cameraScene.getSceneNodeFromType(ESCENE_NODE_TYPE.ESNT_COLLADA_CAMERA);
                    initCamera();
                    m_cameraShakeTimer = 0.0f;
                    m_camera.setAspectRatio(480.0f / 320.0f);
                    __IRR_SCENE().setActiveCamera(m_camera);
                    break;
                }
                case 95:
                {
                    m_SceneLight = __IRR_SCENE().addLightSceneNode();
                    SLight light = m_SceneLight.getLightData();
                    m_SceneLight.setPosition(__IRR_SCENE().getActiveCamera().getPosition());
                    light.AmbientColor = new SColorf(1, 1, 1, 1);
                    light.DiffuseColor = new SColorf(0, 0, 0, 1);
                    light.SpecularColor = new SColorf(0, 0, 0, 1);
                    m_DayTime = -1.0f;
                    break;
                }
                case 97:
                    m_gate.setLevel(m_levelStats.WeaponLevel[WT_Gate]);
                    m_hydra.setLevel(m_levelStats.WeaponLevel[WT_Hydra]);
                    m_fireball.setLevel(m_levelStats.WeaponLevel[WT_Fireball]);
                    m_iceball.setLevel(m_levelStats.WeaponLevel[WT_Iceball]);
                    m_griffin.setLevel(m_levelStats.WeaponLevel[WT_Griffin]);
                    m_spell.setLevel(m_levelStats.WeaponLevel[WT_Spell]);
                    m_gate.reset();
                    if (m_isEndless) m_gate.damageGate(m_damage);
                    m_hydra.reset();
                    m_fireball.reset();
                    m_iceball.reset();
                    m_griffin.reset();
                    m_spell.reset();
                    __IRR_SCENE().getRootSceneNode().setVisible(true);
                    break;
                case 98: prerenderBackground(); break;
                case 99:
                    m_EnterInShop = false;
                    __PROFILE().Set("enter_in_shop", m_EnterInShop);
                    break;
                case 101:
                    if (!m_touch.IsActive() && !LaunchOptions.AutoContinue) --m_LoadingProgress;
                    else
                    {
                        m_touch.ResetInput();
                        fadeOut(1.0f);
                    }
                    break;
                case 102:
                    if (m_fadeDir != 0)
                    {
                        --m_LoadingProgress;
                        UpdateFade(time * 0.001f);
                    }
                    break;
            }
        }

        public void ReleaseLevel()
        {
            __IRR_SCENE().setActiveCamera(null);
            ClearProjectiles(true);
            ParticleManager.ClearParticleSystem();
            foreach (CUnit u in m_units) u.Dispose();
            m_units.Clear();
            m_effects.Clear();
            m_flyingSouls.Clear();
            m_flyingMoney.Clear();
            m_sprIngame = m_sprStonedUnits = m_sprIcedUnits = m_sprEffects = m_sprDizzy = null;
            m_playPauseIcon = null;
            m_spriteSparks = m_spriteDust = m_spriteDie = null;
            if (m_touch != null) { m_touch.Dispose(); m_touch = null; }
            if (m_gate != null) { m_gate.Dispose(); m_gate = null; }
            if (m_hydra != null) { m_hydra.Dispose(); m_hydra = null; }
            if (m_griffin != null) { m_griffin.Dispose(); m_griffin = null; }
            if (m_fireball != null) { m_fireball.Dispose(); m_fireball = null; }
            if (m_iceball != null) { m_iceball.Dispose(); m_iceball = null; }
            if (m_spell != null) { m_spell.Dispose(); m_spell = null; }
            if (m_castle != null) { m_castle.remove(); m_castle = null; }
            if (m_cameraScene != null) { m_cameraScene.remove(); m_cameraScene = null; }
            if (m_SceneLight != null) { m_SceneLight.remove(); m_SceneLight = null; }
            if (m_backTouch != null) { m_backTouch.Dispose(); m_backTouch = null; }
            GameTimer.Speed = 1.0f;
            m_LoadingProgress = 0;
        }

        public void DrawLoadingBar() { }

        public string GetLevelFieldStr(int gd)
        {
            if (gd == GD_Easy) return "unlock_level_easy";
            if (gd == GD_Normal) return "unlock_level_normal";
            if (gd == GD_Hard) return "unlock_level_hard";
            return null;
        }

        void SetUnlockLevel(int gd, int value)
        {
            if (gd == GD_Easy) m_unlockLevelEasy = value;
            else if (gd == GD_Hard) m_unlockLevelHard = value;
            else m_unlockLevelNormal = value;
        }

        public void ReadProfileStats()
        {
            CUserProfile profile = __PROFILE();
            int difficulty, cutScene;
            profile.Get("unlock_level_easy", out m_unlockLevelEasy);
            profile.Get("unlock_level_normal", out m_unlockLevelNormal);
            profile.Get("unlock_level_hard", out m_unlockLevelHard);
            profile.Get("save_level", out m_CurrentLevel);
            profile.Get("save_day", out m_CurrentDay);
            profile.Get("save_difficulty", out difficulty);
            m_difficulty = difficulty;
            profile.Get("enter_in_shop", out m_EnterInShop);
            List<int> daysPerLevel = getNumberOfDaysPerLevel();
            if (m_CurrentLevel >= 0 && m_CurrentLevel < daysPerLevel.Count && m_CurrentDay >= daysPerLevel[m_CurrentLevel]) m_CurrentDay = daysPerLevel[m_CurrentLevel] - 1;
            profile.Get("cutscene", out cutScene);
            SetNextCutscene(cutScene);
            profile.Get("gold", out m_levelStats.Gold);
            profile.Get("souls", out m_levelStats.Souls);
            profile.Get("killed", out m_levelStats.Kills);
            profile.Get("game_number", out m_gameNumber);
            for (int i = 0; i < WT_Count; i++)
            {
                profile.Get("weapon_level_" + i, out m_levelStats.WeaponLevel[i]);
                m_levelStats.WeaponAvailable[i] = profile.GetInt("weapon_available_" + i) != 0;
            }
            profile.Get("spell", out m_levelStats.SpellAvailable);
        }

        public void WriteProfileStats()
        {
            if (m_isEndless) return;
            CUserProfile profile = __PROFILE();
            profile.Set("unlock_level_easy", m_unlockLevelEasy);
            profile.Set("unlock_level_normal", m_unlockLevelNormal);
            profile.Set("unlock_level_hard", m_unlockLevelHard);
            profile.Set("save_level", m_CurrentLevel);
            profile.Set("save_day", m_CurrentDay);
            profile.Set("save_difficulty", m_difficulty);
            m_EnterInShop = true;
            profile.Set("enter_in_shop", m_EnterInShop);
            profile.Set("gold", m_levelStats.Gold);
            profile.Set("souls", m_levelStats.Souls);
            profile.Set("killed", m_levelStats.Kills);
            for (int i = 0; i < WT_Count; i++)
            {
                profile.Set("weapon_level_" + i, m_levelStats.WeaponLevel[i]);
                profile.Set("weapon_available_" + i, m_levelStats.WeaponAvailable[i] ? 1 : 0);
            }
            profile.Set("spell", m_levelStats.SpellAvailable);
            profile.Set("cutscene", m_nextCutscene);
            profile.Set("game_number", m_gameNumber);
        }

        public void StartNewLevel(int level)
        {
            m_damage = 0.0f;
            m_CurrentLevel = level;
            m_CurrentDay = 0;
            m_nextCutscene = CS_Intro;
            m_kills = 0;
            CLevelStats stats = GetLevelStats();
            stats.reset();
            Level levelinfo = loadLevelInfo(getLevelPath(level));
            stats.SpellAvailable = levelinfo.spellAvailableInShop;
            for (int i = 0; i < WT_Count; i++)
            {
                if (CGameConfig.AllWeaponsSold) stats.WeaponLevel[i] = i == WT_Spell ? (LaunchOptions.Spell != -2 ? LaunchOptions.Spell : ST_Tornado) : WL_3;
                else if (i == WT_Spell)
                {
                    stats.WeaponLevel[i] = levelinfo.spellBought ? stats.SpellAvailable : ST_None;
                    stats.WeaponAvailable[i] = stats.SpellAvailable != ST_None;
                }
                else
                {
                    stats.WeaponLevel[i] = levelinfo.weaponStartingLevel[i];
                    stats.WeaponAvailable[i] = levelinfo.weaponAvailableInShop[i];
                }
            }
            ++m_gameNumber;
            WriteProfileStats();
            if (!m_isEndless) CAchievements.GetInstance().setValue("Cash & Carry", 0);
            __MGR_STATES().switchState(new GSLevel());
        }

        public int getUnlockLevel(int gd)
        {
            switch (gd)
            {
                case GD_Easy: return m_unlockLevelEasy;
                case GD_Hard: return m_unlockLevelHard;
                default: return m_unlockLevelNormal;
            }
        }

        public void SetPlayState(int state)
        {
            if (m_PlayState == state) return;
            m_PlayState = state;
            m_hudAlphaTarget = (m_PlayState == PS_IGM || m_PlayState == PS_MissionOverTimer || m_PlayState == PS_MissionOver || m_PlayState == PS_Restart) ? 0.0f : 1.0f;
            switch (m_PlayState)
            {
                case PS_Begin:
                    ResetLevel();
                    m_time = -999999;
                    m_isLevelOver = false;
                    break;
                case PS_Restart:
                    if (!m_isEndless) ReadProfileStats();
                    fadeOut(1.0f);
                    break;
                case PS_IGM:
                case PS_Pause:
                    pauseUnits();
                    __MGR_SOUNDS().stopAllSfx();
                    __MGR_SOUNDS().play(SFX_MENU_PAUSE);
                    break;
                case PS_Play: resumeUnits(); break;
                case PS_MissionOverTimer:
                    if (IsMissionSuccess())
                        SetMusic(m_challenge.getType() != CT_None && m_challenge.isChallengeCompleted() ? M_LEVEL_COMPLETE_WITH_CHALENGE : M_LEVEL_COMPLETE_NO_CHALENGE);
                    else SetMusic(M_LOSE);
                    break;
                case PS_MissionOver:
                    __MGR_SOUNDS().stopAllSfx();
                    SetNextMusic(M_DEBRIEFING, true);
                    break;
                case PS_EnterIGM: PauseMusic(); break;
                case PS_ExitIGM:
                    SetPlayState(PS_Play);
                    ResumeMusic();
                    break;
                case PS_Shop:
                    SetMusic(-1);
                    __MGR_STATES().pushState(new GS_Shop());
                    break;
            }
            if (IsLevelLoaded())
            {
                bool on = m_PlayState == PS_Play;
                m_fireball.getTouch().SetEnabled(on);
                m_iceball.getTouch().SetEnabled(on);
                m_griffin.getTouch().SetEnabled(on);
            }
        }

        void StartCutscene()
        {
            if (LaunchOptions.SkipCutscene)
            {
                m_lightball = false;
                CTimelineController cc = Timeline(m_cameraScene);
                cc.setLoop(false);
                cc.setClip("sceneloop");
                SetPlayState(PS_CutScene);
                EndCutscene();
                return;
            }
            m_fireball.getTouch().SetEnabled(false);
            m_iceball.getTouch().SetEnabled(false);
            m_griffin.getTouch().SetEnabled(false);
            SetPlayState(PS_CutScene);
            CGameConfig.RenderToTexture = false;
            m_lightball = false;
            CUserProfile profile = __PROFILE();
            int count = profile.GetInt("cut_scene_count" + m_nextCutscene);
            if (m_nextCutscene != CS_Intro && count >= 1 && RandFloat(0, 1) < 0.5f) SetNextCutscene(CS_Intro);
            string key = "cut_scene_count" + m_nextCutscene;
            profile.Set(key, profile.GetInt(key) + 1);
            CTimelineController cameraControl = Timeline(m_cameraScene);
            cameraControl.setLoop(false);
            switch (m_nextCutscene)
            {
                case CS_Intro: cameraControl.setClip("caste_intro"); break;
                case CS_Hydra:
                    cameraControl.setClip("hydra_intro");
                    PlaySfx(SFX_HYDRA_ANIMATION);
                    m_lightball = true;
                    m_hydra.Head[0].setAnimAnim("plaing", "idle");
                    break;
                case CS_Hydra3Heads:
                    cameraControl.setClip("hydra_intro");
                    PlaySfx(SFX_HYDRA_ANIMATION);
                    m_lightball = true;
                    m_hydra.Head[0].setAnimAnim("plaing", "idle");
                    m_hydra.Head[1].setAnimAnim("playing_left", "idle_low");
                    m_hydra.Head[2].setAnimAnim("playing_right", "idle_low");
                    break;
                case CS_Griffin:
                    cameraControl.setClip("griffin_intro");
                    m_griffin.getControl().setAnimAnim("activate", "active");
                    PlaySfx(SFX_GRIFIN_ANIMATION);
                    break;
                case CS_HydraGriffin:
                    cameraControl.setClip("hydra_griffin_intro");
                    m_hydra.Head[0].setAnimAnim("argue", "idle");
                    m_griffin.getControl().setAnimAnim("argue", "active");
                    PlaySfx(SFX_GRIFFIN_HYDRA_ANIMATION);
                    break;
            }
            SetNextCutscene(CS_Intro);
        }

        void EndCutscene()
        {
            CTimelineController cameraControl = Timeline(m_cameraScene);
            cameraControl.setClip("sceneloop");
            cameraControl.setLoop(false);
            m_time = m_challenge.getType() == CT_None ? -k_dayTextEndTime : -k_dayChallengeTextEndTime;
            CGameConfig.RenderToTexture = true;
            SetPlayState(PS_Play);
        }

        /// <summary>Skips the running cutscene (used by the editor's play mode and test automation).</summary>
        public void SkipCutscene() { if (m_PlayState == PS_CutScene) EndCutscene(); }

        // ------------------------------------------------------------------ fade

        public void fadeIn(float duration)
        {
            m_hudAlpha = 0.0f;
            if (duration == 0) { m_fadeAlpha = 0; m_fadeDir = 0; }
            else { m_fadeAlpha = 255; m_fadeDuration = duration; m_fadeTimer = 0; m_fadeDir = -1; }
        }

        public void fadeOut(float duration)
        {
            if (duration == 0) { m_fadeAlpha = 255; m_fadeDir = 0; }
            else { m_fadeAlpha = 0; m_fadeDuration = duration; m_fadeTimer = 0; m_fadeDir = +1; }
        }

        void UpdateFade(float time)
        {
            if (m_fadeDir == 0) return;
            m_fadeAlpha = CLAMP((int)(255 * m_fadeTimer / m_fadeDuration), 0, 255);
            if (m_fadeDir < 0) m_fadeAlpha = 255 - m_fadeAlpha;
            if (m_fadeTimer >= m_fadeDuration) m_fadeDir = 0;
            m_fadeTimer += time;
        }

        public void DrawFade()
        {
            if (m_fadeAlpha > 0) Gfx.draw2DRectangle(new SColor(m_fadeAlpha, 0, 0, 0), new r2i(0, 0, 480, 320));
        }

        // ------------------------------------------------------------------ sfx arbitration

        public int PlaySfx(int soundId, bool loop = false, int fadeInTime = 0, int decreasePriority = 0)
        {
            CSoundManager sm = __MGR_SOUNDS();
            SSfxInfo info;
            if (!m_sfxList.TryGetValue(soundId, out info))
            {
                info = new SSfxInfo { id = soundId };
                string soundName = sm.GetSoundNameFromIndex(soundId);
                IAttributes configAttr = soundName != null ? CGameConfig.GetInstance().getAttributes("soundsconfig", soundName) : null;
                if (configAttr != null)
                {
                    info.priority = configAttr.getAttributeAsInt("Priority");
                    info.maxPlayingInstances = configAttr.getAttributeAsInt("Overlap");
                    info.volume = configAttr.getAttributeAsFloat("Volume");
                }
                else
                {
                    info.priority = k_defaultSfxPriority;
                    info.maxPlayingInstances = k_defaultSfxOverlap;
                    info.volume = k_defaultSfxVolume;
                }
                m_sfxList[soundId] = info;
            }
            if (!CheckSoundPriority(info, decreasePriority)) return -1;
            int soundIndex = sm.play(soundId, loop, fadeInTime, info.volume);
            if (soundIndex != -1)
            {
                info.soundIndices.Add(soundIndex);
                info.numPlayingInstances++;
                m_totalPlayingInstances++;
            }
            return soundIndex;
        }

        bool CheckSoundPriority(SSfxInfo info, int decreasePriority)
        {
            if (m_totalPlayingInstances < k_maxPlayingUnitSounds) return true;
            if (info.numPlayingInstances >= info.maxPlayingInstances)
            {
                StopFistSfxInstance(info.id, k_sfxFadeoutTimer);
                return true;
            }
            foreach (SSfxInfo s in m_sfxList.Values)
            {
                if (s.numPlayingInstances > s.maxPlayingInstances)
                {
                    StopFistSfxInstance(s.id, k_sfxFadeoutTimer);
                    return true;
                }
            }
            int soundId = -1, minPriority = 1000;
            foreach (SSfxInfo s in m_sfxList.Values)
            {
                if (info.id == s.id || s.numPlayingInstances <= 0) continue;
                if (s.priority < minPriority) { minPriority = s.priority; soundId = s.id; }
            }
            if (soundId != -1 && minPriority < info.priority - decreasePriority)
            {
                StopFistSfxInstance(soundId, k_sfxFadeoutTimer);
                return true;
            }
            return false;
        }

        void StopFistSfxInstance(int soundId, int fadeOutTime)
        {
            SSfxInfo info;
            if (!m_sfxList.TryGetValue(soundId, out info) || info.soundIndices.Count == 0) return;
            __MGR_SOUNDS().stop(soundId, info.soundIndices[0], fadeOutTime);
            info.numPlayingInstances--;
            info.soundIndices.RemoveAt(0);
            m_totalPlayingInstances--;
        }

        public void StopSfx(int soundId, int soundIndex = 0, int fadeOutTime = 0)
        {
            SSfxInfo info;
            if (m_sfxList.TryGetValue(soundId, out info))
            {
                for (int i = info.soundIndices.Count - 1; i >= 0; i--)
                {
                    if (info.soundIndices[i] != soundIndex) continue;
                    info.soundIndices.RemoveAt(i);
                    info.numPlayingInstances--;
                    m_totalPlayingInstances--;
                }
            }
            __MGR_SOUNDS().stop(soundId, soundIndex, fadeOutTime);
        }

        void UpdateSfx()
        {
            CSoundManager sm = __MGR_SOUNDS();
            List<int> remove = null;
            foreach (SSfxInfo info in m_sfxList.Values)
            {
                if (info.numPlayingInstances > 0)
                {
                    for (int i = info.soundIndices.Count - 1; i >= 0; i--)
                    {
                        if (sm.isSoundPlaying(info.id, info.soundIndices[i])) continue;
                        info.soundIndices.RemoveAt(i);
                        info.numPlayingInstances--;
                        m_totalPlayingInstances--;
                    }
                }
                if (info.numPlayingInstances <= 0) (remove ?? (remove = new List<int>())).Add(info.id);
            }
            if (remove != null) foreach (int id in remove) m_sfxList.Remove(id);
        }

        // ------------------------------------------------------------------ touch

        public CTouchElement getTouch() { return m_touch; }

        void updateTouch()
        {
            bool first = m_touch.m_assignedTouchIndex != -1;
            bool second = m_touch.m_assignedTouchIndex2 != -1;
            m_isNowActivated = (first && !m_firstTouch) || (second && !m_secondTouch);
            m_isNowReleased = (!first && m_firstTouch) || (!second && m_secondTouch);
            if (!first && !second) m_touch.m_useFirstTouchForDefault = true;
            else
            {
                bool inUse = m_unitDragged || m_spell.isCasting();
                if (!inUse && first && second && !m_secondTouch) m_touch.m_useFirstTouchForDefault = false;
            }
            m_firstTouch = first;
            m_secondTouch = second;
        }

        public void reassignTouchTo(CTouchElement touch)
        {
            m_touch.ReassignTouchTo(touch);
            m_isNowActivated = false;
            m_isNowReleased = false;
            m_firstTouch = false;
            m_secondTouch = false;
        }

        public bool isNowActivatedTouch() { return m_isNowActivated; }
        public bool isNowReleasedTouch() { return m_isNowReleased; }

        public bool isActiveTouch()
        {
            CTouchElement touch = getTouch();
            if (touch == null) return false;
            return touch.m_useFirstTouchForDefault ? m_firstTouch : m_secondTouch;
        }

        public void EnableIngameTouchInput() { m_touch.SetEnabled(true); m_touch.ResetInput(); }
        public void DisableIngameTouchInput() { m_touch.SetEnabled(false); m_touch.ResetInput(); }

        // ------------------------------------------------------------------ intro unit camera

        public void showIntroUnitInfo(CUnit unit)
        {
            SetPlayState(PS_IntroUnitCamera);
            m_touch.SetEnabled(false);
            m_introTime = 0.0f;
            m_introUnit = unit;
            m_introUnitStartPos = unit.getPosition();
            m_introUnitStartRot = unit.getSceneNode().getRotation();
            m_introUnit.hideAura();
            m_backTouch = new CTouchElement(CTouchElement.Type_AnyTouch, 0);
            m_backTouch.SetScreenCoordinates(40, 40, 60, 50);
            m_backTouch.SetEnabled(false);
            m_introGoingBack = false;
            pauseUnits();
            if (m_iceball != null) m_iceball.setIntroState();
            __MGR_SOUNDS().play(SFX_ENEMY_TUTORIAL_APPEAR_IN);
        }

        const float k_time = 0.4f;
        const float k_unitTransitionTime = 0.75f;
        const float k_fadeTransitionTime = 0.75f;

        static v3f worldPosFromScreenAndDistance(p2i screen, float distanceFromCamera)
        {
            line3df ray = __IRR_SCENE().getRayFromScreenCoordinates(screen);
            return ray.start + ray.getVector().setLength(distanceFromCamera);
        }

        static int GetInfoText(CUnit unit)
        {
            switch (unit.getUnitKind())
            {
                case UK_Boar: return unit.isFreeBoar() ? STRING_IDS.HELP_TUTO_BOAR_TITLE : STRING_IDS.HELP_TUTO_BOAR_AGGR_TITLE;
                case UK_Bomblin: return STRING_IDS.HELP_TUTO_BOMB_GOBLINS_TITLE;
                case UK_Elf: return STRING_IDS.HELP_TUTO_DARK_ELVES_TITLE;
                case UK_Goblin: return STRING_IDS.HELP_TUTO_GOBLINS_TITLE;
                case UK_Shaman: return unit.isMindControllingShaman() ? STRING_IDS.HELP_TUTO_WARLOCK_BOAR_TITLE : STRING_IDS.HELP_TUTO_WARLOCKS_TITLE;
                case UK_Skeleton: return unit.isRedSkeleton() ? STRING_IDS.HELP_TUTO_SKELETONS_TITLE : STRING_IDS.HELP_TUTO_SKELETON_COLOUR_TITLE;
                case UK_Troll: return STRING_IDS.HELP_TUTO_TROLL_TITLE;
                default: return STRING_IDS.HELP_TUTO_GOBLINS_TITLE;
            }
        }

        void updateIntroUnitCamera(float time)
        {
            v3f targetPos = worldPosFromScreenAndDistance(new p2i(130, 240), 8.5f);
            quaternion targetRot = new quaternion(0, -0.25f * PI, 0);
            float t = MIN(m_introTime / k_unitTransitionTime, 1.0f);
            m_introUnit.setMPosition(m_introUnitStartPos + (targetPos - m_introUnitStartPos) * t);
            quaternion q = new quaternion();
            q.slerp(quaternion.Identity, targetRot, t);
            m_introUnit.getSceneNode().setRotation(q * m_introUnitStartRot);
            m_introUnit.UpdateShadow(time);
            if (!m_introGoingBack && m_introTime > k_unitTransitionTime + k_fadeTransitionTime)
            {
                m_backTouch.SetEnabled(true);
                int tx, ty;
                m_backTouch.GetTouchPos_Current(out tx, out ty);
                if (m_backTouch.IsNowReleased() && m_backTouch.IsPointInside(new p2i(tx, ty)))
                {
                    m_introGoingBack = true;
                    m_introTime = k_unitTransitionTime + k_fadeTransitionTime;
                    m_introUnitStartPos = m_introUnit.getPath().end - m_introUnit.getPath().getVector().setLength(10.0f);
                    __MGR_SOUNDS().play(SFX_MENU_BACK);
                    __MGR_SOUNDS().play(SFX_ENEMY_TUTORIAL_APPEAR_OUT);
                }
            }
            m_introTime += m_introGoingBack ? -time : time;
            if (m_introGoingBack && m_introTime <= 0.0f)
            {
                m_introUnit.setMPosition(m_introUnitStartPos);
                m_touch.SetEnabled(true);
                m_backTouch.Dispose();
                m_backTouch = null;
                SetPlayState(PS_Play);
                if (m_iceball != null) m_iceball.setOldState();
                m_introUnit.setVelocity(v3f.Zero);
            }
        }

        void DrawIntroUnitCamera()
        {
            float q = MIN(m_introTime / k_time, 1.0f);
            Gfx.draw2DRectangle(new SColor(0xff000000), new r2i(0, 0, 480, (int)(q * 40)));
            Gfx.draw2DRectangle(new SColor(0xff000000), new r2i(0, (int)(320 - q * 40), 480, 320));
            Gfx.endScene2D();
            float t = MIN((m_introTime - k_unitTransitionTime) / k_fadeTransitionTime, 1.0f);
            int h = (int)(t * 240);
            if (m_introTime > k_unitTransitionTime)
            {
                SColor c = new SColor(0x9f000000);
                S3DVertex[] vertices =
                {
                    new S3DVertex(worldPosFromScreenAndDistance(new p2i(0, 160 - h / 2), 11.0f), c, new v2f()),
                    new S3DVertex(worldPosFromScreenAndDistance(new p2i(480, 160 - h / 2), 11.0f), c, new v2f()),
                    new S3DVertex(worldPosFromScreenAndDistance(new p2i(0, 160 + h / 2), 11.0f), c, new v2f()),
                    new S3DVertex(worldPosFromScreenAndDistance(new p2i(480, 160 + h / 2), 11.0f), c, new v2f()),
                };
                int[] indices = { 0, 2, 1, 1, 2, 3 };
                SMaterial m = new SMaterial { MaterialType = E_MATERIAL_TYPE.EMT_TRANSPARENT_TEXTURE_VERTEX_ALPHA, Lighting = false, ZBuffer = false };
                Gfx.setTransform(E_TRANSFORMATION_STATE.ETS_WORLD, matrix4.Identity);
                Gfx.drawVertexPrimitiveList(vertices, 4, indices, 6, m);
            }
            Gfx.beginScene2D();
            if (m_introTime > k_unitTransitionTime)
            {
                ASprite.PushClipRect(new r2i(0, 160 - h / 2, 480, 160 + h / 2));
                int txt = GetInfoText(m_introUnit);
                __FONT("MM_Help").DrawString(_TXT_INT(txt), 240, 60, ASprite.HCENTER | ASprite.VCENTER);
                __FONT("MM_Help").DrawWrappedText(_TXT_INT(txt + 1), 350, 180, 220, ASprite.HCENTER | ASprite.VCENTER);
                int tx, ty;
                m_backTouch.GetTouchPos_Current(out tx, out ty);
                int x = m_backTouch.m_screenX + m_backTouch.m_screenW / 2;
                int yy = m_backTouch.m_screenY + m_backTouch.m_screenH / 2;
                m_sprIngame.PaintAFrame(m_backTouch.IsActive() && m_backTouch.IsPointInside(new p2i(tx, ty)) ?
                    MENU_A_MENU_BACK_BUTTON_PRESSED : MENU_A_MENU_BACK_BUTTON_NORMAL, 0, x, yy, 0, 0, 0);
                ASprite.PopClipRect();
            }
        }

        public void SetEndlessScores(Menu_Ingame menu)
        {
            menu.SetEndlessScores(m_kills, m_CurrentDay, m_levelStats.Gold + m_gold, m_levelStats.Souls + m_souls);
        }

        public void SetLevelScores(Menu_Ingame menu)
        {
            int gold = m_levelStats.Gold + m_gold;
            int souls = m_levelStats.Souls + m_souls;
            for (int i = 0; i < WT_Count; ++i)
            {
                if (m_levelStats.WeaponLevel[i] == WL_None) continue;
                SUpgradeInfo info = SUpgradeInfo.GetWeaponInfo(i, m_levelStats.WeaponLevel[i]);
                gold += info.getPriceGold();
                souls += info.getPriceSouls();
            }
            menu.SetLevelScores(gold, souls, getDifficulty());
        }
    }
}
