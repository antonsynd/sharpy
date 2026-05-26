using System.CommandLine;
using Sharpy.Compiler.Discovery.Caching;

namespace Sharpy.Cli.Commands;

internal static class CacheCommand
{
    internal static void Configure(RootCommand root, GlobalOptions globals)
    {
        var command = new Command("cache", "Manage the overload discovery cache");

        var clearCommand = new Command("clear", "Clear the cache");
        var clearDirOpt = new Option<string?>("--cache-dir") { Description = "Custom cache directory" };
        clearCommand.Options.Add(clearDirOpt);
        clearCommand.SetAction((parseResult) =>
        {
            var cacheDir = parseResult.GetValue(clearDirOpt);
            return ClearCache(cacheDir);
        });

        var infoCommand = new Command("info", "Display cache information");
        var infoDirOpt = new Option<string?>("--cache-dir") { Description = "Custom cache directory" };
        infoCommand.Options.Add(infoDirOpt);
        infoCommand.SetAction((parseResult) =>
        {
            var cacheDir = parseResult.GetValue(infoDirOpt);
            return ShowCacheInfo(cacheDir);
        });

        command.Subcommands.Add(clearCommand);
        command.Subcommands.Add(infoCommand);

        root.Subcommands.Add(command);
    }

    static int ClearCache(string? cacheDir)
    {
        try
        {
            var cache = new OverloadIndexCache(cacheDir);
            cache.ClearAll();
            Console.WriteLine("Overload discovery cache cleared successfully.");
            if (cacheDir != null)
            {
                Console.WriteLine($"Cache directory: {cacheDir}");
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error clearing cache: {ex.Message}");
            return 1;
        }
    }

    static int ShowCacheInfo(string? cacheDir)
    {
        try
        {
            var cache = new OverloadIndexCache(cacheDir);
            var info = cache.GetInfo();

            Console.WriteLine("Overload Discovery Cache Information:");
            Console.WriteLine(new string('=', 50));
            Console.WriteLine($"Cache Directory: {info.CacheDirectory}");
            Console.WriteLine($"Cached Assemblies: {info.CachedAssemblies}");
            Console.WriteLine($"Total Size: {CliHelpers.FormatBytes(info.TotalSizeBytes)}");
            Console.WriteLine(new string('=', 50));
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error retrieving cache info: {ex.Message}");
            return 1;
        }
    }
}
