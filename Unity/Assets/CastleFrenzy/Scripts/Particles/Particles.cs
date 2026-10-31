// Particle/: CFpsParticleSystemSceneNode with its box emitter and affectors, the presets read
// from Effects/effects.xml, and the ParticleSystem wrapper/manager used by the game.
using System;
using System.Collections.Generic;
using static CastleFrenzy.G;

namespace CastleFrenzy
{
    public struct SFpsParticle
    {
        public v3f pos, oldPos, vector;
        public uint startTime, endTime;
        public SColor color, startColor;
        public v3f startVector;
        public float spin, targetSpin, initialSpin, spinVariation;
        public float width, height, targetWidthDelta, targetHeightDelta, startWidth, startHeight;
        public float initialRotation;
    }

    static class PRand
    {
        static readonly Random s_r = new Random(1234);
        public static int rand() { return s_r.Next(0, 0x7fffffff); }
    }

    public class CFpsParticleBoxEmitter
    {
        public aabbox3df Box = new aabbox3df(new v3f(-10, 0, -10), new v3f(5, 30, 10));
        public v3f Direction = new v3f(0, 0.03f, 0);
        public int SpeedVariation;
        public uint MinParticlesPerSecond = 5, MaxParticlesPerSecond = 10;
        public float ParticleWidth = 5, ParticleHeight = 5;
        public int SizeVariation;
        public SColor MinStartColor = new SColor(255, 0, 0, 0), MaxStartColor = new SColor(255, 255, 255, 255);
        public uint MinLifeTime = 2000, MaxLifeTime = 4000;
        public int MaxAngleDegreesXY, MaxAngleDegreesYZ, MaxAngleDegreesXZ, InitialRotMin, InitialRotMax;
        uint Time;
        readonly List<SFpsParticle> m_out = new List<SFpsParticle>();

        public CFpsParticleBoxEmitter Clone()
        {
            CFpsParticleBoxEmitter e = (CFpsParticleBoxEmitter)MemberwiseClone();
            e.Time = 0;
            return e;
        }

        public void Reset() { Time = 0; }
        public v3f getDirection() { return Direction; }

        public List<SFpsParticle> emitt(uint now, uint timeSinceLastCall, float scale, quaternion rotation)
        {
            m_out.Clear();
            Time += timeSinceLastCall;
            uint pps = MaxParticlesPerSecond - MinParticlesPerSecond;
            float perSecond = pps != 0 ? (float)MinParticlesPerSecond + (PRand.rand() % pps) : MinParticlesPerSecond;
            float everyWhatMillisecond = 1000.0f / perSecond;
            if (Time <= everyWhatMillisecond) return m_out;
            v3f mn = Box.MinEdge, mx = Box.MaxEdge;
            aabbox3df myBox = new aabbox3df(rotation * (new v3f(mn.X, mn.Y, mn.Z) * scale));
            for (int i = 1; i < 8; ++i)
            {
                v3f e = new v3f((i & 1) != 0 ? mx.X : mn.X, (i & 2) != 0 ? mx.Y : mn.Y, (i & 4) != 0 ? mx.Z : mn.Z);
                myBox.addInternalPoint(rotation * (e * scale));
            }
            v3f myDirection = rotation * (Direction * scale);
            float myW = ParticleWidth * scale, myH = ParticleHeight * scale;
            uint amount = (uint)((Time / everyWhatMillisecond) + 0.5f);
            Time = 0;
            v3f extent = myBox.getExtent();
            if (amount > MaxParticlesPerSecond * 2) amount = MaxParticlesPerSecond * 2;
            for (uint i = 0; i < amount; ++i)
            {
                SFpsParticle p = new SFpsParticle();
                p.pos.X = myBox.MinEdge.X + (extent.X != 0 ? (float)PRand.rand() % extent.X : float.NaN);
                p.pos.Y = myBox.MinEdge.Y + (extent.Y != 0 ? (float)PRand.rand() % extent.Y : float.NaN);
                p.pos.Z = myBox.MinEdge.Z + (extent.Z != 0 ? (float)PRand.rand() % extent.Z : float.NaN);
                if (float.IsNaN(p.pos.X)) p.pos.X = myBox.MinEdge.X;
                if (float.IsNaN(p.pos.Y)) p.pos.Y = myBox.MinEdge.Y;
                if (float.IsNaN(p.pos.Z)) p.pos.Z = myBox.MinEdge.Z;
                p.startTime = now;
                p.width = myW; p.height = myH;
                p.spin = 0;
                int deltaRot = InitialRotMax - InitialRotMin;
                p.initialRotation = InitialRotMin;
                if (deltaRot != 0) p.initialRotation = InitialRotMin + (PRand.rand() % deltaRot);
                if (SizeVariation != 0)
                {
                    int rnd = PRand.rand() % (SizeVariation << 1) - SizeVariation;
                    p.width = p.width + p.width * rnd / 100;
                    p.height = p.height + p.height * rnd / 100;
                }
                v3f tgt = myDirection;
                if (MaxAngleDegreesXY != 0) tgt.rotateXYBy((PRand.rand() % (MaxAngleDegreesXY * 2)) - MaxAngleDegreesXY);
                if (MaxAngleDegreesYZ != 0) tgt.rotateYZBy((PRand.rand() % (MaxAngleDegreesYZ * 2)) - MaxAngleDegreesYZ);
                if (MaxAngleDegreesXZ != 0) tgt.rotateXZBy((PRand.rand() % (MaxAngleDegreesXZ * 2)) - MaxAngleDegreesXZ);
                p.vector = tgt;
                if (MaxLifeTime - MinLifeTime == 0) p.endTime = now + MinLifeTime;
                else p.endTime = now + MinLifeTime + (uint)(PRand.rand() % (MaxLifeTime - MinLifeTime));
                p.color = MinStartColor.getInterpolated(MaxStartColor, (PRand.rand() % 100) / 100.0f);
                p.startColor = p.color;
                if (SpeedVariation > 0) p.vector += p.vector * ((PRand.rand() % SpeedVariation) / 100.0f);
                p.startVector = p.vector;
                m_out.Add(p);
            }
            return m_out;
        }

