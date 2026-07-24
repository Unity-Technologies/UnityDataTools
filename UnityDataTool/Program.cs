using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityDataTools.Analyzer;
using UnityDataTools.Archive;
using UnityDataTools.FileSystem;
using UnityDataTools.ReferenceFinder;
using UnityDataTools.SerializedFile;
using UnityDataTools.TextDumper;

namespace UnityDataTools.UnityDataTool;

public static class Program
{
    const string TypeTreeDataDescription = "Path to an external TypeTree data file to load before processing bundles";

    public static async Task<int> Main(string[] args)
    {
        UnityFileSystem.Init();

        var rootCommand = new RootCommand(BuildRootDescription());
        rootCommand.AddCommand(BuildAnalyzeCommand());
        rootCommand.AddCommand(BuildFindRefsCommand());
        rootCommand.AddCommand(BuildDumpCommand());
        rootCommand.AddCommand(BuildArchiveCommand());
        rootCommand.AddCommand(BuildSerializedFileCommand());

        var r = await rootCommand.InvokeAsync(args);

        UnityFileSystem.Cleanup();

        return r;
    }

    const string DocumentationUrl = "https://github.com/Unity-Technologies/UnityDataTools/blob/main/Documentation/unitydatatool.md";

    static string BuildRootDescription()
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "unknown";

        // Strip the SourceLink build-metadata suffix (e.g. "1.3.5+<commit>").
        var plusIndex = version.IndexOf('+');
        if (plusIndex >= 0)
            version = version.Substring(0, plusIndex);

