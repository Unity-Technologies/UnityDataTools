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

        Dictionary<int, string> m_KeywordNames = new Dictionary<int, string>();
        StringBuilder m_Keywords = new StringBuilder();
        HashSet<string> m_UniqueKeywords = new HashSet<string>();
        HashSet<uint> m_UniquePrograms = new HashSet<uint>();

        public void Init(SQLiteConnection db)
        {
            using var command = new SQLiteCommand(db);

            command.CommandText = Properties.Resources.Shader;
            command.ExecuteNonQuery();

            m_InsertCommand = new SQLiteCommand(db);
            m_InsertCommand.CommandText = "INSERT INTO shaders(id, decompressed_size, sub_shaders, unique_programs, keywords) VALUES(@id, @decompressed_size, @sub_shaders, @unique_programs, @keywords)";
            m_InsertCommand.Parameters.Add("@id", DbType.Int64);
            m_InsertCommand.Parameters.Add("@decompressed_size", DbType.Int32);
            m_InsertCommand.Parameters.Add("@sub_shaders", DbType.Int32);
            m_InsertCommand.Parameters.Add("@unique_programs", DbType.Int32);
            m_InsertCommand.Parameters.Add("@keywords", DbType.String);

            m_InsertSubProgramCommand = new SQLiteCommand(db);
            m_InsertSubProgramCommand.CommandText = "INSERT INTO shader_subprograms(shader, pass, hw_tier, shader_type, api, keywords) VALUES(@shader, @pass, @hw_tier, @shader_type, @api, @keywords)";
            m_InsertSubProgramCommand.Parameters.Add("@shader", DbType.Int64);
            m_InsertSubProgramCommand.Parameters.Add("@pass", DbType.Int32);
            m_InsertSubProgramCommand.Parameters.Add("@hw_tier", DbType.Int32);
            m_InsertSubProgramCommand.Parameters.Add("@shader_type", DbType.String);
            m_InsertSubProgramCommand.Parameters.Add("@api", DbType.Int32);
            m_InsertSubProgramCommand.Parameters.Add("@keywords", DbType.String);
        }

        public void Process(AnalyzerTool analyzer, long objectId, Dictionary<int, int> localToDbFileId, RandomAccessReader reader, out string name, out long streamedDataSize)
        {
            int currentProgram = 0;

            streamedDataSize = 0;

            m_UniqueKeywords.Clear();
            m_UniquePrograms.Clear();

            var parsedForm = reader["m_ParsedForm"];

            m_InsertCommand.Parameters["@id"].Value = objectId;
            m_InsertCommand.Parameters["@sub_shaders"].Value = parsedForm["m_SubShaders"].GetArraySize();

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

            foreach (var subShader in parsedForm["m_SubShaders"])
            {
                foreach (var pass in subShader["m_Passes"])
                {
                    int passNum = 0;
                    ushort[] passKeywordIndices = { };

                    if (!keywordsUnity2021)
                    {
                        m_KeywordNames.Clear();

                        var nameIndices = pass["m_NameIndices"];

                        foreach (var nameIndex in nameIndices)
                        {
                            m_KeywordNames[nameIndex["second"].GetValue<int>()] = nameIndex["first"].GetValue<string>();
                        }
                    }
                    else
                    {
                        passKeywordIndices = pass["m_SerializedKeywordStateMask"].GetValue<ushort[]>();

                        foreach (var index in passKeywordIndices)
                        {
                            m_UniqueKeywords.Add(m_KeywordNames[index]);
                        }
                    }

                    ProcessProgram(objectId, passNum, ref currentProgram, pass["progVertex"]["m_SubPrograms"], "vertex", passKeywordIndices);
                    ProcessProgram(objectId, passNum, ref currentProgram, pass["progFragment"]["m_SubPrograms"], "fragment", passKeywordIndices);
                    ProcessProgram(objectId, passNum, ref currentProgram, pass["progGeometry"]["m_SubPrograms"], "geometry", passKeywordIndices);
                    ProcessProgram(objectId, passNum, ref currentProgram, pass["progHull"]["m_SubPrograms"], "hull", passKeywordIndices);
                    ProcessProgram(objectId, passNum, ref currentProgram, pass["progDomain"]["m_SubPrograms"], "domain", passKeywordIndices);

                    if (pass.HasChild("progRayTracing"))
                    {
                        ProcessProgram(objectId, passNum, ref currentProgram, pass["progRayTracing"]["m_SubPrograms"], "ray tracing", passKeywordIndices);
                    }

                    ++passNum;
                }
            }

            int decompressedSize = 0;

            if (!reader["decompressedLengths"].TypeTreeNode.Children[1].IsLeaf)
            {
                // The decompressed lengths are now stored per graphics API.
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

            m_Keywords.Clear();
            m_Keywords.AppendJoin(' ', m_UniqueKeywords);

            m_InsertCommand.Parameters["@id"].Value = objectId;
            m_InsertCommand.Parameters["@decompressed_size"].Value = decompressedSize;
            m_InsertCommand.Parameters["@sub_shaders"].Value = parsedForm["m_SubShaders"].GetArraySize();
            m_InsertCommand.Parameters["@unique_programs"].Value = m_UniquePrograms.Count;
            m_InsertCommand.Parameters["@keywords"].Value = m_Keywords.ToString();
            m_InsertCommand.ExecuteNonQuery();

            name = parsedForm["m_Name"].GetValue<string>();
        }

        void ProcessProgram(long objectId, int passNum, ref int currentProgram, RandomAccessReader subPrograms, string shaderType, ushort[] passKeywordIndices)
        {
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
                            m_Keywords.Append(name);
                            m_Keywords.Append(' ');
                            m_UniqueKeywords.Add(name);
                        }
                    }
                }
                else
                {
                    foreach (var index in subProgram["m_GlobalKeywordIndices"].GetValue<ushort[]>())
                    {
                        if (m_KeywordNames.TryGetValue(index, out var name))
                        {
                            m_Keywords.Append(name);
                            m_Keywords.Append(' ');
                            m_UniqueKeywords.Add(name);
                        }
                    }

                    foreach (var index in subProgram["m_LocalKeywordIndices"].GetValue<ushort[]>())
                    {
                        if (m_KeywordNames.TryGetValue(index, out var name))
                        {
                            m_Keywords.Append(name);
                            m_Keywords.Append(' ');
                            m_UniqueKeywords.Add(name);
                        }
                    }
                }

                m_InsertSubProgramCommand.Parameters["@shader"].Value = objectId;
                m_InsertSubProgramCommand.Parameters["@pass"].Value = passNum;
                m_InsertSubProgramCommand.Parameters["@hw_tier"].Value = subProgram["m_ShaderHardwareTier"].GetValue<sbyte>();
                m_InsertSubProgramCommand.Parameters["@shader_type"].Value = shaderType;
                m_InsertSubProgramCommand.Parameters["@api"].Value = subProgram["m_GpuProgramType"].GetValue<sbyte>();
                m_InsertSubProgramCommand.Parameters["@keywords"].Value = m_Keywords.ToString();
                m_InsertSubProgramCommand.ExecuteNonQuery();
            }
        }

        void IDisposable.Dispose()
        {
            m_InsertCommand.Dispose();
            m_InsertSubProgramCommand.Dispose();
        }
    }
}
