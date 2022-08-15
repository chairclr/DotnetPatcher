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
					string path = CleanUpName(metadata.GetString(type.Name), false, false) + ".cs";
					if (!string.IsNullOrEmpty(metadata.GetString(type.Namespace)))
						path = Path.Combine(WholeProjectDecompiler.CleanUpPath(metadata.GetString(type.Namespace)), path);
					return DirectoryUtility.GetOutputPath(path, module);
				}, StringComparer.OrdinalIgnoreCase);
		}

        static bool IsReservedFileSystemName(string name)
        {
            switch (name.ToUpperInvariant())
            {
                case "AUX":
                case "COM1":
                case "COM2":
                case "COM3":
                case "COM4":
                case "COM5":
                case "COM6":
                case "COM7":
                case "COM8":
                case "COM9":
                case "CON":
                case "LPT1":
                case "LPT2":
                case "LPT3":
                case "LPT4":
                case "LPT5":
                case "LPT6":
                case "LPT7":
                case "LPT8":
                case "LPT9":
                case "NUL":
                case "PRN":
                    return true;
                default:
                    return false;
            }
        }
        static string CleanUpName(string text, bool separateAtDots, bool treatAsFileName)
        {
            int pos = text.IndexOf(':');
            if (pos > 0)
                text = text.Substring(0, pos);
            pos = text.IndexOf('`');
            if (pos > 0)
                text = text.Substring(0, pos);
            text = text.Trim();
            string extension = null;
            int currentSegmentLength = 0;
            bool supportsLongPaths = true;
            int maxPathLength = 256;
            int maxSegmentLength = 64;
            if (treatAsFileName)
            {
                // Check if input is a file name, i.e., has a valid extension
                // If yes, preserve extension and append it at the end.
                // But only, if the extension length does not exceed maxSegmentLength,
                // if that's the case we just give up and treat the extension no different
                // from the file name.
                int lastDot = text.LastIndexOf('.');
                if (lastDot >= 0 && text.Length - lastDot < maxSegmentLength)
                {
                    string originalText = text;
                    extension = text.Substring(lastDot);
                    text = text.Remove(lastDot);
                    foreach (var c in extension)
                    {
                        if (!(char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.'))
                        {
                            // extension contains an invalid character, therefore cannot be a valid extension.
                            extension = null;
                            text = originalText;
                            break;
                        }
                    }
                }
            }
            // Whitelist allowed characters, replace everything else:
            StringBuilder b = new StringBuilder(text.Length + (extension?.Length ?? 0));
            foreach (var c in text)
            {
                currentSegmentLength++;
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
                {
                    // if the current segment exceeds maxSegmentLength characters,
                    // skip until the end of the segment.
                    if (currentSegmentLength <= maxSegmentLength)
                        b.Append(c);
                }
                else if (c == '.' && b.Length > 0 && b[b.Length - 1] != '.')
                {
                    // if the current segment exceeds maxSegmentLength characters,
                    // skip until the end of the segment.
                    if (separateAtDots || currentSegmentLength <= maxSegmentLength)
                        b.Append('.'); // allow dot, but never two in a row

                    // Reset length at end of segment.
                    if (separateAtDots)
                        currentSegmentLength = 0;
                }
                else if (treatAsFileName && (c == '/' || c == '\\') && currentSegmentLength > 0)
                {
                    // if we treat this as a file name, we've started a new segment
                    b.Append(c);
                    currentSegmentLength = 0;
                }
                else
                {
                    // if the current segment exceeds maxSegmentLength characters,
                    // skip until the end of the segment.
                    if (currentSegmentLength <= maxSegmentLength)
                        b.Append('-');
                }
                if (b.Length >= maxPathLength && !supportsLongPaths)
                    break;  // limit to 200 chars, if long paths are not supported.
            }
            if (b.Length == 0)
                b.Append('-');
            string name = b.ToString();
            if (extension != null)
                name += extension;
            if (IsReservedFileSystemName(name))
                return name + "_";
            else if (name == ".")
                return "_";
            else
                return name;
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