        return
            "UnityDataTool inspects and analyzes Unity file formats, for example the content formats for AssetBundles, " +
            "Player and content directory builds. It can build a database of the Unity objects and their " +
            "references for analysis, dump objects as text, and examine " +
            "archive and SerializedFile internals.\n\n" +
            "Run 'UnityDataTool [command] --help' for detailed help on a specific command.\n\n" +
            $"Documentation: {DocumentationUrl}\n" +
            $"Version: {version}";
    }

    static Command BuildAnalyzeCommand()
    {
        var pathArg = new Argument<FileSystemInfo[]>("paths",
            "One or more files or directories to analyze. Directories are scanned (see --search-pattern and --no-recurse); "
            + "files are analyzed directly. Combine paths to include files from multiple locations, e.g. a build output "
            + "directory and a build report file.")
        {
            Arity = ArgumentArity.OneOrMore
        }.ExistingOnly();
        var oOpt = new Option<string>(aliases: new[] { "--output-file", "-o" }, description: "Filename of the output database", getDefaultValue: () => "database.db");
        var sOpt = new Option<bool>(aliases: new[] { "--skip-references", "-s" }, description: "Do not extract references");
        var scOpt = new Option<bool>(aliases: new[] { "--skip-crc" }, description: "Skip CRC checksum calculation");
        var rOpt = new Option<bool>(aliases: new[] { "--extract-references", "-r" }) { IsHidden = true };
        var pOpt = new Option<string>(aliases: new[] { "--search-pattern", "-p" }, description: "File search pattern applied when scanning directories", getDefaultValue: () => "*");
        var vOpt = new Option<bool>(aliases: new[] { "--verbose", "-v" }, description: "Verbose output");
        var recurseOpt = new Option<bool>(aliases: new[] { "--no-recurse" }, description: "Do not analyze contents of subdirectories inside scanned directories");
        var dOpt = new Option<FileInfo>(aliases: new[] { "--typetree-data", "-d" }, description: TypeTreeDataDescription);
        var bhOpt = new Option<DirectoryInfo>(aliases: new[] { "--build-history" },
            description: "Build history folder of the project (e.g. Library/BuildHistory). The build folder matching the "
            + "analyzed build is located automatically and its ContentLayout.json and build report are included in the analysis.")
            .ExistingOnly();

        var analyzeCommand = new Command("analyze", "Analyze AssetBundles, SerializedFiles and build reports into a database.")
        {
            pathArg,
            oOpt,
            sOpt,
            scOpt,
            rOpt,
            pOpt,
            vOpt,
            recurseOpt,
            dOpt,
            bhOpt
        };

        analyzeCommand.AddAlias("analyse");
        // Bound via InvocationContext because the option count exceeds the strongly-typed
        // SetHandler overloads.
        analyzeCommand.SetHandler((InvocationContext context) =>
        {
            var d = context.ParseResult.GetValueForOption(dOpt);
            var ttResult = LoadTypeTreeDataFile(d);
            if (ttResult != 0)
            {
                context.ExitCode = ttResult;
                return;
            }

            context.ExitCode = HandleAnalyze(
                context.ParseResult.GetValueForArgument(pathArg),
                context.ParseResult.GetValueForOption(oOpt),
                context.ParseResult.GetValueForOption(sOpt),
                context.ParseResult.GetValueForOption(scOpt),
                context.ParseResult.GetValueForOption(rOpt),
                context.ParseResult.GetValueForOption(pOpt),
                context.ParseResult.GetValueForOption(vOpt),
                context.ParseResult.GetValueForOption(recurseOpt),
                context.ParseResult.GetValueForOption(bhOpt));
        });

        return analyzeCommand;
    }

    static Command BuildFindRefsCommand()
    {
        var pathArg = new Argument<FileInfo>("databasePath", "The path to the database generated by the 'analyze' command").ExistingOnly();
        var oOpt = new Option<string>(aliases: new[] { "--output-file", "-o" }, description: "Output file", getDefaultValue: () => "references.txt");
        var iOpt = new Option<long?>(aliases: new[] { "--object-id", "-i" }, description: "Object id ('id' column in the database)");
        var nOpt = new Option<string>(aliases: new[] { "--object-name", "-n" }, description: "Object name");
        var tOpt = new Option<string>(aliases: new[] { "--object-type", "-t" }, description: "Optional object type when searching by name");
        var aOpt = new Option<bool>(aliases: new[] { "--find-all", "-a" }, description: "Find all reference chains originating from the same asset (instead of only one), can be very slow");
        var stdoutOpt = new Option<bool>(aliases: new[] { "--stdout" }, description: "Write the reference chains to stdout instead of a file.");

        var findRefsCommand = new Command("find-refs", "Find reference chains to specified object(s).")
        {
            pathArg,
            oOpt,
            aOpt,
            nOpt,
            tOpt,
            iOpt,
            stdoutOpt,
        };
        findRefsCommand.AddValidator(commandResult =>
        {
            var stdoutResult = commandResult.FindResultFor(stdoutOpt);
            var oResult = commandResult.FindResultFor(oOpt);
            bool stdoutSet = stdoutResult is { IsImplicit: false };
            bool oExplicit = oResult is { IsImplicit: false };
            if (stdoutSet && oExplicit)
            {
                commandResult.ErrorMessage = "--stdout and -o/--output-file are mutually exclusive.";
            }
        });

        findRefsCommand.SetHandler(
            (FileInfo fi, string o, long? i, string n, string t, bool a, bool toStdout) => Task.FromResult(HandleFindReferences(fi, o, i, n, t, a, toStdout)),
            pathArg, oOpt, iOpt, nOpt, tOpt, aOpt, stdoutOpt);

        return findRefsCommand;
    }

    static Command BuildDumpCommand()
    {
        var pathArg = new Argument<FileInfo>("filename", "The path of the file to dump").ExistingOnly();
        var fOpt = new Option<TextDumperTool.DumpFormat>(aliases: new[] { "--output-format", "-f" }, description: "Output format", getDefaultValue: () => TextDumperTool.DumpFormat.Text);
        var aOpt = new Option<bool>(aliases: new[] { "--show-large-arrays", "-a" }, description: "Dump the full content of large arrays of basic data types, instead of summarizing them with a hash");
        // Former option, kept so that existing scripts don't break. Ignored because skipping
        // large arrays (with a hash) is now the default behavior.
        var sOpt = new Option<bool>(aliases: new[] { "--skip-large-arrays", "-s" }) { IsHidden = true };
        var oOpt = new Option<DirectoryInfo>(aliases: new[] { "--output-path", "-o" }, description: "Output folder", getDefaultValue: () => new DirectoryInfo(Environment.CurrentDirectory));
        var objectIdOpt = new Option<long>(aliases: new[] { "--objectid", "-i" }, () => 0, "Only dump the object with this signed 64-bit id (default: 0, dump all objects)");
        var typeOpt = new Option<string>(aliases: new[] { "--type", "-t" }, description: "Filter by object type (ClassID number or type name)");
        var stdoutOpt = new Option<bool>(aliases: new[] { "--stdout" }, description: "Write the dump to stdout instead of a file. Refused for archives that contain more than one SerializedFile.");
        var dOpt = new Option<FileInfo>(aliases: new[] { "--typetree-data", "-d" }, description: TypeTreeDataDescription);

        var dumpCommand = new Command("dump",
            "Dump serialized objects from a SerializedFile as text.\nFor an archive, dumps the objects from each SerializedFile inside;\nother archive content is ignored (use archive extract for that).")
        {
            pathArg,
            fOpt,
            aOpt,
            sOpt,
            oOpt,
            objectIdOpt,
            typeOpt,
            dOpt,
            stdoutOpt,
        };
        dumpCommand.AddValidator(commandResult =>
        {
            var stdoutResult = commandResult.FindResultFor(stdoutOpt);
            var oResult = commandResult.FindResultFor(oOpt);
            bool stdoutSet = stdoutResult is { IsImplicit: false };
            bool oExplicit = oResult is { IsImplicit: false };
            if (stdoutSet && oExplicit)
            {
                commandResult.ErrorMessage = "--stdout and -o/--output-path are mutually exclusive.";
            }
        });
        dumpCommand.SetHandler(
            (FileInfo fi, TextDumperTool.DumpFormat f, bool a, DirectoryInfo o, long objectId, string type, FileInfo d, bool toStdout) =>
            {
                var ttResult = LoadTypeTreeDataFile(d);
                if (ttResult != 0) return Task.FromResult(ttResult);
                var options = new TextDumperTool.DumpOptions
                {
                    Format = f,
                    Path = fi.FullName,
                    OutputPath = o.FullName,
                    ShowLargeArrays = a,
                    ObjectId = objectId,
                    TypeFilter = type,
                    ToStdout = toStdout,
                };
                return Task.FromResult(HandleDump(options));
            },
            pathArg, fOpt, aOpt, oOpt, objectIdOpt, typeOpt, dOpt, stdoutOpt);

        return dumpCommand;
    }

    static Command BuildArchiveCommand()
    {
        var pathArg = new Argument<FileInfo>("filename", "The path of the archive file").ExistingOnly();
        var oOpt = new Option<DirectoryInfo>(aliases: new[] { "--output-path", "-o" }, description: "Output directory of the extracted archive", getDefaultValue: () => new DirectoryInfo("archive"));
        var filterOpt = new Option<string>(aliases: new[] { "--filter" }, description: "Case-insensitive substring filter on file paths inside the archive");

        var extractArchiveCommand = new Command("extract", "Extract an AssetBundle or .data file.")
        {
            pathArg,
            oOpt,
            filterOpt,
        };
        extractArchiveCommand.SetHandler(
            (FileInfo fi, DirectoryInfo o, string filter) => Task.FromResult(ArchiveTool.ExtractContent(fi, o, filter)),
            pathArg, oOpt, filterOpt);

        var fOpt = new Option<ArchiveTool.OutputFormat>(aliases: new[] { "--format", "-f" }, description: "Output format", getDefaultValue: () => ArchiveTool.OutputFormat.Text);

        var listArchiveCommand = new Command("list", "List the contents of an AssetBundle or .data file.")
        {
            pathArg,
            fOpt,
        };
        listArchiveCommand.SetHandler(
            (FileInfo fi, ArchiveTool.OutputFormat f) => Task.FromResult(ArchiveTool.ListContent(fi, f)),
            pathArg, fOpt);

        var headerArchiveCommand = new Command("header", "Display the header of a Unity Archive file.")
        {
            pathArg,
            fOpt,
        };
        headerArchiveCommand.SetHandler(
            (FileInfo fi, ArchiveTool.OutputFormat f) => Task.FromResult(ArchiveTool.PrintHeader(fi, f)),
            pathArg, fOpt);

        var blocksArchiveCommand = new Command("blocks", "Display the block list of a Unity Archive file.")
        {
            pathArg,
            fOpt,
        };
        blocksArchiveCommand.SetHandler(
            (FileInfo fi, ArchiveTool.OutputFormat f) => Task.FromResult(ArchiveTool.ListBlocks(fi, f)),
            pathArg, fOpt);

        var infoArchiveCommand = new Command("info", "Display a high-level summary of a Unity Archive file.")
        {
            pathArg,
            fOpt,
        };
        infoArchiveCommand.SetHandler(
            (FileInfo fi, ArchiveTool.OutputFormat f) => Task.FromResult(ArchiveTool.PrintSummary(fi, f)),
            pathArg, fOpt);

        return new Command("archive", "Inspect or extract the contents of a Unity archive (AssetBundle or web platform .data file).")
        {
            extractArchiveCommand,
            listArchiveCommand,
            headerArchiveCommand,
            blocksArchiveCommand,
            infoArchiveCommand,
        };
    }

    static Command BuildSerializedFileCommand()
    {
        var pathArg = new Argument<FileInfo>("filename", "The path of the SerializedFile").ExistingOnly();
        var fOpt = new Option<SerializedFileTool.OutputFormat>(aliases: new[] { "--format", "-f" }, description: "Output format", getDefaultValue: () => SerializedFileTool.OutputFormat.Text);

        var externalRefsCommand = new Command("externalrefs", "List external file references in a SerializedFile.")
        {
            pathArg,
            fOpt,
        };
        externalRefsCommand.SetHandler(
            (FileInfo fi, SerializedFileTool.OutputFormat f) => Task.FromResult(SerializedFileTool.ListExternalRefs(fi, f)),
            pathArg, fOpt);

        var objectListCommand = new Command("objectlist", "List all objects in a SerializedFile.")
        {
            pathArg,
            fOpt,
        };
        objectListCommand.SetHandler(
            (FileInfo fi, SerializedFileTool.OutputFormat f) => Task.FromResult(SerializedFileTool.ListObjects(fi, f)),
            pathArg, fOpt);

        var headerCommand = new Command("header", "Show SerializedFile header information.")
        {
            pathArg,
            fOpt,
        };
        headerCommand.SetHandler(
            (FileInfo fi, SerializedFileTool.OutputFormat f) => Task.FromResult(SerializedFileTool.PrintHeader(fi, f)),
            pathArg, fOpt);

        var metadataCommand = new Command("metadata", "Show information from the metadata section of the SerializedFile (use `-f Json` for detailed information).")
        {
            pathArg,
            fOpt,
        };
        metadataCommand.SetHandler(
            (FileInfo fi, SerializedFileTool.OutputFormat f) => Task.FromResult(SerializedFileTool.PrintMetadata(fi, f)),
            pathArg, fOpt);

        var serializedFileCommand = new Command("serialized-file", "Inspect a SerializedFile (scene, assets, etc.).")
        {
            externalRefsCommand,
            objectListCommand,
            headerCommand,
            metadataCommand,
        };
        serializedFileCommand.AddAlias("sf");
        return serializedFileCommand;
    }

    static int LoadTypeTreeDataFile(FileInfo typeTreeDataFile)
    {
        if (typeTreeDataFile == null)
            return 0;

        try
        {
            UnityFileSystem.AddTypeTreeSourceFromFile(typeTreeDataFile.FullName);
        }
        catch (EntryPointNotFoundException)
        {
            Console.Error.WriteLine("Error: The version of UnityFileSystemApi does not support external TypeTree data files. Please use a version from Unity 6.5 or newer.");
            return 1;
        }

        return 0;
    }

    static int HandleAnalyze(
        FileSystemInfo[] paths,
        string outputFile,
        bool skipReferences,
        bool skipCrc,
        bool extractReferences,
        string searchPattern,
        bool verbose,
        bool noRecurse,
        DirectoryInfo buildHistory)
    {
        var analyzer = new AnalyzerTool();

        if (extractReferences)
        {
            Console.WriteLine("WARNING: --extract-references, -r option is deprecated (references are now extracted by default)");
        }

        return analyzer.Analyze(new AnalyzerTool.AnalyzeOptions
        {
            Paths = Array.ConvertAll(paths, p => p.FullName),
            DatabaseName = outputFile,
            SearchPattern = searchPattern,
            SkipReferences = skipReferences,
            SkipCrc = skipCrc,
            Verbose = verbose,
            NoRecursion = noRecurse,
            BuildHistoryPath = buildHistory?.FullName,
        });
    }

    static int HandleFindReferences(FileInfo databasePath, string outputFile, long? objectId, string objectName, string objectType, bool findAll, bool toStdout)
    {
        var finder = new ReferenceFinderTool();

        if ((objectId != null && objectName != null) || (objectId == null && objectName == null))
        {
            Console.Error.WriteLine("A value must be provided for either --object-id or --object-name.");
            return 1;
        }

        if (objectId != null)
        {
            return finder.FindReferences(objectId.Value, databasePath.FullName, outputFile, findAll, toStdout);
        }
        else
        {
            return finder.FindReferences(objectName, objectType, databasePath.FullName, outputFile, findAll, toStdout);
        }
    }

    static int HandleDump(TextDumperTool.DumpOptions options)
    {
        return new TextDumperTool().Dump(options);
    }
}
