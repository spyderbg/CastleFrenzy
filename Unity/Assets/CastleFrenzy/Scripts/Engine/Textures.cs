using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CastleFrenzy
{
    /// <summary>A loaded texture (irr::video::ITexture). Size is the original image size.</summary>
    public class ITexture
    {
        public readonly string Name;
        public Texture2D Tex;
        public int RefCount = 1;

        public ITexture(string name, Texture2D tex)
        {
            Name = name;
            Tex = tex;
        }

        public d2i getSize() { return new d2i(Tex.width, Tex.height); }
        public d2i getOriginalSize() { return getSize(); }
        public string getName() { return Name; }

        public void setMinFilter(bool linear) { Tex.filterMode = linear ? FilterMode.Bilinear : FilterMode.Point; }
        public void setMagFilter(bool linear) { setMinFilter(linear); }
        public void grab() { RefCount++; }
        public void drop() { RefCount--; }
    }

    /// <summary>Texture loading from the game data folder (PNG/TGA/BMP names resolve to PNG).</summary>
    public static class TextureCache
    {
        static readonly Dictionary<string, ITexture> s_textures = new Dictionary<string, ITexture>();
        static ITexture s_white;

        public static ITexture White
        {
            get
            {
                if (s_white == null)
                {
                    Texture2D t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    t.SetPixel(0, 0, Color.white);
                    t.Apply();
                    s_white = new ITexture("#white", t);
                }
                return s_white;
            }
        }

        static string Key(string name) { return name.Replace('\\', '/').ToLowerInvariant(); }

        public static ITexture findTexture(string name)
        {
            ITexture t;
            return s_textures.TryGetValue(Key(name), out t) ? t : null;
        }

        /// <summary>Resolves an image path relative to the data root, preferring PNG variants.</summary>
        public static string ResolveImage(string path)
        {
            path = path.Replace('\\', '/');
            int dot = path.LastIndexOf('.');
            int slash = path.LastIndexOf('/');
            string stem = dot > slash ? path.Substring(0, dot) : path;
            foreach (string ext in new[] { ".png", ".PNG", ".tga", ".bmp" })
            {
                string candidate = stem + ext;
                if (GameFiles.Exists(candidate)) return candidate;
            }
            return GameFiles.Exists(path) ? path : null;
        }

        public static Texture2D LoadTexture2D(string resolvedPath, bool linear, bool repeat)
        {
            byte[] bytes = GameFiles.ReadAllBytes(resolvedPath);
            if (bytes == null) return null;
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes, false))
            {
                Logs.error("Could not decode image " + resolvedPath);
                DestroyTexture(tex);
                return null;
            }
            tex.name = resolvedPath;
            tex.filterMode = linear ? FilterMode.Bilinear : FilterMode.Point;
            tex.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            return tex;
        }

        /// <summary>driver->getTexture(name): loads once and caches.</summary>
        public static ITexture getTexture(string name, bool linear = true, bool repeat = true)
        {
            if (string.IsNullOrEmpty(name)) return null;
            ITexture t = findTexture(name);
            if (t != null) return t;
            string path = ResolveImage(name);
            if (path == null)
            {
                Logs.error("Missing texture: " + name);
                return null;
            }
            Texture2D tex = LoadTexture2D(path, linear, repeat);
            if (tex == null) return null;
            t = new ITexture(name, tex);
            s_textures[Key(name)] = t;
            return t;
        }

        public static ITexture addTexture(string name, Texture2D tex)
        {
            ITexture t = new ITexture(name, tex);
            s_textures[Key(name)] = t;
            return t;
        }

        public static void removeTexture(ITexture t)
        {
            if (t == null) return;
            string k = Key(t.Name);
            ITexture cur;
            if (s_textures.TryGetValue(k, out cur) && cur == t)
            {
                s_textures.Remove(k);
                if (t.Tex != null) DestroyTexture(t.Tex);
            }
        }

        public static int getTextureCount() { return s_textures.Count; }

        /// <summary>Destroy that also works in edit mode (editor tools).</summary>
        public static void DestroyTexture(Object t)
        {
            if (t == null) return;
            if (Application.isPlaying) Object.Destroy(t);
            else Object.DestroyImmediate(t);
        }
    }

    /// <summary>Access to the game data copied into StreamingAssets/GameData.</summary>
    public static class GameFiles
    {
        static string s_root;

        public static string Root
        {
            get
            {
                if (s_root == null)
                    s_root = Path.Combine(Application.streamingAssetsPath, "GameData").Replace('\\', '/');
                return s_root;
            }
            set { s_root = value; }
        }

        public static string Full(string relative)
        {
            relative = relative.Replace('\\', '/');
            if (relative.StartsWith("./")) relative = relative.Substring(2);
            if (Path.IsPathRooted(relative)) return relative;
            return Root + "/" + relative;
        }

        public static bool Exists(string relative)
        {
            return File.Exists(Full(relative));
        }

        public static byte[] ReadAllBytes(string relative)
        {
            string p = Full(relative);
            if (!File.Exists(p)) return null;
            return File.ReadAllBytes(p);
        }

        public static string GetFileDir(string relative)
        {
            relative = relative.Replace('\\', '/');
            int slash = relative.LastIndexOf('/');
            return slash < 0 ? "." : relative.Substring(0, slash);
        }

        public static string SavePathOverride;

        /// <summary>Directory for profiles and achievements.</summary>
        public static string SavePath
        {
            get
            {
                string p = SavePathOverride ?? Path.Combine(Application.persistentDataPath, "castle-frenzy").Replace('\\', '/');
                if (!Directory.Exists(p)) Directory.CreateDirectory(p);
                return p + "/";
            }
        }
    }

    public static class Logs
    {
        public static bool Verbose = false;
        public static void error(string msg) { Debug.LogError("[CastleFrenzy] " + msg); }
        public static void warning(string msg) { Debug.LogWarning("[CastleFrenzy] " + msg); }
        public static void info(string msg) { if (Verbose) Debug.Log("[CastleFrenzy] " + msg); }
        public static void trace(string scope, string msg) { if (Verbose) Debug.Log("[" + scope + "] " + msg); }
    }
}
