using System;
using System.Collections.Generic;
using Core.Localization;
using Core.Models;
using Spectre.Console;

namespace Core.Tui {
    /// <summary>
    /// TUI 配置编辑服务：
    /// 承载字段搜索交互、分类字段列表、单项交互编辑（单选、开关、文本/数值/数组输入）、
    /// 取值校验与覆盖应用等用户界面操作，从 TuiRunner 中解耦。
    /// </summary>
    internal static class TuiConfigEditor {
        public static bool SearchAndEditFields(ProjectConfiguration config) {
            while (true) {
                AnsiConsole.MarkupLine($"[bold cyan]{I18n.T("Search.Title", "🔍 搜索配置项")}[/]");
                AnsiConsole.MarkupLine($"[grey]{I18n.T("Search.Desc", "支持输入中文名称、英文键名（如 font、margin、分栏、目录、作者）或功能说明关键字。")}[/]");

                var query = TuiLineEditor.ReadLine($"[bold]{I18n.T("Search.Prompt", "请输入搜索关键词 (直接回车或按 Esc 返回)：")}[/]\n")?.Trim();
                if (string.IsNullOrEmpty(query)) {
                    break;
                }

                var matches = TuiSchemaCatalog.SearchFields(query);
                if (matches.Count == 0) {
                    AnsiConsole.MarkupLine($"[yellow]{string.Format(I18n.T("Search.NotFound", "未找到匹配 \"{0}\" 的配置项。"), Markup.Escape(query))}[/]\n");
                    continue;
                }

