using System;
using Microsoft.Data.Sqlite;
using UnityDataTools.Analyzer.SerializedObjects;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SQLite.Handlers;

public class BuildReportHandler : ISQLiteHandler
{
    private SqliteConnection m_Database;
    private bool m_SchemaCreated;
    private SqliteCommand m_InsertCommand;
    private SqliteCommand m_InsertFileCommand;
    private SqliteCommand m_InsertArchiveContentCommand;

    public void Init(SqliteConnection db)
    {
        // The build_report tables are created lazily, on the first BuildReport object (see
        // EnsureSchema), so a database analyzed without any build report is not cluttered with
        // empty build_report tables.
        m_Database = db;

        m_InsertCommand = db.CreateCommand();
        m_InsertCommand.CommandText = @"INSERT INTO build_reports(
            id, build_type, build_result, platform_name, subtarget, start_time, end_time, total_time_seconds,
            total_size, build_guid, total_errors, total_warnings, options, asset_bundle_options,
            output_path, crc, build_name, build_content_options, build_session_guid, build_manifest_hash,
            build_profile_path, build_profile_guid, data_path
        ) VALUES(
            @id, @build_type, @build_result, @platform_name, @subtarget, @start_time, @end_time, @total_time_seconds,
            @total_size, @build_guid, @total_errors, @total_warnings, @options, @asset_bundle_options,
            @output_path, @crc, @build_name, @build_content_options, @build_session_guid, @build_manifest_hash,
            @build_profile_path, @build_profile_guid, @data_path
        )";