        public int deserializeAttributes(IAttributes inp)
        {
            v3f b = inp.getAttributeAsVector3d("Box");
            if (b.X < 0) b.X = 0; if (b.Y < 0) b.Y = 0; if (b.Z < 0) b.Z = 0;
            Box = new aabbox3df(new v3f(-b.X, -b.Y, -b.Z), new v3f(b.X, b.Y, b.Z));
            Direction = inp.getAttributeAsVector3d("Direction");
            SpeedVariation = inp.getAttributeAsInt("SpeedVariation");
            MinParticlesPerSecond = (uint)Math.Max(0, inp.getAttributeAsInt("MinParticlesPerSecond"));
            MaxParticlesPerSecond = (uint)Math.Max(0, inp.getAttributeAsInt("MaxParticlesPerSecond"));
            MinParticlesPerSecond = Math.Max(1u, MinParticlesPerSecond);
            MaxParticlesPerSecond = Math.Max(MaxParticlesPerSecond, 1u);
            MaxParticlesPerSecond = Math.Min(MaxParticlesPerSecond, 200u);
            MinParticlesPerSecond = Math.Min(MinParticlesPerSecond, MaxParticlesPerSecond);
            ParticleWidth = inp.getAttributeAsFloat("ParticleWidth");
            ParticleHeight = inp.getAttributeAsFloat("ParticleHeight");
            SizeVariation = inp.getAttributeAsInt("SizeVariation");
            MinStartColor = inp.getAttributeAsColor("MinStartColor");
            MaxStartColor = inp.getAttributeAsColor("MaxStartColor");
            MinLifeTime = (uint)Math.Max(0, inp.getAttributeAsInt("MinLifeTime"));
            MaxLifeTime = (uint)Math.Max(0, inp.getAttributeAsInt("MaxLifeTime"));
            MaxAngleDegreesXY = inp.getAttributeAsInt("MaxAngleDegXY");
            MaxAngleDegreesYZ = inp.getAttributeAsInt("MaxAngleDegYZ");
            MaxAngleDegreesXZ = inp.getAttributeAsInt("MaxAngleDegXZ");
            InitialRotMin = inp.getAttributeAsInt("InitialRotMin");
            InitialRotMax = inp.getAttributeAsInt("InitialRotMax");
            MaxLifeTime = Math.Max(MaxLifeTime, MinLifeTime);
            MinLifeTime = Math.Min(MinLifeTime, MaxLifeTime);
            return inp.findAttribute("InitialRotMax");
        }
    }

    public abstract class IFpsParticleAffector
    {
        public bool Enabled = true;
        public abstract void affect(uint lastTime, uint now, SFpsParticle[] p, int count, float scale, quaternion rotation);
        public abstract int deserializeAttributes(int startIndex, IAttributes inp);
        public abstract IFpsParticleAffector Clone();

        protected static bool Expect(IAttributes inp, int idx, string name) { return idx < inp.getAttributeCount() && inp.getAttributeName(idx) == name; }

        protected static bool Window(SFpsParticle p, float startPct, float endPct, uint lastTime, uint now, out uint startTime, out uint endTime)
        {
            uint delta = p.endTime - p.startTime;
            startTime = p.startTime + (uint)(delta * startPct);
            endTime = p.startTime + (uint)(delta * endPct);
            return now >= startTime && (now <= endTime || lastTime < endTime);
        }
    }

    public class CFpsParticleFadeOutAffector : IFpsParticleAffector
    {
        public SColor TargetColor = new SColor(0, 0, 0, 0);
        public float StartTimePercent = 0, EndTimePercent = 1;
        public override IFpsParticleAffector Clone() { return (IFpsParticleAffector)MemberwiseClone(); }

        public override void affect(uint lastTime, uint now, SFpsParticle[] p, int count, float scale, quaternion rotation)
        {
            if (!Enabled) return;
            for (int i = 0; i < count; ++i)
            {
                uint st, et;
                if (!Window(p[i], StartTimePercent, EndTimePercent, lastTime, now, out st, out et)) continue;
                if (now == st || lastTime < st) p[i].startColor = p[i].color;
                if (st == et) p[i].color = TargetColor;
                else if (st < et)
                {
                    uint t = now > et ? et : now;
                    float d = (float)(t - st) / (et - st);
                    p[i].color = p[i].startColor.getInterpolated(TargetColor, 1 - d);
                }
            }
        }

