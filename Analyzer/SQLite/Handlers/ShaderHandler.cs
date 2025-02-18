using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SQLite.Handlers;

public class ShaderHandler : ISQLiteHandler
{
    private SqliteCommand m_InsertCommand;
    private SqliteCommand m_InsertSubProgramCommand;
    private SqliteCommand m_InsertKeywordCommand;
    private SqliteCommand m_InsertSubProgramKeywordsCommand;
        
    static long s_SubProgramId = 0;
    static Dictionary<string, int> s_GlobalKeywords = new();

    public void Init(SqliteConnection db)
    {
        s_SubProgramId = 0;
        s_GlobalKeywords.Clear();
            
        using var command = db.CreateCommand();
        command.CommandText = Properties.Resources.Shader;
        command.ExecuteNonQuery();

        m_InsertCommand = db.CreateCommand();
        m_InsertCommand.CommandText = "INSERT INTO shaders(id, decompressed_size, unique_programs) VALUES(@id, @decompressed_size, @unique_programs)";
        m_InsertCommand.Parameters.Add("@id", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@decompressed_size", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@unique_programs", SqliteType.Integer);

        m_InsertSubProgramCommand = db.CreateCommand();
        m_InsertSubProgramCommand.CommandText = "INSERT INTO shader_subprograms(id, shader, sub_shader, pass, pass_name, sub_program, hw_tier, shader_type, api) VALUES(@id, @shader, @sub_shader, @pass, @pass_name, @sub_program, @hw_tier, @shader_type, @api)";
        m_InsertSubProgramCommand.Parameters.Add("@id", SqliteType.Integer);
        m_InsertSubProgramCommand.Parameters.Add("@shader", SqliteType.Integer);
        m_InsertSubProgramCommand.Parameters.Add("@sub_shader", SqliteType.Integer);
        m_InsertSubProgramCommand.Parameters.Add("@pass", SqliteType.Integer);
        m_InsertSubProgramCommand.Parameters.Add("@pass_name", SqliteType.Text);
        m_InsertSubProgramCommand.Parameters.Add("@sub_program", SqliteType.Integer);
        m_InsertSubProgramCommand.Parameters.Add("@hw_tier", SqliteType.Integer);
        m_InsertSubProgramCommand.Parameters.Add("@shader_type", SqliteType.Text);
        m_InsertSubProgramCommand.Parameters.Add("@api", SqliteType.Integer);

        m_InsertKeywordCommand = db.CreateCommand();
        m_InsertKeywordCommand.CommandText = "INSERT INTO shader_keywords(id, keyword) VALUES(@id, @keyword)";
        m_InsertKeywordCommand.Parameters.Add("@id", SqliteType.Integer);
        m_InsertKeywordCommand.Parameters.Add("@keyword", SqliteType.Text);

        m_InsertSubProgramKeywordsCommand = db.CreateCommand();
        m_InsertSubProgramKeywordsCommand.CommandText = "INSERT INTO shader_subprogram_keywords(subprogram_id, keyword_id) VALUES (@subprogram_id, @keyword_id)";
        m_InsertSubProgramKeywordsCommand.Parameters.Add("@subprogram_id", SqliteType.Integer);
        m_InsertSubProgramKeywordsCommand.Parameters.Add("@keyword_id", SqliteType.Integer);
    }

