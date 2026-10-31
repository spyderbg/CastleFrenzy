// Common/StateMachine, Common/StringTable and IO/TouchScreen/TouchPad ported from the C++ game.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CastleFrenzy
{
    public enum ELanguages
    {
        Language_None = -1,
        Language_English = 0,
        Language_French,
        Language_Spanish,
        Language_Deutsch,
        Language_Italian,
        Language_Japanese,
        Language_Count
    }

    public class CStringTable
    {
        static CStringTable s_instance;
        public static CStringTable GetInstance() { return s_instance ?? (s_instance = new CStringTable()); }

        string[] m_strings = new string[0];
        ELanguages m_currentLanguage = ELanguages.Language_None;

        public ELanguages GetLanguage() { return m_currentLanguage; }
        public ELanguages GetDefaultLanguage() { return ELanguages.Language_English; }
        public bool SetToDefaultLanguage() { return SetLanguage(GetDefaultLanguage()); }

        public string GetText(int id)
        {
            if (id < 0 || id >= m_strings.Length) return "";
            return m_strings[id];
        }

        static string GetLanguageResString(ELanguages language)
        {
            switch (language)
            {
                case ELanguages.Language_English: return "FortressDefender_EN.txt";
                case ELanguages.Language_French: return "FortressDefender_FR.txt";
                case ELanguages.Language_Spanish: return "FortressDefender_ES.txt";
                case ELanguages.Language_Deutsch: return "FortressDefender_DE.txt";
                case ELanguages.Language_Italian: return "FortressDefender_IT.txt";
                case ELanguages.Language_Japanese: return "FortressDefender_JP.txt";
            }
            return "";
        }

        /// <summary>Binary table: count, offsets (uint32, relative to file start), UTF-16LE strings.</summary>
        public static string[] LoadTable(byte[] data)
        {
            int count = BitConverter.ToInt32(data, 0);
            string[] strings = new string[count];
            int prev = BitConverter.ToInt32(data, 4);
            int[] lens = new int[count];
            int[] starts = new int[count];
            for (int i = 0; i < count; i++)
            {
                int cur = (i < count - 1) ? BitConverter.ToInt32(data, 8 + i * 4) : data.Length;
                starts[i] = prev;
                lens[i] = (cur - prev) / 2;
                prev = cur;
            }
            for (int i = 0; i < count; i++)
            {
                int start = starts[i], len = lens[i];
                if (start < 0 || start + len * 2 > data.Length) { strings[i] = ""; continue; }
                string s = Encoding.Unicode.GetString(data, start, len * 2);
                int nul = s.IndexOf('\0');
                strings[i] = nul >= 0 ? s.Substring(0, nul) : s;
            }
            return strings;
        }

        bool Load(ELanguages language)
        {
            byte[] data = GameFiles.ReadAllBytes("Strings/" + GetLanguageResString(language));
            if (data == null) return false;
            m_strings = LoadTable(data);
            return true;
        }

        public bool SetLanguage(ELanguages language)
        {
            if (language == ELanguages.Language_None)
            {
                if (!Load(ELanguages.Language_English)) return false;
                m_currentLanguage = ELanguages.Language_None;
                return true;
            }
            if (m_currentLanguage == language) return true;
            if (!Load(language)) return false;
            m_currentLanguage = language;
            return true;
        }
    }

    public abstract class IStateBase
    {
        public virtual void ctor() { }
        public virtual void dtor() { }
        public virtual void pause() { }
        public virtual void resume() { }
        public virtual void update(float dt) { }
        public virtual void draw() { }
        public virtual void sleep() { }
        public virtual void wakeup() { }
    }

    public class CStateMachine
    {
        static CStateMachine s_instance;
        public static CStateMachine GetInstance() { return s_instance ?? (s_instance = new CStateMachine()); }

        readonly List<IStateBase> m_stateList = new List<IStateBase>();
        int m_stackIdx = -1;
        bool m_isInDrawCallback;
        IStateBase m_switchState, m_pushState;
        int m_popState;

        public int stackSize() { return m_stateList.Count; }
        public IStateBase Top { get { return m_stateList.Count > 0 ? m_stateList[m_stateList.Count - 1] : null; } }

        public T switchState<T>() where T : IStateBase, new() { T t = new T(); m_switchState = t; return t; }
        public void switchState(IStateBase t) { m_switchState = t; }
        public T pushState<T>() where T : IStateBase, new() { T t = new T(); m_pushState = t; return t; }
        public void pushState(IStateBase t) { m_pushState = t; }
        public void popState() { ++m_popState; }

        public void recurseDraw()
        {
            if (!m_isInDrawCallback) return;
            int previousIdx = m_stackIdx;
            if (m_stackIdx == -1) m_stackIdx = m_stateList.Count - 1;
            if (m_stackIdx > 0) m_stateList[--m_stackIdx].draw();
            m_stackIdx = previousIdx;
        }

        public void update(float timestamp)
        {
            if (m_switchState != null)
            {
                m_popState = m_stateList.Count;
                m_pushState = m_switchState;
                m_switchState = null;
            }
            bool pauseBeforePush = (m_popState == 0);
            bool resumeAfterPop = m_pushState == null;
            if (m_popState > 0)
            {
                while (m_popState > 0)
                {
                    m_stateList[m_stateList.Count - 1].dtor();
                    m_stateList.RemoveAt(m_stateList.Count - 1);
                    --m_popState;
                }
                if (resumeAfterPop && m_stateList.Count > 0) m_stateList[m_stateList.Count - 1].resume();
            }
            if (m_pushState != null)
            {
                if (pauseBeforePush && m_stateList.Count > 0) m_stateList[m_stateList.Count - 1].pause();
                m_stateList.Add(m_pushState);
                m_pushState = null;
                m_stateList[m_stateList.Count - 1].ctor();
            }
            if (m_stateList.Count > 0) m_stateList[m_stateList.Count - 1].update(timestamp);
        }

        public void draw()
        {
            if (m_stateList.Count > 0)
            {
                m_isInDrawCallback = true;
                m_stateList[m_stateList.Count - 1].draw();
                m_isInDrawCallback = false;
            }
        }

        public void sleep()
        {
            CSoundManager.GetInstance().stopAllSounds();
            for (int i = m_stateList.Count - 1; i >= 0; i--) m_stateList[i].sleep();
        }

        public void wakeup()
        {
            for (int i = 0; i < m_stateList.Count; i++) m_stateList[i].wakeup();
        }
    }

    // ---------------------------------------------------------------------------- touch

    public static class CTouchPad
    {
        public const int State_Off = 0, State_On = 1;
        public const int Type_AnyTouch = 0, Type_SingleTap = 1, Type_Drag = 2;
        public const int k_max_touches = 5;
        public const int k_tap_time = 400;
        public const int k_tap_max_interval = 500;
        public const int k_taps_max_displacement = 30;
        public const int k_min_drag_displacement = 1;

        public static int s_currentTime;
        public static int s_currentTiltX = -60, s_currentTiltY, s_currentTiltZ;

        static readonly int[] s_touchStartX = new int[k_max_touches], s_touchStartY = new int[k_max_touches];
        static readonly int[] s_touchCurrentX = new int[k_max_touches], s_touchCurrentY = new int[k_max_touches];
        static readonly int[] s_touchCurrentState = new int[k_max_touches];
        static readonly int[] s_touchPrevX = new int[k_max_touches], s_touchPrevY = new int[k_max_touches];
        static readonly int[] s_touchPrevState = new int[k_max_touches];
        static readonly int[] s_touchType = new int[k_max_touches];
        static readonly int[] s_touchTimestamp = new int[k_max_touches];
        static readonly bool[] s_touchHasNewEvent = new bool[k_max_touches];

        public static void InitTouchPad(bool resetTouches = true, bool resetTilt = true)
        {
            if (resetTouches)
            {
                for (int i = 0; i < k_max_touches; i++)
                {
                    s_touchStartX[i] = s_touchStartY[i] = -1;
                    s_touchCurrentX[i] = s_touchCurrentY[i] = -1;
                    s_touchCurrentState[i] = State_Off;
                    s_touchPrevX[i] = s_touchPrevY[i] = -1;
                    s_touchPrevState[i] = State_Off;
                    s_touchType[i] = Type_AnyTouch;
                    s_touchTimestamp[i] = -1;
                    s_touchHasNewEvent[i] = false;
                }
            }
            if (resetTilt) { s_currentTiltX = 0; s_currentTiltY = 0; s_currentTiltZ = 0; }
        }

        public static void ResetTouches() { InitTouchPad(true, false); }

        public static int GetTouchType(int i) { return s_touchType[i]; }
        public static int GetTouchTimestamp(int i) { return s_touchTimestamp[i]; }
        public static int GetTouchCurrentState(int i) { return s_touchCurrentState[i]; }
        public static void GetTouchStartCoordinates(int i, out int x, out int y) { x = s_touchStartX[i]; y = s_touchStartY[i]; }
        public static void GetTouchCurrentCoordinates(int i, out int x, out int y) { x = s_touchCurrentX[i]; y = s_touchCurrentY[i]; }
        public static void GetTouchPrevCoordinates(int i, out int x, out int y) { x = s_touchPrevX[i]; y = s_touchPrevY[i]; }
        public static bool GetNewEventFlag(int i) { return s_touchHasNewEvent[i]; }
        public static void ClearNewEventFlag(int i) { s_touchHasNewEvent[i] = false; }

        public static void SetPrevState(int i)
        {
            s_touchPrevX[i] = s_touchCurrentX[i];
            s_touchPrevY[i] = s_touchCurrentY[i];
            s_touchPrevState[i] = s_touchCurrentState[i];
        }

        public static void EventTouchDown(int i, int x, int y)
        {
            s_touchStartX[i] = x; s_touchStartY[i] = y;
            s_touchCurrentX[i] = x; s_touchCurrentY[i] = y;
            s_touchCurrentState[i] = State_On;
            s_touchType[i] = Type_AnyTouch;
            s_touchTimestamp[i] = s_currentTime;
            s_touchPrevX[i] = x; s_touchPrevY[i] = y;
            s_touchPrevState[i] = State_Off;
            s_touchHasNewEvent[i] = true;
            CTouchElement.UpdateElementsByTouch(i);
        }

        public static void EventTouchMove(int i, int x, int y)
        {
            s_touchCurrentX[i] = x;
            s_touchCurrentY[i] = y;
        }

        public static void EventTouchUp(int i, int x, int y)
        {
            if (x == -1) x = s_touchCurrentX[i];
            if (y == -1) y = s_touchCurrentY[i];
            s_touchCurrentX[i] = x; s_touchCurrentY[i] = y;
            s_touchCurrentState[i] = State_Off;
            int dx = Math.Abs(s_touchCurrentX[i] - s_touchStartX[i]);
            int dy = Math.Abs(s_touchCurrentY[i] - s_touchStartY[i]);
            if (s_currentTime - s_touchTimestamp[i] <= k_tap_time && dx < k_taps_max_displacement && dy < k_taps_max_displacement)
                s_touchType[i] = Type_SingleTap;
            s_touchHasNewEvent[i] = true;
            CTouchElement.UpdateElementsByTouch(i);
        }

        public static void UpdateCounters(int dtime)
        {
            s_currentTime += dtime;
            for (int i = 0; i < k_max_touches; i++)
            {
                if (s_touchType[i] == Type_AnyTouch)
                {
                    int dx = Math.Abs(s_touchCurrentX[i] - s_touchStartX[i]);
                    int dy = Math.Abs(s_touchCurrentY[i] - s_touchStartY[i]);
                    if (dx + dy >= k_min_drag_displacement || s_currentTime - s_touchTimestamp[i] > k_tap_time)
                        s_touchType[i] = Type_Drag;
                }
            }
        }
    }

    public class CTouchElement : IDisposable
    {
        public const int Type_AnyTouch = 0, Type_Button = 1, Type_SingleTap = 2, Type_Drag = 3, Type_DragAndZoom = 4, Type_Joystick = 5;
        const int k_joystick_return_speed = 3;

        public static readonly List<CTouchElement> s_elementArray = new List<CTouchElement>();
        static CTouchElement s_singleEnabledElement;
        static readonly CTouchElement[] s_elementByTouch = new CTouchElement[CTouchPad.k_max_touches];

        public bool m_isEnabled, m_isActivated, m_bJustActivated;
        public int m_justReleasedCounter;
        public int m_elementType;
        public int m_zLevel;
        public int m_screenX, m_screenY, m_screenW, m_screenH;
        public int m_requiredTouchType;
        public bool m_acceptTwoSimultaneousTouches, m_requireTwoSimultaneousTouches;
        public int m_assignedTouchIndex = -1, m_assignedTouchIndex2 = -1;
        public bool m_useFirstTouchForDefault = true;
        public bool m_tapDetectionEnabled;
        public int m_tapDetectionCounter, m_tapsFinalCount, m_lastDetectedTapTimestamp;
        public int m_lastTapX, m_lastTapY, m_lastTouchX, m_lastTouchY, m_lastTouchPrevX, m_lastTouchPrevY;
        public int m_joystickCenterX, m_joystickCenterY, m_joystickCurrentX, m_joystickCurrentY;
        public int m_joystickAngle, m_joystickAmplitude, m_joystickMinAmplitude, m_joystickMaxAmplitude;
        bool m_disposed;

        static void GetElementsParamsByType(int type, out int touchType, out bool acceptTwo, out bool requireTwo, out bool tap)
        {
            switch (type)
            {
                case Type_AnyTouch: touchType = CTouchPad.Type_AnyTouch; acceptTwo = true; requireTwo = false; tap = false; break;
                case Type_Button: touchType = CTouchPad.Type_AnyTouch; acceptTwo = false; requireTwo = false; tap = false; break;
                case Type_SingleTap: touchType = CTouchPad.Type_SingleTap; acceptTwo = false; requireTwo = false; tap = true; break;
                case Type_Drag: touchType = CTouchPad.Type_Drag; acceptTwo = false; requireTwo = false; tap = false; break;
                case Type_DragAndZoom: touchType = CTouchPad.Type_Drag; acceptTwo = true; requireTwo = false; tap = false; break;
                case Type_Joystick: touchType = CTouchPad.Type_AnyTouch; acceptTwo = false; requireTwo = false; tap = false; break;
                default: touchType = -1; acceptTwo = false; requireTwo = false; tap = false; break;
            }
        }

        public CTouchElement(int elementType = Type_AnyTouch, int zLevel = 50)
        {
            m_elementType = elementType;
            m_zLevel = zLevel;
            GetElementsParamsByType(m_elementType, out m_requiredTouchType, out m_acceptTwoSimultaneousTouches, out m_requireTwoSimultaneousTouches, out m_tapDetectionEnabled);
            m_isEnabled = true;
            ResetTapDetection();
            s_elementArray.Insert(0, this);
        }

        /// <summary>Equivalent of the C++ destructor: unregisters the element.</summary>
        public void Dispose()
        {
            if (m_disposed) return;
            ResetInput();
            DisableAllOtherElements(false);
            s_elementArray.Remove(this);
            m_disposed = true;
        }

        public void SetScreenCoordinates(int x, int y, int w, int h) { m_screenX = x; m_screenY = y; m_screenW = w; m_screenH = h; }
        public r2i GetArea() { return new r2i(m_screenX, m_screenY, m_screenX + m_screenW, m_screenY + m_screenH); }

        bool Inside(int x, int y) { return x >= m_screenX && x < m_screenX + m_screenW && y >= m_screenY && y < m_screenY + m_screenH; }

        public bool IsTouchInsideElement(bool second_touch = false)
        {
            int assigned = (second_touch || !m_useFirstTouchForDefault) ? m_assignedTouchIndex2 : m_assignedTouchIndex;
            if (assigned < 0) return false;
            int x, y;
            CTouchPad.GetTouchCurrentCoordinates(assigned, out x, out y);
            return Inside(x, y);
        }

        public void SetEnabled(bool is_enabled)
        {
            m_isEnabled = is_enabled;
            if (!m_isEnabled) ResetInput();
        }

        public bool IsEnabled() { return m_isEnabled; }

        public void DisableAllOtherElements(bool disable)
        {
            if (disable)
            {
                s_singleEnabledElement = this;
                for (int i = 0; i < s_elementArray.Count; i++)
                    if (s_elementArray[i] != this) s_elementArray[i].ResetInput();
            }
            else if (this == s_singleEnabledElement) s_singleEnabledElement = null;
        }

        public void ResetInput()
        {
            if (m_assignedTouchIndex != -1) s_elementByTouch[m_assignedTouchIndex] = null;
            if (m_assignedTouchIndex2 != -1) s_elementByTouch[m_assignedTouchIndex2] = null;
            m_assignedTouchIndex = -1;
            m_assignedTouchIndex2 = -1;
            m_isActivated = false;
            m_bJustActivated = false;
            m_justReleasedCounter = 0;
            ResetTapDetection();
            DisableAllOtherElements(false);
        }

        public void ReassignTouchTo(CTouchElement another, bool second_touch = false)
        {
            if (s_singleEnabledElement == this) s_singleEnabledElement = another;
            bool second = second_touch || !m_useFirstTouchForDefault;
            int assigned = second ? m_assignedTouchIndex2 : m_assignedTouchIndex;
            if (assigned == -1) return;
            s_elementByTouch[assigned] = another;
            bool wasActivated = another.m_isActivated;
            another.m_assignedTouchIndex = assigned;
            another.m_isActivated = another.CheckForActivation();
            another.m_bJustActivated = !wasActivated && another.m_isActivated;
            CTouchPad.SetPrevState(another.m_assignedTouchIndex);
            if (second) m_assignedTouchIndex2 = -1; else m_assignedTouchIndex = -1;
            ResetTapDetection();
        }

        public void SetTapDetectionEnabled(bool is_enabled)
        {
            if (m_tapDetectionEnabled != is_enabled) ResetTapDetection();
            m_tapDetectionEnabled = is_enabled;
        }

        public void ResetTapDetection() { m_tapDetectionCounter = 0; m_tapsFinalCount = 0; m_lastDetectedTapTimestamp = 0; }
        public int GetDetectedTaps(bool intermediate = false) { return intermediate ? m_tapDetectionCounter : m_tapsFinalCount; }
        public void GetTapPos(out int x, out int y) { x = m_lastTapX; y = m_lastTapY; }

        bool CheckForActivation(bool force_two_touches = false)
        {
            if (!m_isEnabled) return false;
            if (s_singleEnabledElement != null && this != s_singleEnabledElement) return false;
            if (m_assignedTouchIndex == -1) return false;
            if ((m_requireTwoSimultaneousTouches || force_two_touches) && m_assignedTouchIndex2 == -1) return false;
            if (m_requiredTouchType == CTouchPad.Type_AnyTouch) return true;
            bool ok = CTouchPad.GetTouchType(m_assignedTouchIndex) == m_requiredTouchType;
            if ((m_acceptTwoSimultaneousTouches || force_two_touches) && m_assignedTouchIndex2 != -1)
                if (CTouchPad.GetTouchType(m_assignedTouchIndex2) != m_requiredTouchType) ok = false;
            return ok;
        }

        bool CheckForFirstTouch()
        {
            if (m_assignedTouchIndex != -1) return false;
            for (int i = 0; i < CTouchPad.k_max_touches; i++)
            {
                if (CTouchPad.GetTouchCurrentState(i) == CTouchPad.State_On && CTouchPad.GetNewEventFlag(i))
                {
                    int x, y;
                    CTouchPad.GetTouchStartCoordinates(i, out x, out y);
                    if (!Inside(x, y)) continue;
                    m_assignedTouchIndex = i;
                    s_elementByTouch[i] = this;
                    CTouchPad.ClearNewEventFlag(i);
                    return true;
                }
            }
            return false;
        }

        bool CheckForSecondTouch()
        {
            if (m_assignedTouchIndex == -1 || m_assignedTouchIndex2 != -1) return false;
            for (int i = 0; i < CTouchPad.k_max_touches; i++)
            {
                if (i == m_assignedTouchIndex) continue;
                if (CTouchPad.GetTouchCurrentState(i) == CTouchPad.State_On && CTouchPad.GetNewEventFlag(i))
                {
                    int x, y;
                    CTouchPad.GetTouchStartCoordinates(i, out x, out y);
                    if (!Inside(x, y)) continue;
                    m_assignedTouchIndex2 = i;
                    s_elementByTouch[i] = this;
                    CTouchPad.ClearNewEventFlag(i);
                    return true;
                }
            }
            return false;
        }

        void Update(bool only_validate_assigned_touches = false)
        {
            if (!m_isEnabled) return;
            if (s_singleEnabledElement != null && this != s_singleEnabledElement) return;
            if (!only_validate_assigned_touches)
            {
                m_bJustActivated = false;
                if (m_justReleasedCounter > 0) m_justReleasedCounter--;
                if (m_assignedTouchIndex == -1) m_bJustActivated = CheckForFirstTouch();
                if (m_acceptTwoSimultaneousTouches && m_assignedTouchIndex2 == -1) CheckForSecondTouch();
                bool activated = CheckForActivation();
                m_bJustActivated = !m_isActivated && activated;
                m_isActivated = activated;
                if (m_elementType == Type_Joystick) UpdateJoystick();
            }
            bool touchIndexReset = false, tapDetected = false;
            int tap_x = 0, tap_y = 0;
            if (m_assignedTouchIndex2 != -1)
            {
                if (CTouchPad.GetTouchCurrentState(m_assignedTouchIndex2) == CTouchPad.State_Off && CTouchPad.GetNewEventFlag(m_assignedTouchIndex2))
                {
                    CTouchPad.ClearNewEventFlag(m_assignedTouchIndex2);
                    if (CTouchPad.GetTouchType(m_assignedTouchIndex2) == CTouchPad.Type_SingleTap) { GetTouchPos_Start(out tap_x, out tap_y, true); tapDetected = true; }
                    s_elementByTouch[m_assignedTouchIndex2] = null;
                    m_assignedTouchIndex2 = -1;
                    touchIndexReset = true;
                }
            }
            if (m_assignedTouchIndex != -1)
            {
                if (CTouchPad.GetTouchCurrentState(m_assignedTouchIndex) == CTouchPad.State_Off && CTouchPad.GetNewEventFlag(m_assignedTouchIndex))
                {
                    CTouchPad.ClearNewEventFlag(m_assignedTouchIndex);
                    if (CTouchPad.GetTouchType(m_assignedTouchIndex) == CTouchPad.Type_SingleTap) { GetTouchPos_Start(out tap_x, out tap_y, false); tapDetected = true; }
                    CTouchPad.GetTouchCurrentCoordinates(m_assignedTouchIndex, out m_lastTouchX, out m_lastTouchY);
                    CTouchPad.GetTouchPrevCoordinates(m_assignedTouchIndex, out m_lastTouchPrevX, out m_lastTouchPrevY);
                    s_elementByTouch[m_assignedTouchIndex] = null;
                    m_assignedTouchIndex = -1;
                    touchIndexReset = true;
                    m_justReleasedCounter = 2;
                }
            }
            if (m_tapDetectionEnabled)
            {
                bool tooDelayed = false, tooDisplaced = false;
                if (m_tapDetectionCounter > 0)
                {
                    tooDelayed = CTouchPad.s_currentTime - m_lastDetectedTapTimestamp > CTouchPad.k_tap_max_interval;
                    if (tapDetected) tooDisplaced = Math.Abs(tap_x - m_lastTapX) + Math.Abs(tap_y - m_lastTapY) > CTouchPad.k_taps_max_displacement;
                }
                if (tooDelayed || tooDisplaced)
                {
                    m_tapsFinalCount = m_tapDetectionCounter;
                    m_tapDetectionCounter = 0;
                }
                else if (tapDetected)
                {
                    m_tapDetectionCounter++;
                    m_lastDetectedTapTimestamp = CTouchPad.s_currentTime;
                    m_lastTapX = tap_x;
                    m_lastTapY = tap_y;
                }
            }
            if (touchIndexReset) m_isActivated = CheckForActivation();
        }

        public bool IsActive()
        {
            if (!m_isEnabled) return false;
            if (s_singleEnabledElement != null && this != s_singleEnabledElement) return false;
            switch (m_elementType)
            {
                case Type_AnyTouch:
                case Type_Button:
                case Type_Joystick:
                    return m_isActivated;
                case Type_Drag:
                case Type_DragAndZoom:
                    return m_isActivated && CTouchPad.GetTouchType(m_assignedTouchIndex) == CTouchPad.Type_Drag;
                case Type_SingleTap:
                    return GetDetectedTaps() == 1;
                default: return false;
            }
        }

        public bool IsNowActivated() { return m_bJustActivated; }
        public bool IsNowReleased() { return m_justReleasedCounter > 0; }

        public bool IsActiveWithTwoTouches(out int dx, out int dy)
        {
            dx = dy = 0;
            if (m_assignedTouchIndex == -1 || m_assignedTouchIndex2 == -1) return false;
            int x1, y1, x2, y2;
            CTouchPad.GetTouchCurrentCoordinates(m_assignedTouchIndex, out x1, out y1);
            CTouchPad.GetTouchCurrentCoordinates(m_assignedTouchIndex2, out x2, out y2);
            dx = x2 - x1; dy = y2 - y1;
            return true;
        }

        public static void UpdateElements()
        {
            if (s_elementArray.Count == 0) return;
            // std::stable_sort by z-level
            List<CTouchElement> sorted = new List<CTouchElement>(s_elementArray);
            int[] order = new int[sorted.Count];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (a, b) => { int c = sorted[a].m_zLevel.CompareTo(sorted[b].m_zLevel); return c != 0 ? c : a.CompareTo(b); });
            s_elementArray.Clear();
            for (int i = 0; i < order.Length; i++) s_elementArray.Add(sorted[order[i]]);
            for (int i = 0; i < s_elementArray.Count; i++)
                if (s_elementArray[i].IsEnabled()) s_elementArray[i].Update();
        }

        public static void UpdateElementsByTouch(int touchIndex)
        {
            CTouchElement e = s_elementByTouch[touchIndex];
            if (e != null) e.Update(true);
        }

        public static void ResetElementsInput()
        {
            s_singleEnabledElement = null;
            for (int i = 0; i < s_elementArray.Count; i++) s_elementArray[i].ResetInput();
            CTouchPad.ResetTouches();
        }

        int Idx(bool second_touch) { return (second_touch || !m_useFirstTouchForDefault) ? m_assignedTouchIndex2 : m_assignedTouchIndex; }

        public void GetTouchPos_Start(out int x, out int y, bool second_touch = false)
        {
            int ti = Idx(second_touch);
            x = 0; y = 0;
            if (ti == -1) return;
            CTouchPad.GetTouchStartCoordinates(ti, out x, out y);
        }

        public int GetCurrentTouchType(bool second_touch = false)
        {
            int ti = (second_touch && m_useFirstTouchForDefault) ? m_assignedTouchIndex2 : m_assignedTouchIndex;
            if (ti == -1) return CTouchPad.Type_AnyTouch;
            return CTouchPad.GetTouchType(ti);
        }

        public int GetCurrentTouchDuration(bool second_touch = false)
        {
            int ti = Idx(second_touch);
            if (ti == -1) return -1;
            return CTouchPad.s_currentTime - CTouchPad.GetTouchTimestamp(ti);
        }

        public void GetTouchPos_Current(out int x, out int y, bool second_touch = false)
        {
            int ti = Idx(second_touch);
            if (ti == -1)
            {
                x = 0; y = 0;
                if (second_touch || !m_useFirstTouchForDefault) return;
                x = m_lastTouchX; y = m_lastTouchY;
            }
            else CTouchPad.GetTouchCurrentCoordinates(ti, out x, out y);
        }

        public void GetTouchPos_Prev(out int x, out int y, bool second_touch = false)
        {
            int ti = Idx(second_touch);
            if (ti == -1)
            {
                x = 0; y = 0;
                if (second_touch || !m_useFirstTouchForDefault) return;
                x = m_lastTouchPrevX; y = m_lastTouchPrevY;
            }
            else CTouchPad.GetTouchPrevCoordinates(ti, out x, out y);
        }

        public void GetTouchPos_DTotal(out int x, out int y, bool second_touch = false)
        {
            int ti = Idx(second_touch);
            x = 0; y = 0;
            if (ti == -1) return;
            int cx, cy, sx, sy;
            GetTouchPos_Start(out sx, out sy, second_touch);
            GetTouchPos_Current(out cx, out cy, second_touch);
            x = cx - sx; y = cy - sy;
        }

        public void GetTouchPos_DT(out int x, out int y, bool second_touch = false)
        {
            int ti = Idx(second_touch);
            if (ti == -1)
            {
                x = 0; y = 0;
                if (second_touch || !m_useFirstTouchForDefault) return;
                x = m_lastTouchX - m_lastTouchPrevX;
                y = m_lastTouchY - m_lastTouchPrevY;
                m_lastTouchPrevX = m_lastTouchX;
                m_lastTouchPrevY = m_lastTouchY;
            }
            else
            {
                int cx, cy, px, py;
                CTouchPad.GetTouchPrevCoordinates(ti, out px, out py);
                CTouchPad.GetTouchCurrentCoordinates(ti, out cx, out cy);
                x = cx - px; y = cy - py;
                CTouchPad.SetPrevState(ti);
            }
        }

        public void InitJoystick(int centerX = -1, int centerY = -1, int maxAmplitude = -1, int minAmplitude = -1)
        {
            if (centerX == -1 && centerY == -1) { centerX = m_screenX + m_screenW / 2; centerY = m_screenY + m_screenH / 2; }
            m_joystickCenterX = centerX; m_joystickCenterY = centerY;
            m_joystickCurrentX = centerX; m_joystickCurrentY = centerY;
            m_joystickAngle = 0; m_joystickAmplitude = 0;
            m_joystickMinAmplitude = minAmplitude == -1 ? 4 : minAmplitude;
            m_joystickMaxAmplitude = maxAmplitude == -1 ? m_screenW / 2 : maxAmplitude;
        }

        void UpdateJoystick()
        {
            if (IsActive())
            {
                GetTouchPos_Current(out m_joystickCurrentX, out m_joystickCurrentY);
                int dx = m_joystickCurrentX - m_joystickCenterX, dy = m_joystickCurrentY - m_joystickCenterY;
                double ang = Math.Atan2(dy, dx);
                m_joystickAngle = (int)(ang * 2048 / Math.PI);
                m_joystickAmplitude = (int)Math.Sqrt(dx * dx + dy * dy);
                if (m_joystickAmplitude < m_joystickMinAmplitude)
                {
                    m_joystickCurrentX = m_joystickCenterX; m_joystickCurrentY = m_joystickCenterY;
                    m_joystickAngle = 0; m_joystickAmplitude = 0;
                }
                else if (m_joystickAmplitude > m_joystickMaxAmplitude)
                {
                    m_joystickAmplitude = m_joystickMaxAmplitude;
                    m_joystickCurrentX = m_joystickCenterX + (int)(Math.Cos(ang) * m_joystickAmplitude);
                    m_joystickCurrentY = m_joystickCenterY + (int)(Math.Sin(ang) * m_joystickAmplitude);
                }
            }
            else
            {
                m_joystickCurrentX -= (m_joystickCurrentX - m_joystickCenterX) / k_joystick_return_speed;
                m_joystickCurrentY -= (m_joystickCurrentY - m_joystickCenterY) / k_joystick_return_speed;
                if (Math.Abs(m_joystickCurrentX - m_joystickCenterX) < k_joystick_return_speed && Math.Abs(m_joystickCurrentY - m_joystickCenterY) < k_joystick_return_speed)
                {
                    m_joystickCurrentX = m_joystickCenterX; m_joystickCurrentY = m_joystickCenterY;
                }
                m_joystickAngle = 0; m_joystickAmplitude = 0;
            }
        }

        public bool IsPointInside(p2i pt)
        {
            return new r2i(new p2i(m_screenX, m_screenY), new d2i(m_screenW, m_screenH)).isPointInside(pt);
        }
    }
}
