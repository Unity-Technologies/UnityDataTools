using System.Collections.Generic;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SerializedObjects;

public class Shader
{
    public string Name { get; init; }
    public int DecompressedSize { get; init; }
    public IReadOnlyList<SubShader> SubShaders { get; init; }
    public IReadOnlyList<string> Keywords { get; init; }

    private Shader() {}

    public class SubShader
    {
        public IReadOnlyList<Pass> Passes { get; init; }
        
        public class Pass
        {
            public string Name { get; init; }

            // The key is the program type (vertex, fragment...) 
            public IReadOnlyDictionary<string, IReadOnlyList<SubProgram>> Programs { get; init;  }
            
            public class SubProgram
            {
                public int HwTier { get; init; }
                public int Api { get; init; }
                public uint BlobIndex { get; init; }
                
                // Keyword index in ShaderData.Keywords 
                public IReadOnlyList<int> Keywords { get; init; }
                
                private SubProgram() {}

                public static SubProgram Read(KeywordSet keywordSet, RandomAccessReader reader, Dictionary<int, string> keywordNames, int hwTier = -1)
                {
                    var api = reader["m_GpuProgramType"].GetValue<sbyte>();
                    var blobIndex = reader["m_BlobIndex"].GetValue<uint>();
                    var keywords = new List<int>();
                    hwTier = hwTier != -1 ? hwTier : reader["m_ShaderHardwareTier"].GetValue<sbyte>();

                    if (reader.HasChild("m_KeywordIndices"))
                    {
                        var indices = reader["m_KeywordIndices"].GetValue<ushort[]>();

                        foreach (var index in indices)
                        {
                            if (keywordNames.TryGetValue(index, out var name))
                            {
                                keywords.Add(keywordSet.GetKeywordIndex(name));
                            }
                        }
                    }
                    else
                    {
                        foreach (var index in reader["m_GlobalKeywordIndices"].GetValue<ushort[]>())
                        {
                            if (keywordNames.TryGetValue(index, out var name))
                            {
                                keywords.Add(keywordSet.GetKeywordIndex(name));
                            }
                        }

                        foreach (var index in reader["m_LocalKeywordIndices"].GetValue<ushort[]>())
                        {
                            if (keywordNames.TryGetValue(index, out var name))
                            {
                                keywords.Add(keywordSet.GetKeywordIndex(name));
                            }
                        }
                    }
                
                    return new SubProgram() { Api = api, BlobIndex = blobIndex, HwTier = hwTier , Keywords = keywords };
                }
            }

            public static Pass Read(KeywordSet keywordSet, RandomAccessReader reader, Dictionary<int, string> keywordNames)
            {
                string name = null;
                Dictionary<string, IReadOnlyList<SubProgram>> programsPerType = new();
                
                if (keywordNames == null)
                {
                    keywordNames = new();

                    var nameIndices = reader["m_NameIndices"];

                    foreach (var nameIndex in nameIndices)
                    {
                        keywordNames[nameIndex["second"].GetValue<int>()] =
                            nameIndex["first"].GetValue<string>();
                    }
                }

                if (reader.HasChild("m_State"))
                {
                    name = reader["m_State"]["m_Name"].GetValue<string>();
                }

                foreach (var progType in s_progTypes)
                {
                    if (!reader.HasChild(progType.fieldName))
                    {
                        continue;
                    }

                    var program = reader[progType.fieldName];

                    // Starting in some Unity 2021.3 version, programs are stored in m_PlayerSubPrograms instead of m_SubPrograms.
                    if (program.HasChild("m_PlayerSubPrograms"))
                    {
                        int numSubPrograms = 0;
                        var subPrograms = program["m_PlayerSubPrograms"];

                        // And they are stored per hardware tiers.
                        foreach (var tierProgram in subPrograms)
                        {
                            // Count total number of programs.
                            numSubPrograms += tierProgram.GetArraySize();
                        }

                        // Preallocate enough elements to avoid allocations.
                        var programs = new List<SubProgram>(numSubPrograms);

                        for (int hwTier = 0; hwTier < subPrograms.GetArraySize(); ++hwTier)
                        {
                            foreach (var subProgram in subPrograms[hwTier])
                            {
                                programs.Add(SubProgram.Read(keywordSet, subProgram, keywordNames, hwTier));
                            }
                        }

                        if (programs.Count > 0)
                        {
                            programsPerType[progType.typeName] = programs;
                        }
                    }
                    else
                    {
                        var subPrograms = program["m_SubPrograms"];

                        if (subPrograms.Count > 0)
                        {
                            var programs = new List<SubProgram>(subPrograms.GetArraySize());
                            
                            foreach (var subProgram in subPrograms)
                            {
                                programs.Add(SubProgram.Read(keywordSet, subProgram, keywordNames));
                            }
                            
                            programsPerType[progType.typeName] = programs;
                        }
                    }
                }

                return new Pass() { Name = name, Programs = programsPerType };
            }
        }

        public static SubShader Read(KeywordSet keywordSet, RandomAccessReader reader, Dictionary<int, string> keywordNames)
        {
            var passesReader = reader["m_Passes"];
            var passes = new List<Pass>(passesReader.GetArraySize());
            
            foreach (var pass in passesReader)
            {
                passes.Add(Pass.Read(keywordSet, pass, keywordNames));
            }

            return new SubShader() {Passes = passes};
        }
    }

    public static Shader Read(RandomAccessReader reader)
    {
        Dictionary<int, string> keywordNames = null;
        KeywordSet keywordSet = new KeywordSet();
        var parsedForm = reader["m_ParsedForm"];

        // Starting in some Unity 2021 version, keyword names are stored in m_KeywordNames.
        if (parsedForm.HasChild("m_KeywordNames"))
        {
            keywordNames = new();

            int i = 0;
            foreach (var keyword in parsedForm["m_KeywordNames"])
            {
                keywordNames[i++] = keyword.GetValue<string>();
            }
        }

        var subShadersReader = parsedForm["m_SubShaders"];
        List<SubShader> subShaders = new (subShadersReader.GetArraySize());
        
        foreach (var subShader in subShadersReader)
        {
            subShaders.Add(SubShader.Read(keywordSet, subShader, keywordNames));
        }
        
        int decompressedSize = 0;

        if (reader["decompressedLengths"].IsArrayOfObjects)
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

        var name = parsedForm["m_Name"].GetValue<string>();

        return new Shader() { DecompressedSize = decompressedSize, Name = name, SubShaders = subShaders, Keywords = keywordSet.Keywords };
    }

    private static readonly IReadOnlyList<(string fieldName, string typeName)> s_progTypes = new List<(string fieldName, string typeName)>()
    {
        ("progVertex", "vertex"),
        ("progFragment", "fragment"),
        ("progGeometry", "geometry"),
        ("progHull", "hull"),
        ("progDomain", "domain"),
        ("progRayTracing", "ray tracing"),
    };

    public class KeywordSet
    {
        public IReadOnlyList<string> Keywords => m_Keywords;

        private List<string> m_Keywords = new();
        private Dictionary<string, int> m_KeywordToIndex = new();
        
        public int GetKeywordIndex(string name)
        {
            int index;
        
            if (m_KeywordToIndex.TryGetValue(name, out index))
            {
                return index;
            }

            index = Keywords.Count;
            m_Keywords.Add(name);
            m_KeywordToIndex[name] = index;

            return index;
        }
    }
}