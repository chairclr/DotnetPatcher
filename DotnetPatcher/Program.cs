
using DotnetPatcher;
using DotnetPatcher.Decompile;
using DotnetPatcher.Patch;
using DotnetPatcher.Diff;

public class Program
{
    public static void Main(string[] args)
    {
        string targetPath = @"C:\Users\chair\source\repos\DotnetPatcher\DotnetPatcherTest\bin\Debug\net6.0\DotnetPatcherTest.dll";
        string outputPath = @"C:\Users\chair\source\repos\DotnetPatcher\DotnetPatcher\bin\Debug\net6.0\src\Test";
        string patchedPath = @"C:\Users\chair\source\repos\DotnetPatcher\DotnetPatcher\bin\Debug\net6.0\src\TestPatched";
        string patchesPath = @"C:\Users\chair\source\repos\DotnetPatcher\DotnetPatcher\bin\Debug\net6.0\src\TestPatches";

        Console.WriteLine("Decompiling");
        Decompiler decompiler = new Decompiler(targetPath, outputPath);
        
        decompiler.Decompile();
        Console.WriteLine("Decompiling succeeded");

        Console.WriteLine("Patching");
        Patcher patcher = new Patcher(outputPath, patchesPath, patchedPath);

        patcher.Patch();
        Console.WriteLine("Patching succeeded");

        Console.WriteLine("Diffing");
        Differ differ = new Differ(outputPath, patchesPath, patchedPath);

        differ.Diff();
        Console.WriteLine("Diffing succeeded");
    }
}