using NUnit.Framework;
using UnityDataTools.FileSystem;
using UnityDataTools.FileSystem.TypeTreeReaders;
using UnityDataTools.Analyzer.SerializedObjects;
using UnityDataTools.TestCommon;

namespace UnityDataTools.Analyzer.Tests;

#pragma warning disable NUnit2005, NUnit2006

public class SerializedObjectsTests : TestForAllVersions
{
    private UnityArchive m_Archive;
    private SerializedFile m_SerializedFile;
    private UnityFileReader m_FileReader;

    static SerializedObjectsTests()
    {
        //ExpectedDataGenerator.GenerateAll();
    }

    public SerializedObjectsTests(Context context) : base(context)
    {
    }

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        UnityFileSystem.Init();

        var path = Path.Combine(Context.UnityDataFolder, "AssetBundles", "assetbundle");
        m_Archive = UnityFileSystem.MountArchive(path, "archive:/");
        m_SerializedFile = UnityFileSystem.OpenSerializedFile("archive:/CAB-5d40f7cad7c871cf2ad2af19ac542994");
        m_FileReader = new UnityFileReader("archive:/CAB-5d40f7cad7c871cf2ad2af19ac542994", 1024*1024);
    }
    
    [OneTimeTearDown]
    public void TearDown()
    {
        m_FileReader.Dispose();
        m_SerializedFile.Dispose();
        m_Archive.Dispose();

        UnityFileSystem.Cleanup();
    }

    [Test]
    public void TestTexture2d()
    {
        var objectInfo = m_SerializedFile.Objects.First(x => x.Id == -9023202112035587373);
        var node = m_SerializedFile.GetTypeTreeRoot(objectInfo.Id);
        var reader = new RandomAccessReader(m_SerializedFile, node, m_FileReader, objectInfo.Offset);
        var texture = Texture2D.Read(reader);
        var expectedTexture = (Texture2D)Context.ExpectedData.Get("Texture2D");
        
        Assert.AreEqual(expectedTexture.Name, texture.Name);
        Assert.AreEqual(expectedTexture.StreamDataSize, texture.StreamDataSize);
        Assert.AreEqual(expectedTexture.Width, texture.Width);
        Assert.AreEqual(expectedTexture.Height, texture.Height);
        Assert.AreEqual(expectedTexture.Format, texture.Format);
        Assert.AreEqual(expectedTexture.MipCount, texture.MipCount);
        Assert.AreEqual(expectedTexture.RwEnabled, texture.RwEnabled);
    }
    
    /*[Test]
    public void AnimationClip()
    {
        var objectInfo = m_SerializedFile.Objects.First(x => x.Id == -9023202112035587373);
        var node = m_SerializedFile.GetTypeTreeRoot(objectInfo.Id);
        var reader = new RandomAccessReader(m_SerializedFile, node, m_FileReader, objectInfo.Offset);
        var texture = Texture2D.Read(reader);
        var expectedTexture = (Texture2D)Context.ExpectedData.Get("Texture2D");
        
        Assert.AreEqual(expectedTexture.Name, texture.Name);
        Assert.AreEqual(expectedTexture.StreamDataSize, texture.StreamDataSize);
        Assert.AreEqual(expectedTexture.Width, texture.Width);
        Assert.AreEqual(expectedTexture.Height, texture.Height);
        Assert.AreEqual(expectedTexture.Format, texture.Format);
        Assert.AreEqual(expectedTexture.MipCount, texture.MipCount);
        Assert.AreEqual(expectedTexture.RwEnabled, texture.RwEnabled);
    }*/

    [Test]
    public void TestShaderReader()
    {
        var objectInfo = m_SerializedFile.Objects.First(x => x.Id == -4850512016903265157);
        var node = m_SerializedFile.GetTypeTreeRoot(objectInfo.Id);
        var reader = new RandomAccessReader(m_SerializedFile, node, m_FileReader, objectInfo.Offset);
        var shader = Shader.Read(reader);
        var expectedShader = (Shader)Context.ExpectedData.Get("Shader");
        
        Assert.AreEqual(expectedShader.Name, shader.Name);
        Assert.AreEqual(expectedShader.DecompressedSize, shader.DecompressedSize);
        CollectionAssert.AreEquivalent(expectedShader.Keywords, shader.Keywords);
        Assert.AreEqual(expectedShader.SubShaders.Count, shader.SubShaders.Count);

        for (int i = 0; i < shader.SubShaders.Count; ++i)
        {
            var subShader = shader.SubShaders[i];
            var expectedSubShader = shader.SubShaders[i];
            
            Assert.AreEqual(expectedSubShader.Passes.Count, subShader.Passes.Count);

            for (int j = 0; j < subShader.Passes.Count; ++j)
            {
                var pass = subShader.Passes[i];
                var expectedPass = expectedSubShader.Passes[i];
                
                Assert.AreEqual(expectedPass.Name, pass.Name);
                Assert.AreEqual(expectedPass.Programs.Count, pass.Programs.Count);
                CollectionAssert.AreEquivalent(expectedPass.Programs.Keys, pass.Programs.Keys);

                foreach (var programsPerType in pass.Programs)
                {
                    var programs = programsPerType.Value;
                    var expectedPrograms = expectedPass.Programs[programsPerType.Key];

                    Assert.AreEqual(expectedPrograms.Count, programs.Count);

                    for (int k = 0; k < programs.Count; ++k)
                    {
                        var program = programs[k];
                        var expectedProgram = expectedPrograms[k];
                        
                        Assert.AreEqual(expectedProgram.Api, program.Api);
                        Assert.AreEqual(expectedProgram.BlobIndex, program.BlobIndex);
                        Assert.AreEqual(expectedProgram.HwTier, program.HwTier);
                        CollectionAssert.AreEquivalent(expectedProgram.Keywords, program.Keywords);
                    }
                }
            }
        }
    }
}
