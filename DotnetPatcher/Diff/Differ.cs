using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.OutputVisitor;
using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using ICSharpCode.Decompiler.CSharp.Transforms;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using DotnetPatcher.Utility;
using CodeChicken.DiffPatch;
using System.Collections.Concurrent;

using DiffPatcher = CodeChicken.DiffPatch.Patcher;
using DiffDiffer = CodeChicken.DiffPatch.Differ;
using System.Text.RegularExpressions;

namespace DotnetPatcher.Diff
{
	public class Differ
	{

		private static string[] DiffableFileExtensions = { ".cs", ".csproj", ".ico", ".resx", ".png", "App.config", ".json", ".targets", ".txt", ".bat", ".sh" };
		public static bool IsDiffable(string relPath) => DiffableFileExtensions.Any(relPath.EndsWith);

		private static readonly string RemovedFileList = "removed_files.list";
		private static readonly Regex HunkOffsetRegex = new Regex(@"@@ -(\d+),(\d+) \+([_\d]+),(\d+) @@", RegexOptions.Compiled);


		public string SourcePath;
		public string PatchPath;
		public string PatchedPath;

		public Differ(string sourcePath, string patchPath, string patchedPath)
		{
			this.SourcePath = sourcePath;
			this.PatchPath = patchPath;
			this.PatchedPath = patchedPath;
		}

		public void Diff()
		{
			List<WorkTask> items = new List<WorkTask>();

			foreach ((string file, string relPath) in DirectoryUtility.EnumerateSrcFiles(PatchedPath))
			{
				if (!File.Exists(Path.Combine(SourcePath, relPath)))
				{
					items.Add(new WorkTask(() => DirectoryUtility.Copy(file, Path.Combine(PatchPath, relPath))));
				}
				else if (IsDiffable(relPath))
				{
					items.Add(new WorkTask(() => DiffFile(relPath)));
				}
			}

			WorkTask.ExecuteParallel(items);

			foreach ((string file, string relPath) in DirectoryUtility.EnumerateFiles(PatchPath))
			{
				string targetPath = relPath.EndsWith(".patch") ? relPath.Substring(0, relPath.Length - 6) : relPath;
				if (!File.Exists(Path.Combine(PatchedPath, targetPath)))
				{
					DirectoryUtility.DeleteFile(file);
				}
			}

			DirectoryUtility.DeleteEmptyDirs(PatchPath);

			string[] removedFiles = 
                DirectoryUtility.EnumerateSrcFiles(SourcePath)
				.Where(f => !f.relPath.StartsWith(".git" + Path.DirectorySeparatorChar) && !File.Exists(Path.Combine(PatchedPath, f.relPath)))
				.Select(f => f.relPath)
				.ToArray();

			string removedFileList = Path.Combine(PatchPath, RemovedFileList);
			if (removedFiles.Length > 0)
			{
				File.WriteAllLines(removedFileList, removedFiles);
			}
			else
			{
				DirectoryUtility.DeleteFile(removedFileList);
			}

		}

		private void DiffFile(string relPath)
		{
			PatchFile patchFile = DiffDiffer.DiffFiles(new LineMatchedDiffer(),
				Path.Combine(SourcePath, relPath).Replace('\\', '/'),
				Path.Combine(PatchedPath, relPath).Replace('\\', '/'));

			string patchPath = Path.Combine(PatchPath, relPath + ".patch");
			if (!patchFile.IsEmpty)
			{
				DirectoryUtility.CreateParentDirectory(patchPath);
				File.WriteAllText(patchPath, patchFile.ToString(true));
			}
			else
			{
				DirectoryUtility.DeleteFile(patchPath);
			}
		}
	}
}
