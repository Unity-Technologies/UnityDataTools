# Example usage of Analyze

This topic gives some examples of using the SQLite output of the UnityDataTools Analyze command.

The command line arguments to invoke Analyze are documented [here](../UnityDataTool/README.md#analyzeanalyse).

The definition of the views, and some internal details about how Analyze is implemented, can be found [here](../Analyzer/README.md).

## Running Queries from the Command line

You can find data in the SQLite database by running SQL queries.

Graphical tools such as "DB Browser" offer a way to run these queries directly from the UI based on whatever database you have open.

However often it is useful to run queries from the command line, and to incorporate queries into your scripts (bash, powershell, etc).  So some of the example on this page show the command line syntax for running simple queries.

These examples assume you have `sqlite3` available in the path for your command prompt or terminal. On Windows that means that a directory containing `sqlite3.exe` is included in your PATH environmental variable.

On Windows, sqlite3.exe is available as part of the "SQLite command line tools", published from [www.sqlite.org|www.sqlite.org].

Note: The examples in this topic assume that your database file is called `Analysis.db`, and that is in your current working directory.

## Example: Object Count

Starting things simple: running the following command on a command prompt will invoke a query will print the total number of objects in the build.

```
sqlite3 Analysis.db "SELECT COUNT(*) FROM objects;"
```

## Example: Shader Information

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

Another example query is the top 5 shaders by size.

```
sqlite3 Analysis.db ".mode column" "SELECT name, pretty_size, serialized_file FROM shader_view ORDER BY size DESC LIMIT 5;"
```

Example output (based on a build of the [megacity-metro](https://github.com/Unity-Technologies/megacity-metro) project):
```
name                                        pretty_size  serialized_file
------------------------------------------  -----------  --------------------------------
TextMeshPro/Mobile/Distance Field           191.5 KB     resources.assets
Hidden/Universal Render Pipeline/UberPost   144.3 KB     globalgamemanagers.assets
Shader Graphs/CustomLightingBuildingsB_LOD  139.6 KB     1b2fdfe013c58ffd57d7663eb8db3e60
Universal Render Pipeline/Lit               115.5 KB     1b2fdfe013c58ffd57d7663eb8db3e60
Shader Graphs/CustomLightingBuildingsB      113.4 KB     1b2fdfe013c58ffd57d7663eb8db3e60
```


## Example: Using AI tools to help write queries

This is not a tutorial on using AI tools.  However one useful tip:

Many AI tools let you provide context by uploading a file or copying text.  They are helpful for crafting SQL statements and creating scripts.  However by default they probably do not know what to expect inside a UnityDataTools SQLite database.

To provide this information you could run this command that dumps the current schema into a text file.

```
sqlite3 Analysis.db ".schema" > schema_dump.sql.txt
```

Then provide that file as context, prior to asking it to write queries based on the available tables, views and columns.  For example: *Help me write a command line calling sqlite3 for Analysis.db that will print the top 5 shaders by the size column.  It will print the name, pretty_size and serialized_file.*

## Example: Finding AssetBundles containing a certain object type

If you want to find out which AssetBundles in a build contain a certain object type you can try a query like this:

```
sqlite3 Analysis.db "SELECT DISTINCT asset_bundle FROM object_view WHERE type = 'MonoBehaviour';"
```

The above query takes advantage of the object_view which pulls together the data from multiple tables.  The equivalent query that works with the underlying tables directly would be the following:_

```
sqlite3 Analysis.db "SELECT DISTINCT ab.name AS asset_bundle FROM objects o INNER JOIN types t ON o.type = t.id INNER JOIN serialized_files sf ON o.serialized_file = sf.id LEFT JOIN asset_bundles ab ON sf.asset_bundle = ab.id WHERE t.name = 'MonoBehaviour';"
```

Note: Both MonoBehaviours and ScriptableObjects have the same serialized type "MonoBehaviour".


## Example: Finding instances of a scripting class

The previous example shows how to find all MonoBehaviours and ScriptableObjects.  But you may want to filter this based on the actual scripting class.  This is a bit more involved than the previous examples, so lets first breakdown the approach.

The serialized data for scipting class does not directly sort the class name, instead it stores a reference to a MonoScript.  The MonoScript in turn records the assembly, namespace and classname.

This is an example MonoScript from a `UnityDataTool dump` of a Serialized File:

```
ID: -5763254701832525334 (ClassID: 115) MonoScript
  m_Name (string) ReferencedUnityObjects
  m_ExecutionOrder (int) 0
  m_PropertiesHash (Hash128)
  ...
  m_ClassName (string) ReferencedUnityObjects
  m_Namespace (string) Unity.Scenes
  m_AssemblyName (string) Unity.Scenes
```

Currently UnityDataTool does implement custom handling for MonoScript objects, so we only have the m_Name field, which matches the m_ClassName field._ However so long as the class name is unique in your project this can be used to match against.

For example to list all distinct class names in the build you can run this query

```
SELECT DISTINCT name FROM object_view WHERE type = 'MonoScript';
```

The actual scripting objects of that type may be spread all through your AssetBundles (or Player build).  To find them we need to make use of the `refs` table, which records the references from each object to other objects.  If we find each MonoBehaviour object that references the MonoScript with the desired class name then we have found all instances of that class.

For example, to search for all instances of the class ReferencedUnityObjects we could run this query:

```
SELECT mb.asset_bundle, mb.serialized_file, mb.name, mb.object_id
FROM object_view mb
INNER JOIN refs r ON mb.id = r.object
INNER JOIN objects ms ON r.referenced_object = ms.id
WHERE mb.type = 'MonoBehaviour' 
  AND r.property_type = 'MonoScript'
  AND ms.name = 'ReferencedUnityObjects';
```
