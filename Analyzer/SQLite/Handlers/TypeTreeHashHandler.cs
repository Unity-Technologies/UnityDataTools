using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using UnityDataTools.FileSystem;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SQLite.Handlers;

internal class TypeTreeHashData
{
    public int Count;
    public int SerializedSize;
    public string Category;
    public int TypeId;
    public string QualifiedName;
}

public class TypeTreeHashHandler : SQLiteHandlerBase
{
    private Dictionary<string, TypeTreeHashData> m_TypeTreeHashes = new();
    private SqliteCommand m_InsertCommand;

    public override void Init(SqliteConnection db)
    {
        // Table is already created in Init.sql
        m_InsertCommand = db.CreateCommand();
        m_InsertCommand.CommandText = "INSERT INTO typetree_hashes (hash, count, serialized_size, category, type_id, qualified_name) VALUES (@hash, @count, @serialized_size, @category, @type_id, @qualified_name)";
        m_InsertCommand.Parameters.Add("@hash", SqliteType.Text);
        m_InsertCommand.Parameters.Add("@count", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@serialized_size", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@category", SqliteType.Text);
        m_InsertCommand.Parameters.Add("@type_id", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@qualified_name", SqliteType.Text);
    }

    public override void ProcessSerializedFile(SerializedFile sf, SqliteTransaction transaction)
    {
        int typeTreeCount = sf.GetTypeTreeCount();

        for (int i = 0; i < typeTreeCount; i++)
        {
            TypeTreeInfo info = sf.GetTypeTreeInfo(i);

            // Convert hash uint32[4] to hex string
            string hashString = $"{info.Hash0:x8}{info.Hash1:x8}{info.Hash2:x8}{info.Hash3:x8}";

            // Build qualified name
            string qualifiedName;
            if (info.Category == TypeTreeCategory.RefType)
            {
                // For RefType: namespace.class (from assembly)
                if (!string.IsNullOrEmpty(info.NamespaceName))
                    qualifiedName = $"{info.NamespaceName}.{info.ClassName}";
                else
                    qualifiedName = info.ClassName;
            }
            else
            {
                // For ObjectType: use the Unity type name from TypeId
                qualifiedName = info.ClassName;
            }

            if (m_TypeTreeHashes.TryGetValue(hashString, out var existingData))
            {
                // Already seen this hash - increment count
                existingData.Count++;
            }
            else
            {
                // First time seeing this hash - add to dictionary
                m_TypeTreeHashes[hashString] = new TypeTreeHashData
                {
                    Count = 1,
                    SerializedSize = info.SerializedSize,
                    Category = info.Category.ToString(),
                    TypeId = info.TypeId,
                    QualifiedName = qualifiedName
                };
            }
        }
    }

    public override void Finalize(SqliteConnection db)
    {
        // Write all TypeTree hash data to database
        using (var transaction = db.BeginTransaction())
        {
            m_InsertCommand.Transaction = transaction;
            foreach (var kvp in m_TypeTreeHashes)
            {
                m_InsertCommand.Parameters["@hash"].Value = kvp.Key;
                m_InsertCommand.Parameters["@count"].Value = kvp.Value.Count;
                m_InsertCommand.Parameters["@serialized_size"].Value = kvp.Value.SerializedSize;
                m_InsertCommand.Parameters["@category"].Value = kvp.Value.Category;
                m_InsertCommand.Parameters["@type_id"].Value = kvp.Value.TypeId;
                m_InsertCommand.Parameters["@qualified_name"].Value = kvp.Value.QualifiedName;
                m_InsertCommand.ExecuteNonQuery();
            }
            transaction.Commit();
        }
    }

    public override void Dispose()
    {
        m_InsertCommand?.Dispose();
    }
}