        public override int deserializeAttributes(int i, IAttributes inp)
        {
            if (!Expect(inp, i, "TargetColor")) return i;
            TargetColor = inp.getAttributeAsColor(i++);
            if (!Expect(inp, i, "StartTime(%)")) return i;
            StartTimePercent = inp.getAttributeAsInt(i++) / 100f;
            if (!Expect(inp, i, "EndTime(%)")) return i;
            EndTimePercent = inp.getAttributeAsInt(i++) / 100f;
            return i;
        }
    }

    public class CFpsParticleGravityAffector : IFpsParticleAffector
    {
        public v3f Gravity = new v3f(0, 0, -0.03f);
        public float StartTimePercent = 0, EndTimePercent = 1;
        public override IFpsParticleAffector Clone() { return (IFpsParticleAffector)MemberwiseClone(); }

        public override void affect(uint lastTime, uint now, SFpsParticle[] p, int count, float scale, quaternion rotation)
        {
            if (!Enabled) return;
            v3f g = rotation * (Gravity * scale);
            for (int i = 0; i < count; ++i)
            {
                uint st, et;
                if (!Window(p[i], StartTimePercent, EndTimePercent, lastTime, now, out st, out et)) continue;
                if (now == st || lastTime < st) p[i].startVector = p[i].vector;
                if (st == et) p[i].vector = g;
                else if (st < et)
                {
                    uint t = now > et ? et : now;
                    float d = (float)(t - st) / (et - st);
                    p[i].vector = p[i].startVector.getInterpolated(g, 1 - d);
                }
            }
        }

        public override int deserializeAttributes(int i, IAttributes inp)
        {
            if (!Expect(inp, i, "Gravity")) return i;
            Gravity = inp.getAttributeAsVector3d(i++);
            if (!Expect(inp, i, "StartTime(%)")) return i;
            StartTimePercent = inp.getAttributeAsInt(i++) / 100f;
            if (!Expect(inp, i, "EndTime(%)")) return i;
            EndTimePercent = inp.getAttributeAsInt(i++) / 100f;
            return i;
        }
    }

    public class CFpsParticleAttractionAffector : IFpsParticleAffector
    {
        public v3f Point;
        public float Speed = 1;
        public bool Attract = true, AffectX = true, AffectY = true, AffectZ = true;
        uint LastTime;
        public override IFpsParticleAffector Clone() { CFpsParticleAttractionAffector a = (CFpsParticleAttractionAffector)MemberwiseClone(); a.LastTime = 0; return a; }

        public override void affect(uint lastTime, uint now, SFpsParticle[] p, int count, float scale, quaternion rotation)
        {
            if (LastTime == 0) { LastTime = now; return; }
            float dt = (now - LastTime) / 1000.0f;
            LastTime = now;
            if (!Enabled) return;
            v3f myPoint = rotation * (Point * scale);
            float mySpeed = Speed * scale;
            for (int i = 0; i < count; ++i)
            {
                v3f dir = (myPoint - p[i].pos).normalized() * (mySpeed * dt);
                if (!Attract) dir *= -1.0f;
                if (AffectX) p[i].pos.X += dir.X;
                if (AffectY) p[i].pos.Y += dir.Y;
                if (AffectZ) p[i].pos.Z += dir.Z;
            }
        }

        public override int deserializeAttributes(int i, IAttributes inp)
        {
            if (!Expect(inp, i, "Point")) return i; Point = inp.getAttributeAsVector3d(i++);
            if (!Expect(inp, i, "Speed")) return i; Speed = inp.getAttributeAsFloat(i++);
            if (!Expect(inp, i, "Attract")) return i; Attract = inp.getAttributeAsBool(i++);
            if (!Expect(inp, i, "AffectX")) return i; AffectX = inp.getAttributeAsBool(i++);
            if (!Expect(inp, i, "AffectY")) return i; AffectY = inp.getAttributeAsBool(i++);
            if (!Expect(inp, i, "AffectZ")) return i; AffectZ = inp.getAttributeAsBool(i++);
            return i;
        }
    }

    public class CFpsParticleRotationAffector : IFpsParticleAffector
    {
        public v3f PivotPoint, Speed = new v3f(5, 5, 5);
        uint LastTime;
        public override IFpsParticleAffector Clone() { CFpsParticleRotationAffector a = (CFpsParticleRotationAffector)MemberwiseClone(); a.LastTime = 0; return a; }

        public override void affect(uint lastTime, uint now, SFpsParticle[] p, int count, float scale, quaternion rotation)
        {
            if (LastTime == 0) { LastTime = now; return; }
            float dt = (now - LastTime) / 1000.0f;
            LastTime = now;
            if (!Enabled) return;
            v3f pivot = rotation * (PivotPoint * scale), speed = rotation * (Speed * scale);
            for (int i = 0; i < count; ++i)
            {
                if (speed.X != 0) p[i].pos.rotateYZBy(dt * speed.X, pivot);
                if (speed.Y != 0) p[i].pos.rotateXZBy(dt * speed.Y, pivot);
                if (speed.Z != 0) p[i].pos.rotateXYBy(dt * speed.Z, pivot);
            }
        }

        public override int deserializeAttributes(int i, IAttributes inp)
        {
            if (!Expect(inp, i, "PivotPoint")) return i; PivotPoint = inp.getAttributeAsVector3d(i++);
            if (!Expect(inp, i, "Speed")) return i; Speed = inp.getAttributeAsVector3d(i++);
            return i;
        }
    }

