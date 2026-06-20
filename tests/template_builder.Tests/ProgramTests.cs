using System.Linq;
using Xunit;

namespace template_builder.Tests;

public class ProgramTests {
	[Fact]
	public void CreateRootCommand_RegistersBuildCommand() {
		var logger = new TestLogger();

		var root = global::Program.Program.CreateRootCommand(logger);

		Assert.Contains(root.Subcommands, c => c.Name == "build");
	}

	[Fact]
	public void CreateRootCommand_DoesNotThrowWithInjectedLogger() {
		var logger = new TestLogger();

		var root = global::Program.Program.CreateRootCommand(logger);

		// 装配过程不应主动写日志（只在 Main 的 Info("Application started...") 中触发）
		Assert.NotNull(root);
		Assert.Empty(logger.Entries);
	}
}
