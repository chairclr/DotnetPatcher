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

namespace DotnetPatcher.Decompile
{
	public class Decompiler
	{
		private readonly DecompilerSettings decompilerSettings = new DecompilerSettings(LanguageVersion.Latest)
		{
			CSharpFormattingOptions = FormattingOptionsFactory.CreateAllman()
		};
		private DecompilerUtility.ExtendedProjectDecompiler projectDecompiler;

		public string TargetFile;
		public string SourceOutputDirectory;

		public Decompiler(string targetFile, string sourceOutputDirectory)
		{
			this.TargetFile = targetFile;
			this.SourceOutputDirectory = sourceOutputDirectory;
		}


		public void DeleteOldSource()
		{
			if (Directory.Exists(SourceOutputDirectory))
				Directory.Delete(SourceOutputDirectory, true);
			else
				Directory.CreateDirectory(SourceOutputDirectory);
		}


		public void Decompile(string[] decompiledLibraries = null)
		{
			if (!File.Exists(TargetFile))
				throw new FileNotFoundException($"{TargetFile} does not exist");

			DeleteOldSource();

			PEFile mainModule = ModuleReader.ReadModule(TargetFile, true);

			projectDecompiler = new DecompilerUtility.ExtendedProjectDecompiler(new EmbeddedAssemblyResolver(mainModule, mainModule.Reader.DetectTargetFrameworkId(), SourceOutputDirectory));
			projectDecompiler.Settings.CSharpFormattingOptions = FormattingOptionsFactory.CreateKRStyle();

			List<WorkTask> items = new List<WorkTask>();
			HashSet<string> files = new HashSet<string>();
			HashSet<string> resources = new HashSet<string>();
			List<string> exclude = new List<string>();

			if (decompiledLibraries != null)
			{
				foreach (string lib in decompiledLibraries)
				{
					Resource libRes = mainModule.Resources.Single(r => r.Name.EndsWith(lib + ".dll"));
					ProjectFileUtility.AddEmbeddedLibrary(libRes, SourceOutputDirectory, projectDecompiler, decompilerSettings, projectDecompiler.AssemblyResolver, items);
					exclude.Add(DirectoryUtility.GetOutputPath(libRes.Name, mainModule));
				}
			}

			DecompilerUtility.DecompileModule(mainModule, projectDecompiler.AssemblyResolver, projectDecompiler, items, files, resources, SourceOutputDirectory, decompilerSettings, exclude);

			items.Add(ProjectFileUtility.WriteProjectFile(mainModule, SourceOutputDirectory, files, resources, decompiledLibraries));
			items.Add(ProjectFileUtility.WriteCommonConfigurationFile(SourceOutputDirectory));

			WorkTask.ExecuteParallel(items);
		}
	}
}
