using System.Collections.Generic;
using Core;

namespace template_builder.Tests.Fixtures {
	/// <summary>
	/// 记录 xelatex 子进程调用并返回预设结果的 fake runner。仅用于单测，避免真的 spawn xelatex。
	/// </summary>
	internal class FakeXelatexRunner : IXelatexRunner {
		public record Call(string WorkingDir, string Arguments, int TimeoutSeconds);

		public List<Call> Calls { get; } = new();
		public Queue<XelatexResult> Results { get; } = new();

		public XelatexResult Run(string workingDir, string arguments, int timeoutSeconds) {
			Calls.Add(new Call(workingDir, arguments, timeoutSeconds));
			if (Results.Count == 0) {
				return new XelatexResult(0, "", false); // 默认成功
			}
			return Results.Dequeue();
		}
	}
}