    public class CFpsParticleSizeAffector : IFpsParticleAffector
    {
        public float TargetWidth = 1, TargetHeight = 1, StartTimePercent = 0, EndTimePercent = 1;
        public int Variation;
        public override IFpsParticleAffector Clone() { return (IFpsParticleAffector)MemberwiseClone(); }

        public override void affect(uint lastTime, uint now, SFpsParticle[] p, int count, float scale, quaternion rotation)
        {
            if (!Enabled) return;
            float tw = TargetWidth * scale, th = TargetHeight * scale;
            for (int i = 0; i < count; ++i)
            {
                uint st, et;
                if (!Window(p[i], StartTimePercent, EndTimePercent, lastTime, now, out st, out et)) continue;
                if (now == st || lastTime < st)
                {
                    p[i].startWidth = p[i].width;
                    p[i].startHeight = p[i].height;
                    if (Variation > 0)
                    {
                        float rnd = ((PRand.rand() % (Variation << 1)) - Variation) * 0.01f;
                        p[i].targetWidthDelta = tw + tw * rnd - p[i].startWidth;
                        p[i].targetHeightDelta = th + th * rnd - p[i].startHeight;
                    }
                    else
                    {
                        p[i].targetWidthDelta = tw - p[i].startWidth;
                        p[i].targetHeightDelta = th - p[i].startHeight;
                    }
                }
                if (st == et)
                {
                    p[i].width = p[i].startWidth + p[i].targetWidthDelta;
                    p[i].height = p[i].startHeight + p[i].targetHeightDelta;
                }
                else if (st < et)
                {
                    uint t = now > et ? et : now;
                    float d = (float)(t - st) / (et - st);
                    p[i].width = p[i].startWidth + p[i].targetWidthDelta * d;
                    p[i].height = p[i].startHeight + p[i].targetHeightDelta * d;
                }
            }
        }

        public override int deserializeAttributes(int i, IAttributes inp)
        {
            if (!Expect(inp, i, "TargetWidth")) return i; TargetWidth = inp.getAttributeAsFloat(i++);
            if (!Expect(inp, i, "TargetHeight")) return i; TargetHeight = inp.getAttributeAsFloat(i++);
            if (!Expect(inp, i, "Variation")) return i; Variation = inp.getAttributeAsInt(i++);
            if (!Expect(inp, i, "StartTime(%)")) return i; StartTimePercent = inp.getAttributeAsInt(i++) / 100f;
            if (!Expect(inp, i, "EndTime(%)")) return i; EndTimePercent = inp.getAttributeAsInt(i++) / 100f;
            return i;
        }
    }

    public class CFpsParticleSpinAffector : IFpsParticleAffector
    {
        public int MinSpin = 0, MaxSpin = 360;
        public float StartTimePercent = 0, EndTimePercent = 1;
        public override IFpsParticleAffector Clone() { return (IFpsParticleAffector)MemberwiseClone(); }

        public override void affect(uint lastTime, uint now, SFpsParticle[] p, int count, float scale, quaternion rotation)
        {
            if (!Enabled) return;
            for (int i = 0; i < count; ++i)
            {
                uint st, et;
                if (!Window(p[i], StartTimePercent, EndTimePercent, lastTime, now, out st, out et)) continue;
                if (now == st || lastTime < st)
                {
                    p[i].targetSpin = MinSpin;
                    int deltaSpin = MaxSpin - MinSpin;
                    if (deltaSpin != 0) p[i].targetSpin += deltaSpin > 0 ? (PRand.rand() % deltaSpin) : -(PRand.rand() % deltaSpin);
                    p[i].targetSpin *= DEGTORAD;
                    p[i].initialSpin = p[i].spin;
                }
                if (st == et) p[i].spin = p[i].targetSpin;
                else if (st < et)
                {
                    uint t = now > et ? et : now;
                    float d = (float)(t - st) / (et - st);
                    p[i].spin = p[i].initialSpin + p[i].targetSpin * d;
                }
            }
        }

        public override int deserializeAttributes(int i, IAttributes inp)
        {
            if (!Expect(inp, i, "MinSpin")) return i; MinSpin = inp.getAttributeAsInt(i++);
            if (!Expect(inp, i, "MaxSpin")) return i; MaxSpin = inp.getAttributeAsInt(i++);
            if (!Expect(inp, i, "StartTime(%)")) return i; StartTimePercent = inp.getAttributeAsInt(i++) / 100f;
            if (!Expect(inp, i, "EndTime(%)")) return i; EndTimePercent = inp.getAttributeAsInt(i++) / 100f;
            return i;
        }
    }

    public class CFpsParticleSystemSceneNode : ISceneNode
    {
        public readonly List<IFpsParticleAffector> AffectorList = new List<IFpsParticleAffector>();
        public CFpsParticleBoxEmitter Emitter;
        SFpsParticle[] Particles = new SFpsParticle[64];
        int ParticleCount;
        uint LastEmitTime;
        public bool DirectionalRotation = true, ProjectDirection, ParticlesAreGlobal = true;
        public int SysMinLifeTime = -1, SysMaxLifeTime = -1, RestartTimeMin = -1, RestartTimeMax = -1, StartDelay;
        uint TotalEmitTime, RandomSysLifeTime, RandomRestartTime, TotalRestartTime;
        int TotalDelay;
        bool m_bPaused;
        public float SystemScale = 1.0f;
        public quaternion SystemRotation = quaternion.Identity;
        public readonly SMaterial Material = new SMaterial();
        S3DVertex[] m_verts = new S3DVertex[256];
        int[] m_indices = new int[384];

