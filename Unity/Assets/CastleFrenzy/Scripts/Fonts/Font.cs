// Fonts/Font and Fonts/FontManager: sprite fonts with character maps from Fonts/*.txt.
using System;
using System.Collections.Generic;

namespace CastleFrenzy
{
    public class CFont
    {
        public const char SpaceChar = ' ';
        public const char NewLineChar = '\n';
        public const char NonBreakableSpaceChar = (char)160;
        public const char OptionalNewLineChar = '|';

        public const int HCENTER = 1, VCENTER = 2, HVCENTER = 3, LEFT = 4, RIGHT = 8, TOP = 16, BOTTOM = 32;

        ASprite m_pSprite;
        char[] m_pCharMap = new char[0];
        int[] m_pCharIndex = new int[0];
        bool m_gradient;
        float m_scaleX = 1, m_scaleY = 1;
        public float CharSpacing, LineSpacing, SpaceWidth, LineHeight;
        public string FileName;

        float m_infoW, m_infoH;
        int m_infoLines;
        readonly List<int> m_infoOffsets = new List<int>();

        public bool Load(string name)
        {
            FileName = name;
            IAttributes attr = CGameConfig.GetInstance().getAttributes("font", name);
            if (attr == null) { Logs.error("Unknown font " + name); attr = new IAttributes(); }
            string filename = attr.getAttributeAsString("FileName");
            m_pSprite = new ASprite();
            if (!m_pSprite.Load("Fonts/" + filename)) { Logs.error("Font sprite not loaded: " + name); return false; }
            m_pSprite._modules_image.setMinFilter(true);
            m_pSprite._material.ZBuffer = false;
            m_pSprite._material.ZWriteEnable = false;
            m_pSprite._material.Lighting = false;
            m_pSprite._material.BackfaceCulling = false;
            m_pSprite._material.EmissiveColor = new SColor(1, 1, 1, 1);

            byte[] data = GameFiles.ReadAllBytes("Fonts/" + filename + ".txt");
            if (data == null) { Logs.error("Font not loaded: " + name); return false; }
            int count = (data.Length - 2) / 2;
            m_pCharMap = new char[count];
            m_pCharIndex = new int[count];
            for (int i = 0; i < count; i++) { m_pCharMap[i] = (char)BitConverter.ToUInt16(data, 2 + i * 2); m_pCharIndex[i] = i; }
            char[] map = m_pCharMap;
            // std::sort is not stable; duplicate characters resolve to any index, use a stable order.
            Array.Sort(m_pCharIndex, (a, b) => { int c = map[a].CompareTo(map[b]); return c != 0 ? c : a.CompareTo(b); });

            SColor color1 = new SColor(255, 255, 255, 255), color2 = new SColor(0, 0, 0, 0);
            int alpha = 255;
            bool gradient = false;
            if (attr.existsAttribute("Color")) color1 = attr.getAttributeAsColor("Color");
            if (attr.existsAttribute("Color1")) color1 = attr.getAttributeAsColor("Color1");
            if (attr.existsAttribute("Color2")) { gradient = true; color2 = attr.getAttributeAsColor("Color2"); }
            if (attr.existsAttribute("Alpha")) alpha = attr.getAttributeAsInt("Alpha");
            SetColor1(color1);
            SetColor2(color2);
            SetAlpha(alpha);
            EnableGradient(gradient);
            CharSpacing = attr.getAttributeAsInt("CharSpacing");
            LineSpacing = attr.getAttributeAsInt("LineSpacing");
            SpaceWidth = attr.getAttributeAsInt("SpaceWidth");
            if (SpaceWidth == 0) SpaceWidth = GetCharWidth(' ');
            LineHeight = m_pSprite.GetFModuleHeight(0, 0);
            if (attr.existsAttribute("FontSize")) SetScale(attr.getAttributeAsFloat("FontSize") / LineHeight);
            return true;
        }

        public void SetScale(float sx, float sy) { m_scaleX = sx; m_scaleY = sy; }
        public void SetScale(float s) { m_scaleX = s; m_scaleY = s; }
        public float GetScaleX() { return m_scaleX; }
        public float GetScaleY() { return m_scaleY; }

        int FindChar(char c)
        {
            int left = 0, right = m_pCharMap.Length - 1;
            while (left <= right)
            {
                int mid = (left + right) / 2;
                char m = m_pCharMap[m_pCharIndex[mid]];
                if (m == c) return m_pCharIndex[mid];
                if (m < c) left = mid + 1; else right = mid - 1;
            }
            return 0;
        }

