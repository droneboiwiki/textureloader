using System;
using System.Runtime.InteropServices;
using System.CommandLine;
using System.CommandLine.Parsing;

internal class Program
{
    private static async void Main(string[] args)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Error("Only Windows is supported");
        }
        var root = new RootCommand { };
        {
            var arg1 = new Option<string>(
                "--assets",
                "Path to directory of asset files or .apk");
            var arg2 = new Option<string>(
                "--output",
                "Output directory path (directory must be empty)");
            var arg3 = new Option<bool>(
                "--webp",
                "Output .webp instead of .png (Not supported for packing)");
            arg1.AddAlias("-a"); arg1.IsRequired = true;
            arg2.AddAlias("-o"); arg2.IsRequired = true;
            arg3.AddAlias("-w");
            arg1.AddValidator(ValidateOptionA);
            arg2.AddValidator(r => ValidateOptionO(r, true));
            var command = new Command("extract", "Extract textures from assets or .apk to a directory") { arg1, arg2, arg3 };
            command.SetHandler(Extract.Run, arg1, arg2, arg3);
            root.AddCommand(command);
        }
        {
            var arg1 = new Option<string>(
                "--assets",
                "Path to directory of asset files or .apk");
            var arg2 = new Option<string>(
                "--textures",
                "Path to directory of modified extracted textures.");
            var arg3 = new Option<string>(
                "--output",
                "Output directory path (or file path for .apk)");
            arg1.AddAlias("-a"); arg1.IsRequired = true;
            arg2.AddAlias("-t"); arg2.IsRequired = true;
            arg3.AddAlias("-o"); arg3.IsRequired = true;
            arg1.AddValidator(ValidateOptionA);
            arg2.AddValidator(ValidateOptionT);
            arg3.AddValidator(r=> ValidateOptionO(r, false));
            var command = new Command("pack", "Save textures from a directory to asset files") { arg1, arg2, arg3 };
            command.SetHandler(Pack.Run, arg1, arg2, arg3);
            root.AddCommand(command);
        }

        Config.Init();

        await root.InvokeAsync(args);
    }
    private static void ValidateOptionA(OptionResult r)
    {
        string arg = r.GetValueOrDefault<string>();
        bool isApk = arg.EndsWith(".apk");
        if (isApk)
        {
            if (!File.Exists(arg)) Error("File does not exist, possibly invalid path?");
            return;
        }
        if (!Directory.Exists(arg)) Error("Directory does not exist, possibly invalid path?");
        var dir = new DirectoryInfo(arg);
        var existingFiles = dir.GetFiles().Select(f => f.Name);
        var expectedFiles = new string[]
        {
            "globalgamemanagers", "globalgamemanagers.assets", "level0", "level1", "level2",
            "resources.assets", "resources.assets.resS", "resources.resource",
            "sharedassets0.assets", "sharedassets0.assets.resS",
            "sharedassets1.assets", "sharedassets1.assets.resS",
            "sharedassets2.assets", "sharedassets2.assets.resS",
        };
        foreach (string expectedFile in expectedFiles)
        {
            if (!existingFiles.Contains(expectedFile)) Error($"Could not find missing file: {expectedFile}");
        }
    }
    private static void ValidateOptionT(OptionResult r)
    {
        string arg = r.GetValueOrDefault<string>();
        var dir = new DirectoryInfo(arg);
        if (!dir.Exists) Error("Directory does not exist, possibly invalid path?");
        var existingFiles = dir.GetFiles().Select(f => f.Name);
        var expectedFiles = new string[]
        {
            "map.yaml"
        };
        foreach (string expectedFile in expectedFiles)
        {
            if (!existingFiles.Contains(expectedFile)) Error($"Could not find missing file: {expectedFile}");
        }
    }
    private static void ValidateOptionO(OptionResult r, bool mustBeEmpty = false)
    {
        string arg = r.GetValueOrDefault<string>();
        bool isApk = arg.EndsWith(".apk");
        if (isApk)
        {
            var file = new FileInfo(arg);
            if (!file.Directory.Exists) Error("File parent directory does not exist, possibly invalid path?");
            if (mustBeEmpty && file.Exists) Error($"File already exists: {arg}");
            return;
        }
        var dir = new DirectoryInfo(arg);
        if (!dir.Exists) Error("Directory does not exist, possibly invalid path?");
        if (mustBeEmpty && dir.GetFileSystemInfos().Length != 0) Error("Output directory must be empty");
    }
    public static void Error(string msg)
    {
        Console.WriteLine(msg);
        Environment.Exit(0);
    }
}