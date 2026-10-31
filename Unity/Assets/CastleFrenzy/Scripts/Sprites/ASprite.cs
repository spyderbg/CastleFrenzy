// Sprites/ASprite: AuroraMX YAML sprites compiled into the same flat module/frame/animation
// arrays the C++ runtime used (GP_Data profile), with identical painting semantics.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CastleFrenzy
{
    public class ASprite
    {
        public const float SPRITE_SCALE_3D = 1 / 43.0f;

        public const int FLAG_FLIP_X = 0x01;
        public const int FLAG_FLIP_Y = 0x02;
        public const int FLAG_ROT_90 = 0x04;
        public const int FLAG_FREE_ROT = 0x08;
        public const int FLAG_USER0 = 0x10;
        public const int FLAG_USER1 = 0x20;
        public const int FLAG_HYPER_FM = 0x10;
        public const int FLAG_INDEX_EX_MASK = 0xC0;
        public const int INDEX_EX_SHIFT = 2;
        public const int FLAG_OFFSET_FM = 0x10;
        public const int FLAG_OFFSET_AF = 0x20;

        public const int BOTTOM = 32, HCENTER = 1, LEFT = 4, RIGHT = 8, TOP = 16, VCENTER = 2;

        // Modules
        public int _nModules;
        public int[] _modules_x, _modules_y, _modules_w, _modules_h;
        // FModules (parts)
        public int nFModules;
        int[] _fm_index, _fm_ox, _fm_oy, _fm_flags;
        // Frames
        public int _nFrames;
        int[] _frames_nfm, _frames_fm_start, _frames_frect_start, _frames_frect_num;
        int[] _frames_rc; // x, y, w, h
        int[] _frame_rects; // x, y, w, h
        // Animation frames
        public int _nAFrames;
        int[] _af_frame, _af_time, _af_ox, _af_oy, _af_flags;
        public int _nAnims;
        int[] _anims_naf, _anims_af_start;

        public string[] FrameNames = new string[0];
        public string[] AnimNames = new string[0];

        public ITexture _modules_image;
        public SMaterial _material;
        public SColor _color = new SColor(0xFFFFFFFF);
        public string SourcePath;

        int _space_width, _line_height, _char_spacing, _line_spacing = 4;
        int _crt_pal;
        public static int _text_w, _text_h;

        // ------------------------------------------------------------------ loading

        public bool Load(string name)
        {
            if (name == null) return false;
            string fileBase = "Sprites/" + name;
            if (fileBase.EndsWith(".yaml") || fileBase.EndsWith(".yml")) return LoadYamlSprite(fileBase);
            foreach (string ext in new[] { ".yaml", ".yml" })
                if (GameFiles.Exists(fileBase + ext)) return LoadYamlSprite(fileBase + ext);
            Logs.error("Missing sprite YAML: " + fileBase + ".yaml");
            return false;
        }

        static int ParseId(string text)
        {
            if (text == null || !text.StartsWith("0x")) throw new YamlException("IDs must be hexadecimal strings, e.g. \"0x1000\"");
            return Convert.ToInt32(text.Substring(2), 16);
        }

        static int ParseFlags(YamlNode node, bool hyperAllowed)
        {
            int bits = 0;
            foreach (YamlNode f in node.List("flags"))
            {
                switch (f.Value)
                {
                    case "FLIP_X": bits |= 1; break;
                    case "FLIP_Y": bits |= 2; break;
                    case "ROT_90": bits |= 4; break;
                    case "HYPER_FM": if (hyperAllowed) bits |= 16; else throw new YamlException("HYPER_FM not allowed here"); break;
                    default: throw new YamlException("Unsupported sprite flag: " + f.Value);
                }
            }
            return bits;
        }

        /// <summary>Compiles the sprite description (same rules as Src/Sprites/SpriteYaml.cpp).</summary>
        public void Compile(YamlNode root, out string image, out string alphaImage, out uint transparent, out bool hasTransparent)
        {
            YamlNode sprite = root["sprite"] ?? root;
            if (sprite.Str("version") != "0001") throw new YamlException("Unsupported sprite version; expected \"0001\"");
            List<YamlNode> images = sprite.List("images"), modules = sprite.List("modules"), frames = sprite.List("frames"), anims = sprite.List("animations");
            if (images.Count == 0 || modules.Count == 0) throw new YamlException("A sprite needs an image and image modules");
            Dictionary<int, int> imageIds = new Dictionary<int, int>(), moduleIds = new Dictionary<int, int>(), frameIds = new Dictionary<int, int>();
            for (int i = 0; i < images.Count; i++) imageIds[ParseId(images[i].Str("id"))] = i;
            for (int i = 0; i < frames.Count; i++) frameIds[ParseId(frames[i].Str("id"))] = i;
            int atlas = -1;
            _nModules = modules.Count;
            _modules_x = new int[_nModules]; _modules_y = new int[_nModules]; _modules_w = new int[_nModules]; _modules_h = new int[_nModules];
            for (int i = 0; i < modules.Count; i++)
            {
                YamlNode m = modules[i];
                moduleIds[ParseId(m.Str("id"))] = i;
                if (m.Str("type") != "MD_IMAGE") throw new YamlException("The game supports MD_IMAGE modules in one external atlas");
                int img = imageIds[ParseId(m.Str("image"))];
                if (atlas < 0) atlas = img;
                if (atlas != img) throw new YamlException("The game supports one image atlas per sprite");
                _modules_x[i] = m.Int("x"); _modules_y[i] = m.Int("y"); _modules_w[i] = m.Int("w"); _modules_h[i] = m.Int("h");
            }
            _nFrames = frames.Count;
            _frames_nfm = new int[_nFrames]; _frames_fm_start = new int[_nFrames];
            _frames_frect_start = new int[_nFrames]; _frames_frect_num = new int[_nFrames];
            _frames_rc = new int[_nFrames * 4];
            FrameNames = new string[_nFrames];
            List<int> fmIndex = new List<int>(), fmOx = new List<int>(), fmOy = new List<int>(), fmFlags = new List<int>();
            List<int> rects = new List<int>();
            for (int i = 0; i < frames.Count; i++)
            {
                YamlNode f = frames[i];
                FrameNames[i] = f.Str("name", "");
                _frames_fm_start[i] = fmIndex.Count;
                foreach (YamlNode p in f.List("parts"))
                {
                    int bits = ParseFlags(p, true);
                    int target = (bits & 16) != 0 ? frameIds[ParseId(p.Str("module"))] : moduleIds[ParseId(p.Str("module"))];
                    fmIndex.Add(target); fmOx.Add(p.Int("x")); fmOy.Add(p.Int("y")); fmFlags.Add(bits);
                }
                _frames_nfm[i] = fmIndex.Count - _frames_fm_start[i];
                _frames_frect_start[i] = rects.Count / 4;
                foreach (YamlNode r in f.List("rects"))
                {
                    int x1 = r.Int("x1"), y1 = r.Int("y1"), x2 = r.Int("x2"), y2 = r.Int("y2");
                    rects.Add(x1); rects.Add(y1); rects.Add(x2 - x1); rects.Add(y2 - y1);
                }
                _frames_frect_num[i] = rects.Count / 4 - _frames_frect_start[i];
            }
            nFModules = fmIndex.Count;
            _fm_index = fmIndex.ToArray(); _fm_ox = fmOx.ToArray(); _fm_oy = fmOy.ToArray(); _fm_flags = fmFlags.ToArray();
            _frame_rects = rects.ToArray();
            // bounds (including transformed hyperframes)
            int[] visited = new int[_nFrames];
            for (int i = 0; i < _nFrames; i++) Bounds(i, 0, visited);

            _nAnims = anims.Count;
            AnimNames = new string[_nAnims];
            _anims_naf = new int[_nAnims]; _anims_af_start = new int[_nAnims];
            List<int> afF = new List<int>(), afT = new List<int>(), afX = new List<int>(), afY = new List<int>(), afFl = new List<int>();
            for (int i = 0; i < anims.Count; i++)
            {
                YamlNode a = anims[i];
                AnimNames[i] = a.Str("name", "");
                _anims_af_start[i] = afF.Count;
                foreach (YamlNode s in a.List("frames"))
                {
                    int frame = frameIds[ParseId(s.Str("frame"))];
                    afF.Add(frame); afT.Add(s.Int("time")); afX.Add((sbyte)s.Int("x")); afY.Add((sbyte)s.Int("y")); afFl.Add(ParseFlags(s, false));
                }
                _anims_naf[i] = afF.Count - _anims_af_start[i];
            }
            _nAFrames = afF.Count;
            _af_frame = afF.ToArray(); _af_time = afT.ToArray(); _af_ox = afX.ToArray(); _af_oy = afY.ToArray(); _af_flags = afFl.ToArray();

            YamlNode imgNode = images[atlas];
            image = imgNode.Str("file");
            alphaImage = imgNode.Str("alphaFile");
            string tr = imgNode.Str("transparent");
            hasTransparent = tr != null;
            transparent = 0;
            if (hasTransparent)
            {
                string t = tr.StartsWith("0x") || tr.StartsWith("0X") ? tr.Substring(2) : tr;
                transparent = Convert.ToUInt32(t, tr.StartsWith("0x") || tr.StartsWith("0X") ? 16 : 10);
            }
        }

        void Bounds(int index, int depth, int[] visited)
        {
            if (depth > 128 || visited[index] == 1) throw new YamlException("Cyclic or excessively nested hyperframes");
            if (visited[index] == 2) return;
            visited[index] = 1;
            bool first = true;
            int bx1 = 0, by1 = 0, bx2 = 0, by2 = 0;
            for (int k = 0; k < _frames_nfm[index]; k++)
            {
                int f = _frames_fm_start[index] + k;
                int px1, py1, px2, py2;
                if ((_fm_flags[f] & 16) != 0)
                {
                    int child = _fm_index[f];
                    Bounds(child, depth + 1, visited);
                    int cx1 = _frames_rc[child * 4], cy1 = _frames_rc[child * 4 + 1];
                    int cx2 = cx1 + _frames_rc[child * 4 + 2], cy2 = cy1 + _frames_rc[child * 4 + 3];
                    int[] xs = { cx1, cx2 }, ys = { cy1, cy2 };
                    px1 = py1 = px2 = py2 = 0;
                    bool fp = true;
                    foreach (int x0 in xs) foreach (int y0 in ys)
                    {
                        int x = (_fm_flags[f] & 1) != 0 ? -x0 : x0, y = (_fm_flags[f] & 2) != 0 ? -y0 : y0;
                        if ((_fm_flags[f] & 4) != 0) { int ox = x; x = -y; y = ox; }
                        x += _fm_ox[f]; y += _fm_oy[f];
                        if (fp) { px1 = px2 = x; py1 = py2 = y; fp = false; }
                        else { px1 = Math.Min(px1, x); py1 = Math.Min(py1, y); px2 = Math.Max(px2, x); py2 = Math.Max(py2, y); }
                    }
                }
                else
                {
                    int w = _modules_w[_fm_index[f]], h = _modules_h[_fm_index[f]];
                    if ((_fm_flags[f] & 4) != 0) { int t = w; w = h; h = t; }
                    px1 = _fm_ox[f]; py1 = _fm_oy[f]; px2 = px1 + w; py2 = py1 + h;
                }
                if (first) { bx1 = px1; by1 = py1; bx2 = px2; by2 = py2; first = false; }
                else { bx1 = Math.Min(bx1, px1); by1 = Math.Min(by1, py1); bx2 = Math.Max(bx2, px2); by2 = Math.Max(by2, py2); }
            }
            _frames_rc[index * 4] = bx1; _frames_rc[index * 4 + 1] = by1;
            _frames_rc[index * 4 + 2] = bx2 - bx1; _frames_rc[index * 4 + 3] = by2 - by1;
            visited[index] = 2;
        }

        static string SpriteImagePath(string spriteName, string image)
        {
            string path = image.Replace('\\', '/');
            if (path.StartsWith("/") || (path.Length > 1 && path[1] == ':')) return path;
            string dir = GameFiles.GetFileDir(spriteName);
            if (path.StartsWith("./")) path = path.Substring(2);
            return dir + "/" + path;
        }

        bool LoadYamlSprite(string spriteName)
        {
            byte[] bytes = GameFiles.ReadAllBytes(spriteName);
            if (bytes == null) return false;
            return LoadYamlText(System.Text.Encoding.UTF8.GetString(bytes), spriteName, true);
        }

        /// <summary>Compiles sprite YAML text; images resolve relative to spriteName (used by the editor tools).</summary>
        public bool LoadYamlText(string text, string spriteName, bool cacheAtlas)
        {
            string image, alphaImage;
            uint transparent;
            bool hasTransparent;
            try
            {
                Compile(MiniYaml.Parse(text), out image, out alphaImage, out transparent, out hasTransparent);
            }
            catch (Exception e)
            {
                Logs.error("Invalid sprite YAML " + spriteName + ": " + e.Message);
                return false;
            }
            SourcePath = spriteName;
            string texture = TextureCache.ResolveImage(SpriteImagePath(spriteName, image));
            if (texture == null) { Logs.error("Missing image for sprite YAML " + spriteName + ": " + image); return false; }
            bool authoringAlpha = hasTransparent || !string.IsNullOrEmpty(alphaImage);
            string textureName = authoringAlpha ? spriteName + "#yaml-atlas" : texture;
            if (authoringAlpha && (!cacheAtlas || TextureCache.findTexture(textureName) == null))
            {
                Texture2D img = TextureCache.LoadTexture2D(texture, false, false);
                if (img == null) return false;
                Color32[] px = img.GetPixels32();
                Color32[] alpha = null;
                if (!string.IsNullOrEmpty(alphaImage))
                {
                    string mask = TextureCache.ResolveImage(SpriteImagePath(spriteName, alphaImage));
                    Texture2D a = mask != null ? TextureCache.LoadTexture2D(mask, false, false) : null;
                    if (a == null || a.width != img.width || a.height != img.height) { Logs.error("Could not load sprite alpha mask: " + spriteName); return false; }
                    alpha = a.GetPixels32();
                    TextureCache.DestroyTexture(a);
                }
                byte tr = (byte)((transparent >> 16) & 0xff), tg = (byte)((transparent >> 8) & 0xff), tb = (byte)(transparent & 0xff);
                for (int i = 0; i < px.Length; i++)
                {
                    Color32 c = px[i];
                    if (alpha != null) c.a = alpha[i].r;
                    if (hasTransparent && c.r == tr && c.g == tg && c.b == tb) c.a = 0;
                    px[i] = c;
                }
                img.SetPixels32(px);
                img.Apply(false);
                ITexture old = TextureCache.findTexture(textureName);
                if (old != null) TextureCache.removeTexture(old);
                TextureCache.addTexture(textureName, img);
            }
            return LoadTexture(textureName);
        }

        public bool LoadTexture(string textureName)
        {
            ITexture t = TextureCache.findTexture(textureName) ?? TextureCache.getTexture(textureName, false, false);
            if (t == null) return false;
            t.grab();
            _modules_image = t;
            if (_material == null)
            {
                _material = new SMaterial();
                _material.Lighting = false;
                _material.MaterialType = E_MATERIAL_TYPE.EMT_TRANSPARENT_ALPHA_CHANNEL;
            }
            _material.Textures[0] = _modules_image;
            _modules_image.setMinFilter(false);
            return true;
        }

        // ------------------------------------------------------------------ accessors

        public int GetAFrameTime(int anim, int aframe)
        {
            int i = _anims_af_start[anim] + aframe;
            if (i < 0 || i >= _nAFrames) return 1;
            return _af_time[i] & 0xFF;
        }

        public int GetAFrames(int anim) { return anim >= 0 && anim < _nAnims ? _anims_naf[anim] & 0xFF : 0; }
        public int GetFModules(int frame) { return _frames_nfm[frame] & 0xFFFF; }
        public int GetModuleWidth(int module) { return _modules_w[module] & 0xFF; }
        public int GetModuleHeight(int module) { return _modules_h[module] & 0xFF; }
        public int GetFrameLeft(int frame) { return _frames_rc[frame * 4]; }
        public int GetFrameTop(int frame) { return _frames_rc[frame * 4 + 1]; }
        public int GetFrameWidth(int frame) { return _frames_rc[frame * 4 + 2]; }
        public int GetFrameHeight(int frame) { return _frames_rc[frame * 4 + 3]; }
        public p2i GetFramePos(int frame) { return new p2i(GetFrameLeft(frame), GetFrameTop(frame)); }
        public d2i GetFrameSize(int frame) { return new d2i(GetFrameWidth(frame), GetFrameHeight(frame)); }
        public r2i GetFrameRect(int frame) { return new r2i(GetFramePos(frame), GetFrameSize(frame)); }

        public int GetFrameRectsNum(int frame)
        {
            if (frame < 0 || frame > GetFramesNum()) return 0;
            if (frame >= _nFrames) return 0;
            return _frames_frect_num[frame];
        }

        public bool GetFrameRect(out r2i rect, int frame, int rect_idx)
        {
            rect = new r2i();
            int n = GetFrameRectsNum(frame);
            if (n == 0 || n <= rect_idx) return false;
            int p = (_frames_frect_start[frame] + rect_idx) * 4;
            rect = new r2i(_frame_rects[p], _frame_rects[p + 1], _frame_rects[p + 2] + _frame_rects[p], _frame_rects[p + 3] + _frame_rects[p + 1]);
            return true;
        }

        public int GetFrames() { return _nFrames; }
        public int GetModulesNum() { return _nModules; }
        public int GetFramesNum() { return _nFrames; }
        public int GetAnimsNum() { return _nAnims; }

        public int GetAnimFrame(int anim, int aframe)
        {
            if (anim < 0 || aframe < 0) return -1;
            int i = _anims_af_start[anim] + aframe;
            if (i >= _nAFrames) return -1;
            return _af_frame[i];
        }

        public int GetFModuleOffX(int frame, int fmodule) { return _fm_ox[_frames_fm_start[frame] + fmodule]; }
        public int GetFModuleOffY(int frame, int fmodule) { return _fm_oy[_frames_fm_start[frame] + fmodule]; }
        public int GetFModuleWidth(int frame, int fmodule) { return _modules_w[_fm_index[_frames_fm_start[frame] + fmodule]]; }
        public int GetFModuleHeight(int frame, int fmodule) { return _modules_h[_fm_index[_frames_fm_start[frame] + fmodule]]; }

        public int FindAnim(string name) { return Array.IndexOf(AnimNames, name); }
        public int FindFrame(string name) { return Array.IndexOf(FrameNames, name); }

        // ------------------------------------------------------------------ painting

        public void PaintAFrame(int anim, int aframe, int posX, int posY, int flags, int hx, int hy, int alpha = 255, bool is3d = false)
        {
            int off = _anims_af_start[anim] + aframe;
            if (off < 0 || off >= _nAFrames) return;
            int frame = _af_frame[off];
            int temp_hx = _af_ox[off], temp_hy = _af_oy[off];
            if ((flags & FLAG_FLIP_X) != 0) hx += temp_hx; else hx -= temp_hx;
            if ((flags & FLAG_FLIP_Y) != 0) hy += temp_hy; else hy -= temp_hy;
            PaintFrame(frame, posX - hx, posY - hy, flags ^ (_af_flags[off] & 0x0F), hx, hy, alpha, 0, 100, 100, is3d);
        }

        public void PaintFrame(int frame, int posX, int posY, int flag_alpha = 255, bool is3d = false)
        {
            PaintFrame(frame, posX, posY, 0, 0, 0, flag_alpha, 0, 100, 100, is3d);
        }

        public void PaintFrame(int frame, int posX, int posY, int flags, int hx, int hy, int alpha = 255, int rotate = 0, int xscale = 100, int yscale = 100, bool is3d = false)
        {
            if (alpha == 0 || frame < 0 || frame >= _nFrames) return;
            int n = _frames_nfm[frame] & 0xFFFF;
            for (int fmodule = 0; fmodule < n; fmodule++)
                PaintFModule(frame, fmodule, posX, posY, flags, hx, hy, alpha, rotate, xscale, yscale, is3d);
        }

        public void PaintFModule(int frame, int fmodule, int posX, int posY, int flags, int hx, int hy, int alpha = 255, int rotate = 0, int xscale = 100, int yscale = 100, bool is3d = false)
        {
            int off = _frames_fm_start[frame] + fmodule;
            int fm_flags = _fm_flags[off];
            int index = _fm_index[off];
            int temp_hx = _fm_ox[off], temp_hy = _fm_oy[off];
            if ((flags & FLAG_FLIP_X) != 0) posX -= temp_hx; else posX += temp_hx;
            if ((flags & FLAG_FLIP_Y) != 0) posY -= temp_hy; else posY += temp_hy;
            if ((fm_flags & FLAG_HYPER_FM) != 0)
            {
                PaintFrame(index, posX, posY, flags ^ (fm_flags & 0x0F), hx, hy, alpha, rotate, xscale, yscale, is3d);
            }
            else
            {
                if ((flags & FLAG_FLIP_X) != 0) posX -= _modules_w[index];
                if ((flags & FLAG_FLIP_Y) != 0) posY -= _modules_h[index];
                PaintModule(index, posX, posY, flags ^ (fm_flags & 0x0F), rotate, xscale, yscale, alpha, is3d);
            }
        }

        static readonly SColor[] s_colors = new SColor[4];
        static readonly S3DVertex[] s_verts = new S3DVertex[4];
        static readonly int[] s_indices = { 0, 2, 1, 1, 2, 3 };

        public void PaintModule(int module, int posX, int posY, int flags, int rotate = 0, int xscale = 100, int yscale = 100, int alpha = 255, bool is3d = false)
        {
            int srcPosX = _modules_x[module], srcPosY = _modules_y[module];
            int sizeX = _modules_w[module], sizeY = _modules_h[module];
            if (sizeX <= 0 || sizeY <= 0 || _modules_image == null) return;
            r2i destRect = new r2i(posX, posY, posX + sizeX, posY + sizeY);
            r2i srcRect = new r2i(srcPosX, srcPosY, srcPosX + sizeX, srcPosY + sizeY);
            r2i? clipRect = s_clipRectStack.Count > 0 ? s_clipRectStack[s_clipRectStack.Count - 1] : (r2i?)null;
            if ((flags & FLAG_FLIP_X) != 0) { int t = srcRect.LowerRightCorner.X; srcRect.LowerRightCorner.X = srcRect.UpperLeftCorner.X; srcRect.UpperLeftCorner.X = t; }
            if ((flags & FLAG_FLIP_Y) != 0) { int t = srcRect.LowerRightCorner.Y; srcRect.LowerRightCorner.Y = srcRect.UpperLeftCorner.Y; srcRect.UpperLeftCorner.Y = t; }
            SColor color = _color;
            color.A = (color.A * alpha) >> 8;
            bool freeRotScale = rotate != 0 || xscale != 100 || yscale != 100;
            if (freeRotScale)
                PushTransform(new v3f(posX, posY, 0), new v3f(xscale / 100f, yscale / 100f, 1.0f), new v3f(0, 0, rotate * IrrMath.DEGTORAD));
            if (is3d)
            {
                float dx1 = posX, dx2 = posX + sizeX, dy1 = posY, dy2 = posY + sizeY;
                float w = _modules_image.Tex.width, h = _modules_image.Tex.height;
                float sx1 = srcRect.UpperLeftCorner.X / w, sx2 = srcRect.LowerRightCorner.X / w;
                float sy1 = srcRect.UpperLeftCorner.Y / h, sy2 = srcRect.LowerRightCorner.Y / h;
                s_verts[0] = new S3DVertex(dx1, dy1, 0, color, sx1, sy1);
                s_verts[1] = new S3DVertex(dx2, dy1, 0, color, sx2, sy1);
                s_verts[2] = new S3DVertex(dx1, dy2, 0, color, sx1, sy2);
                s_verts[3] = new S3DVertex(dx2, dy2, 0, color, sx2, sy2);
                Gfx.drawVertexPrimitiveList(s_verts, 4, s_indices, 6, _material);
            }
            else
            {
                Gfx.set2DMaterial(_material);
                s_colors[0] = s_colors[1] = s_colors[2] = s_colors[3] = color;
                Gfx.draw2DImage(_modules_image, destRect, srcRect, clipRect, s_colors);
            }
            if (freeRotScale) PopTransform();
        }

        // ------------------------------------------------------------------ 3D

        static void SetMatrix(v3f pos, float xs, float ys)
        {
            CCameraSceneNode cam = CSceneManager.Instance.getActiveCamera();
            if (cam == null) return;
            v3f view = cam.getTarget() - cam.getAbsolutePosition();
            matrix4 m1 = matrix4.Identity.buildCameraLookAtMatrix(pos, pos - view, -cam.getUpVector());
            matrix4 m2;
            m1.getInverse(out m2);
            m2.postScale(new v3f(xs, ys, 1.0f));
            Gfx.setTransform(E_TRANSFORMATION_STATE.ETS_WORLD, m2);
        }

        public void PaintAFrame3D(int anim, int aframe, v3f pos, int flags, int hx = 0, int hy = 0, float xscale = SPRITE_SCALE_3D, float yscale = SPRITE_SCALE_3D)
        {
            SetMatrix(pos, xscale, yscale);
            PaintAFrame(anim, aframe, 0, 0, flags, hx, hy, 255, true);
        }

        public void PaintFrame3D(int frame, v3f pos, int flags, int hx = 0, int hy = 0, float xscale = SPRITE_SCALE_3D, float yscale = SPRITE_SCALE_3D)
        {
            SetMatrix(pos, xscale, yscale);
            PaintFrame(frame, 0, 0, flags, hx, hy, 255, 0, 100, 100, true);
        }

        // ------------------------------------------------------------------ legacy string drawing

        public int GetLineSpacing() { return _line_spacing; }
        public void SetLineSpacing(int s) { _line_spacing = s; }
        public void SetCharSpacing(int s) { _char_spacing = s; }
        public void SetCurrentPalette(int pal) { _crt_pal = pal; }
        public int GetCurrentPalette() { return _crt_pal; }

        // ------------------------------------------------------------------ transform & clip stacks

        static readonly List<matrix4> s_transformStack = new List<matrix4>();
        static readonly List<r2i> s_clipRectStack = new List<r2i>();

        public static void PushTransform(v3f pivot, v3f scale, v3f rotation)
        {
            matrix4 m = matrix4.Identity;
            m = m * matrix4.Identity.setTranslation(pivot);
            m = m * matrix4.Identity.setRotationRadians(rotation);
            m = m * matrix4.Identity.setScale(scale);
            m = m * matrix4.Identity.setTranslation(-pivot);
            PushTransform(m);
        }

        public static void PushTranslation(v3f offset) { PushTransform(matrix4.Identity.setTranslation(offset)); }

        public static void PushTransform(matrix4 m)
        {
            matrix4 old = Gfx.getTransform(E_TRANSFORMATION_STATE.ETS_WORLD);
            s_transformStack.Add(old);
            Gfx.setTransform(E_TRANSFORMATION_STATE.ETS_WORLD, old * m);
        }

        public static void PopTransform()
        {
            if (s_transformStack.Count == 0) return;
            matrix4 old = s_transformStack[s_transformStack.Count - 1];
            Gfx.setTransform(E_TRANSFORMATION_STATE.ETS_WORLD, old);
            s_transformStack.RemoveAt(s_transformStack.Count - 1);
        }

        public static void PushClipRect(r2i r)
        {
            if (s_clipRectStack.Count > 0) r.clipAgainst(s_clipRectStack[s_clipRectStack.Count - 1]);
            s_clipRectStack.Add(r);
        }

        public static void PushClipRect(p2i pos, d2i size) { PushClipRect(new r2i(pos, size)); }

        public static void PopClipRect()
        {
            if (s_clipRectStack.Count > 0) s_clipRectStack.RemoveAt(s_clipRectStack.Count - 1);
        }

        public static void ResetStacks() { s_transformStack.Clear(); s_clipRectStack.Clear(); }

        public void Release()
        {
            if (_modules_image != null) { _modules_image.drop(); _modules_image = null; }
        }
    }

    /// <summary>Sprites/ASpriteInstance</summary>
    public class ASpriteInstance
    {
        int x, y;
        int m_nCurrentAnim, m_nCurrentAFrame, m_nLoop, m_nNextAnim;
        float m_nCurrentFTime;
        ASprite m_nCurrentSprite;

        public ASpriteInstance() { Stop(); }

        public void Stop() { m_nCurrentAnim = -1; m_nCurrentAFrame = 0; m_nCurrentFTime = 0; m_nNextAnim = -1; }
        public void SetAnim(int anim, int loop = 0xFFFFFF) { Stop(); m_nCurrentAnim = anim; m_nLoop = loop; }
        public void SetPosition(int px, int py) { x = px; y = py; }
        public void SetASprite(ASprite s) { m_nCurrentSprite = s; }
        public ASprite GetASprite() { return m_nCurrentSprite; }
        public int GetCurrentAnim() { return m_nCurrentAnim; }
        public int GetFrame() { return m_nCurrentAFrame; }
        public int GetX() { return x; }
        public int GetY() { return y; }

        public void StartAnim(ASprite sprite, int anim, int px, int py, int loop = 0xFFFFFF)
        {
            Stop();
            m_nCurrentSprite = sprite; m_nCurrentAnim = anim; x = px; y = py; m_nLoop = loop;
        }

        public void SetTransitionAnim(int anim, int nextAnim) { SetAnim(anim); m_nNextAnim = nextAnim; }

        public void Update(float dt)
        {
            if (m_nCurrentSprite == null || m_nCurrentAnim < 0) return;
            int time = m_nCurrentSprite.GetAFrameTime(m_nCurrentAnim, m_nCurrentAFrame);
            if (time == 0) return;
            m_nCurrentFTime += 25 * dt;
            while (time > 0 && m_nCurrentFTime >= time)
            {
                m_nCurrentFTime -= time;
                m_nCurrentAFrame++;
                if (m_nCurrentAFrame >= m_nCurrentSprite.GetAFrames(m_nCurrentAnim))
                {
                    if (m_nNextAnim >= 0) { SetAnim(m_nNextAnim); return; }
                    m_nLoop--;
                    if (m_nLoop < 0) { Stop(); return; }
                    m_nCurrentAFrame = 0;
                    m_nNextAnim = -1;
                }
                time = m_nCurrentSprite.GetAFrameTime(m_nCurrentAnim, m_nCurrentAFrame);
            }
        }

        public bool IsAnimEnded() { return m_nLoop < 0 && m_nCurrentAFrame == 0 && m_nCurrentFTime == 0; }

        public void Draw(int alpha = 255)
        {
            if (m_nCurrentSprite == null || m_nCurrentAnim < 0) return;
            m_nCurrentSprite.PaintAFrame(m_nCurrentAnim, m_nCurrentAFrame, x, y, 0, 0, 0, alpha);
        }

        public void Draw3D(v3f pos, float scale = 1.0f)
        {
            if (m_nCurrentSprite == null || m_nCurrentAnim < 0) return;
            m_nCurrentSprite.PaintAFrame3D(m_nCurrentAnim, m_nCurrentAFrame, pos, 0, 0, 0, scale * ASprite.SPRITE_SCALE_3D, scale * ASprite.SPRITE_SCALE_3D);
        }
    }

    /// <summary>Sprites/AnimationInstance: CAnimationInstance + CSpriteInstance.</summary>
    public abstract class CAnimationInstance
    {
        public const int ANIM_INSTANCE_LOOP = 1;
        public const int SPRITE_INSTANCE_FLIP_X = 1 << 1;
        public const int SPRITE_INSTANCE_FLIP_Y = 1 << 2;
        public const int ANIM_INSTANCE_OPPOSITE_DIRECTION = 1 << 3;

        protected int m_PosX, m_PosY, m_PosZ;
        protected ushort m_AnimIdx;
        protected int m_Flag;
        protected int m_FPS;
        protected double m_UpdateStep, m_SystemTime, m_LastUpdateTime;
        protected bool m_Playing;

        protected CAnimationInstance(int flags = 0) { m_Flag = flags; }

        public void update(double timestamp)
        {
            m_SystemTime += timestamp;
            if (m_SystemTime < m_LastUpdateTime + m_UpdateStep) return;
            m_LastUpdateTime = m_SystemTime;
            Update();
        }

        public abstract void Render();
        public abstract void Update();
        public virtual short GetCurrAnimIdx() { return unchecked((short)m_AnimIdx); }
        public abstract void StartAnimation(int anim_idx = 0);
        public abstract void StopAnimation();
        public abstract void ResetAnimation();
        public bool IsPlaying() { return m_Playing; }
        public void SetFPS(int fps) { m_FPS = fps; m_UpdateStep = 1000.0 / fps; }
        public int GetFPS() { return m_FPS; }
        public int GetFlags() { return m_Flag; }
        public void SetFlags(int f) { m_Flag |= f; }
        public void UnsetFlags(int f) { m_Flag &= ~f; }
        public void SetPosition(int x, int y, int z) { m_PosX = x; m_PosY = y; m_PosZ = z; }
        public void GetPosition(out int x, out int y, out int z) { x = m_PosX; y = m_PosY; z = m_PosZ; }
    }

    public class CSpriteInstance : CAnimationInstance
    {
        ASprite m_Sprite;
        int m_PlayedFrames;
        int m_AFrame;
        int m_Alpha = 0xFF;
        bool m_EndlessFrame;
        int m_DefaultFrameIdx;
        int m_LastPlayedFrameIdx = -1;

        public CSpriteInstance(ASprite sprite, int flags = 0) : base(flags) { m_Sprite = sprite; SetFPS(25); }

        public CSpriteInstance(ASprite sprite, int posX, int posY, int posZ, int flags = 0) : base(flags)
        {
            m_Sprite = sprite;
            m_PosX = posX; m_PosY = posY; m_PosZ = posZ;
            SetFPS(25);
        }

        public ASprite GetSprite() { return m_Sprite; }
        public override void StopAnimation() { m_Playing = false; }
        public override void ResetAnimation() { m_PlayedFrames = 0; }
        public int GetPlayedFrames() { return m_PlayedFrames; }
        public int GetLastPlayedFrameIdx() { return m_LastPlayedFrameIdx; }
        public void SetAlpha(int a) { m_Alpha = a & 0xff; }
        public int GetAlpha() { return m_Alpha; }
        public void SetDefaultFrame(int frame) { if (m_Sprite == null || m_Sprite.GetFramesNum() < frame) return; m_DefaultFrameIdx = frame; }
        public int GetDefaultFrame() { return m_DefaultFrameIdx; }

        bool IsAnimExists(int anim) { return m_Sprite != null && anim < m_Sprite.GetAnimsNum() && anim >= 0; }

        public override void Update()
        {
            if (!m_Playing || m_Sprite == null) return;
            if (m_Sprite.GetAnimsNum() == 0) { m_Playing = false; return; }
            if (!m_EndlessFrame && m_Sprite.GetAFrames(m_AnimIdx) >= m_AFrame && m_Sprite.GetAFrameTime(m_AnimIdx, m_AFrame) == 0)
            {
                m_EndlessFrame = true;
                m_Playing = false;
                return;
            }
            int frames_num = GetFramesNum();
            if ((m_Flag & ANIM_INSTANCE_OPPOSITE_DIRECTION) != 0)
            {
                if (--m_PlayedFrames <= 0)
                {
                    if ((m_Flag & ANIM_INSTANCE_LOOP) != 0) m_PlayedFrames = GetFramesNum();
                    else { m_Playing = false; m_PlayedFrames = 0; }
                }
            }
            else if (++m_PlayedFrames > frames_num)
            {
                if ((m_Flag & ANIM_INSTANCE_LOOP) != 0) m_PlayedFrames = 1;
                else { m_Playing = false; m_PlayedFrames = frames_num; }
            }
            m_AFrame = 0;
            if (!IsAnimExists(m_AnimIdx)) return;
            int frames = m_Sprite.GetAFrameTime(m_AnimIdx, m_AFrame);
            while (m_PlayedFrames > frames)
            {
                if (m_AFrame + 1 >= m_Sprite.GetAFrames(m_AnimIdx)) break;
                int ft = m_Sprite.GetAFrameTime(m_AnimIdx, ++m_AFrame);
                if (ft == 0) ft = 1;
                frames += ft;
            }
        }

        public override void Render()
        {
            if (m_Sprite == null) return;
            if (m_Sprite.GetAnimsNum() == 0 || m_AnimIdx > m_Sprite.GetAnimsNum() || m_AnimIdx == m_Sprite.GetAnimsNum())
            {
                m_LastPlayedFrameIdx = m_DefaultFrameIdx;
                m_Sprite.PaintFrame(m_DefaultFrameIdx, m_PosX, m_PosY, 0);
            }
            else if (m_AFrame >= 0)
            {
                m_LastPlayedFrameIdx = m_AFrame;
                m_Sprite.PaintAFrame(m_AnimIdx, m_AFrame, m_PosX, m_PosY, m_Flag, 0, 0, m_Alpha);
            }
        }

        public override void StartAnimation(int anim = 0)
        {
            if (!IsAnimExists(anim)) { m_AnimIdx = 0xFFFF; return; }
            m_Playing = true;
            m_LastUpdateTime = 0;
            m_EndlessFrame = false;
            if (anim != m_AnimIdx)
            {
                m_AnimIdx = (ushort)anim;
                m_PlayedFrames = 0;
                m_AFrame = 0;
            }
        }

        public void ChangeAnimation(int anim)
        {
            if (!IsAnimExists(anim)) { m_AnimIdx = 0xFFFF; return; }
            if (anim == m_AnimIdx || m_Sprite == null) return;
            int oldA = m_Sprite.GetAFrames(m_AnimIdx), newA = m_Sprite.GetAFrames(anim);
            if (oldA != newA) return;
            for (int i = 0; i < oldA; i++)
                if (m_Sprite.GetAFrameTime(m_AnimIdx, i) != m_Sprite.GetAFrameTime(anim, i)) return;
            m_AnimIdx = (ushort)anim;
        }

        public int GetFramesNum()
        {
            if (!IsAnimExists(m_AnimIdx)) { m_AnimIdx = 0xFFFF; return 0; }
            int frames_num = 0;
            int frames = m_Sprite.GetAFrames(m_AnimIdx);
            while (frames > 0)
            {
                int ft = m_Sprite.GetAFrameTime(m_AnimIdx, frames - 1);
                if (ft == 0) ft = 1;
                frames_num += ft;
                frames--;
            }
            return frames_num;
        }

        public void SetLastFrame() { m_PlayedFrames = GetFramesNum(); }
    }
}
