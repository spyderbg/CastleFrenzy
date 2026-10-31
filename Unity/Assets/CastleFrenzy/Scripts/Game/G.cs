// Global helpers replacing the C++ macros (_TXT, __FONT, __PROFILE, CLAMP...) and game enums.
// Enums are int constants so ported arithmetic and comparisons stay identical to the C++ code.
using System;
using System.Collections.Generic;

namespace CastleFrenzy
{
    public static class G
    {
        public const int OS_SCREEN_W = 480, OS_SCREEN_H = 320;
        public const int GAME_SCREEN_W = 480, GAME_SCREEN_H = 320;
        public const int GAME_SCREEN_W2 = 240, GAME_SCREEN_H2 = 160;
        public const float PI = 3.14159265359f;
        public const float DEGTORAD = PI / 180.0f;
        public const float RADTODEG = 180.0f / PI;

        // ---- managers
        public static string _TXT(int id) { return CStringTable.GetInstance().GetText(id); }
        public static string _TXT_INT(int id) { return CStringTable.GetInstance().GetText(id); }
        public static CFont __FONT(string name) { return CFontManager.GetInstance().GetFont(name); }
        public static CSoundManager __MGR_SOUNDS() { return CSoundManager.GetInstance(); }
        public static CStateMachine __MGR_STATES() { return CStateMachine.GetInstance(); }
        public static CStringTable __MGR_STRINGS() { return CStringTable.GetInstance(); }
        public static CSceneManager __IRR_SCENE() { return CSceneManager.Instance; }
        public static CUserProfile __PROFILE() { return Profile.Get(); }
        public static CGameConfig __GAME_CONFIG() { return CGameConfig.GetInstance(); }
        public static CApplication __APP() { return CApplication.GetInstance(); }

        public static bool IS_LANG_NONE() { return CStringTable.GetInstance().GetLanguage() == ELanguages.Language_None; }
        public static bool IS_LANG_ENGLISH() { return CStringTable.GetInstance().GetLanguage() == ELanguages.Language_English; }
        public static bool IS_LANG_SPANISH() { return CStringTable.GetInstance().GetLanguage() == ELanguages.Language_Spanish; }
        public static bool IS_LANG_FRENCH() { return CStringTable.GetInstance().GetLanguage() == ELanguages.Language_French; }
        public static bool IS_LANG_DEUTCH() { return CStringTable.GetInstance().GetLanguage() == ELanguages.Language_Deutsch; }
        public static bool IS_LANG_ITALIAN() { return CStringTable.GetInstance().GetLanguage() == ELanguages.Language_Italian; }
        public static bool IS_LANG_JAPANESE() { return CStringTable.GetInstance().GetLanguage() == ELanguages.Language_Japanese; }

        // ---- math macros
        public static int CLAMP(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }
        public static float CLAMP(float v, float lo, float hi) { return v < lo ? lo : (v > hi ? hi : v); }
        public static int ABS(int v) { return v < 0 ? -v : v; }
        public static float ABS(float v) { return v < 0 ? -v : v; }
        public static int MIN(int a, int b) { return a < b ? a : b; }
        public static int MAX(int a, int b) { return a > b ? a : b; }
        public static float MIN(float a, float b) { return a < b ? a : b; }
        public static float MAX(float a, float b) { return a > b ? a : b; }
        public static int SGN(float x) { return x > 0 ? 1 : -1; }
        public static float sqrtf(float x) { return (float)Math.Sqrt(x); }
        public static float sinf(float x) { return (float)Math.Sin(x); }
        public static float cosf(float x) { return (float)Math.Cos(x); }
        public static float tanf(float x) { return (float)Math.Tan(x); }
        public static float atan2f(float y, float x) { return (float)Math.Atan2(y, x); }
        public static float acosf(float x) { return (float)Math.Acos(x); }
        public static float asinf(float x) { return (float)Math.Asin(x); }
        public static float fabsf(float x) { return Math.Abs(x); }
        public static float powf(float x, float y) { return (float)Math.Pow(x, y); }
        public static float floorf(float x) { return (float)Math.Floor(x); }
        public static float ceilf(float x) { return (float)Math.Ceiling(x); }
        public static float fmodf(float x, float y) { return x % y; }
        public static float expf(float x) { return (float)Math.Exp(x); }

