using UnityEngine;

namespace OfficeHell.View
{
    /// <summary>
    /// The greybox needs Chinese glyphs without shipping a font asset. A dynamic OS font covers
    /// that with zero setup. TextMeshPro with a generated SDF atlas is the answer for the real
    /// build, and this is the reason none of the ui code hardcodes a font reference.
    /// </summary>
    public static class FontProvider
    {
        static readonly string[] Candidates =
        {
            "Microsoft YaHei",
            "微软雅黑",
            "SimHei",
            "PingFang SC",
            "Noto Sans CJK SC",
            "Arial Unicode MS",
            "Arial",
        };

        static Font _font;

        public static Font Font
        {
            get
            {
                if (_font != null)
                {
                    return _font;
                }

                _font = Font.CreateDynamicFontFromOSFont(Candidates, 32);
                if (_font == null)
                {
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }

                if (_font == null)
                {
                    Debug.LogWarning("[FontProvider] no usable font found, labels will be blank");
                }

                return _font;
            }
        }

        public static Material FontMaterial
        {
            get
            {
                Font f = Font;
                return f != null ? f.material : null;
            }
        }
    }
}
