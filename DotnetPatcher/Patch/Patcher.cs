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
using static CodeChicken.DiffPatch.Patcher;

namespace DotnetPatcher.Patch
{
	public class Patcher
	{
		private readonly ConcurrentBag<FilePatcher?> results = new ConcurrentBag<FilePatcher?>();
		private static readonly string RemovedFileList = "removed_files.list";

		public DiffPatcher.Mode PatcherMode;

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
			PatcherMode = DiffPatcher.Mode.FUZZY;

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
                        FilePatcher? filePatcher = PatchFile(file);

                        if (filePatcher is not null)
                        {
                            string patchedPathReal = Path.GetFullPath(DirectoryUtility.PreparePath(filePatcher.PatchedPath));

                            newFiles.Add(patchedPathReal);
                        }
					}));
					noCopy.Add(relPath.Substring(0, relPath.Length - 6));
				}
				else if (relPath != RemovedFileList)
				{
					string destination = Path.GetFullPath(Path.Combine(PatchedPath, relPath));

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
					string destination = Path.GetFullPath(Path.Combine(PatchedPath, relPath));

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
				if (!newFiles.Contains(Path.GetFullPath(file)))
					File.Delete(file);

			DirectoryUtility.DeleteEmptyDirs(PatchedPath);

			if (PatchFailureCount > 0)
			{
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Errors occured during patching process");
                Console.ResetColor();
			}

            Console.Write("Patching Stats: ");

            if (PatchFailureCount > 0) Console.ForegroundColor = ConsoleColor.Red;
            Console.Write($"{PatchFailureCount} Errors ");
            Console.ResetColor();

            if (PatchWarningCount > 0) Console.ForegroundColor = ConsoleColor.Red;
            Console.Write($"{PatchWarningCount} Warnings ");
            Console.ResetColor();

            if (PatchFuzzyCount > 0) Console.ForegroundColor = ConsoleColor.Blue;
            Console.Write($"{PatchFuzzyCount} Fuzzy Patches\n");
            Console.ResetColor();

            foreach (FilePatcher? patcher in results)
            {
                if (patcher is null)
                    continue;
                if (patcher.results is null)
                    continue;
                foreach (Result result in patcher.results)
                {
                    if (PatchFailureCount > 0)
                    {
                        if (!result.success)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"Failed Patch at {patcher.patchFilePath}");
                            Console.WriteLine(result.patch.ToString());
                            Console.ResetColor();
                        }
                    }

                    if (PatchFuzzyCount > 0)
                    {
                        if (result.mode == Mode.FUZZY)
                        {
                            Console.ForegroundColor = ConsoleColor.Blue;
                            Console.WriteLine($"Patch ({patcher.patchFilePath}) fuzzy patched with quality of {(result.fuzzyQuality * 100):F2}%");
                            Console.ResetColor();
                        }
                    }

                    if (PatchWarningCount > 0)
                    {
                        if (result.offsetWarning)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"Warned Patch at {patcher.patchFilePath}");
                            Console.WriteLine(result.patch.ToString());
                            Console.ResetColor();
                        }
                    }
                }
            }
        }

		private FilePatcher? PatchFile(string patchPath)
		{
            FilePatcher? patcher = null;
            try
            {
                patcher = FilePatcher.FromPatchFile(patchPath);
                results.Add(patcher);

                patcher.Patch(PatcherMode);
                DirectoryUtility.CreateParentDirectory(patcher.PatchedPath);
                patcher.Save();

                foreach (Result result in patcher.results)
                {
                    if (!result.success)
                    {
                        PatchFailureCount++;
                        continue;
                    }

                    if (result.offsetWarning) PatchWarningCount++;
                    if (result.mode == Mode.EXACT) PatchExactCount++;
                    else if (result.mode == Mode.OFFSET) PatchOffsetCount++;
                    else if (result.mode == Mode.FUZZY) PatchFuzzyCount++;
                }

               return patcher;
            }
             catch (Exception e)
            {
                PatchFailureCount++;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Exception occured when patching with {patcher?.patchFilePath ?? "Uknown file (\"FilePatcher? patcher\" was null)"}");
                Console.WriteLine(e.ToString());
                Console.ResetColor();
                return patcher;
            }
		}
	}
}
