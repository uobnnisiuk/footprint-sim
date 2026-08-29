using System.Text.Json;
using FootprintSim.Serialization;

namespace FootprintSim.Scenarios;

public static class ScenarioLoader
{
    public static ScenarioDefinition Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var json = File.ReadAllText(path);
        var scenario = JsonSerializer.Deserialize<ScenarioDefinition>(json, JsonDefaults.Options)
            ?? throw new InvalidDataException($"Scenario '{path}' was empty.");

        ScenarioValidator.Validate(scenario);
        return scenario;
    }
}