        // ---- C rand()
        public const int RAND_MAX = 0x7fffffff;
        static Random s_random = new Random();
        public static void srand(int seed) { s_random = new Random(seed); }
        public static int rand() { return s_random.Next(0, RAND_MAX); }
        public static int RandInt(int min, int max) { return min + (rand() % (max - min + 1)); }
        public static float RandFloat(float min, float max) { return (rand() / (float)RAND_MAX) * (max - min) + min; }

        // ---- remaps (Game/Misc)
        public static int IntRemap1(int x, int f0, int f1, int t0, int t1)
        {
            long tw = t1 - t0, fw = f1 - f0;
            if (fw == 0) return (t0 + t1) / 2;
            return (int)(t0 + (fw + 2L * ((long)x - f0) * tw) / (2L * fw));
        }

        public static int IntRemap2(int x, int f0, int f1, int t0, int t1)
        {
            long tw = t1 - t0 + 1, fw = f1 - f0 + 1;
            return (int)(t0 + (2L * ((long)x - f0) + 1L) * tw / (2L * fw));
        }

        public static int IntRemap3(int x, int f0, int f1, int t0, int t1)
        {
            if (f0 == f1) return t0;
            if (x < (f0 + f1) / 2) return t0 + (t1 - t0 + 1) * (x - f0) / (f1 - f0);
            return t1 - (t1 - t0 + 1) * (f1 - x) / (f1 - f0);
        }

        public static float FloatRemap(float x, float f0, float f1, float t0, float t1)
        {
            if (f1 - f0 == 0.0f) return (t0 + t1) / 2.0f;
            return t0 + (x - f0) / (f1 - f0) * (t1 - t0);
        }

        public static float LinearUpFlatDown(float x, float x1, float x2, float x3, float x4)
        {
            if (x < x1) return 0.0f;
            if (x < x2) return (x - x1) / (x2 - x1);
            if (x < x3) return 1.0f;
            if (x < x4) return (x4 - x) / (x4 - x3);
            return 0.0f;
        }

        public static float LinearUpFlatUp(float x, float x1, float x2, float x3, float x4)
        {
            if (x < x2) return (x - x2) / (x2 - x1);
            if (x < x3) return 0.0f;
            return (x - x3) / (x4 - x3);
        }

        public static float LinearStep(float x, float x1, float x2)
        {
            if (x < x1) return 0.0f;
            if (x < x2) return (x - x1) / (x2 - x1);
            return 1.0f;
        }

        public static v3f randomVectorInSphere(float radius = 1.0f)
        {
            v3f r;
            do { r = new v3f(RandFloat(-1, 1), RandFloat(-1, 1), RandFloat(-1, 1)); } while (r.dotProduct(r) > 1.0f);
            return r * radius;
        }

        public static v2f HermiteInterpolation(float t, v2f p1, v2f p2, v2f m1, v2f m2)
        {
            return p1 * ((1 + 2 * t) * (1 - t) * (1 - t)) + m1 * (t * (1 - t) * (1 - t)) + p2 * (t * t * (3 - 2 * t)) + m2 * (t * t * (t - 1));
        }

        public static v2f HermiteInterpolationDerivative(float t, v2f p1, v2f p2, v2f m1, v2f m2)
        {
            return p1 * (6 * (t - 1) * t) - p2 * (6 * (t - 1) * t) + m2 * (t * (3 * t - 2)) + m1 * ((t - 1) * (3 * t - 1));
        }

        public static v2f HermiteInterpolationSecondDerivative(float t, v2f p1, v2f p2, v2f m1, v2f m2)
        {
            return m1 * (6 * t - 4) + m2 * (6 * t - 2) + (p1 - p2) * (6 * (2 * t - 1));
        }

