# Example usage of Analyze

This topic gives some examples of using the SQLite output of the UnityDataTools Analyze command.

The command line arguments to invoke Analyze are documented [here](../UnityDataTool/README.md#analyzeanalyse).

The tables and some internal details about how Analyze is implemented can be found [here](../Analyzer/README.md).

Note: The examples in this topic assume that your database file is called `Analysis.db` and that is in your current working directory.

## Command line configuration

The examples assume you have `sqlite3` available in your path for your command prompt or terminal.

On Windows, sqlite3.exe is available as part of the "SQLite command line tools", published from [www.sqlite.org|www.sqlite.org].

## Example 1: Object Count

To get the total number of objects in the build output run this command.

```
sqlite3 Analysis.db "SELECT COUNT(*) FROM objects;"
```

## Example 2: Shader keywords

shader_view has a lot of useful information about shaders.  For example to see the list of keywords for a particular shader, try the following command.  This should work with both Powershell and Bash:
```
sqlite3 Analysis.db ".mode column" "SELECT keywords FROM shader_view WHERE name = 'Sprites/Default';"
```

Example output:

```
keywords
-------------------
PIXELSNAP_ON,
INSTANCING_ON,
ETC1_EXTERNAL_ALPHA
```

Another example query is the top 10 shaders by size.

```
sqlite3 Analysis.db ".mode column" "SELECT name, pretty_size, serialized_file FROM shader_view ORDER BY size DESC LIMIT 10;"
```

Example output (using a build of the [megacity-metro](https://github.com/Unity-Technologies/megacity-metro) project):
```
name                                        pretty_size  serialized_file
------------------------------------------  -----------  --------------------------------
TextMeshPro/Mobile/Distance Field           191.5 KB     resources.assets
Hidden/Universal Render Pipeline/UberPost   144.3 KB     globalgamemanagers.assets
Shader Graphs/CustomLightingBuildingsB_LOD  139.6 KB     1b2fdfe013c58ffd57d7663eb8db3e60
Universal Render Pipeline/Lit               115.5 KB     1b2fdfe013c58ffd57d7663eb8db3e60
Shader Graphs/CustomLightingBuildingsB      113.4 KB     1b2fdfe013c58ffd57d7663eb8db3e60
Shader Graphs/CustomLightingBuildings        82.1 KB     1b2fdfe013c58ffd57d7663eb8db3e60
Shader Graphs/IlluminatedSignsArray          71.7 KB     1b2fdfe013c58ffd57d7663eb8db3e60
Shader Graphs/CustomLightingGlow             71.6 KB     1b2fdfe013c58ffd57d7663eb8db3e60
TextMeshPro/Mobile/Distance Field            70.4 KB     resources.assets
Shader Graphs/IlluminatedSigns               67.2 KB     1b2fdfe013c58ffd57d7663eb8db3e60
```


## Example: Using AI tools to help write queries

This is not a tutorial on using AI tools.  However one useful tip:

Many AI tools let you provide context by uploading a file or copying text.  They are helpful for crafting SQL statements and creating scripts.  However by default they probably do not know what to expect inside a UnityDataTools SQLite database.

To provide this information you could run this command that dumps the current schema into a text file.

```
sqlite3 Analysis.db ".schema" > schema_dump.sql.txt
```

Then provide that file as context, prior to asking it to write queries based on the available tables, views and columns.  For example: *Help me write a command line calling sqlite3 for Analysis.db that will print the top 10 shaders by the size column.  It will print the name, pretty_size and serialized_file*