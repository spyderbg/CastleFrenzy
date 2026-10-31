// irr::io::IAttributes, CGameConfig (config/*.xml objects) and the user profile store.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace CastleFrenzy
{
    public enum E_ATTRIBUTE_TYPE { EAT_INT, EAT_FLOAT, EAT_STRING, EAT_BOOL, EAT_ENUM, EAT_COLOR, EAT_COLORF, EAT_VECTOR3D, EAT_POSITION2D, EAT_RECT, EAT_UNKNOWN }

    public static class Attr
    {
        public static int ParseInt(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            s = s.Trim();
            int i = 0, sign = 1;
            if (i < s.Length && (s[i] == '-' || s[i] == '+')) { if (s[i] == '-') sign = -1; i++; }
            long v = 0;
            while (i < s.Length && s[i] >= '0' && s[i] <= '9') { v = v * 10 + (s[i] - '0'); i++; }
            return (int)(v * sign);
        }

        public static float ParseFloat(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            s = s.Trim();
            int end = 0;
            while (end < s.Length && ("+-.0123456789eE".IndexOf(s[end]) >= 0)) end++;
            float f;
            if (float.TryParse(s.Substring(0, end), NumberStyles.Float, CultureInfo.InvariantCulture, out f)) return f;
            return 0;
        }

        public static uint ParseColor(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            s = s.Trim();
            if (s.StartsWith("0x") || s.StartsWith("0X")) s = s.Substring(2);
            uint c;
            uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out c);
            return c;
        }

        public static float[] ParseFloats(string s, int count)
        {
            float[] r = new float[count];
            if (string.IsNullOrEmpty(s)) return r;
            string[] parts = s.Split(new[] { ',', ' ', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < count && i < parts.Length; i++) r[i] = ParseFloat(parts[i]);
            return r;
        }

        public static string F(float f) { return f.ToString("0.000000", CultureInfo.InvariantCulture); }
    }

    /// <summary>Ordered typed attribute list (irr::io::CAttributes).</summary>
    public class IAttributes
    {
        class Entry { public string Name; public E_ATTRIBUTE_TYPE Type; public string Value; }
        readonly List<Entry> m_list = new List<Entry>();

        static readonly Dictionary<string, E_ATTRIBUTE_TYPE> s_types = new Dictionary<string, E_ATTRIBUTE_TYPE>
        {
            { "int", E_ATTRIBUTE_TYPE.EAT_INT }, { "float", E_ATTRIBUTE_TYPE.EAT_FLOAT }, { "string", E_ATTRIBUTE_TYPE.EAT_STRING },
            { "stringw", E_ATTRIBUTE_TYPE.EAT_STRING }, { "bool", E_ATTRIBUTE_TYPE.EAT_BOOL }, { "enum", E_ATTRIBUTE_TYPE.EAT_ENUM },
            { "color", E_ATTRIBUTE_TYPE.EAT_COLOR }, { "colorf", E_ATTRIBUTE_TYPE.EAT_COLORF }, { "vector3d", E_ATTRIBUTE_TYPE.EAT_VECTOR3D },
            { "position", E_ATTRIBUTE_TYPE.EAT_POSITION2D }, { "rect", E_ATTRIBUTE_TYPE.EAT_RECT },
        };

        public static string TypeName(E_ATTRIBUTE_TYPE t)
        {
            switch (t)
            {
                case E_ATTRIBUTE_TYPE.EAT_INT: return "int";
                case E_ATTRIBUTE_TYPE.EAT_FLOAT: return "float";
                case E_ATTRIBUTE_TYPE.EAT_BOOL: return "bool";
                case E_ATTRIBUTE_TYPE.EAT_ENUM: return "enum";
                case E_ATTRIBUTE_TYPE.EAT_COLOR: return "color";
                case E_ATTRIBUTE_TYPE.EAT_COLORF: return "colorf";
                case E_ATTRIBUTE_TYPE.EAT_VECTOR3D: return "vector3d";
                case E_ATTRIBUTE_TYPE.EAT_POSITION2D: return "position";
                case E_ATTRIBUTE_TYPE.EAT_RECT: return "rect";
                default: return "string";
            }
        }

        public int getAttributeCount() { return m_list.Count; }
        public string getAttributeName(int i) { return m_list[i].Name; }
        public E_ATTRIBUTE_TYPE getAttributeType(int i) { return m_list[i].Type; }
        public string getAttributeTypeString(int i) { return TypeName(m_list[i].Type); }

        public int findAttribute(string name)
        {
            for (int i = 0; i < m_list.Count; i++) if (m_list[i].Name == name) return i;
            return -1;
        }

        public bool existsAttribute(string name) { return findAttribute(name) >= 0; }

        void SetRaw(string name, E_ATTRIBUTE_TYPE type, string value)
        {
            int i = findAttribute(name);
            if (i >= 0) { m_list[i].Value = value; return; }
            m_list.Add(new Entry { Name = name, Type = type, Value = value });
        }

        public void addRaw(string name, E_ATTRIBUTE_TYPE type, string value) { m_list.Add(new Entry { Name = name, Type = type, Value = value }); }
        public string getRaw(int i) { return m_list[i].Value; }

        // --- getters by name
        public int getAttributeAsInt(string name) { int i = findAttribute(name); return i >= 0 ? getAttributeAsInt(i) : 0; }
        public float getAttributeAsFloat(string name) { int i = findAttribute(name); return i >= 0 ? getAttributeAsFloat(i) : 0; }
        public string getAttributeAsString(string name) { int i = findAttribute(name); return i >= 0 ? getAttributeAsString(i) : ""; }
        public string getAttributeAsStringW(string name) { return getAttributeAsString(name); }
        public bool getAttributeAsBool(string name) { int i = findAttribute(name); return i >= 0 && getAttributeAsBool(i); }
        public SColor getAttributeAsColor(string name) { int i = findAttribute(name); return i >= 0 ? getAttributeAsColor(i) : new SColor(0); }
        public v3f getAttributeAsVector3d(string name) { int i = findAttribute(name); return i >= 0 ? getAttributeAsVector3d(i) : v3f.Zero; }
        public r2i getAttributeAsRect(string name) { int i = findAttribute(name); return i >= 0 ? getAttributeAsRect(i) : new r2i(); }
        public p2i getAttributeAsPosition2d(string name) { int i = findAttribute(name); return i >= 0 ? getAttributeAsPosition2d(i) : new p2i(); }

        // --- getters by index (with Irrlicht-like conversions)
        public int getAttributeAsInt(int i)
        {
            Entry e = m_list[i];
            switch (e.Type)
            {
                case E_ATTRIBUTE_TYPE.EAT_FLOAT: return (int)Attr.ParseFloat(e.Value);
                case E_ATTRIBUTE_TYPE.EAT_BOOL: return e.Value == "true" ? 1 : 0;
                case E_ATTRIBUTE_TYPE.EAT_COLOR: return (int)Attr.ParseColor(e.Value);
                default: return Attr.ParseInt(e.Value);
            }
        }

        public float getAttributeAsFloat(int i)
        {
            Entry e = m_list[i];
            if (e.Type == E_ATTRIBUTE_TYPE.EAT_BOOL) return e.Value == "true" ? 1 : 0;
            return Attr.ParseFloat(e.Value);
        }

        public string getAttributeAsString(int i) { return m_list[i].Value ?? ""; }

        public bool getAttributeAsBool(int i)
        {
            Entry e = m_list[i];
            if (e.Type == E_ATTRIBUTE_TYPE.EAT_INT || e.Type == E_ATTRIBUTE_TYPE.EAT_FLOAT) return Attr.ParseFloat(e.Value) != 0;
            return e.Value == "true";
        }

        public SColor getAttributeAsColor(int i) { return new SColor(Attr.ParseColor(m_list[i].Value)); }

        public SColorf getAttributeAsColorf(int i)
        {
            float[] f = Attr.ParseFloats(m_list[i].Value, 4);
            return new SColorf(f[0], f[1], f[2], f[3]);
        }

        public v3f getAttributeAsVector3d(int i) { float[] f = Attr.ParseFloats(m_list[i].Value, 3); return new v3f(f[0], f[1], f[2]); }
        public p2i getAttributeAsPosition2d(int i) { float[] f = Attr.ParseFloats(m_list[i].Value, 2); return new p2i((int)f[0], (int)f[1]); }
        public r2i getAttributeAsRect(int i) { float[] f = Attr.ParseFloats(m_list[i].Value, 4); return new r2i((int)f[0], (int)f[1], (int)f[2], (int)f[3]); }

        // --- setters
        public void setAttribute(string name, int v) { int i = findAttribute(name); if (i >= 0) setAttribute(i, v); else addInt(name, v); }
        public void setAttribute(string name, float v) { int i = findAttribute(name); if (i >= 0) setAttribute(i, v); else addFloat(name, v); }
        public void setAttribute(string name, bool v) { int i = findAttribute(name); if (i >= 0) m_list[i].Value = v ? "true" : "false"; else addBool(name, v); }
        public void setAttribute(string name, string v) { int i = findAttribute(name); if (i >= 0) m_list[i].Value = v; else addString(name, v); }
        public void setAttribute(string name, SColor v) { SetRaw(name, E_ATTRIBUTE_TYPE.EAT_COLOR, v.color.ToString("x8")); }
        public void setAttribute(string name, v3f v) { SetRaw(name, E_ATTRIBUTE_TYPE.EAT_VECTOR3D, Attr.F(v.X) + ", " + Attr.F(v.Y) + ", " + Attr.F(v.Z)); }

        public void setAttribute(int i, int v)
        {
            Entry e = m_list[i];
            if (e.Type == E_ATTRIBUTE_TYPE.EAT_FLOAT) e.Value = Attr.F(v);
            else if (e.Type == E_ATTRIBUTE_TYPE.EAT_BOOL) e.Value = v != 0 ? "true" : "false";
            else e.Value = v.ToString(CultureInfo.InvariantCulture);
        }

        public void setAttribute(int i, float v)
        {
            Entry e = m_list[i];
            if (e.Type == E_ATTRIBUTE_TYPE.EAT_INT) e.Value = ((int)v).ToString(CultureInfo.InvariantCulture);
            else if (e.Type == E_ATTRIBUTE_TYPE.EAT_BOOL) e.Value = v != 0 ? "true" : "false";
            else e.Value = Attr.F(v);
        }

        public void setAttribute(int i, string v)
        {
            Entry e = m_list[i];
            if (e.Type == E_ATTRIBUTE_TYPE.EAT_INT) e.Value = Attr.ParseInt(v).ToString(CultureInfo.InvariantCulture);
            else if (e.Type == E_ATTRIBUTE_TYPE.EAT_FLOAT) e.Value = Attr.F(Attr.ParseFloat(v));
            else e.Value = v;
        }

        public void addInt(string name, int v) { m_list.Add(new Entry { Name = name, Type = E_ATTRIBUTE_TYPE.EAT_INT, Value = v.ToString(CultureInfo.InvariantCulture) }); }
        public void addFloat(string name, float v) { m_list.Add(new Entry { Name = name, Type = E_ATTRIBUTE_TYPE.EAT_FLOAT, Value = Attr.F(v) }); }
        public void addString(string name, string v) { m_list.Add(new Entry { Name = name, Type = E_ATTRIBUTE_TYPE.EAT_STRING, Value = v ?? "" }); }
        public void addBool(string name, bool v) { m_list.Add(new Entry { Name = name, Type = E_ATTRIBUTE_TYPE.EAT_BOOL, Value = v ? "true" : "false" }); }

        public void clear() { m_list.Clear(); }

        /// <summary>Copies src attributes into this (CGameConfig::copyAttributes).</summary>
        public void copyFrom(IAttributes src, bool overwrite)
        {
            for (int i = 0; i < src.m_list.Count; i++)
            {
                Entry e = src.m_list[i];
                int j = findAttribute(e.Name);
                if (j >= 0)
                {
                    if (!overwrite) continue;
                    m_list[j].Value = e.Value;
                }
                else m_list.Add(new Entry { Name = e.Name, Type = e.Type, Value = e.Value });
            }
        }

        /// <summary>Reads child attribute elements of the current element (CAttributes::read).</summary>
        public void read(IXMLReader xml, string elementName)
        {
            if (xml.isEmptyElement()) return;
            while (xml.read())
            {
                EXML_NODE t = xml.getNodeType();
                if (t == EXML_NODE.EXN_ELEMENT)
                {
                    string tag = xml.getNodeName();
                    E_ATTRIBUTE_TYPE type;
                    string name = xml.getAttributeValue("name");
                    if (name != null && s_types.TryGetValue(tag, out type))
                    {
                        string value = xml.getAttributeValueSafe("value");
                        if (type == E_ATTRIBUTE_TYPE.EAT_BOOL) value = value.Trim().ToLowerInvariant() == "true" ? "true" : "false";
                        m_list.Add(new Entry { Name = name, Type = type, Value = value });
                    }
                }
                else if (t == EXML_NODE.EXN_ELEMENT_END && xml.getNodeName() == elementName) break;
            }
        }

        public void write(IXMLWriter w)
        {
            foreach (Entry e in m_list)
            {
                w.writeText("\t");
                w.writeElement(TypeName(e.Type), true, "name", e.Name, "value", e.Value);
                w.writeLineBreak();
            }
        }
    }

    public class CGameConfig
    {
        public static readonly bool DisableRTT = true;
        public static readonly bool EnableCheats = false;
        public static SColor EmissiveColorForInteractiveObjects = new SColor(0xff505050);
        public static bool NoWeaponCD = false;
        public static bool AllWeaponsSold = false;
        public static bool AllowDowngrades = false;
        public static bool Invulnerable = false;
        public static bool DontSpawnUnits = false;
        public static bool RenderToTexture = false;
        public static bool DrawFPS = false;
        public static bool UnlockAllLevels = false;

        static CGameConfig s_instance;
        public static CGameConfig GetInstance() { return s_instance ?? (s_instance = new CGameConfig()); }

        // type -> id -> attributes; SortedDictionary mirrors irr::core::map iteration order.
        readonly SortedDictionary<string, SortedDictionary<string, IAttributes>> m_objects =
            new SortedDictionary<string, SortedDictionary<string, IAttributes>>(StringComparer.Ordinal);

        public bool importFile(string fileName)
        {
            IXMLReader xml = IXMLReader.FromFile(fileName);
            if (xml == null) return false;
            while (xml.read())
            {
                if (xml.getNodeType() != EXML_NODE.EXN_ELEMENT) continue;
                string objectType = xml.getNodeName();
                string objectId = xml.getAttributeValue("id") ?? "";
                clear(objectType, objectId);
                IAttributes objectAttr = new IAttributes();
                IAttributes parentAttr = getAttributes(objectType, xml.getAttributeValue("extends") ?? "");
                objectAttr.read(xml, objectType);
                if (parentAttr != null) objectAttr.copyFrom(parentAttr, false);
                setAttributes(objectType, objectId, objectAttr);
            }
            return true;
        }

        public bool importObjects(string fileName, SortedDictionary<string, IAttributes> attrMap, string type)
        {
            IXMLReader xml = IXMLReader.FromFile(fileName);
            if (xml == null) return false;
            while (xml.read())
            {
                if (xml.getNodeType() != EXML_NODE.EXN_ELEMENT) continue;
                string objectType = xml.getNodeName();
                if (type != objectType) continue;
                string objectId = xml.getAttributeValue("id") ?? "";
                IAttributes objectAttr = new IAttributes();
                IAttributes parentAttr = getAttributes(objectType, xml.getAttributeValue("extends") ?? "");
                objectAttr.read(xml, objectType);
                if (parentAttr != null) objectAttr.copyFrom(parentAttr, false);
                attrMap[objectId] = objectAttr;
            }
            return true;
        }

        public bool exportObjects(string fileName, SortedDictionary<string, IAttributes> map, string objectName)
        {
            IXMLWriter xml = new IXMLWriter(GameFiles.Full(fileName), false);
            xml.writeXMLHeader();
            xml.writeLineBreak();
            xml.writeComment(objectName + " config file");
            xml.writeLineBreak();
            xml.writeLineBreak();
            foreach (var kv in map)
            {
                xml.writeElement(objectName, false, "id", kv.Key);
                xml.writeLineBreak();
                kv.Value.write(xml);
                xml.writeClosingTag(objectName);
                xml.writeLineBreak();
                xml.writeLineBreak();
            }
            return xml.close();
        }

        public void clear() { m_objects.Clear(); }
        public void clear(string type) { m_objects.Remove(type); }

        public void clear(string type, string id)
        {
            SortedDictionary<string, IAttributes> map;
            if (!m_objects.TryGetValue(type, out map)) return;
            map.Remove(id);
            if (map.Count == 0) m_objects.Remove(type);
        }

        public IAttributes getAttributes(string type, string id)
        {
            SortedDictionary<string, IAttributes> map;
            if (!m_objects.TryGetValue(type, out map)) return null;
            IAttributes a;
            return map.TryGetValue(id ?? "", out a) ? a : null;
        }

        public void setAttributes(string type, string id, IAttributes attributes)
        {
            SortedDictionary<string, IAttributes> map;
            if (!m_objects.TryGetValue(type, out map)) m_objects[type] = map = new SortedDictionary<string, IAttributes>(StringComparer.Ordinal);
            map[id] = attributes;
        }

        public SortedDictionary<string, SortedDictionary<string, IAttributes>> getObjectAttrMap() { return m_objects; }

        public SortedDictionary<string, IAttributes> getAttributesMap(string type)
        {
            SortedDictionary<string, IAttributes> map;
            if (!m_objects.TryGetValue(type, out map)) m_objects[type] = map = new SortedDictionary<string, IAttributes>(StringComparer.Ordinal);
            return map;
        }
    }

    /// <summary>
    /// CUserProfile: a persistent key/value store backed by &lt;save&gt;/user_profile.xml with defaults
    /// from config/Profiles/default.xml. Every Set is written through like the C++ destructor save.
    /// </summary>
    public class CUserProfile
    {
        const string DEFAULT_PROFILE_FILE_NAME = "default";
        const string DEFAULT_PROFILE_NAME = "Profile";
        const string DEFAULT_PROFILE_TYPE = "ProfileDefault";
        const string USER_PROFILE_TYPE = "ProfileUser";

        readonly string m_name;
        readonly string m_fileName;
        static bool s_loaded;
        static bool s_dirty;

        public CUserProfile(string name, string fileName)
        {
            m_name = name;
            m_fileName = fileName;
            if (!s_loaded) Load();
        }

        static string ProfilePath(string fileName) { return GameFiles.SavePath + fileName + ".xml"; }

        void Load()
        {
            s_loaded = true;
            CGameConfig gc = CGameConfig.GetInstance();
            // default profile: saved copy, else the shipped one
            if (!File.Exists(ProfilePath(DEFAULT_PROFILE_FILE_NAME)) || !gc.importFile(ProfilePath(DEFAULT_PROFILE_FILE_NAME)))
                gc.importFile("config/Profiles/default.xml");
            if (gc.getAttributes(DEFAULT_PROFILE_TYPE, DEFAULT_PROFILE_NAME) == null)
            {
                IAttributes a = new IAttributes();
                a.addInt("_version", 1);
                gc.setAttributes(DEFAULT_PROFILE_TYPE, DEFAULT_PROFILE_NAME, a);
            }
            IAttributes def = gc.getAttributes(DEFAULT_PROFILE_TYPE, DEFAULT_PROFILE_NAME);
            if (File.Exists(ProfilePath(m_fileName))) gc.importFile(ProfilePath(m_fileName));
            IAttributes user = gc.getAttributes(USER_PROFILE_TYPE, m_name);
            if (user == null || user.getAttributeAsInt("_version") != def.getAttributeAsInt("_version"))
            {
                user = new IAttributes();
                user.copyFrom(def, true);
                gc.setAttributes(USER_PROFILE_TYPE, m_name, user);
                s_dirty = true;
            }
            else user.copyFrom(def, false);
            Save();
        }

        IAttributes Attrs
        {
            get
            {
                CGameConfig gc = CGameConfig.GetInstance();
                IAttributes a = gc.getAttributes(USER_PROFILE_TYPE, m_name);
                if (a == null)
                {
                    a = new IAttributes();
                    IAttributes def = gc.getAttributes(DEFAULT_PROFILE_TYPE, DEFAULT_PROFILE_NAME);
                    if (def != null) a.copyFrom(def, true);
                    gc.setAttributes(USER_PROFILE_TYPE, m_name, a);
                }
                return a;
            }
        }

        int Index(string var)
        {
            IAttributes a = Attrs;
            int idx = a.findAttribute(var);
            if (idx >= 0) return idx;
            // DefProfile_AddNewAttribute + sync: new variables default to int 0
            IAttributes def = CGameConfig.GetInstance().getAttributes(DEFAULT_PROFILE_TYPE, DEFAULT_PROFILE_NAME);
            if (def != null && def.findAttribute(var) < 0) def.addInt(var, 0);
            a.addInt(var, 0);
            return a.findAttribute(var);
        }

        public bool Get(string var, out int f) { int i = Index(var); f = Attrs.getAttributeAsInt(i); return true; }
        public bool Get(string var, out float f) { int i = Index(var); f = Attrs.getAttributeAsFloat(i); return true; }
        public bool Get(string var, out string f) { int i = Index(var); f = Attrs.getAttributeAsString(i); return true; }
        public bool Get(string var, out bool f) { int v; Get(var, out v); f = v != 0; return true; }
        public bool Set(string var, int f) { int i = Index(var); Attrs.setAttribute(i, f); s_dirty = true; return true; }
        public bool Set(string var, bool f) { return Set(var, f ? 1 : 0); }
        public bool Set(string var, float f) { int i = Index(var); Attrs.setAttribute(i, f); s_dirty = true; return true; }
        public bool Set(string var, string f) { int i = Index(var); Attrs.setAttribute(i, f); s_dirty = true; return true; }

        public int GetInt(string var) { int v; Get(var, out v); return v; }
        public float GetFloat(string var) { float v; Get(var, out v); return v; }

        public void ResetToDefault()
        {
            CGameConfig gc = CGameConfig.GetInstance();
            gc.clear(USER_PROFILE_TYPE, m_name);
            IAttributes a = Attrs;
            s_dirty = true;
            Save();
        }

        public void Save()
        {
            if (!s_dirty) return;
            s_dirty = false;
            CGameConfig gc = CGameConfig.GetInstance();
            IAttributes def = gc.getAttributes(DEFAULT_PROFILE_TYPE, DEFAULT_PROFILE_NAME);
            if (def != null)
            {
                var dm = new SortedDictionary<string, IAttributes>(StringComparer.Ordinal) { { DEFAULT_PROFILE_NAME, def } };
                gc.exportObjects(ProfilePath(DEFAULT_PROFILE_FILE_NAME), dm, DEFAULT_PROFILE_TYPE);
            }
            var um = new SortedDictionary<string, IAttributes>(StringComparer.Ordinal) { { m_name, Attrs } };
            gc.exportObjects(ProfilePath(m_fileName), um, USER_PROFILE_TYPE);
        }

        /// <summary>Flushes pending writes (called once per frame and on quit).</summary>
        public static void Flush()
        {
            if (s_dirty) new CUserProfile("Player1", "user_profile").Save();
        }

        public static void ForceReload() { s_loaded = false; }
    }

    public static class Profile
    {
        /// <summary>__PROFILE()</summary>
        public static CUserProfile Get() { return new CUserProfile("Player1", "user_profile"); }
    }
}
