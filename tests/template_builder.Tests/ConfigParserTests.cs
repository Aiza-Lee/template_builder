using System;
using Utils;
using Xunit;

namespace template_builder.Tests;

public class ConfigParserTests {
	[Fact]
	public void ParseConfigFile_OverridesDefaultsAndKeepsUnspecifiedValues() {
		var logger = new TestLogger();
		var parser = new ConfigParser("TEX", logger);

		parser.ParseConfigFile(
			"""
			{
				"TEX": {
					"author": "Unit Tester",
					"code": {
						"font_family": "CustomFont",
						"auto_break_lines": false,
						"tab_size": 8
					},
					"title": {
						"content": "Custom Title"
					}
				}
			}
			"""
		);

		Assert.Equal("Unit Tester", parser["AUTHOR"].GetAsString());
		Assert.Equal("CustomFont", parser["CODE_FONT_FAMILY"].GetAsString());
		Assert.Equal(8, parser["CODE_TAB_SIZE"].GetAsInt());
		Assert.Equal("Custom Title", parser["TITLE_CONTENT"].GetAsString());
		Assert.Equal("Times New Roman", parser["GLOBAL_MAIN_FONT"].GetAsString());
	}

	[Fact]
	public void ParseConfigFile_InvalidRoot_LogsErrorAndKeepsDefaults() {
		var logger = new TestLogger();
		var parser = new ConfigParser("TEX", logger);

		parser.ParseConfigFile("""{ "PROGRAM": { "include_file_types": [".foo"] } }""");

		Assert.Contains(logger.Entries, entry =>
			entry.Level == LogLevel.ERROR && entry.Message.Contains("does not contain 'TEX'", StringComparison.OrdinalIgnoreCase));
		Assert.Equal("Aiza", parser["AUTHOR"].GetAsString());
	}

	[Fact]
	public void ParseConfigFile_StrictMode_ThrowsOnUnknownKey() {
		var logger = new TestLogger();
		var parser = new ConfigParser("TEX", logger, ConfigStrictness.Strict);

		var ex = Assert.Throws<UnknownConfigKeyException>(() =>
			parser.ParseConfigFile(
				"""
				{
					"TEX": {
						"totally_made_up_key": "oops"
					}
				}
				"""
			)
		);

		Assert.Contains("TOTALLY_MADE_UP_KEY", ex.Message);
	}

	[Fact]
	public void ParseConfigFile_LaxMode_WarnsOnUnknownKey() {
		var logger = new TestLogger();
		var parser = new ConfigParser("TEX", logger); // 默认 Lax

		parser.ParseConfigFile(
			"""
			{
				"TEX": {
					"totally_made_up_key": "oops"
				}
			}
			"""
		);

		Assert.Contains(logger.Entries, entry =>
			entry.Level == LogLevel.WARNING && entry.Message.Contains("TOTALLY_MADE_UP_KEY"));
	}
}
