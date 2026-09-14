using System.Collections.Generic;
using Core;

namespace template_builder.Tests.Fixtures {
    /// <summary>
    /// 记录 xelatex 子进程调用并返回预设结果的 fake runner。仅用于单测，避免真的 spawn xelatex。
    /// </summary>
    internal class FakeXelatexRunner : IXelatexRunner {
        public record Call(string WorkingDir, string Arguments, int TimeoutSeconds, List<string>? ExtraFontDirs = null);

        public List<Call> Calls { get; } = new();
        public Queue<XelatexResult> Results { get; } = new();

        /// <summary>
        /// 每次 Run() 调用时执行的副作用列表（与 Results 一一对应）。
        /// 可用于模拟 xelatex 生成 PDF / 中间文件等副作用。null 项表示无副作用。
        /// </summary>
        public Queue<Action<string, string>?> SideEffects { get; } = new();

        public XelatexResult Run(string workingDir, string arguments, int timeoutSeconds, IEnumerable<string>? extraFontDirs = null) {
            Calls.Add(new Call(workingDir, arguments, timeoutSeconds, extraFontDirs?.ToList()));
            // 执行副作用（如果有）
            if (SideEffects.Count > 0) {
                var sideEffect = SideEffects.Dequeue();
                sideEffect?.Invoke(workingDir, arguments);
            }
            if (Results.Count == 0) {
                return new XelatexResult(0, "", false); // 默认成功
            }
            return Results.Dequeue();
        }
    }
}
