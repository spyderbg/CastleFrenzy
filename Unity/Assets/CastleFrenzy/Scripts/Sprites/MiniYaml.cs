// A small YAML subset parser for the AuroraMX sprite format: block mappings and sequences,
// flow mappings { a: b } and flow sequences [a, b], quoted/plain scalars and comments.
using System;
using System.Collections.Generic;
using System.Text;

namespace CastleFrenzy
{
    public class YamlNode
    {
        public enum Kind { Scalar, Map, Seq, Null }
        public Kind Type;
        public string Value;
        public readonly List<KeyValuePair<string, YamlNode>> Map = new List<KeyValuePair<string, YamlNode>>();
        public readonly List<YamlNode> Seq = new List<YamlNode>();

        public static YamlNode Scalar(string v) { return new YamlNode { Type = Kind.Scalar, Value = v }; }

        public YamlNode this[string key]
        {
            get
            {
                if (Type != Kind.Map) return null;
                for (int i = 0; i < Map.Count; i++) if (Map[i].Key == key) return Map[i].Value;
                return null;
            }
        }

        public string Str(string key, string fallback = null)
        {
            YamlNode n = this[key];
            return n != null && n.Type == Kind.Scalar ? n.Value : fallback;
        }

        public int Int(string key, int fallback = 0)
        {
            string s = Str(key);
            if (s == null) return fallback;
            int v;
            return int.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out v) ? v : fallback;
        }

