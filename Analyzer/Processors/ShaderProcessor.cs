using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.Processors
{
    public class ShaderProcessor : IProcessor, IDisposable
    {
        SQLiteCommand m_InsertCommand;
        SQLiteCommand m_InsertSubProgramCommand;
        SQLiteCommand m_InsertKeywordCommand;
        SQLiteCommand m_InsertSubProgramKeywordsCommand;

        List<int> m_Keywords = new();
        Dictionary<int, string> m_KeywordNames = new();
        HashSet<uint> m_UniquePrograms = new();

        static Dictionary<string, int> s_Keywords = new();
        static long s_SubProgramId = 0;

        static readonly List<(string fieldName, string typeName)> s_progTypes = new()
        {
            ("progVertex", "vertex"),
            ("progFragment", "fragment"),
            ("progGeometry", "geometry"),
            ("progHull", "hull"),
            ("progDomain", "domain"),
            ("progRayTracing", "ray tracing"),
        };

        public void Init(SQLiteConnection db)
        {
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

        public void Process(AnalyzerTool analyzer, long objectId, Dictionary<int, int> localToDbFileId, RandomAccessReader reader, out string name, out long streamedDataSize)
        {
            streamedDataSize = 0;

            m_UniquePrograms.Clear();

            var parsedForm = reader["m_ParsedForm"];

            m_InsertCommand.Parameters["@id"].Value = objectId;

            // Starting in some Unity 2021 version, keyword names are stored in m_KeywordNames.
            bool keywordsUnity2021 = false;

            if (parsedForm.HasChild("m_KeywordNames"))
            {
                keywordsUnity2021 = true;

                m_KeywordNames.Clear();

                int i = 0;
                foreach (var keyword in parsedForm["m_KeywordNames"])
                {
                    m_KeywordNames[i++] = keyword.GetValue<string>();
                }
            }

            int subShaderNum = 0;
            foreach (var subShader in parsedForm["m_SubShaders"])
            {
                int passNum = 0;

                m_InsertSubProgramCommand.Parameters["@sub_shader"].Value = subShaderNum++;

                foreach (var pass in subShader["m_Passes"])
                {
                    if (!keywordsUnity2021)
                    {
                        m_KeywordNames.Clear();

                        var nameIndices = pass["m_NameIndices"];

                        foreach (var nameIndex in nameIndices)
                        {
                            m_KeywordNames[nameIndex["second"].GetValue<int>()] = nameIndex["first"].GetValue<string>();
                        }
                    }

                    string passName = "";
                    if (pass.HasChild("m_State"))
                    {
                        passName = pass["m_State"]["m_Name"].GetValue<string>();
                    }

                    m_InsertSubProgramCommand.Parameters["@shader"].Value = objectId;
                    m_InsertSubProgramCommand.Parameters["@pass"].Value = passNum;
                    m_InsertSubProgramCommand.Parameters["@pass_name"].Value = passName;

                    foreach (var progType in s_progTypes)
                    {
                        if (!pass.HasChild(progType.fieldName))
                        {
                            continue;
                        }

                        var program = pass[progType.fieldName];

                        m_InsertSubProgramCommand.Parameters["@shader_type"].Value = progType.typeName;

                        // Sarting in some Unity 2021.3 version, programs are stored in m_PlayerSubPrograms instead of m_SubPrograms.
                        if (program.HasChild("m_PlayerSubPrograms"))
                        {
                            int hwTier = 0;

                            // And they are stored per hardware tiers.
                            foreach (var tierProgram in program["m_PlayerSubPrograms"])
                            {
                                ProcessProgram(tierProgram, hwTier++);
                            }
                        }
                        else
                        {
                            ProcessProgram(program["m_SubPrograms"]);
                        }
                    }

                    ++passNum;
                }
            }

            int decompressedSize = 0;

            if (!reader["decompressedLengths"].TypeTreeNode.Children[1].IsLeaf)
            {
                // The decompressed lengths are stored per graphics API.
                foreach (var apiLengths in reader["decompressedLengths"])
                {
                    foreach (var blockSize in apiLengths.GetValue<int[]>())
                    {
                        decompressedSize += blockSize;
                    }
                }

                // Take the average (not ideal, but better than nothing).
                decompressedSize /= reader["decompressedLengths"].GetArraySize();
            }
            else
            {
                foreach (var blockSize in reader["decompressedLengths"].GetValue<int[]>())
                {
                    decompressedSize += blockSize;
                }
            }

            m_InsertCommand.Parameters["@id"].Value = objectId;
            m_InsertCommand.Parameters["@decompressed_size"].Value = decompressedSize;
            m_InsertCommand.Parameters["@unique_programs"].Value = m_UniquePrograms.Count;
            m_InsertCommand.ExecuteNonQuery();

            name = parsedForm["m_Name"].GetValue<string>();
        }

        void ProcessProgram(RandomAccessReader subPrograms, int hwTier = -1)
        {
            int progNum = 0;

            foreach (var subProgram in subPrograms)
            {
                m_Keywords.Clear();

                m_UniquePrograms.Add(subProgram["m_BlobIndex"].GetValue<uint>());

                if (subProgram.HasChild("m_KeywordIndices"))
                {
                    var indices = subProgram["m_KeywordIndices"].GetValue<ushort[]>();

                    foreach (var index in indices)
                    {
                        if (m_KeywordNames.TryGetValue(index, out var name))
                        {
                            m_Keywords.Add(GetKeywordId(name));
                        }
                    }
                }
                else
                {
                    foreach (var index in subProgram["m_GlobalKeywordIndices"].GetValue<ushort[]>())
                    {
                        if (m_KeywordNames.TryGetValue(index, out var name))
                        {
                            m_Keywords.Add(GetKeywordId(name));
                        }
                    }

                    foreach (var index in subProgram["m_LocalKeywordIndices"].GetValue<ushort[]>())
                    {
                        if (m_KeywordNames.TryGetValue(index, out var name))
                        {
                            m_Keywords.Add(GetKeywordId(name));
                        }
                    }
                }

                m_InsertSubProgramCommand.Parameters["@id"].Value = s_SubProgramId;
                m_InsertSubProgramCommand.Parameters["@sub_program"].Value = progNum++;
                m_InsertSubProgramCommand.Parameters["@hw_tier"].Value = hwTier != -1 ? hwTier : subProgram["m_ShaderHardwareTier"].GetValue<sbyte>();
                m_InsertSubProgramCommand.Parameters["@api"].Value = subProgram["m_GpuProgramType"].GetValue<sbyte>();
                m_InsertSubProgramCommand.ExecuteNonQuery();

                m_InsertSubProgramKeywordsCommand.Parameters["@subprogram_id"].Value = s_SubProgramId;
                foreach (var keyword in m_Keywords)
                {
                    m_InsertSubProgramKeywordsCommand.Parameters["@keyword_id"].Value = keyword;
                    m_InsertSubProgramKeywordsCommand.ExecuteNonQuery();
                }

                ++s_SubProgramId;
            }
        }

        int GetKeywordId(string keyword)
        {
            int id;

            if (!s_Keywords.TryGetValue(keyword, out id))
            {
                id = s_Keywords.Count;
                s_Keywords[keyword] = id;

                m_InsertKeywordCommand.Parameters["@id"].Value = id;
                m_InsertKeywordCommand.Parameters["@keyword"].Value = keyword;
                m_InsertKeywordCommand.ExecuteNonQuery();
            }

            return id;
        }

        void IDisposable.Dispose()
        {
            m_InsertCommand.Dispose();
            m_InsertSubProgramCommand.Dispose();
            m_InsertKeywordCommand.Dispose();
            m_InsertSubProgramKeywordsCommand.Dispose();
        }
    }
}