        public static v2f HermiteInterpolationOffset(float t, float offset, v2f p1, v2f p2, v2f m1, v2f m2)
        {
            v2f x = HermiteInterpolation(t, p1, p2, m1, m2);
            v2f dx = HermiteInterpolationDerivative(t, p1, p2, m1, m2);
            v2f n = new v2f(-dx.Y, dx.X);
            return x + n * (offset / n.getLength());
        }

        public static float HermiteInterpolationCurvature(float t, v2f p1, v2f p2, v2f m1, v2f m2)
        {
            v2f dx = HermiteInterpolationDerivative(t, p1, p2, m1, m2);
            v2f ddx = HermiteInterpolationSecondDerivative(t, p1, p2, m1, m2);
            v2f pddx = new v2f(-ddx.Y, ddx.X);
            return dx.dotProduct(pddx) * powf(dx.dotProduct(dx), -1.5f);
        }

        public static float GetFloatWithDefault(IAttributes attr, string name, float defaultVal)
        {
            int index = attr.findAttribute(name);
            return index >= 0 ? attr.getAttributeAsFloat(index) : defaultVal;
        }

        // ---- scene helpers (Game/Misc)
        public static p2i screenFrom3DPos(v3f pos3d) { return CSceneManager.Instance.getScreenCoordinatesFrom3DPosition(pos3d); }
        public static v2f vector2DFrom3DPos(v3f pos3d) { p2i p = screenFrom3DPos(pos3d); return new v2f(p.X, p.Y); }

        public static void cleanNode(ISceneNode node, bool unit = false, bool @ref = false)
        {
            List<ISceneNode> children = new List<ISceneNode>(node.Children);
            foreach (ISceneNode child in children) cleanNode(child, unit);
            if ((node.Name.Contains("_PIVOT") && node.Children.Count == 0) || node.getType() == ESCENE_NODE_TYPE.ESNT_LIGHT)
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
                if (unit || @ref)
                {
                    material.MaterialType = E_MATERIAL_TYPE.EMT_TRANSPARENT_ALPHA_CHANNEL_REF;
                    if (unit) material.BackfaceCulling = true;
                }
                if (node.getUID().Contains("light")) material.MaterialType = E_MATERIAL_TYPE.EMT_TRANSPARENT_ADD_COLOR;
                if (material.MaterialType == E_MATERIAL_TYPE.EMT_SOLID) material.MaterialType = E_MATERIAL_TYPE.EMT_TRANSPARENT_ALPHA_CHANNEL_REF;
            }
        }

        public static void setEmissiveColor(ISceneNode node, SColor color)
        {
            foreach (ISceneNode child in node.Children) setEmissiveColor(child, color);
            for (int i = 0; i < node.getMaterialCount(); ++i) node.getMaterial(i).EmissiveColor = color;
        }

        /// <summary>LoadColladaScene: loads a .bdae scene and switches its animator to step interpolation.</summary>
        public static ISceneNode LoadColladaScene(string filename, bool cache = false)
        {
            ISceneNode result = CSceneManager.Instance.loadColladaScene(filename);
            if (result != null && result.getFirstAnimator() != null)
                result.getFirstAnimator().setInterpolationMode(ISceneNodeAnimator.E_INTERPOLATION_MODE.EIT_STEP);
            return result;
        }

        public static CTimelineController Timeline(ISceneNode node) { return node.getFirstAnimator().getTimelineCtrl(); }

        public static string FormatString(string fmt, params object[] args) { return CStringFormat.Format(fmt, args); }

