using Spectre.Console;

namespace Core.Tui {
    /// <summary>
    /// TUI 运行环境的可用性判定。
    /// <para>
    /// 存在的理由：Spectre.Console 在非交互模式下读取按键会抛
    /// <c>InvalidOperationException: Failed to read input in non-interactive mode</c>，
    /// 若不在进入 TUI 前拦截，用户看到的是整段未捕获异常堆栈（真实踩过）。
    /// 典型触发场景：`--tui` 被写进 CI 脚本、被管道接住、或输出重定向到文件。
    /// </para>
    /// </summary>
    internal static class TuiEnvironment {
        /// <summary>当前进程能否承载全屏交互界面。</summary>
        public static bool SupportsInteractiveUi() => Evaluate(
            inputRedirected: Console.IsInputRedirected,
            outputRedirected: Console.IsOutputRedirected,
            capabilitiesInteractive: AnsiConsole.Profile.Capabilities.Interactive
        );

        /// <summary>
        /// 纯函数形式的判定逻辑，便于单元测试（真实进程的重定向状态在测试宿主下不稳定）。
        /// </summary>
        /// <remarks>
        /// 输入与输出都必须未被重定向：全屏界面既要读按键也要直接写终端；
        /// 任一被重定向时 Live 渲染与按键读取都无法正常工作。
        /// </remarks>
        internal static bool Evaluate(bool inputRedirected, bool outputRedirected, bool capabilitiesInteractive) =>
            !inputRedirected && !outputRedirected && capabilitiesInteractive;

        /// <summary>
        /// 安全获取当前终端窗口宽度。
        /// 在重定向、CI 或非交互无头环境下返回安全后备值（默认 80）。
        /// </summary>
        public static int SafeWindowWidth(int fallback = 80) {
            try {
                return Console.WindowWidth > 0 ? Console.WindowWidth : fallback;
            } catch {
                return fallback;
            }
        }
    }
}
