using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace UnityDataTools.TestCommon;

public class ExpectedData
{
    private Dictionary<string, object> m_ExpectedValues = new();

    public void Add(string key, object value)
    {
        m_ExpectedValues[key] = value;
    }

    public object Get(string key)
    {
        return m_ExpectedValues[key];
    }
    
    public void Save(string path)
    {
        var settings = new JsonSerializerSettings();
        settings.TypeNameHandling = TypeNameHandling.All;
        
        File.WriteAllText(Path.Combine(path, "ExpectedValues.json"), JsonConvert.SerializeObject(m_ExpectedValues, Formatting.Indented, settings));
    }

    public void Load(string path)
    {
        path = Path.Combine(path, "ExpectedValues.json");

        if (!File.Exists(path))
        {
            return;
        }
        
        var settings = new JsonSerializerSettings();
        settings.TypeNameHandling = TypeNameHandling.All;
        
        m_ExpectedValues = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(path), settings);
    }
}
