using System;
using Microsoft.Data.Sqlite;
using UnityDataTools.Analyzer.SerializedObjects;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SQLite.Handlers;

public class BuildReportHandler : ISQLiteHandler
{
    private SqliteCommand m_InsertCommand;

    public void Init(SqliteConnection db)
    {
        using var command = db.CreateCommand();
        command.CommandText = Properties.Resources.BuildReport ?? throw new InvalidOperationException("BuildReport resource not found");
        command.ExecuteNonQuery();

        m_InsertCommand = db.CreateCommand();
        m_InsertCommand.CommandText = @"INSERT INTO build_reports(
            id, build_guid, platform_name, subtarget, options, asset_bundle_options,
            output_path, crc, total_size, total_time_ticks, total_errors, total_warnings,
            build_type, build_result
        ) VALUES(
            @id, @build_guid, @platform_name, @subtarget, @options, @asset_bundle_options,
            @output_path, @crc, @total_size, @total_time_ticks, @total_errors, @total_warnings,
            @build_type, @build_result
        )";

        m_InsertCommand.Parameters.Add("@id", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@build_guid", SqliteType.Text);
        m_InsertCommand.Parameters.Add("@platform_name", SqliteType.Text);
        m_InsertCommand.Parameters.Add("@subtarget", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@options", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@asset_bundle_options", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@output_path", SqliteType.Text);
        m_InsertCommand.Parameters.Add("@crc", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@total_size", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@total_time_ticks", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@total_errors", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@total_warnings", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@build_type", SqliteType.Text);
        m_InsertCommand.Parameters.Add("@build_result", SqliteType.Text);
    }

    public void Process(Context ctx, long objectId, RandomAccessReader reader, out string name, out long streamDataSize)
    {
        var buildReport = BuildReport.Read(reader);
        m_InsertCommand.Transaction = ctx.Transaction;
        m_InsertCommand.Parameters["@id"].Value = objectId;
        m_InsertCommand.Parameters["@build_guid"].Value = buildReport.BuildGuid;
        m_InsertCommand.Parameters["@platform_name"].Value = buildReport.PlatformName;
        m_InsertCommand.Parameters["@subtarget"].Value = buildReport.Subtarget;
        m_InsertCommand.Parameters["@options"].Value = buildReport.Options;
        m_InsertCommand.Parameters["@asset_bundle_options"].Value = buildReport.AssetBundleOptions;
        m_InsertCommand.Parameters["@output_path"].Value = buildReport.OutputPath;
        m_InsertCommand.Parameters["@crc"].Value = buildReport.Crc;
        m_InsertCommand.Parameters["@total_size"].Value = (long)buildReport.TotalSize;
        m_InsertCommand.Parameters["@total_time_ticks"].Value = (long)buildReport.TotalTimeTicks;
        m_InsertCommand.Parameters["@total_errors"].Value = buildReport.TotalErrors;
        m_InsertCommand.Parameters["@total_warnings"].Value = buildReport.TotalWarnings;
        m_InsertCommand.Parameters["@build_type"].Value = BuildReport.GetBuildTypeString(buildReport.BuildType);
        m_InsertCommand.Parameters["@build_result"].Value = buildReport.BuildResult;

        m_InsertCommand.ExecuteNonQuery();

        streamDataSize = 0;
        name = buildReport.Name;
    }

    public void Finalize(SqliteConnection db)
    {
    }

    void IDisposable.Dispose()
    {
        m_InsertCommand?.Dispose();
    }
}