        public override ESCENE_NODE_TYPE getType() { return ESCENE_NODE_TYPE.ESNT_UNKNOWN; }
        public override int getMaterialCount() { return 1; }
        public override SMaterial getMaterial(int i) { return Material; }

        void SetRandomLifeTime()
        {
            TotalEmitTime = 0; TotalRestartTime = 0; TotalDelay = 0;
            RandomSysLifeTime = SysMinLifeTime == SysMaxLifeTime ? (uint)SysMinLifeTime : (uint)(SysMinLifeTime + (PRand.rand() % (SysMaxLifeTime - SysMinLifeTime)));
            RandomRestartTime = RestartTimeMin == RestartTimeMax ? (uint)RestartTimeMin : (uint)(RestartTimeMin + (PRand.rand() % (RestartTimeMax - RestartTimeMin)));
        }

        public bool IsEnded()
        {
            return ParticleCount == 0 && TotalEmitTime > RandomSysLifeTime && (RestartTimeMin == -1 || RestartTimeMax == -1);
        }

        public void Restart()
        {
            if (Emitter != null) Emitter.Reset();
            m_bPaused = false;
            LastEmitTime = 0;
            SetRandomLifeTime();
        }

        public void Pause() { m_bPaused = true; }
        public void UnPause() { m_bPaused = false; }
        public void SetSystemScale(float s) { SystemScale = s; }
        public void SetSystemRotation(quaternion q) { SystemRotation = q; }

        public void deserializeAttributes(IAttributes inp)
        {
            Name = inp.getAttributeAsString("Name");
            RelativeTranslation = inp.getAttributeAsVector3d("Position");
            int ri = inp.findAttribute("Rotation");
            if (ri >= 0) { float[] q = Attr.ParseFloats(inp.getRaw(ri), 4); RelativeRotation = new quaternion(q[0], q[1], q[2], q[3]); }
            int si = inp.findAttribute("Scale");
            RelativeScale = si >= 0 ? inp.getAttributeAsVector3d(si) : new v3f(1, 1, 1);
            ParticlesAreGlobal = inp.getAttributeAsBool("GlobalParticles");
            DirectionalRotation = inp.getAttributeAsBool("DirectionalRotation");
            ProjectDirection = inp.getAttributeAsBool("ProjectDirection");
            SysMinLifeTime = inp.existsAttribute("SysMinLifeTime") ? inp.getAttributeAsInt("SysMinLifeTime") : -1;
            SysMaxLifeTime = inp.existsAttribute("SysMaxLifeTime") ? inp.getAttributeAsInt("SysMaxLifeTime") : -1;
            RestartTimeMin = inp.existsAttribute("RestartTimeMin") ? inp.getAttributeAsInt("RestartTimeMin") : -1;
            RestartTimeMax = inp.existsAttribute("RestartTimeMax") ? inp.getAttributeAsInt("RestartTimeMax") : -1;
            StartDelay = inp.getAttributeAsInt("StartDelay");
            SetRandomLifeTime();
            int emitterIdx = inp.findAttribute("Emitter");
            if (emitterIdx == -1) return;
            Emitter = null;
            if (inp.getAttributeAsString(emitterIdx) == "Box") Emitter = new CFpsParticleBoxEmitter();
            int idx = 0;
            if (Emitter != null) idx = Emitter.deserializeAttributes(inp);
            ++idx;
            AffectorList.Clear();
            int cnt = inp.getAttributeCount();
            while (idx < cnt)
            {
                if (inp.getAttributeName(idx) != "Affector") return;
                string type = inp.getAttributeAsString(idx);
                IFpsParticleAffector aff = null;
                switch (type)
                {
                    case "Attract": aff = new CFpsParticleAttractionAffector(); break;
                    case "FadeOut": aff = new CFpsParticleFadeOutAffector(); break;
                    case "Gravity": aff = new CFpsParticleGravityAffector(); break;
                    case "Scale": case "Size": aff = new CFpsParticleSizeAffector(); break;
                    case "Rotate": case "Rotation": aff = new CFpsParticleRotationAffector(); break;
                    case "Spin": aff = new CFpsParticleSpinAffector(); break;
                }
                ++idx;
                if (aff != null)
                {
                    idx = aff.deserializeAttributes(idx, inp);
                    AffectorList.Add(aff);
                }
            }
        }

        public CFpsParticleSystemSceneNode CloneNode()
        {
            CFpsParticleSystemSceneNode nb = new CFpsParticleSystemSceneNode
            {
                Name = Name, RelativeTranslation = RelativeTranslation, RelativeRotation = RelativeRotation, RelativeScale = RelativeScale,
                DirectionalRotation = DirectionalRotation, ProjectDirection = ProjectDirection, SysMinLifeTime = SysMinLifeTime,
                SysMaxLifeTime = SysMaxLifeTime, RestartTimeMin = RestartTimeMin, RestartTimeMax = RestartTimeMax, StartDelay = StartDelay,
                ParticlesAreGlobal = ParticlesAreGlobal, LastEmitTime = LastEmitTime,
            };
            nb.SetRandomLifeTime();
            nb.Material.MaterialType = Material.MaterialType;
            nb.Material.Textures[0] = Material.Textures[0];
            nb.Material.Lighting = false;
            nb.Material.ZWriteEnable = false;
            nb.Material.ZBuffer = Material.ZBuffer;
            nb.Material.BackfaceCulling = false;
            foreach (IFpsParticleAffector a in AffectorList) nb.AffectorList.Add(a.Clone());
            if (Emitter != null) nb.Emitter = Emitter.Clone();
            return nb;
        }

