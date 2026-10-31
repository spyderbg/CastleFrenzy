// Editable model of a Castle Frenzy sprite YAML file (the format AuroraMX edits and the game loads):
// images, modules, frames (parts + collision rects) and animations (steps).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CastleFrenzy.EditorTools
{
    [Serializable]
    public class SpriteImageDef
    {
        public string id = "0x0000", file = "", transparent = "", alphaFile = "";
        public SpriteImageDef Clone() { return (SpriteImageDef)MemberwiseClone(); }
    }

    [Serializable]
    public class SpriteModuleDef
    {
        public string id = "0x1000", type = "MD_IMAGE", image = "0x0000";
        public int x, y, w = 8, h = 8;
        public SpriteModuleDef Clone() { return (SpriteModuleDef)MemberwiseClone(); }
    }

    [Serializable]
    public class SpritePartDef
    {
        public string module = "0x1000";
        public int x, y;
        public bool flipX, flipY, rot90, hyper;

        public SpritePartDef Clone() { return (SpritePartDef)MemberwiseClone(); }

        public int Bits { get { return (flipX ? 1 : 0) | (flipY ? 2 : 0) | (rot90 ? 4 : 0) | (hyper ? 16 : 0); } }
    }

    [Serializable]
    public class SpriteRectDef
    {
        public int x1, y1, x2, y2;
        public SpriteRectDef Clone() { return (SpriteRectDef)MemberwiseClone(); }
    }

    [Serializable]
    public class SpriteFrameDef
    {
        public string id = "0x2000", name = "";
        public List<SpritePartDef> parts = new List<SpritePartDef>();
        public List<SpriteRectDef> rects = new List<SpriteRectDef>();

        public SpriteFrameDef Clone()
        {
            SpriteFrameDef f = new SpriteFrameDef { id = id, name = name };
            foreach (SpritePartDef p in parts) f.parts.Add(p.Clone());
            foreach (SpriteRectDef r in rects) f.rects.Add(r.Clone());
            return f;
        }
    }

    [Serializable]
    public class SpriteStepDef
    {
        public string frame = "0x2000";
        public int time = 1, x, y;
        public bool flipX, flipY, rot90;
        public SpriteStepDef Clone() { return (SpriteStepDef)MemberwiseClone(); }
    }

    [Serializable]
    public class SpriteAnimDef
    {
        public string id = "0x3000", name = "";
        public List<SpriteStepDef> frames = new List<SpriteStepDef>();

        public SpriteAnimDef Clone()
        {
            SpriteAnimDef a = new SpriteAnimDef { id = id, name = name };
            foreach (SpriteStepDef s in frames) a.frames.Add(s.Clone());
            return a;
        }
    }

    [Serializable]
    public class SpriteDocument
    {
        public string header = "";
        public string version = "0001";
        public List<SpriteImageDef> images = new List<SpriteImageDef>();
        public List<SpriteModuleDef> modules = new List<SpriteModuleDef>();
        public List<SpriteFrameDef> frames = new List<SpriteFrameDef>();
        public List<SpriteAnimDef> animations = new List<SpriteAnimDef>();

        public static int ParseId(string s)
        {
            if (string.IsNullOrEmpty(s) || !s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return -1;
            int v;
            return int.TryParse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v) ? v : -1;
        }

        public static string FormatId(int v) { return "0x" + v.ToString("X4", CultureInfo.InvariantCulture); }

        public int ModuleIndex(string id) { return modules.FindIndex(m => m.id == id); }
        public int FrameIndex(string id) { return frames.FindIndex(f => f.id == id); }

        /// <summary>Next free id in a range (modules 0x1000, frames 0x2000, animations 0x3000).</summary>
        public string NextId<T>(List<T> list, Func<T, string> getId, int baseId)
        {
            int max = baseId - 1;
            foreach (T t in list) max = Math.Max(max, ParseId(getId(t)));
            return FormatId(max + 1);
        }

        public static SpriteDocument Parse(string text)
        {
            YamlNode root = MiniYaml.Parse(text);
            YamlNode sprite = root["sprite"] ?? root;
            SpriteDocument d = new SpriteDocument();
            StringBuilder hdr = new StringBuilder();
            foreach (string line in text.Split('\n'))
            {
                string t = line.TrimEnd('\r');
                if (t.StartsWith("#")) hdr.Append(t).Append('\n');
                else break;
            }
            d.header = hdr.ToString();
            d.version = sprite.Str("version", "0001");
            foreach (YamlNode n in sprite.List("images"))
                d.images.Add(new SpriteImageDef { id = n.Str("id", "0x0000"), file = n.Str("file", ""), transparent = n.Str("transparent", ""), alphaFile = n.Str("alphaFile", "") });
            foreach (YamlNode n in sprite.List("modules"))
                d.modules.Add(new SpriteModuleDef { id = n.Str("id"), type = n.Str("type", "MD_IMAGE"), image = n.Str("image", "0x0000"), x = n.Int("x"), y = n.Int("y"), w = n.Int("w"), h = n.Int("h") });
            foreach (YamlNode n in sprite.List("frames"))
            {
                SpriteFrameDef f = new SpriteFrameDef { id = n.Str("id"), name = n.Str("name", "") };
                foreach (YamlNode p in n.List("parts"))
                {
                    SpritePartDef part = new SpritePartDef { module = p.Str("module"), x = p.Int("x"), y = p.Int("y") };
                    foreach (YamlNode fl in p.List("flags"))
                    {
                        if (fl.Value == "FLIP_X") part.flipX = true;
                        else if (fl.Value == "FLIP_Y") part.flipY = true;
                        else if (fl.Value == "ROT_90") part.rot90 = true;
                        else if (fl.Value == "HYPER_FM") part.hyper = true;
                    }
                    f.parts.Add(part);
                }
                foreach (YamlNode r in n.List("rects")) f.rects.Add(new SpriteRectDef { x1 = r.Int("x1"), y1 = r.Int("y1"), x2 = r.Int("x2"), y2 = r.Int("y2") });
                d.frames.Add(f);
            }
            foreach (YamlNode n in sprite.List("animations"))
            {
                SpriteAnimDef a = new SpriteAnimDef { id = n.Str("id"), name = n.Str("name", "") };
                foreach (YamlNode s in n.List("frames"))
                {
                    SpriteStepDef st = new SpriteStepDef { frame = s.Str("frame"), time = s.Int("time"), x = s.Int("x"), y = s.Int("y") };
                    foreach (YamlNode fl in s.List("flags"))
                    {
                        if (fl.Value == "FLIP_X") st.flipX = true;
                        else if (fl.Value == "FLIP_Y") st.flipY = true;
                        else if (fl.Value == "ROT_90") st.rot90 = true;
                    }
                    a.frames.Add(st);
                }
                d.animations.Add(a);
            }
            return d;
        }

        static string Q(string s) { return "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""; }
        static string I(int v) { return v.ToString(CultureInfo.InvariantCulture); }

        static string Flags(bool fx, bool fy, bool r90, bool hyper)
        {
            List<string> f = new List<string>();
            if (fx) f.Add("FLIP_X");
            if (fy) f.Add("FLIP_Y");
            if (r90) f.Add("ROT_90");
            if (hyper) f.Add("HYPER_FM");
            return f.Count == 0 ? "" : ", flags: [" + string.Join(", ", f) + "]";
        }

        /// <summary>Serializes in the layout used by Data/Sprites/*.yaml.</summary>
        public string Serialize()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(header);
            sb.Append("sprite:\n");
            sb.Append("  version: ").Append(Q(version)).Append("\n\n");
            sb.Append("  images:\n");
            foreach (SpriteImageDef i in images)
            {
                sb.Append("    - id: ").Append(Q(i.id)).Append('\n');
                sb.Append("      file: ").Append(Q(i.file)).Append('\n');
                if (!string.IsNullOrEmpty(i.transparent)) sb.Append("      transparent: ").Append(Q(i.transparent)).Append('\n');
                if (!string.IsNullOrEmpty(i.alphaFile)) sb.Append("      alphaFile: ").Append(Q(i.alphaFile)).Append('\n');
            }
            sb.Append('\n');
            if (modules.Count == 0) sb.Append("  modules: []\n");
            else
            {
                sb.Append("  modules:\n");
                foreach (SpriteModuleDef m in modules)
                {
                    sb.Append("    - id: ").Append(Q(m.id)).Append('\n');
                    sb.Append("      type: ").Append(m.type).Append('\n');
                    sb.Append("      image: ").Append(Q(m.image)).Append('\n');
                    sb.Append("      x: ").Append(I(m.x)).Append('\n');
                    sb.Append("      y: ").Append(I(m.y)).Append('\n');
                    sb.Append("      w: ").Append(I(m.w)).Append('\n');
                    sb.Append("      h: ").Append(I(m.h)).Append('\n');
                }
            }
            sb.Append('\n');
            if (frames.Count == 0) sb.Append("  frames: []\n");
            else
            {
                sb.Append("  frames:\n");
                foreach (SpriteFrameDef f in frames)
                {
                    sb.Append("    - id: ").Append(Q(f.id)).Append('\n');
                    sb.Append("      name: ").Append(Q(f.name)).Append('\n');
                    if (f.rects.Count > 0)
                    {
                        sb.Append("      rects:\n");
                        foreach (SpriteRectDef r in f.rects)
                            sb.Append("        - { x1: ").Append(I(r.x1)).Append(", y1: ").Append(I(r.y1)).Append(", x2: ").Append(I(r.x2)).Append(", y2: ").Append(I(r.y2)).Append(" }\n");
                    }
                    if (f.parts.Count == 0) sb.Append("      parts: []\n");
                    else
                    {
                        sb.Append("      parts:\n");
                        foreach (SpritePartDef p in f.parts)
                            sb.Append("        - { module: ").Append(Q(p.module)).Append(", x: ").Append(I(p.x)).Append(", y: ").Append(I(p.y))
                              .Append(Flags(p.flipX, p.flipY, p.rot90, p.hyper)).Append(" }\n");
                    }
                }
            }
            sb.Append('\n');
            if (animations.Count == 0) sb.Append("  animations: []\n");
            else
            {
                sb.Append("  animations:\n");
                foreach (SpriteAnimDef a in animations)
                {
                    sb.Append("    - id: ").Append(Q(a.id)).Append('\n');
                    sb.Append("      name: ").Append(Q(a.name)).Append('\n');
                    if (a.frames.Count == 0) sb.Append("      frames: []\n");
                    else
                    {
                        sb.Append("      frames:\n");
                        foreach (SpriteStepDef s in a.frames)
                            sb.Append("        - { frame: ").Append(Q(s.frame)).Append(", time: ").Append(I(s.time)).Append(", x: ").Append(I(s.x)).Append(", y: ").Append(I(s.y))
                              .Append(Flags(s.flipX, s.flipY, s.rot90, false)).Append(" }\n");
                    }
                }
            }
            return sb.ToString();
        }

        /// <summary>Checks the rules the game's loader enforces (ASprite.Compile).</summary>
        public List<string> Validate()
        {
            List<string> errors = new List<string>();
            if (version != "0001") errors.Add("version must be \"0001\"");
            if (images.Count == 0) errors.Add("a sprite needs an image");
            HashSet<string> imageIds = new HashSet<string>(), moduleIds = new HashSet<string>(), frameIds = new HashSet<string>(), animIds = new HashSet<string>();
            foreach (SpriteImageDef i in images) if (!imageIds.Add(i.id)) errors.Add("duplicate image id " + i.id);
            string atlas = null;
            foreach (SpriteModuleDef m in modules)
            {
                if (ParseId(m.id) < 0) errors.Add("bad module id " + m.id);
                if (!moduleIds.Add(m.id)) errors.Add("duplicate module id " + m.id);
                if (m.type != "MD_IMAGE") errors.Add("module " + m.id + ": only MD_IMAGE is supported");
                if (!imageIds.Contains(m.image)) errors.Add("module " + m.id + " references missing image " + m.image);
                if (atlas == null) atlas = m.image;
                else if (atlas != m.image) errors.Add("the game supports one image atlas per sprite");
                if (m.w < 0 || m.h < 0) errors.Add("module " + m.id + ": size must not be negative");
            }
            foreach (SpriteFrameDef f in frames)
            {
                if (ParseId(f.id) < 0) errors.Add("bad frame id " + f.id);
                if (!frameIds.Add(f.id)) errors.Add("duplicate frame id " + f.id);
            }
            foreach (SpriteFrameDef f in frames)
                foreach (SpritePartDef p in f.parts)
                    if (p.hyper ? !frameIds.Contains(p.module) : !moduleIds.Contains(p.module))
                        errors.Add("frame " + f.id + " references missing " + (p.hyper ? "frame " : "module ") + p.module);
            foreach (SpriteAnimDef a in animations)
            {
                if (ParseId(a.id) < 0) errors.Add("bad animation id " + a.id);
                if (!animIds.Add(a.id)) errors.Add("duplicate animation id " + a.id);
                foreach (SpriteStepDef s in a.frames)
                {
                    if (!frameIds.Contains(s.frame)) errors.Add("animation " + a.id + " references missing frame " + s.frame);
                    if (s.x < -128 || s.x > 127 || s.y < -128 || s.y > 127) errors.Add("animation " + a.id + ": step offsets must fit in a signed byte");
                    if (s.time < 0 || s.time > 255) errors.Add("animation " + a.id + ": step time must be 0..255");
                }
            }
            return errors;
        }
    }
}
