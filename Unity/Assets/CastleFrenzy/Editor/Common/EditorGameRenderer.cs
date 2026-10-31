// Renders through the game's own Gfx pipeline (sprites, fonts, BDAE scenes) into a RenderTexture,
// so the editor tools preview exactly what the game draws.
using System;
using UnityEngine;

namespace CastleFrenzy.EditorTools
{
    public sealed class EditorGameRenderer : IDisposable
    {
        GameObject m_go;
        Camera m_cam;
        RenderTexture m_rt;

        public RenderTexture Texture { get { return m_rt; } }

        /// <summary>The preview shares the game's static renderer, so it is paused while the game itself runs.</summary>
        public static bool Available { get { return !Application.isPlaying; } }

        public EditorGameRenderer()
        {
            m_go = new GameObject("CastleFrenzy.EditorPreview") { hideFlags = HideFlags.HideAndDontSave };
            m_cam = m_go.AddComponent<Camera>();
            m_cam.enabled = false;
            m_cam.clearFlags = CameraClearFlags.SolidColor;
            m_cam.backgroundColor = Color.black;
            m_cam.cullingMask = 0;
            m_cam.orthographic = true;
        }

        /// <summary>Creates the scene manager the runtime expects (scene graph, cameras, animators).</summary>
        public static void EnsureRuntime()
        {
            if (CSceneManager.Instance == null) CSceneManager.Create();
        }

        public void Render(int width, int height, Action draw)
        {
            if (!Available || width < 8 || height < 8) return;
            if (m_rt == null || m_rt.width != width || m_rt.height != height)
            {
                if (m_rt != null) { m_rt.Release(); UnityEngine.Object.DestroyImmediate(m_rt); }
                m_rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { hideFlags = HideFlags.HideAndDontSave, name = "CastleFrenzy.EditorPreview" };
                m_rt.Create();
            }
            EnsureRuntime();
            m_cam.targetTexture = m_rt;
            Gfx.Init(m_cam);
            Gfx.TargetSize = new Vector2Int(width, height);
            Gfx.BeginFrame();
            try { draw(); }
            catch (Exception e) { Debug.LogException(e); }
            finally
            {
                Gfx.EndFrame();
                m_cam.Render();
                Gfx.TargetSize = null;
            }
        }

        public void Dispose()
        {
            if (m_rt != null) { m_rt.Release(); UnityEngine.Object.DestroyImmediate(m_rt); m_rt = null; }
            if (m_go != null) { UnityEngine.Object.DestroyImmediate(m_go); m_go = null; }
        }
    }
}
