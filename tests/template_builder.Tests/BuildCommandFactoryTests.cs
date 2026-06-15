using System.CommandLine;
using System.Linq;
using Core.Commands;
using Utils;
using Xunit;

namespace template_builder.Tests;

public class BuildCommandFactoryTests {
	[Fact]
	public void CreateCommand_ReturnsBuildCommandWithExpectedName() {
		var factory = new BuildCommandFactory(new TestLogger());

		var cmd = factory.CreateCommand();

		Assert.Equal("build", cmd.Name);
	}

	[Fact]
	public void CreateCommand_ExposesAllExpectedOptions() {
		var factory = new BuildCommandFactory(new TestLogger());

		var cmd = factory.CreateCommand();
		// System.CommandLine 把第一个位置参数当作 Name，剩余为 Aliases
		var names = cmd.Options.Select(o => o.Name).ToHashSet();
		var aliases = cmd.Options.SelectMany(o => o.Aliases).ToHashSet();

		Assert.Contains("--source-files-folder", names);
		Assert.Contains("-s", aliases);
		Assert.Contains("--output", names);
		Assert.Contains("-o", aliases);
		Assert.Contains("--verbose", names);
		Assert.Contains("-v", aliases);
		Assert.Contains("--config", names);
		Assert.Contains("-c", aliases);
	}
}
