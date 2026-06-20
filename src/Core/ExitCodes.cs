namespace Core {
	/// <summary>
	/// CLI 退出码的统一常量来源。
	/// <para>
	/// 0 成功；1 xelatex 编译失败；2 命令行参数错误（含未注册 key）；
	/// 3 模板存在未替换占位符；4 用户配置 JSON 损坏；5 嵌入式资源缺失。
	/// </para>
	/// </summary>
	internal static class ExitCodes {
		public const int Success = 0;
		public const int XelatexFailure = 1;
		public const int InvalidArguments = 2;
		public const int UnresolvedPlaceholders = 3;
		public const int MalformedConfig = 4;
		public const int MissingEmbeddedResource = 5;
	}
}
