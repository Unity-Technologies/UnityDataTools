using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Microsoft.Data.Sqlite;

namespace UnityDataTools.ReferenceFinder;

class ReferenceTreeNode
{
    public ReferenceTreeNode(long id)
    {
        Id = id;
    }

    public readonly long Id;
    public Dictionary<(long id, string propertyPath), ReferenceTreeNode> Children = new Dictionary<(long, string), ReferenceTreeNode>();
}

public class ReferenceFinderTool
{
    // Minimum analyze database schema version find-refs can read. The normalized refs table
    // (issue #44) is version 1; databases produced before schema versioning report 0.
    const long RequiredSchemaVersion = 1;

    SqliteCommand m_GetRefsCommand;
    SqliteCommand m_GetObjectCommand;
    List<ReferenceTreeNode> m_Roots = new List<ReferenceTreeNode>();
    HashSet<(long, string)> m_ProcessedObjects = new HashSet<(long, string)>();

    TextWriter m_Writer;

    public int FindReferences(string objectName, string objectType, string databasePath, string outputFile, bool findAll, bool toStdout = false)
    {
        var objectIds = new List<long>();
        using var db = OpenDatabase(databasePath);
        if (db == null)
        {
            return 1;
        }

        var checkRefsTableCmd = db.CreateCommand();
        checkRefsTableCmd.CommandText = "SELECT EXISTS (SELECT 1 FROM refs)";
        var hasRefs = checkRefsTableCmd.ExecuteScalar();
        if ((long)hasRefs == 0)
        {
            Console.WriteLine("Database 'refs' table empty! Make sure to not use the --skip-references option when generating the database");
            return 1;
        }

        SqliteCommand getObjectIds;

        if (objectType != null && objectType != "")
        {
            getObjectIds = db.CreateCommand();
            getObjectIds.CommandText = "SELECT id FROM object_view WHERE name = @name AND type = @type";
            getObjectIds.Parameters.AddWithValue("@type", objectType);
        }
        else
        {
            getObjectIds = db.CreateCommand();
            getObjectIds.CommandText = "SELECT id FROM object_view WHERE name = @name";
        }

        getObjectIds.Parameters.AddWithValue("@name", objectName);

        using (var reader = getObjectIds.ExecuteReader())
        {
            while (reader.Read())
            {
                objectIds.Add(reader.GetInt64(0));
            }
        }

        if (objectIds.Count == 0)
        {
            Console.WriteLine("No object found!");
            return 1;
        }

        return FindReferences(db, outputFile, objectIds, findAll, toStdout);
    }

    public int FindReferences(long objectId, string databasePath, string outputFile, bool findAll, bool toStdout = false)
    {
        var objectIds = new List<long>();
        using var db = OpenDatabase(databasePath);
        if (db == null)
        {
            return 1;
        }

        objectIds.Add(objectId);

        return FindReferences(db, outputFile, objectIds, findAll, toStdout);
    }

    // Opens the analyze database for reading. Uses SqliteConnectionStringBuilder (matching SQLiteWriter) rather than a
    // hand-written connection string, which used a legacy System.Data.SQLite keyword that Microsoft.Data.Sqlite rejects.
    static SqliteConnection OpenDatabase(string databasePath)
    {
        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
                ForeignKeys = false,
            }.ConnectionString;
            var db = new SqliteConnection(connectionString);
            db.Open();

            using (var versionCmd = db.CreateCommand())
            {
                versionCmd.CommandText = "PRAGMA user_version";
                var version = (long)versionCmd.ExecuteScalar();
                if (version < RequiredSchemaVersion)
                {
                    Console.WriteLine("The provided database uses an unsupported schema version. Re-run 'analyze' on the Unity content to regenerate it.");
                    db.Dispose();
                    return null;
                }
            }

