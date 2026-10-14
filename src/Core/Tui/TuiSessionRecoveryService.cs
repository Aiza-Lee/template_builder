using System;
using System.Collections.Generic;
using System.IO;
using Core.Localization;
using Core.Models;
using Spectre.Console;
using Utils;

namespace Core.Tui {
    /// <summary>
    /// TUI 会话配置加载与恢复服务：
    /// 负责配置文件丢失、文件损坏、格式解析失败等场景下的自愈交互（重新加载、切换路径、重置为内置默认、退出等）。
    /// </summary>
    internal static class TuiSessionRecoveryService {
        public static void LoadOrRecoverConfiguration(
            ProjectSession session,
            TuiLaunchOptions launch,
            ILogger logger,
            Func<TuiLaunchOptions, FileInfo?, bool?, (FileInfo ConfigFile, bool UserProvided)>? promptConfigFile = null
        ) {
            while (true) {
                if (session.ConfigFile == null) {
                    session.Configuration = new ProjectConfiguration(logger, ConfigStrictness.Lax);
                    session.UserProvidedConfig = false;
                    session.IsConfigurationLoaded = true;
                    session.LoadedConfigPath = null;
                    return;
                }

                if (!File.Exists(session.ConfigFile.FullName)) {
                    AnsiConsole.WriteLine();
                    var missingBody = string.Format(
                        I18n.T("Prompt.ConfigMissingBody", "✘ 配置文件不存在：\n  文件路径：{0}\n\n文件在磁盘上缺失。请在外部创建/恢复该文件，或选择其他恢复方案。"),
                        Markup.Escape(session.ConfigFile.FullName)
                    );
                    var panel = new Panel(new Markup(missingBody)) {
                        Header = new PanelHeader($"[bold red]{I18n.T("Prompt.ConfigMissing", " 配置文件缺失 ")}[/]"),
                        Border = BoxBorder.Rounded
                    };
                    AnsiConsole.Write(panel);
                    AnsiConsole.WriteLine();

                    var menuItems = new List<TuiMenuItem> {
                        new("RELOAD", I18n.T("Recovery.ReloadMissing", "外部创建/恢复后重新加载")),
                        new("RESELECT", I18n.T("Recovery.ReselectMissing", "重新输入/选择配置文件路径")),
                        new("DEFAULT", I18n.T("Recovery.UseDefault", "使用内置默认配置继续 (不依赖外部配置)")),
                        new("EXIT", I18n.T("Recovery.Exit", "退出程序"), action: TuiMenuAction.Back)
                    };

                    var chosen = TuiMenu.Prompt(
                        $"[yellow]{I18n.T("Prompt.RecoveryAction", "请选择恢复操作：")}[/]",
                        menuItems
                    );

                    if (chosen.Id == "RELOAD") {
                        continue;
                    }

                    if (chosen.Id == "RESELECT" && promptConfigFile != null) {
                        var (newFile, isUser) = promptConfigFile(launch, session.ConfigFile, session.UserProvidedConfig);
                        session.ConfigFile = newFile;
                        session.UserProvidedConfig = isUser;
                        continue;
                    }

                    if (chosen.Id == "DEFAULT") {
                        session.Configuration = new ProjectConfiguration(logger, ConfigStrictness.Lax);
                        session.ConfigFile = null;
                        session.UserProvidedConfig = false;
                        session.IsConfigurationLoaded = true;
                        session.LoadedConfigPath = null;
                        AnsiConsole.MarkupLine($"[yellow]{I18n.T("Recovery.SwitchedToDefault", "✔ 已切换为内置默认配置。")}[/]");
                        return;
                    }

                    Environment.Exit(ExitCodes.InvalidArguments);
                    return;
                }

                try {
                    var content = File.ReadAllText(session.ConfigFile.FullName);
                    var strictness = session.UserProvidedConfig ? ConfigStrictness.Strict : ConfigStrictness.Lax;
                    var config = new ProjectConfiguration(logger, strictness);
                    config.LoadProjectConfig(content, session.ConfigFile.FullName);

                    // 如果此前会话已有覆盖项，保留覆盖项
                    if (session.Configuration != null) {
                        foreach (var (k, v) in session.Configuration.GetActiveOverrides()) {
                            config.TrySetOverride(k, v);
                        }
                    }

                    session.Configuration = config;
                    session.IsConfigurationLoaded = true;
                    session.LoadedConfigPath = session.ConfigFile.FullName;
                    return;
                } catch (Exception ex) {
                    AnsiConsole.WriteLine();
                    var corruptBody = string.Format(
                        I18n.T("Prompt.ConfigCorruptBody", "✘ 配置文件损坏或无法解析：\n  文件路径：{0}\n  错误信息：{1}\n\n请在外部编辑器中修复该文件，或重新选择有效配置文件。"),
                        Markup.Escape(session.ConfigFile.FullName),
                        Markup.Escape(ex.Message)
                    );
                    var panel = new Panel(new Markup(corruptBody)) {
                        Header = new PanelHeader($"[bold red]{I18n.T("Prompt.ConfigCorrupt", " 配置文件损坏 ")}[/]"),
                        Border = BoxBorder.Rounded
                    };
                    AnsiConsole.Write(panel);
                    AnsiConsole.WriteLine();

                    var menuItems = new List<TuiMenuItem> {
                        new("RELOAD", I18n.T("Recovery.ReloadCorrupt", "外部修复后重新加载")),
                        new("RESELECT", I18n.T("Recovery.ReselectCorrupt", "重新输入/选择其他配置文件路径")),
                        new("IGNORE", I18n.T("Recovery.IgnoreCorrupt", "使用内置默认配置继续 (忽略损坏配置)")),
                        new("EXIT", I18n.T("Recovery.Exit", "退出程序"), action: TuiMenuAction.Back)
                    };

                    var chosen = TuiMenu.Prompt(
                        $"[yellow]{I18n.T("Prompt.RecoveryAction", "请选择恢复操作：")}[/]",
                        menuItems
                    );

                    if (chosen.Id == "RELOAD") {
                        continue;
                    }

                    if (chosen.Id == "RESELECT" && promptConfigFile != null) {
                        var (newFile, isUser) = promptConfigFile(launch, session.ConfigFile, session.UserProvidedConfig);
                        session.ConfigFile = newFile;
                        session.UserProvidedConfig = isUser;
                        continue;
                    }

                    if (chosen.Id == "IGNORE") {
                        session.Configuration = new ProjectConfiguration(logger, ConfigStrictness.Lax);
                        session.ConfigFile = null;
                        session.UserProvidedConfig = false;
                        session.IsConfigurationLoaded = true;
                        session.LoadedConfigPath = null;
                        AnsiConsole.MarkupLine($"[yellow]{I18n.T("Recovery.SwitchedToDefault", "✔ 已切换为内置默认配置。")}[/]");
                        return;
                    }

                    Environment.Exit(ExitCodes.InvalidArguments);
                    return;
                }
            }
        }
    }
}
