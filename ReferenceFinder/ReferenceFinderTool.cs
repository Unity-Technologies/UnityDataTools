using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;

namespace UnityDataTools.ReferenceFinder
{
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
        SQLiteCommand m_GetRefsCommand;
        SQLiteCommand m_GetObjectCommand;
        List<ReferenceTreeNode> m_Roots = new List<ReferenceTreeNode>();
        HashSet<(long, string)> m_ProcessedObjects = new HashSet<(long, string)>();

        StreamWriter m_Writer;

        public int FindReferences(string objectName, string objectType, string databasePath, string outputFile, bool findAll)
        {
            var objectIds = new List<long>();
            SQLiteConnection db;

            try
            {
                db = new SQLiteConnection($"Data Source={databasePath};Version=3;Foreign Keys=False;");
                db.Open();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error opening database: {e.Message}");
                return 1;
            }

            var checkRefsTableCmd = db.CreateCommand();
            checkRefsTableCmd.CommandText = "SELECT EXISTS (SELECT 1 FROM refs)";
            var hasRefs = checkRefsTableCmd.ExecuteScalar();
            if ((long)hasRefs == 0)
            {
                Console.WriteLine("Database 'refs' table empty! Did you use the 'analyze' command with the -r option to generate the database?");
                return 1;
            }

            SQLiteCommand getObjectIds;

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

            return FindReferences(db, outputFile, objectIds, findAll);
        }

        public int FindReferences(long objectId, string databasePath, string outputFile, bool findAll)
        {
            var objectIds = new List<long>();
            SQLiteConnection db;

            try
            {
                db = new SQLiteConnection($"Data Source={databasePath};Version=3;Foreign Keys=False;");
                db.Open();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error opening database: {e.Message}");
                return 1;
            }

            objectIds.Add(objectId);

            return FindReferences(db, outputFile, objectIds, findAll);
        }

        int FindReferences(SQLiteConnection db, string outputFile, IList<long> objectIds, bool findAll)
        {
            m_Writer = new StreamWriter(outputFile);

            m_GetRefsCommand = db.CreateCommand();
            m_GetRefsCommand.CommandText = @"SELECT object, property_path, EXISTS (SELECT * FROM assets a WHERE a.object = r.object) FROM refs r WHERE referenced_object = @id";
            m_GetRefsCommand.Parameters.Add("@id", DbType.Int64);

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
	            ON r.referenced_object = s.id AND r.property_path = 'm_Script'
	            WHERE r.object = o.id),
	            '') script
            FROM object_view o
            WHERE o.id =  @id";
            m_GetObjectCommand.Parameters.Add("@id", DbType.Int64);

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

                var objectType = reader.GetString(0);
                var objectName = reader.GetString(1);
                var gameObject = reader.GetString(2);
                var script = reader.GetString(3);

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
}
