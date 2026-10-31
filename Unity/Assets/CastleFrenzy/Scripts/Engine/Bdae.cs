// Reader for the game's BRES/Collada ("0,0,0,325") binary model files (.bdae).
// Layouts follow Lib/Irrlicht/tools/ColladaConverter/include/SDatabaseCollada.h:
// little-endian, 32-bit file offsets as pointers, 0 or 0xFFFFFFFF meaning null.
using System;
using System.Collections.Generic;
using System.Text;

namespace CastleFrenzy
{
    public class BdaeException : Exception { public BdaeException(string m) : base(m) { } }

    public class BdaeClip { public string Id; public int Start, End; }

    public class BdaeChannel { public string Source, Target; public int Type; public int Reserved0; }

    public class BdaeSampler
    {
        public int Interpolation; // 0 step, 1 linear
        public int[] Times;
        public float[] Values;
    }

    public class BdaeAnimation
    {
        public string Id;
        public List<BdaeChannel> Channels = new List<BdaeChannel>();
        public List<BdaeSampler> Samplers = new List<BdaeSampler>();
        /// <summary>Texture transform defaults (OffsetU, OffsetV, Rotate, ScaleU, ScaleV) for CT_EFFECT_TRANSFORM tracks.</summary>
        public float[] TextureDefaults;
    }

    public class BdaeEvent { public int TimeMs; public string[] Names; }

    public class BdaeCamera { public string Id; public bool Ortho; public float XFov, AspectRatio, ZNear, ZFar; public string Target; }

    public class BdaeImage { public string Id, Name, Path; public int Type; }

    public class BdaeTextureRef { public BdaeImage Image; public float OffsetU, OffsetV, RotateUV, RepeatU = 1, RepeatV = 1; }

    public class BdaeEffect
    {
        public string Id, Name;
        public SColor? Ambient, Diffuse, Specular, Emission;
        public List<BdaeTextureRef> DiffuseTextures, AmbientTextures;
        public float Shininess, Transparency = 1, SpecLevel;
        public bool TransparentIsTexture;
        public bool DoubleSided;
    }

    public class BdaeMaterial
    {
        public string Id, Name, EffectUrl;
        public BdaeEffect Effect;
        public bool IsAdditive;
        public SColor DiffuseColor;
        public BdaeImage DiffuseSurface, EnvmapSurface;
        public float EnvmapIntensity;
        public int RenderSet, RenderPriority;
        public bool BackfaceCulling, FrontfaceCulling;
    }

    public class BdaeMeshBuffer
    {
        public string Material;
        public int[] Indices;
        public int MinIndex, MaxIndex;
    }

    public class BdaeGeometry
    {
        public string Id, Name;
        public int Type; // 0 mesh, 1 spline
        public int VertexCount;
        public v3f[] Positions;
        public v2f[] UVs;
        public SColor[] Colors;
        public List<BdaeMeshBuffer> Buffers = new List<BdaeMeshBuffer>();
        public bool SplineClosed;
        public v3f[] SplinePoints;
        public aabbox3df Box;
    }

    public class BdaeSkin
    {
        public string Source;
        public matrix4 BindShapeMatrix;
        public string[] Joints;
        public matrix4[] BindPoses;
        public float[] Weights;
        public byte[] VCount;
        public ushort[] V; // pairs (joint, weightIndex)
    }

    public class BdaeMorph
    {
        public string Source;
        public uint Method;
        public string[] Targets;
        public float[] Weights;
        public bool Relative { get { return Method == 1; } }
    }

    public class BdaeController
    {
        public int Type; // 0 skin, 1 morph
        public string Id;
        public BdaeSkin Skin;
        public BdaeMorph Morph;
    }

    public class BdaeInstanceMaterial { public string Symbol, Target; }

    public class BdaeInstance
    {
        public int Type; // SInstance::InstanceType
        public string Url;
        public BdaeGeometry Geometry;
        public BdaeController Controller;
        public BdaeCamera Camera;
        public List<BdaeInstanceMaterial> Materials = new List<BdaeInstanceMaterial>();
        public int DrawLayer;
        public string[] Skeletons;
    }

