using NUnit.Framework;
using UnityDataTools.FileSystem;
using UnityDataTools.FileSystem.TypeTreeReaders;
using UnityDataTools.Analyzer.SerializedObjects;
using UnityDataTools.TestCommon;
using UnityDataTools.UnityDataTool.Tests;

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
    
    T ReadObject<T>(long id, Func<RandomAccessReader, T> creator)
    {
        var objectInfo = m_SerializedFile.Objects.First(x => x.Id == id);
        var node = m_SerializedFile.GetTypeTreeRoot(objectInfo.Id);
        var reader = new RandomAccessReader(m_SerializedFile, node, m_FileReader, objectInfo.Offset);
        return creator(reader);
    }

    [TestCase("Texture1", -9023202112035587373)]
    [TestCase("Texture2", 404836592933730457)]
    public void TestTexture2d(string name, long id)
    {
        var texture = ReadObject(id, Texture2D.Read);
        var expectedTexture = (Texture2D)Context.ExpectedData.Get(name);
        
        Assert.AreEqual(expectedTexture.Name, texture.Name);
        Assert.AreEqual(expectedTexture.StreamDataSize, texture.StreamDataSize);
        Assert.AreEqual(expectedTexture.Width, texture.Width);
        Assert.AreEqual(expectedTexture.Height, texture.Height);
        Assert.AreEqual(expectedTexture.Format, texture.Format);
        Assert.AreEqual(expectedTexture.MipCount, texture.MipCount);
        Assert.AreEqual(expectedTexture.RwEnabled, texture.RwEnabled);
    }
    
    [Test]
    public void TestAnimationClip()
    {
        var clip = ReadObject(2152370074763270995, AnimationClip.Read);
        var expectedClip = (AnimationClip)Context.ExpectedData.Get("AnimationClip");
        
        Assert.AreEqual(expectedClip.Name, clip.Name);
        Assert.AreEqual(expectedClip.Events, clip.Events);
        Assert.AreEqual(expectedClip.Legacy, clip.Legacy);
    }
    
    [Test]
    public void TestAudioClip()
    {
        var clip = ReadObject(-8074603400156879931, AudioClip.Read);
        var expectedClip = (AudioClip)Context.ExpectedData.Get("AudioClip");
        
        Assert.AreEqual(expectedClip.Name, clip.Name);
        Assert.AreEqual(expectedClip.Channels, clip.Channels);
        Assert.AreEqual(expectedClip.Format, clip.Format);
        Assert.AreEqual(expectedClip.Frequency, clip.Frequency);
        Assert.AreEqual(expectedClip.LoadType, clip.LoadType);
        Assert.AreEqual(expectedClip.BitsPerSample, clip.BitsPerSample);
        Assert.AreEqual(expectedClip.StreamDataSize, clip.StreamDataSize);
    }
    
    [Test]
    public void TestAssetBundle()
    {
        var bundle = ReadObject(1, AssetBundle.Read);
        var expectedBundle = (AssetBundle)Context.ExpectedData.Get("AssetBundle");
        
        Assert.AreEqual(expectedBundle.Name, bundle.Name);
        Assert.AreEqual(expectedBundle.Assets.Count, bundle.Assets.Count);

        for (int i = 0; i < bundle.Assets.Count; ++i)
        {
            var asset = bundle.Assets[i];
            var expectedAsset = expectedBundle.Assets[i];
            
            Assert.AreEqual(expectedAsset.Name, asset.Name);
            Assert.AreEqual(expectedAsset.PPtr.FileId, asset.PPtr.FileId);
            Assert.AreEqual(expectedAsset.PPtr.PathId, asset.PPtr.PathId);
        }
    }
    
    [Test]
    public void TestMesh()
    {
        var mesh = ReadObject(4693305862354978555, Mesh.Read);
        var expectedMesh = (Mesh)Context.ExpectedData.Get("Mesh");
        
        Assert.AreEqual(expectedMesh.Name, mesh.Name);
        Assert.AreEqual(expectedMesh.Bones, mesh.Bones);
        Assert.AreEqual(expectedMesh.Compression, mesh.Compression);
        Assert.AreEqual(expectedMesh.Indices, mesh.Indices);
        Assert.AreEqual(expectedMesh.Vertices, mesh.Vertices);
        Assert.AreEqual(expectedMesh.BlendShapes, mesh.BlendShapes);
        Assert.AreEqual(expectedMesh.RwEnabled, mesh.RwEnabled);
        Assert.AreEqual(expectedMesh.StreamDataSize, mesh.StreamDataSize);
    }

    [Test]
    public void TestShaderReader()
    {
        var shader = ReadObject(-4850512016903265157, Shader.Read);
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
