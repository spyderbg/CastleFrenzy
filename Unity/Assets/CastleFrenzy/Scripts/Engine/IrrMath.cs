// Irrlicht-compatible math types used by the ported game code.
// Semantics follow Lib/Irrlicht/include with _IRR_USE_RIGHT_HAND_CONVENTION_ defined,
// so the game logic produces exactly the numbers of the original C++ build.
using System;
using UnityEngine;

namespace CastleFrenzy
{
    public static class IrrMath
    {
        public const float PI = 3.14159265359f;
        public const float DEGTORAD = PI / 180.0f;
        public const float RADTODEG = 180.0f / PI;
        public const double DEGTORAD64 = Math.PI / 180.0;
        public const double RADTODEG64 = 180.0 / Math.PI;
        public const float ROUNDING_ERROR_32 = 0.000001f;
        public const double ROUNDING_ERROR_64 = 0.00000001;

        public static bool equals(float a, float b, float tolerance = ROUNDING_ERROR_32) { return (a + tolerance >= b) && (a - tolerance <= b); }
        public static bool iszero(float a) { return Math.Abs(a) <= ROUNDING_ERROR_32; }
        public static float clamp(float v, float lo, float hi) { return v < lo ? lo : (v > hi ? hi : v); }
        public static int clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }
        public static float lerp(float a, float b, float t) { return a * (1.0f - t) + b * t; }
        public static float reciprocal_squareroot(float x) { return 1.0f / (float)Math.Sqrt(x); }
    }

    [Serializable]
    public struct v3f
    {
        public float X, Y, Z;

        public v3f(float x, float y, float z) { X = x; Y = y; Z = z; }
        public v3f(float n) { X = n; Y = n; Z = n; }

        public static readonly v3f Zero = new v3f(0, 0, 0);

        public float this[int i]
        {
            get { return i == 0 ? X : (i == 1 ? Y : Z); }
            set { if (i == 0) X = value; else if (i == 1) Y = value; else Z = value; }
        }

        public static v3f operator -(v3f a) { return new v3f(-a.X, -a.Y, -a.Z); }
        public static v3f operator +(v3f a, v3f b) { return new v3f(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }
        public static v3f operator -(v3f a, v3f b) { return new v3f(a.X - b.X, a.Y - b.Y, a.Z - b.Z); }
        public static v3f operator *(v3f a, v3f b) { return new v3f(a.X * b.X, a.Y * b.Y, a.Z * b.Z); }
        public static v3f operator /(v3f a, v3f b) { return new v3f(a.X / b.X, a.Y / b.Y, a.Z / b.Z); }
        public static v3f operator +(v3f a, float v) { return new v3f(a.X + v, a.Y + v, a.Z + v); }
        public static v3f operator -(v3f a, float v) { return new v3f(a.X - v, a.Y - v, a.Z - v); }
        public static v3f operator *(v3f a, float v) { return new v3f(a.X * v, a.Y * v, a.Z * v); }
        public static v3f operator *(float v, v3f a) { return new v3f(a.X * v, a.Y * v, a.Z * v); }
        public static v3f operator /(v3f a, float v) { float i = 1.0f / v; return new v3f(a.X * i, a.Y * i, a.Z * i); }
        public static bool operator ==(v3f a, v3f b) { return a.equals(b); }
        public static bool operator !=(v3f a, v3f b) { return !a.equals(b); }
        public override bool Equals(object o) { return o is v3f && equals((v3f)o); }
        public override int GetHashCode() { return X.GetHashCode() ^ (Y.GetHashCode() << 2) ^ (Z.GetHashCode() >> 2); }
        public override string ToString() { return string.Format("({0}, {1}, {2})", X, Y, Z); }

        public static implicit operator Vector3(v3f v) { return new Vector3(v.X, v.Y, v.Z); }
        public static implicit operator v3f(Vector3 v) { return new v3f(v.x, v.y, v.z); }

        public bool equals(v3f o, float tolerance = IrrMath.ROUNDING_ERROR_32)
        {
            return IrrMath.equals(X, o.X, tolerance) && IrrMath.equals(Y, o.Y, tolerance) && IrrMath.equals(Z, o.Z, tolerance);
        }

        public v3f set(float x, float y, float z) { X = x; Y = y; Z = z; return this; }
        public v3f set(v3f p) { X = p.X; Y = p.Y; Z = p.Z; return this; }
        public float getLength() { return (float)Math.Sqrt((double)(X * X + Y * Y + Z * Z)); }
        public float getLengthSQ() { return X * X + Y * Y + Z * Z; }
        public float dotProduct(v3f o) { return X * o.X + Y * o.Y + Z * o.Z; }
        public float getDistanceFrom(v3f o) { return (this - o).getLength(); }
        public float getDistanceFromSQ(v3f o) { return (this - o).getLengthSQ(); }

        /// <summary>Right-hand convention cross product (negated standard cross, as in the engine).</summary>
        public v3f crossProduct(v3f p)
        {
            return new v3f(-Y * p.Z + Z * p.Y, -Z * p.X + X * p.Z, -X * p.Y + Y * p.X);
        }

        public bool isBetweenPoints(v3f begin, v3f end)
        {
            float f = (end - begin).getLengthSQ();
            return getDistanceFromSQ(begin) <= f && getDistanceFromSQ(end) <= f;
        }

        /// <summary>Normalizes in place and returns the result (like the C++ reference return).</summary>
        public v3f normalize()
        {
            float l = X * X + Y * Y + Z * Z;
            if (l == 0) return this;
            l = IrrMath.reciprocal_squareroot(l);
            X *= l; Y *= l; Z *= l;
            return this;
        }

        public v3f normalized() { v3f c = this; c.normalize(); return c; }

        public v3f setLength(float newlength)
        {
            normalize();
            X *= newlength; Y *= newlength; Z *= newlength;
            return this;
        }

        public v3f invert() { X = -X; Y = -Y; Z = -Z; return this; }

        public void rotateXZBy(double degrees, v3f center = default(v3f))
        {
            degrees *= IrrMath.DEGTORAD64;
            float cs = (float)Math.Cos(degrees), sn = (float)Math.Sin(degrees);
            X -= center.X; Z -= center.Z;
            set(X * cs - Z * sn, Y, X * sn + Z * cs);
            X += center.X; Z += center.Z;
        }

        public void rotateXYBy(double degrees, v3f center = default(v3f))
        {
            degrees *= IrrMath.DEGTORAD64;
            float cs = (float)Math.Cos(degrees), sn = (float)Math.Sin(degrees);
            X -= center.X; Y -= center.Y;
            set(X * cs - Y * sn, X * sn + Y * cs, Z);
            X += center.X; Y += center.Y;
        }

        public void rotateYZBy(double degrees, v3f center = default(v3f))
        {
            degrees *= IrrMath.DEGTORAD64;
            float cs = (float)Math.Cos(degrees), sn = (float)Math.Sin(degrees);
            Z -= center.Z; Y -= center.Y;
            set(X, Y * cs - Z * sn, Y * sn + Z * cs);
            Z += center.Z; Y += center.Y;
        }

        /// <summary>Returns other*(1-d) + this*d, exactly like vector3d::getInterpolated.</summary>
        public v3f getInterpolated(v3f other, float d)
        {
            float inv = 1.0f - d;
            return new v3f(other.X * inv + X * d, other.Y * inv + Y * d, other.Z * inv + Z * d);
        }

        public v3f getInterpolated_quadratic(v3f v2, v3f v3, float d)
        {
            float inv = 1.0f - d, mul0 = inv * inv, mul1 = 2.0f * d * inv, mul2 = d * d;
            return new v3f(X * mul0 + v2.X * mul1 + v3.X * mul2, Y * mul0 + v2.Y * mul1 + v3.Y * mul2, Z * mul0 + v2.Z * mul1 + v3.Z * mul2);
        }

        public v3f getHorizontalAngle()
        {
            v3f angle = new v3f();
            angle.Y = (float)(Math.Atan2(X, Z) * IrrMath.RADTODEG64);
            if (angle.Y < 0.0f) angle.Y += 360.0f;
            if (angle.Y >= 360.0f) angle.Y -= 360.0f;
            double z1 = Math.Sqrt(X * X + Z * Z);
            angle.X = (float)(Math.Atan2(z1, Y) * IrrMath.RADTODEG64 - 90.0);
            if (angle.X < 0.0f) angle.X += 360.0f;
            if (angle.X >= 360.0f) angle.X -= 360.0f;
            return angle;
        }

        public v3f rotationToDirection(v3f forwards)
        {
            double cr = Math.Cos(IrrMath.DEGTORAD64 * X), sr = Math.Sin(IrrMath.DEGTORAD64 * X);
            double cp = Math.Cos(IrrMath.DEGTORAD64 * Y), sp = Math.Sin(IrrMath.DEGTORAD64 * Y);
            double cy = Math.Cos(IrrMath.DEGTORAD64 * Z), sy = Math.Sin(IrrMath.DEGTORAD64 * Z);
            double srsp = sr * sp, crsp = cr * sp;
            double m0 = cp * cy, m1 = cp * sy, m2 = -sp;
            double m3 = srsp * cy - cr * sy, m4 = srsp * sy + cr * cy, m5 = sr * cp;
            double m6 = crsp * cy + sr * sy, m7 = crsp * sy - sr * cy, m8 = cr * cp;
            return new v3f((float)(forwards.X * m0 + forwards.Y * m3 + forwards.Z * m6),
                           (float)(forwards.X * m1 + forwards.Y * m4 + forwards.Z * m7),
                           (float)(forwards.X * m2 + forwards.Y * m5 + forwards.Z * m8));
        }
    }

    [Serializable]
    public struct v2f
    {
        public float X, Y;
        public v2f(float x, float y) { X = x; Y = y; }
        public static v2f operator -(v2f a) { return new v2f(-a.X, -a.Y); }
        public static v2f operator +(v2f a, v2f b) { return new v2f(a.X + b.X, a.Y + b.Y); }
        public static v2f operator -(v2f a, v2f b) { return new v2f(a.X - b.X, a.Y - b.Y); }
        public static v2f operator *(v2f a, v2f b) { return new v2f(a.X * b.X, a.Y * b.Y); }
        public static v2f operator *(v2f a, float v) { return new v2f(a.X * v, a.Y * v); }
        public static v2f operator *(float v, v2f a) { return new v2f(a.X * v, a.Y * v); }
        public static v2f operator /(v2f a, float v) { return new v2f(a.X / v, a.Y / v); }
        public static bool operator ==(v2f a, v2f b) { return IrrMath.equals(a.X, b.X) && IrrMath.equals(a.Y, b.Y); }
        public static bool operator !=(v2f a, v2f b) { return !(a == b); }
        public override bool Equals(object o) { return o is v2f && this == (v2f)o; }
        public override int GetHashCode() { return X.GetHashCode() ^ (Y.GetHashCode() << 2); }
        public override string ToString() { return string.Format("({0}, {1})", X, Y); }
        public v2f set(float x, float y) { X = x; Y = y; return this; }
        public float getLength() { return (float)Math.Sqrt(X * X + Y * Y); }
        public float getLengthSQ() { return X * X + Y * Y; }
        public float dotProduct(v2f o) { return X * o.X + Y * o.Y; }
        public float getDistanceFrom(v2f o) { return (this - o).getLength(); }
        public float getDistanceFromSQ(v2f o) { return (this - o).getLengthSQ(); }
        public v2f normalize()
        {
            float l = X * X + Y * Y;
            if (l == 0) return this;
            l = IrrMath.reciprocal_squareroot(l);
            X *= l; Y *= l;
            return this;
        }
        public v2f getInterpolated(v2f other, float d)
        {
            float inv = 1.0f - d;
            return new v2f(other.X * inv + X * d, other.Y * inv + Y * d);
        }
        public double getAngle()
        {
            if (Y == 0) return X < 0 ? 180 : 0;
            else if (X == 0) return Y < 0 ? 90 : 270;
            double tmp = Y / Math.Sqrt(X * X + Y * Y);
            tmp = Math.Atan(Math.Sqrt(1 - tmp * tmp) / tmp) * IrrMath.RADTODEG64;
            if (X > 0 && Y > 0) return tmp + 270;
            else if (X > 0 && Y < 0) return tmp + 90;
            else if (X < 0 && Y < 0) return 90 - tmp;
            else if (X < 0 && Y > 0) return 270 - tmp;
            return tmp;
        }
        public double getAngleTrig()
        {
            if (Y == 0) return X < 0 ? 180 : 0;
            else if (X == 0) return Y < 0 ? 270 : 90;
            if (Y > 0)
            {
                if (X > 0) return Math.Atan(Y / X) * IrrMath.RADTODEG64;
                else return 180.0 - Math.Atan(Y / -X) * IrrMath.RADTODEG64;
            }
            else
            {
                if (X > 0) return 360.0 - Math.Atan(-Y / X) * IrrMath.RADTODEG64;
                else return 180.0 + Math.Atan(-Y / -X) * IrrMath.RADTODEG64;
            }
        }
        public void rotateBy(double degrees, v2f center = default(v2f))
        {
            degrees *= IrrMath.DEGTORAD64;
            float cs = (float)Math.Cos(degrees), sn = (float)Math.Sin(degrees);
            X -= center.X; Y -= center.Y;
            set(X * cs - Y * sn, X * sn + Y * cs);
            X += center.X; Y += center.Y;
        }
    }

    [Serializable]
    public struct p2i
    {
        public int X, Y;
        public p2i(int x, int y) { X = x; Y = y; }
        public static p2i operator +(p2i a, p2i b) { return new p2i(a.X + b.X, a.Y + b.Y); }
        public static p2i operator -(p2i a, p2i b) { return new p2i(a.X - b.X, a.Y - b.Y); }
        public static p2i operator +(p2i a, d2i b) { return new p2i(a.X + b.Width, a.Y + b.Height); }
        public static p2i operator -(p2i a, d2i b) { return new p2i(a.X - b.Width, a.Y - b.Height); }
        public static p2i operator -(p2i a) { return new p2i(-a.X, -a.Y); }
        public static bool operator ==(p2i a, p2i b) { return a.X == b.X && a.Y == b.Y; }
        public static bool operator !=(p2i a, p2i b) { return !(a == b); }
        public override bool Equals(object o) { return o is p2i && this == (p2i)o; }
        public override int GetHashCode() { return X ^ (Y << 16); }
        public override string ToString() { return string.Format("({0}, {1})", X, Y); }
        public float getDistanceFrom(p2i o) { float dx = X - o.X, dy = Y - o.Y; return (float)Math.Sqrt(dx * dx + dy * dy); }
    }

    [Serializable]
    public struct d2i
    {
        public int Width, Height;
        public d2i(int w, int h) { Width = w; Height = h; }
        public static d2i operator /(d2i a, int s) { return new d2i(a.Width / s, a.Height / s); }
        public static d2i operator *(d2i a, int s) { return new d2i(a.Width * s, a.Height * s); }
        public static d2i operator +(d2i a, d2i b) { return new d2i(a.Width + b.Width, a.Height + b.Height); }
        public static bool operator ==(d2i a, d2i b) { return a.Width == b.Width && a.Height == b.Height; }
        public static bool operator !=(d2i a, d2i b) { return !(a == b); }
        public override bool Equals(object o) { return o is d2i && this == (d2i)o; }
        public override int GetHashCode() { return Width ^ (Height << 16); }
        public static implicit operator p2i(d2i d) { return new p2i(d.Width, d.Height); }
    }

    [Serializable]
    public struct d2f
    {
        public float Width, Height;
        public d2f(float w, float h) { Width = w; Height = h; }
        public void set(float w, float h) { Width = w; Height = h; }
    }

    [Serializable]
    public struct r2i
    {
        public p2i UpperLeftCorner, LowerRightCorner;
        public r2i(int x, int y, int x2, int y2) { UpperLeftCorner = new p2i(x, y); LowerRightCorner = new p2i(x2, y2); }
        public r2i(p2i ul, p2i lr) { UpperLeftCorner = ul; LowerRightCorner = lr; }
        public r2i(p2i pos, d2i size) { UpperLeftCorner = pos; LowerRightCorner = new p2i(pos.X + size.Width, pos.Y + size.Height); }
        public static r2i operator +(r2i r, p2i p) { return new r2i(r.UpperLeftCorner + p, r.LowerRightCorner + p); }
        public static r2i operator -(r2i r, p2i p) { return new r2i(r.UpperLeftCorner - p, r.LowerRightCorner - p); }
        public static bool operator ==(r2i a, r2i b) { return a.UpperLeftCorner == b.UpperLeftCorner && a.LowerRightCorner == b.LowerRightCorner; }
        public static bool operator !=(r2i a, r2i b) { return !(a == b); }
        public override bool Equals(object o) { return o is r2i && this == (r2i)o; }
        public override int GetHashCode() { return UpperLeftCorner.GetHashCode() ^ (LowerRightCorner.GetHashCode() << 1); }
        public override string ToString() { return string.Format("[{0},{1} - {2},{3}]", UpperLeftCorner.X, UpperLeftCorner.Y, LowerRightCorner.X, LowerRightCorner.Y); }
        public int getWidth() { return LowerRightCorner.X - UpperLeftCorner.X; }
        public int getHeight() { return LowerRightCorner.Y - UpperLeftCorner.Y; }
        public int getArea() { return getWidth() * getHeight(); }
        public p2i getCenter() { return new p2i((UpperLeftCorner.X + LowerRightCorner.X) / 2, (UpperLeftCorner.Y + LowerRightCorner.Y) / 2); }
        public d2i getSize() { return new d2i(getWidth(), getHeight()); }
        public bool isPointInside(p2i pos)
        {
            return UpperLeftCorner.X <= pos.X && UpperLeftCorner.Y <= pos.Y && LowerRightCorner.X >= pos.X && LowerRightCorner.Y >= pos.Y;
        }
        public bool isRectCollided(r2i other)
        {
            return LowerRightCorner.Y > other.UpperLeftCorner.Y && UpperLeftCorner.Y < other.LowerRightCorner.Y &&
                   LowerRightCorner.X > other.UpperLeftCorner.X && UpperLeftCorner.X < other.LowerRightCorner.X;
        }
        public void clipAgainst(r2i other)
        {
            if (other.LowerRightCorner.X < LowerRightCorner.X) LowerRightCorner.X = other.LowerRightCorner.X;
            if (other.LowerRightCorner.Y < LowerRightCorner.Y) LowerRightCorner.Y = other.LowerRightCorner.Y;
            if (other.UpperLeftCorner.X > UpperLeftCorner.X) UpperLeftCorner.X = other.UpperLeftCorner.X;
            if (other.UpperLeftCorner.Y > UpperLeftCorner.Y) UpperLeftCorner.Y = other.UpperLeftCorner.Y;
            if (UpperLeftCorner.Y > LowerRightCorner.Y) UpperLeftCorner.Y = LowerRightCorner.Y;
            if (UpperLeftCorner.X > LowerRightCorner.X) UpperLeftCorner.X = LowerRightCorner.X;
        }
        public void repair()
        {
            if (LowerRightCorner.X < UpperLeftCorner.X) { int t = LowerRightCorner.X; LowerRightCorner.X = UpperLeftCorner.X; UpperLeftCorner.X = t; }
            if (LowerRightCorner.Y < UpperLeftCorner.Y) { int t = LowerRightCorner.Y; LowerRightCorner.Y = UpperLeftCorner.Y; UpperLeftCorner.Y = t; }
        }
        public bool isValid() { return LowerRightCorner.X >= UpperLeftCorner.X && LowerRightCorner.Y >= UpperLeftCorner.Y; }
        public void addInternalPoint(p2i p) { addInternalPoint(p.X, p.Y); }
        public void addInternalPoint(int x, int y)
        {
            if (x > LowerRightCorner.X) LowerRightCorner.X = x;
            if (y > LowerRightCorner.Y) LowerRightCorner.Y = y;
            if (x < UpperLeftCorner.X) UpperLeftCorner.X = x;
            if (y < UpperLeftCorner.Y) UpperLeftCorner.Y = y;
        }
    }

    [Serializable]
    public struct r2f
    {
        public v2f UpperLeftCorner, LowerRightCorner;
        public r2f(float x, float y, float x2, float y2) { UpperLeftCorner = new v2f(x, y); LowerRightCorner = new v2f(x2, y2); }
        public float getWidth() { return LowerRightCorner.X - UpperLeftCorner.X; }
        public float getHeight() { return LowerRightCorner.Y - UpperLeftCorner.Y; }
        public bool isPointInside(v2f p) { return UpperLeftCorner.X <= p.X && UpperLeftCorner.Y <= p.Y && LowerRightCorner.X >= p.X && LowerRightCorner.Y >= p.Y; }
    }

    [Serializable]
    public struct quaternion
    {
        public float X, Y, Z, W;

        public quaternion(float x, float y, float z, float w) { X = x; Y = y; Z = z; W = w; }
        /// <summary>Euler angles in radians.</summary>
        public quaternion(float x, float y, float z) { X = Y = Z = 0; W = 1; set(x, y, z); }
        public quaternion(v3f euler) { X = Y = Z = 0; W = 1; set(euler.X, euler.Y, euler.Z); }
        public quaternion(matrix4 m) { X = Y = Z = 0; W = 1; fromMatrix(m); }

        public static quaternion Identity { get { return new quaternion(0, 0, 0, 1); } }

        public static bool operator ==(quaternion a, quaternion b) { return a.X == b.X && a.Y == b.Y && a.Z == b.Z && a.W == b.W; }
        public static bool operator !=(quaternion a, quaternion b) { return !(a == b); }
        public override bool Equals(object o) { return o is quaternion && this == (quaternion)o; }
        public override int GetHashCode() { return X.GetHashCode() ^ Y.GetHashCode() ^ Z.GetHashCode() ^ W.GetHashCode(); }

        /// <summary>Right-hand convention product (operands swapped versus the standard formula).</summary>
        public static quaternion operator *(quaternion a, quaternion other)
        {
            quaternion tmp;
            tmp.W = (other.W * a.W) - (other.X * a.X) - (other.Y * a.Y) - (other.Z * a.Z);
            tmp.X = (other.W * a.X) + (other.X * a.W) + (other.Y * a.Z) - (other.Z * a.Y);
            tmp.Y = (other.W * a.Y) + (other.Y * a.W) + (other.Z * a.X) - (other.X * a.Z);
            tmp.Z = (other.W * a.Z) + (other.Z * a.W) + (other.X * a.Y) - (other.Y * a.X);
            return tmp;
        }
        public static quaternion operator *(quaternion a, float s) { return new quaternion(s * a.X, s * a.Y, s * a.Z, s * a.W); }
        public static quaternion operator +(quaternion a, quaternion b) { return new quaternion(a.X + b.X, a.Y + b.Y, a.Z + b.Z, a.W + b.W); }

        public static v3f operator *(quaternion q, v3f v)
        {
            v3f qvec = new v3f(q.X, q.Y, q.Z);
            v3f uv = qvec.crossProduct(v);
            v3f uuv = qvec.crossProduct(uv);
            uv *= (2.0f * q.W);
            uuv *= 2.0f;
            return v + uv + uuv;
        }

        public void fromMatrix(matrix4 m)
        {
            float diag = m.at(0, 0) + m.at(1, 1) + m.at(2, 2);
            if (diag > 0.0f)
            {
                float scale = (float)Math.Sqrt(1.0f + diag);
                W = 0.5f * scale; scale = 0.5f / scale;
                X = (m.at(2, 1) - m.at(1, 2)) * scale;
                Y = (m.at(0, 2) - m.at(2, 0)) * scale;
                Z = (m.at(1, 0) - m.at(0, 1)) * scale;
            }
            else if (m.at(0, 0) > m.at(1, 1) && m.at(0, 0) > m.at(2, 2))
            {
                float scale = (float)Math.Sqrt(1.0f + m.at(0, 0) - m.at(1, 1) - m.at(2, 2));
                X = 0.5f * scale; scale = 0.5f / scale;
                Y = (m.at(0, 1) + m.at(1, 0)) * scale;
                Z = (m.at(2, 0) + m.at(0, 2)) * scale;
                W = (m.at(2, 1) - m.at(1, 2)) * scale;
            }
            else if (m.at(1, 1) > m.at(2, 2))
            {
                float scale = (float)Math.Sqrt(1.0f + m.at(1, 1) - m.at(0, 0) - m.at(2, 2));
                Y = 0.5f * scale; scale = 0.5f / scale;
                X = (m.at(0, 1) + m.at(1, 0)) * scale;
                Z = (m.at(1, 2) + m.at(2, 1)) * scale;
                W = (m.at(0, 2) - m.at(2, 0)) * scale;
            }
            else
            {
                float scale = (float)Math.Sqrt(1.0f + m.at(2, 2) - m.at(0, 0) - m.at(1, 1));
                Z = 0.5f * scale; scale = 0.5f / scale;
                X = (m.at(0, 2) + m.at(2, 0)) * scale;
                Y = (m.at(1, 2) + m.at(2, 1)) * scale;
                W = (m.at(1, 0) - m.at(0, 1)) * scale;
            }
            normalize();
        }

        /// <summary>quaternion::getMatrix() with the right-hand convention (getMatrix_transposed).</summary>
        public matrix4 getMatrix() { return getMatrix_transposed(); }

        public matrix4 getMatrix_transposed()
        {
            float _2xx = 2.0f * X * X, _2yy = 2.0f * Y * Y, _2zz = 2.0f * Z * Z;
            float _2xy = 2.0f * X * Y, _2xz = 2.0f * X * Z, _2xw = 2.0f * X * W;
            float _2yz = 2.0f * Y * Z, _2yw = 2.0f * Y * W, _2zw = 2.0f * Z * W;
            matrix4 d = matrix4.Identity;
            d[0] = 1.0f - _2yy - _2zz; d[4] = _2xy + _2zw; d[8] = _2xz - _2yw; d[12] = 0.0f;
            d[1] = _2xy - _2zw; d[5] = 1.0f - _2xx - _2zz; d[9] = _2yz + _2xw; d[13] = 0.0f;
            d[2] = _2xz + _2yw; d[6] = _2yz - _2xw; d[10] = 1.0f - _2yy - _2xx; d[14] = 0.0f;
            d[3] = 0; d[7] = 0; d[11] = 0; d[15] = 1;
            return d;
        }

        /// <summary>The non-transposed quaternion::getMatrix(dest) overload.</summary>
        public matrix4 getMatrixStd()
        {
            float _2xx = 2.0f * X * X, _2yy = 2.0f * Y * Y, _2zz = 2.0f * Z * Z;
            float _2xy = 2.0f * X * Y, _2xz = 2.0f * X * Z, _2xw = 2.0f * X * W;
            float _2yz = 2.0f * Y * Z, _2yw = 2.0f * Y * W, _2zw = 2.0f * Z * W;
            matrix4 d = matrix4.Identity;
            d[0] = 1.0f - _2yy - _2zz; d[1] = _2xy + _2zw; d[2] = _2xz - _2yw; d[3] = 0.0f;
            d[4] = _2xy - _2zw; d[5] = 1.0f - _2xx - _2zz; d[6] = _2yz + _2xw; d[7] = 0.0f;
            d[8] = _2xz + _2yw; d[9] = _2yz - _2xw; d[10] = 1.0f - _2yy - _2xx; d[11] = 0.0f;
            d[12] = 0; d[13] = 0; d[14] = 0; d[15] = 1;
            return d;
        }

        public quaternion makeInverse() { X = -X; Y = -Y; Z = -Z; return this; }
        public quaternion set(float x, float y, float z, float w) { X = x; Y = y; Z = z; W = w; return this; }

        /// <summary>Set from euler angles in radians.</summary>
        public quaternion set(float x, float y, float z)
        {
            double angle = x * 0.5; double sr = Math.Sin(angle), cr = Math.Cos(angle);
            angle = y * 0.5; double sp = Math.Sin(angle), cp = Math.Cos(angle);
            angle = z * 0.5; double sy = Math.Sin(angle), cy = Math.Cos(angle);
            double cpcy = cp * cy, spcy = sp * cy, cpsy = cp * sy, spsy = sp * sy;
            X = (float)(sr * cpcy - cr * spsy);
            Y = (float)(cr * spcy + sr * cpsy);
            Z = (float)(cr * cpsy - sr * spcy);
            W = (float)(cr * cpcy + sr * spsy);
            return normalize();
        }
        public quaternion set(v3f v) { return set(v.X, v.Y, v.Z); }

        public quaternion normalize()
        {
            float n = X * X + Y * Y + Z * Z + W * W;
            if (n == 1) return this;
            float r = IrrMath.reciprocal_squareroot(n);
            X *= r; Y *= r; Z *= r; W *= r;
            return this;
        }

        public float dotProduct(quaternion q2) { return X * q2.X + Y * q2.Y + Z * q2.Z + W * q2.W; }

        public quaternion slerp(quaternion q1, quaternion q2, float time)
        {
            float angle = q1.dotProduct(q2);
            if (angle < 0.0f) { q1 = q1 * -1.0f; angle *= -1.0f; }
            float scale, invscale;
            if ((angle + 1.0f) > 0.05f)
            {
                if ((1.0f - angle) >= 0.05f)
                {
                    float theta = (float)Math.Acos(angle);
                    float invsintheta = 1.0f / (float)Math.Sin(theta);
                    scale = (float)Math.Sin(theta * (1.0f - time)) * invsintheta;
                    invscale = (float)Math.Sin(theta * time) * invsintheta;
                    this = (q1 * scale) + (q2 * invscale);
                }
                else
                {
                    scale = 1.0f - time; invscale = time;
                    this = (q1 * scale) + (q2 * invscale);
                    normalize();
                }
            }
            else
            {
                q2 = new quaternion(-q1.Y, q1.X, -q1.W, q1.Z);
                scale = (float)Math.Sin(IrrMath.PI * (0.5f - time));
                invscale = (float)Math.Sin(IrrMath.PI * time);
                this = (q1 * scale) + (q2 * invscale);
            }
            return this;
        }

        public quaternion fromAngleAxis(float angle, v3f axis)
        {
            float fHalfAngle = 0.5f * angle, fSin = (float)Math.Sin(fHalfAngle);
            W = (float)Math.Cos(fHalfAngle);
            X = fSin * axis.X; Y = fSin * axis.Y; Z = fSin * axis.Z;
            return this;
        }

        public void toAngleAxis(out float angle, out v3f axis)
        {
            float scale = (float)Math.Sqrt(X * X + Y * Y + Z * Z);
            if (IrrMath.iszero(scale) || W > 1.0f || W < -1.0f)
            {
                angle = 0.0f; axis = new v3f(0, 1, 0);
            }
            else
            {
                float invscale = 1.0f / scale;
                angle = 2.0f * (float)Math.Acos(W);
                axis = new v3f(X * invscale, Y * invscale, Z * invscale);
            }
        }

        public void toEulerDegree(out v3f euler) { euler = getMatrixStd().getRotationDegrees(); }
        public void toEuler(out v3f euler) { toEulerDegree(out euler); euler *= IrrMath.DEGTORAD; }

        public quaternion makeIdentity() { W = 1; X = 0; Y = 0; Z = 0; return this; }

        public quaternion rotationFromTo(v3f from, v3f to)
        {
            v3f v0 = from.normalized(), v1 = to.normalized();
            float d = v0.dotProduct(v1);
            if (d >= 1.0f) return makeIdentity();
            if (d <= -1.0f)
            {
                v3f axis = new v3f(1, 0, 0).crossProduct(from);
                if (axis.getLengthSQ() == 0) axis = new v3f(0, 1, 0).crossProduct(from);
                axis.normalize();
                return fromAngleAxis(IrrMath.PI, axis);
            }
            float s = (float)Math.Sqrt((1 + d) * 2), invs = 1.0f / s;
            v3f c = new v3f(v0.Y * v1.Z - v0.Z * v1.Y, v0.Z * v1.X - v0.X * v1.Z, v0.X * v1.Y - v0.Y * v1.X) * invs;
            X = c.X; Y = c.Y; Z = c.Z; W = s * 0.5f;
            return this;
        }
    }

    /// <summary>
    /// Irrlicht CMatrix4 on top of Unity's Matrix4x4. Both store elements column-major
    /// (Irrlicht M[i] == Matrix4x4[i]), and Irrlicht's product equals Unity's product.
    /// </summary>
    [Serializable]
    public struct matrix4
    {
        public Matrix4x4 m;

        public matrix4(Matrix4x4 mat) { m = mat; }
        public static matrix4 Identity { get { return new matrix4(Matrix4x4.identity); } }

        public float this[int i] { get { return m[i]; } set { m[i] = value; } }
        /// <summary>Irrlicht's operator()(row, col) == M[row*4+col].</summary>
        public float at(int row, int col) { return m[row * 4 + col]; }

        public static matrix4 operator *(matrix4 a, matrix4 b) { return new matrix4(a.m * b.m); }
        public static matrix4 operator *(matrix4 a, float s)
        {
            matrix4 r = a;
            for (int i = 0; i < 16; i++) r.m[i] = a.m[i] * s;
            return r;
        }

        public bool isIdentity() { return m == Matrix4x4.identity; }
        public matrix4 makeIdentity() { m = Matrix4x4.identity; return this; }

        public v3f getTranslation() { return new v3f(m[12], m[13], m[14]); }
        public matrix4 setTranslation(v3f t) { m[12] = t.X; m[13] = t.Y; m[14] = t.Z; return this; }
        public matrix4 setInverseTranslation(v3f t) { m[12] = -t.X; m[13] = -t.Y; m[14] = -t.Z; return this; }
        public matrix4 setScale(v3f s) { m[0] = s.X; m[5] = s.Y; m[10] = s.Z; return this; }

        public matrix4 postScale(v3f s)
        {
            m[0] *= s.X; m[1] *= s.X; m[2] *= s.X;
            m[4] *= s.Y; m[5] *= s.Y; m[6] *= s.Y;
            m[8] *= s.Z; m[9] *= s.Z; m[10] *= s.Z;
            return this;
        }

        public v3f getScale()
        {
            return new v3f(new v3f(m[0], m[1], m[2]).getLength(), new v3f(m[4], m[5], m[6]).getLength(), new v3f(m[8], m[9], m[10]).getLength());
        }

        public matrix4 setRotationDegrees(v3f rotation) { return setRotationRadians(rotation * IrrMath.DEGTORAD); }

        public matrix4 setRotationRadians(v3f rotation)
        {
            double cr = Math.Cos(rotation.X), sr = Math.Sin(rotation.X);
            double cp = Math.Cos(rotation.Y), sp = Math.Sin(rotation.Y);
            double cy = Math.Cos(rotation.Z), sy = Math.Sin(rotation.Z);
            m[0] = (float)(cp * cy); m[1] = (float)(cp * sy); m[2] = (float)(-sp);
            double srsp = sr * sp, crsp = cr * sp;
            m[4] = (float)(srsp * cy - cr * sy); m[5] = (float)(srsp * sy + cr * cy); m[6] = (float)(sr * cp);
            m[8] = (float)(crsp * cy + sr * sy); m[9] = (float)(crsp * sy - sr * cy); m[10] = (float)(cr * cp);
            return this;
        }

        public v3f getRotationDegrees()
        {
            double Y = -Math.Asin(at(0, 2));
            double C = Math.Cos(Y);
            Y *= IrrMath.RADTODEG64;
            double rotx, roty, X, Z;
            if (Math.Abs(C) > IrrMath.ROUNDING_ERROR_64)
            {
                float invC = (float)(1.0 / C);
                rotx = at(2, 2) * invC; roty = at(1, 2) * invC;
                X = Math.Atan2(roty, rotx) * IrrMath.RADTODEG64;
                rotx = at(0, 0) * invC; roty = at(0, 1) * invC;
                Z = Math.Atan2(roty, rotx) * IrrMath.RADTODEG64;
            }
            else
            {
                X = 0.0;
                rotx = at(1, 1); roty = -at(1, 0);
                Z = Math.Atan2(roty, rotx) * IrrMath.RADTODEG64;
            }
            if (X < 0.0) X += 360.0;
            if (Y < 0.0) Y += 360.0;
            if (Z < 0.0) Z += 360.0;
            return new v3f((float)X, (float)Y, (float)Z);
        }

        public void rotateVect(ref v3f v)
        {
            v3f t = v;
            v.X = t.X * m[0] + t.Y * m[4] + t.Z * m[8];
            v.Y = t.X * m[1] + t.Y * m[5] + t.Z * m[9];
            v.Z = t.X * m[2] + t.Y * m[6] + t.Z * m[10];
        }
        public v3f rotateVect(v3f t) { rotateVect(ref t); return t; }

        public void inverseRotateVect(ref v3f v)
        {
            v3f t = v;
            v.X = t.X * m[0] + t.Y * m[1] + t.Z * m[2];
            v.Y = t.X * m[4] + t.Y * m[5] + t.Z * m[6];
            v.Z = t.X * m[8] + t.Y * m[9] + t.Z * m[10];
        }

        public void transformVect(ref v3f v)
        {
            float x = v.X * m[0] + v.Y * m[4] + v.Z * m[8] + m[12];
            float y = v.X * m[1] + v.Y * m[5] + v.Z * m[9] + m[13];
            float z = v.X * m[2] + v.Y * m[6] + v.Z * m[10] + m[14];
            v.X = x; v.Y = y; v.Z = z;
        }
        public v3f transformVect(v3f v) { transformVect(ref v); return v; }

        /// <summary>Full 4D transform with w output (multiplyWith1x4Matrix).</summary>
        public void transformVect4(v3f v, out float x, out float y, out float z, out float w)
        {
            x = v.X * m[0] + v.Y * m[4] + v.Z * m[8] + m[12];
            y = v.X * m[1] + v.Y * m[5] + v.Z * m[9] + m[13];
            z = v.X * m[2] + v.Y * m[6] + v.Z * m[10] + m[14];
            w = v.X * m[3] + v.Y * m[7] + v.Z * m[11] + m[15];
        }

        public bool getInverse(out matrix4 o)
        {
            float det = m.determinant;
            if (Math.Abs(det) < float.Epsilon) { o = Identity; return false; }
            o = new matrix4(m.inverse);
            return true;
        }

        public bool makeInverse()
        {
            matrix4 tmp;
            if (getInverse(out tmp)) { this = tmp; return true; }
            return false;
        }

        public matrix4 getTransposed() { return new matrix4(m.transpose); }

        /// <summary>Irrlicht's buildProjectionMatrixPerspectiveFov (D3D-style, z forward).</summary>
        public matrix4 buildProjectionMatrixPerspectiveFov(float fieldOfViewRadians, float aspectRatio, float zNear, float zFar)
        {
            double h = 1.0 / Math.Tan(fieldOfViewRadians / 2.0);
            float w = (float)(h / aspectRatio);
            Matrix4x4 r = new Matrix4x4();
            r[0] = w; r[5] = (float)h;
            r[10] = zFar / (zFar - zNear); r[11] = 1;
            r[14] = -zNear * zFar / (zFar - zNear); r[15] = 0;
            m = r;
            return this;
        }

        /// <summary>Irrlicht's buildProjectionMatrixOrtho (centered, z forward).</summary>
        public matrix4 buildProjectionMatrixOrtho(float widthOfViewVolume, float heightOfViewVolume, float zNear, float zFar)
        {
            Matrix4x4 r = new Matrix4x4();
            r[0] = 2 / widthOfViewVolume;
            r[5] = 2 / heightOfViewVolume;
            r[10] = 1 / (zFar - zNear);
            r[14] = zNear / (zNear - zFar);
            r[15] = 1;
            m = r;
            return this;
        }

        /// <summary>Irrlicht's buildCameraLookAtMatrix with the right-hand cross product.</summary>
        public matrix4 buildCameraLookAtMatrix(v3f position, v3f target, v3f upVector)
        {
            v3f zaxis = (target - position).normalized();
            v3f xaxis = upVector.crossProduct(zaxis).normalized();
            v3f yaxis = zaxis.crossProduct(xaxis);
            Matrix4x4 r = new Matrix4x4();
            r[0] = xaxis.X; r[1] = yaxis.X; r[2] = zaxis.X; r[3] = 0;
            r[4] = xaxis.Y; r[5] = yaxis.Y; r[6] = zaxis.Y; r[7] = 0;
            r[8] = xaxis.Z; r[9] = yaxis.Z; r[10] = zaxis.Z; r[11] = 0;
            r[12] = -xaxis.dotProduct(position); r[13] = -yaxis.dotProduct(position); r[14] = -zaxis.dotProduct(position); r[15] = 1;
            m = r;
            return this;
        }

        public matrix4 buildTextureTransform(float rotateRad, v2f rotatecenter, v2f translate, v2f scale)
        {
            float c = (float)Math.Cos(rotateRad), s = (float)Math.Sin(rotateRad);
            Matrix4x4 r = new Matrix4x4();
            r[0] = c * scale.X; r[1] = s * scale.Y; r[2] = 0; r[3] = 0;
            r[4] = -s * scale.X; r[5] = c * scale.Y; r[6] = 0; r[7] = 0;
            r[8] = scale.X * (rotatecenter.X - c * rotatecenter.X + s * rotatecenter.Y) + translate.X;
            r[9] = scale.Y * (rotatecenter.Y - s * rotatecenter.X - c * rotatecenter.Y) + translate.Y;
            r[10] = 1; r[11] = 0; r[12] = 0; r[13] = 0; r[14] = 0; r[15] = 1;
            m = r;
            return this;
        }

        public void transformBox(ref aabbox3df box)
        {
            v3f a = transformVect(box.MinEdge), b = transformVect(box.MaxEdge);
            box = new aabbox3df(a);
            box.addInternalPoint(b);
        }

        public void transformBoxEx(ref aabbox3df box)
        {
            v3f mn = box.MinEdge, mx = box.MaxEdge;
            aabbox3df r = new aabbox3df(transformVect(mn));
            for (int i = 1; i < 8; i++)
            {
                v3f p = new v3f((i & 1) != 0 ? mx.X : mn.X, (i & 2) != 0 ? mx.Y : mn.Y, (i & 4) != 0 ? mx.Z : mn.Z);
                r.addInternalPoint(transformVect(p));
            }
            box = r;
        }
    }

    [Serializable]
    public struct line3df
    {
        public v3f start, end;
        public line3df(v3f s, v3f e) { start = s; end = e; }
        public line3df(float xa, float ya, float za, float xb, float yb, float zb) { start = new v3f(xa, ya, za); end = new v3f(xb, yb, zb); }
        public void setLine(v3f s, v3f e) { start = s; end = e; }
        public float getLength() { return start.getDistanceFrom(end); }
        public float getLengthSQ() { return start.getDistanceFromSQ(end); }
        public v3f getMiddle() { return (start + end) * 0.5f; }
        public v3f getVector() { return end - start; }
        public bool isPointBetweenStartAndEnd(v3f p) { return p.isBetweenPoints(start, end); }
        public v3f getClosestPoint(v3f point)
        {
            v3f c = point - start, v = end - start;
            float d = v.getLength();
            v /= d;
            float t = v.dotProduct(c);
            if (t < 0.0f) return start;
            if (t > d) return end;
            v *= t;
            return start + v;
        }
        public bool getIntersectionWithSphere(v3f sorigin, float sradius, out double outdistance)
        {
            v3f q = sorigin - start;
            float c = q.getLength();
            float v = q.dotProduct(getVector().normalized());
            float d = sradius * sradius - (c * c - v * v);
            outdistance = 0;
            if (d < 0.0) return false;
            outdistance = v - Math.Sqrt(d);
            return true;
        }
    }

    public enum EIntersectionRelation3D { ISREL3D_FRONT = 0, ISREL3D_BACK, ISREL3D_PLANAR, ISREL3D_SPANNING, ISREL3D_CLIPPED }

    [Serializable]
    public struct plane3df
    {
        public v3f Normal;
        public float D;

        public plane3df(v3f point, v3f normal) { Normal = normal; D = -point.dotProduct(normal); }
        public plane3df(float px, float py, float pz, float nx, float ny, float nz) : this(new v3f(px, py, pz), new v3f(nx, ny, nz)) { }
        public plane3df(v3f p1, v3f p2, v3f p3) { Normal = v3f.Zero; D = 0; setPlane(p1, p2, p3); }

        public void setPlane(v3f point, v3f nvector) { Normal = nvector; recalculateD(point); }
        public void setPlane(v3f nvect, float d) { Normal = nvect; D = d; }
        public void setPlane(v3f p1, v3f p2, v3f p3)
        {
            Normal = (p2 - p1).crossProduct(p3 - p1);
            Normal.normalize();
            recalculateD(p1);
        }
        public void recalculateD(v3f p) { D = -p.dotProduct(Normal); }
        public v3f getMemberPoint() { return Normal * -D; }

        /// <summary>Like Irrlicht, leaves outIntersection untouched when the line is parallel.</summary>
        public bool getIntersectionWithLine(v3f linePoint, v3f lineVect, ref v3f outIntersection)
        {
            float t2 = Normal.dotProduct(lineVect);
            if (t2 == 0) return false;
            float t = -(Normal.dotProduct(linePoint) + D) / t2;
            outIntersection = linePoint + (lineVect * t);
            return true;
        }

        public float getKnownIntersectionWithLine(v3f p1, v3f p2)
        {
            v3f vect = p2 - p1;
            float t2 = Normal.dotProduct(vect);
            return -((Normal.dotProduct(p1) + D) / t2);
        }

        public bool getIntersectionWithLimitedLine(v3f p1, v3f p2, ref v3f outIntersection)
        {
            return getIntersectionWithLine(p1, p2 - p1, ref outIntersection) && outIntersection.isBetweenPoints(p1, p2);
        }

        public EIntersectionRelation3D classifyPointRelation(v3f point)
        {
            float d = Normal.dotProduct(point) + D;
            if (d < -IrrMath.ROUNDING_ERROR_32) return EIntersectionRelation3D.ISREL3D_BACK;
            if (d > IrrMath.ROUNDING_ERROR_32) return EIntersectionRelation3D.ISREL3D_FRONT;
            return EIntersectionRelation3D.ISREL3D_PLANAR;
        }

        public bool getIntersectionWithPlane(plane3df other, out v3f outLinePoint, out v3f outLineVect)
        {
            float fn00 = Normal.getLength(), fn01 = Normal.dotProduct(other.Normal), fn11 = other.Normal.getLength();
            double det = fn00 * fn11 - fn01 * fn01;
            outLinePoint = v3f.Zero; outLineVect = v3f.Zero;
            if (Math.Abs(det) < IrrMath.ROUNDING_ERROR_64) return false;
            double invdet = 1.0 / det;
            double fc0 = (fn11 * -D + fn01 * other.D) * invdet;
            double fc1 = (fn00 * -other.D + fn01 * D) * invdet;
            outLineVect = Normal.crossProduct(other.Normal);
            outLinePoint = Normal * (float)fc0 + other.Normal * (float)fc1;
            return true;
        }

        public bool getIntersectionWithPlanes(plane3df o1, plane3df o2, out v3f outPoint)
        {
            v3f lp, lv;
            outPoint = v3f.Zero;
            if (getIntersectionWithPlane(o1, out lp, out lv))
                return o2.getIntersectionWithLine(lp, lv, ref outPoint);
            return false;
        }

        public float getDistanceTo(v3f point) { return point.dotProduct(Normal) + D; }
        public bool isFrontFacing(v3f lookDirection) { return Normal.dotProduct(lookDirection) <= 0.0f; }
    }

    [Serializable]
    public struct aabbox3df
    {
        public v3f MinEdge, MaxEdge;
        public aabbox3df(v3f init) { MinEdge = init; MaxEdge = init; }
        public aabbox3df(v3f min, v3f max) { MinEdge = min; MaxEdge = max; }
        public void reset(v3f p) { MinEdge = p; MaxEdge = p; }
        public void addInternalPoint(v3f p)
        {
            if (p.X > MaxEdge.X) MaxEdge.X = p.X;
            if (p.Y > MaxEdge.Y) MaxEdge.Y = p.Y;
            if (p.Z > MaxEdge.Z) MaxEdge.Z = p.Z;
            if (p.X < MinEdge.X) MinEdge.X = p.X;
            if (p.Y < MinEdge.Y) MinEdge.Y = p.Y;
            if (p.Z < MinEdge.Z) MinEdge.Z = p.Z;
        }
        public void addInternalBox(aabbox3df b) { addInternalPoint(b.MaxEdge); addInternalPoint(b.MinEdge); }
        public v3f getCenter() { return (MinEdge + MaxEdge) / 2; }
        public v3f getExtent() { return MaxEdge - MinEdge; }
        public bool isPointInside(v3f p)
        {
            return p.X >= MinEdge.X && p.X <= MaxEdge.X && p.Y >= MinEdge.Y && p.Y <= MaxEdge.Y && p.Z >= MinEdge.Z && p.Z <= MaxEdge.Z;
        }
        public bool intersectsWithBox(aabbox3df o)
        {
            return MinEdge.X <= o.MaxEdge.X && MinEdge.Y <= o.MaxEdge.Y && MinEdge.Z <= o.MaxEdge.Z &&
                   MaxEdge.X >= o.MinEdge.X && MaxEdge.Y >= o.MinEdge.Y && MaxEdge.Z >= o.MinEdge.Z;
        }
        public bool intersectsWithLine(line3df line)
        {
            return intersectsWithLine(line.getMiddle(), line.getVector().normalized(), line.getLength() * 0.5f);
        }
        public bool intersectsWithLine(v3f linemiddle, v3f linevect, float halflength)
        {
            v3f e = getExtent() * 0.5f;
            v3f t = getCenter() - linemiddle;
            if ((Math.Abs(t.X) > e.X + halflength * Math.Abs(linevect.X)) ||
                (Math.Abs(t.Y) > e.Y + halflength * Math.Abs(linevect.Y)) ||
                (Math.Abs(t.Z) > e.Z + halflength * Math.Abs(linevect.Z)))
                return false;
            float r = e.Y * Math.Abs(linevect.Z) + e.Z * Math.Abs(linevect.Y);
            if (Math.Abs(t.Y * linevect.Z - t.Z * linevect.Y) > r) return false;
            r = e.X * Math.Abs(linevect.Z) + e.Z * Math.Abs(linevect.X);
            if (Math.Abs(t.Z * linevect.X - t.X * linevect.Z) > r) return false;
            r = e.X * Math.Abs(linevect.Y) + e.Y * Math.Abs(linevect.X);
            if (Math.Abs(t.X * linevect.Y - t.Y * linevect.X) > r) return false;
            return true;
        }
    }

    /// <summary>32-bit ARGB color (irr::video::SColor).</summary>
    [Serializable]
    public struct SColor
    {
        public uint color;
        public SColor(uint c) { color = c; }
        public SColor(int a, int r, int g, int b) { color = (((uint)a & 0xff) << 24) | (((uint)r & 0xff) << 16) | (((uint)g & 0xff) << 8) | ((uint)b & 0xff); }
        public static implicit operator SColor(uint c) { return new SColor(c); }
        public int A { get { return (int)(color >> 24); } set { color = (color & 0x00ffffff) | (((uint)value & 0xff) << 24); } }
        public int R { get { return (int)((color >> 16) & 0xff); } set { color = (color & 0xff00ffff) | (((uint)value & 0xff) << 16); } }
        public int G { get { return (int)((color >> 8) & 0xff); } set { color = (color & 0xffff00ff) | (((uint)value & 0xff) << 8); } }
        public int B { get { return (int)(color & 0xff); } set { color = (color & 0xffffff00) | ((uint)value & 0xff); } }
        public int getAlpha() { return A; }
        public int getRed() { return R; }
        public int getGreen() { return G; }
        public int getBlue() { return B; }
        public void setAlpha(int a) { A = a; }
        public void set(int a, int r, int g, int b) { this = new SColor(a, r, g, b); }
        /// <summary>Irrlicht SColor * float scales all four channels.</summary>
        public static SColor operator *(SColor c, float s)
        {
            return new SColor((int)(c.A * s), (int)(c.R * s), (int)(c.G * s), (int)(c.B * s));
        }
        public SColor getInterpolated(SColor other, float d)
        {
            float inv = 1.0f - d;
            return new SColor((int)(other.A * inv + A * d), (int)(other.R * inv + R * d), (int)(other.G * inv + G * d), (int)(other.B * inv + B * d));
        }
        public Color32 ToColor32() { return new Color32((byte)R, (byte)G, (byte)B, (byte)A); }
        public Color ToColor() { return new Color(R / 255f, G / 255f, B / 255f, A / 255f); }
        public override string ToString() { return "0x" + color.ToString("X8"); }
    }

    [Serializable]
    public struct SColorf
    {
        public float r, g, b, a;
        public SColorf(float r, float g, float b, float a = 1.0f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public SColorf(SColor c) { const float inv = 1.0f / 255.0f; r = c.R * inv; g = c.G * inv; b = c.B * inv; a = c.A * inv; }
        public SColor toSColor() { return new SColor((int)(a * 255.0f), (int)(r * 255.0f), (int)(g * 255.0f), (int)(b * 255.0f)); }
        public SColorf getInterpolated(SColorf other, float d)
        {
            float inv = 1.0f - d;
            return new SColorf(other.r * inv + r * d, other.g * inv + g * d, other.b * inv + b * d, other.a * inv + a * d);
        }
        public Color ToColor() { return new Color(r, g, b, a); }
    }
}
