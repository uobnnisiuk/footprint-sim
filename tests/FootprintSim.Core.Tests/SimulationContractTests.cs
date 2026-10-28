using System.Text.Json;
using FootprintSim.Evidence;
using FootprintSim.Events;
using FootprintSim.Inference;
using FootprintSim.Scenarios;
using FootprintSim.Serialization;
using FootprintSim.Simulation;
using Xunit;

namespace FootprintSim.Core.Tests;

public sealed class SimulationContractTests
{
    [Fact]
    public void SameSeedProducesIdenticalEventAndObservationJson()
    {
        var scenario = LoadScenario("s1_debris_transport");
        var engine = new SimulationEngine();

        var first = engine.Run(scenario, 42);
        var second = engine.Run(scenario, 42);

        Assert.Equal(
            JsonDefaults.Serialize(first.GroundTruthEvents),
            JsonDefaults.Serialize(second.GroundTruthEvents));
        Assert.Equal(
            JsonDefaults.Serialize(first.ObservationSet),
            JsonDefaults.Serialize(second.ObservationSet));
    }

    [Theory]
    [InlineData("s0_walking_baseline", 0)]
    [InlineData("s0_walking_baseline", 42)]
    [InlineData("s1_debris_transport", 0)]
    [InlineData("s1_debris_transport", 42)]
    public void SameSeedProducesIdenticalObservationJsonForSampleScenarios(string scenarioDirectory, int seed)
    {
        var scenario = LoadScenario(scenarioDirectory);
        var engine = new SimulationEngine();

        var first = engine.Run(scenario, seed);
        var second = engine.Run(scenario, seed);

        Assert.Equal(
            JsonDefaults.Serialize(first.ObservationSet),
            JsonDefaults.Serialize(second.ObservationSet));
    }

    [Theory]
    [InlineData("s0_walking_baseline")]
    [InlineData("s1_debris_transport")]
    public void EventTicksAndSequencesHaveStableTotalOrder(string scenarioDirectory)
    {
        var run = new SimulationEngine().Run(LoadScenario(scenarioDirectory), 42);

        Assert.Equal(Enumerable.Range(0, run.GroundTruthEvents.Count),
            run.GroundTruthEvents.Select(simulationEvent => simulationEvent.Sequence));
        Assert.Equal(
            run.GroundTruthEvents.Count,
            run.GroundTruthEvents.Select(simulationEvent => simulationEvent.Id).Distinct().Count());

        var eventsById = run.GroundTruthEvents.ToDictionary(
            simulationEvent => simulationEvent.Id,
            StringComparer.Ordinal);

        for (var index = 1; index < run.GroundTruthEvents.Count; index++)
        {
            var previous = run.GroundTruthEvents[index - 1];
            var current = run.GroundTruthEvents[index];
            Assert.True(previous.Tick <= current.Tick);
            Assert.True(previous.Tick < current.Tick || previous.Sequence < current.Sequence);
        }

        foreach (var simulationEvent in run.GroundTruthEvents.Where(item => item.CauseEventId is not null))
        {
            Assert.True(eventsById.TryGetValue(simulationEvent.CauseEventId!, out var cause));
            Assert.True(cause.Sequence < simulationEvent.Sequence);
        }
    }

    [Fact]
    public void PersonAndDeviceHaveIndependentPositionsAfterSeparation()
    {
        var run = new SimulationEngine().Run(LoadScenario("s1_debris_transport"), 42);

        var person = run.GroundTruthFinalEntityStates["person-mika"];
        var device = run.GroundTruthFinalEntityStates["device-mika-phone"];

        Assert.NotEqual(person.Position, device.Position);
        Assert.Equal("deposition-zone", person.Position.RegionId);
        Assert.Equal("downstream-bank", device.Position.RegionId);

        var separation = Assert.Single(run.GroundTruthEvents, simulationEvent =>
            simulationEvent.Kind == EventKind.PersonDeviceSeparated);
        Assert.Equal(
            "slope-channel",
            separation.RelatedPositions["device-mika-phone"].RegionId);
    }

    [Fact]
    public void HazardTransportMovesPersonBeyondNormalWalkingRange()
    {
        var scenario = LoadScenario("s1_debris_transport");
        var run = new SimulationEngine().Run(scenario, 42);
        var initial = scenario.Entities.Persons.Single(
            person => person.Id == scenario.TargetPersonId).InitialPosition;
        var finalPosition = run.GroundTruthFinalEntityStates[scenario.TargetPersonId].Position;

        Assert.True(finalPosition.PlanarDistanceTo(initial) > scenario.NormalWalkingRadius);
        Assert.Contains(run.GroundTruthEvents, simulationEvent =>
            simulationEvent.Kind == EventKind.HazardTransported &&
            simulationEvent.SubjectIds.Contains(scenario.TargetPersonId));

        var summary = Infer(scenario, run.ObservationSet);
        Assert.True(summary.HazardTransportExpandsSearchZone);
        Assert.Contains("deposition-zone", summary.HazardTransportEnvelopeRegionIds);
        Assert.DoesNotContain("deposition-zone", summary.NormalMovementRegionIds);
    }