        m_InsertCommand.Parameters.Add("@id", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@build_type", SqliteType.Text);
        m_InsertCommand.Parameters.Add("@build_result", SqliteType.Text);
        m_InsertCommand.Parameters.Add("@platform_name", SqliteType.Text);
        m_InsertCommand.Parameters.Add("@subtarget", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@start_time", SqliteType.Text);
        m_InsertCommand.Parameters.Add("@end_time", SqliteType.Text);
        m_InsertCommand.Parameters.Add("@total_time_seconds", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@total_size", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@build_guid", SqliteType.Text);
        m_InsertCommand.Parameters.Add("@total_errors", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@total_warnings", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@options", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@asset_bundle_options", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@output_path", SqliteType.Text);
        m_InsertCommand.Parameters.Add("@crc", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@build_name", SqliteType.Text);
        m_InsertCommand.Parameters.Add("@build_content_options", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@build_session_guid", SqliteType.Text);
        m_InsertCommand.Parameters.Add("@build_manifest_hash", SqliteType.Text);
        m_InsertCommand.Parameters.Add("@build_profile_path", SqliteType.Text);
        m_InsertCommand.Parameters.Add("@build_profile_guid", SqliteType.Text);
        m_InsertCommand.Parameters.Add("@data_path", SqliteType.Text);

        m_InsertFileCommand = db.CreateCommand();
        m_InsertFileCommand.CommandText = @"INSERT INTO build_report_files(
            build_report_id, file_index, path, role, size
        ) VALUES(
            @build_report_id, @file_index, @path, @role, @size
        )";

        m_InsertFileCommand.Parameters.Add("@build_report_id", SqliteType.Integer);
        m_InsertFileCommand.Parameters.Add("@file_index", SqliteType.Integer);
        m_InsertFileCommand.Parameters.Add("@path", SqliteType.Text);
        m_InsertFileCommand.Parameters.Add("@role", SqliteType.Text);
        m_InsertFileCommand.Parameters.Add("@size", SqliteType.Integer);

        m_InsertArchiveContentCommand = db.CreateCommand();
        m_InsertArchiveContentCommand.CommandText = @"INSERT INTO build_report_archive_contents(
            build_report_id, archive, archive_content
        ) VALUES(
            @build_report_id, @archive, @archive_content
        )";

        m_InsertArchiveContentCommand.Parameters.Add("@build_report_id", SqliteType.Integer);
        m_InsertArchiveContentCommand.Parameters.Add("@archive", SqliteType.Text);
        m_InsertArchiveContentCommand.Parameters.Add("@archive_content", SqliteType.Text);
    }

    // Creates the build_report schema on first use. Runs inside the current file's transaction, so
    // it is committed together with the rows it is about to receive.
    private void EnsureSchema(SqliteTransaction transaction)
    {
        if (m_SchemaCreated)
            return;

        using var command = m_Database.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Properties.Resources.BuildReport ?? throw new InvalidOperationException("BuildReport resource not found");
        command.ExecuteNonQuery();

        m_SchemaCreated = true;
    }

    public void Process(Context ctx, long objectId, RandomAccessReader reader, out string name, out long streamDataSize)
    {
        EnsureSchema(ctx.Transaction);

        var buildReport = BuildReport.Read(reader);
        m_InsertCommand.Transaction = ctx.Transaction;
        m_InsertCommand.Parameters["@id"].Value = objectId;
        m_InsertCommand.Parameters["@build_type"].Value = BuildReport.GetBuildTypeString(buildReport.BuildType);
        m_InsertCommand.Parameters["@build_result"].Value = buildReport.BuildResult;
        m_InsertCommand.Parameters["@platform_name"].Value = buildReport.PlatformName;
        m_InsertCommand.Parameters["@subtarget"].Value = buildReport.Subtarget;
        m_InsertCommand.Parameters["@start_time"].Value = buildReport.StartTime;
        m_InsertCommand.Parameters["@end_time"].Value = buildReport.EndTime;
        m_InsertCommand.Parameters["@total_time_seconds"].Value = buildReport.TotalTimeSeconds;
        m_InsertCommand.Parameters["@total_size"].Value = (long)buildReport.TotalSize;
        m_InsertCommand.Parameters["@build_guid"].Value = buildReport.BuildGuid;
        m_InsertCommand.Parameters["@total_errors"].Value = buildReport.TotalErrors;
        m_InsertCommand.Parameters["@total_warnings"].Value = buildReport.TotalWarnings;
        m_InsertCommand.Parameters["@options"].Value = buildReport.Options;
        m_InsertCommand.Parameters["@asset_bundle_options"].Value = buildReport.AssetBundleOptions;
        m_InsertCommand.Parameters["@output_path"].Value = buildReport.OutputPath;
        m_InsertCommand.Parameters["@crc"].Value = buildReport.Crc;
        m_InsertCommand.Parameters["@build_name"].Value = (object)buildReport.BuildName ?? DBNull.Value;
        m_InsertCommand.Parameters["@build_content_options"].Value = (object)buildReport.BuildContentOptions ?? DBNull.Value;
        m_InsertCommand.Parameters["@build_session_guid"].Value = (object)buildReport.BuildSessionGuid ?? DBNull.Value;
        m_InsertCommand.Parameters["@build_manifest_hash"].Value = (object)buildReport.BuildManifestHash ?? DBNull.Value;
        m_InsertCommand.Parameters["@build_profile_path"].Value = (object)buildReport.BuildProfilePath ?? DBNull.Value;
        m_InsertCommand.Parameters["@build_profile_guid"].Value = (object)buildReport.BuildProfileGuid ?? DBNull.Value;
        m_InsertCommand.Parameters["@data_path"].Value = (object)buildReport.DataPath ?? DBNull.Value;

        m_InsertCommand.ExecuteNonQuery();

        // Insert files
        foreach (var file in buildReport.Files)
        {
            m_InsertFileCommand.Transaction = ctx.Transaction;
            m_InsertFileCommand.Parameters["@build_report_id"].Value = objectId;
            m_InsertFileCommand.Parameters["@file_index"].Value = file.Id;
            m_InsertFileCommand.Parameters["@path"].Value = file.Path;
            m_InsertFileCommand.Parameters["@role"].Value = file.Role;
            m_InsertFileCommand.Parameters["@size"].Value = (long)file.Size;
            m_InsertFileCommand.ExecuteNonQuery();
        }

        // Insert archive contents mapping
        foreach (var mapping in buildReport.fileListArchiveHelper.internalNameToArchiveMapping)
        {
            m_InsertArchiveContentCommand.Transaction = ctx.Transaction;
            m_InsertArchiveContentCommand.Parameters["@build_report_id"].Value = objectId;
            m_InsertArchiveContentCommand.Parameters["@archive"].Value = mapping.Value;
            m_InsertArchiveContentCommand.Parameters["@archive_content"].Value = mapping.Key;
            m_InsertArchiveContentCommand.ExecuteNonQuery();
        }

        streamDataSize = 0;
        name = buildReport.Name;
    }

    public void Finalize(SqliteConnection db)
    {
    }

    void IDisposable.Dispose()
    {
        m_InsertCommand?.Dispose();
        m_InsertFileCommand?.Dispose();
        m_InsertArchiveContentCommand?.Dispose();
    }
}