        float GetCharWidth(char c) { return m_pSprite.GetFModuleWidth(0, FindChar(c)) * m_scaleX; }

        float DrawChar(char c, float x, float y, int alpha)
        {
            bool scale = Math.Abs(m_scaleX - 1.0f) > 0.01f || Math.Abs(m_scaleY - 1.0f) > 0.01f;
            if (scale) ASprite.PushTransform(new v3f(x, y, 0), new v3f(m_scaleX, m_scaleY, 1), new v3f(0, 0, 0));
            int index = FindChar(c);
            m_pSprite.PaintFModule(0, index, (int)x, (int)y, 0, 0, 0, alpha);
            if (scale) ASprite.PopTransform();
            return m_pSprite.GetFModuleWidth(0, index) * m_scaleX;
        }

        public d2f GetSubStringSize(string s, int begin, int end)
        {
            d2f size = new d2f(0, 0);
            float width = 0;
            for (int i = begin; i < end; i++)
            {
                char c = s[i];
                if (c == SpaceChar || c == NonBreakableSpaceChar) width += SpaceWidth * m_scaleX;
                else if (c == NewLineChar)
                {
                    size.Width = Math.Max(size.Width, width);
                    size.Height += LineHeight * m_scaleY + LineSpacing;
                    width = 0;
                }
                else if (c == OptionalNewLineChar) { }
                else width += GetCharWidth(c) + (width > 0 ? CharSpacing : 0);
            }
            if (width > 0)
            {
                size.Width = Math.Max(size.Width, width);
                size.Height += size.Height != 0 ? LineHeight * m_scaleY + LineSpacing : LineHeight * m_scaleY;
            }
            return size;
        }

        public void DrawSubString(string s, int begin, int end, float x, float y, int align, int alpha = 255)
        {
            if (align != 0)
            {
                d2f size = GetSubStringSize(s, begin, end);
                if ((align & HCENTER) != 0) x -= size.Width / 2;
                if ((align & VCENTER) != 0) y -= size.Height / 2;
                if ((align & RIGHT) != 0) x -= size.Width;
                if ((align & BOTTOM) != 0) y -= size.Height;
            }
            float px = x, py = y - m_pSprite.GetFModuleOffY(0, 0) * m_scaleY;
            for (int i = begin; i < end; i++)
            {
                char c = s[i];
                if (c == SpaceChar || c == NonBreakableSpaceChar) px += SpaceWidth * m_scaleX;
                else if (c == NewLineChar) { px = x; py += LineHeight * m_scaleY + LineSpacing; }
                else if (c == OptionalNewLineChar) { }
                else px += DrawChar(c, px, py, alpha) + CharSpacing * m_scaleX;
            }
        }

        public d2f GetStringSize(string s) { return s == null ? new d2f() : GetSubStringSize(s, 0, s.Length); }

