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

namespace DotnetPatcher.Utility
{
	public class DirectoryUtility
	{
		private static string[] nonSourceDirs = { "bin", "obj", ".vs" };
		public static IEnumerable<(string file, string relPath)> EnumerateSrcFiles(string dir) =>
			EnumerateFiles(dir).Where(f => !f.relPath.Split('/', '\\').Any(nonSourceDirs.Contains));

		public static void CreateDirectory(string dir)
		{
			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir);
		}
		public static void CreateParentDirectory(string path) => CreateDirectory(Path.GetDirectoryName(path));
		public static string GetOutputPath(string path, PEFile module)
		{
			if (path.EndsWith(".dll"))
			{
				ICSharpCode.Decompiler.Metadata.AssemblyReference? asmRef = module.AssemblyReferences.SingleOrDefault(r => path.EndsWith(r.Name + ".dll"));
				if (asmRef != null)
					path = Path.Combine(path.Substring(0, path.Length - asmRef.Name.Length - 5), asmRef.Name + ".dll");
			}

			string? rootNamespace = AssemblyUtility.GetAssemblyTitle(module);
			if (path.StartsWith(rootNamespace))
				path = path.Substring(rootNamespace.Length + 1);

			path = path.Replace("Libraries.", "Libraries/");
			path = path.Replace('\\', '/');

			int stopFolderzingAt = path.IndexOf('/');
			if (stopFolderzingAt < 0)
				stopFolderzingAt = path.LastIndexOf('.');
			path = new StringBuilder(path).Replace(".", "/", 0, stopFolderzingAt).ToString();

			if (Util.IsCultureFile(path))
				path = path.Insert(path.LastIndexOf('.'), "/Main");

			return path;
		}

		public static string PreparePath(string path) => path.Replace('/', Path.DirectorySeparatorChar);
		public static bool DeleteEmptyDirs(string dir)
		{
			if (!Directory.Exists(dir))
				return true;

			return DeleteEmptyDirsRecursion(dir);
		}
		private static bool DeleteEmptyDirsRecursion(string dir)
		{
			bool allEmpty = true;

			foreach (string subDir in Directory.EnumerateDirectories(dir))
				allEmpty &= DeleteEmptyDirsRecursion(subDir);

			if (!allEmpty || Directory.EnumerateFiles(dir).Any())
				return false;

			Directory.Delete(dir);

			return true;
		}

		public static string RelPath(string basePath, string path)
		{
			if (path.Last() == Path.DirectorySeparatorChar)
				path = path.Substring(0, path.Length - 1);

			if (basePath.Last() != Path.DirectorySeparatorChar)
				basePath += Path.DirectorySeparatorChar;

			if (path + Path.DirectorySeparatorChar == basePath) return "";

			if (!path.StartsWith(basePath))
			{
				path = Path.GetFullPath(path);
				basePath = Path.GetFullPath(basePath);
			}

			if (!path.StartsWith(basePath))
				throw new ArgumentException("Path \"" + path + "\" is not relative to \"" + basePath + "\"");

			return path.Substring(basePath.Length);
		}
		public static IEnumerable<(string file, string relPath)> EnumerateFiles(string dir) =>
		   Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
		   .Select(path => (file: path, relPath: RelPath(dir, path)));
		public static void Copy(string from, string to)
		{
			CreateParentDirectory(to);

			if (File.Exists(to))
			{
				File.SetAttributes(to, FileAttributes.Normal);
			}

			File.Copy(from, to, true);

		}
		public static void DeleteFile(string path)
		{
			if (File.Exists(path))
			{
				File.SetAttributes(path, FileAttributes.Normal);
				File.Delete(path);
			}
		}
	}
}
