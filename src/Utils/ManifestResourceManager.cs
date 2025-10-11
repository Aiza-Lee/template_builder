namespace Utils {
	internal sealed class ManifestResourceManager(ILogger logger) {
		private readonly ILogger _logger = logger;

		public string GetResourceInString(string resourceName) {
			var assembly = System.Reflection.Assembly.GetExecutingAssembly();
			using var stream = assembly.GetManifestResourceStream(resourceName);
			if (stream != null) {
				using var reader = new StreamReader(stream);
				return reader.ReadToEnd();
			} else {
				_logger?.Error($"Resource '{resourceName}' not found in embedded resources.");
				return string.Empty;
			}
		}

		public string? GetContentInString(FileInfo fileInfo) {
			if (!fileInfo.Exists) {
				_logger?.Error($"File '{fileInfo.FullName}' does not exist.");
				return null;
			}
			return File.ReadAllText(fileInfo.FullName);
		}
	}
}