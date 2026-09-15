# Analyzer

The Analyzer is a class library that can be used to analyze the content of Unity data files such
as AssetBundles and SerializedFiles. It iterates through all the serialized objects and uses the
TypeTree to extract information about these objects (e.g. name, size, etc.)

The most common use of this library is through the [analyze](command-analyze.md)
command of the UnityDataTool.  This uses the Analyze library to generate a SQLite database.

Once generated, a tool such as the [DB Browser for SQLite](https://sqlitebrowser.org/), or the command line `sqlite3` tool, can be used to look at the content of the database.

# Example usage

See [this topic](analyze-examples.md) for examples of how to use the SQLite output of the UnityDataTool Analyze command.

# Database Schema

The database provides tables and views. The views join multiple tables together to provide useful
presentations of the data, and with a GUI-based SQLite tool it is often possible to skip writing
queries altogether and simply browse them. `object_view` is the main entry point: one row per
analyzed object, with its type, SerializedFile and archive names already resolved.

The schema is documented in full on these pages:

| Tables and views | Page |
|---|---|
| Core schema: objects, types, files, archives, references, AssetBundle and type-specific views | [Analyzer Database Schema](analyzer-schema.md) |
| `content_layout*` (ContentDirectory builds) | [ContentLayout in the Analyze Database](contentlayout-database.md) |
| `build_report*` | [BuildReport](buildreport.md) |
| `addressables_build*` | [Addressables Build Report Analysis](addressables-build-reports.md) |

A produced database also carries short notes on its non-obvious columns inside the `CREATE`
statements, so `sqlite3 Analysis.db ".schema objects"` describes the columns without needing these
pages. The pages remain the full reference.

# Advanced

## Using the library

The [AnalyzerTool](../Analyzer/AnalyzerTool.cs) class is the API entry point. The main method is called
Analyze. It is currently hard coded to write using the [SQLiteWriter](../Analyzer/SQLite/Writers/SQLiteWriter.cs),
but this approach could be extended to add support for other outputs.

Calling this method processes the provided paths, which can be individual files or directories.
Directories are scanned recursively for files matching the search pattern (unless recursion is
disabled). It will add a row in the 'objects' table for each serialized object. This table contains
basic information such as the size and the name of the object (if it has one).

## Extending the Library

The extracted information is forwarded to the [SQLiteWriter](../Analyzer/SQLite/Writers/SQLiteWriter.cs),
which writes it into a SQLite database.

The core properties that apply to all Unity Objects are extracted into the `objects` table.
However much of the most useful Analyze functionality comes by virtue of the type-specific information that is extracted for
important types like Meshes, Shaders, Texture2D and AnimationClips.  For example, when a Mesh object is encountered in a Serialized
File, then rows are added to both the `objects` table and the `meshes` table.  The meshes table contains columns that only apply to Mesh objects, for example the number of vertices, indices, bones, and channels.  The `mesh_view` is a view that joins the `objects` table with the `meshes` table, so that you can see all the properties of a Mesh object in one place.

Each supported Unity object type follows the same pattern:
* A Handler class in the SQLite/Handlers, e.g. [MeshHandler.cs](../Analyzer/SQLite/Handlers/MeshHandler.cs).
* The registration of the handler in the m_Handlers dictionary in [SerializedFileSQLiteWriter.cs](../Analyzer/SQLite/Writers/SerializedFileSQLiteWriter.cs).
* SQL statements defining extra tables and views associated with the type, e.g. [Mesh.sql](../Analyzer/Resources/Mesh.sql).
* A Reader class that uses RandomAccessReader to read properties from the serialized object, e.g. [Mesh.cs](../Analyzer/SerializedObjects/Mesh.cs).

It would be possible to extend the Analyze library to add additional columns for the existing types, or by following the same pattern to add additional types.  The [dump](command-dump.md) feature of UnityDataTool is a useful way to see the property names and other details of the serialization for a type.  Based on that information, code in the Reader class can use the RandomAccessReader to retrieve those properties to bring them into the SQLite database.

## Supporting Other File Formats

Another direction of possible extension is to support analyzing additional file formats, beyond Unity SerializedFiles.

This the approach taken to analyze Addressables Build Layout files, which are JSON files using the format defined in [BuildLayout.cs](../UnityDataModels/BuildLayout.cs).

Support for another file format could be added by deriving an additional class from SQLiteWriter and implementing a class derived from ISQLiteFileParser.  Then follow the existing code structure convention to add new Commands (derived from AbstractCommand) and Resource .sql files to establish additional tables in the database.

An example of another file format that could be useful to support, as the tool evolves, are the yaml [.manifest files](https://docs.unity3d.com/Manual/assetbundles-file-format.html), generated by BuildPipeline.BuildAssetBundles().