    public class BdaeNode
    {
        public string Id, Name, Sid;
        public v3f Position;
        public quaternion Rotation;
        public v3f Scale;
        public bool Visible;
        public List<BdaeNode> Children = new List<BdaeNode>();
        public List<BdaeInstance> Instances = new List<BdaeInstance>();
        public bool HasBillboard;
        public int BillboardType, BillboardOrientation;
    }

    public class BdaeVisualScene { public string Id, Name; public List<BdaeNode> Nodes = new List<BdaeNode>(); }

    public class BdaeFile
    {
        public const int IT_Animation = 0, IT_Camera = 1, IT_Controller = 2, IT_Geometry = 3, IT_Light = 4, IT_Node = 5, IT_VisualScene = 6;

        public string Path;
        public int UpAxis, SamplingRate;
        public List<BdaeClip> Clips = new List<BdaeClip>();
        public List<BdaeAnimation> Animations = new List<BdaeAnimation>();
        public List<BdaeEvent> Events = new List<BdaeEvent>();
        public List<BdaeCamera> Cameras = new List<BdaeCamera>();
        public List<BdaeImage> Images = new List<BdaeImage>();
        public List<BdaeEffect> Effects = new List<BdaeEffect>();
        public List<BdaeMaterial> Materials = new List<BdaeMaterial>();
        public List<BdaeGeometry> Geometries = new List<BdaeGeometry>();
        public List<BdaeController> Controllers = new List<BdaeController>();
        public List<BdaeVisualScene> VisualScenes = new List<BdaeVisualScene>();

        byte[] d;
        readonly Dictionary<uint, BdaeImage> m_imageByOffset = new Dictionary<uint, BdaeImage>();
        readonly Dictionary<uint, BdaeGeometry> m_geomByOffset = new Dictionary<uint, BdaeGeometry>();
        readonly Dictionary<uint, BdaeController> m_ctrlByOffset = new Dictionary<uint, BdaeController>();
        readonly Dictionary<uint, BdaeCamera> m_camByOffset = new Dictionary<uint, BdaeCamera>();
        readonly Dictionary<uint, BdaeEffect> m_effectByOffset = new Dictionary<uint, BdaeEffect>();

        static readonly Dictionary<string, BdaeFile> s_cache = new Dictionary<string, BdaeFile>();

        /// <summary>Loads (and caches) a .bdae from the game data folder.</summary>
        public static BdaeFile Load(string relativePath)
        {
            string key = relativePath.Replace('\\', '/').ToLowerInvariant();
            BdaeFile f;
            if (s_cache.TryGetValue(key, out f)) return f;
            byte[] bytes = GameFiles.ReadAllBytes(relativePath);
            if (bytes == null) throw new BdaeException("Missing model " + relativePath);
            f = new BdaeFile(relativePath, bytes);
            s_cache[key] = f;
            return f;
        }

        public static void ClearCache() { s_cache.Clear(); }

