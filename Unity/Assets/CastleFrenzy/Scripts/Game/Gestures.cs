// Common/Gestures: spell gesture recognition (circle = boulder, zig-zag = lightning, "V" = tornado, dash = ghost army).
using System.Collections.Generic;
using static CastleFrenzy.G;
using static CastleFrenzy.GE;

namespace CastleFrenzy
{
    public class CGesture
    {
        static CGesture s_instance;
        public static CGesture GetInstance() { return s_instance ?? (s_instance = new CGesture()); }

        public struct SKey
        {
            public p2i Point;
            public float Time;
        }

        float m_time;
        r2i m_bbox;
        readonly List<SKey> m_input = new List<SKey>(), m_actualInput = new List<SKey>();
        List<v2f> m_output = new List<v2f>(), m_output2 = new List<v2f>();
        readonly List<int> m_dir = new List<int>(), m_angle = new List<int>(), m_len2 = new List<int>(), m_dir2 = new List<int>(), m_angle2 = new List<int>();
        int m_lastGesture = ST_None;

        public List<SKey> GetInput() { return m_input; }

        public void Reset()
        {
            m_time = 0.0f;
            m_input.Clear();
            m_actualInput.Clear();
        }

        public void Update(p2i point, float time)
        {
            if (m_input.Count == 0)
            {
                m_time = 0.0f;
                SKey key = new SKey { Point = point, Time = m_time };
                m_input.Add(key);
                m_actualInput.Add(key);
                m_bbox = new r2i(key.Point, key.Point);
            }
            else
            {
                p2i last = m_input[m_input.Count - 1].Point;
                v2f v1 = new v2f(last.X, last.Y), v2 = new v2f(point.X, point.Y);
                const int spacing = 20;
                int len = (int)(v2 - v1).getLength();
                int count = len > 0 ? len / spacing : 0;
                for (int i = 0; i < count; i++)
                {
                    float k = (1.0f + i) / count;
                    v2f v = v1 + (v2 - v1).normalize() * ((i + 1) * spacing);
                    SKey key = new SKey { Point = new p2i((int)v.X, (int)v.Y), Time = m_time + time * k };
                    m_input.Add(key);
                    m_bbox.addInternalPoint(key.Point);
                }
                if (count > 0) m_actualInput.Add(new SKey { Point = point, Time = m_time + time });
            }
            m_time += time;
        }

        bool MatchCircle()
        {
            const int MIN_POINTS = 5;
            if (m_input.Count < MIN_POINTS) return false;
            const float MAX_DURATION = 3.0f;
            if (m_input[m_input.Count - 1].Time > MAX_DURATION) return false;
            const int MIN_SIZE = 40;
            if (m_bbox.getHeight() < MIN_SIZE || m_bbox.getWidth() < MIN_SIZE) return false;
            const float MIN_ASPECT = 0.333f;
            float aspect = (float)m_bbox.getWidth() / m_bbox.getHeight();
            if (aspect > 1.0f) aspect = 1.0f / aspect;
            if (aspect < MIN_ASPECT) return false;
            int[,] grid = new int[3, 3];
            foreach (SKey k in m_input)
            {
                int x = IntRemap3(k.Point.X, m_bbox.UpperLeftCorner.X, m_bbox.LowerRightCorner.X, 0, 2);
                int y = IntRemap3(k.Point.Y, m_bbox.UpperLeftCorner.Y, m_bbox.LowerRightCorner.Y, 0, 2);
                grid[x, y] = 1;
            }
            int center = grid[1, 1];
            int edges = grid[1, 0] + grid[0, 1] + grid[2, 1] + grid[1, 2];
            int corners = grid[0, 0] + grid[0, 2] + grid[2, 2] + grid[2, 0];
            return center == 0 && edges == 4 && corners >= 3;
        }