            return db;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error opening database: {e.Message}");
            return null;
        }
    }

    int FindReferences(SqliteConnection db, string outputFile, IList<long> objectIds, bool findAll, bool toStdout)
    {
        m_Writer = toStdout ? Console.Out : new StreamWriter(outputFile);

        m_GetRefsCommand = db.CreateCommand();
        m_GetRefsCommand.CommandText = @"SELECT object, property_path, EXISTS (SELECT * FROM assets a WHERE a.object = r.object) FROM refs_view r WHERE referenced_object = @id";
        m_GetRefsCommand.Parameters.Add("@id", SqliteType.Integer);

        // Resolve the 'm_Script' property path to its id once so the per-object script lookup below
        // filters on the indexed integer column instead of scanning the property_names table.
        long scriptPathId = -1;
        using (var scriptPathCmd = db.CreateCommand())
        {
            scriptPathCmd.CommandText = "SELECT id FROM property_names WHERE name = 'm_Script'";
            var result = scriptPathCmd.ExecuteScalar();
            if (result != null)
                scriptPathId = (long)result;
        }

        m_GetObjectCommand = db.CreateCommand();
        m_GetObjectCommand.CommandText =
            @"SELECT o.type, IFNULL(o.name, '') name,
            IIF(o.game_object IS NOT NULL,
	            (SELECT go.name || ' (id=' || go.id || ')'
	            FROM objects go
	            WHERE go.id = o.game_object),
	            '') game_object,
            IIF (o.type = 'MonoBehaviour',
	            (SELECT s.name FROM objects s
	            LEFT JOIN refs r
	            ON r.referenced_object = s.id AND r.property_path = @scriptPathId
	            WHERE r.object = o.id),
	            '') script
            FROM object_view o
            WHERE o.id =  @id";
        m_GetObjectCommand.Parameters.Add("@id", SqliteType.Integer);
        m_GetObjectCommand.Parameters.AddWithValue("@scriptPathId", scriptPathId);

        for (int i = 0; i < objectIds.Count; ++i)
        {
            var command = db.CreateCommand();
            command.CommandText = "SELECT name, type, asset_bundle, serialized_file FROM object_view WHERE id = @id";
            command.Parameters.AddWithValue("@id", objectIds[i]);

            using (var reader = command.ExecuteReader())
            {
                reader.Read();

                m_Writer.WriteLine($"Reference chains to {(reader.IsDBNull(0) ? "" : reader.GetString(0))}");
                m_Writer.WriteLine($"  ID:             {objectIds[i]}");
                m_Writer.WriteLine($"  Type:           {reader.GetString(1)}");
                m_Writer.WriteLine($"  AssetBundle:    {(reader.IsDBNull(2) ? "" : reader.GetString(2))}");
                m_Writer.WriteLine($"  SerializedFile: {reader.GetString(3)}");
                m_Writer.WriteLine();
            }

            ProcessReferences(objectIds[i], findAll);

            command.CommandText = "SELECT asset_name, asset_bundle, serialized_file FROM asset_view WHERE id = @id";

            foreach (var root in m_Roots)
            {
                command.Parameters["@id"].Value = root.Id;

                using (var reader = command.ExecuteReader())
                {
                    reader.Read();

                    m_Writer.WriteLine("Found reference in:");
                    m_Writer.WriteLine(reader.GetString(0));
                    m_Writer.WriteLine($"(AssetBundle = {reader.GetString(1)}; SerializedFile = {reader.GetString(2)})");
                }

                OutputReferenceNode(root, "", 1);
                m_Writer.WriteLine();
            }

            m_Writer.WriteLine($"Analyzed {m_ProcessedObjects.Count} object(s).");
            m_Writer.WriteLine($"Found {m_Roots.Count} reference chain(s).");

            if (i < objectIds.Count - 1)
            {
                m_Writer.WriteLine();
                m_Writer.WriteLine(new string('*', 80));
                m_Writer.WriteLine();
            }
        }

        // Don't close Console.Out when writing to stdout; just flush it.
        if (toStdout)
            m_Writer.Flush();
        else
            m_Writer.Close();

        return 0;
    }

    void OutputReferenceNode(ReferenceTreeNode node, string propertyPath, int indentation)
    {
        var indent = new string(' ', indentation * 2);

        m_GetObjectCommand.Parameters["@id"].Value = node.Id;

        using (var reader = m_GetObjectCommand.ExecuteReader())
        {
            reader.Read();

            // game_object and script come from correlated subqueries that yield NULL when there is no matching row
            // (e.g. a ScriptableObject is a MonoBehaviour whose m_GameObject PPtr is 0, or a MonoBehaviour with no
            // m_Script reference), so both must be null-checked.
            var objectType = reader.GetString(0);
            var objectName = reader.GetString(1);
            var gameObject = reader.IsDBNull(2) ? "" : reader.GetString(2);
            var script = reader.IsDBNull(3) ? "" : reader.GetString(3);

            if (propertyPath != "")
            {
                m_Writer.Write(indent);
                m_Writer.WriteLine($"↓ {propertyPath}");
            }
            m_Writer.Write(indent);
            m_Writer.Write($"{objectType} (id={node.Id})");
            if (objectName != "")
                m_Writer.Write($" {objectName}");
            if (script != "")
                m_Writer.Write($" [Script = {script}]");
            if (gameObject != "")
                m_Writer.Write($" [Component of {gameObject}]");
            m_Writer.WriteLine();
        }

        foreach (var child in node.Children)
        {
            OutputReferenceNode(child.Value, child.Key.propertyPath, indentation + 1);
        }
    }

    ReferenceTreeNode ProcessReferences(long id, bool findAll)
    {
        var references = new List<(long id, string propertyPath, bool isAsset)>();

        m_GetRefsCommand.Parameters["@id"].Value = id;

        using (var reader = m_GetRefsCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                references.Add((reader.GetInt64(0), reader.GetString(1), reader.GetBoolean(2)));
            }
        }

        ReferenceTreeNode node = new ReferenceTreeNode(id);
        var wasUsed = false;

        foreach (var reference in references)
        {
            if (!m_ProcessedObjects.Contains((reference.id, findAll ? reference.propertyPath : "")))
            {
                m_ProcessedObjects.Add((reference.id, findAll ? reference.propertyPath : ""));

                if (reference.isAsset)
                {
                    var assetNode = new ReferenceTreeNode(reference.id);
                    m_Roots.Add(assetNode);
                    assetNode.Children.Add((reference.id, reference.propertyPath), node);
                    wasUsed = true;
                }
                else
                {
                    var parentNode = ProcessReferences(reference.id, findAll);
                    if (parentNode != null)
                    {
                        parentNode.Children.Add((reference.id, reference.propertyPath), node);
                        wasUsed = true;
                    }
                }
            }
        }

        return wasUsed ? node : null;
    }
}