        public void DrawString(string str, float x, float y, int align, int alpha = 255)
        {
            if (str == null) return;
            if (align != 0)
            {
                d2f size = GetStringSize(str);
                if ((align & VCENTER) != 0) y -= size.Height / 2;
                if ((align & BOTTOM) != 0) y -= size.Height;
                align &= ~VCENTER;
                align &= ~BOTTOM;
            }
            float py = y;
            int last = 0;
            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] == NewLineChar)
                {
                    DrawSubString(str, last, i, x, py, align, alpha);
                    py += LineHeight * m_scaleY + LineSpacing;
                    last = i + 1;
                }
            }
            if (last < str.Length) DrawSubString(str, last, str.Length, x, py, align, alpha);
        }

        public d2f GetWrappedTextSize(string str, int maxWidth, bool update = true)
        {
            if (update) UpdateTextInfo(str, maxWidth);
            return new d2f(m_infoW, m_infoH);
        }

        public int GetWrappedTextLines(string str, int maxWidth, bool update = true)
        {
            if (update) UpdateTextInfo(str, maxWidth);
            return m_infoLines;
        }

        void UpdateTextInfo(string str, float maxWidth)
        {
            m_infoW = 0; m_infoH = 0; m_infoLines = 0;
            m_infoOffsets.Clear();
            if (str == null) return;
            int begin = 0, last = -1;
            float lastWidth = 0, width = 0;
            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];
                if (c == OptionalNewLineChar) { last = i; lastWidth = width; }
                else if (c == SpaceChar) { last = i; lastWidth = width; width += SpaceWidth * m_scaleX; }
                else if (c == NonBreakableSpaceChar) width += SpaceWidth * m_scaleX;
                else if (c == NewLineChar)
                {
                    m_infoW = Math.Max(m_infoW, width);
                    m_infoH += LineHeight * m_scaleY + LineSpacing;
                    m_infoLines++;
                    m_infoOffsets.Add(begin); m_infoOffsets.Add(i);
                    width = 0; begin = i + 1; last = -1;
                }
                else width += GetCharWidth(c) + (width > 0 ? CharSpacing * m_scaleX : 0);

                if (width > maxWidth && last >= 0)
                {
                    m_infoW = Math.Max(m_infoW, lastWidth);
                    m_infoH += LineHeight * m_scaleY + LineSpacing;
                    m_infoLines++;
                    m_infoOffsets.Add(begin); m_infoOffsets.Add(last);
                    width -= lastWidth;
                    if (str[last] == SpaceChar) width -= SpaceWidth * m_scaleX;
                    begin = last + 1;
                    last = -1;
                }
            }
            if (width > 0)
            {
                m_infoW = Math.Max(m_infoW, width);
                m_infoH += m_infoH != 0 ? LineHeight * m_scaleY + LineSpacing : LineHeight * m_scaleY;
                m_infoLines++;
                m_infoOffsets.Add(begin); m_infoOffsets.Add(str.Length);
            }
        }

        public void DrawWrappedText(string str, float x, float y, int maxWidth, int align, int alpha = 255, bool update = true)
        {
            if (str == null) return;
            if (update) UpdateTextInfo(str, maxWidth);
            if (align != 0)
            {
                if ((align & VCENTER) != 0) y -= m_infoH / 2;
                if ((align & BOTTOM) != 0) y -= m_infoH;
                align &= ~VCENTER;
                align &= ~BOTTOM;
            }
            float py = y;
            for (int i = 0; i < m_infoLines; i++)
            {
                DrawSubString(str, m_infoOffsets[i * 2], m_infoOffsets[i * 2 + 1], x, py, align, alpha);
                py += LineHeight * m_scaleY + LineSpacing;
            }
        }

        public void EnableGradient(bool enable)
        {
            m_gradient = enable;
            m_pSprite._material.MaterialType = enable ? E_MATERIAL_TYPE.EMT_A8I8_GRADIENT : E_MATERIAL_TYPE.EMT_TRANSPARENT_TEXTURE_VERTEX_ALPHA;
        }

        public bool IsGradientEnabled() { return m_gradient; }

        public void SetColor1(SColor color)
        {
            SColor c = m_pSprite._color;
            c.R = color.R; c.G = color.G; c.B = color.B;
            m_pSprite._color = c;
        }

        public void SetColor2(SColor color)
        {
            SColor c = m_pSprite._material.EmissiveColor;
            c.R = color.R; c.G = color.G; c.B = color.B;
            m_pSprite._material.EmissiveColor = c;
        }

        public void SetAlpha(int alpha)
        {
            alpha = Math.Max(0, Math.Min(255, alpha));
            SColor c = m_pSprite._color;
            c.A = alpha;
            m_pSprite._color = c;
        }

        public SColor GetColor1() { SColor c = m_pSprite._color; c.A = 255; return c; }
        public SColor GetColor2() { SColor c = m_pSprite._material.EmissiveColor; c.A = 255; return c; }
        public int GetAlpha() { return m_pSprite._color.A; }
    }

    public class CFontManager
    {
        static CFontManager s_instance;
        public static CFontManager GetInstance() { return s_instance ?? (s_instance = new CFontManager()); }

        bool m_initialized;
        readonly Dictionary<string, CFont> m_fontMap = new Dictionary<string, CFont>();

        public CFont GetFont(string name)
        {
            if (!m_initialized) m_initialized = CGameConfig.GetInstance().importFile("config/fonts.xml");
            string fontName = name;
            IAttributes attr = CGameConfig.GetInstance().getAttributes("font", name);
            if (attr != null && CStringTable.GetInstance().GetLanguage() == ELanguages.Language_Japanese && attr.existsAttribute("JapaneseEquivalent"))
                fontName = attr.getAttributeAsString("JapaneseEquivalent");
            CFont font;
            if (m_fontMap.TryGetValue(fontName, out font)) return font;
            font = new CFont();
            font.Load(fontName);
            m_fontMap[fontName] = font;
            return font;
        }

        public void ReleaseFonts() { m_fontMap.Clear(); }
    }
}