        public List<YamlNode> List(string key)
        {
            YamlNode n = this[key];
            return n != null && n.Type == Kind.Seq ? n.Seq : new List<YamlNode>();
        }
    }

    public class YamlException : Exception { public YamlException(string m) : base(m) { } }

    public static class MiniYaml
    {
        struct Line { public int Indent; public string Text; public int No; }

        public static YamlNode Parse(string text)
        {
            List<Line> lines = new List<Line>();
            string[] raw = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < raw.Length; i++)
            {
                string l = StripComment(raw[i]);
                if (l.Trim().Length == 0) continue;
                if (l.Trim() == "---" || l.Trim() == "...") continue;
                int ind = 0;
                while (ind < l.Length && l[ind] == ' ') ind++;
                lines.Add(new Line { Indent = ind, Text = l.Substring(ind).TrimEnd(), No = i + 1 });
            }
            int pos = 0;
            if (lines.Count == 0) return new YamlNode { Type = YamlNode.Kind.Null };
            return ParseBlock(lines, ref pos, lines[0].Indent);
        }

        static string StripComment(string l)
        {
            bool inS = false, inD = false;
            for (int i = 0; i < l.Length; i++)
            {
                char c = l[i];
                if (c == '\'' && !inD) inS = !inS;
                else if (c == '"' && !inS) inD = !inD;
                else if (c == '#' && !inS && !inD && (i == 0 || l[i - 1] == ' ' || l[i - 1] == '\t')) return l.Substring(0, i);
            }
            return l;
        }

        static YamlNode ParseBlock(List<Line> lines, ref int pos, int indent)
        {
            if (pos >= lines.Count) return new YamlNode { Type = YamlNode.Kind.Null };
            if (lines[pos].Text.StartsWith("- ") || lines[pos].Text == "-") return ParseSeq(lines, ref pos, indent);
            return ParseMap(lines, ref pos, indent);
        }

        static YamlNode ParseSeq(List<Line> lines, ref int pos, int indent)
        {
            YamlNode seq = new YamlNode { Type = YamlNode.Kind.Seq };
            while (pos < lines.Count && lines[pos].Indent == indent && (lines[pos].Text.StartsWith("- ") || lines[pos].Text == "-"))
            {
                Line l = lines[pos];
                string rest = l.Text.Length > 1 ? l.Text.Substring(2).Trim() : "";
                if (rest.Length == 0)
                {
                    pos++;
                    seq.Seq.Add(pos < lines.Count && lines[pos].Indent > indent ? ParseBlock(lines, ref pos, lines[pos].Indent) : new YamlNode { Type = YamlNode.Kind.Null });
                }
                else if (rest.StartsWith("{") || rest.StartsWith("[") || !IsKeyValue(rest))
                {
                    seq.Seq.Add(ParseInline(rest, l.No));
                    pos++;
                }
                else
                {
                    // "- key: value" starts an inline mapping whose further keys are indented by 2
                    int childIndent = indent + 2;
                    Line replaced = new Line { Indent = childIndent, Text = rest, No = l.No };
                    lines[pos] = replaced;
                    seq.Seq.Add(ParseMap(lines, ref pos, childIndent));
                }
            }
            return seq;
        }

        static bool IsKeyValue(string s)
        {
            bool inS = false, inD = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\'' && !inD) inS = !inS;
                else if (c == '"' && !inS) inD = !inD;
                else if (c == ':' && !inS && !inD && (i + 1 == s.Length || s[i + 1] == ' ')) return true;
            }
            return false;
        }

        static int KeyColon(string s)
        {
            bool inS = false, inD = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\'' && !inD) inS = !inS;
                else if (c == '"' && !inS) inD = !inD;
                else if (c == ':' && !inS && !inD && (i + 1 == s.Length || s[i + 1] == ' ')) return i;
            }
            return -1;
        }

        static YamlNode ParseMap(List<Line> lines, ref int pos, int indent)
        {
            YamlNode map = new YamlNode { Type = YamlNode.Kind.Map };
            while (pos < lines.Count && lines[pos].Indent == indent)
            {
                Line l = lines[pos];
                if (l.Text.StartsWith("- ")) break;
                int c = KeyColon(l.Text);
                if (c < 0) throw new YamlException("Expected 'key: value' at line " + l.No);
                string key = Unquote(l.Text.Substring(0, c).Trim());
                string rest = l.Text.Substring(c + 1).Trim();
                pos++;
                YamlNode value;
                if (rest.Length == 0)
                {
                    if (pos < lines.Count && (lines[pos].Indent > indent || (lines[pos].Indent == indent && lines[pos].Text.StartsWith("- "))))
                        value = ParseBlock(lines, ref pos, lines[pos].Indent);
                    else value = new YamlNode { Type = YamlNode.Kind.Null };
                }
                else value = ParseInline(rest, l.No);
                map.Map.Add(new KeyValuePair<string, YamlNode>(key, value));
            }
            return map;
        }

        static YamlNode ParseInline(string s, int lineNo)
        {
            int i = 0;
            YamlNode n = ParseFlow(s, ref i, lineNo, false);
            return n;
        }

        static void SkipWs(string s, ref int i) { while (i < s.Length && (s[i] == ' ' || s[i] == '\t')) i++; }

        static YamlNode ParseFlow(string s, ref int i, int lineNo, bool inFlow)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) return new YamlNode { Type = YamlNode.Kind.Null };
            if (s[i] == '{')
            {
                i++;
                YamlNode map = new YamlNode { Type = YamlNode.Kind.Map };
                while (true)
                {
                    SkipWs(s, ref i);
                    if (i >= s.Length) throw new YamlException("Unterminated mapping at line " + lineNo);
                    if (s[i] == '}') { i++; break; }
                    string key = ReadFlowScalar(s, ref i, true);
                    SkipWs(s, ref i);
                    if (i >= s.Length || s[i] != ':') throw new YamlException("Expected ':' at line " + lineNo);
                    i++;
                    YamlNode v = ParseFlow(s, ref i, lineNo, true);
                    map.Map.Add(new KeyValuePair<string, YamlNode>(Unquote(key), v));
                    SkipWs(s, ref i);
                    if (i < s.Length && s[i] == ',') i++;
                }
                return map;
            }
            if (s[i] == '[')
            {
                i++;
                YamlNode seq = new YamlNode { Type = YamlNode.Kind.Seq };
                while (true)
                {
                    SkipWs(s, ref i);
                    if (i >= s.Length) throw new YamlException("Unterminated sequence at line " + lineNo);
                    if (s[i] == ']') { i++; break; }
                    seq.Seq.Add(ParseFlow(s, ref i, lineNo, true));
                    SkipWs(s, ref i);
                    if (i < s.Length && s[i] == ',') i++;
                }
                return seq;
            }
            string sc = ReadFlowScalar(s, ref i, inFlow);
            return YamlNode.Scalar(Unquote(sc.Trim()));
        }

        static string ReadFlowScalar(string s, ref int i, bool inFlow)
        {
            SkipWs(s, ref i);
            if (i < s.Length && (s[i] == '"' || s[i] == '\''))
            {
                char q = s[i];
                int start = i++;
                StringBuilder sb = new StringBuilder();
                while (i < s.Length)
                {
                    if (s[i] == '\\' && q == '"' && i + 1 < s.Length) { sb.Append(s[i + 1]); i += 2; continue; }
                    if (s[i] == q)
                    {
                        if (q == '\'' && i + 1 < s.Length && s[i + 1] == '\'') { sb.Append('\''); i += 2; continue; }
                        i++;
                        break;
                    }
                    sb.Append(s[i++]);
                }
                return q + sb.ToString() + q;
            }
            int b = i;
            while (i < s.Length)
            {
                char c = s[i];
                if (inFlow && (c == ',' || c == '}' || c == ']')) break;
                if (inFlow && c == ':' && (i + 1 >= s.Length || s[i + 1] == ' ')) break;
                i++;
            }
            return s.Substring(b, i - b).Trim();
        }

        static string Unquote(string s)
        {
            if (s.Length >= 2 && ((s[0] == '"' && s[s.Length - 1] == '"') || (s[0] == '\'' && s[s.Length - 1] == '\'')))
                return s.Substring(1, s.Length - 2);
            return s;
        }
    }
}
