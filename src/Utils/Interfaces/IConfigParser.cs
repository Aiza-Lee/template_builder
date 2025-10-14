namespace Utils {
	internal interface IConfigParser {
		/// <summary>
		/// 解析配置文件内容
		/// </summary>
		/// <param name="content">字符串形式的配置文件内容</param>
		void ParseConfigFile(string content);
		
		ReadonlyConfigValue this[string key] { get; }

		IEnumerable<KeyValuePair<string, string>> GetAllConfigsAsString();
	}
}