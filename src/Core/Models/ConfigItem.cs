namespace Core.Models {
    /// <summary>
    /// 单个配置项模型，支持精确的值来源追溯（内置默认 -> 项目配置 -> 运行时覆盖）。
    /// </summary>
    internal sealed class ConfigItem {
        public string Key { get; }
        public string Section { get; }
        public ConfigSource Source { get; private set; }
        public string? BuiltinValue { get; }
        public string? ProjectValue { get; private set; }
        public string? OverrideValue { get; private set; }

        public string? EffectiveValue => Source switch {
            ConfigSource.Override => OverrideValue,
            ConfigSource.Project => ProjectValue,
            _ => BuiltinValue
        };

        public bool IsOverridden => Source == ConfigSource.Override;
        public bool HasProjectValue => ProjectValue != null;

        public ConfigItem(string key, string section, string? builtinValue, string? projectValue = null, string? overrideValue = null) {
            Key = key;
            Section = section;
            BuiltinValue = builtinValue;
            ProjectValue = projectValue;
            OverrideValue = overrideValue;
            UpdateSource();
        }

        public void SetProjectValue(string? value) {
            ProjectValue = value;
            UpdateSource();
        }

        public void SetOverride(string? value) {
            OverrideValue = value;
            UpdateSource();
        }

        public void ClearOverride() {
            OverrideValue = null;
            UpdateSource();
        }

        public void ResetToBuiltin() {
            if (ProjectValue != null) {
                OverrideValue = BuiltinValue;
            } else {
                OverrideValue = null;
            }
            UpdateSource();
        }

        private void UpdateSource() {
            if (OverrideValue != null) {
                Source = ConfigSource.Override;
            } else if (ProjectValue != null) {
                Source = ConfigSource.Project;
            } else {
                Source = ConfigSource.Builtin;
            }
        }
    }
}
