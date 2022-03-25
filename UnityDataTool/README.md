# UnityDataTool

The UnityDataTool provides a set of commands related to Unity data files. The tool is invoked from the comman line like this:

`UnityDataTool [command] [command options]`

# Commands

## analyze/analyse

This command extracts useful information from AssetBundles and SerializedFiles and dumps the results in a SQLite database. The files must include a TypeTree, otherwise it the tool will fail. It is an improved version of the [AssetBundle Analyzer](https://github.com/faelenor/asset-bundle-analyzer).

The command takes the path of the folder containing the files to analyze as argument. It also provides the following options:
* -o, --output-file \<database-filename\>: filename of the database that will be created, the default is database.db.
* -r, --extract-references: extract all references (PPtrs), the default is off as it significantly increases the processing time and the size of the database.
* -p, --search-pattern \<pattern\>: search pattern used to determine which files are asset bundles, the default is \*.

Refer to this [documentation](../Analyzer/README.md#How-to-use-the-database) for more information about the output database structure.

## find-refs

This command finds reference chains leading to specific objects. It requires a database that was created by the 'analyze' command with the --extract-references option. It takes an object id or name as input and will find reference chains originating from a root asset to the specified object(s). A root asset is an asset that was explicitely added to an AssetBundle at build time. It can be particularly useful to determine why an asset was included (and potentially more than once) in a build.

The command takes the path of the database as argument. It also provides the following options:
* -i, --object_id \<id\>: id of the object to analyze
* -n, --object-name \<name\>: name of the objects to analyze (it can be useful to find the origin of duplicates as they will have different ids but the same name)
* -t, --object-type \<type\>: type of the objects to analyze, used to filter objects when using the -n option
* -o, --output-file \<filename\>: name of the output file
* -a, --find-all: this will force a search for all reference chains originating from the same root object instead of stopping at the first one. It may take a lot more time.
Note that either --object-id or --object-name must be provided.

Refer to this [documentation](../ReferenceFinder/README.md#How-to-interpret-the-output-file) for more information about the content of the output file.

## dump

This command dumps the content of a SerializedFile into a file of the selected format. It currently only supports the 'text' format, which is similar to the binary2text output format.

The command takes the path of the file to dump as argument. It also provides the following options:
* -f, --output-format \<format\>: output format, default is 'text'.
* -s, --skip-large-arrays: the content of basic data type arrays with a large number of elements won't be dumped.

Refer to this [documentation](../TextDumper/README.md#How-to-interpret-the-output-files) for more information about the content of the output file.

## archive

The archive command offers a set of archive-related sub-commands.

**extract**
This sub-command extracts the content of an archive. It takes the archive path as argument.

**list**
This sub-command lists the content of an archive. It takes the archive path as argument.