                string? lastSelectedFieldKey = null;
                while (true) {
                    var menuItems = new List<TuiMenuItem>();

                    foreach (var field in matches) {
                        var item = config.GetItem(field.Key);
                        var isOverridden = item?.IsOverridden == true;
                        var cur = item?.EffectiveValue;
                        var displayCur = TuiViewRenderer.FormatFieldValueDisplay(cur, field.Key);
                        if (isOverridden) {
                            displayCur += " [" + I18n.T("Badge.Modified", "已改") + "]";
                        } else if (item?.HasProjectValue == true) {
                            displayCur += " [" + I18n.T("Badge.Project", "项目") + "]";
                        }
                        var catPrefix = !string.IsNullOrEmpty(field.Category) ? $"[{field.Category}] " : "";
                        menuItems.Add(new TuiMenuItem(field.Key, $"{catPrefix}{field.DisplayLabel}", displayCur, isOverridden: isOverridden, tag: field));
                    }

                    var searchTitle = string.Format(I18n.T("Search.Prefix", "搜索: \"{0}\""), Markup.Escape(query));
                    var chosen = TuiMenu.Prompt(
                        $"{searchTitle} ({matches.Count})：",
                        menuItems,
                        initialSelectedId: lastSelectedFieldKey,
                        onEnterAction: TuiMenuItem.ConfirmSentinel,
                        onDeleteAction: TuiMenuItem.DeleteSentinel
                    );

                    if (chosen.IsBuild) {
                        return true;
                    }

                    if (chosen.IsBack) {
                        break;
                    }

                    lastSelectedFieldKey = chosen.Id;

                    if (chosen.IsClear) {
                        if (chosen.Tag is TuiConfigField delField) {
                            ClearFieldAction(delField, config);
                        }
                        continue;
                    }

                    if (chosen.Tag is not TuiConfigField selectedField) continue;

                    EditSingleField(selectedField, config);
                }
            }
            return false;
        }

        public static bool EditCategoryFields(TuiCategoryGroup category, ProjectConfiguration config) {
            string? lastSelectedFieldKey = null;
            while (true) {
                var menuItems = new List<TuiMenuItem>();

                foreach (var field in category.Fields) {
                    var item = config.GetItem(field.Key);
                    var isOverridden = item?.IsOverridden == true;
                    var cur = item?.EffectiveValue;
                    var displayCur = TuiViewRenderer.FormatFieldValueDisplay(cur, field.Key);
                    if (isOverridden) {
                        displayCur += " [" + I18n.T("Badge.Modified", "已改") + "]";
                    } else if (item?.HasProjectValue == true) {
                        displayCur += " [" + I18n.T("Badge.Project", "项目") + "]";
                    }
                    menuItems.Add(new TuiMenuItem(field.Key, field.DisplayLabel, displayCur, isOverridden: isOverridden, tag: field));
                }

                var chosen = TuiMenu.Prompt(
                    $"[bold {TuiTheme.Secondary}]{Markup.Escape(category.DisplayTitle)}[/]  [grey]{Markup.Escape(category.DisplayDescription)}[/]",
                    menuItems,
                    initialSelectedId: lastSelectedFieldKey,
                    onEnterAction: TuiMenuItem.ConfirmSentinel,
                    onDeleteAction: TuiMenuItem.DeleteSentinel
                );

                if (chosen.IsBuild) {
                    return true;
                }

                if (chosen.IsBack) {
                    break;
                }

                lastSelectedFieldKey = chosen.Id;

                if (chosen.IsClear) {
                    if (chosen.Tag is TuiConfigField delField) {
                        ClearFieldAction(delField, config);
                    }
                    continue;
                }

                if (chosen.Tag is not TuiConfigField selectedField) continue;

                EditSingleField(selectedField, config);
            }
            return false;
        }

        public static void ClearFieldAction(TuiConfigField field, ProjectConfiguration config) {
            var item = config.GetItem(field.Key);
            if (item == null) return;
            if (field.Kind is TuiFieldKind.Text or TuiFieldKind.Array) {
                ApplyFieldValue(field, item, string.Empty, config);
            } else {
                if (item.IsOverridden) {
                    config.ClearOverride(field.Key);
                    AnsiConsole.MarkupLine($"[green]{string.Format(I18n.T("FieldEdit.Cleared", "✔ 已清除覆盖并恢复默认 {0}"), Markup.Escape(field.DisplayLabel))}[/]");
                } else {
                    AnsiConsole.MarkupLine($"[grey]{string.Format(I18n.T("FieldEdit.NotModified", "配置项 {0} 未修改，无需清空。"), Markup.Escape(field.DisplayLabel))}[/]");
                }
            }
        }

        public static void EditSingleField(TuiConfigField field, ProjectConfiguration config) {
            var item = config.GetItem(field.Key);
            if (item == null) return;

            TuiViewRenderer.RenderFieldCard(AnsiConsole.Console, field, item);

            switch (field.Kind) {
                case TuiFieldKind.Choice:
                    PromptChoiceField(field, item, config);
                    break;
                case TuiFieldKind.Toggle:
                    PromptToggleField(field, item, config);
                    break;
                default:
                    PromptTextField(field, item, config);
                    break;
            }
        }

        public static void PromptChoiceField(TuiConfigField field, ConfigItem item, ProjectConfiguration config) {
            var menuItems = new List<TuiMenuItem>();

            foreach (var choice in field.Choices ?? []) {
                var isCurrent = string.Equals(choice, item.EffectiveValue, StringComparison.Ordinal);
                var label = choice;
                if (string.Equals(choice, item.BuiltinValue, StringComparison.Ordinal)) {
                    label += " [" + I18n.T("Badge.Default", "默认") + "]";
                }
                if (item.HasProjectValue && string.Equals(choice, item.ProjectValue, StringComparison.Ordinal)) {
                    label += " [" + I18n.T("Badge.Project", "项目") + "]";
                }
                menuItems.Add(new TuiMenuItem(choice, label, isOverridden: isCurrent));
            }

            if (item.HasProjectValue && item.IsOverridden) {
                menuItems.Add(new TuiMenuItem("__RESET_PROJECT__", string.Format(I18n.T("FieldEdit.RestoreProject", "恢复项目值: {0}"), item.ProjectValue)));
            }

            if (item.BuiltinValue != null && (item.IsOverridden || !string.Equals(item.EffectiveValue, item.BuiltinValue, StringComparison.Ordinal))) {
                menuItems.Add(new TuiMenuItem("__RESET_BUILTIN__", string.Format(I18n.T("FieldEdit.RestoreBuiltin", "恢复默认值: {0}"), item.BuiltinValue)));
            }

            var chosen = TuiMenu.Prompt(
                $"[bold]{Markup.Escape(field.DisplayLabel)}[/]：",
                menuItems,
                initialSelectedId: item.EffectiveValue,
                onDeleteAction: TuiMenuItem.DeleteSentinel
            );
            if (chosen.IsBack) {
                return;
            }

            if (chosen.IsClear) {
                if (item.IsOverridden) {
                    config.ClearOverride(field.Key);
                    AnsiConsole.MarkupLine($"[green]{string.Format(I18n.T("FieldEdit.Cleared", "✔ 已清除覆盖并恢复默认 {0}"), Markup.Escape(field.DisplayLabel))}[/]");
                }
                return;
            }

            if (chosen.Id == "__RESET_PROJECT__") {
                config.ResetToProject(field.Key);
                AnsiConsole.MarkupLine($"[green]{string.Format(I18n.T("FieldEdit.RestoredProject", "✔ 已恢复项目配置值 {0} = {1}"), Markup.Escape(field.DisplayLabel), Markup.Escape(item.ProjectValue ?? ""))}[/]");
                return;
            }

            if (chosen.Id == "__RESET_BUILTIN__") {
                config.ResetToBuiltin(field.Key);
                AnsiConsole.MarkupLine($"[green]{string.Format(I18n.T("FieldEdit.RestoredBuiltin", "✔ 已恢复内置默认值 {0} = {1}"), Markup.Escape(field.DisplayLabel), Markup.Escape(item.BuiltinValue ?? ""))}[/]");
                return;
            }

            ApplyFieldValue(field, item, chosen.Id, config);
        }

        public static void PromptToggleField(TuiConfigField field, ConfigItem item, ProjectConfiguration config) {
            var menuItems = new List<TuiMenuItem>();

            var isTrueCurrent = string.Equals(item.EffectiveValue, "true", StringComparison.OrdinalIgnoreCase);
            string trueLabel;
            string falseLabel;

            if (field.Key == "LAYOUT_TOC_IN_COLUMNS") {
                falseLabel = I18n.T("Choice.FollowBody", "跟随正文");
                trueLabel = I18n.T("Choice.SingleColumn", "独立单栏");
            } else {
                trueLabel = I18n.T("Choice.Enabled", "开启");
                falseLabel = I18n.T("Choice.Disabled", "关闭");
            }

            if (string.Equals(item.BuiltinValue, "true", StringComparison.OrdinalIgnoreCase)) trueLabel += " [" + I18n.T("Badge.Default", "默认") + "]";
            if (string.Equals(item.BuiltinValue, "false", StringComparison.OrdinalIgnoreCase)) falseLabel += " [" + I18n.T("Badge.Default", "默认") + "]";
            if (item.HasProjectValue) {
                if (string.Equals(item.ProjectValue, "true", StringComparison.OrdinalIgnoreCase)) trueLabel += " [" + I18n.T("Badge.Project", "项目") + "]";
                if (string.Equals(item.ProjectValue, "false", StringComparison.OrdinalIgnoreCase)) falseLabel += " [" + I18n.T("Badge.Project", "项目") + "]";
            }

            if (field.Key == "LAYOUT_TOC_IN_COLUMNS") {
                menuItems.Add(new TuiMenuItem("false", falseLabel, isOverridden: !isTrueCurrent));
                menuItems.Add(new TuiMenuItem("true", trueLabel, isOverridden: isTrueCurrent));
            } else {
                menuItems.Add(new TuiMenuItem("true", trueLabel, isOverridden: isTrueCurrent));
                menuItems.Add(new TuiMenuItem("false", falseLabel, isOverridden: !isTrueCurrent));
            }

            if (item.HasProjectValue && item.IsOverridden) {
                var projDisplay = TuiViewRenderer.FormatFieldValueDisplay(item.ProjectValue, field.Key);
                menuItems.Add(new TuiMenuItem("__RESET_PROJECT__", string.Format(I18n.T("FieldEdit.RestoreProject", "恢复项目值: {0}"), projDisplay)));
            }

            if (item.BuiltinValue != null && (item.IsOverridden || !string.Equals(item.EffectiveValue, item.BuiltinValue, StringComparison.OrdinalIgnoreCase))) {
                var builtinDisplay = TuiViewRenderer.FormatFieldValueDisplay(item.BuiltinValue, field.Key);
                menuItems.Add(new TuiMenuItem("__RESET_BUILTIN__", string.Format(I18n.T("FieldEdit.RestoreBuiltin", "恢复默认值: {0}"), builtinDisplay)));
            }

            var chosen = TuiMenu.Prompt(
                $"[bold]{Markup.Escape(field.DisplayLabel)}[/]：",
                menuItems,
                initialSelectedId: item.EffectiveValue?.ToLowerInvariant(),
                onDeleteAction: TuiMenuItem.DeleteSentinel
            );
            if (chosen.IsBack) {
                return;
            }

            if (chosen.IsClear) {
                if (item.IsOverridden) {
                    config.ClearOverride(field.Key);
                    AnsiConsole.MarkupLine($"[green]{string.Format(I18n.T("FieldEdit.Cleared", "✔ 已清除覆盖并恢复默认 {0}"), Markup.Escape(field.DisplayLabel))}[/]");
                }
                return;
            }

            if (chosen.Id == "__RESET_PROJECT__") {
                config.ResetToProject(field.Key);
                AnsiConsole.MarkupLine($"[green]{string.Format(I18n.T("FieldEdit.RestoredProject", "✔ 已恢复项目配置值 {0} = {1}"), Markup.Escape(field.DisplayLabel), Markup.Escape(item.ProjectValue ?? ""))}[/]");
                return;
            }

            if (chosen.Id == "__RESET_BUILTIN__") {
                config.ResetToBuiltin(field.Key);
                AnsiConsole.MarkupLine($"[green]{string.Format(I18n.T("FieldEdit.RestoredBuiltin", "✔ 已恢复内置默认值 {0} = {1}"), Markup.Escape(field.DisplayLabel), Markup.Escape(item.BuiltinValue ?? ""))}[/]");
                return;
            }

            ApplyFieldValue(field, item, chosen.Id, config);
        }

        public static void PromptTextField(TuiConfigField field, ConfigItem item, ProjectConfiguration config) {
            var isNumber = field.Kind == TuiFieldKind.Number;
            var emptyText = I18n.T("Field.Empty", "<空>");
            var dispEffective = string.IsNullOrEmpty(item.EffectiveValue) ? emptyText : item.EffectiveValue;
            var dispProject = item.ProjectValue != null ? (string.IsNullOrEmpty(item.ProjectValue) ? emptyText : item.ProjectValue) : "-";
            var dispBuiltin = string.IsNullOrEmpty(item.BuiltinValue) ? emptyText : item.BuiltinValue;

            var actionItems = new List<TuiMenuItem>();
            actionItems.Add(new TuiMenuItem("EDIT", string.Format(I18n.T("FieldEdit.EditValue", "编辑取值: {0}"), dispEffective)));

            if (item.HasProjectValue && item.IsOverridden) {
                actionItems.Add(new TuiMenuItem("RESET_PROJECT", string.Format(I18n.T("FieldEdit.RestoreProject", "恢复项目值: {0}"), dispProject)));
            }

            if (item.BuiltinValue != null && (item.IsOverridden || !string.Equals(item.EffectiveValue, item.BuiltinValue, StringComparison.Ordinal))) {
                actionItems.Add(new TuiMenuItem("RESET_BUILTIN", string.Format(I18n.T("FieldEdit.RestoreBuiltin", "恢复默认值: {0}"), dispBuiltin)));
            }

            var chosen = TuiMenu.Prompt(
                $"[bold]{Markup.Escape(field.DisplayLabel)}[/]：",
                actionItems,
                onDeleteAction: TuiMenuItem.DeleteSentinel
            );
            if (chosen.IsBack) {
                return;
            }

            if (chosen.IsClear) {
                if (isNumber) {
                    if (item.IsOverridden) {
                        config.ClearOverride(field.Key);
                        AnsiConsole.MarkupLine($"[green]{string.Format(I18n.T("FieldEdit.Cleared", "✔ 已清除覆盖并恢复默认 {0}"), Markup.Escape(field.DisplayLabel))}[/]");
                    }
                } else {
                    ApplyFieldValue(field, item, string.Empty, config);
                }
                return;
            }

            if (chosen.Id == "RESET_PROJECT") {
                config.ResetToProject(field.Key);
                AnsiConsole.MarkupLine($"[green]{string.Format(I18n.T("FieldEdit.RestoredProject", "✔ 已恢复项目配置值 {0} = {1}"), Markup.Escape(field.DisplayLabel), Markup.Escape(dispProject))}[/]");
                return;
            }

            if (chosen.Id == "RESET_BUILTIN") {
                config.ResetToBuiltin(field.Key);
                AnsiConsole.MarkupLine($"[green]{string.Format(I18n.T("FieldEdit.RestoredBuiltin", "✔ 已恢复内置默认值 {0} = {1}"), Markup.Escape(field.DisplayLabel), Markup.Escape(dispBuiltin))}[/]");
                return;
            }

            if (chosen.Id == "EDIT") {
                string? input;
                if (isNumber) {
                    var rangeHint = field.Minimum.HasValue && field.Maximum.HasValue
                        ? $" ({field.Minimum.Value} ~ {field.Maximum.Value})"
                        : (field.Minimum.HasValue ? $" (>= {field.Minimum.Value})" : "");
                    var promptInt = string.Format(I18n.T("FieldEdit.PromptInt", "请输入 {0} (整数{1}，直接回车确认，Esc 取消)："), Markup.Escape(field.DisplayLabel), rangeHint);
                    input = TuiLineEditor.ReadLine(
                        $"[bold]{promptInt}[/]\n",
                        item.EffectiveValue,
                        raw => {
                            var (isValid, err) = Core.Validation.ConfigValidator.ValidateSingleValue(field, raw);
                            return isValid ? (true, null) : (false, err?.Message);
                        }
                    );
                } else if (field.Kind == TuiFieldKind.Array) {
                    var promptList = string.Format(I18n.T("FieldEdit.PromptList", "请输入 {0} (逗号分隔，直接回车确认，Esc 取消)："), Markup.Escape(field.DisplayLabel));
                    input = TuiLineEditor.ReadLine(
                        $"[bold]{promptList}[/]\n",
                        item.EffectiveValue
                    );
                } else {
                    var isMultiline = item.EffectiveValue?.Contains('\n') == true;
                    var hint = isMultiline
                        ? $"[grey]{I18n.T("FieldEdit.PromptMulti", "(多行编辑，Ctrl+Enter 确认，Enter 换行，Esc 取消)")}[/]"
                        : $"[grey]{I18n.T("FieldEdit.PromptSingle", "(直接回车确认，Shift+Enter 换行，Esc 取消)")}[/]";
                    input = TuiLineEditor.ReadLine(
                        $"[bold]{Markup.Escape(field.DisplayLabel)}[/] {hint}：\n",
                        item.EffectiveValue,
                        raw => {
                            var (isValid, err) = Core.Validation.ConfigValidator.ValidateSingleValue(field, raw);
                            return isValid ? (true, null) : (false, err?.Message);
                        },
                        multiline: isMultiline
                    );
                }

                if (input == null) {
                    AnsiConsole.MarkupLine("[grey]" + I18n.T("FieldEdit.Cancelled", "已取消编辑，未作任何修改。") + "[/]");
                    return;
                }

                var finalVal = isNumber ? input.Trim() : input;
                ApplyFieldValue(field, item, finalVal, config);
            }
        }

        public static void ApplyFieldValue(TuiConfigField field, ConfigItem item, string value, ProjectConfiguration config) {
            var (isValid, err) = Core.Validation.ConfigValidator.ValidateSingleValue(field, value);
            if (!isValid && err != null) {
                var failFmt = I18n.T("FieldEdit.FailedValidation", "✘ 校验失败：{0}（期望：{1}；建议：{2}）");
                AnsiConsole.MarkupLine($"[red]{string.Format(failFmt, Markup.Escape(err.Message), Markup.Escape(err.Expected), Markup.Escape(err.Recommendation))}[/]");
                return;
            }

            var emptyText = I18n.T("Field.Empty", "<空>");
            if (item.HasProjectValue && string.Equals(value, item.ProjectValue, StringComparison.Ordinal)) {
                config.ResetToProject(field.Key);
                var disp = string.IsNullOrEmpty(value) ? emptyText : value;
                var matchProj = string.Format(I18n.T("FieldEdit.MatchesProject", "✔ 与项目配置一致，恢复为项目值 {0} = {1}"), Markup.Escape(field.DisplayLabel), Markup.Escape(disp));
                AnsiConsole.MarkupLine($"[grey]{matchProj}[/]");
            } else if (!item.HasProjectValue && string.Equals(value, item.BuiltinValue, StringComparison.Ordinal)) {
                config.ClearOverride(field.Key);
                var disp = string.IsNullOrEmpty(value) ? emptyText : value;
                var matchDef = string.Format(I18n.T("FieldEdit.MatchesBuiltin", "✔ 与内置默认一致，无需覆盖 {0} = {1}"), Markup.Escape(field.DisplayLabel), Markup.Escape(disp));
                AnsiConsole.MarkupLine($"[grey]{matchDef}[/]");
            } else {
                var success = config.TrySetOverride(field.Key, value);
                var disp = string.IsNullOrEmpty(value) ? emptyText : value;
                if (success) {
                    var setMsg = string.Format(I18n.T("FieldEdit.OverrideSet", "✔ 已设置运行时覆盖 {0} = {1}"), Markup.Escape(field.DisplayLabel), Markup.Escape(disp));
                    AnsiConsole.MarkupLine($"[bold {TuiTheme.Warning}]{setMsg}[/]");
                } else {
                    var failMsg = string.Format(I18n.T("FieldEdit.CannotSet", "✘ 无法设置配置项 {0}，值非法或无法解析"), Markup.Escape(field.DisplayLabel));
                    AnsiConsole.MarkupLine($"[red]{failMsg}[/]");
                }
            }
        }
    }
}