        public BdaeFile(string path, byte[] data)
        {
            Path = path;
            d = data;
            if (d.Length < 32 || d[0] != 'B' || d[1] != 'R' || d[2] != 'E' || d[3] != 'S')
                throw new BdaeException("Not a BRES file: " + path);
            uint root = U(28);
            string version = Str(root);
            if (version != "0,0,0,325") throw new BdaeException("Unsupported Collada layout " + version + " in " + path);
            UpAxis = I(root + 4);
            SamplingRate = I(root + 12);

            // Images first (referenced by materials/effects)
            foreach (uint p in Vec(root + 52, 20))
            {
                BdaeImage img = new BdaeImage { Id = Str(p), Name = Str(p + 4), Path = Str(p + 8), Type = I(p + 12) };
                Images.Add(img);
                m_imageByOffset[p] = img;
            }
            foreach (uint p in Vec(root + 36, 28))
            {
                BdaeCamera c = new BdaeCamera
                {
                    Id = Str(p), Ortho = I(p + 4) != 0, XFov = F(p + 8), AspectRatio = F(p + 12),
                    ZNear = F(p + 16), ZFar = F(p + 20), Target = Str(p + 24)
                };
                Cameras.Add(c);
                m_camByOffset[p] = c;
            }
            foreach (uint p in Vec(root + 60, 92)) { BdaeEffect e = ReadEffect(p); Effects.Add(e); m_effectByOffset[p] = e; }
            foreach (uint p in Vec(root + 68, 64)) Materials.Add(ReadMaterial(p));
            foreach (uint p in Vec(root + 76, 16)) { BdaeGeometry g = ReadGeometry(p); Geometries.Add(g); m_geomByOffset[p] = g; }
            foreach (uint p in Vec(root + 84, 12)) { BdaeController c = ReadController(p); Controllers.Add(c); m_ctrlByOffset[p] = c; }

            foreach (uint p in Vec(root + 28, 12))
                Clips.Add(new BdaeClip { Id = Str(p), Start = I(p + 4), End = I(p + 8) });
            foreach (uint p in Vec(root + 16, 36)) Animations.Add(ReadAnimation(p));
            uint ev = U(root + 24);
            if (!IsNull(ev))
            {
                int[] times = ReadSourceInts(ev);
                List<uint> events = Vec(ev + 12, 8);
                for (int k = 0; k < events.Count && k < times.Length; k++)
                {
                    List<uint> names = Vec(events[k], 4);
                    string[] arr = new string[names.Count];
                    for (int n = 0; n < names.Count; n++) arr[n] = Str(names[n]);
                    Events.Add(new BdaeEvent { TimeMs = times[k], Names = arr });
                }
            }
            foreach (uint p in Vec(root + 108, 16))
            {
                BdaeVisualScene vs = new BdaeVisualScene { Id = Str(p), Name = Str(p + 4) };
                foreach (uint n in Vec(p + 8, 80)) vs.Nodes.Add(ReadNode(n, 0));
                VisualScenes.Add(vs);
            }
            foreach (BdaeMaterial m in Materials)
            {
                string fx = m.EffectUrl != null && m.EffectUrl.StartsWith("#") ? m.EffectUrl.Substring(1) : m.EffectUrl;
                if (m.Effect == null) m.Effect = Effects.Find(e => e.Id == fx);
            }
            d = null; // release raw data
        }

        // ------------------------------------------------------------------ primitives

        static bool IsNull(uint p) { return p == 0 || p == 0xFFFFFFFF; }
        uint U(uint o) { return BitConverter.ToUInt32(d, (int)o); }
        int I(uint o) { return BitConverter.ToInt32(d, (int)o); }
        float F(uint o) { return BitConverter.ToSingle(d, (int)o); }
        short S16(uint o) { return BitConverter.ToInt16(d, (int)o); }

        string Str(uint field)
        {
            uint p = U(field);
            if (IsNull(p) || p >= d.Length) return "";
            int end = (int)p;
            while (end < d.Length && d[end] != 0) end++;
            return Encoding.UTF8.GetString(d, (int)p, end - (int)p);
        }

        string CStr(uint p)
        {
            if (IsNull(p) || p >= d.Length) return "";
            int end = (int)p;
            while (end < d.Length && d[end] != 0) end++;
            return Encoding.UTF8.GetString(d, (int)p, end - (int)p);
        }

        List<uint> Vec(uint field, int stride)
        {
            uint count = U(field), ptr = U(field + 4);
            List<uint> r = new List<uint>((int)Math.Min(count, 100000));
            if (count == 0 || IsNull(ptr)) return r;
            if (count > 10000000 || ptr + count * (uint)stride > d.Length) throw new BdaeException("Invalid vector in " + Path);
            for (uint k = 0; k < count; k++) r.Add(ptr + k * (uint)stride);
            return r;
        }

        int ComponentSize(int c) { switch (c) { case 0: case 1: return 1; case 2: case 3: return 2; default: return 4; } }