        public override void OnAnimate(int timeMs)
        {
            base.OnAnimate(timeMs);
            doParticleSystem((uint)timeMs);
        }

        void doParticleSystem(uint time)
        {
            if (LastEmitTime == 0) LastEmitTime = time;
            uint now = time;
            uint timediff = time - LastEmitTime;
            LastEmitTime = time;
            if (m_bPaused) return;
            bool bDelay = false;
            if (TotalDelay < StartDelay) { TotalDelay += (int)timediff; bDelay = true; }
            bool bEmit = true;
            if (!bDelay && SysMinLifeTime != -1 && SysMaxLifeTime != -1 && TotalEmitTime > RandomSysLifeTime)
            {
                if (RestartTimeMin != -1 && RestartTimeMax != -1)
                {
                    TotalRestartTime += timediff;
                    if (TotalRestartTime > RandomRestartTime) Restart();
                }
                bEmit = false;
            }
            v3f oldPos = getPosition();
            setPosition(SystemRotation * (oldPos * SystemScale));
            updateAbsolutePosition();
            if (Emitter != null && IsVisible && bEmit && !bDelay)
            {
                uint t = timediff;
                if (t == 0 && StartDelay < 0) t = (uint)(-StartDelay);
                List<SFpsParticle> arr = Emitter.emitt(now, t, SystemScale, SystemRotation);
                int newParticles = arr.Count;
                if (newParticles > 16250 - ParticleCount) newParticles = 16250 - ParticleCount;
                for (int i = 0; i < newParticles; ++i)
                {
                    SFpsParticle p = arr[i];
                    AbsoluteTransformation.rotateVect(ref p.startVector);
                    if (ParticlesAreGlobal) AbsoluteTransformation.transformVect(ref p.pos);
                    if (ParticleCount >= Particles.Length) Array.Resize(ref Particles, Particles.Length * 2);
                    Particles[ParticleCount++] = p;
                }
            }
            if (DirectionalRotation) for (int i = 0; i < ParticleCount; i++) Particles[i].oldPos = Particles[i].pos;
            foreach (IFpsParticleAffector a in AffectorList) a.affect(now - timediff, now, Particles, ParticleCount, SystemScale, SystemRotation);
            float scale = timediff;
            for (int i = 0; i < ParticleCount;)
            {
                if (now > Particles[i].endTime)
                {
                    Array.Copy(Particles, i + 1, Particles, i, ParticleCount - i - 1);
                    ParticleCount--;
                }
                else
                {
                    Particles[i].pos += Particles[i].vector * scale;
                    ++i;
                }
            }
            if (!bDelay && bEmit) TotalEmitTime += timediff;
            setPosition(oldPos);
        }

        public override void OnRegisterSceneNode()
        {
            if (IsVisible && ParticleCount != 0)
            {
                CSceneManager.Instance.registerNodeForRendering(this, 0, Material);
                base.OnRegisterSceneNode();
            }
        }

        public override void render(int buffer)
        {
            CCameraSceneNode camera = CSceneManager.Instance.getActiveCamera();
            if (camera == null || ParticleCount == 0) return;
            bool hasSpin = false;
            SColor targetTransparency = new SColor(0);
            foreach (IFpsParticleAffector a in AffectorList)
            {
                if (a is CFpsParticleSpinAffector) hasSpin = true;
                if (a is CFpsParticleFadeOutAffector) targetTransparency = ((CFpsParticleFadeOutAffector)a).TargetColor;
            }
            matrix4 m = camera.getViewMatrix();
            v3f view = new v3f(-m[2], -m[6], -m[10]) * -1.0f;
            if (m_verts.Length < ParticleCount * 4) { m_verts = new S3DVertex[ParticleCount * 4]; m_indices = new int[ParticleCount * 6]; }
            int idx = 0;
            for (int i = 0; i < ParticleCount; ++i)
            {
                SFpsParticle p = Particles[i];
                float f = 0.5f * p.width;
                v3f hor = new v3f(m[0] * f, m[4] * f, m[8] * f);
                f = -0.5f * p.height;
                v3f ver = new v3f(m[1] * f, m[5] * f, m[9] * f);
                if (p.initialRotation != 0)
                {
                    quaternion q = new quaternion();
                    q.fromAngleAxis(p.initialRotation * DEGTORAD, view);
                    matrix4 mat = q.getMatrix();
                    mat.rotateVect(ref hor); mat.rotateVect(ref ver);
                }
                if (hasSpin)
                {
                    quaternion q = new quaternion();
                    q.fromAngleAxis(p.spin, view);
                    matrix4 mat = q.getMatrix();
                    mat.rotateVect(ref hor); mat.rotateVect(ref ver);
                }
                else if (DirectionalRotation)
                {
                    v3f moved = p.pos - p.oldPos;
                    if (moved.getLengthSQ() > 0)
                    {
                        quaternion q = new quaternion();
                        if (!ProjectDirection)
                        {
                            v3f dir = Emitter != null ? Emitter.getDirection() : new v3f(0, 0, 1);
                            q.rotationFromTo(SystemRotation * (dir * SystemScale), moved);
                        }
                        else
                        {
                            p2i p1 = CSceneManager.Instance.getScreenCoordinatesFrom3DPosition(p.pos);
                            p2i p2 = CSceneManager.Instance.getScreenCoordinatesFrom3DPosition(p.oldPos);
                            q.fromAngleAxis((float)Math.Atan2(p1.X - p2.X, p1.Y - p2.Y), view);
                        }
                        matrix4 mat = q.getMatrix();
                        mat.rotateVect(ref hor); mat.rotateVect(ref ver);
                    }
                }
                m_verts[idx + 3] = new S3DVertex(p.pos + hor + ver, p.color, new v2f(1, 0));
                m_verts[idx + 2] = new S3DVertex(p.pos + hor - ver, p.color, new v2f(1, 1));
                m_verts[idx + 1] = new S3DVertex(p.pos - hor - ver, p.color, new v2f(0, 1));
                m_verts[idx + 0] = new S3DVertex(p.pos - hor + ver, p.color, new v2f(0, 0));
                int k = i * 6;
                m_indices[k] = idx; m_indices[k + 1] = idx + 2; m_indices[k + 2] = idx + 1;
                m_indices[k + 3] = idx; m_indices[k + 4] = idx + 3; m_indices[k + 5] = idx + 2;
                idx += 4;
            }
            matrix4 world = matrix4.Identity;
            if (!ParticlesAreGlobal) world.setTranslation(AbsoluteTransformation.getTranslation());
            Gfx.setTransform(E_TRANSFORMATION_STATE.ETS_WORLD, world);
            Gfx.drawVertexPrimitiveList(m_verts, ParticleCount * 4, m_indices, ParticleCount * 6, Material);
        }
    }

