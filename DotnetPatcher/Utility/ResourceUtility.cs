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
	public class ResourceUtility
	{
		public static void ExtractResource(string projectOutputDirectory, string name, Resource res, string projectDir)
		{
			string path = Path.Combine(projectOutputDirectory, projectDir, name);
			DirectoryUtility.CreateParentDirectory(path);

			Stream s = res.TryOpenStream();
			s.Position = 0;
			using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
				s.CopyTo(fs);
		}

		public static WorkTask ExtractResourceAsync(string projectOutputDirectory, string name, Resource res, string projectDir)
		{
			return new WorkTask(() =>
			{
				ExtractResource(projectOutputDirectory, name, res, projectDir);
			});
		}

		public static IEnumerable<(string path, Resource r)> GetResourceFiles(PEFile module)
		{
			return module.Resources.Where(r => r.ResourceType == ResourceType.Embedded).Select(res => (DirectoryUtility.GetOutputPath(res.Name, module), res));
		}
	}
}