        float ReadComponent(int c, uint o)
        {
            switch (c)
            {
                case 0: return (sbyte)d[o];
                case 1: return d[o];
                case 2: return BitConverter.ToInt16(d, (int)o);
                case 3: return BitConverter.ToUInt16(d, (int)o);
                case 4: return BitConverter.ToInt32(d, (int)o);
                case 5: return BitConverter.ToUInt32(d, (int)o);
                default: return BitConverter.ToSingle(d, (int)o);
            }
        }

        float[] ReadSourceFloats(uint src)
        {
            int c = I(src);
            uint count = U(src + 4), ptr = U(src + 8);
            float[] r = new float[count];
            int sz = ComponentSize(c);
            for (uint k = 0; k < count; k++) r[k] = ReadComponent(c, ptr + k * (uint)sz);
            return r;
        }

        int[] ReadSourceInts(uint src)
        {
            int c = I(src);
            uint count = U(src + 4), ptr = U(src + 8);
            int[] r = new int[count];
            int sz = ComponentSize(c);
            for (uint k = 0; k < count; k++) r[k] = (int)ReadComponent(c, ptr + k * (uint)sz);
            return r;
        }

        SColor ReadColor(uint p) { return new SColor(d[p + 3], d[p], d[p + 1], d[p + 2]); }

        // ------------------------------------------------------------------ records

        /// <summary>Image references are either library indices or (unrelocated) offsets.</summary>
        BdaeImage ImageRef(uint v)
        {
            if (v == 0xFFFFFFFF) return null;
            if (v < Images.Count) return Images[(int)v];
            BdaeImage img;
            return m_imageByOffset.TryGetValue(v, out img) ? img : null;
        }

        void ReadColorOrTexture(uint field, out SColor? color, out List<BdaeTextureRef> textures, int[] samplers, int[] surfaces)
        {
            color = null; textures = null;
            byte type = d[field];
            uint ptr = U(field + 4);
            if (IsNull(ptr)) return;
            if (type == 0) { color = ReadColor(ptr); return; }
            textures = new List<BdaeTextureRef>();
            foreach (uint t in Vec(ptr, 28))
            {
                BdaeTextureRef tr = new BdaeTextureRef
                {
                    OffsetU = F(t + 8), OffsetV = F(t + 12), RotateUV = F(t + 16), RepeatU = F(t + 20), RepeatV = F(t + 24)
                };
                // STexture.texture -> SEffect.samplers[i].surface -> SEffect.surfaces[j].image -> library image
                int sampler = I(t);
                if (sampler >= 0 && sampler < samplers.Length)
                {
                    int surface = samplers[sampler];
                    if (surface >= 0 && surface < surfaces.Length) tr.Image = ImageRef((uint)surfaces[surface]);
                }
                textures.Add(tr);
            }
        }

        BdaeEffect ReadEffect(uint p)
        {
            BdaeEffect e = new BdaeEffect { Id = Str(p), Name = Str(p + 4) };
            List<uint> sv = Vec(p + 76, 4), fv = Vec(p + 84, 4);
            int[] samplers = new int[sv.Count], surfaces = new int[fv.Count];
            for (int k = 0; k < sv.Count; k++) samplers[k] = I(sv[k]);
            for (int k = 0; k < fv.Count; k++) surfaces[k] = I(fv[k]);
            List<BdaeTextureRef> tmp;
            ReadColorOrTexture(p + 8, out e.Ambient, out e.AmbientTextures, samplers, surfaces);
            ReadColorOrTexture(p + 16, out e.Diffuse, out e.DiffuseTextures, samplers, surfaces);
            ReadColorOrTexture(p + 24, out e.Specular, out tmp, samplers, surfaces);
            ReadColorOrTexture(p + 32, out e.Emission, out tmp, samplers, surfaces);
            e.Shininess = F(p + 40);
            e.TransparentIsTexture = d[p + 56] == 1;
            e.Transparency = F(p + 64);
            e.SpecLevel = F(p + 68);
            e.DoubleSided = I(p + 72) != 0;
            return e;
        }