        /// <summary>Utils/StringUtils FormatNumber: per-language thousands separators.</summary>
        public static string FormatNumber(int number)
        {
            bool separate = (number > 9999 && IS_LANG_SPANISH()) || (number > 999 && IS_LANG_DEUTCH()) ||
                (number > 999 && (IS_LANG_ENGLISH() || (number > 9999 && IS_LANG_FRENCH()) || (number > 9999 && IS_LANG_JAPANESE()) || (number > 999 && IS_LANG_ITALIAN())));
            if (!separate) return number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            char sep = IS_LANG_DEUTCH() ? '.' : (IS_LANG_ENGLISH() || IS_LANG_JAPANESE()) ? ',' : ' ';
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            string digits = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0) sb.Append(sep);
                sb.Append(digits[i]);
            }
            return sb.ToString();
        }
        public static void RemoveUnusedTextures() { }
        public static void CleanColladaCache(bool whole) { }
        public static void FrameEnded(int ms) { }
        public static void AddFrameEvent(int type, int data) { }
    }

    /// <summary>Minimal printf-style formatting (%s %d %i %u %f %.Nf %02d %%) used by the string helpers.</summary>
    public static class CStringFormat
    {
        public static string Format(string fmt, params object[] args)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            int ai = 0;
            for (int i = 0; i < fmt.Length; i++)
            {
                char c = fmt[i];
                if (c != '%') { sb.Append(c); continue; }
                if (i + 1 < fmt.Length && fmt[i + 1] == '%') { sb.Append('%'); i++; continue; }
                int j = i + 1;
                string flags = "";
                while (j < fmt.Length && "-+ 0#".IndexOf(fmt[j]) >= 0) flags += fmt[j++];
                int width = 0;
                while (j < fmt.Length && char.IsDigit(fmt[j])) width = width * 10 + (fmt[j++] - '0');
                int prec = -1;
                if (j < fmt.Length && fmt[j] == '.') { j++; prec = 0; while (j < fmt.Length && char.IsDigit(fmt[j])) prec = prec * 10 + (fmt[j++] - '0'); }
                while (j < fmt.Length && (fmt[j] == 'l' || fmt[j] == 'h')) j++;
                if (j >= fmt.Length) break;
                char conv = fmt[j];
                object a = ai < args.Length ? args[ai++] : null;
                string s;
                switch (conv)
                {
                    case 'd': case 'i': case 'u':
                        s = Convert.ToInt64(a ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    case 'x': s = Convert.ToInt64(a ?? 0).ToString("x"); break;
                    case 'X': s = Convert.ToInt64(a ?? 0).ToString("X"); break;
                    case 'f':
                        s = Convert.ToDouble(a ?? 0).ToString("F" + (prec < 0 ? 6 : prec), System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    case 'c': s = a is char ? a.ToString() : ((char)Convert.ToInt32(a ?? 0)).ToString(); break;
                    default: s = a != null ? a.ToString() : ""; break;
                }
                if (width > s.Length)
                {
                    bool left = flags.Contains("-");
                    char pad = flags.Contains("0") && !left && conv != 's' ? '0' : ' ';
                    if (pad == '0' && s.StartsWith("-")) s = "-" + s.Substring(1).PadLeft(width - 1, '0');
                    else s = left ? s.PadRight(width) : s.PadLeft(width, pad);
                }
                sb.Append(s);
                i = j;
            }
            return sb.ToString();
        }
    }

    /// <summary>Game enums from the C++ headers, as int constants.</summary>
    public static class GE
    {
        // EWeaponType
        public const int WT_Gate = 0, WT_Hydra = 1, WT_Fireball = 2, WT_Iceball = 3, WT_Griffin = 4, WT_Spell = 5, WT_Count = 6, WT_Invalid = -1, WT_First = 0, WT_Last = WT_Count - 1;
        // EWeaponLevel
        public const int WL_1 = 0, WL_2 = 1, WL_3 = 2, WL_Count = 3, WL_None = -1, WL_First = 0, WL_Last = WL_Count - 1;
        // ESpellType
        public const int ST_Boulder = 0, ST_Lightning = 1, ST_GhostArmy = 2, ST_Tornado = 3, ST_Count = 4, ST_None = -1;
        // EUnitDamageSource
        public const int UDS_GroundFall = 0, UDS_GroundDrag = 1, UDS_WallHit = 2, UDS_Fireball = 3, UDS_GriffinSmash = 4, UDS_TrollSmash = 5,
            UDS_BoarSmash = 6, UDS_Petrify = 7, UDS_Lightning = 8, UDS_Tornado = 9, UDS_GhostArmy = 10;
        // UnitKind
        public const int UK_Goblin = 0, UK_Boar = 1, UK_BoarRider = 2, UK_Shaman = 3, UK_Troll = 4, UK_Elf = 5, UK_Skeleton = 6, UK_Bomblin = 7, UK_Unknown = 8;
        // GameDifficulty
        public const int GD_Easy = 0, GD_Normal = 1, GD_Hard = 2, GD_Num = 3;
        // ChallengeType
        public const int CT_None = 0, CT_PerfectHealth = 1, CT_GoblinWallSmash = 2, CT_ShamanWallSmash = 3, CT_BoarRampage = 4, CT_GriffinFireballMaster = 5,
            CT_GriffinFireballSuperMaster = 6, CT_FlickMaster = 7, CT_OneHitOneKill = 8, CT_ManyEnemiesWithSpecial = 9, CT_ThatWasClose = 10,
            CT_WallSmasher = 11, CT_GroundSmasher = 12, CT_MenuGoblins = 13, CT_MenuTrolls = 14, CT_MenuShamans = 15, CT_ShamanCombo = 16,
            CT_TrollCombo = 17, CT_ComboMaster = 18, CT_Count = 19;
        // EUnitState
        public const int US_Walking = 0, US_Dragged = 1, US_Falling = 2, US_FallingOnHydra = 3, US_FallingInWater = 4, US_ToBeEeatenByHydra = 5,
            US_BeingEatenByHydra = 6, US_Petrified = 7, US_Stunned = 8, US_Stumble = 9, US_Attacking = 10, US_Dying = 11, US_Dead = 12, US_Victory = 13,
            US_Riding = 14, US_TurningAroundToRunAway = 15, US_RunningAwayThrowingStuffAround = 16, US_MindControlling = 17, US_ElfWallGrab = 18,
            US_ElfWallFall = 19, US_SkeletonAppear = 20, US_SkeletonLand = 21, US_Tornado = 22, US_Earthquake = 23, US_RunningAway = 24, US_None = 25;
        // EUnitAnimation
        public const int UA_Walk_Normal = 0, UA_Walk_Injured = 1, UA_Walk_Victory = 2, UA_Walk_Fast = 3, UA_Walk_Charge = 4, UA_Dragged = 5,
            UA_DraggedMindControl = 6, UA_Falling = 7, UA_Flying = 8, UA_FlyingToFalling = 9, UA_Stunned = 10, UA_Stumble = 11, UA_Attack = 12,
            UA_Die_GroundFall = 13, UA_Die_GroundSmash = 14, UA_Die_WallHit = 15, UA_Riding_Walk = 16, UA_Riding_Attack = 17, UA_Riding_Dragged = 18,
            UA_Riding_Stunned = 19, UA_Riding_Victory = 20, UA_TurnAround = 21, UA_MindControl = 22, UA_ElfWallGrab = 23, UA_ElfWallFall = 24,
            UA_ElfWallLand = 25, UA_ElfJump = 26, UA_ElfUnhangHigh = 27, UA_ElfUnhangLow = 28, UA_ElfSlide = 29, UA_SkeletonAppear = 30,
            UA_SkeletonLand = 31, UA_Earthquake = 32, UA_TotalCount = 33, UA_Invalid = -1;
        // FrameEventType
        public const int FET_Loading = 0, FET_SpawnUnit = 1, FET_PlaySound = 2, FET_PlayMusic = 3, FET_UnitDying = 4, FET_UnitDead = 5;
        // eTransitionType
        public const int transition_none = 0, transition_finishing = 1, transition_show = 2, transition_hide = 3, transition_showLeft = 4,
            transition_hideLeft = 5, transition_showRight = 6, transition_hideRight = 7, transition_showTop = 8, transition_hideTop = 9,
            transition_showWithZoom = 10, transition_hideWithZoom = 11, transition_stretch_show = 12, transition_stretch_hide = 13;
        // GlobalMenuActions
        public const int button_action_gain_focus = -123456, button_action_lose_focus = -123455;
    }
}
