namespace Utils {
	internal interface IConfigParser {
		void ParseConfigFile(FileInfo fileInfo);
		string QueryConfig(string key);
		IEnumerable<KeyValuePair<string, string>> GetAllConfigs();
	}
}