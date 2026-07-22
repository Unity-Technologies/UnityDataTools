using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using UnityDataTools.Analyzer.SerializedObjects;
using UnityDataTools.FileSystem;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SQLite.Handlers;

// Writes the ContentSummary object (Unity 6.6+). There is at most one per serialized file, so this
// is simpler than PackedAssetsHandler, but it follows the same on-demand schema pattern: the
// build_report_content_* tables and views are created lazily on the first ContentSummary object.
public class ContentSummaryHandler : ISQLiteHandler
{
    private SqliteConnection m_Database;
    private bool m_SchemaCreated;
    private SqliteCommand m_InsertSummaryCommand;
    private SqliteCommand m_InsertTypeStatCommand;
    private SqliteCommand m_InsertAssetStatCommand;
    private SqliteCommand m_InsertTypeCommand;

    // Type ids already added to the types table, to skip redundant INSERTs across all reports.
    private HashSet<int> m_InsertedTypes = new();

    public void Init(SqliteConnection db)
    {
        m_Database = db;

        m_InsertSummaryCommand = db.CreateCommand();
        m_InsertSummaryCommand.CommandText = @"INSERT INTO build_report_content_summary(
            id, serialized_file_size, reused_serialized_file_size, resource_data_size, header_size,
            serialized_file_count, reused_serialized_file_count, resource_file_count, object_count
        ) VALUES(
            @id, @serialized_file_size, @reused_serialized_file_size, @resource_data_size, @header_size,
            @serialized_file_count, @reused_serialized_file_count, @resource_file_count, @object_count
        )";
        m_InsertSummaryCommand.Parameters.Add("@id", SqliteType.Integer);
        m_InsertSummaryCommand.Parameters.Add("@serialized_file_size", SqliteType.Integer);
        m_InsertSummaryCommand.Parameters.Add("@reused_serialized_file_size", SqliteType.Integer);
        m_InsertSummaryCommand.Parameters.Add("@resource_data_size", SqliteType.Integer);
        m_InsertSummaryCommand.Parameters.Add("@header_size", SqliteType.Integer);
        m_InsertSummaryCommand.Parameters.Add("@serialized_file_count", SqliteType.Integer);
        m_InsertSummaryCommand.Parameters.Add("@reused_serialized_file_count", SqliteType.Integer);
        m_InsertSummaryCommand.Parameters.Add("@resource_file_count", SqliteType.Integer);
        m_InsertSummaryCommand.Parameters.Add("@object_count", SqliteType.Integer);