        BdaeMaterial ReadMaterial(uint p)
        {
            BdaeMaterial m = new BdaeMaterial
            {
                Id = Str(p), Name = Str(p + 4), EffectUrl = Str(p + 12),
                IsAdditive = I(p + 28) != 0, DiffuseColor = ReadColor(p + 32),
                EnvmapIntensity = F(p + 44), RenderSet = I(p + 48), RenderPriority = I(p + 52),
                BackfaceCulling = I(p + 56) != 0, FrontfaceCulling = I(p + 60) != 0
            };
            uint fx = U(p + 24);
            BdaeEffect eff;
            if (m_effectByOffset.TryGetValue(fx, out eff)) m.Effect = eff;
            m.DiffuseSurface = ImageRef(U(p + 36));
            m.EnvmapSurface = ImageRef(U(p + 40));
            return m;
        }

        BdaeGeometry ReadGeometry(uint p)
        {
            BdaeGeometry g = new BdaeGeometry { Id = Str(p), Name = Str(p + 4), Type = I(p + 8) };
            uint ptr = U(p + 12);
            if (IsNull(ptr)) return g;
            if (g.Type == 1)
            {
                g.SplineClosed = I(ptr) != 0;
                float[] pos = ReadSourceFloats(ptr + 4);
                g.SplinePoints = new v3f[pos.Length / 3];
                for (int k = 0; k < g.SplinePoints.Length; k++) g.SplinePoints[k] = new v3f(pos[k * 3], pos[k * 3 + 1], pos[k * 3 + 2]);
                return g;
            }
            uint mesh = ptr;
            if (I(mesh) != 1) throw new BdaeException("Only interleaved meshes are supported: " + Path);
            int count = I(mesh + 4);
            uint inter = U(mesh + 8);
            int stride = I(inter);
            List<uint> offs = Vec(inter + 4, 4), types = Vec(inter + 12, 4), scales = Vec(inter + 20, 4);
            uint data = U(inter + 28);
            g.VertexCount = count;
            List<uint> bufs = Vec(mesh + 12, 60);
            int posSrc = -1, uvSrc = -1, colSrc = -1;
            foreach (uint b in bufs)
            {
                if (I(b) != 0) throw new BdaeException("Only triangle lists are supported: " + Path);
                sbyte pS = (sbyte)d[b + 12], uS = (sbyte)d[b + 16], cS = (sbyte)d[b + 22];
                if (posSrc < 0) posSrc = pS;
                if (uvSrc < 0) uvSrc = uS;
                if (colSrc < 0) colSrc = cS;
                List<uint> idx = Vec(b + 24, 2);
                int[] indices = new int[idx.Count];
                for (int k = 0; k < idx.Count; k++) indices[k] = (ushort)S16(idx[k]);
                g.Buffers.Add(new BdaeMeshBuffer { Material = Str(b + 4), Indices = indices, MinIndex = (ushort)S16(b + 32), MaxIndex = (ushort)S16(b + 34) });
            }
            Func<int, int, float[][]> attribute = (source, width) =>
            {
                if (source < 0) return null;
                int comp = I(types[source]) & 15;
                int csize = ComponentSize(comp);
                int off = I(offs[source]);
                uint sc = U(scales[source]);
                float[] scale = null;
                if (!IsNull(sc)) { scale = new float[6]; for (int k = 0; k < 6; k++) scale[k] = F(sc + (uint)(k * 4)); }
                float[][] r = new float[count][];
                for (int v = 0; v < count; v++)
                {
                    float[] e = new float[width];
                    uint basePtr = data + (uint)(v * stride + off);
                    for (int c = 0; c < width; c++)
                    {
                        float x = ReadComponent(comp, basePtr + (uint)(c * csize));
                        if (scale != null && c < 3) x = x * scale[c] + scale[c + 3];
                        e[c] = x;
                    }
                    r[v] = e;
                }
                return r;
            };
            float[][] pos3 = attribute(posSrc, 3);
            if (pos3 != null)
            {
                g.Positions = new v3f[count];
                for (int v = 0; v < count; v++) g.Positions[v] = new v3f(pos3[v][0], pos3[v][1], pos3[v][2]);
                g.Box = new aabbox3df(g.Positions.Length > 0 ? g.Positions[0] : v3f.Zero);
                foreach (v3f q in g.Positions) g.Box.addInternalPoint(q);
            }
            float[][] uv2 = attribute(uvSrc, 2);
            if (uv2 != null)
            {
                g.UVs = new v2f[count];
                for (int v = 0; v < count; v++) g.UVs[v] = new v2f(uv2[v][0], uv2[v][1]);
            }
            float[][] c4 = attribute(colSrc, 4);
            if (c4 != null)
            {
                g.Colors = new SColor[count];
                for (int v = 0; v < count; v++) g.Colors[v] = new SColor((int)c4[v][3], (int)c4[v][0], (int)c4[v][1], (int)c4[v][2]);
            }
            return g;
        }