    public void Process(Context ctx, long objectId, RandomAccessReader reader, out string name, out long streamDataSize)
    {
        var shader = SerializedObjects.Shader.Read(reader);
        var uniquePrograms = new HashSet<uint>();
        var localToGlobalKeywords = new Dictionary<int, int>();

        for (int i = 0; i < shader.Keywords.Count; ++i)
        {
            var keyword = shader.Keywords[i];
            var id = GetKeywordId(keyword,ctx.Transaction);
            localToGlobalKeywords[i] = id;
        }

        m_InsertCommand.Transaction = ctx.Transaction;
        m_InsertCommand.Parameters["@id"].Value = objectId;

        for (int subShaderIndex = 0; subShaderIndex < shader.SubShaders.Count; ++subShaderIndex)
        {
            var subShader = shader.SubShaders[subShaderIndex];

            m_InsertSubProgramCommand.Parameters["@sub_shader"].Value = subShaderIndex;

            for (int passIndex = 0; passIndex < subShader.Passes.Count; ++passIndex)
            {
                var pass = subShader.Passes[passIndex];
                m_InsertSubProgramCommand.Transaction = ctx.Transaction;    
                m_InsertSubProgramCommand.Parameters["@shader"].Value = objectId;
                m_InsertSubProgramCommand.Parameters["@pass"].Value = passIndex;
                m_InsertSubProgramCommand.Parameters["@pass_name"].Value = pass.Name;

                foreach (var kv in pass.Programs)
                {
                    var progType = kv.Key;
                    var programs = kv.Value;
                        
                    m_InsertSubProgramCommand.Parameters["@shader_type"].Value = progType;

                    for (int programIndex = 0; programIndex < programs.Count; ++programIndex)
                    {
                        var program = programs[programIndex];

                        uniquePrograms.Add(program.BlobIndex);
                            
                        m_InsertSubProgramCommand.Parameters["@id"].Value = s_SubProgramId;
                        m_InsertSubProgramCommand.Parameters["@sub_program"].Value = programIndex;
                        m_InsertSubProgramCommand.Parameters["@hw_tier"].Value = program.HwTier;
                        m_InsertSubProgramCommand.Parameters["@api"].Value = program.Api;
                        m_InsertSubProgramCommand.ExecuteNonQuery();
                        
                        m_InsertSubProgramKeywordsCommand.Transaction = ctx.Transaction;
                        m_InsertSubProgramKeywordsCommand.Parameters["@subprogram_id"].Value = s_SubProgramId;
                        foreach (var keyword in program.Keywords)
                        {
                            m_InsertSubProgramKeywordsCommand.Parameters["@keyword_id"].Value = localToGlobalKeywords[keyword];
                            m_InsertSubProgramKeywordsCommand.ExecuteNonQuery();
                        }

                        ++s_SubProgramId;
                    }
                }
            }
        }
            
        m_InsertCommand.Parameters["@id"].Value = objectId;
        m_InsertCommand.Parameters["@decompressed_size"].Value = shader.DecompressedSize;
        m_InsertCommand.Parameters["@unique_programs"].Value = uniquePrograms.Count;
        
        m_InsertCommand.ExecuteNonQuery();

        name = shader.Name;
        streamDataSize = 0;
    }

    private int GetKeywordId(string keyword, SqliteTransaction ctxTransaction)
    {
        int id;

        if (!s_GlobalKeywords.TryGetValue(keyword, out id))
        {
            id = s_GlobalKeywords.Count;
            s_GlobalKeywords[keyword] = id;
            m_InsertKeywordCommand.Transaction = ctxTransaction;
            m_InsertKeywordCommand.Parameters["@id"].Value = id;
            m_InsertKeywordCommand.Parameters["@keyword"].Value = keyword;
            m_InsertKeywordCommand.ExecuteNonQuery();
        }

        return id;
    }

    public void Finalize(SqliteConnection db)
    {
        using var command = new SqliteCommand();
        command.Connection = db;
        command.CommandText = "CREATE INDEX shader_subprograms_shader_index ON shader_subprograms(shader)";
        command.ExecuteNonQuery();
            
        command.CommandText = "CREATE INDEX shader_subprogram_keywords_subprogram_id_index ON shader_subprogram_keywords(subprogram_id)";
        command.ExecuteNonQuery();
    }

    void IDisposable.Dispose()
    {
        m_InsertCommand?.Dispose();
        m_InsertSubProgramCommand?.Dispose();
        m_InsertKeywordCommand?.Dispose();
        m_InsertSubProgramKeywordsCommand?.Dispose();
    }
}