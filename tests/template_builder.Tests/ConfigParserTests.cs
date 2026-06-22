using System;
using Utils;
using Utils.Exceptions;
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

	[Fact]
	public void ParseConfigFile_InvalidJson_ThrowsMalformedConfigException() {
		var logger = new TestLogger();
		var parser = new ConfigParser("TEX", logger);

		var ex = Assert.Throws<MalformedConfigException>(() =>
			parser.ParseConfigFile("""{ "TEX": {""")
		);

		Assert.Contains("Failed to parse JSON content", ex.Message);
	}

	[Fact]
	public void ParseConfigFile_SourcePath_PropagatedToException() {
		var logger = new TestLogger();
		var parser = new ConfigParser("TEX", logger);

		var ex = Assert.Throws<MalformedConfigException>(() =>
			parser.ParseConfigFile("""{ "TEX": {""", "/tmp/fake/config.json")
		);

		Assert.Equal("/tmp/fake/config.json", ex.SourcePath);
	}

	[Fact]
	public void ParseConfigFile_JsoncWithComments_Accepted() {
		var logger = new TestLogger();
		var parser = new ConfigParser("TEX", logger);

		parser.ParseConfigFile(
			"""
			{
				// single-line comment at top
				"TEX": {
					/* block comment */
					"author": "JsonC Tester",
					"code": {
						"font_family": "JsonCFont" // trailing comment
					}
				}
			}
			"""
		);

		Assert.Equal("JsonC Tester", parser["AUTHOR"].GetAsString());
		Assert.Equal("JsonCFont", parser["CODE_FONT_FAMILY"].GetAsString());
	}

	[Fact]
	public void ParseConfigFile_TrailingComma_Accepted() {
		var logger = new TestLogger();
		var parser = new ConfigParser("TEX", logger);

		parser.ParseConfigFile(
			"""
			{
				"TEX": {
					"author": "Trailing Comma",
					"code": {
						"font_family": "F1",
						"tab_size": 8,
					},
				}
			}
			"""
		);

		Assert.Equal("Trailing Comma", parser["AUTHOR"].GetAsString());
		Assert.Equal(8, parser["CODE_TAB_SIZE"].GetAsInt());
	}

	// ----- GetAsBool(fallback) -----

	[Fact]
	public void GetAsBool_TrueLiteral_ReturnsTrue() {
		var logger = new TestLogger();
		var parser = new ConfigParser("TEX", logger);

		parser.ParseConfigFile("""{ "TEX": { "global": { "cjk_auto_fake_bold": true } } }""");

		Assert.True(parser["GLOBAL_CJK_AUTO_FAKE_BOLD"].GetAsBool(false));
	}

	[Fact]
	public void GetAsBool_FalseLiteral_ReturnsFalse() {
		var logger = new TestLogger();
		var parser = new ConfigParser("TEX", logger);

		parser.ParseConfigFile("""{ "TEX": { "global": { "cjk_auto_fake_bold": false } } }""");

		Assert.False(parser["GLOBAL_CJK_AUTO_FAKE_BOLD"].GetAsBool(true));
	}

	[Fact]
	public void GetAsBool_StringTrue_ParsesToTrue() {
		var logger = new TestLogger();
		var parser = new ConfigParser("TEX", logger);

		parser.ParseConfigFile("""{ "TEX": { "global": { "cjk_auto_fake_bold": "true" } } }""");

		Assert.True(parser["GLOBAL_CJK_AUTO_FAKE_BOLD"].GetAsBool(false));
	}

	[Fact]
	public void GetAsBool_StringFalse_ParsesToFalse() {
		var logger = new TestLogger();
		var parser = new ConfigParser("TEX", logger);

		parser.ParseConfigFile("""{ "TEX": { "global": { "cjk_auto_fake_bold": "false" } } }""");

		Assert.False(parser["GLOBAL_CJK_AUTO_FAKE_BOLD"].GetAsBool(true));
	}

	[Fact]
	public void GetAsBool_StringOne_ParsesToTrue() {
		var logger = new TestLogger();
		var parser = new ConfigParser("TEX", logger);

		parser.ParseConfigFile("""{ "TEX": { "global": { "cjk_auto_fake_bold": "1" } } }""");

		Assert.True(parser["GLOBAL_CJK_AUTO_FAKE_BOLD"].GetAsBool(false));
	}

	[Fact]
	public void GetAsBool_StringZero_ParsesToFalse() {
		var logger = new TestLogger();
		var parser = new ConfigParser("TEX", logger);

		parser.ParseConfigFile("""{ "TEX": { "global": { "cjk_auto_fake_bold": "0" } } }""");

		Assert.False(parser["GLOBAL_CJK_AUTO_FAKE_BOLD"].GetAsBool(true));
	}

	[Fact]
	public void GetAsBool_EmptyString_ReturnsFallback() {
		var logger = new TestLogger();
		var parser = new ConfigParser("TEX", logger);

		parser.ParseConfigFile("""{ "TEX": { "global": { "cjk_auto_fake_bold": "" } } }""");

		Assert.True(parser["GLOBAL_CJK_AUTO_FAKE_BOLD"].GetAsBool(true));
		Assert.False(parser["GLOBAL_CJK_AUTO_FAKE_BOLD"].GetAsBool(false));
	}

	[Fact]
	public void GetAsBool_UnparseableString_ReturnsFallback() {
		var logger = new TestLogger();
		var parser = new ConfigParser("TEX", logger);

		parser.ParseConfigFile("""{ "TEX": { "global": { "cjk_auto_fake_bold": "maybe" } } }""");

		Assert.True(parser["GLOBAL_CJK_AUTO_FAKE_BOLD"].GetAsBool(true));
		Assert.False(parser["GLOBAL_CJK_AUTO_FAKE_BOLD"].GetAsBool(false));
	}

	[Fact]
	public void GetAsBool_MissingKey_ReturnsFallback() {
		var logger = new TestLogger();
		var parser = new ConfigParser("TEX", logger);

		// 用未注册的 key：DefaultConfig.jsonc 没注册 TOTALLY_MADE_UP_BOOL，
		// ConfigParser.this[key] 返回空对象占位 → GetAsBool 走 fallback 路径。
		Assert.True(parser["TOTALLY_MADE_UP_BOOL"].GetAsBool(true));
		Assert.False(parser["TOTALLY_MADE_UP_BOOL"].GetAsBool(false));
	}

	[Fact]
	public void GetAsBool_RegisteredDefault_OverridesFallback() {
		var logger = new TestLogger();
		var parser = new ConfigParser("TEX", logger);

		// GLOBAL_CJK_AUTO_FAKE_BOLD 已在 DefaultConfig.jsonc 注册默认值 true。
		// 即使调用 fallback=false，GetAsBool 也应返回默认值 true（已注册 key 优先于 fallback）。
		Assert.True(parser["GLOBAL_CJK_AUTO_FAKE_BOLD"].GetAsBool(false));
	}

	[Fact]
	public void GetAsBool_NoFallback_ThrowsOnUnparseable() {
		var logger = new TestLogger();
		var parser = new ConfigParser("TEX", logger);

		parser.ParseConfigFile("""{ "TEX": { "global": { "cjk_auto_fake_bold": "maybe" } } }""");

		Assert.Throws<InvalidCastException>(() => parser["GLOBAL_CJK_AUTO_FAKE_BOLD"].GetAsBool());
	}
}