        void calculateOutput()
        {
            m_output.Clear();
            foreach (SKey k in m_input) m_output.Add(new v2f(k.Point.X, k.Point.Y));
            m_dir.Clear();
            for (int i = 1; i < m_output.Count; i++)
            {
                v2f v = m_output[i] - m_output[i - 1];
                m_dir.Add((int)(atan2f(-v.Y, v.X) * RADTODEG));
            }
            m_angle.Clear();
            for (int i = 1; i < m_dir.Count; i++) m_angle.Add(WrapAngle(m_dir[i] - m_dir[i - 1]));
        }

        static int WrapAngle(int angle)
        {
            if (angle > 180) angle -= 360;
            if (angle <= -180) angle += 360;
            return angle;
        }

        static void Erase(List<v2f> l, int p, int n)
        {
            if (p >= l.Count) return;
            l.RemoveRange(p, System.Math.Min(n, l.Count - p));
        }

        void calculateOutput2()
        {
            int len = 0, sum = 0, mid_a = 0, mid_i = -1;
            m_output2 = new List<v2f>(m_output);
            for (int i = m_angle.Count - 1; i >= 0; i--)
            {
                int a = m_angle[i];
                len += 1;
                sum += a;
                if (System.Math.Abs(a) > mid_a) { mid_a = System.Math.Abs(a); mid_i = i; }
                const int treshold = 70;
                if (System.Math.Abs(sum) >= treshold && System.Math.Abs(a) < treshold)
                {
                    int offset = mid_i - i;
                    i += offset;
                    len -= offset;
                    a = treshold;
                }
                if (System.Math.Abs(a) >= treshold)
                {
                    if (len >= 2) Erase(m_output2, i + 2, len - 1);
                    len = 0; sum = 0; mid_a = 0; mid_i = -1;
                }
            }
            if (len >= 1) Erase(m_output2, 1, len);
            m_len2.Clear();
            m_dir2.Clear();
            for (int i = 1; i < m_output2.Count; i++)
            {
                v2f v = m_output2[i] - m_output2[i - 1];
                m_len2.Add((int)v.getLength());
                m_dir2.Add((int)(atan2f(-v.Y, v.X) * RADTODEG));
            }
            m_angle2.Clear();
            for (int i = 1; i < m_dir2.Count; i++) m_angle2.Add(WrapAngle(m_dir2[i] - m_dir2[i - 1]));
        }

        static bool angleNear(int angle1, int angle2, int treshold) { return System.Math.Abs(WrapAngle(angle1 - angle2)) <= treshold; }

        public int MatchGesture()
        {
            calculateOutput();
            calculateOutput2();
            m_lastGesture = ST_None;
            if (MatchCircle()) m_lastGesture = ST_Boulder;
            if (MatchTornado()) m_lastGesture = ST_Tornado;
            if (MatchLightning()) m_lastGesture = ST_Lightning;
            if (MatchGhostArmy()) m_lastGesture = ST_GhostArmy;
            return m_lastGesture;
        }

        bool MatchTornado()
        {
            if (m_dir2.Count != 2) return false;
            if (!angleNear(m_dir2[0], -70, 25)) return false;
            if (!angleNear(m_angle2[0], 180 - 60, 40)) return false;
            return m_len2[0] >= 30 && m_len2[1] >= 30;
        }

        bool MatchLightning()
        {
            if (m_angle2.Count < 2) return false;
            for (int i = 0; i < m_angle2.Count; i++)
            {
                int dir = (i % 2) == 0 ? m_angle2[0] : -m_angle2[0];
                dir = dir > 0 ? 1 : -1;
                if (!angleNear(m_angle2[i], dir * 120, 50)) return false;
                if (i < 3 && m_len2[i] < 30) return false;
            }
            return true;
        }

        bool MatchGhostArmy()
        {
            if (m_dir2.Count == 1 && angleNear(m_dir2[0], -20, 20) && m_len2[0] > 200) return true;
            if (m_dir2.Count == 2 && angleNear(m_dir2[0], -70, 30) && m_len2[0] < 150 && angleNear(m_dir2[1], -15, 20) && m_len2[1] > 150) return true;
            return false;
        }
    }
}
