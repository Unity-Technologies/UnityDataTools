using NUnit.Framework;

namespace UnityDataTools.TestCommon;

public class Context
{
    // e.g. <CurrentProjectFolder>/bin/Debug/net6.0/Data/AssetBundles/2021.1.0f1
    public string UnityDataFolder { get; }
    
    // e.g. 2021.1.0f1
    public string UnityDataVersion { get; }
    
    // e.g. <CurrentProjectFolder>/bin/Debug/net6.0/Data
    public string TestDataFolder { get; }
    
    // e.g. <CurrentProjectFolder>/bin/Debug/net6.0/ExpectedData/2021.1.0f1
    public string ExpectedDataFolder { get; }
    
    public ExpectedData ExpectedData { get; } = new();

    public Context(string folder)
    {
        var di = new DirectoryInfo(folder);
        
        UnityDataFolder = folder;
        UnityDataVersion = di.Name;
        TestDataFolder = di.Parent.Parent.FullName;
        ExpectedDataFolder = Path.Combine(di.Parent.Parent.Parent.FullName, "ExpectedData", UnityDataVersion);
        
        ExpectedData.Load(ExpectedDataFolder);
    }

    public override string ToString()
    {
        // To show up nicely in the test report.
        return UnityDataVersion;
    }
}
