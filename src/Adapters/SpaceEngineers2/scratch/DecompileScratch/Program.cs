using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using ICSharpCode.Decompiler.Metadata;

internal static class Program
{
    private static readonly string[] Dlls =
    [
        "Game2.Client.dll",
        "Game2.Simulation.dll",
        "Game2.Game.dll",
        "VRage.Core.dll",
        "VRage.DCS.dll",
        "VRage.Library.dll",
        "VRage.Physics.dll",
        "VRage.Input.dll"
    ];

    public static int Main(string[] args)
    {
        if (args.Length == 1 && (args[0] is "--help" or "-h"))
        {
            PrintUsage();
            return 0;
        }

        if (!TryParseArguments(args, out string? gameDirectory, out string? outputDirectory, out string? error))
        {
            Console.Error.WriteLine(error);
            PrintUsage();
            return 2;
        }

        string gameDirectoryPath = gameDirectory!;
        string outputDirectoryPath = outputDirectory!;

        if (!Directory.Exists(gameDirectoryPath))
        {
            Console.Error.WriteLine($"Game directory does not exist: {gameDirectoryPath}");
            return 2;
        }

        Directory.CreateDirectory(outputDirectoryPath);

        int skipped = 0;
        int failures = 0;
        int completed = 0;

        foreach (string dll in Dlls)
        {
            string dllPath = Path.Combine(gameDirectoryPath, dll);
            if (!File.Exists(dllPath))
            {
                Console.WriteLine($"Skipping {dll} because it was not found in the game directory.");
                skipped++;
                continue;
            }

            string targetDirectory = Path.Combine(outputDirectoryPath, Path.GetFileNameWithoutExtension(dll));
            Directory.CreateDirectory(targetDirectory);
            Console.WriteLine($"Decompiling {dll} to {targetDirectory}...");

            try
            {
                var module = new PEFile(dllPath);
                var resolver = new UniversalAssemblyResolver(dllPath, false, module.DetectTargetFrameworkId());
                resolver.AddSearchDirectory(gameDirectoryPath);

                var settings = new DecompilerSettings(LanguageVersion.Latest)
                {
                    ThrowOnAssemblyResolveErrors = false
                };

                var decompiler = new WholeProjectDecompiler(settings, resolver, null, null, null);
                decompiler.DecompileProject(module, targetDirectory);
                Console.WriteLine($"Completed {dll}.");
                completed++;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error decompiling {dll}: {ex.Message}");
                failures++;
            }
        }

        Console.WriteLine($"Decompilation finished: {completed} completed, {skipped} skipped, {failures} failed.");
        return failures == 0 ? 0 : 1;
    }

    private static bool TryParseArguments(
        string[] args,
        out string? gameDirectory,
        out string? outputDirectory,
        out string? error)
    {
        gameDirectory = null;
        outputDirectory = null;
        error = null;

        for (int index = 0; index < args.Length; index++)
        {
            string option = args[index];
            if (option is not ("--game-directory" or "--output-directory"))
            {
                error = $"Unknown option: {option}";
                return false;
            }

            if (++index >= args.Length || string.IsNullOrWhiteSpace(args[index]))
            {
                error = $"Missing value for {option}.";
                return false;
            }

            string value = Path.GetFullPath(args[index]);
            if (option == "--game-directory")
            {
                if (gameDirectory is not null)
                {
                    error = "The game directory was provided more than once.";
                    return false;
                }

                gameDirectory = value;
            }
            else
            {
                if (outputDirectory is not null)
                {
                    error = "The output directory was provided more than once.";
                    return false;
                }

                outputDirectory = value;
            }
        }

        if (gameDirectory is null || outputDirectory is null)
        {
            error = "Both --game-directory and --output-directory are required.";
            return false;
        }

        return true;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run -- --game-directory <SE2 Game2 path> --output-directory <output path>");
    }
}