    public class CParticlePreset
    {
        public readonly string m_particleName;
        public readonly List<CFpsParticleSystemSceneNode> m_ParticleSystems = new List<CFpsParticleSystemSceneNode>();
        public CParticlePreset(string name) { m_particleName = name; }
    }

    public class ParticleSystem
    {
        ISceneNode m_node;
        float m_timeToLive = -1, m_lifeTime;

        public ParticleSystem(string typeName)
        {
            m_node = CSceneManager.Instance.addEmptySceneNode();
            LoadParticleSystem(typeName);
        }

        public ISceneNode getNode() { return m_node; }
        public void Update(float timestep) { m_lifeTime += timestep; }
        public void SetTimeToLive(float t) { m_timeToLive = t; }

        public void UnloadParticleSystem()
        {
            if (m_node != null) { m_node.removeAll(); m_node.remove(); m_node = null; }
        }

        public void LoadParticleSystem(string typeName)
        {
            m_node.removeAll();
            ParticleManager.SetObjectParticleType(m_node, typeName);
        }

        IEnumerable<CFpsParticleSystemSceneNode> Systems()
        {
            if (m_node == null) yield break;
            foreach (ISceneNode c in m_node.Children) if (c is CFpsParticleSystemSceneNode) yield return (CFpsParticleSystemSceneNode)c;
        }

        public bool IsAlive()
        {
            foreach (CFpsParticleSystemSceneNode ps in Systems()) if (!ps.IsEnded()) return true;
            return false;
        }

        public void SetPosition(v3f v) { if (m_node != null) m_node.setPosition(v); }
        public void SetScale(float scale) { foreach (var ps in Systems()) ps.SetSystemScale(scale); }
        public void SetRotation(quaternion q) { foreach (var ps in Systems()) ps.SetSystemRotation(q); }
        public void SetZBuffer(bool enabled) { foreach (var ps in Systems()) ps.Material.ZBuffer = enabled; }
        public void SetFogEnabled(bool enabled) { }
        public void Pause() { foreach (var ps in Systems()) ps.Pause(); }
        public void UnPause() { foreach (var ps in Systems()) ps.UnPause(); }
        public void Restart() { foreach (var ps in Systems()) ps.Restart(); }
        public void drop() { UnloadParticleSystem(); }
    }

    public static class ParticleManager
    {
        public static readonly List<ParticleSystem> m_particleSystemList = new List<ParticleSystem>();
        static readonly List<CParticlePreset> m_ParticlePresets = new List<CParticlePreset>();

        public static void ClearParticleSystem()
        {
            foreach (ParticleSystem p in m_particleSystemList) p.drop();
            m_particleSystemList.Clear();
        }

        public static ParticleSystem AddParticleSystem(string typeName)
        {
            ParticleSystem ps = new ParticleSystem(typeName);
            m_particleSystemList.Add(ps);
            return ps;
        }

        public static void UpdateParticleSystem(float timestep)
        {
            for (int i = 0; i < m_particleSystemList.Count;)
            {
                ParticleSystem p = m_particleSystemList[i];
                p.Update(timestep);
                if (!p.IsAlive()) { m_particleSystemList.RemoveAt(i); p.drop(); }
                else i++;
            }
        }

        public static bool IsParticleSystemAlive(ParticleSystem particle)
        {
            return m_particleSystemList.Contains(particle) && particle.IsAlive();
        }

        public static void RemoveParticleSystem(ParticleSystem particle)
        {
            if (m_particleSystemList.Remove(particle)) particle.drop();
        }

