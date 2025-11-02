namespace Utils {
	/// <summary>
	/// 配置文件解析器接口，通过解析字符串形式的配置文件内容，提供访问配置值的方法
	/// </summary>
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