    [Fact]
    public void S1SearchSummaryUsesPersonEvidenceWithoutTreatingDeviceFixAsPersonLocation()
    {
        var scenario = LoadScenario("s1_debris_transport");
        var run = new SimulationEngine().Run(scenario, 42);
        var summary = Infer(scenario, run.ObservationSet);

        Assert.Equal(EvidenceState.SupportedButUncertain, summary.EvidenceState);
        Assert.Equal("hillside-building", summary.LastConfirmedPosition?.RegionId);
        Assert.Equal("slope-channel", summary.LastReportedPosition?.RegionId);
        Assert.Equal(EvidenceState.SupportedButUncertain, summary.LastReportedPositionState);
        Assert.DoesNotContain(run.ObservationSet.Observations, observation =>
            observation.SubjectIds.Contains("person-mika") &&
            observation.ReportedRegionId == "downstream-bank");

        Assert.Collection(
            summary.SearchPriorityZones,
            zone =>
            {
                Assert.Equal(1, zone.Priority);
                Assert.Equal("last-reported-person-observation", zone.Basis);
                Assert.Equal(EvidenceState.SupportedButUncertain, zone.EvidenceState);
                Assert.Equal(["slope-channel"], zone.RegionIds);
            },
            zone =>
            {
                Assert.Equal(2, zone.Priority);
                Assert.Equal("declared-hazard-transport-envelope", zone.Basis);
                Assert.Contains("deposition-zone", zone.RegionIds);
            },
            zone =>
            {
                Assert.Equal(3, zone.Priority);
                Assert.Equal("normal-movement-baseline-from-last-confirmed-position", zone.Basis);
                Assert.DoesNotContain("deposition-zone", zone.RegionIds);
            });
    }

