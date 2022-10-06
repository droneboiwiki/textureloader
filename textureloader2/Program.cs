using System;
using System.Runtime.InteropServices;
using System.CommandLine;
using SixLabors.ImageSharp;

internal class Program
{
    private static async void Main(string[] args)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.WriteLine("Only Windows is supported");
            Environment.Exit(0);
        }
        var root = new RootCommand { };
        {
            var arg1 = new Option<string>(
                "--assets",
                "Path to directory of asset files");
            var arg2 = new Option<string>(
                "--output",
                "Output directory path (directory must be empty)");
            var command = new Command("extract", "Extract assets to a directory") { arg1, arg2 };
            command.SetHandler(Extract.Run, arg1, arg2);
            root.AddCommand(command);
        }
        {
            var arg1 = new Option<string>(
                "--assets",
                "Path to directory of asset files");
            var arg2 = new Option<string>(
                "--output",
                "Output directory path (directory must be empty)");
            var command = new Command("pack", "Save assets from a directory to asset files") { arg1, arg2 };
            command.SetHandler(Pack, arg1, arg2);
            root.AddCommand(command);
        }

        Config.Init();

        await root.InvokeAsync(args);
    }
    
    private static void Pack(string assetDir, string outputDir)
    {

    }
}