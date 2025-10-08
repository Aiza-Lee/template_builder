using System.CommandLine;

namespace Program {
	public class Program {
		public static int Main(string[] args) {

			Option<FileInfo> fileOption = new("--file") {
				Description = "The file to process",
			};

			RootCommand rootCommand = new("A sample application");
			rootCommand.Options.Add(fileOption);

			rootCommand.SetAction(parseResult => {
				var fileInfo = parseResult.GetValue(fileOption);
				if (fileInfo != null) {
					ProcessFile(fileInfo);
				} else {
					Console.WriteLine("File not found or not specified.");
				}
				return 0;
			});

			return rootCommand.Parse(args).Invoke();
		}

		private static void ProcessFile(FileInfo fileInfo) {
			foreach (var line in File.ReadLines(fileInfo.FullName)) {
				Console.WriteLine(line);
			}
		}
	}
}