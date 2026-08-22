using System;
using System.IO;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using ICSharpCode.Decompiler.Metadata;

class Program
{
    static void Main()
    {
        string gameDir = @"D:\SteamLibrary\steamapps\common\SpaceEngineers2\Game2";
        string outputRoot = @"C:\Users\tomer\dev\Kontrol\se2";

        string[] dlls = new[]
        {
            "Game2.Client.dll",
            "Game2.Simulation.dll",
            "Game2.Game.dll",
            "VRage.Core.dll",
            "VRage.DCS.dll",
            "VRage.Library.dll",
            "VRage.Physics.dll",
            "VRage.Input.dll"
        };

        foreach (var dll in dlls)
        {
            string dllPath = Path.Combine(gameDir, dll);
            if (!File.Exists(dllPath))
            {
                Console.WriteLine($"Skipping {dll} - not found at {dllPath}");
                continue;
            }

            string targetDir = Path.Combine(outputRoot, Path.GetFileNameWithoutExtension(dll));
            Directory.CreateDirectory(targetDir);
            Console.WriteLine($"Decompiling {dll} to {targetDir}...");

            try
            {
                var module = new PEFile(dllPath);
                var resolver = new UniversalAssemblyResolver(dllPath, false, module.DetectTargetFrameworkId());
                resolver.AddSearchDirectory(gameDir);

                var settings = new DecompilerSettings(LanguageVersion.Latest)
                {
                    ThrowOnAssemblyResolveErrors = false
                };

                var decompiler = new WholeProjectDecompiler(settings, resolver, null, null, null);
                decompiler.DecompileProject(module, targetDir);
                Console.WriteLine($"Completed {dll}!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error decompiling {dll}: {ex.Message}");
            }
        }

        Console.WriteLine("All DLLs decompiled successfully!");
    }
}
