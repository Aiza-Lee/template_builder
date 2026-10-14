using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Core.Tui {
    /// <summary>
    /// 全局终端宽度需求注册中心。
    /// 解决终端宽度硬编码问题，各组件/视图可独立注册自身所需的最小可用宽度，
    /// 注册中心全局动态取最大值作为整体界面的终端宽度准入阈值。
    /// 当实际终端宽度低于此阈值时，自动触发“窗口过小”警告面板并停止渲染业务内容。
    /// </summary>
    public static class TerminalWidthRegistry {
        private static readonly object _lock = new();
        private static readonly Dictionary<string, int> _registrations = new(StringComparer.OrdinalIgnoreCase);

        static TerminalWidthRegistry() {
            InitializeDefaults();
        }

        /// <summary>
        /// 初始化内置核心组件的最小宽度需求。
        /// </summary>
        public static void InitializeDefaults() {
            lock (_lock) {
                _registrations.Clear();
                _registrations["LineEditor"] = 40;
                _registrations["Breadcrumb"] = 45;
                _registrations["Banner"] = 48;
                _registrations["FieldCard"] = 50;
                _registrations["OverridesTable"] = 55;
                _registrations["Dashboard"] = 60;
                _registrations["SourceTreePreview"] = 60;
                _registrations["Menu"] = 60;
            }
        }

        /// <summary>
        /// 注册指定组件所需的最小终端宽度。
        /// </summary>
        /// <param name="componentName">组件唯一名称，如 "Menu", "Dashboard"</param>
        /// <param name="minWidth">该组件正常排版所需的最小列宽（正整数）</param>
        public static void Register(string componentName, int minWidth) {
            if (string.IsNullOrWhiteSpace(componentName)) {
                throw new ArgumentException("组件名称不能为空", nameof(componentName));
            }
            if (minWidth <= 0) {
                throw new ArgumentOutOfRangeException(nameof(minWidth), "组件所需宽度必须为正整数");
            }

            lock (_lock) {
                _registrations[componentName] = minWidth;
            }
        }

        /// <summary>
        /// 注销指定组件的宽度需求。
        /// </summary>
        public static bool Unregister(string componentName) {
            if (string.IsNullOrWhiteSpace(componentName)) return false;
            lock (_lock) {
                return _registrations.Remove(componentName);
            }
        }

        /// <summary>
        /// 获取所有注册组件中要求的最大宽度值。
        /// </summary>
        public static int GetRequiredWidth() {
            lock (_lock) {
                return _registrations.Count > 0 ? _registrations.Values.Max() : 60;
            }
        }

        /// <summary>
        /// 获取导致当前最大宽度的瓶颈组件信息。
        /// </summary>
        public static (string ComponentName, int Width) GetBottleneck() {
            lock (_lock) {
                if (_registrations.Count == 0) return ("Default", 60);
                var maxEntry = _registrations.OrderByDescending(kv => kv.Value).First();
                return (maxEntry.Key, maxEntry.Value);
            }
        }

        /// <summary>
        /// 获取当前所有已注册组件及其宽度要求的只读快照。
        /// </summary>
        public static IReadOnlyDictionary<string, int> GetRegistrations() {
            lock (_lock) {
                return new Dictionary<string, int>(_registrations, StringComparer.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// 判定指定宽度是否低于当前要求的最小宽度。
        /// </summary>
        public static bool IsWidthTooSmall(int currentWidth) {
            return currentWidth < GetRequiredWidth();
        }

        /// <summary>
        /// 生成“终端窗口过小”警告面板，明确指示当前宽度与要求的最小宽度。
        /// </summary>
        public static IRenderable RenderTooSmallWarning(int currentWidth, int requiredWidth) {
            var grid = new Grid().AddColumn();
            grid.AddRow(new Markup($"[bold {TuiTheme.Danger}]{Core.Localization.I18n.T("Terminal.TooSmallTitle", "✘ 终端窗口过小")}[/]"));
            grid.AddRow(new Text(string.Empty));
            grid.AddRow(new Markup($"[grey]{Core.Localization.I18n.T("Terminal.CurrentWidth", "当前终端宽度：")}[/][bold {TuiTheme.Warning}]{currentWidth}[/] [grey]{Core.Localization.I18n.T("Terminal.Columns", "列")}[/]"));
            grid.AddRow(new Markup($"[grey]{Core.Localization.I18n.T("Terminal.RequiredWidth", "要求最小宽度：")}[/][bold {TuiTheme.Success}]{requiredWidth}[/] [grey]{Core.Localization.I18n.T("Terminal.Columns", "列")}[/]"));
            grid.AddRow(new Text(string.Empty));
            grid.AddRow(new Markup($"[grey]{Core.Localization.I18n.T("Terminal.ResizeTip", "提示：请拉大终端窗口以继续展示界面内容...")}[/]"));

            return new Panel(grid)
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.FromHex(TuiTheme.Warning))
                .Header($"[{TuiTheme.Danger}]{Core.Localization.I18n.T("Terminal.AlertHeader", " 窗口尺寸告警 ")}[/]");
        }

        /// <summary>
        /// 阻塞直到终端宽度达到要求，实时检测并更新宽度提示（适用于步骤流转或准备渲染前）。
        /// 当终端尺寸恢复达标时，可自动回调 onRestored 触发界面/Banner 自适应重绘。
        /// </summary>
        public static void EnsureAdequateWidth(IAnsiConsole? console = null, Action<int>? onRestored = null) {
            if (!TuiEnvironment.SupportsInteractiveUi() || Console.IsInputRedirected) {
                onRestored?.Invoke(TuiEnvironment.SafeWindowWidth());
                return;
            }

            var required = GetRequiredWidth();
            var current = TuiEnvironment.SafeWindowWidth();
            if (current < required) {
                var ansi = console ?? AnsiConsole.Console;
                while (current < required) {
                    ansi.Clear();
                    ansi.Write(RenderTooSmallWarning(current, required));

                    while (!Console.KeyAvailable) {
                        var w = TuiEnvironment.SafeWindowWidth();
                        if (w != current) {
                            current = w;
                            break;
                        }
                        Thread.Sleep(50);
                    }

                    if (Console.KeyAvailable) {
                        try { Console.ReadKey(true); } catch { }
                    }

                    current = TuiEnvironment.SafeWindowWidth();
                    required = GetRequiredWidth();
                }

                ansi.Clear();
            }

            onRestored?.Invoke(current);
        }

        /// <summary>
        /// 全局终端宽度保障并自适应重绘：确保终端宽度满足要求，并在宽度达标/变动后自适应触发 renderAction。
        /// </summary>
        public static void EnsureAdequateWidthAndRender(IAnsiConsole console, Action<int> renderAction) {
            EnsureAdequateWidth(console, onRestored: renderAction);
        }
    }
}
