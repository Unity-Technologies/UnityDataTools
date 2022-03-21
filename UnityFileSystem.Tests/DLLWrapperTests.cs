using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace UnityDataTools.FileSystem.Native.Tests
{
    public class InitCleanupTests
    {
        [TearDown]
        public void TearDown()
        {
            // In case a test fails
            DllWrapper.Cleanup();
        }

        [Test]
        public void Init_CalledOnce_ReturnSuccess()
        {
            var r = DllWrapper.Init();
            Assert.AreEqual(ReturnCode.Success, r);
        }

        [Test]
        public void Cleanup_CalledOnce_ReturnSuccess()
        {
            DllWrapper.Init();
            var r = DllWrapper.Cleanup();
            Assert.AreEqual(ReturnCode.Success, r);
        }

        [Test]
        public void Init_CalledTwice_ReturnError()
        {
            DllWrapper.Init();
            var r = DllWrapper.Init();
            Assert.AreEqual(ReturnCode.AlreadyInitialized, r);
        }

        [Test]
        public void Cleanup_CalledTwice_ReturnError()
        {
            DllWrapper.Init();

            DllWrapper.Cleanup();
            var r = DllWrapper.Cleanup();
            Assert.AreEqual(ReturnCode.NotInitialized, r);
        }

        [Test]
        public void AnyFunction_NotInitialized_ReturnError()
        {
            var r = DllWrapper.MountArchive("", "", out _);
            Assert.AreEqual(ReturnCode.NotInitialized, r);

            r = DllWrapper.UnmountArchive(IntPtr.Zero);
            Assert.AreEqual(ReturnCode.NotInitialized, r);

            r = DllWrapper.GetArchiveNodeCount(new UnityArchiveHandle(), out _);
            Assert.AreEqual(ReturnCode.NotInitialized, r);

            r = DllWrapper.GetArchiveNode(new UnityArchiveHandle(), 0, null, 0, out _, out _);
            Assert.AreEqual(ReturnCode.NotInitialized, r);

            r = DllWrapper.OpenFile("", out _);
            Assert.AreEqual(ReturnCode.NotInitialized, r);

            r = DllWrapper.ReadFile(new UnityFileHandle(), 0, null, out _);
            Assert.AreEqual(ReturnCode.NotInitialized, r);

            r = DllWrapper.SeekFile(new UnityFileHandle(), 0, 0, out _);
            Assert.AreEqual(ReturnCode.NotInitialized, r);

            r = DllWrapper.GetFileSize(new UnityFileHandle(), out _);
            Assert.AreEqual(ReturnCode.NotInitialized, r);

            r = DllWrapper.CloseFile(IntPtr.Zero);
            Assert.AreEqual(ReturnCode.NotInitialized, r);

            r = DllWrapper.OpenSerializedFile("", out _);
            Assert.AreEqual(ReturnCode.NotInitialized, r);

            r = DllWrapper.CloseSerializedFile(IntPtr.Zero);
            Assert.AreEqual(ReturnCode.NotInitialized, r);

            r = DllWrapper.GetExternalReferenceCount(new SerializedFileHandle(), out _);
            Assert.AreEqual(ReturnCode.NotInitialized, r);

            r = DllWrapper.GetExternalReference(new SerializedFileHandle(), 0, null, 0, null, out _);
            Assert.AreEqual(ReturnCode.NotInitialized, r);

            r = DllWrapper.GetObjectCount(new SerializedFileHandle(), out _);
            Assert.AreEqual(ReturnCode.NotInitialized, r);

            r = DllWrapper.GetObjectInfo(new SerializedFileHandle(), null, 0);
            Assert.AreEqual(ReturnCode.NotInitialized, r);

            r = DllWrapper.GetTypeTree(new SerializedFileHandle(), 0, out _);
            Assert.AreEqual(ReturnCode.NotInitialized, r);

            r = DllWrapper.GetTypeTreeNodeInfo(new TypeTreeHandle(), 0, null, 0, null, 0, out _, out _, out _, out _, out _, out _);
            Assert.AreEqual(ReturnCode.NotInitialized, r);
        }
    }

    public class MountUnmountTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            DllWrapper.Init();
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            DllWrapper.Cleanup();
        }

        [Test]
        public void MountArchive_InvalidPath_ReturnError()
        {
            var r = DllWrapper.MountArchive("bad/path", "archive:/", out var handle);
            Assert.AreEqual(ReturnCode.FileNotFound, r);
            Assert.True(handle.IsInvalid);
        }

        [Test]
        public void MountArchive_InvalidArchive_ReturnError()
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "invalidfile");
            var r = DllWrapper.MountArchive(path, "archive:/", out var handle);
            Assert.AreEqual(ReturnCode.FileFormatError, r);
            Assert.True(handle.IsInvalid);
        }

        [Test]
        public void UnmountArchive_InvalidHandle_ReturnError()
        {
            var r = DllWrapper.UnmountArchive(IntPtr.Zero);
            Assert.AreEqual(ReturnCode.InvalidArgument, r);
        }

        [Test]
        public void MountArchive_ActualArchive_ReturnSuccess()
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "assetbundle");
            var r = DllWrapper.MountArchive(path, "archive:/", out var handle);
            Assert.AreEqual(ReturnCode.Success, r);
            Assert.IsFalse(handle.IsInvalid);

            handle.Dispose();
        }

        [Test]
        public void UnmountArchive_ActualArchive_ReturnSuccess()
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "assetbundle");
            DllWrapper.MountArchive(path, "archive:/", out var handle);

            Assert.DoesNotThrow(() => handle.Dispose());
        }
    }

    public class ArchiveTests
    {
        private UnityArchiveHandle archive;

        [OneTimeSetUp]
        public void Setup()
        {
            DllWrapper.Init();
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "assetbundle");
            DllWrapper.MountArchive(path, "archive:/", out archive);
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            archive.Dispose();
            DllWrapper.Cleanup();
        }

        [Test]
        public void GetArchiveNodeCount_InvalidHandle_ReturnError()
        {
            var r = DllWrapper.GetArchiveNodeCount(new UnityArchiveHandle(), out _);
            Assert.AreEqual(ReturnCode.InvalidArgument, r);
        }

        [Test]
        public void GetArchiveNodeCount_ReturnExpectedCount()
        {
            var r = DllWrapper.GetArchiveNodeCount(archive, out var count);
            Assert.AreEqual(ReturnCode.Success, r);
            Assert.AreEqual(3, count);
        }

        [Test]
        public void GetArchiveNode_ValidArchive_ReturnExpectedNode()
        {
            var path = new StringBuilder(256);
            var r = DllWrapper.GetArchiveNode(archive, 0, path, 256, out var size, out var flags);
            Assert.AreEqual(ReturnCode.Success, r);

            Assert.AreEqual("CAB-5d40f7cad7c871cf2ad2af19ac542994", path.ToString());
            Assert.AreEqual(199368, size);
            Assert.AreEqual(ArchiveNodeFlags.SerializedFile, flags);

            r = DllWrapper.GetArchiveNode(archive, 1, path, 256, out size, out flags);
            Assert.AreEqual(ReturnCode.Success, r);

            Assert.AreEqual("CAB-5d40f7cad7c871cf2ad2af19ac542994.resS", path.ToString());
            Assert.AreEqual(2833848, size);
            Assert.AreEqual(ArchiveNodeFlags.None, flags);

            r = DllWrapper.GetArchiveNode(archive, 2, path, 256, out size, out flags);
            Assert.AreEqual(ReturnCode.Success, r);

            Assert.AreEqual("CAB-5d40f7cad7c871cf2ad2af19ac542994.resource", path.ToString());
            Assert.AreEqual(5248, size);
            Assert.AreEqual(ArchiveNodeFlags.None, flags);
        }

        [Test]
        public void GetArchiveNode_StringTooSmall_ReturnError()
        {
            var path = new StringBuilder(10);
            var r = DllWrapper.GetArchiveNode(archive, 0, path, 10, out var size, out var flags);
            Assert.AreEqual(ReturnCode.DestinationBufferTooSmall, r);
        }

        [Test]
        public void GetArchiveNode_InvalidHandle_ReturnError()
        {
            var r = DllWrapper.GetArchiveNode(new UnityArchiveHandle(), 0, new StringBuilder(), 256, out var size, out var flags);
            Assert.AreEqual(ReturnCode.InvalidArgument, r);
        }
    }

    public class FileTests
    {
        private UnityArchiveHandle archive;

        [OneTimeSetUp]
        public void Setup()
        {
            DllWrapper.Init();
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "assetbundle");
            DllWrapper.MountArchive(path, "archive:/", out archive);
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            archive.Dispose();
            DllWrapper.Cleanup();
        }

        [Test]
        public void OpenFile_InvalidPath_ReturnError()
        {
            var r = DllWrapper.OpenFile("bad/path", out _);
            Assert.AreEqual(ReturnCode.FileNotFound, r);
        }

        [Test]
        public void OpenFile_LocalFileSystem_ReturnSuccess()
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "TextFile.txt");
            var r = DllWrapper.OpenFile(path, out var file);
            Assert.AreEqual(ReturnCode.Success, r);
            Assert.IsFalse(file.IsInvalid);

            file.Dispose();
        }

        [Test]
        public void CloseFile_LocalFileSystem_ReturnSuccess()
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "TextFile.txt");
            DllWrapper.OpenFile(path, out var file);

            Assert.DoesNotThrow(() => file.Dispose());
        }

        [Test]
        public void CloseFile_InvalidHandle_ReturnError()
        {
            var r = DllWrapper.CloseFile(IntPtr.Zero);
            Assert.AreEqual(ReturnCode.InvalidArgument, r);
        }

        [Test]
        public void GetFileSize_LocalFileSystem_ReturnSize()
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "TextFile.txt");
            DllWrapper.OpenFile(path, out var file);
            DllWrapper.GetFileSize(file, out var size);

            Assert.AreEqual(21, size);

            file.Dispose();
        }

        [Test]
        public void GetFileSize_InvalidHandle_ReturnError()
        {
            var r = DllWrapper.GetFileSize(new UnityFileHandle(), out _);
            Assert.AreEqual(ReturnCode.InvalidArgument, r);
        }

        [Test]
        public void SeekFile_LocalFileSystem_SeekAtExpectedPosition()
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "TextFile.txt");
            DllWrapper.OpenFile(path, out var file);

            DllWrapper.SeekFile(file, 16, SeekOrigin.Begin, out var newPos);
            Assert.AreEqual(16, newPos);

            var buffer = new Byte[16];
            DllWrapper.ReadFile(file, 4, buffer, out var actualSize);
            Assert.AreEqual(4, actualSize);
            Assert.AreEqual("file", Encoding.Default.GetString(buffer, 0, (int)actualSize));

            DllWrapper.SeekFile(file, -4, SeekOrigin.Current, out newPos);
            Assert.AreEqual(16, newPos);

            buffer = new Byte[16];
            DllWrapper.ReadFile(file, 4, buffer, out actualSize);
            Assert.AreEqual(4, actualSize);
            Assert.AreEqual("file", Encoding.Default.GetString(buffer, 0, (int)actualSize));

            DllWrapper.SeekFile(file, -5, SeekOrigin.End, out newPos);
            Assert.AreEqual(16, newPos);

            buffer = new Byte[16];
            DllWrapper.ReadFile(file, 4, buffer, out actualSize);
            Assert.AreEqual(4, actualSize);
            Assert.AreEqual("file", Encoding.Default.GetString(buffer, 0, (int)actualSize));

            file.Dispose();
        }

        [Test]
        public void SeekFile_InvalidHandle_ReturnError()
        {
            var r = DllWrapper.SeekFile(new UnityFileHandle(), 0, SeekOrigin.Begin, out _);
            Assert.AreEqual(ReturnCode.InvalidArgument, r);
        }

        [Test]
        public void ReadFile_LocalFileSystem_ReadExpectedData()
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "TextFile.txt");
            DllWrapper.OpenFile(path, out var file);

            var buffer = new Byte[1000];

            var r = DllWrapper.ReadFile(file, 1000, buffer, out var actualSize);

            Assert.AreEqual(21, actualSize);
            Assert.AreEqual("This is my text file.", Encoding.Default.GetString(buffer, 0, (int)actualSize));

            file.Dispose();
        }

        [Test]
        public void ReadFile_InvalidHandle_ReturnError()
        {
            var r = DllWrapper.ReadFile(new UnityFileHandle(), 10, new byte[10], out _);
            Assert.AreEqual(ReturnCode.InvalidArgument, r);
        }

        [Test]
        public void OpenFile_ArchiveFileSystem_ReturnSuccess()
        {
            var r = DllWrapper.OpenFile("archive:/CAB-5d40f7cad7c871cf2ad2af19ac542994", out var file);
            Assert.AreEqual(ReturnCode.Success, r);
            Assert.IsFalse(file.IsInvalid);

            file.Dispose();
        }

        [Test]
        public void CloseFile_ArchiveFileSystem_ReturnSuccess()
        {
            DllWrapper.OpenFile("archive:/CAB-5d40f7cad7c871cf2ad2af19ac542994", out var file);

            Assert.DoesNotThrow(() => file.Dispose());
        }

        [Test]
        public void ReadFile_ArchiveFileSystem_ReadExpectedData()
        {
            DllWrapper.OpenFile("archive:/CAB-5d40f7cad7c871cf2ad2af19ac542994", out var file);

            var buffer = new Byte[100];
            Byte[] expectedBuffer = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 22, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 231, 141, 0, 0, 0, 0, 0, 3, 10, 200, 0, 0, 0, 0, 0, 1, 231, 192, 0, 0, 0, 0, 0, 0, 0, 0, 50, 48, 50, 48, 46, 51, 46, 49, 55, 102, 49, 0, 19, 0, 0, 0, 1, 13, 0, 0, 0, 23, 0, 0, 0, 0, 255, 255, 241, 159, 126, 32, 195, 37, 88, 156, 52, 101, 84, 239, 125, 28, 173, 201, 54, 0, 0, 0, 42, 2, 0, 0 };

            var r = DllWrapper.ReadFile(file, 100, buffer, out var actualSize);

            //Console.WriteLine("{{{0}}}", string.Join(", ", buffer));

            Assert.AreEqual(ReturnCode.Success, r);
            Assert.AreEqual(100, actualSize);
            Assert.AreEqual(expectedBuffer, buffer);

            file.Dispose();
        }
    }

    public class SerializedFileTests
    {
        private UnityArchiveHandle archive;

        [OneTimeSetUp]
        public void Setup()
        {
            DllWrapper.Init();
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "assetbundle");
            DllWrapper.MountArchive(path, "archive:/", out archive);
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            archive.Dispose();
            DllWrapper.Cleanup();
        }

        [Test]
        public void OpenSerializedFile_InvalidPath_ReturnError()
        {
            var r = DllWrapper.OpenSerializedFile("bad/path", out _);
            Assert.AreEqual(ReturnCode.FileNotFound, r);
        }

        [Test]
        [Ignore("This test crashes, this condition is not handled properly in Unity")]
        public void OpenSerializedFile_NotSerializedFile_ReturnError()
        {
            var r = DllWrapper.OpenSerializedFile("archive:/CAB-5d40f7cad7c871cf2ad2af19ac542994.resS", out _);

            // It would be better to test for an actual error code instead of !success but it's not possible because of the way
            // the SerializedFile is implemented! When opening a SerializedFile, the header is read and if the version is higher than some value,
            // the kSerializedFileLoadError_HigherSerializedFileVersion error is returned. If it's not, then it will be another error. There's no
            // signature to prevent this kind of issue.
            Assert.AreNotEqual(ReturnCode.Success, r);
        }

        [Test]
        public void OpenSerializedFile_ValidSerializedFile_ReturnSuccess()
        {
            var r = DllWrapper.OpenSerializedFile("archive:/CAB-5d40f7cad7c871cf2ad2af19ac542994", out var file);
            Assert.AreEqual(ReturnCode.Success, r);
            file.Dispose();
        }

        [Test]
        public void CloseSerializedFile_ValidSerializedFile_ReturnSuccess()
        {
            DllWrapper.OpenSerializedFile("archive:/CAB-5d40f7cad7c871cf2ad2af19ac542994", out var file);
            Assert.DoesNotThrow(() => file.Dispose());
        }

        [Test]
        public void CloseSerializedFile_InvalidHandle_ReturnError()
        {
            var r = DllWrapper.CloseSerializedFile(IntPtr.Zero);
            Assert.AreEqual(ReturnCode.InvalidArgument, r);
        }

        [Test]
        public void GetExternalReferenceCount_ValidSerializedFile_ReturnExpectedExternalReferenceCount()
        {
            DllWrapper.OpenSerializedFile("archive:/CAB-5d40f7cad7c871cf2ad2af19ac542994", out var file);

            var r = DllWrapper.GetExternalReferenceCount(file, out var count);
            Assert.AreEqual(ReturnCode.Success, r);
            Assert.AreEqual(3, count);

            file.Dispose();
        }

        [Test]
        public void GetExternalReferenceCount_InvalidHandle_ReturnError()
        {
            var r = DllWrapper.GetExternalReferenceCount(new SerializedFileHandle(), out _);
            Assert.AreEqual(ReturnCode.InvalidArgument, r);
        }

        [Test]
        public void GetExternalReference_StringTooSmall_ReturnError()
        {
            DllWrapper.OpenSerializedFile("archive:/CAB-5d40f7cad7c871cf2ad2af19ac542994", out var file);

            var path = new StringBuilder(10);
            var guid = new StringBuilder(32);
            var r = DllWrapper.GetExternalReference(file, 0, path, 10, guid, out var refType);
            Assert.AreEqual(ReturnCode.DestinationBufferTooSmall, r);

            file.Dispose();
        }

        [Test]
        public void GetExternalReference_ValidSerializedFile_ReturnExpectedExternalReferences()
        {
            DllWrapper.OpenSerializedFile("archive:/CAB-5d40f7cad7c871cf2ad2af19ac542994", out var file);

            var path = new StringBuilder(256);
            var guid = new StringBuilder(32);
            var r = DllWrapper.GetExternalReference(file, 0, path, 256, guid, out var refType);
            Assert.AreEqual(ReturnCode.Success, r);
            Assert.AreEqual("Library/unity default resources", path.ToString());
            Assert.AreEqual("0000000000000000e000000000000000", guid.ToString());
            Assert.AreEqual(ExternalReferenceType.NonAssetType, refType);

            r = DllWrapper.GetExternalReference(file, 1, path, 256, guid, out refType);
            Assert.AreEqual(ReturnCode.Success, r);
            Assert.AreEqual("Resources/unity_builtin_extra", path.ToString());
            Assert.AreEqual("0000000000000000f000000000000000", guid.ToString());
            Assert.AreEqual(ExternalReferenceType.NonAssetType, refType);

            r = DllWrapper.GetExternalReference(file, 2, path, 256, guid, out refType);
            Assert.AreEqual(ReturnCode.Success, r);
            Assert.AreEqual("archive:/CAB-35fce856128a6714740898681ea54bbe/CAB-35fce856128a6714740898681ea54bbe", path.ToString());
            Assert.AreEqual("00000000000000000000000000000000", guid.ToString());
            Assert.AreEqual(ExternalReferenceType.NonAssetType, refType);

            file.Dispose();
        }

        [Test]
        public void GetExternalReference_InvalidHandle_ReturnError()
        {
            var r = DllWrapper.GetExternalReferenceCount(new SerializedFileHandle(), out _);
            Assert.AreEqual(ReturnCode.InvalidArgument, r);
        }

        [Test]
        public void GetObjectCount_ValidSerializedFile_ReturnExpectedObjectCount()
        {
            DllWrapper.OpenSerializedFile("archive:/CAB-5d40f7cad7c871cf2ad2af19ac542994", out var file);

            var r = DllWrapper.GetObjectCount(file, out var count);
            Assert.AreEqual(ReturnCode.Success, r);
            Assert.AreEqual(41, count);

            file.Dispose();
        }

        [Test]
        public void GetObjectCount_InvalidHandle_ReturnError()
        {
            var r = DllWrapper.GetObjectCount(new SerializedFileHandle(), out _);
            Assert.AreEqual(ReturnCode.InvalidArgument, r);
        }

        [Test]
        public void GetObjectInfo_ValidSerializedFile_ReturnExpectedObjectInfo()
        {
            DllWrapper.OpenSerializedFile("archive:/CAB-5d40f7cad7c871cf2ad2af19ac542994", out var file);
            DllWrapper.GetObjectCount(file, out var count);

            var objectInfo = new ObjectInfo[count];
            var r = DllWrapper.GetObjectInfo(file, objectInfo, count);
            Assert.AreEqual(ReturnCode.Success, r);

            // Just make sure that first and last ObjectInfo struct are filled.

            Assert.AreEqual(-8720048570983375440, objectInfo[0].Id);
            Assert.AreEqual(124864, objectInfo[0].Offset);
            Assert.AreEqual(156, objectInfo[0].Size);
            Assert.AreEqual(23, objectInfo[0].TypeId);

            Assert.AreEqual(9001362461581137807, objectInfo[40].Id);
            Assert.AreEqual(199320, objectInfo[39].Offset);
            Assert.AreEqual(24, objectInfo[39].Size);
            Assert.AreEqual(33, objectInfo[39].TypeId);

            file.Dispose();
        }

        [Test]
        public void GetObjectInfo_InvalidHandle_ReturnError()
        {
            var r = DllWrapper.GetObjectInfo(new SerializedFileHandle(), new ObjectInfo[0], 0);
            Assert.AreEqual(ReturnCode.InvalidArgument, r);
        }
    }

    public class TypeTreeTests
    {
        private UnityArchiveHandle      archive;
        private SerializedFileHandle    serializedFile;
        private ObjectInfo[]            objects;

        [OneTimeSetUp]
        public void Setup()
        {
            DllWrapper.Init();
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "assetbundle");
            DllWrapper.MountArchive(path, "archive:/", out archive);

            DllWrapper.OpenSerializedFile("archive:/CAB-5d40f7cad7c871cf2ad2af19ac542994", out serializedFile);
            DllWrapper.GetObjectCount(serializedFile, out var count);
            objects = new ObjectInfo[count];
            DllWrapper.GetObjectInfo(serializedFile, objects, objects.Length);
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            serializedFile.Dispose();
            archive.Dispose();
            DllWrapper.Cleanup();
        }

        [Test]
        public void GetTypeTree_InvalidObjectId_ReturnError()
        {
            var r = DllWrapper.GetTypeTree(serializedFile, 0, out var typeTree);
            Assert.AreEqual(ReturnCode.InvalidObjectId, r);
        }

        [Test]
        public void GetTypeTree_InvalidHandle_ReturnError()
        {
            var r = DllWrapper.GetTypeTree(new SerializedFileHandle(), 0, out _);
            Assert.AreEqual(ReturnCode.InvalidArgument, r);
        }

        [Test]
        public void GetTypeTree_ValidSerializedFile_ReturnSuccess()
        {
            var r = DllWrapper.GetTypeTree(serializedFile, objects[0].Id, out var typeTree);
            Assert.AreEqual(ReturnCode.Success, r);
            Assert.IsFalse(typeTree.IsInvalid);
        }

        [Test]
        public void GetTypeTreeNodeInfo_InvalidHandle_ReturnError()
        {
            var r = DllWrapper.GetTypeTreeNodeInfo(new TypeTreeHandle(), 0, new StringBuilder(), 0, new StringBuilder(), 0, out _, out _, out _, out _, out _, out _);
            Assert.AreEqual(ReturnCode.InvalidArgument, r);
        }

        [Test]
        public void GetTypeTreeNodeInfo_TypeStringTooSmall_ReturnError()
        {
            DllWrapper.GetTypeTree(serializedFile, objects[0].Id, out var typeTree);
            var r = DllWrapper.GetTypeTreeNodeInfo(typeTree, 0, new StringBuilder(1), 1, new StringBuilder(256), 256, out _, out _, out _, out _, out _, out _);
            Assert.AreEqual(ReturnCode.DestinationBufferTooSmall, r);
        }

        [Test]
        public void GetTypeTreeNodeInfo_NameStringTooSmall_ReturnError()
        {
            DllWrapper.GetTypeTree(serializedFile, objects[0].Id, out var typeTree);
            var r = DllWrapper.GetTypeTreeNodeInfo(typeTree, 0, new StringBuilder(256), 256, new StringBuilder(1), 1, out _, out _, out _, out _, out _, out _);
            Assert.AreEqual(ReturnCode.DestinationBufferTooSmall, r);
        }

        [Test]
        public void GetTypeTreeNodeInfo_ValidData_ReturnExpectedValues()
        {
            foreach (var obj in objects)
            {
                DllWrapper.GetTypeTree(serializedFile, obj.Id, out var typeTree);
                var type = new StringBuilder(256);
                var name = new StringBuilder(256);
                var r = DllWrapper.GetTypeTreeNodeInfo(typeTree, 0, type, type.Capacity, name, name.Capacity, out var offset, out var size, out var flags, out var metaFlags, out var firstChildNode, out var nextNode);
                Assert.AreEqual(ReturnCode.Success, r);
                Assert.AreNotEqual("", type.ToString());
                Assert.AreEqual("Base", name.ToString());
                Assert.AreEqual(-1, offset);
                Assert.AreNotEqual(0, size);
                Assert.AreEqual(TypeTreeFlags.None, flags);
                Assert.AreEqual(1, firstChildNode);
                Assert.AreEqual(0, nextNode);
            }
        }

        [Test]
        public void GetTypeTreeNodeInfo_IterateAll_ReturnExpectedValues()
        {
            int ProcessNode(TypeTreeHandle typeTree, int nodeIndex)
            {
                var type = new StringBuilder(256);
                var name = new StringBuilder(256);
                var r = DllWrapper.GetTypeTreeNodeInfo(typeTree, nodeIndex, type, type.Capacity, name, name.Capacity, out var offset, out var size, out var flags, out var metaFlags, out var nextChild, out var nextNode);

                Assert.AreEqual(ReturnCode.Success, r);
                Assert.AreNotEqual("", type.ToString());
                Assert.AreNotEqual("", name.ToString());
                Assert.GreaterOrEqual(offset, -1);
                Assert.GreaterOrEqual(size, -1);

                while (nextChild != 0)
                {
                    nextChild = ProcessNode(typeTree, nextChild);
                }

                return nextNode;
            }

            foreach (var obj in objects)
            {
                DllWrapper.GetTypeTree(serializedFile, obj.Id, out var typeTree);
                var type = new StringBuilder(256);
                var name = new StringBuilder(256);
                var r = DllWrapper.GetTypeTreeNodeInfo(typeTree, 0, type, type.Capacity, name, name.Capacity, out var offset, out var size, out var flags, out var metaFlags, out var firstChildNode, out var nextNode);

                ProcessNode(typeTree, firstChildNode);
            }
        }
    }
}
