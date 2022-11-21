using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SQLite.Handlers;

public class ShaderHandler : ISQLiteHandler
{
    SQLiteCommand m_InsertCommand;
    SQLiteCommand m_InsertSubProgramCommand;
    SQLiteCommand m_InsertKeywordCommand;
    SQLiteCommand m_InsertSubProgramKeywordsCommand;
        
    static long s_SubProgramId = 0;
    static Dictionary<string, int> s_GlobalKeywords = new();

    public void Init(SQLiteConnection db)
    {
        s_SubProgramId = 0;
        s_GlobalKeywords.Clear();
            
        using var command = new SQLiteCommand(db);

        command.CommandText = Properties.Resources.Shader;
        command.ExecuteNonQuery();

        m_InsertCommand = new SQLiteCommand(db);
        m_InsertCommand.CommandText = "INSERT INTO shaders(id, decompressed_size, unique_programs) VALUES(@id, @decompressed_size, @unique_programs)";
        m_InsertCommand.Parameters.Add("@id", DbType.Int64);
        m_InsertCommand.Parameters.Add("@decompressed_size", DbType.Int32);
        m_InsertCommand.Parameters.Add("@unique_programs", DbType.Int32);

        m_InsertSubProgramCommand = new SQLiteCommand(db);
        m_InsertSubProgramCommand.CommandText = "INSERT INTO shader_subprograms(shader, sub_shader, pass, pass_name, sub_program, hw_tier, shader_type, api) VALUES(@shader, @sub_shader, @pass, @pass_name, @sub_program, @hw_tier, @shader_type, @api)";
        m_InsertSubProgramCommand.Parameters.Add("@id", DbType.Int64);
        m_InsertSubProgramCommand.Parameters.Add("@shader", DbType.Int64);
        m_InsertSubProgramCommand.Parameters.Add("@sub_shader", DbType.Int32);
        m_InsertSubProgramCommand.Parameters.Add("@pass", DbType.Int32);
        m_InsertSubProgramCommand.Parameters.Add("@pass_name", DbType.String);
        m_InsertSubProgramCommand.Parameters.Add("@sub_program", DbType.Int32);
        m_InsertSubProgramCommand.Parameters.Add("@hw_tier", DbType.Int32);
        m_InsertSubProgramCommand.Parameters.Add("@shader_type", DbType.String);
        m_InsertSubProgramCommand.Parameters.Add("@api", DbType.Int32);

        m_InsertKeywordCommand = new SQLiteCommand(db);
        m_InsertKeywordCommand.CommandText = "INSERT INTO shader_keywords(id, keyword) VALUES(@id, @keyword)";
        m_InsertKeywordCommand.Parameters.Add("@id", DbType.Int32);
        m_InsertKeywordCommand.Parameters.Add("@keyword", DbType.String);

        m_InsertSubProgramKeywordsCommand = new SQLiteCommand(db);
        m_InsertSubProgramKeywordsCommand.CommandText = "INSERT INTO shader_subprogram_keywords(subprogram_id, keyword_id) VALUES (@subprogram_id, @keyword_id)";
        m_InsertSubProgramKeywordsCommand.Parameters.Add("@subprogram_id", DbType.Int64);
        m_InsertSubProgramKeywordsCommand.Parameters.Add("@keyword_id", DbType.Int32);
    }

    public void Process(Context ctx, long objectId, RandomAccessReader reader, out string name, out long streamDataSize)
    {
        var shader = SerializedObjects.Shader.Read(reader);
        var uniquePrograms = new HashSet<uint>();
        var localToGlobalKeywords = new Dictionary<int, int>();

        for (int i = 0; i < shader.Keywords.Count; ++i)
        {
            var keyword = shader.Keywords[i];
            var id = GetKeywordId(keyword);
            localToGlobalKeywords[i] = id;
        }

        m_InsertCommand.Parameters["@id"].Value = objectId;

        for (int subShaderIndex = 0; subShaderIndex < shader.SubShaders.Count; ++subShaderIndex)
        {
            var subShader = shader.SubShaders[subShaderIndex];

            m_InsertSubProgramCommand.Parameters["@sub_shader"].Value = subShaderIndex;

            for (int passIndex = 0; passIndex < subShader.Passes.Count; ++passIndex)
            {
                var pass = subShader.Passes[passIndex];
                    
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

    private int GetKeywordId(string keyword)
    {
        int id;

        if (!s_GlobalKeywords.TryGetValue(keyword, out id))
        {
            id = s_GlobalKeywords.Count;
            s_GlobalKeywords[keyword] = id;

            m_InsertKeywordCommand.Parameters["@id"].Value = id;
            m_InsertKeywordCommand.Parameters["@keyword"].Value = keyword;
            m_InsertKeywordCommand.ExecuteNonQuery();
        }

        return id;
    }

    public void Finalize(SQLiteConnection db)
    {
        using var command = new SQLiteCommand(db);
            
        command.CommandText = "CREATE INDEX shader_subprograms_shader_index ON shader_subprograms(shader)";
        command.ExecuteNonQuery();
            
        command.CommandText = "CREATE INDEX shader_subprogram_keywords_subprogram_id_index ON shader_subprogram_keywords(subprogram_id)";
        command.ExecuteNonQuery();
    }

    void IDisposable.Dispose()
    {
        m_InsertCommand.Dispose();
        m_InsertSubProgramCommand.Dispose();
        m_InsertKeywordCommand.Dispose();
        m_InsertSubProgramKeywordsCommand.Dispose();
    }
}