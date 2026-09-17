using System;
using System.IO;
using System.Text;

namespace UnityDataTools.FileSystem;

// This is the main entry point. Provides methods to mount archives and open files.
public static class UnityFileSystem
{
    // Serialized file version 26 (Unity 6.7) needs the shared subtree and registry frame entry
    // points, which arrived together in this library version. Older files are read through the
    // same library, so there is no reason to keep a fallback for an earlier one.
    public const int RequiredDllVersion = 2;

    public static void Init()
    {
        // Initialize the native library.
        var r = DllWrapper.Init();

        if (r != ReturnCode.Success && r != ReturnCode.AlreadyInitialized)
        {
            HandleErrors(r);
        }

        var dllVersion = GetDllVersion();
        if (dllVersion < RequiredDllVersion)
        {
            throw new NotSupportedException(
                $"UnityFileSystemApi version {dllVersion} is too old; version {RequiredDllVersion} or newer is required. " +
                "Replace the UnityFileSystemApi library shipped beside UnityDataTool with one from Unity 6.7 or newer.");
        }
    }

    public static void Cleanup()
    {
        // Uninitialize the native library.
        var r = DllWrapper.Cleanup();

        if (r != ReturnCode.Success && r != ReturnCode.NotInitialized)
        {
            HandleErrors(r);
        }
    }

    public static UnityArchive MountArchive(string path, string mountPoint)
    {
        var r = DllWrapper.MountArchive(path, mountPoint, out var handle);
        HandleErrors(r, path);

        return new UnityArchive() { m_Handle = handle };
    }

    public static UnityFile OpenFile(string path)
    {
        var r = DllWrapper.OpenFile(path, out var handle);
        UnityFileSystem.HandleErrors(r, path);

        return new UnityFile() { m_Handle = handle };
    }

    public static long AddTypeTreeSourceFromFile(string path)
    {
        var r = DllWrapper.AddTypeTreeSourceFromFile(path, out var handle);
        HandleErrors(r, path);
        return handle;
    }

    public static void RemoveTypeTreeSource(long handle)
    {
        var r = DllWrapper.RemoveTypeTreeSource(handle);
        HandleErrors(r);
    }

    public static int GetDllVersion()
    {
        var r = DllWrapper.GetDllVersion(out var version);
        HandleErrors(r);
        return version;
    }

    public static string GetUnityVersion()
    {
        var version = new StringBuilder(256);
        var r = DllWrapper.GetUnityVersion(version, version.Capacity);
        HandleErrors(r);
        return version.ToString();
    }

    public static SerializedFile OpenSerializedFile(string path)
    {
        var r = DllWrapper.OpenSerializedFile(path, out var handle);

        if (r == ReturnCode.UnknownError)
            throw new SerializedFileOpenException(path);

        UnityFileSystem.HandleErrors(r, path);

        return new SerializedFile() { m_Handle = handle };
    }

    internal static void HandleErrors(ReturnCode returnCode, string filename = "")
    {
        switch (returnCode)
        {
            case ReturnCode.AlreadyInitialized:
                throw new InvalidOperationException("UnityFileSystem is already initialized.");

            case ReturnCode.NotInitialized:
                throw new InvalidOperationException("UnityFileSystem is not initialized.");

            case ReturnCode.FileNotFound:
                throw new FileNotFoundException("File not found.", filename);

            case ReturnCode.FileFormatError:
                throw new NotSupportedException($"Invalid file format reading {filename}.");

            case ReturnCode.InvalidArgument:
                throw new ArgumentException();

            case ReturnCode.HigherSerializedFileVersion:
                throw new NotSupportedException("SerializedFile version not supported.");

            case ReturnCode.DestinationBufferTooSmall:
                throw new ArgumentException("Destination buffer too small.");

            case ReturnCode.InvalidObjectId:
                throw new ArgumentException("Invalid object id.");

            case ReturnCode.UnknownError:
                throw new Exception("Unknown error.");

            case ReturnCode.FileError:
                throw new IOException("File operation error.");

            case ReturnCode.TypeNotFound:
                throw new ArgumentException("Type not found.");

            case ReturnCode.HigherTypeTreeVersion:
                throw new NotSupportedException($"A TypeTree in {filename} was written by a newer version of Unity.");

            // Every node is read through the subtree API, so the native side has no reason to ask
            // for it. Reaching this means a walk was added that bypasses TypeTreeNode.
            case ReturnCode.RequiresSubtreeApi:
                throw new InvalidOperationException("Shared subtree reference read through the non-subtree API.");
        }
    }
}
