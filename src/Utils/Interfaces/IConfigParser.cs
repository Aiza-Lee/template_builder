namespace Utils {
	internal interface IConfigParser {
		/// <summary>
		/// 解析配置文件内容
		/// </summary>
		/// <param name="content">字符串形式的配置文件内容</param>
		void ParseConfigFile(string content);
		/// <summary>
		/// 查询配置项的值
		/// </summary>
		/// <param name="key">键值</param>
		/// <returns>返回配置项的值</returns>
		string[] QueryConfig(string key);
		IEnumerable<KeyValuePair<string, string[]>> GetAllConfigs();
	}
}