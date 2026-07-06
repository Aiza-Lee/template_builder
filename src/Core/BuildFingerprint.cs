using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.FileSystemGlobbing;
using Utils;

namespace Core {
	/// <summary>
	/// Round 4 新增：构建指纹计算工具，用于增量构建（<c>PROGRAM.build.incremental=true</c>）。
	/// <para>
	/// 哈希输入：源树内容（按相对路径排序的 <c>relpath\tlength\tmtime_ticks\tsha1</c>）+
	/// TEX/PROGRAM 两个 parser 的全量 key→value（按 key 排序）+ Main.tex / CodeBlock.tex
	/// 解析后的内容 + minted outputdir 字面量。
	/// </para>
	/// <para>
	/// sidecar 文件名 <c>&lt;pdf-basename&gt;.tbuild</c>，与 PDF 同目录，记录 SHA-1 16-hex prefix。
	/// 同输入 → 同哈希；任一输入变化 → 哈希变化。
	/// </para>
	/// </summary>
	internal static class BuildFingerprint {
		internal const string SidecarSuffix = ".tbuild";

		/// <summary>
		/// 计算当前构建的指纹哈希。任一输入变化都会改变结果（SHA-1 仅用于等值检查，
		/// 碰撞概率在本场景下可忽略）。
		/// </summary>
		internal static string Compute(
			string sourceDir,
			IEnumerable<string> ignoreGlobs,
			IConfigParser texParser,
			IConfigParser programParser,
			string mainTemplateContent,
			string codeBlockTemplateContent,
			string mintedOutputDir
		) {
			var canonical = new StringBuilder();

			// 1. 源树（按相对路径排序，保证确定性）
			if (Directory.Exists(sourceDir)) {
				var matcher = BuildMatcher(ignoreGlobs);
				var rootFull = Path.GetFullPath(sourceDir);
				var entries = new List<(string RelPath, FileInfo Info)>();
				WalkSourceTree(new DirectoryInfo(rootFull), rootFull, matcher, entries);
				entries.Sort((a, b) => string.CompareOrdinal(a.RelPath, b.RelPath));
				foreach (var (rel, info) in entries) {
					string fileHash;
					using (var stream = File.OpenRead(info.FullName)) {
						var hash = SHA1.HashData(ReadAllBytes(stream));
						fileHash = Convert.ToHexString(hash).ToLowerInvariant();
					}
					canonical
						.Append(rel).Append('\t')
						.Append(info.Length).Append('\t')
						.Append(info.LastWriteTimeUtc.Ticks).Append('\t')
						.Append(fileHash).Append('\n');
				}
			}

			// 2. 配置：两个 parser 的全量 key→value，按 key 排序
			AppendConfigSection(canonical, "TEX", texParser);
			AppendConfigSection(canonical, "PROGRAM", programParser);

			// 3. 模板内容（Main.tex + CodeBlock.tex 解析后）
			canonical.Append("MAIN_TEMPLATE\n").Append(mainTemplateContent).Append('\n');
			canonical.Append("CODEBLOCK_TEMPLATE\n").Append(codeBlockTemplateContent).Append('\n');

			// 4. minted outputdir 字面量
			canonical.Append("MINTED_OUTPUTDIR\n").Append(mintedOutputDir).Append('\n');

			// 5. 最终 SHA-1 → 16-hex prefix
			var bytes = Encoding.UTF8.GetBytes(canonical.ToString());
			var finalHash = SHA1.HashData(bytes);
			return Convert.ToHexString(finalHash).ToLowerInvariant();
		}

		private static void AppendConfigSection(StringBuilder canonical, string section, IConfigParser parser) {
			var pairs = parser.GetAllConfigsAsString().ToList();
			pairs.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
			foreach (var (key, value) in pairs) {
				// 假设 value 不含换行（项目内 config 均为标量/短串）。若违反会污染哈希。
				canonical.Append(section).Append(':').Append(key).Append('=').Append(value).Append('\n');
			}
		}

		private static void WalkSourceTree(
			DirectoryInfo dir,
			string rootFull,
			Matcher matcher,
			List<(string RelPath, FileInfo Info)> entries
		) {
			foreach (var sub in dir.GetDirectories()) {
				if (sub.Name.StartsWith('.')) continue;
				if (!matcher.Match(sub.Name).HasMatches) continue;
				WalkSourceTree(sub, rootFull, matcher, entries);
			}
			foreach (var file in dir.GetFiles()) {
				if (file.Name.StartsWith('.')) continue;
				if (!matcher.Match(file.Name).HasMatches) continue;
				var rel = Path.GetRelativePath(rootFull, file.FullName).Replace('\\', '/');
				entries.Add((rel, file));
			}
		}

		private static Matcher BuildMatcher(IEnumerable<string> ignoreGlobs) {
			var matcher = new Matcher();
			matcher.AddInclude("*");
			foreach (var p in ignoreGlobs) {
				if (!string.IsNullOrWhiteSpace(p)) {
					matcher.AddExclude(p);
				}
			}
			return matcher;
		}

		/// <summary>
		/// 读取 sidecar 文件的哈希值。文件不存在、不可读、内容为空 → 返回 false。
		/// </summary>
		internal static bool TryLoadSidecar(string sidecarPath, out string storedHash) {
			storedHash = string.Empty;
			if (!File.Exists(sidecarPath)) return false;
			try {
				var line = File.ReadAllText(sidecarPath).Trim();
				if (line.Length == 0) return false;
				storedHash = line;
				return true;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// 把哈希写入 sidecar 文件。原子写：先写 <c>&lt;path&gt;.tmp</c> 再
		/// <see cref="File.Move(string, string, bool)"/> 覆盖。
		/// </summary>
		internal static void WriteSidecar(string sidecarPath, string hash) {
			var tmpPath = sidecarPath + ".tmp";
			File.WriteAllText(tmpPath, hash);
			File.Move(tmpPath, sidecarPath, overwrite: true);
		}

		private static byte[] ReadAllBytes(Stream stream) {
			using var ms = new MemoryStream();
			stream.CopyTo(ms);
			return ms.ToArray();
		}
	}
}
