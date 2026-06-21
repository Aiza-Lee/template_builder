using System.CommandLine;
using System.IO;
using System.Linq;
using Core.Commands;
using Utils;
using Xunit;

namespace template_builder.Tests;

public class RootCommandFactoryTests {
	[Fact]
	public void CreateRootCommand_ExposesAllThreeSubcommands() {
		var factory = new RootCommandFactory(new TestLogger());

		var root = factory.CreateRootCommand();

		var subcommandNames = root.Subcommands.Select(c => c.Name).ToHashSet();
		Assert.Contains("build", subcommandNames);
		Assert.Contains("validate", subcommandNames);
		Assert.Contains("init", subcommandNames);
	}

	[Fact]
	public void CreateRootCommand_BuildSubcommand_ExposesAllExpectedOptions() {
		var factory = new RootCommandFactory(new TestLogger());

		var root = factory.CreateRootCommand();
		var build = root.Subcommands.Single(c => c.Name == "build");
		var names = build.Options.Select(o => o.Name).ToHashSet();
		var aliases = build.Options.SelectMany(o => o.Aliases).ToHashSet();

		Assert.Contains("--source-files-folder", names);
		Assert.Contains("-s", aliases);
		Assert.Contains("--output", names);
		Assert.Contains("-o", aliases);
		Assert.Contains("--verbose", names);
		Assert.Contains("-v", aliases);
		Assert.Contains("--config", names);
		Assert.Contains("-c", aliases);
		Assert.Contains("--template-dir", names);
		Assert.Contains("-t", aliases);
	}

	[Fact]
	public void Invoke_Build_WithNonExistentSourceFolder_ReturnsNonZero() {
		var factory = new RootCommandFactory(new TestLogger());
		var root = factory.CreateRootCommand();
		var outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pdf");

		var result = root.Parse(new[] { "build", "-s", "/no/such/source/folder", "-o", outputPath }).Invoke();

		Assert.NotEqual(0, result);
	}

	[Fact]
	public void Invoke_NoSubcommand_ReturnsInvalidArguments() {
		var factory = new RootCommandFactory(new TestLogger());
		var root = factory.CreateRootCommand();

		var result = root.Parse(Array.Empty<string>()).Invoke();

		// ExitCodes.InvalidArguments = 2
		Assert.Equal(2, result);
	}

	[Fact]
	public void Invoke_Validate_IsWiredUpToStub() {
		var factory = new RootCommandFactory(new TestLogger());
		var root = factory.CreateRootCommand();

		var result = root.Parse(new[] { "validate", "-s", "/tmp", "-c", "/tmp/cfg.json" }).Invoke();

		// stub: not yet implemented → non-zero
		Assert.NotEqual(0, result);
	}

	[Fact]
	public void Invoke_Init_IsWiredUpToStub() {
		var factory = new RootCommandFactory(new TestLogger());
		var root = factory.CreateRootCommand();

		var result = root.Parse(new[] { "init", "-o", "/tmp/cfg.jsonc" }).Invoke();

		// stub: not yet implemented → non-zero
		Assert.NotEqual(0, result);
	}
}
