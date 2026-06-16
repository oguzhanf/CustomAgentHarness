namespace YourCustomAgentHarness.Shared.Manifest;

using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public static class BlueprintLoader
{
    public static IDeserializer CreateDeserializer() =>
        new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

    public static AgentBlueprint LoadYaml(string path)
    {
        var yaml = File.ReadAllText(path);
        return CreateDeserializer().Deserialize<AgentBlueprint>(yaml)
               ?? throw new InvalidOperationException($"Failed to deserialize blueprint at {path}");
    }

    public static IReadOnlyList<AgentBlueprint> LoadAll(string blueprintsDir)
    {
        if (!Directory.Exists(blueprintsDir)) return Array.Empty<AgentBlueprint>();
        return Directory.GetFiles(blueprintsDir, "*.harness.yaml")
            .Select(LoadYaml)
            .ToList();
    }
}
