
<h1 align="center">
Dotnet Patcher
</h1>
<p align="center">
Dotnet Patcher is a Dotnet decompiling, patching, and diffing tool.
</p>
<br>

<h2>
Examples
</h2>


<h3>
Decompiling & Patching
</h3>


```cs
// Decompile an executable into source code
Decompiler executableDecompiler = new Decompiler(executablePath, decompilerOutputPath);
executableDecompiler.Decompile();

// Patch the decompiled source code with some patches
Patcher executablePatcher = new Patcher(decompilerOutputPath, patchesPath, patchedOutputPath);
executablePatcher.Patch();
```

<h3>
Creating patches with diff
</h3>


```cs
// Create some patches based on the changes made to the decompiled source code
Differ patchDiffer = new Differ(decompilerOutputPath, patchesPath, patchedOutputPath);

patchDiffer.Diff();
```

<h2>
Main features
</h2>


- Quickly decompile .NET executables into source code
- Patch code using fuzzy patches, exact patches, and offset patches
- Quickly create patches based on the difference between two directories

<h2>
Planned features
</h2>


- Both synchronous and asynchronous decompilation, patching, and diffing

<h2>
Contributing
</h2>


To contribute, open a pull request and I will review it and accept the PR if it suitable.

<h2>
Questions?
</h2>


Open an issue!