using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
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

namespace DotnetPatcher.Patch
{
	public class Patcher
	{
		private readonly ConcurrentBag<FilePatcher> results = new ConcurrentBag<FilePatcher>();
		private static readonly string RemovedFileList = "removed_files.list";

		public DiffPatcher.Mode mode;

		public int PatchFailureCount = 0;
		public int PatchWarningCount = 0;
		public int PatchExactCount = 0;
		public int PatchOffsetCount = 0;
		public int PatchFuzzyCount = 0;

		public string SourcePath;
		public string PatchPath;
		public string PatchedPath;

		public Patcher(string sourcePath, string patchPath, string patchedPath)
		{
			this.SourcePath = sourcePath;
			this.PatchPath = patchPath;
			this.PatchedPath = patchedPath;
		}

		public void Patch()
		{

			mode = DiffPatcher.Mode.FUZZY;

			string removedFileList = Path.Combine(PatchPath, RemovedFileList);
			HashSet<string> noCopy = File.Exists(removedFileList) ? new HashSet<string>(File.ReadAllLines(removedFileList)) : new HashSet<string>();

			HashSet<string> newFiles = new HashSet<string>();

			List<WorkTask> patchTasks = new List<WorkTask>();
			List<WorkTask> patchCopyTasks = new List<WorkTask>();
			List<WorkTask> copyTasks = new List<WorkTask>();

			foreach ((string file, string relPath) in DirectoryUtility.EnumerateFiles(PatchPath))
			{
				if (relPath.EndsWith(".patch"))
				{
					patchTasks.Add(new WorkTask(() =>
					{
						string patchedPathReal = Path.GetFullPath(DirectoryUtility.PreparePath(PatchFile(file).PatchedPath));

						newFiles.Add(patchedPathReal);
					}));
					noCopy.Add(relPath.Substring(0, relPath.Length - 6));
				}
				else if (relPath != RemovedFileList)
				{
					string destination = Path.Combine(PatchedPath, relPath);

					patchCopyTasks.Add(new WorkTask(() =>
					{
						DirectoryUtility.Copy(file, destination);
					}));
					newFiles.Add(destination);
				}
			}
			foreach ((string file, string relPath) in DirectoryUtility.EnumerateSrcFiles(SourcePath))
			{
				if (!noCopy.Contains(relPath))
				{
					string destination = Path.Combine(PatchedPath, relPath);

					copyTasks.Add(new WorkTask(() =>
					{
						DirectoryUtility.Copy(file, destination);
					}));
					newFiles.Add(destination);
				}
			}

			WorkTask.ExecuteParallel(patchTasks);
			WorkTask.ExecuteParallel(patchCopyTasks);
			WorkTask.ExecuteParallel(copyTasks);


			foreach ((string file, string relPath) in DirectoryUtility.EnumerateSrcFiles(PatchedPath))
				if (!newFiles.Contains(file))
					File.Delete(file);

			DirectoryUtility.DeleteEmptyDirs(PatchedPath);

			if (PatchFuzzyCount > 0 || mode == DiffPatcher.Mode.FUZZY && PatchFailureCount > 0)
			{
				Console.WriteLine("Some errors occured, this is so sad.");
			}
		}

		private FilePatcher PatchFile(string patchPath)
		{
			FilePatcher patcher = FilePatcher.FromPatchFile(patchPath);

			patcher.Patch(mode);
			results.Add(patcher);
			DirectoryUtility.CreateParentDirectory(patcher.PatchedPath);
			patcher.Save();

			foreach (DiffPatcher.Result result in patcher.results)
			{
				if (!result.success)
				{
					PatchFailureCount++;
					continue;
				}

				if (result.mode == DiffPatcher.Mode.FUZZY || result.offsetWarning) PatchWarningCount++;
				if (result.mode == DiffPatcher.Mode.EXACT) PatchExactCount++;
				else if (result.mode == DiffPatcher.Mode.OFFSET) PatchOffsetCount++;
				else if (result.mode == DiffPatcher.Mode.FUZZY) PatchFuzzyCount++;
			}

			return patcher;
		}
	}
}
