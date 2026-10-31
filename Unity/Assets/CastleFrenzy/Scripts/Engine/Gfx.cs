// Immediate-mode renderer replacing Irrlicht's IVideoDriver.
// Every draw call of a frame is appended, in call order, to one CommandBuffer that the
// game camera executes, so 2D/3D interleaving behaves exactly like the original engine.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CastleFrenzy
{
    public enum E_MATERIAL_TYPE
    {
        EMT_SOLID,
        EMT_TRANSPARENT_ALPHA_CHANNEL,
        EMT_TRANSPARENT_ALPHA_CHANNEL_REF,
        EMT_TRANSPARENT_ADD_COLOR,
        EMT_TRANSPARENT_VERTEX_ALPHA,
        EMT_TRANSPARENT_TEXTURE_VERTEX_ALPHA,
        EMT_A8I8_GRADIENT,
        EMT_LIGHTMAP,
        EMT_2D_ALPHA,
        EMT_TRANSPARENT_REFLECTION_2_LAYER,
    }

    public enum E_MATERIAL_FLAG
    {
        EMF_LIGHTING,
        EMF_ZBUFFER,
        EMF_ZWRITE_ENABLE,
        EMF_BACK_FACE_CULLING,
        EMF_FRONT_FACE_CULLING,
        EMF_NORMALIZE_NORMALS,
        EMF_2D_USE_VERTEX_ALPHA,
        EMF_2D_USE_TEXTURE_ALPHA,
        EMF_BILINEAR_FILTER,
    }

    public enum E_TRANSFORMATION_STATE { ETS_VIEW, ETS_WORLD, ETS_PROJECTION }

    /// <summary>irr::video::SMaterial with the fields the game and the Collada loader use.</summary>
    public class SMaterial
    {
        public E_MATERIAL_TYPE MaterialType = E_MATERIAL_TYPE.EMT_SOLID;
        public ITexture[] Textures = new ITexture[2];
        public matrix4 TextureMatrix = matrix4.Identity;
        public SColor AmbientColor = new SColor(255, 255, 255, 255);
        public SColor DiffuseColor = new SColor(255, 255, 255, 255);
        public SColor EmissiveColor = new SColor(0, 0, 0, 0);
        public SColor SpecularColor = new SColor(255, 255, 255, 255);
        public float Shininess;
        public bool Lighting = true;
        public bool ZBuffer = true;
        public bool ZWriteEnable = true;
        public bool BackfaceCulling = true;
        public bool FrontfaceCulling = false;
        public bool UseVertexAlpha2D;
        public bool UseTextureAlpha2D;
        /// <summary>Rendering priority used by the Collada scenes (0 = close .. 255 = far).</summary>
        public int RenderPriority;

        public SMaterial Clone()
        {
            SMaterial m = (SMaterial)MemberwiseClone();
            m.Textures = (ITexture[])Textures.Clone();
            return m;
        }

        public void setFlag(E_MATERIAL_FLAG flag, bool value)
        {
            switch (flag)
            {
                case E_MATERIAL_FLAG.EMF_LIGHTING: Lighting = value; break;
                case E_MATERIAL_FLAG.EMF_ZBUFFER: ZBuffer = value; break;
                case E_MATERIAL_FLAG.EMF_ZWRITE_ENABLE: ZWriteEnable = value; break;
                case E_MATERIAL_FLAG.EMF_BACK_FACE_CULLING: BackfaceCulling = value; break;
                case E_MATERIAL_FLAG.EMF_FRONT_FACE_CULLING: FrontfaceCulling = value; break;
                case E_MATERIAL_FLAG.EMF_2D_USE_VERTEX_ALPHA: UseVertexAlpha2D = value; break;
                case E_MATERIAL_FLAG.EMF_2D_USE_TEXTURE_ALPHA: UseTextureAlpha2D = value; break;
            }
        }

        public bool getFlag(E_MATERIAL_FLAG flag)
        {
            switch (flag)
            {
                case E_MATERIAL_FLAG.EMF_LIGHTING: return Lighting;
                case E_MATERIAL_FLAG.EMF_ZBUFFER: return ZBuffer;
                case E_MATERIAL_FLAG.EMF_ZWRITE_ENABLE: return ZWriteEnable;
                case E_MATERIAL_FLAG.EMF_BACK_FACE_CULLING: return BackfaceCulling;
                case E_MATERIAL_FLAG.EMF_FRONT_FACE_CULLING: return FrontfaceCulling;
                case E_MATERIAL_FLAG.EMF_2D_USE_VERTEX_ALPHA: return UseVertexAlpha2D;
                case E_MATERIAL_FLAG.EMF_2D_USE_TEXTURE_ALPHA: return UseTextureAlpha2D;
            }
            return false;
        }

        public void setTexture(int i, ITexture t) { Textures[i] = t; }
        public ITexture getTexture(int i) { return Textures[i]; }
        public void setMaterialType(E_MATERIAL_TYPE t) { MaterialType = t; }
        public E_MATERIAL_TYPE getMaterialType() { return MaterialType; }
        public void setEmissiveColor(SColor c) { EmissiveColor = c; }
        public SColor getEmissiveColor() { return EmissiveColor; }
        public void setDiffuseColor(SColor c) { DiffuseColor = c; }
        public SColor getDiffuseColor() { return DiffuseColor; }
        public void setAmbientColor(SColor c) { AmbientColor = c; }
        public SColor getAmbientColor() { return AmbientColor; }
        public void setSpecularColor(SColor c) { SpecularColor = c; }
        public void setShininess(float s) { Shininess = s; }
        public void setTextureMatrix(int i, matrix4 m) { if (i == 0) TextureMatrix = m; }
        public matrix4 getTextureMatrix(int i) { return TextureMatrix; }

        public bool isTransparent()
        {
            return MaterialType == E_MATERIAL_TYPE.EMT_TRANSPARENT_ALPHA_CHANNEL ||
                   MaterialType == E_MATERIAL_TYPE.EMT_TRANSPARENT_ADD_COLOR ||
                   MaterialType == E_MATERIAL_TYPE.EMT_TRANSPARENT_VERTEX_ALPHA ||
                   MaterialType == E_MATERIAL_TYPE.EMT_TRANSPARENT_TEXTURE_VERTEX_ALPHA ||
                   MaterialType == E_MATERIAL_TYPE.EMT_TRANSPARENT_REFLECTION_2_LAYER ||
                   MaterialType == E_MATERIAL_TYPE.EMT_A8I8_GRADIENT ||
                   MaterialType == E_MATERIAL_TYPE.EMT_2D_ALPHA;
        }
    }

    /// <summary>A vertex for drawVertexPrimitiveList (irr::video::S3DVertex).</summary>
    public struct S3DVertex
    {
        public v3f Pos;
        public SColor Color;
        public v2f TCoords;
        public S3DVertex(v3f pos, SColor color, v2f uv) { Pos = pos; Color = color; TCoords = uv; }
        public S3DVertex(float x, float y, float z, SColor color, float u, float v) { Pos = new v3f(x, y, z); Color = color; TCoords = new v2f(u, v); }
    }

    public static class Gfx
    {
        public const int SCREEN_W = 480;
        public const int SCREEN_H = 320;

        // Unity's front faces are clockwise on screen; the engine's (glFrontFace(GL_CCW)) are counter-clockwise.
        public static bool SwapCulling = true;

        static CommandBuffer s_cmd;
        static Camera s_camera;
        static Shader s_spriteShader, s_meshShader;
        static readonly Dictionary<int, Material> s_spriteMaterials = new Dictionary<int, Material>();
        static readonly Dictionary<int, Material> s_meshMaterials = new Dictionary<int, Material>();
        static readonly List<Mesh> s_meshPool = new List<Mesh>();
        static int s_meshPoolUsed;
        static readonly List<MaterialPropertyBlock> s_propPool = new List<MaterialPropertyBlock>();
        static int s_propPoolUsed;

        // Transforms (Irrlicht conventions)
        static matrix4 s_world = matrix4.Identity, s_view = matrix4.Identity, s_proj = matrix4.Identity;
        static matrix4 s_world3DBackup, s_view3DBackup, s_proj3DBackup;
        static bool s_in2D;
        static bool s_vpDirty = true;
        static r2i s_viewport = new r2i(0, 0, SCREEN_W, SCREEN_H);
        static Rect s_pixelRect;

        // Global lighting (single ambient scene light, see CPlayfield)
        public static SColorf LightAmbient = new SColorf(1, 1, 1, 1);
        public static bool LightingEnabled = true;

        // 2D batch
        static readonly List<Vector3> s_bv = new List<Vector3>(4096);
        static readonly List<Vector2> s_buv = new List<Vector2>(4096);
        static readonly List<Color32> s_bc = new List<Color32>(4096);
        static readonly List<int> s_bi = new List<int>(6144);
        static ITexture s_bTex;
        static int s_bKey = -1;
        static SColor s_bColor2;
        static bool s_bEmissive;
        static SColor s_bEmissiveColor;

        // Current 2D material (set2DMaterial / set2DTexture)
        static ITexture s_2dTex;
        static E_MATERIAL_TYPE s_2dType = E_MATERIAL_TYPE.EMT_2D_ALPHA;
        static SColor s_2dColor2;

        public static int DrawCalls { get; private set; }
        public static int Primitives { get; private set; }

        public static CommandBuffer Cmd { get { return s_cmd; } }
        public static bool In2D { get { return s_in2D; } }

        public static void Init(Camera cam)
        {
            s_camera = cam;
            s_spriteShader = Resources.Load<Shader>("Shaders/CF_Sprite");
            s_meshShader = Resources.Load<Shader>("Shaders/CF_Mesh");
            if (s_spriteShader == null) s_spriteShader = Shader.Find("CastleFrenzy/Sprite");
            if (s_meshShader == null) s_meshShader = Shader.Find("CastleFrenzy/Mesh");
            if (s_cmd == null)
            {
                s_cmd = new CommandBuffer { name = "CastleFrenzy" };
            }
            cam.RemoveAllCommandBuffers();
            cam.AddCommandBuffer(CameraEvent.AfterEverything, s_cmd);
        }

        /// <summary>Pixel rectangle of the 3:2 game area inside the window.</summary>
        public static Rect PixelRect { get { return s_pixelRect; } }

        public static Rect ComputeGameRect(int w, int h)
        {
            float target = (float)SCREEN_W / SCREEN_H;
            float aspect = (float)w / h;
            if (aspect > target)
            {
                float gw = h * target;
                return new Rect((w - gw) * 0.5f, 0, gw, h);
            }
            float gh = w / target;
            return new Rect(0, (h - gh) * 0.5f, w, gh);
        }

        /// <summary>Render target size used instead of the window size (editor previews render into a RenderTexture).</summary>
        public static Vector2Int? TargetSize;

        public static void BeginFrame()
        {
            s_cmd.Clear();
            s_meshPoolUsed = 0;
            s_propPoolUsed = 0;
            DrawCalls = 0;
            Primitives = 0;
            s_pixelRect = TargetSize.HasValue ? ComputeGameRect(TargetSize.Value.x, TargetSize.Value.y) : ComputeGameRect(Screen.width, Screen.height);
            s_in2D = false;
            s_world = matrix4.Identity;
            s_view = matrix4.Identity;
            s_proj = matrix4.Identity;
            s_viewport = new r2i(0, 0, SCREEN_W, SCREEN_H);
            s_cmd.SetViewport(s_pixelRect);
            s_cmd.ClearRenderTarget(true, true, Color.black);
            s_vpDirty = true;
            ResetBatch();
        }

        /// <summary>Clears the depth buffer (used before drawing a second scene pass, e.g. the help book models).</summary>
        public static void clearZBuffer()
        {
            Flush();
            s_cmd.ClearRenderTarget(true, false, Color.black);
        }

        public static void EndFrame()
        {
            Flush();
            if (s_in2D) endScene2D();
        }

        // ------------------------------------------------------------------ transforms

        public static void setTransform(E_TRANSFORMATION_STATE state, matrix4 mat)
        {
            switch (state)
            {
                case E_TRANSFORMATION_STATE.ETS_WORLD: s_world = mat; break;
                case E_TRANSFORMATION_STATE.ETS_VIEW: Flush(); s_view = mat; s_vpDirty = true; break;
                case E_TRANSFORMATION_STATE.ETS_PROJECTION: Flush(); s_proj = mat; s_vpDirty = true; break;
            }
        }

        public static matrix4 getTransform(E_TRANSFORMATION_STATE state)
        {
            switch (state)
            {
                case E_TRANSFORMATION_STATE.ETS_WORLD: return s_world;
                case E_TRANSFORMATION_STATE.ETS_VIEW: return s_view;
                default: return s_proj;
            }
        }

        static void ApplyViewProjection()
        {
            if (!s_vpDirty) return;
            s_vpDirty = false;
            if (s_in2D)
            {
                Matrix4x4 ortho = Matrix4x4.Ortho(0, SCREEN_W, SCREEN_H, 0, -1, 1);
                s_cmd.SetViewProjectionMatrices(Matrix4x4.identity, ortho);
                return;
            }
            // Irrlicht view space looks along +Z; Unity's looks along -Z.
            Matrix4x4 S = Matrix4x4.Scale(new Vector3(1, 1, -1));
            Matrix4x4 D = Matrix4x4.identity;
            D[2, 2] = 2; D[2, 3] = -1; // z_gl = 2 z_d3d - w
            Matrix4x4 view = S * s_view.m;
            Matrix4x4 proj = D * s_proj.m * S;
            s_cmd.SetViewProjectionMatrices(view, proj);
        }

        public static void setViewPort(r2i area)
        {
            Flush();
            s_viewport = area;
            float sx = s_pixelRect.width / SCREEN_W, sy = s_pixelRect.height / SCREEN_H;
            // Unity viewport origin is bottom-left.
            Rect r = new Rect(s_pixelRect.x + area.UpperLeftCorner.X * sx,
                              s_pixelRect.y + (SCREEN_H - area.LowerRightCorner.Y) * sy,
                              area.getWidth() * sx, area.getHeight() * sy);
            s_cmd.SetViewport(r);
        }

        public static r2i getViewPort() { return s_viewport; }
        public static d2i getScreenSize() { return new d2i(SCREEN_W, SCREEN_H); }

        public static void beginScene2D()
        {
            Flush();
            if (s_in2D) return;
            s_world3DBackup = s_world;
            s_view3DBackup = s_view;
            s_proj3DBackup = s_proj;
            s_world = matrix4.Identity;
            s_in2D = true;
            s_vpDirty = true;
            s_2dTex = null;
            s_2dType = E_MATERIAL_TYPE.EMT_2D_ALPHA;
        }

        public static void endScene2D()
        {
            Flush();
            if (!s_in2D) return;
            s_in2D = false;
            s_world = s_world3DBackup;
            s_view = s_view3DBackup;
            s_proj = s_proj3DBackup;
            s_vpDirty = true;
        }

        // ------------------------------------------------------------------ resources

        static Mesh NextMesh()
        {
            if (s_meshPoolUsed >= s_meshPool.Count)
            {
                Mesh m = new Mesh();
                m.MarkDynamic();
                m.indexFormat = IndexFormat.UInt32;
                s_meshPool.Add(m);
            }
            Mesh mesh = s_meshPool[s_meshPoolUsed++];
            mesh.Clear();
            return mesh;
        }

        static MaterialPropertyBlock NextProps()
        {
            if (s_propPoolUsed >= s_propPool.Count) s_propPool.Add(new MaterialPropertyBlock());
            MaterialPropertyBlock p = s_propPool[s_propPoolUsed++];
            p.Clear();
            return p;
        }

        static void BlendFor(E_MATERIAL_TYPE type, out BlendMode src, out BlendMode dst, out bool transparent)
        {
            transparent = true;
            switch (type)
            {
                // The engine fork's GL renderers: trans_add is alpha-weighted additive (GL_SRC_ALPHA, GL_ONE),
                // trans_vertex_alpha takes alpha from the vertices only (GL_ONE, GL_ONE_MINUS_SRC_ALPHA).
                case E_MATERIAL_TYPE.EMT_TRANSPARENT_ADD_COLOR:
                    src = BlendMode.SrcAlpha; dst = BlendMode.One; break;
                case E_MATERIAL_TYPE.EMT_TRANSPARENT_VERTEX_ALPHA:
                    src = BlendMode.One; dst = BlendMode.OneMinusSrcAlpha; break;
                case E_MATERIAL_TYPE.EMT_TRANSPARENT_REFLECTION_2_LAYER:
                    src = BlendMode.One; dst = BlendMode.OneMinusSrcColor; break;
                case E_MATERIAL_TYPE.EMT_SOLID:
                case E_MATERIAL_TYPE.EMT_TRANSPARENT_ALPHA_CHANNEL_REF:
                case E_MATERIAL_TYPE.EMT_LIGHTMAP:
                    src = BlendMode.One; dst = BlendMode.Zero; transparent = false; break;
                default:
                    src = BlendMode.SrcAlpha; dst = BlendMode.OneMinusSrcAlpha; break;
            }
        }

        static Material SpriteMaterial(E_MATERIAL_TYPE type, bool depthTest, bool alphaRef)
        {
            int key = (int)type | (depthTest ? 0x100 : 0) | (alphaRef ? 0x200 : 0);
            Material m;
            if (s_spriteMaterials.TryGetValue(key, out m)) return m;
            m = new Material(s_spriteShader) { name = "CF_Sprite_" + key };
            BlendMode src, dst; bool transparent;
            BlendFor(type, out src, out dst, out transparent);
            if (type == E_MATERIAL_TYPE.EMT_SOLID) { src = BlendMode.SrcAlpha; dst = BlendMode.OneMinusSrcAlpha; }
            m.SetFloat("_SrcBlend", (float)src);
            m.SetFloat("_DstBlend", (float)dst);
            m.SetFloat("_ZTest", depthTest ? (float)CompareFunction.LessEqual : (float)CompareFunction.Always);
            m.SetFloat("_ZWrite", 0);
            m.SetFloat("_Gradient", type == E_MATERIAL_TYPE.EMT_A8I8_GRADIENT ? 1 : 0);
            m.SetFloat("_VertexAlpha", type == E_MATERIAL_TYPE.EMT_TRANSPARENT_VERTEX_ALPHA ? 1 : 0);
            m.SetFloat("_Cutoff", alphaRef ? 0.5f : -1f);
            s_spriteMaterials[key] = m;
            return m;
        }

        static Material MeshMaterial(SMaterial mat)
        {
            BlendMode src, dst; bool transparent;
            BlendFor(mat.MaterialType, out src, out dst, out transparent);
            CullMode cull = CullMode.Off;
            if (mat.BackfaceCulling && !mat.FrontfaceCulling) cull = SwapCulling ? CullMode.Front : CullMode.Back;
            else if (mat.FrontfaceCulling && !mat.BackfaceCulling) cull = SwapCulling ? CullMode.Back : CullMode.Front;
            bool zwrite = mat.ZWriteEnable && !transparent;
            bool alphaRef = mat.MaterialType == E_MATERIAL_TYPE.EMT_TRANSPARENT_ALPHA_CHANNEL_REF;
            int key = ((int)src) | ((int)dst << 4) | ((int)cull << 8) | ((zwrite ? 1 : 0) << 10) | ((mat.ZBuffer ? 1 : 0) << 11) | ((alphaRef ? 1 : 0) << 12);
            Material m;
            if (s_meshMaterials.TryGetValue(key, out m)) return m;
            m = new Material(s_meshShader) { name = "CF_Mesh_" + key };
            m.SetFloat("_SrcBlend", (float)src);
            m.SetFloat("_DstBlend", (float)dst);
            m.SetFloat("_Cull", (float)cull);
            m.SetFloat("_ZWrite", zwrite ? 1 : 0);
            m.SetFloat("_ZTest", mat.ZBuffer ? (float)CompareFunction.LessEqual : (float)CompareFunction.Always);
            m.SetFloat("_Cutoff", alphaRef ? 0.5f : -1f);
            m.renderQueue = transparent ? 3000 : 2000;
            s_meshMaterials[key] = m;
            return m;
        }

        // ------------------------------------------------------------------ 2D batching

        static void ResetBatch()
        {
            s_bv.Clear(); s_buv.Clear(); s_bc.Clear(); s_bi.Clear();
            s_bTex = null; s_bKey = -1;
        }

        public static void Flush()
        {
            if (s_bi.Count == 0) { ResetBatch(); return; }
            ApplyViewProjection();
            Mesh mesh = NextMesh();
            mesh.SetVertices(s_bv);
            mesh.SetUVs(0, s_buv);
            mesh.SetColors(s_bc);
            mesh.SetTriangles(s_bi, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            E_MATERIAL_TYPE type = (E_MATERIAL_TYPE)(s_bKey & 0xff);
            Material mat = SpriteMaterial(type, (s_bKey & 0x100) != 0, (s_bKey & 0x200) != 0);
            MaterialPropertyBlock p = NextProps();
            p.SetTexture("_MainTex", (s_bTex != null ? s_bTex : TextureCache.White).Tex);
            p.SetColor("_Color2", s_bColor2.ToColor());
            p.SetFloat("_UseEmissive", s_bEmissive ? 1 : 0);
            p.SetColor("_Emissive", s_bEmissiveColor.ToColor());
            s_cmd.DrawMesh(mesh, Matrix4x4.identity, mat, 0, 0, p);
            DrawCalls++;
            Primitives += s_bi.Count / 3;
            ResetBatch();
        }

        static void PrepareBatch(ITexture tex, E_MATERIAL_TYPE type, SColor color2, bool depthTest, bool alphaRef, bool emissive, SColor emissiveColor)
        {
            int key = (int)type | (depthTest ? 0x100 : 0) | (alphaRef ? 0x200 : 0);
            if (s_bi.Count > 0 && (tex != s_bTex || key != s_bKey || color2.color != s_bColor2.color ||
                emissive != s_bEmissive || emissiveColor.color != s_bEmissiveColor.color || s_bv.Count > 60000))
                Flush();
            s_bTex = tex;
            s_bKey = key;
            s_bColor2 = color2;
            s_bEmissive = emissive;
            s_bEmissiveColor = emissiveColor;
        }

        static Vector3 W2D(float x, float y)
        {
            // World transform applied to 2D vertices (ASprite::PushTransform).
            Matrix4x4 m = s_world.m;
            return new Vector3(m[0] * x + m[4] * y + m[12], m[1] * x + m[5] * y + m[13], 0);
        }

        /// <summary>Sets texture/material for subsequent 2D quads (driver->set2DMaterial).</summary>
        public static void set2DMaterial(SMaterial mat)
        {
            s_2dTex = mat.Textures[0];
            s_2dType = mat.MaterialType == E_MATERIAL_TYPE.EMT_A8I8_GRADIENT ? E_MATERIAL_TYPE.EMT_A8I8_GRADIENT :
                       mat.MaterialType == E_MATERIAL_TYPE.EMT_TRANSPARENT_ADD_COLOR ? E_MATERIAL_TYPE.EMT_TRANSPARENT_ADD_COLOR :
                       E_MATERIAL_TYPE.EMT_2D_ALPHA;
            s_2dColor2 = mat.EmissiveColor;
        }

        public static void set2DTexture(ITexture tex)
        {
            s_2dTex = tex;
            s_2dType = E_MATERIAL_TYPE.EMT_2D_ALPHA;
        }

        static bool ClipQuad(ref r2i pos, ref r2f tc, r2i clip)
        {
            if (pos.LowerRightCorner.X <= clip.UpperLeftCorner.X || pos.UpperLeftCorner.X >= clip.LowerRightCorner.X ||
                pos.LowerRightCorner.Y <= clip.UpperLeftCorner.Y || pos.UpperLeftCorner.Y >= clip.LowerRightCorner.Y)
                return false;
            float w = pos.getWidth(), h = pos.getHeight();
            if (w == 0 || h == 0) return false;
            float du = (tc.LowerRightCorner.X - tc.UpperLeftCorner.X) / w;
            float dv = (tc.LowerRightCorner.Y - tc.UpperLeftCorner.Y) / h;
            if (pos.UpperLeftCorner.X < clip.UpperLeftCorner.X)
            {
                tc.UpperLeftCorner.X += (clip.UpperLeftCorner.X - pos.UpperLeftCorner.X) * du;
                pos.UpperLeftCorner.X = clip.UpperLeftCorner.X;
            }
            if (pos.LowerRightCorner.X > clip.LowerRightCorner.X)
            {
                tc.LowerRightCorner.X -= (pos.LowerRightCorner.X - clip.LowerRightCorner.X) * du;
                pos.LowerRightCorner.X = clip.LowerRightCorner.X;
            }
            if (pos.UpperLeftCorner.Y < clip.UpperLeftCorner.Y)
            {
                tc.UpperLeftCorner.Y += (clip.UpperLeftCorner.Y - pos.UpperLeftCorner.Y) * dv;
                pos.UpperLeftCorner.Y = clip.UpperLeftCorner.Y;
            }
            if (pos.LowerRightCorner.Y > clip.LowerRightCorner.Y)
            {
                tc.LowerRightCorner.Y -= (pos.LowerRightCorner.Y - clip.LowerRightCorner.Y) * dv;
                pos.LowerRightCorner.Y = clip.LowerRightCorner.Y;
            }
            return true;
        }

        /// <summary>Quad colors follow the driver: [0]=upper-left, [1]=lower-left, [2]=lower-right, [3]=upper-right.</summary>
        static void AddQuad(r2i pos, r2f tc, SColor c0, SColor c1, SColor c2, SColor c3)
        {
            int b = s_bv.Count;
            float l = pos.UpperLeftCorner.X, t = pos.UpperLeftCorner.Y, r = pos.LowerRightCorner.X, bo = pos.LowerRightCorner.Y;
            s_bv.Add(W2D(l, t)); s_buv.Add(new Vector2(tc.UpperLeftCorner.X, 1 - tc.UpperLeftCorner.Y)); s_bc.Add(c0.ToColor32());
            s_bv.Add(W2D(r, t)); s_buv.Add(new Vector2(tc.LowerRightCorner.X, 1 - tc.UpperLeftCorner.Y)); s_bc.Add(c3.ToColor32());
            s_bv.Add(W2D(r, bo)); s_buv.Add(new Vector2(tc.LowerRightCorner.X, 1 - tc.LowerRightCorner.Y)); s_bc.Add(c2.ToColor32());
            s_bv.Add(W2D(l, bo)); s_buv.Add(new Vector2(tc.UpperLeftCorner.X, 1 - tc.LowerRightCorner.Y)); s_bc.Add(c1.ToColor32());
            s_bi.Add(b); s_bi.Add(b + 1); s_bi.Add(b + 2);
            s_bi.Add(b); s_bi.Add(b + 2); s_bi.Add(b + 3);
        }

        /// <summary>draw2DImage(texture, destRect, sourceRect, clipRect, colors, useAlpha) using the current 2D material.</summary>
        public static void draw2DImage(ITexture texture, r2i destRect, r2i sourceRect, r2i? clipRect, SColor[] colors)
        {
            if (texture == null) return;
            if (!s_in2D) { beginScene2D(); }
            float invW = 1f / texture.Tex.width, invH = 1f / texture.Tex.height;
            r2f tc = new r2f(sourceRect.UpperLeftCorner.X * invW, sourceRect.UpperLeftCorner.Y * invH,
                             sourceRect.LowerRightCorner.X * invW, sourceRect.LowerRightCorner.Y * invH);
            r2i pos = destRect;
            if (clipRect.HasValue && !ClipQuad(ref pos, ref tc, clipRect.Value)) return;
            E_MATERIAL_TYPE type = s_2dTex == texture ? s_2dType : E_MATERIAL_TYPE.EMT_2D_ALPHA;
            PrepareBatch(texture, type, s_2dColor2, false, false, false, default(SColor));
            AddQuad(pos, tc, colors[0], colors[1], colors[2], colors[3]);
        }

        public static void draw2DImage(ITexture texture, r2i destRect, r2i sourceRect, r2i? clipRect, SColor color)
        {
            s_tmpColors[0] = s_tmpColors[1] = s_tmpColors[2] = s_tmpColors[3] = color;
            draw2DImage(texture, destRect, sourceRect, clipRect, s_tmpColors);
        }

        static readonly SColor[] s_tmpColors = new SColor[4];

        public static void draw2DRectangle(SColor color, r2i pos, r2i? clip = null)
        {
            if (!s_in2D) { beginScene2D(); }
            if (clip.HasValue) pos.clipAgainst(clip.Value);
            PrepareBatch(null, E_MATERIAL_TYPE.EMT_2D_ALPHA, default(SColor), false, false, false, default(SColor));
            AddQuad(pos, new r2f(0, 0, 1, 1), color, color, color, color);
        }

        public static void draw2DRectangle(r2i pos, SColor colorLeftUp, SColor colorRightUp, SColor colorLeftDown, SColor colorRightDown, r2i? clip = null)
        {
            if (!s_in2D) { beginScene2D(); }
            if (clip.HasValue) pos.clipAgainst(clip.Value);
            PrepareBatch(null, E_MATERIAL_TYPE.EMT_2D_ALPHA, default(SColor), false, false, false, default(SColor));
            // Same array the driver builds: {LU, RU, RD, LD} mapped to UL, LL, LR, UR.
            AddQuad(pos, new r2f(0, 0, 1, 1), colorLeftUp, colorRightUp, colorRightDown, colorLeftDown);
        }

        public static void draw2DLine(p2i start, p2i end, SColor color)
        {
            if (!s_in2D) { beginScene2D(); }
            PrepareBatch(null, E_MATERIAL_TYPE.EMT_2D_ALPHA, default(SColor), false, false, false, default(SColor));
            Vector2 a = new Vector2(start.X, start.Y), b2 = new Vector2(end.X, end.Y);
            Vector2 d = b2 - a;
            if (d.sqrMagnitude < 0.0001f) d = Vector2.right;
            Vector2 n = new Vector2(-d.y, d.x).normalized * 0.5f;
            int b = s_bv.Count;
            Color32 c = color.ToColor32();
            s_bv.Add(W2D(a.x - n.x, a.y - n.y)); s_bv.Add(W2D(b2.x - n.x, b2.y - n.y));
            s_bv.Add(W2D(b2.x + n.x, b2.y + n.y)); s_bv.Add(W2D(a.x + n.x, a.y + n.y));
            for (int i = 0; i < 4; i++) { s_buv.Add(Vector2.zero); s_bc.Add(c); }
            s_bi.Add(b); s_bi.Add(b + 1); s_bi.Add(b + 2); s_bi.Add(b); s_bi.Add(b + 2); s_bi.Add(b + 3);
        }

        // ------------------------------------------------------------------ 3D

        /// <summary>drawVertexPrimitiveList for triangle lists with the given material and the current world matrix.</summary>
        public static void drawVertexPrimitiveList(S3DVertex[] vertices, int vertexCount, int[] indices, int indexCount, SMaterial material)
        {
            if (vertexCount == 0 || indexCount == 0) return;
            if (s_in2D)
            {
                // 2D path: transform by world on the CPU into the sprite batch.
                PrepareBatch(material.Textures[0], material.MaterialType, material.EmissiveColor, false, false, false, default(SColor));
                int b = s_bv.Count;
                for (int i = 0; i < vertexCount; i++)
                {
                    S3DVertex v = vertices[i];
                    s_bv.Add(W2D(v.Pos.X, v.Pos.Y));
                    s_buv.Add(new Vector2(v.TCoords.X, 1 - v.TCoords.Y));
                    s_bc.Add(v.Color.ToColor32());
                }
                for (int i = 0; i < indexCount; i++) s_bi.Add(b + indices[i]);
                return;
            }
            Flush();
            ApplyViewProjection();
            Mesh mesh = NextMesh();
            s_tv.Clear(); s_tuv.Clear(); s_tc.Clear(); s_ti.Clear();
            for (int i = 0; i < vertexCount; i++)
            {
                S3DVertex v = vertices[i];
                s_tv.Add(new Vector3(v.Pos.X, v.Pos.Y, v.Pos.Z));
                s_tuv.Add(new Vector2(v.TCoords.X, 1 - v.TCoords.Y));
                s_tc.Add(v.Color.ToColor32());
            }
            for (int i = 0; i < indexCount; i++) s_ti.Add(indices[i]);
            mesh.SetVertices(s_tv);
            mesh.SetUVs(0, s_tuv);
            mesh.SetColors(s_tc);
            mesh.SetTriangles(s_ti, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            bool lit = material.Lighting && LightingEnabled;
            Material mat = SpriteMaterial(material.MaterialType == E_MATERIAL_TYPE.EMT_SOLID ? E_MATERIAL_TYPE.EMT_TRANSPARENT_ALPHA_CHANNEL : material.MaterialType,
                                          material.ZBuffer, material.MaterialType == E_MATERIAL_TYPE.EMT_TRANSPARENT_ALPHA_CHANNEL_REF);
            MaterialPropertyBlock p = NextProps();
            p.SetTexture("_MainTex", (material.Textures[0] != null ? material.Textures[0] : TextureCache.White).Tex);
            p.SetColor("_Color2", material.EmissiveColor.ToColor());
            p.SetFloat("_UseEmissive", lit ? 1 : 0);
            p.SetColor("_Emissive", LitColor(material).ToColor());
            s_cmd.DrawMesh(mesh, s_world.m, mat, 0, 0, p);
            DrawCalls++;
            Primitives += indexCount / 3;
        }

        /// <summary>draw3DLine with the current world transform; thickness (pixels) > 1 draws a camera-facing strip.</summary>
        public static void draw3DLine(v3f start, v3f end, SColor color, E_MATERIAL_TYPE type = E_MATERIAL_TYPE.EMT_TRANSPARENT_ALPHA_CHANNEL, float thickness = 1.0f)
        {
            Flush();
            ApplyViewProjection();
            Mesh mesh = NextMesh();
            Color32 c = color.ToColor32();
            CCameraSceneNode cam = thickness > 1.0f ? CSceneManager.Instance.getActiveCamera() : null;
            if (cam == null)
            {
                mesh.SetVertices(new List<Vector3> { start, end });
                mesh.SetColors(new List<Color32> { c, c });
                mesh.SetUVs(0, new List<Vector2> { Vector2.zero, Vector2.zero });
                mesh.SetIndices(new[] { 0, 1 }, MeshTopology.Lines, 0, false);
            }
            else
            {
                v3f ws = s_world.transformVect(start), we = s_world.transformVect(end);
                v3f eye = cam.getAbsolutePosition();
                v3f side = (we - ws).crossProduct(eye - (ws + we) * 0.5f);
                if (side.getLengthSQ() == 0) side = new v3f(0, 1, 0);
                side.normalize();
                // One pixel at distance d spans 2*d*tan(fov/2)/screenHeight world units.
                float px = 2.0f * (float)System.Math.Tan(cam.getFOV() * 0.5f) / G.GAME_SCREEN_H;
                v3f hs = side * (0.5f * thickness * px * (eye - ws).getLength());
                v3f he = side * (0.5f * thickness * px * (eye - we).getLength());
                matrix4 inv = s_world;
                inv.makeInverse();
                mesh.SetVertices(new List<Vector3> { inv.transformVect(ws - hs), inv.transformVect(ws + hs), inv.transformVect(we + he), inv.transformVect(we - he) });
                mesh.SetColors(new List<Color32> { c, c, c, c });
                mesh.SetUVs(0, new List<Vector2> { Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero });
                mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0, false);
            }
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            MaterialPropertyBlock p = NextProps();
            p.SetTexture("_MainTex", TextureCache.White.Tex);
            s_cmd.DrawMesh(mesh, s_world.m, SpriteMaterial(type, true, false), 0, 0, p);
            DrawCalls++;
        }

        static readonly List<Vector3> s_tv = new List<Vector3>();
        static readonly List<Vector2> s_tuv = new List<Vector2>();
        static readonly List<Color32> s_tc = new List<Color32>();
        static readonly List<int> s_ti = new List<int>();

        /// <summary>The fixed-function lit color: emissive + ambient * light ambient.</summary>
        public static SColorf LitColor(SMaterial m)
        {
            SColorf e = new SColorf(m.EmissiveColor), a = new SColorf(m.AmbientColor);
            return new SColorf(Mathf.Clamp01(e.r + a.r * LightAmbient.r), Mathf.Clamp01(e.g + a.g * LightAmbient.g), Mathf.Clamp01(e.b + a.b * LightAmbient.b), 1);
        }

        static readonly Matrix4x4[] s_identityBones = new Matrix4x4[64];

        /// <summary>Draws one submesh of a prepared Unity mesh with a world matrix (or GPU skinning palette).</summary>
        public static void drawMesh(Mesh mesh, int submesh, matrix4 world, SMaterial material, Matrix4x4[] bones)
        {
            if (mesh == null) return;
            Flush();
            ApplyViewProjection();
            Material mat = MeshMaterial(material);
            MaterialPropertyBlock p = NextProps();
            ITexture tex = material.Textures[0];
            p.SetTexture("_MainTex", (tex != null ? tex : TextureCache.White).Tex);
            matrix4 tm = material.TextureMatrix;
            p.SetVector("_TexRow0", new Vector4(tm[0], tm[4], tm[8], 0));
            p.SetVector("_TexRow1", new Vector4(tm[1], tm[5], tm[9], 0));
            bool lit = material.Lighting && LightingEnabled;
            p.SetFloat("_Lighting", lit ? 1 : 0);
            p.SetColor("_Emissive", material.EmissiveColor.ToColor());
            p.SetColor("_Ambient", material.AmbientColor.ToColor());
            p.SetColor("_Diffuse", material.DiffuseColor.ToColor());
            p.SetColor("_LightAmbient", LightAmbient.ToColor());
            if (bones != null)
            {
                p.SetFloat("_Skinned", 1);
                p.SetMatrixArray("_Bones", bones);
                s_cmd.DrawMesh(mesh, Matrix4x4.identity, mat, submesh, 0, p);
            }
            else
            {
                p.SetFloat("_Skinned", 0);
                p.SetMatrixArray("_Bones", s_identityBones);
                s_cmd.DrawMesh(mesh, world.m, mat, submesh, 0, p);
            }
            DrawCalls++;
        }
    }
}