        m_InsertTypeStatCommand = db.CreateCommand();
        m_InsertTypeStatCommand.CommandText = @"INSERT INTO build_report_content_type_stats(
            content_summary_id, type, size, object_count, resource_count
        ) VALUES(
            @content_summary_id, @type, @size, @object_count, @resource_count
        )";
        m_InsertTypeStatCommand.Parameters.Add("@content_summary_id", SqliteType.Integer);
        m_InsertTypeStatCommand.Parameters.Add("@type", SqliteType.Integer);
        m_InsertTypeStatCommand.Parameters.Add("@size", SqliteType.Integer);
        m_InsertTypeStatCommand.Parameters.Add("@object_count", SqliteType.Integer);
        m_InsertTypeStatCommand.Parameters.Add("@resource_count", SqliteType.Integer);

        m_InsertAssetStatCommand = db.CreateCommand();
        m_InsertAssetStatCommand.CommandText = @"INSERT INTO build_report_content_asset_stats(
            content_summary_id, source_asset_guid, source_asset_path, size, object_count, resource_count
        ) VALUES(
            @content_summary_id, @source_asset_guid, @source_asset_path, @size, @object_count, @resource_count
        )";
        m_InsertAssetStatCommand.Parameters.Add("@content_summary_id", SqliteType.Integer);
        m_InsertAssetStatCommand.Parameters.Add("@source_asset_guid", SqliteType.Text);
        m_InsertAssetStatCommand.Parameters.Add("@source_asset_path", SqliteType.Text);
        m_InsertAssetStatCommand.Parameters.Add("@size", SqliteType.Integer);
        m_InsertAssetStatCommand.Parameters.Add("@object_count", SqliteType.Integer);
        m_InsertAssetStatCommand.Parameters.Add("@resource_count", SqliteType.Integer);

        // Populate the shared types table (INSERT OR IGNORE) so the type-stats view can show type
        // names even when the build output and its TypeTrees are not analyzed alongside the report.
        m_InsertTypeCommand = db.CreateCommand();
        m_InsertTypeCommand.CommandText = "INSERT OR IGNORE INTO types(id, name) VALUES(@id, @name)";
        m_InsertTypeCommand.Parameters.Add("@id", SqliteType.Integer);
        m_InsertTypeCommand.Parameters.Add("@name", SqliteType.Text);
    }

    private void EnsureSchema(SqliteTransaction transaction)
    {
        if (m_SchemaCreated)
            return;

        using var command = m_Database.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Properties.Resources.ContentSummary ?? throw new InvalidOperationException("ContentSummary resource not found");
        command.ExecuteNonQuery();

        m_SchemaCreated = true;
    }

    public void Process(Context ctx, long objectId, RandomAccessReader reader, out string name, out long streamDataSize)
    {
        EnsureSchema(ctx.Transaction);

        var contentSummary = ContentSummary.Read(reader);

        m_InsertSummaryCommand.Transaction = ctx.Transaction;
        m_InsertSummaryCommand.Parameters["@id"].Value = objectId;
        m_InsertSummaryCommand.Parameters["@serialized_file_size"].Value = (long)contentSummary.SerializedFileSize;
        m_InsertSummaryCommand.Parameters["@reused_serialized_file_size"].Value = (long)contentSummary.ReusedSerializedFileSize;
        m_InsertSummaryCommand.Parameters["@resource_data_size"].Value = (long)contentSummary.ResourceDataSize;
        m_InsertSummaryCommand.Parameters["@header_size"].Value = (long)contentSummary.HeaderSize;
        m_InsertSummaryCommand.Parameters["@serialized_file_count"].Value = contentSummary.SerializedFileCount;
        m_InsertSummaryCommand.Parameters["@reused_serialized_file_count"].Value = contentSummary.ReusedSerializedFileCount;
        m_InsertSummaryCommand.Parameters["@resource_file_count"].Value = contentSummary.ResourceFileCount;
        m_InsertSummaryCommand.Parameters["@object_count"].Value = contentSummary.ObjectCount;
        m_InsertSummaryCommand.ExecuteNonQuery();

        foreach (var typeStat in contentSummary.TypeStats)
        {
            if (m_InsertedTypes.Add(typeStat.Type) &&
                TypeIdRegistry.TryGetTypeName(typeStat.Type, out var typeName))
            {
                m_InsertTypeCommand.Transaction = ctx.Transaction;
                m_InsertTypeCommand.Parameters["@id"].Value = typeStat.Type;
                m_InsertTypeCommand.Parameters["@name"].Value = typeName;
                m_InsertTypeCommand.ExecuteNonQuery();
            }

            m_InsertTypeStatCommand.Transaction = ctx.Transaction;
            m_InsertTypeStatCommand.Parameters["@content_summary_id"].Value = objectId;
            m_InsertTypeStatCommand.Parameters["@type"].Value = typeStat.Type;
            m_InsertTypeStatCommand.Parameters["@size"].Value = (long)typeStat.Size;
            m_InsertTypeStatCommand.Parameters["@object_count"].Value = typeStat.ObjectCount;
            m_InsertTypeStatCommand.Parameters["@resource_count"].Value = typeStat.ResourceCount;
            m_InsertTypeStatCommand.ExecuteNonQuery();
        }

        foreach (var assetStat in contentSummary.AssetStats)
        {
            m_InsertAssetStatCommand.Transaction = ctx.Transaction;
            m_InsertAssetStatCommand.Parameters["@content_summary_id"].Value = objectId;
            m_InsertAssetStatCommand.Parameters["@source_asset_guid"].Value = assetStat.SourceAssetGUID;
            m_InsertAssetStatCommand.Parameters["@source_asset_path"].Value = assetStat.SourceAssetPath;
            m_InsertAssetStatCommand.Parameters["@size"].Value = (long)assetStat.Size;
            m_InsertAssetStatCommand.Parameters["@object_count"].Value = assetStat.ObjectCount;
            m_InsertAssetStatCommand.Parameters["@resource_count"].Value = assetStat.ResourceCount;
            m_InsertAssetStatCommand.ExecuteNonQuery();
        }

        streamDataSize = 0;
        name = string.Empty;
    }

    public void Finalize(SqliteConnection db)
    {
    }

    void IDisposable.Dispose()
    {
        m_InsertSummaryCommand?.Dispose();
        m_InsertTypeStatCommand?.Dispose();
        m_InsertAssetStatCommand?.Dispose();
        m_InsertTypeCommand?.Dispose();
    }
}
