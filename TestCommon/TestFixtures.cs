using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace UnityDataTools.TestCommon;

// Base class that facilitates iterating through sub-sub-folders
// inside the Data location.  E.g. GetContexts("AssetBundles")
// finds "TestCommon/Data/AssetBundles/2019.4.0f1", "TestCommon/Data/AssetBundles/2020.3.0f1" etc.
public class BaseTestFixture
{
    protected Context Context { get; }

    private static Dictionary<string, List<Context>> m_Cache = new();

    public BaseTestFixture(Context context)
    {
        Context = context;
    }

    // Tests that have files that record the expected results for each version
    // of Unity can override this method to regenerate those expected results.
    protected virtual void OnLoadExpectedData(Context context)
    {
    }

    [OneTimeSetUp]
    public void LoadExpectedData()
    {
        OnLoadExpectedData(Context);

        // Load json file with the expected results for a test based on
        // folder structure convention (e.g. ExpectedData/<UnityVersion>/ExpectedVersions.json)
        Context.ExpectedData.Load(Context.ExpectedDataFolder);
    }

    protected static IEnumerable<Context> GetContexts(string dataFolder)
    {
        if (m_Cache.TryGetValue(dataFolder, out var cases))
        {
            return cases;
        }

        cases = new List<Context>();
        m_Cache[TestContext.CurrentContext.TestDirectory] = cases;

        var subfolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", dataFolder);

        foreach (var folder in Directory.EnumerateDirectories(subfolder))
        {
            cases.Add(new Context(folder));
        }

        return cases;
    }
}

// Test fixture that repeats the tests for each folder inside TestCommon/Data/AssetBundles.
// Each sub-folder is expected to have results of an AssetBundle build repeated with a
// different version of Unity.
[TestFixtureSource(typeof(AssetBundleTestFixture), nameof(GetContexts))]
public class AssetBundleTestFixture : BaseTestFixture
{
    public AssetBundleTestFixture(Context context) : base(context)
    {
    }

    public static IEnumerable<Context> GetContexts()
    {
        return BaseTestFixture.GetContexts("AssetBundles");
    }
}

[TestFixtureSource(typeof(PlayerDataTestFixture), nameof(GetContexts))]
public class PlayerDataTestFixture : BaseTestFixture
{
    public PlayerDataTestFixture(Context context) : base(context)
    {
    }

    public static IEnumerable<Context> GetContexts()
    {
        return BaseTestFixture.GetContexts("PlayerData");
    }
}
