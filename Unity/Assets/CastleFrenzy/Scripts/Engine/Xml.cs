// Minimal XML reader/writer with the semantics of Irrlicht's IrrXML (EXN_* node stream),
// tolerant of UTF-8/UTF-16 with or without BOM and of multiple top-level elements.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CastleFrenzy
{
    public enum EXML_NODE { EXN_NONE, EXN_ELEMENT, EXN_ELEMENT_END, EXN_TEXT, EXN_COMMENT, EXN_CDATA, EXN_UNKNOWN }

    public class IXMLReader
    {
        struct Node
        {
            public EXML_NODE Type;
            public string Name;
            public List<KeyValuePair<string, string>> Attributes;
            public bool Empty;
        }

        readonly List<Node> m_nodes = new List<Node>();
        int m_pos = -1;

        public static string DecodeText(byte[] data)
        {
            if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE) return Encoding.Unicode.GetString(data, 2, data.Length - 2);
            if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF) return Encoding.BigEndianUnicode.GetString(data, 2, data.Length - 2);
            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF) return Encoding.UTF8.GetString(data, 3, data.Length - 3);
            if (data.Length >= 2 && data[1] == 0 && data[0] != 0) return Encoding.Unicode.GetString(data);
            return Encoding.UTF8.GetString(data);
        }

        public static IXMLReader FromFile(string relativeOrAbsolute)
        {
            string p = GameFiles.Full(relativeOrAbsolute);
            if (!File.Exists(p)) return null;
            return new IXMLReader(DecodeText(File.ReadAllBytes(p)));
        }

        public IXMLReader(string text) { Parse(text); }

        static string Unescape(string s)
        {
            if (s.IndexOf('&') < 0) return s;
            return s.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"").Replace("&apos;", "'").Replace("&amp;", "&");
        }

        void Parse(string t)
        {
            int i = 0, n = t.Length;
            while (i < n)
            {
                if (t[i] != '<')
                {
                    int s = i;
                    while (i < n && t[i] != '<') i++;
                    string text = t.Substring(s, i - s);
                    if (text.Trim().Length > 0) m_nodes.Add(new Node { Type = EXML_NODE.EXN_TEXT, Name = Unescape(text) });
                    continue;
                }
                if (string.CompareOrdinal(t, i, "<!--", 0, 4) == 0)
                {
                    int e = t.IndexOf("-->", i + 4, StringComparison.Ordinal);
                    if (e < 0) e = n - 3;
                    m_nodes.Add(new Node { Type = EXML_NODE.EXN_COMMENT, Name = t.Substring(i + 4, e - i - 4) });
                    i = e + 3;
                    continue;
                }
                if (string.CompareOrdinal(t, i, "<![CDATA[", 0, 9) == 0)
                {
                    int e = t.IndexOf("]]>", i + 9, StringComparison.Ordinal);
                    if (e < 0) e = n - 3;
                    m_nodes.Add(new Node { Type = EXML_NODE.EXN_CDATA, Name = t.Substring(i + 9, e - i - 9) });
                    i = e + 3;
                    continue;
                }
                if (i + 1 < n && (t[i + 1] == '?' || t[i + 1] == '!'))
                {
                    int e = t.IndexOf('>', i);
                    if (e < 0) e = n - 1;
                    m_nodes.Add(new Node { Type = EXML_NODE.EXN_UNKNOWN, Name = "" });
                    i = e + 1;
                    continue;
                }
                if (i + 1 < n && t[i + 1] == '/')
                {
                    int e = t.IndexOf('>', i);
                    if (e < 0) e = n - 1;
                    m_nodes.Add(new Node { Type = EXML_NODE.EXN_ELEMENT_END, Name = t.Substring(i + 2, e - i - 2).Trim() });
                    i = e + 1;
                    continue;
                }
                // element
                i++;
                int ns = i;
                while (i < n && !char.IsWhiteSpace(t[i]) && t[i] != '>' && t[i] != '/') i++;
                Node node = new Node { Type = EXML_NODE.EXN_ELEMENT, Name = t.Substring(ns, i - ns), Attributes = new List<KeyValuePair<string, string>>() };
                while (i < n)
                {
                    while (i < n && char.IsWhiteSpace(t[i])) i++;
                    if (i >= n) break;
                    if (t[i] == '>') { i++; break; }
                    if (t[i] == '/' && i + 1 < n && t[i + 1] == '>') { node.Empty = true; i += 2; break; }
                    int an = i;
                    while (i < n && t[i] != '=' && !char.IsWhiteSpace(t[i]) && t[i] != '>' && t[i] != '/') i++;
                    string aname = t.Substring(an, i - an);
                    while (i < n && char.IsWhiteSpace(t[i])) i++;
                    string aval = "";
                    if (i < n && t[i] == '=')
                    {
                        i++;
                        while (i < n && char.IsWhiteSpace(t[i])) i++;
                        if (i < n && (t[i] == '"' || t[i] == '\''))
                        {
                            char q = t[i++];
                            int vs = i;
                            while (i < n && t[i] != q) i++;
                            aval = Unescape(t.Substring(vs, i - vs));
                            i++;
                        }
                    }
                    if (aname.Length > 0) node.Attributes.Add(new KeyValuePair<string, string>(aname, aval));
                    else i++;
                }
                m_nodes.Add(node);
            }
        }

        public bool read() { m_pos++; return m_pos < m_nodes.Count; }
        public EXML_NODE getNodeType() { return m_pos >= 0 && m_pos < m_nodes.Count ? m_nodes[m_pos].Type : EXML_NODE.EXN_NONE; }
        public string getNodeName() { return m_pos >= 0 && m_pos < m_nodes.Count ? m_nodes[m_pos].Name : ""; }
        public string getNodeData() { return getNodeName(); }
        public bool isEmptyElement() { return m_pos >= 0 && m_pos < m_nodes.Count && m_nodes[m_pos].Empty; }
        public int getAttributeCount() { var a = m_nodes[m_pos].Attributes; return a != null ? a.Count : 0; }
        public string getAttributeName(int i) { return m_nodes[m_pos].Attributes[i].Key; }

        public string getAttributeValue(int i)
        {
            var a = m_nodes[m_pos].Attributes;
            return a != null && i >= 0 && i < a.Count ? a[i].Value : null;
        }

        public int getAttributeValueAsInt(int i) { return Attr.ParseInt(getAttributeValue(i)); }

        public string getAttributeValue(string name)
        {
            if (m_pos < 0 || m_pos >= m_nodes.Count) return null;
            var a = m_nodes[m_pos].Attributes;
            if (a == null) return null;
            for (int i = 0; i < a.Count; i++) if (a[i].Key == name) return a[i].Value;
            return null;
        }

        public string getAttributeValueSafe(string name) { return getAttributeValue(name) ?? ""; }
        public int getAttributeValueAsInt(string name) { return Attr.ParseInt(getAttributeValue(name)); }
        public float getAttributeValueAsFloat(string name) { return Attr.ParseFloat(getAttributeValue(name)); }
    }

    /// <summary>XML writer producing the same layout as Irrlicht's CXMLWriter.</summary>
    public class IXMLWriter
    {
        readonly StringBuilder m_sb = new StringBuilder();
        readonly string m_path;
        readonly bool m_utf16;

        public IXMLWriter(string absolutePath, bool utf16 = true) { m_path = absolutePath; m_utf16 = utf16; }

        static string Escape(string s)
        {
            return (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
        }

        public void writeXMLHeader() { m_sb.Append("<?xml version=\"1.0\"?>"); }
        public void writeLineBreak() { m_sb.Append("\r\n"); }
        public void writeComment(string c) { m_sb.Append("<!--").Append(c).Append("-->"); }
        public void writeText(string t) { m_sb.Append(Escape(t)); }

        public void writeElement(string name, bool empty = false, params string[] attrs)
        {
            m_sb.Append('<').Append(name);
            for (int i = 0; i + 1 < attrs.Length; i += 2)
                m_sb.Append(' ').Append(attrs[i]).Append("=\"").Append(Escape(attrs[i + 1])).Append('"');
            m_sb.Append(empty ? " />" : ">");
        }

        public void writeClosingTag(string name) { m_sb.Append("</").Append(name).Append('>'); }

        public string Text { get { return m_sb.ToString(); } }

        public bool close()
        {
            try
            {
                string dir = Path.GetDirectoryName(m_path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string tmp = m_path + ".tmp";
                byte[] body = m_utf16 ? Encoding.Unicode.GetBytes(m_sb.ToString()) : Encoding.UTF8.GetBytes(m_sb.ToString());
                using (FileStream fs = new FileStream(tmp, FileMode.Create, FileAccess.Write))
                {
                    if (m_utf16) { fs.WriteByte(0xFF); fs.WriteByte(0xFE); }
                    fs.Write(body, 0, body.Length);
                }
                if (File.Exists(m_path)) File.Delete(m_path);
                File.Move(tmp, m_path);
                return true;
            }
            catch (Exception e)
            {
                Logs.error("Could not write " + m_path + ": " + e.Message);
                return false;
            }
        }
    }
}