    [Fact]
    public void ObservationJsonDoesNotContainPrivateGroundTruthFieldsOrPayload()
    {
        var scenario = LoadScenario("s1_debris_transport");
        var run = new SimulationEngine().Run(scenario, 42);

        var json = JsonDefaults.Serialize(run.ObservationSet);

        Assert.DoesNotContain("causeEventId", json);
        Assert.DoesNotContain("relatedPositions", json);
        Assert.DoesNotContain("groundTruth", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("speedPerTick", json);
        Assert.DoesNotContain("stopProbabilityApplied", json);
        Assert.Contains("publicPayload", json);

        var inferenceParameters = typeof(IRescueInference)
            .GetMethod(nameof(IRescueInference.Infer))!
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();
        Assert.DoesNotContain(typeof(SimulationRun), inferenceParameters);
        Assert.DoesNotContain(typeof(SimulationEvent), inferenceParameters);
    }

    [Fact]
    public void EveryEvidenceBoundaryStateRoundTripsThroughJson()
    {
        foreach (var expected in Enum.GetValues<EvidenceState>())
        {
            var original = new ObservationRecord
            {
                ObservationId = "obs-contract-test",
                ObservedTick = 1,
                AvailableTick = 2,
                EvidenceType = "contract-test",
                SubjectIds = ["person-test"],
                State = expected
            };

            var json = JsonDefaults.Serialize(original);
            var restored = JsonSerializer.Deserialize<ObservationRecord>(json, JsonDefaults.Options);

            Assert.NotNull(restored);
            Assert.Equal(expected, restored.State);
        }
    }

    [Fact]
    public void InferencePreservesIndeterminateUnsupportedAndConflictingQueryStates()
    {
        var scenario = LoadScenario("s1_debris_transport");
        var empty = new ObservationSet
        {
            ScenarioId = scenario.Id,
            Seed = 42
        };
        var unrelated = empty with
        {
            Observations =
            [
                Observation("other-person", EvidenceState.Confirmed)
            ]
        };
        var conflicting = empty with
        {
            Observations =
            [
                Observation(scenario.TargetPersonId, EvidenceState.Conflicting)
            ]
        };

        Assert.Equal(EvidenceState.Indeterminate, Infer(scenario, empty).EvidenceState);
        Assert.Equal(EvidenceState.UnsupportedForQuery, Infer(scenario, unrelated).EvidenceState);
        Assert.Equal(EvidenceState.Conflicting, Infer(scenario, conflicting).EvidenceState);
    }

    [Theory]
    [InlineData("s0_walking_baseline", "s0-walking-baseline")]
    [InlineData("s1_debris_transport", "s1-debris-transport")]
    public void SampleScenariosLoadAndValidate(string scenarioDirectory, string expectedId)
    {
        var scenario = LoadScenario(scenarioDirectory);

        Assert.Equal(expectedId, scenario.Id);
        Assert.NotEmpty(scenario.Regions);
        Assert.NotEmpty(scenario.Steps);
    }

    [Fact]
    public void S0ReportsLastConfirmedLocationAndNormalMovementRegions()
    {
        var scenario = LoadScenario("s0_walking_baseline");
        var run = new SimulationEngine().Run(scenario, 42);

        var summary = Infer(scenario, run.ObservationSet);

        Assert.Equal(EvidenceState.SupportedButUncertain, summary.EvidenceState);
        Assert.Equal("building-entry", summary.LastConfirmedPosition?.RegionId);
        Assert.NotEmpty(summary.NormalMovementRegionIds);
        Assert.Empty(summary.HazardTransportEnvelopeRegionIds);
    }

    [Theory]
    [InlineData("radius", double.NaN)]
    [InlineData("radius", double.PositiveInfinity)]
    [InlineData("radius", double.NegativeInfinity)]
    [InlineData("passability", double.NaN)]
    [InlineData("passability", double.PositiveInfinity)]
    [InlineData("passability", double.NegativeInfinity)]
    [InlineData("travelCost", double.NaN)]
    [InlineData("travelCost", double.PositiveInfinity)]
    [InlineData("travelCost", double.NegativeInfinity)]
    public void ScenarioRejectsNonFiniteRadiusAndTopology(string field, double value)
    {
        var scenario = CreateRadiusAndTopologyScenario(
            field == "radius" ? value : 1,
            field == "passability" ? value : 1,
            field == "travelCost" ? value : 1);

        var exception = Assert.Throws<InvalidDataException>(() => ScenarioValidator.Validate(scenario));

        var expectedMessage = field switch
        {
            "radius" => "Normal walking radius must be finite.",
            "passability" => "Region 'validation-region' passability must be between 0 and 1.",
            _ => "Region 'validation-region' connection to 'validation-region' travel cost must be finite."
        };
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Theory]
    [InlineData(0, 0, double.MinValue)]
    [InlineData(double.MaxValue, 1, double.MaxValue)]
    [InlineData(1, 0.5, 0)]
    [InlineData(1, 1, -1)]
    public void ScenarioAcceptsFiniteRadiusAndTopologyBoundaries(
        double radius, double passability, double travelCost)
    {
        ScenarioValidator.Validate(CreateRadiusAndTopologyScenario(radius, passability, travelCost));
    }

    [Theory]
    [InlineData(-1, 1, "Normal walking radius cannot be negative.")]
    [InlineData(1, -0.1, "Region 'validation-region' passability must be between 0 and 1.")]
    [InlineData(1, 1.1, "Region 'validation-region' passability must be between 0 and 1.")]
    public void ScenarioPreservesFiniteRadiusAndPassabilityRejections(
        double radius, double passability, string expectedMessage)
    {
        var scenario = CreateRadiusAndTopologyScenario(radius, passability, 1);

        var exception = Assert.Throws<InvalidDataException>(() => ScenarioValidator.Validate(scenario));

        Assert.Equal(expectedMessage, exception.Message);
    }

    private static ScenarioDefinition CreateRadiusAndTopologyScenario(
        double radius, double passability, double travelCost)
    {
        var baseline = LoadScenario("s0_walking_baseline");
        return new ScenarioDefinition
        {
            Id = "radius-and-topology-validation",
            Title = "Radius and topology validation",
            Description = "Synthetic validator test",
            TargetPersonId = baseline.TargetPersonId,
            NormalWalkingRadius = radius,
            Entities = baseline.Entities,
            Regions = baseline.Regions.Append(new FootprintSim.Model.RegionDefinition
            {
                Id = "validation-region",
                Type = FootprintSim.Model.RegionType.Outdoor,
                Center = new FootprintSim.Model.Position(0, 0, 0, "validation-region"),
                Passability = passability,
                Connections =
                [
                    new FootprintSim.Model.RegionConnection
                    {
                        ToRegionId = "validation-region",
                        TravelCost = travelCost
                    }
                ]
            }).ToArray()
        };
    }

    private static ObservationRecord Observation(string subjectId, EvidenceState state)
    {
        return new ObservationRecord
        {
            ObservationId = $"obs-{subjectId}-{state}",
            ObservedTick = 1,
            AvailableTick = 1,
            EvidenceType = "contract-test",
            SubjectIds = [subjectId],
            State = state
        };
    }

    private static SearchZoneSummary Infer(
        ScenarioDefinition scenario,
        ObservationSet observations)
    {
        return new BasicRescueInference().Infer(
            new RescueContext
            {
                ScenarioId = scenario.Id,
                TargetPersonId = scenario.TargetPersonId,
                NormalWalkingRadius = scenario.NormalWalkingRadius,
                PublicRegions = scenario.Regions,
                DeclaredHazardEnvelopeRegionIds = scenario.HazardTransportEnvelopeRegionIds
            },
            observations);
    }

    private static ScenarioDefinition LoadScenario(string directory)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null &&
               !File.Exists(Path.Combine(current.FullName, "FootprintSim.sln")))
        {
            current = current.Parent;
        }

        Assert.NotNull(current);
        return ScenarioLoader.Load(Path.Combine(
            current.FullName,
            "scenarios",
            directory,
            "scenario.json"));
    }
}
