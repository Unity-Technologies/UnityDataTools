# UnityDataTools

The UnityDataTool is a command line tool and showcase of the UnityFileSystemApi native dynamic library.
The main purpose is for analysis of the content of Unity data files, for example AssetBundles and
Player content.

The [command line tool](./Documentation/unitydatatool.md) runs directly on Unity data files, without requiring the Editor to be running.  It covers most functionality of the Unity tools WebExtract and binary2text, with better performance.  And it adds a lot of additional functionality, for example the ability to create a SQLite database for detailed analysis of build content. See [examples](./Documentation/analyze-examples.md) and [comparing builds](./Documentation/comparing-builds.md) for examples of how to use the command line tool.

It is designed to scale for large build outputs and has been used to fine-tune big Unity-based games.

The tool also provides comprehensive analysis of **Unity Addressables build reports**, automatically detecting and parsing Addressables JSON build outputs to extract detailed information about bundles, assets, dependencies, file sizes, and build performance metrics. See the [Addressables Build Report Analysis documentation](./Documentation/addressables-build-reports.md) for complete details.

The command line tool uses the UnityFileSystemApi library to access the content of Unity Archives and Serialized files, which are Unity's primary binary formats. This repository also serves as a reference for how this library could be used as part of incorporating functionality into your own tools.

## Repository content

The repository contains the following items:
* [UnityDataTool](Documentation/unitydatatool.md): a command-line tool providing access to the Analyzer, TextDumper and other class libraries.
* [Analyzer](Documentation/analyzer.md): a class library that can be used to extract key information
  from Unity data files and output it into a SQLite database.
* [TextDumper](Documentation/textdumper.md): a class library that can be used to dump SerializedFiles into
  a human-readable format (similar to binary2text).
* [ReferenceFinder](Documentation/referencefinder.md): a class library that can be used to find
  reference chains from objects to other objects using a database created by the Analyzer
* UnityFileSystem: source code and binaries of a .NET class library exposing the functionalities or the
  UnityFileSystemApi native library.
* UnityFileSystem.Tests: test suite for the UnityFileSystem library.
* UnityFileSystemTestData: the Unity project used to generate the test data.
* TestCommon: a helper library used by the test projects.

## Downloads

Prebuilt Windows and Mac builds are available in the "Actions" tab. Each update to the main branch triggers a new build.

To use:
1. Download and unzip the build for your platform.
2. Run UnityDataTool from the extracted location, or add it to your system PATH.

Refer to the [commit history](https://github.com/Unity-Technologies/UnityDataTools/commits/main/) to see the recent improvements to the tool.

## Getting UnityFileSystemApi

UnityFileSystemApi is distributed in the Tools folder of the Unity Editor (from version 2022.1.0a14). The UnityDataTools repository includes a Windows, Mac, and Linux copy of the library in the `UnityFileSystem/` directory. 

The library is backward compatible and can read data files from most Unity versions, so typically the version that is provided with UnityDataTools can be used "as is".

To analyze data using the library from a specific version of the Unity Editor, copy the appropriate UnityFileSystemApi file from your Unity Editor installation (`{UnityEditor}/Data/Tools/`) to `UnityDataTool/UnityFileSystem/` prior to building:

The file name is as follows:

- Windows: `UnityFileSystemApi.dll`
- Mac: `UnityFileSystemApi.dylib`
- Linux: `UnityFileSystemApi.so`

## How to Build

1. Clone or download this repository.
2. Install the [.NET 9.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0).
3. (Optional) Overwrite the checked in version of the UnityFileSystemApi library with one from your Unity Editor installation, as described above.
4. Build using `dotnet build -c Release` or your preferred IDE (tested with Visual Studio and Rider on Windows/Mac).

On Windows, the executable is written to `UnityDataTool\bin\Release\net9.0`. Add this location to your system PATH for convenient access.

See the [command-line tool documentation](./Documentation/unitydatatool.md) for usage instructions.

## Purpose of UnityFileSystemApi

UnityFileSystemApi exposes the core functionality of WebExtract and binary2text to open and read the Unity Archive and Serialized File formats, exposed as a flexible, performant library. It enables custom tools for binary2text-like output and efficient SQLite database generation.

## Origins

This tool is the evolution of the [AssetBundle Analyzer](https://github.com/faelenor/asset-bundle-analyzer)
written by [Francis Pagé](https://www.github.com/faelenor).

That project was the first to introduce the SQLite database analysis of Unity build output to address
the difficulty of diagnosing build issues through the raw binary2text output, which is large and difficult to navigate.

The AssetBundle Analyzer was quite successful, but it has several issues. It
is extremely slow as it runs WebExtract and binary2text on all the AssetBundles of a project and
has to parse very large text files. It can also easily fail because the syntax used by binary2text
is not standard and can even be impossible to parse in some occasions.

UnityDataTools and UnityFileSystemApi were created to overcome these issues, providing fast, reliable access to Unity data files and enabling advanced analysis.

## Disclaimer

This project is provided on an "as-is" basis and is not officially supported by Unity. It is an experimental tool and example of UnityFileSystemApi usage. Bug reports and pull requests are welcome, but support is not guaranteed.