        BdaeController ReadController(uint p)
        {
            BdaeController c = new BdaeController { Type = I(p), Id = Str(p + 4) };
            uint ptr = U(p + 8);
            if (IsNull(ptr)) return c;
            if (c.Type == 0)
            {
                BdaeSkin s = new BdaeSkin { Source = Str(ptr) };
                s.BindShapeMatrix = ReadMatrix(ptr + 4);
                List<uint> joints = Vec(ptr + 68, 4);
                s.Joints = new string[joints.Count];
                for (int k = 0; k < joints.Count; k++) s.Joints[k] = Str(joints[k]);
                List<uint> bp = Vec(ptr + 76, 4);
                s.BindPoses = new matrix4[bp.Count / 16];
                for (int k = 0; k < s.BindPoses.Length; k++) s.BindPoses[k] = ReadMatrix(bp[k * 16]);
                List<uint> w = Vec(ptr + 84, 4);
                s.Weights = new float[w.Count];
                for (int k = 0; k < w.Count; k++) s.Weights[k] = F(w[k]);
                List<uint> vc = Vec(ptr + 92, 1);
                s.VCount = new byte[vc.Count];
                for (int k = 0; k < vc.Count; k++) s.VCount[k] = d[vc[k]];
                List<uint> vv = Vec(ptr + 100, 2);
                s.V = new ushort[vv.Count];
                for (int k = 0; k < vv.Count; k++) s.V[k] = (ushort)S16(vv[k]);
                c.Skin = s;
            }
            else if (c.Type == 1)
            {
                BdaeMorph m = new BdaeMorph { Source = Str(ptr), Method = U(ptr + 4) };
                List<uint> t = Vec(ptr + 8, 4);
                m.Targets = new string[t.Count];
                for (int k = 0; k < t.Count; k++) m.Targets[k] = Str(t[k]);
                List<uint> w = Vec(ptr + 24, 4);
                m.Weights = new float[w.Count];
                for (int k = 0; k < w.Count; k++) m.Weights[k] = F(w[k]);
                c.Morph = m;
            }
            return c;
        }

        matrix4 ReadMatrix(uint p)
        {
            matrix4 m = matrix4.Identity;
            for (int k = 0; k < 16; k++) m[k] = F(p + (uint)(k * 4));
            return m;
        }

        BdaeAnimation ReadAnimation(uint p)
        {
            BdaeAnimation a = new BdaeAnimation { Id = Str(p) };
            List<uint> sources = Vec(p + 4, 12);
            foreach (uint sp in Vec(p + 12, 20))
            {
                int interp = I(sp), input = I(sp + 4), output = I(sp + 8);
                BdaeSampler s = new BdaeSampler { Interpolation = interp };
                if (input >= 0 && input < sources.Count) s.Times = ReadSourceInts(sources[input]);
                if (output >= 0 && output < sources.Count) s.Values = ReadSourceFloats(sources[output]);
                if (s.Times == null) s.Times = new int[0];
                if (s.Values == null) s.Values = new float[0];
                a.Samplers.Add(s);
            }
            foreach (uint cp in Vec(p + 20, 16))
                a.Channels.Add(new BdaeChannel { Source = Str(cp), Target = Str(cp + 4), Type = I(cp + 8), Reserved0 = d[cp + 12] });
            if (a.Channels.Count > 0 && (a.Channels[0].Type & 0x100) != 0)
            {
                uint extra = U(p + 32);
                a.TextureDefaults = new float[] { 0, 0, 0, 1, 1 };
                if (!IsNull(extra)) for (int k = 0; k < 5; k++) a.TextureDefaults[k] = F(extra + (uint)(k * 4));
            }
            return a;
        }

