using Spectre.Console;

namespace Core.Tui {
    /// <summary>
    /// TUI 视觉设计系统与统一低饱和度色系（Nord / Muted Slate 风格）。
    /// 提供统一的颜色常量、色彩标记格式化及低饱和度平滑渐变光谱。
    /// </summary>
    internal static class TuiTheme {
        // 主题色常量（低饱和度、统一冷暖平衡色系）
        public const string Primary = "#7aa2f7";      // 柔和石板蓝 (主强调/品牌/光标)
        public const string PrimaryDim = "#5e81ac";   // 哑光钢蓝
        public const string Secondary = "#88c0d0";    // 柔和冰青 (次强调/分类/高亮)
        public const string Accent = "#81a1c1";       // 霜雾蓝
        public const string Success = "#a3be8c";      // 灰绿/鼠尾草绿 (成功/有效)
        public const string Warning = "#e0af68";      // 暖琥珀/柔和橙黄 (已修改/警告)
        public const string Danger = "#bf616a";       // 柔和珊瑚红/砖红 (错误/清空)
        public const string Muted = "#6c7086";        // 哑光石板灰 (辅助文字/次要信息)
        public const string Border = "#4c566a";       // 边框微弱色
        public const string Surface = "#2e3440";      // 高亮背景色 (选中底色)
        public const string TextLight = "#d8dee9";    // 亮白文本
        public const string TextDim = "#8892b0";      // 次要说明文本

        // 低饱和度渐变光谱（优雅冷色调横向平滑过渡，杜绝高饱和度霓虹刺激）
        private static readonly (int R, int G, int B)[] s_spectrum = [
            (122, 162, 247), // #7aa2f7 柔和蓝
            (129, 161, 193), // #81a1c1 霜雾蓝
            (136, 192, 208), // #88c0d0 冰青
            (143, 188, 187), // #8fbcbb 灰绿青
            (163, 190, 140), // #a3be8c 鼠尾草绿
            (180, 142, 173), // #b48ead 柔和灰紫
            (122, 162, 247)  // 闭合
        ];

        /// <summary>
        /// 根据插值系数 t (0.0 ~ 1.0) 计算低饱和度优雅渐变色 RGB。
        /// </summary>
        public static (int R, int G, int B) GetSpectrumColor(double t) {
            t = ((t % 1.0) + 1.0) % 1.0;
            var stops = s_spectrum;
            var scaled = t * (stops.Length - 1);
            var idx = (int)scaled;
            var frac = scaled - idx;
            if (idx >= stops.Length - 1) return stops[^1];
            var c1 = stops[idx];
            var c2 = stops[idx + 1];
            var r = (int)(c1.R + (c2.R - c1.R) * frac);
            var g = (int)(c1.G + (c2.G - c1.G) * frac);
            var b = (int)(c1.B + (c2.B - c1.B) * frac);
            return (r, g, b);
        }
    }
}
