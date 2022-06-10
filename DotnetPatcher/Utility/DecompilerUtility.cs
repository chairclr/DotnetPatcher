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

namespace DotnetPatcher.Utility
{
	public class DecompilerUtility
	{
		public class ExtendedProjectDecompiler : WholeProjectDecompiler
		{
			public ExtendedProjectDecompiler(IAssemblyResolver assemblyResolver) : base(assemblyResolver)
			{
			}

			public new bool IncludeTypeWhenDecompilingProject(PEFile module, TypeDefinitionHandle type) => base.IncludeTypeWhenDecompilingProject(module, type);
		}
		public static CSharpDecompiler CreateDecompiler(DecompilerTypeSystem ts, DecompilerSettings settings)
		{
			CSharpDecompiler decompiler = new CSharpDecompiler(ts, settings);
			decompiler.AstTransforms.Add(new EscapeInvalidIdentifiers());
			decompiler.AstTransforms.Add(new RemoveCLSCompliantAttribute());
			return decompiler;
		}

		public static IEnumerable<IGrouping<string, TypeDefinitionHandle>> GetCodeFiles(PEFile module, ExtendedProjectDecompiler decompiler)
		{
			MetadataReader? metadata = module.Metadata;
			return module.Metadata.GetTopLevelTypeDefinitions().Where(td => decompiler.IncludeTypeWhenDecompilingProject(module, td))
				.GroupBy(h =>
				{
					TypeDefinition type = metadata.GetTypeDefinition(h);
					string path = WholeProjectDecompiler.CleanUpFileName(metadata.GetString(type.Name)) + ".cs";
					if (!string.IsNullOrEmpty(metadata.GetString(type.Namespace)))
						path = Path.Combine(WholeProjectDecompiler.CleanUpFileName(metadata.GetString(type.Namespace)), path);
					return DirectoryUtility.GetOutputPath(path, module);
				}, StringComparer.OrdinalIgnoreCase);
		}

		public static void DecompileSourceFile(DecompilerTypeSystem ts, IGrouping<string, TypeDefinitionHandle> src, string projectOutputDirectory, string projectName, DecompilerSettings settings, string conditional = null)
		{
			string path = Path.Combine(projectOutputDirectory, projectName, src.Key);
			DirectoryUtility.CreateParentDirectory(path);

			using (StringWriter writer = new StringWriter())
			{
				if (conditional != null)
					writer.WriteLine("#if " + conditional);

				DecompilerUtility.CreateDecompiler(ts, settings)
					.DecompileTypes(src.ToArray())
					.AcceptVisitor(new CSharpOutputVisitor(writer, settings.CSharpFormattingOptions));

				if (conditional != null)
					writer.WriteLine("#endif");

				string source = writer.ToString();

				File.WriteAllText(path, source);
			}
		}
		public static WorkTask DecompileSourceFileAsync(DecompilerTypeSystem ts, IGrouping<string, TypeDefinitionHandle> src, string projectOutputDirectory, string projectName, DecompilerSettings settings, string conditional = null)
		{
			return new WorkTask(() =>
			{
				DecompileSourceFile(ts, src, projectOutputDirectory, projectName, settings, conditional);
			});
		}

		public static DecompilerTypeSystem DecompileModule(PEFile module, IAssemblyResolver resolver, ExtendedProjectDecompiler decompiler, List<WorkTask> items, ISet<string> sourceSet, ISet<string> resourceSet, string projectOutputDirectory, DecompilerSettings settings, ICollection<string> exclude = null, string conditional = null)
		{
			string projectDir = AssemblyUtility.GetAssemblyTitle(module);
			List<IGrouping<string, TypeDefinitionHandle>> sources = GetCodeFiles(module, decompiler).ToList();
			List<(string path, Resource r)> resources = ResourceUtility.GetResourceFiles(module).ToList();
			if (exclude != null)
			{
				sources.RemoveAll(src => exclude.Contains(src.Key));
				resources.RemoveAll(res => exclude.Contains(res.path));
			}

			DecompilerTypeSystem ts = new DecompilerTypeSystem(module, resolver, settings);
			items.AddRange(sources
				.Where(src => sourceSet.Add(src.Key))
				.Select(src => DecompileSourceFileAsync(ts, src, projectOutputDirectory, projectDir, settings, conditional)));

			if (conditional != null && resources.Any(res => !resourceSet.Contains(res.path)))
				throw new Exception($"Conditional ({conditional}) resources not supported");

			items.AddRange(resources
				.Where(res => resourceSet.Add(res.path))
				.Select(res => ResourceUtility.ExtractResourceAsync(projectOutputDirectory, res.path, res.r, projectDir)));

			return ts;
		}
	}
}
