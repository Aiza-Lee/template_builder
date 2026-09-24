using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Models {
    /// <summary>
    /// 排版预设：提供比赛打印、屏幕阅读、紧凑速查等一键排版方案。
    /// </summary>
    /// <param name="Id">预设标识符（如 competition, reading, compact）</param>
    /// <param name="Name">预设名称</param>
    /// <param name="Tagline">简明技术摘要（纸张、栏数、字号、行距、边距）</param>
    /// <param name="Description">用途解释与排版设计意图</param>
    /// <param name="Settings">生效的键值对集合</param>
    internal sealed record LayoutPreset(
        string Id,
        string Name,
        string Tagline,
        string Description,
        IReadOnlyDictionary<string, string> Settings
    );

    /// <summary>
    /// 内置排版预设中心。
    /// </summary>
    internal static class LayoutPresets {
        /// <summary>
        /// 比赛打印预设：面向 ACM/ICPC/CCPC 等程序设计竞赛打印与纸质打铁场景。
        /// A4 横向双栏、代码 6pt/6pt 极致紧凑、1cm 边距、关闭彩色链接（纯黑白无重影）、关闭独立封面、保留目录。
        /// </summary>
        public static readonly LayoutPreset Competition = new(
            Id: "competition",
            Name: "比赛打印 (Competition Print)",
            Tagline: "A4 横向 · 双栏 · 6pt/6pt · 1cm 边距 · 黑白打印友好 · 无封面有目录",
            Description: "面向 ACM/ICPC 竞赛打印与纸质速查。最大化纸张利用率，紧凑双栏，黑白友好，关闭封面，保留目录。",
            Settings: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                ["DOCCLASS_ORIENTATION"] = "landscape",
                ["LAYOUT_COLUMNS"] = "2",
                ["LAYOUT_TOC_IN_COLUMNS"] = "false",
                ["CODE_FONT_SIZE"] = "6pt",
                ["CODE_LINE_HEIGHT"] = "6pt",
                ["DOCCLASS_BASE_FONT_SIZE"] = "10pt",
                ["GEOMETRY_LEFT_MARGIN"] = "1cm",
                ["GEOMETRY_RIGHT_MARGIN"] = "1cm",
                ["GEOMETRY_TOP_MARGIN"] = "1cm",
                ["GEOMETRY_BOTTOM_MARGIN"] = "1.2cm",
                ["GEOMETRY_COLUMN_SEP"] = "1cm",
                ["GEOMETRY_COLUMN_RULE"] = "false",
                ["TITLE_ENABLE_COVER"] = "false",
                ["TOC_ENABLE_TOC"] = "true",
                ["HYPERREF_ENABLE_COLORLINKS"] = "false",
                ["CODE_BG_COLOR"] = "gray!3",
                ["CODE_BREAK_LINES"] = "true"
            }
        );

        /// <summary>
        /// 屏幕阅读预设：面向电脑屏幕、平板设备阅览与日常代码评审。
        /// A4 纵向单栏、代码 10pt/13pt 适度行距、2cm 舒适边距、开启彩色超链接便于点击跳转、关闭独立封面、保留目录。
        /// </summary>
        public static readonly LayoutPreset Reading = new(
            Id: "reading",
            Name: "屏幕阅读 (Screen Reading)",
            Tagline: "A4 纵向 · 单栏 · 10pt/13pt · 2cm 边距 · 彩色超链接 · 舒适阅读",
            Description: "面向电子屏幕、平板及日常代码评审。单栏舒适行宽，代码字号增大至 10pt 并留足行距，开启彩色超链接便于点击跳转。",
            Settings: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                ["DOCCLASS_ORIENTATION"] = "portrait",
                ["LAYOUT_COLUMNS"] = "1",
                ["LAYOUT_TOC_IN_COLUMNS"] = "true",
                ["CODE_FONT_SIZE"] = "10pt",
                ["CODE_LINE_HEIGHT"] = "13pt",
                ["DOCCLASS_BASE_FONT_SIZE"] = "10pt",
                ["GEOMETRY_LEFT_MARGIN"] = "2cm",
                ["GEOMETRY_RIGHT_MARGIN"] = "2cm",
                ["GEOMETRY_TOP_MARGIN"] = "2cm",
                ["GEOMETRY_BOTTOM_MARGIN"] = "2cm",
                ["GEOMETRY_COLUMN_SEP"] = "1cm",
                ["GEOMETRY_COLUMN_RULE"] = "false",
                ["TITLE_ENABLE_COVER"] = "false",
                ["TOC_ENABLE_TOC"] = "true",
                ["HYPERREF_ENABLE_COLORLINKS"] = "true",
                ["CODE_BG_COLOR"] = "gray!3",
                ["CODE_BREAK_LINES"] = "true"
            }
        );

        /// <summary>
        /// 紧凑速查预设：面向多栏极速查阅与极致纸张利用。
        /// A4 横向双栏、代码 5.5pt/5.5pt、0.6cm 超小边距、双栏中缝竖线分割、关闭封面与目录，第 1 页直接开始代码正文。
        /// </summary>
        public static readonly LayoutPreset CompactCheatSheet = new(
            Id: "compact",
            Name: "紧凑速查 (Compact CheatSheet)",
            Tagline: "A4 横向 · 双栏 · 5.5pt/5.5pt · 0.6cm 边距 · 中缝垂直线 · 第1页直入正文",
            Description: "面向多栏极速查阅与极限纸张利用。双栏极致排版，超小边距，带垂直分隔线，关闭封面与目录，首页直接开始代码正文。",
            Settings: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                ["DOCCLASS_ORIENTATION"] = "landscape",
                ["LAYOUT_COLUMNS"] = "2",
                ["LAYOUT_TOC_IN_COLUMNS"] = "false",
                ["CODE_FONT_SIZE"] = "5.5pt",
                ["CODE_LINE_HEIGHT"] = "5.5pt",
                ["DOCCLASS_BASE_FONT_SIZE"] = "10pt",
                ["GEOMETRY_LEFT_MARGIN"] = "0.6cm",
                ["GEOMETRY_RIGHT_MARGIN"] = "0.6cm",
                ["GEOMETRY_TOP_MARGIN"] = "0.6cm",
                ["GEOMETRY_BOTTOM_MARGIN"] = "0.8cm",
                ["GEOMETRY_COLUMN_SEP"] = "0.6cm",
                ["GEOMETRY_COLUMN_RULE"] = "true",
                ["TITLE_ENABLE_COVER"] = "false",
                ["TOC_ENABLE_TOC"] = "false",
                ["HYPERREF_ENABLE_COLORLINKS"] = "false",
                ["CODE_BG_COLOR"] = "gray!3",
                ["CODE_BREAK_LINES"] = "true"
            }
        );

        /// <summary>全部内置预设清单</summary>
        public static readonly IReadOnlyList<LayoutPreset> All = [
            Competition,
            Reading,
            CompactCheatSheet
        ];

        /// <summary>
        /// 按预设 ID 或名称查找预设。
        /// </summary>
        public static LayoutPreset? FindById(string? id) {
            if (string.IsNullOrWhiteSpace(id)) return null;
            var clean = id.Trim();
            return All.FirstOrDefault(p =>
                string.Equals(p.Id, clean, StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains(clean, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 将指定预设应用至项目配置会话中（作为运行时覆盖注入，同时保留其他未冲突配置）。
        /// </summary>
        public static void Apply(ProjectConfiguration configuration, LayoutPreset preset) {
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(preset);

            foreach (var (key, value) in preset.Settings) {
                configuration.TrySetOverride(key, value);
            }
        }
    }
}
