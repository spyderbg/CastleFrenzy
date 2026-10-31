// Scene graph replacing Irrlicht's ISceneManager/ISceneNode, including the Collada scene nodes
// (mesh, skinned mesh, morphing mesh, camera), the Collada animator, timeline controller and events.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CastleFrenzy
{
    public enum ESCENE_NODE_TYPE
    {
        ESNT_UNKNOWN, ESNT_EMPTY, ESNT_CAMERA, ESNT_LIGHT, ESNT_MESH, ESNT_SCENE_MANAGER,
        ESNT_COLLADA_ROOT, ESNT_COLLADA_NODE, ESNT_COLLADA_MESH, ESNT_COLLADA_SKIN_MESH,
        ESNT_COLLADA_MORPH_MESH, ESNT_COLLADA_CAMERA, ESNT_COLLADA_LIGHT,
    }

    public enum E_LIGHT_TYPE { ELT_POINT, ELT_SPOT, ELT_DIRECTIONAL, ELT_AMBIENT }

    public class SLight
    {
        public E_LIGHT_TYPE Type = E_LIGHT_TYPE.ELT_POINT;
        public SColorf AmbientColor = new SColorf(0, 0, 0, 1);
        public SColorf DiffuseColor = new SColorf(1, 1, 1, 1);
        public SColorf SpecularColor = new SColorf(1, 1, 1, 1);
        public float Radius = 100;
    }

    public class ISceneNode
    {
        public ISceneNode Parent;
        public readonly List<ISceneNode> Children = new List<ISceneNode>();
        public readonly List<ISceneNodeAnimator> Animators = new List<ISceneNodeAnimator>();
        public string Name = "";
        public v3f RelativeTranslation;
        public quaternion RelativeRotation = quaternion.Identity;
        public v3f RelativeScale = new v3f(1, 1, 1);
        public matrix4 AbsoluteTransformation = matrix4.Identity;
        public bool IsVisible = true;
        public int RenderingLayer;
        public float CameraOffset;

        public virtual ESCENE_NODE_TYPE getType() { return ESCENE_NODE_TYPE.ESNT_UNKNOWN; }
        public virtual string getUID() { return ""; }
        public virtual string getScopeID() { return ""; }
        public string getName() { return Name; }
        public void setName(string n) { Name = n ?? ""; }

        public void grab() { }
        public void drop() { }

        public virtual int getMaterialCount() { return 0; }
        public virtual SMaterial getMaterial(int i) { return null; }

        public List<ISceneNode> getChildren() { return Children; }
        public ISceneNode getParent() { return Parent; }
        public List<ISceneNodeAnimator> getAnimators() { return Animators; }

        public void addAnimator(ISceneNodeAnimator a)
        {
            if (a == null) return;
            Animators.Add(a);
            a.onBind(this);
        }

        public ISceneNodeAnimator getFirstAnimator() { return Animators.Count > 0 ? Animators[0] : null; }

        public virtual void addChild(ISceneNode child)
        {
            if (child == null || child == this) return;
            if (child.Parent != null) child.Parent.removeChild(child);
            child.Parent = this;
            Children.Add(child);
        }

        public virtual bool removeChild(ISceneNode child)
        {
            if (Children.Remove(child)) { child.Parent = null; return true; }
            return false;
        }

        public void removeAll()
        {
            foreach (ISceneNode c in Children) c.Parent = null;
            Children.Clear();
        }

        public void remove() { if (Parent != null) Parent.removeChild(this); }

        public bool isVisible() { return IsVisible; }
        public virtual void setVisible(bool v) { IsVisible = v; }

        public v3f getPosition() { return RelativeTranslation; }
        public virtual void setPosition(v3f p) { RelativeTranslation = p; }
        public void setPosition(float x, float y, float z) { setPosition(new v3f(x, y, z)); }
        public quaternion getRotation() { return RelativeRotation; }
        public virtual void setRotation(quaternion q) { RelativeRotation = q; }
        public v3f getScale() { return RelativeScale; }
        public virtual void setScale(v3f s) { RelativeScale = s; }

        public matrix4 getRelativeTransformation()
        {
            matrix4 m = RelativeRotation.getMatrix();
            if (RelativeScale != new v3f(1, 1, 1)) m.postScale(RelativeScale);
            m.setTranslation(RelativeTranslation);
            return m;
        }

        public void setRelativeTransformation(matrix4 mat)
        {
            RelativeTranslation = mat.getTranslation();
            RelativeRotation = new quaternion(mat);
            RelativeScale = mat.getScale();
        }

        public matrix4 getAbsoluteTransformation() { return AbsoluteTransformation; }
        public void setAbsoluteTransformation(matrix4 m) { AbsoluteTransformation = m; }
        public v3f getAbsolutePosition() { return AbsoluteTransformation.getTranslation(); }

        public void updateAbsolutePosition(bool updateChildren = false)
        {
            if (Parent != null) AbsoluteTransformation = Parent.AbsoluteTransformation * getRelativeTransformation();
            else AbsoluteTransformation = getRelativeTransformation();
            if (updateChildren) foreach (ISceneNode c in Children) c.updateAbsolutePosition(true);
        }

        public virtual void OnAnimate(int timeMs)
        {
            if (!IsVisible) return;
            for (int i = 0; i < Animators.Count; i++) Animators[i].animateNode(this, timeMs);
            updateAbsolutePosition();
            for (int i = 0; i < Children.Count; i++) Children[i].OnAnimate(timeMs);
        }

        public virtual void OnRegisterSceneNode()
        {
            if (!IsVisible) return;
            for (int i = 0; i < Children.Count; i++) Children[i].OnRegisterSceneNode();
        }

        public virtual void render(int buffer) { }
        public virtual aabbox3df getBoundingBox() { return new aabbox3df(v3f.Zero); }

        public aabbox3df getTransformedBoundingBox()
        {
            aabbox3df b = getBoundingBox();
            AbsoluteTransformation.transformBoxEx(ref b);
            return b;
        }

        public ISceneNode getSceneNodeFromName(string name)
        {
            if (Name == name) return this;
            for (int i = 0; i < Children.Count; i++)
            {
                ISceneNode n = Children[i].getSceneNodeFromName(name);
                if (n != null) return n;
            }
            return null;
        }

        public ISceneNode getSceneNodeFromUID(string uid)
        {
            if (getUID() == uid) return this;
            for (int i = 0; i < Children.Count; i++)
            {
                ISceneNode n = Children[i].getSceneNodeFromUID(uid);
                if (n != null) return n;
            }
            return null;
        }

        public ISceneNode getSceneNodeFromScopeID(string sid)
        {
            if (getScopeID() == sid) return this;
            for (int i = 0; i < Children.Count; i++)
            {
                ISceneNode n = Children[i].getSceneNodeFromScopeID(sid);
                if (n != null) return n;
            }
            return null;
        }

        public ISceneNode getSceneNodeFromType(ESCENE_NODE_TYPE type)
        {
            if (getType() == type) return this;
            for (int i = 0; i < Children.Count; i++)
            {
                ISceneNode n = Children[i].getSceneNodeFromType(type);
                if (n != null) return n;
            }
            return null;
        }

        public void setRenderingLayer(int l) { RenderingLayer = l; }
        public int getRenderingLayer() { return RenderingLayer; }
    }

    public class CEmptySceneNode : ISceneNode
    {
        public override ESCENE_NODE_TYPE getType() { return ESCENE_NODE_TYPE.ESNT_EMPTY; }
    }

    public class CLightSceneNode : ISceneNode
    {
        public SLight LightData = new SLight();
        public SLight getLightData() { return LightData; }
        public override ESCENE_NODE_TYPE getType() { return ESCENE_NODE_TYPE.ESNT_LIGHT; }
    }

    // ---------------------------------------------------------------------------- camera

    public class SViewFrustum
    {
        public const int VF_FAR_PLANE = 0, VF_NEAR_PLANE = 1, VF_LEFT_PLANE = 2, VF_RIGHT_PLANE = 3, VF_BOTTOM_PLANE = 4, VF_TOP_PLANE = 5, VF_PLANE_COUNT = 6;
        public v3f cameraPosition;
        public readonly plane3df[] planes = new plane3df[6];
        public aabbox3df boundingBox;

        public void setFrom(matrix4 mat)
        {
            planes[VF_LEFT_PLANE] = P(mat[3] + mat[0], mat[7] + mat[4], mat[11] + mat[8], mat[15] + mat[12]);
            planes[VF_RIGHT_PLANE] = P(mat[3] - mat[0], mat[7] - mat[4], mat[11] - mat[8], mat[15] - mat[12]);
            planes[VF_TOP_PLANE] = P(mat[3] - mat[1], mat[7] - mat[5], mat[11] - mat[9], mat[15] - mat[13]);
            planes[VF_BOTTOM_PLANE] = P(mat[3] + mat[1], mat[7] + mat[5], mat[11] + mat[9], mat[15] + mat[13]);
            planes[VF_FAR_PLANE] = P(mat[3] - mat[2], mat[7] - mat[6], mat[11] - mat[10], mat[15] - mat[14]);
            planes[VF_NEAR_PLANE] = P(mat[2], mat[6], mat[10], mat[14]);
            for (int i = 0; i < VF_PLANE_COUNT; ++i)
            {
                float len = -IrrMath.reciprocal_squareroot(planes[i].Normal.getLengthSQ());
                planes[i].Normal *= len;
                planes[i].D *= len;
            }
            boundingBox.reset(cameraPosition);
            boundingBox.addInternalPoint(getFarLeftUp());
            boundingBox.addInternalPoint(getFarRightUp());
            boundingBox.addInternalPoint(getFarLeftDown());
            boundingBox.addInternalPoint(getFarRightDown());
        }

        static plane3df P(float x, float y, float z, float d) { plane3df p = new plane3df(); p.Normal = new v3f(x, y, z); p.D = d; return p; }

        v3f I3(int a, int b, int c) { v3f p; planes[a].getIntersectionWithPlanes(planes[b], planes[c], out p); return p; }
        public v3f getFarLeftUp() { return I3(VF_FAR_PLANE, VF_TOP_PLANE, VF_LEFT_PLANE); }
        public v3f getFarLeftDown() { return I3(VF_FAR_PLANE, VF_BOTTOM_PLANE, VF_LEFT_PLANE); }
        public v3f getFarRightUp() { return I3(VF_FAR_PLANE, VF_TOP_PLANE, VF_RIGHT_PLANE); }
        public v3f getFarRightDown() { return I3(VF_FAR_PLANE, VF_BOTTOM_PLANE, VF_RIGHT_PLANE); }
        public v3f getNearLeftUp() { return I3(VF_NEAR_PLANE, VF_TOP_PLANE, VF_LEFT_PLANE); }
        public v3f getNearLeftDown() { return I3(VF_NEAR_PLANE, VF_BOTTOM_PLANE, VF_LEFT_PLANE); }
        public v3f getNearRightUp() { return I3(VF_NEAR_PLANE, VF_TOP_PLANE, VF_RIGHT_PLANE); }
        public v3f getNearRightDown() { return I3(VF_NEAR_PLANE, VF_BOTTOM_PLANE, VF_RIGHT_PLANE); }
        public aabbox3df getBoundingBox() { return boundingBox; }
    }

    public class CCameraSceneNode : ISceneNode
    {
        public v3f Target = new v3f(0, 0, 100);
        public v3f UpVector = new v3f(0, 1, 0);
        public float ZNear = 1.0f, ZFar = 3000.0f;
        public float Fovy = IrrMath.PI / 2.5f;
        public float Aspect = 4.0f / 3.0f;
        public float Magy;
        public bool IsOrthogonal;
        public matrix4 Projection = matrix4.Identity, View = matrix4.Identity;
        public readonly SViewFrustum ViewArea = new SViewFrustum();

        public CCameraSceneNode() { recalculateProjectionMatrix(); }

        public override ESCENE_NODE_TYPE getType() { return ESCENE_NODE_TYPE.ESNT_CAMERA; }

        public void setProjectionMatrix(matrix4 projection, bool isOrthogonal = false) { IsOrthogonal = isOrthogonal; Projection = projection; }
        public matrix4 getProjectionMatrix() { return Projection; }
        public matrix4 getViewMatrix() { return View; }
        public void setTarget(v3f pos) { Target = pos; }
        public v3f getTarget() { return Target; }
        public void setUpVector(v3f up) { UpVector = up; }
        public v3f getUpVector() { return UpVector; }
        public float getNearValue() { return ZNear; }
        public float getFarValue() { return ZFar; }
        public float getAspectRatio() { return Aspect; }
        public float getFOV() { return Fovy; }
        public void setNearValue(float f) { ZNear = f; recalculateProjectionMatrix(); }
        public void setFarValue(float f) { ZFar = f; recalculateProjectionMatrix(); }
        public void setAspectRatio(float f) { Aspect = f; recalculateProjectionMatrix(); }
        public void setFOV(float f) { Fovy = f; recalculateProjectionMatrix(); }
        public void setMAG(float f) { Magy = f; recalculateProjectionMatrix(); }
        public bool isOrthogonal() { return IsOrthogonal; }
        public SViewFrustum getViewFrustum() { return ViewArea; }

        public void recalculateProjectionMatrix()
        {
            if (IsOrthogonal)
            {
                matrix4 m = matrix4.Identity;
                float w = 2 * Magy * Aspect, h = 2 * Magy;
                m[0] = 2 / w; m[5] = 2 / h; m[10] = 1 / (ZFar - ZNear); m[14] = ZNear / (ZNear - ZFar);
                Projection = m;
            }
            else Projection = matrix4.Identity.buildProjectionMatrixPerspectiveFov(Fovy, Aspect, ZNear, ZFar);
        }

        /// <summary>Computes the view matrix and frustum (CCameraSceneNode::OnRegisterSceneNode).</summary>
        public virtual void updateView()
        {
            v3f pos = getAbsolutePosition();
            v3f tgtv = (Target - pos).normalized();
            v3f up = UpVector.normalized();
            float dp = tgtv.dotProduct(up);
            if (IrrMath.equals(Math.Abs(dp), 1.0f)) up.X += 0.5f;
            View = matrix4.Identity.buildCameraLookAtMatrix(pos, Target, up);
            ViewArea.cameraPosition = pos;
            ViewArea.setFrom(Projection * View);
        }

        public override void OnRegisterSceneNode()
        {
            if (!IsVisible) return;
            updateView();
            base.OnRegisterSceneNode();
        }
    }

    public class CColladaCameraSceneNode : CCameraSceneNode
    {
        public readonly BdaeCamera Camera;
        public ISceneNode TargetNode;

        public CColladaCameraSceneNode(BdaeCamera camera, int upAxis)
        {
            Camera = camera;
            if (upAxis == 0) UpVector = new v3f(1, 0, 0);
            else if (upAxis == 2) UpVector = new v3f(0, 0, 1);
            else UpVector = new v3f(0, 1, 0);
            if (!camera.Ortho)
            {
                setFOV((float)Math.Atan(Math.Tan(camera.XFov * IrrMath.DEGTORAD / 2.0f) / camera.AspectRatio) * 2.0f);
            }
            else
            {
                IsOrthogonal = true;
                setAspectRatio(camera.AspectRatio);
                setMAG(camera.XFov / camera.AspectRatio);
            }
        }

        public override ESCENE_NODE_TYPE getType() { return ESCENE_NODE_TYPE.ESNT_COLLADA_CAMERA; }
        public override string getUID() { return Camera.Id; }

        public override void updateView()
        {
            if (TargetNode != null) Target = TargetNode.getAbsolutePosition();
            else Target = AbsoluteTransformation.transformVect(new v3f(0, 0, -100.0f));
            base.updateView();
        }
    }

    // ---------------------------------------------------------------------------- collada nodes

    public class CColladaSceneNode : ISceneNode
    {
        public readonly BdaeNode Node;
        public CColladaSceneNode(BdaeNode node)
        {
            Node = node;
            if (node != null)
            {
                Name = node.Name;
                RelativeTranslation = node.Position;
                RelativeRotation = node.Rotation;
                RelativeScale = node.Scale;
            }
        }
        public override ESCENE_NODE_TYPE getType() { return ESCENE_NODE_TYPE.ESNT_COLLADA_NODE; }
        public override string getUID() { return Node != null ? Node.Id : ""; }
        public override string getScopeID() { return Node != null ? Node.Sid : ""; }

        public void resetTransform(bool resetChildren)
        {
            if (Node != null)
            {
                RelativeTranslation = Node.Position;
                RelativeRotation = Node.Rotation;
                RelativeScale = Node.Scale;
            }
            if (resetChildren)
                foreach (ISceneNode c in Children)
                    if (c is CColladaSceneNode) ((CColladaSceneNode)c).resetTransform(true);
        }
    }

    /// <summary>Unity mesh built from a Bdae geometry, shared between instances.</summary>
    public class ColladaMeshData
    {
        public Mesh Mesh;
        public BdaeGeometry Geometry;
        public aabbox3df Box;
        public bool Skinned;

        static readonly Dictionary<string, ColladaMeshData> s_cache = new Dictionary<string, ColladaMeshData>();

        public static ColladaMeshData Get(BdaeFile file, BdaeGeometry g, BdaeSkin skin)
        {
            string key = file.Path + "|" + g.Id + (skin != null ? "|skin" : "");
            ColladaMeshData d;
            if (s_cache.TryGetValue(key, out d)) return d;
            d = new ColladaMeshData { Geometry = g, Box = g.Box, Skinned = skin != null };
            d.Mesh = BuildMesh(g, skin, null);
            s_cache[key] = d;
            return d;
        }

        public static Mesh BuildMesh(BdaeGeometry g, BdaeSkin skin, v3f[] positionsOverride)
        {
            Mesh m = new Mesh { name = g.Id };
            m.indexFormat = g.VertexCount > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            int n = g.VertexCount;
            Vector3[] v = new Vector3[n];
            v3f[] src = positionsOverride ?? g.Positions;
            for (int i = 0; i < n; i++) v[i] = src != null ? (Vector3)src[i] : Vector3.zero;
            m.vertices = v;
            if (g.UVs != null)
            {
                Vector2[] uv = new Vector2[n];
                for (int i = 0; i < n; i++) uv[i] = new Vector2(g.UVs[i].X, g.UVs[i].Y);
                m.uv = uv;
            }
            Color32[] cols = new Color32[n];
            for (int i = 0; i < n; i++) cols[i] = g.Colors != null ? g.Colors[i].ToColor32() : new Color32(255, 255, 255, 255);
            m.colors32 = cols;
            if (skin != null)
            {
                List<Vector4> bi = new List<Vector4>(n), bw = new List<Vector4>(n);
                int cursor = 0;
                int[] js = new int[16]; float[] ws = new float[16];
                for (int i = 0; i < n; i++)
                {
                    int count = i < skin.VCount.Length ? skin.VCount[i] : 0;
                    int k = 0;
                    for (int c = 0; c < count; c++, cursor += 2)
                    {
                        int j = skin.V[cursor], wi = skin.V[cursor + 1];
                        float w = wi < skin.Weights.Length ? skin.Weights[wi] : 0;
                        if (w == 0 || k >= 16) continue;
                        js[k] = j; ws[k] = w; k++;
                    }
                    // keep the 4 strongest influences, renormalized
                    for (int a = 0; a < k; a++)
                        for (int b = a + 1; b < k; b++)
                            if (ws[b] > ws[a]) { float tw = ws[a]; ws[a] = ws[b]; ws[b] = tw; int tj = js[a]; js[a] = js[b]; js[b] = tj; }
                    int used = Math.Min(4, k);
                    float sum = 0;
                    for (int a = 0; a < used; a++) sum += ws[a];
                    Vector4 iv = Vector4.zero, wv = Vector4.zero;
                    for (int a = 0; a < used; a++) { iv[a] = js[a]; wv[a] = sum > 0 ? ws[a] / sum : 0; }
                    if (used == 0) wv.x = 1;
                    bi.Add(iv); bw.Add(wv);
                }
                m.SetUVs(1, bi);
                m.SetUVs(2, bw);
            }
            m.subMeshCount = g.Buffers.Count;
            for (int b = 0; b < g.Buffers.Count; b++) m.SetTriangles(g.Buffers[b].Indices, b, false);
            m.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            return m;
        }
    }

    public class CColladaMeshSceneNode : ISceneNode
    {
        public CColladaRootSceneNode Root;
        public ColladaMeshData Data;
        public readonly List<SMaterial> Materials = new List<SMaterial>();

        public override ESCENE_NODE_TYPE getType() { return ESCENE_NODE_TYPE.ESNT_COLLADA_MESH; }
        public override int getMaterialCount() { return Materials.Count; }
        public override SMaterial getMaterial(int i) { return Materials[i]; }
        public override aabbox3df getBoundingBox() { return Data != null ? Data.Box : new aabbox3df(v3f.Zero); }

        public virtual Mesh CurrentMesh { get { return Data != null ? Data.Mesh : null; } }

        public override void OnRegisterSceneNode()
        {
            if (!IsVisible) return;
            if (CurrentMesh != null)
                for (int i = 0; i < Materials.Count; i++)
                    CSceneManager.Instance.registerNodeForRendering(this, i, Materials[i]);
            base.OnRegisterSceneNode();
        }

        public override void render(int buffer)
        {
            Gfx.drawMesh(CurrentMesh, buffer, AbsoluteTransformation, Materials[buffer], null);
        }
    }

    public class CSkinnedMeshSceneNode : CColladaMeshSceneNode
    {
        public BdaeSkin Skin;
        ISceneNode[] m_joints;
        readonly Matrix4x4[] m_palette = new Matrix4x4[64];

        public override ESCENE_NODE_TYPE getType() { return ESCENE_NODE_TYPE.ESNT_COLLADA_SKIN_MESH; }

        void ResolveJoints()
        {
            m_joints = new ISceneNode[Skin.Joints.Length];
            for (int i = 0; i < Skin.Joints.Length; i++) m_joints[i] = Root.getSceneNodeFromScopeID(Skin.Joints[i]);
        }

        public override void render(int buffer)
        {
            if (m_joints == null) ResolveJoints();
            int n = Math.Min(64, m_joints.Length);
            for (int i = 0; i < n; i++)
            {
                matrix4 jm = m_joints[i] != null ? m_joints[i].AbsoluteTransformation : matrix4.Identity;
                m_palette[i] = (jm * Skin.BindPoses[i] * Skin.BindShapeMatrix).m;
            }
            Gfx.drawMesh(CurrentMesh, buffer, AbsoluteTransformation, Materials[buffer], m_palette);
        }

        public override aabbox3df getBoundingBox()
        {
            // Skinned vertices are in world space; approximate with the joint positions.
            if (m_joints == null) ResolveJoints();
            aabbox3df box = new aabbox3df(getAbsolutePosition());
            foreach (ISceneNode j in m_joints) if (j != null) box.addInternalPoint(j.getAbsolutePosition());
            matrix4 inv;
            if (AbsoluteTransformation.getInverse(out inv)) inv.transformBoxEx(ref box);
            return box;
        }
    }

    public class CColladaMorphingMeshSceneNode : CColladaMeshSceneNode
    {
        public BdaeMorph Morph;
        public string ControllerId;
        public BdaeGeometry[] Targets; // [0] = source
        public float[] Weights;
        float[] m_lastWeights;
        Mesh m_mesh;
        v3f[] m_work;

        public override ESCENE_NODE_TYPE getType() { return ESCENE_NODE_TYPE.ESNT_COLLADA_MORPH_MESH; }
        public override string getUID() { return ControllerId; }
        public override Mesh CurrentMesh { get { UpdateMorph(); return m_mesh; } }

        public void Init()
        {
            Weights = new float[Targets.Length];
            Weights[0] = 1;
            for (int i = 1; i < Targets.Length && i - 1 < Morph.Weights.Length; i++) Weights[i] = Morph.Weights[i - 1];
            m_mesh = ColladaMeshData.BuildMesh(Targets[0], null, null);
            m_mesh.MarkDynamic();
            m_work = new v3f[Targets[0].VertexCount];
        }

        void UpdateMorph()
        {
            Weights[0] = 1.0f;
            if (!Morph.Relative) for (int i = 1; i < Weights.Length; i++) Weights[0] -= Weights[i];
            bool changed = m_lastWeights == null;
            if (!changed) for (int i = 0; i < Weights.Length; i++) if (Weights[i] != m_lastWeights[i]) { changed = true; break; }
            if (!changed) return;
            m_lastWeights = (float[])Weights.Clone();
            int n = m_work.Length;
            for (int v = 0; v < n; v++) m_work[v] = v3f.Zero;
            for (int t = 0; t < Targets.Length; t++)
            {
                float w = Weights[t];
                if (w == 0 || Targets[t].Positions == null) continue;
                v3f[] p = Targets[t].Positions;
                for (int v = 0; v < n && v < p.Length; v++) m_work[v] += p[v] * w;
            }
            Vector3[] verts = new Vector3[n];
            for (int v = 0; v < n; v++) verts[v] = m_work[v];
            m_mesh.vertices = verts;
        }
    }

    /// <summary>Root of a loaded .bdae scene; owns the animator and resolves animation targets.</summary>
    public class CColladaRootSceneNode : ISceneNode
    {
        public BdaeFile File;
        public readonly Dictionary<string, List<SMaterial>> EffectMaterials = new Dictionary<string, List<SMaterial>>();
        public readonly List<CColladaMorphingMeshSceneNode> MorphMeshes = new List<CColladaMorphingMeshSceneNode>();
        public override ESCENE_NODE_TYPE getType() { return ESCENE_NODE_TYPE.ESNT_COLLADA_ROOT; }

        public CColladaMorphingMeshSceneNode getMorphingMesh(string uid)
        {
            foreach (CColladaMorphingMeshSceneNode m in MorphMeshes) if (m.ControllerId == uid) return m;
            return null;
        }
    }

    // ---------------------------------------------------------------------------- animation

    public delegate void TimelineCallback(CTimelineController ctrl, object userData);
    public struct STriggeredEvent { public int elapsedTime; public string name; }
    public delegate void EventsCallback(STriggeredEvent ev, object userData);

    public class CTimelineController
    {
        public bool IsLooping;
        float m_localStartTime, m_localTimeRange, m_localTime, m_lastTime, m_scale = 1.0f;
        List<BdaeClip> m_clips;
        int m_currentClip;
        bool m_bCallback, m_bLastTime;
        int m_start, m_end, m_ctrlTime;
        TimelineCallback m_callback;
        object m_callbackData;

        public void setCallback(TimelineCallback cb, object data) { m_callback = cb; m_callbackData = data; }
        void invokeCallback() { if (m_callback != null) m_callback(this, m_callbackData); }

        public int getCtrlTime() { return m_ctrlTime; }
        public int getStart() { return m_start; }
        public int getEnd() { return m_end; }
        public int getLength() { return m_end - m_start; }
        public float getScale() { return m_scale; }
        public void setScale(float s) { m_scale = s; }
        public bool getLoop() { return IsLooping; }
        public void setLoop(bool l) { IsLooping = l; }

        public void setAnimationClips(List<BdaeClip> clips)
        {
            m_clips = clips;
            if (m_clips != null && m_clips.Count > 0) setClip(0);
            else { m_start = 0; m_end = 1; }
        }

        public void setRange(int start, int end)
        {
            if (m_clips == null || m_clips.Count == 0)
            {
                m_start = start; m_end = end;
                m_localStartTime = m_start / 1000.0f;
                m_localTimeRange = (m_end - m_start) / 1000.0f;
            }
            jumpTo(m_start);
        }

        public void jumpTo(int timeMs)
        {
            m_ctrlTime = timeMs;
            m_localTime = timeMs / 1000.0f;
            m_bCallback = false;
            m_bLastTime = false;
        }

        public void update(int timeMs)
        {
            float time = timeMs / 1000.0f;
            float dt = (time - m_lastTime) * m_scale;
            float endTime = m_end / 1000.0f;
            if (!m_bLastTime) { dt = 0; m_bLastTime = true; }
            m_localTime += dt;
            m_lastTime = time;
            if (m_localTime > endTime)
            {
                if (IsLooping)
                {
                    float forwardTime = 0;
                    if (m_localTimeRange != 0) forwardTime = (m_localTime - endTime) % m_localTimeRange;
                    m_localTime = m_localStartTime + forwardTime;
                    invokeCallback();
                }
                else
                {
                    m_localTime = endTime;
                    if (!m_bCallback) { m_bCallback = true; invokeCallback(); }
                }
            }
            m_ctrlTime = (int)(m_localTime * 1000 + 0.5f);
        }

        public int setClip(string name)
        {
            int idx = getClipIndex(name);
            if (idx >= 0) setClip(idx);
            return idx;
        }

        public void setClip(int idx)
        {
            if (m_clips == null || idx < 0 || idx >= m_clips.Count) return;
            m_currentClip = idx;
            m_bCallback = false;
            m_start = m_clips[idx].Start;
            m_end = m_clips[idx].End;
            m_localStartTime = m_start / 1000.0f;
            m_localTimeRange = (m_end - m_start) / 1000.0f;
            m_localTime = m_localStartTime;
            m_ctrlTime = m_start;
            m_bLastTime = false;
        }

        public int getClipIndex(string name)
        {
            if (m_clips == null || name == null) return -1;
            for (int i = 0; i < m_clips.Count; i++)
                if (string.Equals(m_clips[i].Id, name, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        public int getClipCount() { return m_clips != null ? m_clips.Count : 0; }
        public string getClipName(int idx) { return m_clips != null && idx >= 0 && idx < m_clips.Count ? m_clips[idx].Id : null; }
        public int getClipStart(int idx) { return m_clips[idx].Start; }
        public int getClipEnd(int idx) { return m_clips[idx].End; }
        public int getClipLength(int idx) { return m_clips[idx].End - m_clips[idx].Start; }
        public int getCurrentClipIndex() { return m_currentClip; }
        public int getCurrentClipStart() { return m_clips != null && m_clips.Count > 0 ? m_clips[m_currentClip].Start : m_start; }
        public int getCurrentClipEnd() { return m_clips != null && m_clips.Count > 0 ? m_clips[m_currentClip].End : m_end; }
        public int getCurrentClipLength() { return getCurrentClipEnd() - getCurrentClipStart(); }
    }

    public class ISceneNodeAnimator
    {
        public enum E_INTERPOLATION_MODE { EIT_DEFAULT, EIT_STEP, EIT_LINEAR }
        protected CTimelineController m_timeline;
        public E_INTERPOLATION_MODE InterpolationType = E_INTERPOLATION_MODE.EIT_DEFAULT;
        public virtual void animateNode(ISceneNode node, int timeMs) { }
        public virtual void onBind(ISceneNode node) { }
        public CTimelineController getTimelineCtrl() { return m_timeline; }
        public void setInterpolationMode(E_INTERPOLATION_MODE m) { InterpolationType = m; }
        public E_INTERPOLATION_MODE getInterpolationMode() { return InterpolationType; }
        public virtual void setEventsCallback(EventsCallback cb, object userData = null) { }
    }

    /// <summary>collada::CSceneNodeAnimator: applies every track of a .bdae to its bound targets.</summary>
    public class CColladaSceneNodeAnimator : ISceneNodeAnimator
    {
        class Binding
        {
            public BdaeAnimation Anim;
            public int Channel;          // index of the first channel
            public ISceneNode Node;      // transform targets
            public List<SMaterial> Materials; // texture transform targets
            public CColladaMorphingMeshSceneNode Morph;
            public int MorphIndex;
        }

        readonly BdaeFile m_file;
        readonly List<Binding> m_bindings = new List<Binding>();
        ISceneNode m_bindTo;
        EventsCallback m_eventsCallback;
        object m_eventsUserData;
        int m_lengthMs;

        public CColladaSceneNodeAnimator(BdaeFile file)
        {
            m_file = file;
            m_timeline = new CTimelineController();
            if (file.Clips.Count > 0) m_timeline.setAnimationClips(file.Clips);
            int start = int.MaxValue, end = int.MinValue;
            foreach (BdaeAnimation a in file.Animations)
                foreach (BdaeSampler s in a.Samplers)
                    if (s.Times.Length > 0)
                    {
                        start = Math.Min(start, s.Times[0]);
                        end = Math.Max(end, s.Times[s.Times.Length - 1]);
                    }
            if (start == int.MaxValue) { start = 0; end = 0; }
            m_lengthMs = Math.Max(1, end - start);
            m_timeline.setRange(start, end);
        }

        public override void setEventsCallback(EventsCallback cb, object userData = null)
        {
            m_eventsCallback = cb;
            m_eventsUserData = userData;
        }

        public override void onBind(ISceneNode node)
        {
            if (node == m_bindTo) return;
            m_bindTo = node;
            m_bindings.Clear();
            CColladaRootSceneNode root = node as CColladaRootSceneNode;
            if (root == null) return;
            foreach (BdaeAnimation a in m_file.Animations)
            {
                if (a.Channels.Count == 0 || a.Samplers.Count == 0) continue;
                BdaeChannel ch = a.Channels[0];
                Binding b = new Binding { Anim = a, Channel = 0 };
                if ((ch.Type & 0x100) != 0)
                {
                    List<SMaterial> mats;
                    if (root.EffectMaterials.TryGetValue(ch.Target, out mats)) b.Materials = mats;
                    else continue;
                }
                else if (ch.Type == 14)
                {
                    b.Morph = root.getMorphingMesh(ch.Target);
                    b.MorphIndex = ch.Reserved0 + 1;
                    if (b.Morph == null) continue;
                }
                else
                {
                    b.Node = root.getSceneNodeFromUID(ch.Target);
                    if (b.Node == null) continue;
                }
                m_bindings.Add(b);
            }
        }

        static bool findKeyFrameNo(int[] input, int timeMs, out int outFrame)
        {
            int left = 1, right = input.Length - 1;
            while (left <= right)
            {
                int m = (left + right) >> 1;
                if (timeMs < input[m]) right = m - 1;
                else left = m + 1;
            }
            outFrame = right;
            if (outFrame < 0) outFrame = 0;
            return !((timeMs == input[outFrame]) || (outFrame == input.Length - 1));
        }

        static float Sample1(BdaeSampler s, int timeMs, bool interpolate, int width, int comp)
        {
            int frame;
            bool between = findKeyFrameNo(s.Times, timeMs, out frame) && interpolate && s.Interpolation != 0;
            float v0 = s.Values[frame * width + comp];
            if (!between) return v0;
            float ratio = IrrMath.clamp((float)(timeMs - s.Times[frame]) / (s.Times[frame + 1] - s.Times[frame]), 0, 1);
            return IrrMath.lerp(v0, s.Values[(frame + 1) * width + comp], ratio);
        }

        public override void animateNode(ISceneNode node, int timeMs)
        {
            int before = m_timeline.getCtrlTime();
            m_timeline.update(timeMs);
            int t = m_timeline.getCtrlTime();
            DispatchEvents(before, t, m_timeline.getCurrentClipStart(), m_timeline.getCurrentClipEnd());
            bool interpolate = InterpolationType != E_INTERPOLATION_MODE.EIT_STEP;
            for (int i = 0; i < m_bindings.Count; i++) Apply(m_bindings[i], t, interpolate);
        }

        void Apply(Binding b, int t, bool interpolate)
        {
            BdaeAnimation a = b.Anim;
            BdaeChannel ch = a.Channels[0];
            BdaeSampler s = a.Samplers[0];
            if (s.Times.Length == 0) return;
            switch (ch.Type)
            {
                case 1: // CT_POSITION
                    b.Node.RelativeTranslation = new v3f(Sample1(s, t, interpolate, 3, 0), Sample1(s, t, interpolate, 3, 1), Sample1(s, t, interpolate, 3, 2));
                    break;
                case 2: b.Node.RelativeTranslation.X = Sample1(s, t, interpolate, 1, 0); break;
                case 3: b.Node.RelativeTranslation.Y = Sample1(s, t, interpolate, 1, 0); break;
                case 4: b.Node.RelativeTranslation.Z = Sample1(s, t, interpolate, 1, 0); break;
                case 5: // CT_ROTATION
                {
                    int frame;
                    bool between = findKeyFrameNo(s.Times, t, out frame) && interpolate && s.Interpolation != 0;
                    quaternion k0 = new quaternion(s.Values[frame * 4], s.Values[frame * 4 + 1], s.Values[frame * 4 + 2], s.Values[frame * 4 + 3]);
                    if (between)
                    {
                        quaternion k1 = new quaternion(s.Values[frame * 4 + 4], s.Values[frame * 4 + 5], s.Values[frame * 4 + 6], s.Values[frame * 4 + 7]);
                        float ratio = IrrMath.clamp((float)(t - s.Times[frame]) / (s.Times[frame + 1] - s.Times[frame]), 0, 1);
                        quaternion o = new quaternion();
                        o.slerp(k0, k1, ratio);
                        b.Node.RelativeRotation = o;
                    }
                    else b.Node.RelativeRotation = k0;
                    break;
                }
                case 9: // CT_ROTATION_W: angle around the current axis
                {
                    float angle = Sample1(s, t, interpolate, 1, 0);
                    quaternion q = b.Node.RelativeRotation;
                    float oldAngle; v3f axis;
                    q.toAngleAxis(out oldAngle, out axis);
                    q.fromAngleAxis(angle, axis);
                    b.Node.RelativeRotation = q;
                    break;
                }
                case 10: // CT_SCALE
                    b.Node.RelativeScale = new v3f(Sample1(s, t, interpolate, 3, 0), Sample1(s, t, interpolate, 3, 1), Sample1(s, t, interpolate, 3, 2));
                    break;
                case 11: b.Node.RelativeScale.X = Sample1(s, t, interpolate, 1, 0); break;
                case 12: b.Node.RelativeScale.Y = Sample1(s, t, interpolate, 1, 0); break;
                case 13: b.Node.RelativeScale.Z = Sample1(s, t, interpolate, 1, 0); break;
                case 14: // CT_MORPHING_WEIGHT
                    if (b.MorphIndex < b.Morph.Weights.Length) b.Morph.Weights[b.MorphIndex] = Sample1(s, t, interpolate, 1, 0);
                    break;
                case 20: // CT_VISIBILITY
                    b.Node.IsVisible = Sample1(s, t, false, 1, 0) != 0;
                    break;
                default:
                    if ((ch.Type & 0x100) != 0) ApplyTextureTransform(b, t, interpolate);
                    break;
            }
        }

        void ApplyTextureTransform(Binding b, int t, bool interpolate)
        {
            BdaeAnimation a = b.Anim;
            float[] dflt = a.TextureDefaults ?? new float[] { 0, 0, 0, 1, 1 };
            float offU = dflt[0], offV = dflt[1], rot = dflt[2], scU = dflt[3], scV = dflt[4];
            for (int k = 0; k < a.Channels.Count && k < a.Samplers.Count; k++)
            {
                BdaeSampler s = a.Samplers[k];
                if (s.Times.Length == 0) continue;
                float v = Sample1(s, t, interpolate, 1, 0);
                switch (a.Channels[k].Type & 0xff)
                {
                    case 1: offU = v; break;
                    case 2: offV = v; break;
                    case 3: rot = v; break;
                    case 4: scU = v; break;
                    case 5: scV = v; break;
                }
            }
            matrix4 m = matrix4.Identity.buildTextureTransform(rot / 180.0f * 3.141596f, new v2f(0.5f, 0.5f), new v2f(offU, offV), new v2f(scU, scV));
            foreach (SMaterial mat in b.Materials) mat.TextureMatrix = m;
        }

        int FindEntry(int time)
        {
            List<BdaeEvent> ev = m_file.Events;
            for (int i = 0; i < ev.Count; i++) if (time < ev[i].TimeMs) return i - 1;
            return ev.Count - 1;
        }

        void Dispatch(int from, int to, int currentTime)
        {
            List<BdaeEvent> ev = m_file.Events;
            for (int i = Math.Max(0, from); i <= to && i < ev.Count; i++)
                for (int j = 0; j < ev[i].Names.Length; j++)
                    m_eventsCallback(new STriggeredEvent { elapsedTime = currentTime - ev[i].TimeMs, name = ev[i].Names[j] }, m_eventsUserData);
        }

        void DispatchEvents(int timeFrom, int timeTo, int timeBegin, int timeEnd)
        {
            if (m_file.Events.Count == 0 || timeFrom == timeTo || m_eventsCallback == null) return;
            if (timeFrom <= timeTo)
            {
                Dispatch(FindEntry(timeFrom - 1) + 1, FindEntry(timeTo), timeTo);
            }
            else
            {
                Dispatch(FindEntry(timeFrom - 1) + 1, FindEntry(timeEnd), timeEnd + (timeTo - timeBegin));
                Dispatch(FindEntry(timeBegin - 1) + 1, FindEntry(timeTo), timeTo);
            }
        }
    }

    // ---------------------------------------------------------------------------- loader

    public static class ColladaLoader
    {
        /// <summary>Builds a scene from a .bdae (CColladaDatabase::constructScene + animator).</summary>
        public static ISceneNode Load(string path)
        {
            BdaeFile file;
            try { file = BdaeFile.Load(path); }
            catch (Exception e) { Logs.error("Could not load " + path + ": " + e.Message); return null; }
            CColladaRootSceneNode root = new CColladaRootSceneNode { File = file };
            string dir = GameFiles.GetFileDir(path);
            List<KeyValuePair<CColladaCameraSceneNode, string>> cameraTargets = new List<KeyValuePair<CColladaCameraSceneNode, string>>();
            foreach (BdaeVisualScene vs in file.VisualScenes)
            {
                CColladaSceneNode vsNode = new CColladaSceneNode(null) { Name = vs.Name };
                foreach (BdaeNode n in vs.Nodes) vsNode.addChild(ConstructNode(file, n, root, dir, cameraTargets));
                root.addChild(vsNode);
            }
            foreach (var ct in cameraTargets)
            {
                string id = ct.Value.StartsWith("#") ? ct.Value.Substring(1) : ct.Value;
                ct.Key.TargetNode = root.getSceneNodeFromUID(id);
            }
            CColladaSceneNodeAnimator anim = new CColladaSceneNodeAnimator(file);
            root.addAnimator(anim);
            root.updateAbsolutePosition(true);
            return root;
        }

        static ISceneNode ConstructNode(BdaeFile file, BdaeNode node, CColladaRootSceneNode root, string dir, List<KeyValuePair<CColladaCameraSceneNode, string>> cameraTargets)
        {
            CColladaSceneNode sceneNode = new CColladaSceneNode(node);
            foreach (BdaeInstance inst in node.Instances)
            {
                switch (inst.Type)
                {
                    case BdaeFile.IT_Light:
                        sceneNode.addChild(new CLightSceneNode());
                        break;
                    case BdaeFile.IT_Camera:
                        if (inst.Camera != null)
                        {
                            CColladaCameraSceneNode cam = new CColladaCameraSceneNode(inst.Camera, file.UpAxis);
                            sceneNode.addChild(cam);
                            if (!string.IsNullOrEmpty(inst.Camera.Target)) cameraTargets.Add(new KeyValuePair<CColladaCameraSceneNode, string>(cam, inst.Camera.Target));
                        }
                        break;
                    case BdaeFile.IT_Geometry:
                        if (inst.Geometry != null && inst.Geometry.Type == 0)
                        {
                            CColladaMeshSceneNode mn = new CColladaMeshSceneNode { Root = root };
                            mn.Data = ColladaMeshData.Get(file, inst.Geometry, null);
                            BuildMaterials(file, inst, inst.Geometry, mn, root, dir);
                            mn.RenderingLayer = inst.DrawLayer;
                            sceneNode.addChild(mn);
                        }
                        break;
                    case BdaeFile.IT_Controller:
                        if (inst.Controller != null)
                        {
                            BdaeController c = inst.Controller;
                            if (c.Type == 0 && c.Skin != null)
                            {
                                BdaeGeometry g = file.FindGeometry(c.Skin.Source);
                                if (g == null) break;
                                CSkinnedMeshSceneNode sn = new CSkinnedMeshSceneNode { Root = root, Skin = c.Skin };
                                sn.Data = ColladaMeshData.Get(file, g, c.Skin);
                                BuildMaterials(file, inst, g, sn, root, dir);
                                sn.RenderingLayer = inst.DrawLayer;
                                sceneNode.addChild(sn);
                            }
                            else if (c.Type == 1 && c.Morph != null)
                            {
                                BdaeGeometry src = file.FindGeometry(c.Morph.Source);
                                if (src == null) break;
                                CColladaMorphingMeshSceneNode mm = new CColladaMorphingMeshSceneNode { Root = root, Morph = c.Morph, ControllerId = c.Id };
                                List<BdaeGeometry> targets = new List<BdaeGeometry> { src };
                                foreach (string t in c.Morph.Targets)
                                {
                                    BdaeGeometry tg = file.FindGeometry(t);
                                    if (tg != null) targets.Add(tg);
                                }
                                mm.Targets = targets.ToArray();
                                mm.Init();
                                mm.Data = new ColladaMeshData { Geometry = src, Box = src.Box };
                                BuildMaterials(file, inst, src, mm, root, dir);
                                mm.RenderingLayer = inst.DrawLayer;
                                root.MorphMeshes.Add(mm);
                                sceneNode.addChild(mm);
                            }
                        }
                        break;
                }
            }
            foreach (BdaeNode child in node.Children) sceneNode.addChild(ConstructNode(file, child, root, dir, cameraTargets));
            return sceneNode;
        }

        static void BuildMaterials(BdaeFile file, BdaeInstance inst, BdaeGeometry g, CColladaMeshSceneNode node, CColladaRootSceneNode root, string dir)
        {
            foreach (BdaeMeshBuffer b in g.Buffers)
            {
                string matId = b.Material;
                foreach (BdaeInstanceMaterial im in inst.Materials)
                    if (!string.IsNullOrEmpty(im.Symbol) && im.Symbol == b.Material) { matId = im.Target; break; }
                BdaeMaterial bm = file.FindMaterial(matId);
                if (bm == null && inst.Materials.Count == 1) bm = file.FindMaterial(inst.Materials[0].Target);
                SMaterial mat = PrepareMaterial(bm, dir);
                node.Materials.Add(mat);
                if (bm != null && bm.Effect != null)
                {
                    List<SMaterial> list;
                    if (!root.EffectMaterials.TryGetValue(bm.Effect.Id, out list)) root.EffectMaterials[bm.Effect.Id] = list = new List<SMaterial>();
                    list.Add(mat);
                }
            }
        }

        static ITexture ImageTexture(BdaeImage img, string dir)
        {
            if (img == null) return null;
            string file = img.Path.Replace('\\', '/');
            int slash = file.LastIndexOf('/');
            if (slash >= 0) file = file.Substring(slash + 1);
            return TextureCache.getTexture(dir + "/" + file, true, true);
        }

        /// <summary>collada::CMaterial::prepareMaterial.</summary>
        static SMaterial PrepareMaterial(BdaeMaterial bm, string dir)
        {
            SMaterial mat = new SMaterial();
            if (bm == null) return mat;
            BdaeEffect fx = bm.Effect;
            mat.RenderPriority = bm.RenderPriority;
            if (fx != null)
            {
                if (fx.DiffuseTextures != null)
                {
                    for (int j = 0; j < fx.DiffuseTextures.Count && j < 2; j++)
                    {
                        BdaeTextureRef tr = fx.DiffuseTextures[j];
                        mat.Textures[j] = ImageTexture(tr.Image, dir);
                        matrix4 tm = matrix4.Identity.buildTextureTransform(tr.RotateUV, new v2f(0, 0), new v2f(tr.OffsetU, tr.OffsetV), new v2f(tr.RepeatU, tr.RepeatV));
                        if (j == 0 && !tm.isIdentity()) mat.TextureMatrix = tm;
                    }
                }
                else if (fx.Diffuse.HasValue)
                {
                    SColor c = fx.Diffuse.Value;
                    mat.DiffuseColor = new SColor((int)(c.A * fx.Transparency), c.R, c.G, c.B);
                }
            }
            if (bm.DiffuseSurface != null)
            {
                mat.Textures[0] = ImageTexture(bm.DiffuseSurface, dir);
                mat.DiffuseColor = bm.DiffuseColor;
            }
            mat.BackfaceCulling = bm.BackfaceCulling;
            mat.FrontfaceCulling = bm.FrontfaceCulling;
            if (fx != null)
            {
                if (fx.TransparentIsTexture || bm.RenderSet == 1) mat.MaterialType = E_MATERIAL_TYPE.EMT_TRANSPARENT_ALPHA_CHANNEL;
                else if (fx.Transparency != 1.0f) mat.MaterialType = E_MATERIAL_TYPE.EMT_TRANSPARENT_ALPHA_CHANNEL;
            }
            if (bm.IsAdditive)
            {
                mat.MaterialType = E_MATERIAL_TYPE.EMT_TRANSPARENT_ADD_COLOR;
                mat.DiffuseColor = bm.DiffuseColor;
            }
            if (fx != null)
            {
                mat.Shininess = fx.Shininess;
                if (fx.AmbientTextures != null)
                {
                    mat.MaterialType = E_MATERIAL_TYPE.EMT_LIGHTMAP;
                    if (fx.AmbientTextures.Count > 0) mat.Textures[1] = ImageTexture(fx.AmbientTextures[0].Image, dir);
                }
                else if (fx.Ambient.HasValue) mat.AmbientColor = fx.Ambient.Value;
                if (fx.Emission.HasValue) mat.EmissiveColor = fx.Emission.Value;
                if (fx.Specular.HasValue) mat.SpecularColor = fx.Specular.Value;
                if (fx.DoubleSided) { mat.BackfaceCulling = false; mat.FrontfaceCulling = false; }
                else { mat.BackfaceCulling = bm.BackfaceCulling; mat.FrontfaceCulling = bm.FrontfaceCulling; }
            }
            return mat;
        }
    }

    // ---------------------------------------------------------------------------- scene manager

    public class CSceneManager
    {
        public static CSceneManager Instance { get; private set; }

        public readonly ISceneNode RootSceneNode = new CEmptySceneNode { Name = "root" };
        CCameraSceneNode m_activeCamera;
        float m_sceneTimeMs;
        public SColorf AmbientLight = new SColorf(0, 0, 0, 0);
        readonly List<CLightSceneNode> m_lights = new List<CLightSceneNode>();

        struct RenderItem
        {
            public ISceneNode Node;
            public int Buffer;
            public SMaterial Material;
            public int Layer;
            public float Distance;
            public int Order;
        }

        readonly List<RenderItem> m_solid = new List<RenderItem>();
        readonly List<RenderItem> m_transparent = new List<RenderItem>();
        v3f m_camPos;

        public static void Create() { Instance = new CSceneManager(); }

        public ISceneNode getRootSceneNode() { return RootSceneNode; }
        public CCameraSceneNode getActiveCamera() { return m_activeCamera; }
        public void setActiveCamera(CCameraSceneNode cam) { m_activeCamera = cam; }

        public ISceneNode addEmptySceneNode(ISceneNode parent = null)
        {
            CEmptySceneNode n = new CEmptySceneNode();
            (parent ?? RootSceneNode).addChild(n);
            return n;
        }

        public CCameraSceneNode addCameraSceneNode(ISceneNode parent = null, v3f? position = null, v3f? lookat = null)
        {
            CCameraSceneNode c = new CCameraSceneNode();
            if (position.HasValue) c.setPosition(position.Value);
            if (lookat.HasValue) c.setTarget(lookat.Value);
            (parent ?? RootSceneNode).addChild(c);
            m_activeCamera = c;
            return c;
        }

        public CLightSceneNode addLightSceneNode(ISceneNode parent = null)
        {
            CLightSceneNode l = new CLightSceneNode();
            (parent ?? RootSceneNode).addChild(l);
            return l;
        }

        public ISceneNode getSceneNodeFromName(string name, ISceneNode start = null)
        {
            return (start ?? RootSceneNode).getSceneNodeFromName(name);
        }

        public ISceneNode loadColladaScene(string path) { return ColladaLoader.Load(path); }

        public void update(float deltaMs)
        {
            m_sceneTimeMs += deltaMs;
            RootSceneNode.OnAnimate((int)m_sceneTimeMs);
        }

        public int getSceneTimeMs() { return (int)m_sceneTimeMs; }

        public void registerNodeForRendering(ISceneNode node, int buffer, SMaterial material)
        {
            RenderItem it = new RenderItem
            {
                Node = node, Buffer = buffer, Material = material, Layer = node.RenderingLayer,
                Distance = node.AbsoluteTransformation.getTranslation().getDistanceFromSQ(m_camPos) + node.CameraOffset,
                Order = m_solid.Count + m_transparent.Count
            };
            if (material.isTransparent()) m_transparent.Add(it);
            else m_solid.Add(it);
        }

        void CollectLights(ISceneNode n)
        {
            if (!n.IsVisible) return;
            if (n is CLightSceneNode) m_lights.Add((CLightSceneNode)n);
            foreach (ISceneNode c in n.Children) CollectLights(c);
        }

        /// <summary>Renders the scene from the active camera (CSceneManager::drawAll).</summary>
        public void drawAll(ISceneNode root = null)
        {
            m_solid.Clear();
            m_transparent.Clear();
            if (m_activeCamera != null)
            {
                m_activeCamera.updateView();
                m_camPos = m_activeCamera.getAbsolutePosition();
            }
            (root ?? RootSceneNode).OnRegisterSceneNode();

            // Lighting: the game uses one ambient light (diffuse black).
            m_lights.Clear();
            CollectLights(RootSceneNode);
            SColorf amb = AmbientLight;
            foreach (CLightSceneNode l in m_lights)
            {
                amb.r += l.LightData.AmbientColor.r;
                amb.g += l.LightData.AmbientColor.g;
                amb.b += l.LightData.AmbientColor.b;
            }
            Gfx.LightAmbient = amb;

            if (m_activeCamera != null)
            {
                Gfx.setTransform(E_TRANSFORMATION_STATE.ETS_PROJECTION, m_activeCamera.getProjectionMatrix());
                Gfx.setTransform(E_TRANSFORMATION_STATE.ETS_VIEW, m_activeCamera.getViewMatrix());
            }
            m_solid.Sort((a, b) =>
            {
                if (a.Layer != b.Layer) return b.Layer.CompareTo(a.Layer);
                return a.Order.CompareTo(b.Order);
            });
            m_transparent.Sort((a, b) =>
            {
                if (a.Layer != b.Layer) return b.Layer.CompareTo(a.Layer);
                if (a.Distance != b.Distance) return b.Distance.CompareTo(a.Distance);
                return a.Order.CompareTo(b.Order);
            });
            for (int i = 0; i < m_solid.Count; i++) m_solid[i].Node.render(m_solid[i].Buffer);
            for (int i = 0; i < m_transparent.Count; i++) m_transparent[i].Node.render(m_transparent[i].Buffer);
        }

        // ---------------------------------------------------------------- collision manager

        public line3df getRayFromScreenCoordinates(p2i pos, CCameraSceneNode camera = null)
        {
            line3df ln = new line3df(0, 0, 0, 0, 0, 0);
            if (camera == null) camera = m_activeCamera;
            if (camera == null) return ln;
            camera.updateView();
            SViewFrustum f = camera.getViewFrustum();
            v3f farLeftUp = f.getFarLeftUp();
            v3f lefttoright = f.getFarRightUp() - farLeftUp;
            v3f uptodown = f.getFarLeftDown() - farLeftUp;
            r2i viewPort = Gfx.getViewPort();
            float dx = pos.X / (float)viewPort.getWidth();
            float dy = pos.Y / (float)viewPort.getHeight();
            if (camera.isOrthogonal()) ln.start = f.cameraPosition + (lefttoright * (dx - 0.5f)) + (uptodown * (dy - 0.5f));
            else ln.start = f.cameraPosition;
            ln.end = farLeftUp + (lefttoright * dx) + (uptodown * dy);
            return ln;
        }

        public p2i getScreenCoordinatesFrom3DPosition(v3f pos3d, CCameraSceneNode camera = null)
        {
            if (camera == null) camera = m_activeCamera;
            if (camera == null) return new p2i(-1000, -1000);
            camera.updateView();
            r2i viewPort = Gfx.getViewPort();
            int w = viewPort.getWidth() / 2, h = viewPort.getHeight() / 2;
            matrix4 trans = camera.getProjectionMatrix() * camera.getViewMatrix();
            float x, y, z, ww;
            trans.transformVect4(pos3d, out x, out y, out z, out ww);
            if (ww < 0) return new p2i(-10000, -10000);
            float zDiv = ww == 0.0f ? 1.0f : 1.0f / ww;
            int rx = Round32(w * x * zDiv) + w;
            int ry = h - Round32(h * (y * zDiv));
            return new p2i(rx, ry);
        }

        static int Round32(float x) { return (int)Math.Floor(x + 0.5f); }
    }
}