        BdaeNode ReadNode(uint p, int depth)
        {
            if (depth > 100) throw new BdaeException("Scene hierarchy too deep in " + Path);
            BdaeNode n = new BdaeNode
            {
                Id = Str(p), Name = Str(p + 4), Sid = Str(p + 8),
                Position = new v3f(F(p + 12), F(p + 16), F(p + 20)),
                Rotation = new quaternion(F(p + 24), F(p + 28), F(p + 32), F(p + 36)),
                Scale = new v3f(F(p + 40), F(p + 44), F(p + 48)),
                Visible = I(p + 52) != 0
            };
            foreach (uint ip in Vec(p + 64, 8))
            {
                int type = I(ip);
                uint data = U(ip + 4);
                BdaeInstance inst = new BdaeInstance { Type = type };
                if (!IsNull(data))
                {
                    inst.Url = Str(data + 4);
                    switch (type)
                    {
                        case IT_Geometry:
                        {
                            BdaeGeometry g; m_geomByOffset.TryGetValue(U(data + 8), out g);
                            if (g == null) g = Geometries.Find(x => "#" + x.Id == inst.Url);
                            inst.Geometry = g;
                            ReadBindMaterial(data + 12, inst);
                            inst.DrawLayer = I(data + 20);
                            break;
                        }
                        case IT_Controller:
                        {
                            BdaeController c; m_ctrlByOffset.TryGetValue(U(data + 8), out c);
                            if (c == null) c = Controllers.Find(x => "#" + x.Id == inst.Url);
                            inst.Controller = c;
                            ReadBindMaterial(data + 12, inst);
                            inst.DrawLayer = I(data + 20);
                            List<uint> sk = Vec(data + 24, 4);
                            inst.Skeletons = new string[sk.Count];
                            for (int k = 0; k < sk.Count; k++) inst.Skeletons[k] = Str(sk[k]);
                            break;
                        }
                        case IT_Camera:
                        {
                            BdaeCamera c; m_camByOffset.TryGetValue(U(data + 8), out c);
                            if (c == null) c = Cameras.Find(x => "#" + x.Id == inst.Url);
                            inst.Camera = c;
                            break;
                        }
                    }
                }
                n.Instances.Add(inst);
            }
            uint bb = U(p + 76);
            if (!IsNull(bb))
            {
                n.HasBillboard = true;
                n.BillboardType = I(bb);
                n.BillboardOrientation = I(bb + 4);
            }
            foreach (uint c in Vec(p + 56, 80)) n.Children.Add(ReadNode(c, depth + 1));
            return n;
        }

        void ReadBindMaterial(uint field, BdaeInstance inst)
        {
            foreach (uint m in Vec(field, 20))
            {
                string target = Str(m + 4);
                if (target.StartsWith("#")) target = target.Substring(1);
                inst.Materials.Add(new BdaeInstanceMaterial { Symbol = Str(m), Target = target });
            }
        }

        public BdaeMaterial FindMaterial(string id)
        {
            foreach (BdaeMaterial m in Materials) if (m.Id == id) return m;
            return null;
        }

        public BdaeGeometry FindGeometry(string url)
        {
            string id = url.StartsWith("#") ? url.Substring(1) : url;
            foreach (BdaeGeometry g in Geometries) if (g.Id == id) return g;
            return null;
        }

        public BdaeController FindController(string url)
        {
            string id = url.StartsWith("#") ? url.Substring(1) : url;
            foreach (BdaeController c in Controllers) if (c.Id == id) return c;
            return null;
        }
    }
}