        static E_MATERIAL_TYPE MaterialFromName(string n)
        {
            switch (n)
            {
                case "trans_add": return E_MATERIAL_TYPE.EMT_TRANSPARENT_ADD_COLOR;
                case "trans_alphach": return E_MATERIAL_TYPE.EMT_TRANSPARENT_ALPHA_CHANNEL;
                case "trans_alphach_ref": return E_MATERIAL_TYPE.EMT_TRANSPARENT_ALPHA_CHANNEL_REF;
                case "solid": return E_MATERIAL_TYPE.EMT_SOLID;
                case "trans_vertex_alpha": return E_MATERIAL_TYPE.EMT_TRANSPARENT_VERTEX_ALPHA;
                case "trans_reflection_2layer": return E_MATERIAL_TYPE.EMT_TRANSPARENT_REFLECTION_2_LAYER;
                default: return E_MATERIAL_TYPE.EMT_TRANSPARENT_TEXTURE_VERTEX_ALPHA;
            }
        }

        public static int LoadParticlePresets()
        {
            m_ParticlePresets.Clear();
            IXMLReader xml = IXMLReader.FromFile("Effects/effects.xml");
            if (xml == null) return 1;
            while (xml.read())
            {
                if (xml.getNodeName() != "effect" || xml.getNodeType() != EXML_NODE.EXN_ELEMENT) continue;
                CParticlePreset preset = new CParticlePreset(xml.getAttributeValueSafe("name"));
                m_ParticlePresets.Add(preset);
                bool endReached = false;
                while (!endReached && xml.read())
                {
                    if (xml.getNodeType() == EXML_NODE.EXN_ELEMENT_END && xml.getNodeName() == "effect") endReached = true;
                    else if (xml.getNodeType() == EXML_NODE.EXN_ELEMENT && xml.getNodeName() == "ps")
                    {
                        bool psEnd = false;
                        while (!psEnd && xml.read())
                        {
                            if (xml.getNodeType() == EXML_NODE.EXN_ELEMENT_END && xml.getNodeName() == "ps") psEnd = true;
                            else if (xml.getNodeType() == EXML_NODE.EXN_ELEMENT && xml.getNodeName() == "attributes")
                            {
                                IAttributes attributes = new IAttributes();
                                ReadAllAttributes(xml, attributes);
                                CFpsParticleSystemSceneNode node = new CFpsParticleSystemSceneNode();
                                node.Material.Lighting = false;
                                node.Material.Textures[0] = TextureCache.getTexture("Effects/" + attributes.getAttributeAsString("Texture"), true, false);
                                node.Material.MaterialType = MaterialFromName(attributes.getAttributeAsString("MaterialType"));
                                node.Material.ZWriteEnable = false;
                                node.Material.BackfaceCulling = false;
                                node.deserializeAttributes(attributes);
                                node.IsVisible = false;
                                preset.m_ParticleSystems.Add(node);
                            }
                        }
                    }
                }
            }
            return 0;
        }

        /// <summary>IAttributes::read(xml, true) keeping duplicate names (Affector, StartTime(%)...).</summary>
        static void ReadAllAttributes(IXMLReader xml, IAttributes a)
        {
            if (xml.isEmptyElement()) return;
            while (xml.read())
            {
                EXML_NODE t = xml.getNodeType();
                if (t == EXML_NODE.EXN_ELEMENT_END && xml.getNodeName() == "attributes") break;
                if (t != EXML_NODE.EXN_ELEMENT) continue;
                string name = xml.getAttributeValue("name");
                if (name == null) continue;
                E_ATTRIBUTE_TYPE type;
                switch (xml.getNodeName())
                {
                    case "int": type = E_ATTRIBUTE_TYPE.EAT_INT; break;
                    case "float": type = E_ATTRIBUTE_TYPE.EAT_FLOAT; break;
                    case "bool": type = E_ATTRIBUTE_TYPE.EAT_BOOL; break;
                    case "color": type = E_ATTRIBUTE_TYPE.EAT_COLOR; break;
                    case "vector3d": type = E_ATTRIBUTE_TYPE.EAT_VECTOR3D; break;
                    case "enum": type = E_ATTRIBUTE_TYPE.EAT_ENUM; break;
                    default: type = E_ATTRIBUTE_TYPE.EAT_STRING; break;
                }
                a.addRaw(name, type, xml.getAttributeValueSafe("value"));
            }
        }

        public static void SetObjectParticleType(ISceneNode node, string particleType)
        {
            CParticlePreset preset = m_ParticlePresets.Find(p => p.m_particleName == particleType);
            if (preset == null) { Logs.warning("Unknown particle preset " + particleType); return; }
            foreach (CFpsParticleSystemSceneNode src in preset.m_ParticleSystems)
            {
                CFpsParticleSystemSceneNode pNode = src.CloneNode();
                pNode.IsVisible = true;
                pNode.Restart();
                node.addChild(pNode);
                foreach (IFpsParticleAffector a in pNode.AffectorList)
                {
                    if (a is CFpsParticleAttractionAffector) ((CFpsParticleAttractionAffector)a).Point = node.getAbsolutePosition() + ((CFpsParticleAttractionAffector)a).Point;
                    if (a is CFpsParticleRotationAffector) ((CFpsParticleRotationAffector)a).PivotPoint = node.getAbsolutePosition() + ((CFpsParticleRotationAffector)a).PivotPoint;
                }
            }
        }
    }
}
