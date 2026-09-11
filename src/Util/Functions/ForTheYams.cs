using System.IO;
using YamlDotNet.Serialization;

namespace DadsEZContainers.Util.Functions;

public static class YamlUtils
{
    internal static void ReadYaml(string yamlInput)
    {
        if (string.IsNullOrWhiteSpace(yamlInput))
        {
            DadsEZContainersPlugin.yamlData = new Dictionary<string, Dictionary<string, List<string>>>();
            return;
        }

        IDeserializer deserializer = new DeserializerBuilder().Build();
        DadsEZContainersPlugin.yamlData = deserializer.Deserialize<Dictionary<string, Dictionary<string, List<string>>>>(yamlInput);
        DadsEZContainersPlugin.DadsEZContainersLogger.LogIfReleaseAndDebugEnable($"yamlData:\n{yamlInput}");
    }

    internal static void ParseGroups()
    {
        if (DadsEZContainersPlugin.yamlData.TryGetValue("groups", out Dictionary<string, List<string>> groupData))
        {
            foreach (KeyValuePair<string, List<string>> group in groupData)
            {
                DadsEZContainersPlugin.groups[group.Key] = new HashSet<string>(group.Value);
            }
        }
    }

    public static void WriteYaml(string filePath)
    {
        ISerializer serializer = new SerializerBuilder().Build();
        using StreamWriter output = new(filePath);
        serializer.Serialize(output, DadsEZContainersPlugin.yamlData);

        // Serialize the data again to YAML format
        string serializedData = serializer.Serialize(DadsEZContainersPlugin.yamlData);

        // Append the serialized YAML data to the file
        File.AppendAllText(filePath, serializedData);
    }
}