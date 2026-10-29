using System.Text.Json;
using FootprintSim.Evidence;
using FootprintSim.Evaluation;
using FootprintSim.Hazards;
using FootprintSim.Inference;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;
using FootprintSim.Interactions.Truth;
using FootprintSim.Scenarios;
using FootprintSim.Serialization;
using FootprintSim.Simulation;
using Xunit;

namespace FootprintSim.Core.Tests;

public sealed class M1C3Tests
{
    [Fact]
    public void S3DefinesTargetSpecificSearchSpaceAndAllEvidenceConditions()
    {
        var scenario = LoadS3();

        Assert.Equal(3, scenario.SchemaVersion);
        Assert.Equal(10, scenario.EvaluationSearchableRegionIds.Count);
        Assert.DoesNotContain("person-b-shelter", scenario.EvaluationSearchableRegionIds);
        Assert.Equal(
            Enum.GetValues<WeftEvidenceCondition>().OrderBy(value => value),
            scenario.WeftEvaluation!.ConditionCycle.OrderBy(value => value));

        var result = RunBatch([0]);
        Assert.All(result.Runs, run => Assert.Equal(10, run.SearchableRegionCount));
    }

    [Fact]
    public void S3SeparatesActualEncounterObservationAndAvailableEvidence()
    {
        var confirmed = new SimulationEngine().Run(LoadS3(), 0);
        var unavailable = new SimulationEngine().Run(LoadS3(), 4);
        var deviceOnly = new SimulationEngine().Run(LoadS3(), 5);

        var occurrence = Assert.Single(confirmed.GroundTruthInteractionOccurrences);
        Assert.IsType<InteractionOccurrence>(occurrence);
        Assert.Equal(InteractionVisibility.PrivateTruth, occurrence.Visibility);
        var observation = Assert.Single(confirmed.InteractionObservations!.Observations);
        Assert.Null(observation.ReferencedInteractionId);
        Assert.NotEqual(occurrence.GetType(), observation.GetType());
        Assert.Single(confirmed.CrossThreadConstraints!.Constraints);

        Assert.Single(unavailable.GroundTruthInteractionOccurrences);
        Assert.Equal(
            InteractionVisibility.PrivateEvidence,
            Assert.Single(unavailable.InteractionObservations!.Observations).Visibility);
        Assert.Empty(unavailable.CrossThreadConstraints!.PubliclyAvailableAt(30).Constraints);

        Assert.Single(deviceOnly.GroundTruthInteractionOccurrences);
        Assert.Empty(deviceOnly.InteractionObservations!.Observations);
        Assert.Empty(deviceOnly.CrossThreadConstraints!.Constraints);
        Assert.DoesNotContain(
            typeof(SearchStrategyInput).GetProperties(),
            property => property.PropertyType == typeof(InteractionOccurrence));
        Assert.DoesNotContain(
            typeof(RescueContext).GetProperties(),
            property => property.PropertyType == typeof(WeftEvaluationDefinition));
        var publicInputJson = JsonDefaults.Serialize(Input(LoadS3(), confirmed, 0));
        Assert.DoesNotContain("encounterRegionId", publicInputJson, StringComparison.Ordinal);
        Assert.DoesNotContain("conditionCycle", publicInputJson, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyCrossThreadStrategyConsumesCrossThreadConstraints()
    {
        var scenario = LoadS3();
        var run = new SimulationEngine().Run(scenario, 0);
        var input = Input(scenario, run, 30);
        var strategies = new ISearchStrategy[]
        {
            new LastConfirmedOnlyStrategy(),
            new HazardAwareStrategy(),
            new OwnEvidenceConstrainedStrategy(),
            new CrossThreadConstrainedStrategy()
        };

        var results = strategies.Select(strategy => strategy.Evaluate(input)).ToArray();
        Assert.All(results.Take(3), result => Assert.Empty(result.CrossThreadConstraintAudits));
        Assert.NotEmpty(results[3].CrossThreadConstraintAudits);
        Assert.True(results[3].PreferredCandidateRegionIds.Count <
            results[2].PreferredCandidateRegionIds.Count);
    }

    [Fact]
    public void ConfirmedAndUncertainConstraintsPreserveOuterSafety()
    {
        var scenario = LoadS3();
        var confirmedRun = new SimulationEngine().Run(scenario, 0);
        var uncertainRun = new SimulationEngine().Run(scenario, 1);
        var confirmed = new CrossThreadConstrainedStrategy().Evaluate(
            Input(scenario, confirmedRun, 30));
        var uncertain = new CrossThreadConstrainedStrategy().Evaluate(
            Input(scenario, uncertainRun, 30));
        var uncertainOwn = new OwnEvidenceConstrainedStrategy().Evaluate(
            Input(scenario, uncertainRun, 30));

        Assert.Equal(confirmed.PreferredCandidateRegionIds, confirmed.OuterPlausibleCandidateRegionIds);
        Assert.Contains(confirmed.CrossThreadConstraintAudits, audit =>
            audit.Disposition == ConstraintApplicationDisposition.Narrow);
        Assert.True(uncertain.PreferredCandidateRegionIds.Count <
            uncertain.OuterPlausibleCandidateRegionIds.Count);
        Assert.Equal(
            uncertainOwn.OuterPlausibleCandidateRegionIds,
            uncertain.OuterPlausibleCandidateRegionIds);
        Assert.Contains(uncertain.CrossThreadConstraintAudits, audit =>
            audit.Disposition == ConstraintApplicationDisposition.Branch &&
            audit.AlternativesRetained);
    }

    [Fact]
    public void DelayedConstraintChangesNothingBeforeAvailabilityAndAppliesAtBoundary()
    {
        var scenario = LoadS3();
        var run = new SimulationEngine().Run(scenario, 2);
        var own = new OwnEvidenceConstrainedStrategy();
        var cross = new CrossThreadConstrainedStrategy();

        var beforeOwn = own.Evaluate(Input(scenario, run, 15));
        var beforeCross = cross.Evaluate(Input(scenario, run, 15));
        var availableOwn = own.Evaluate(Input(scenario, run, 16));
        var availableCross = cross.Evaluate(Input(scenario, run, 16));

        Assert.Equal(beforeOwn.CandidateRegionIds, beforeCross.CandidateRegionIds);
        Assert.Empty(beforeCross.CrossThreadConstraintAudits);
        Assert.True(availableCross.PreferredCandidateRegionIds.Count <
            availableOwn.PreferredCandidateRegionIds.Count);
        Assert.All(availableCross.CrossThreadConstraintAudits, audit =>
            Assert.Equal(16, audit.ApplicationTick));
    }

    [Fact]
    public void ConflictNeverCollapsesOuterCandidates()
    {
        var scenario = LoadS3();
        var run = new SimulationEngine().Run(scenario, 3);
        var own = new OwnEvidenceConstrainedStrategy().Evaluate(Input(scenario, run, 30));
        var cross = new CrossThreadConstrainedStrategy().Evaluate(Input(scenario, run, 30));

        Assert.Empty(cross.PreferredCandidateRegionIds);
        Assert.Equal(own.OuterPlausibleCandidateRegionIds, cross.OuterPlausibleCandidateRegionIds);
        Assert.NotEmpty(cross.ConflictedCandidateRegionIds);
        Assert.Contains(cross.CrossThreadConstraintAudits, audit =>
            audit.Disposition == ConstraintApplicationDisposition.MarkConflict &&
            audit.ConflictDetected);
    }

    [Theory]
    [InlineData(4, WeftEvidenceCondition.Unavailable)]
    [InlineData(5, WeftEvidenceCondition.DeviceOnly)]
    public void UnavailableAndDeviceOnlyConditionsDoNotNarrowPersonWarp(
        int seed,
        WeftEvidenceCondition condition)
    {
        var scenario = LoadS3();
        var run = new SimulationEngine().Run(scenario, seed);
        var input = Input(scenario, run, 30);
        var own = new OwnEvidenceConstrainedStrategy().Evaluate(input);
        var cross = new CrossThreadConstrainedStrategy().Evaluate(input);

        Assert.Equal(condition, run.WeftEvidenceCondition);
        Assert.Equal(own.CandidateRegionIds, cross.CandidateRegionIds);
        Assert.Empty(cross.CrossThreadConstraintAudits);
        Assert.Empty(cross.ConfirmedPersonRegionIds);
    }

    [Fact]
    public void ExactTickAndInclusiveIntervalRemainRecoverableInS3Audits()
    {
        var scenario = LoadS3();
        var exact = new CrossThreadConstrainedStrategy().Evaluate(
            Input(scenario, new SimulationEngine().Run(scenario, 0), 30));
        var interval = new CrossThreadConstrainedStrategy().Evaluate(
            Input(scenario, new SimulationEngine().Run(scenario, 6), 30));

        Assert.All(exact.CrossThreadConstraintAudits, audit =>
            Assert.Equal(ConstraintTemporalKind.ExactTick, audit.ConstraintTemporalKind));
        Assert.All(interval.CrossThreadConstraintAudits, audit =>
        {
            Assert.Equal(ConstraintTemporalKind.InclusiveInterval, audit.ConstraintTemporalKind);
            Assert.Equal(8, audit.SourceObservedTimeWindow.StartTick);
            Assert.Equal(9, audit.SourceObservedTimeWindow.EndTick);
            Assert.Equal(InteractionRelationKind.PersonPersonEncounter, audit.RelationKind);
            Assert.NotEmpty(audit.SourceParticipants);
            Assert.NotEmpty(audit.Provenance);
        });
    }

    [Fact]
    public void BatchPairsSameTruthAndAggregatesEachCondition()
    {
        var result = RunBatch(Enumerable.Range(0, 12).ToArray());

        foreach (var seedGroup in result.Runs.GroupBy(run => run.Seed))
        {
            Assert.Single(seedGroup.Select(run => run.TruthOutcomeIdentifier).Distinct());
            Assert.Single(seedGroup.Select(run => run.RealizedPersonFinalRegionId).Distinct());
            Assert.Single(seedGroup.Select(run => JsonSerializer.Serialize(
                run.RealizedDeviceFinalRegionIds)).Distinct());
        }

        var allPairs = Assert.Single(result.Aggregate.PairedWeftGroups, group =>
            group.Condition is null);
        Assert.Equal(12, allPairs.PairCount);
        Assert.Equal(6, result.Aggregate.PairedWeftGroups.Count(group =>
            group.Condition is not null));
        Assert.All(result.Aggregate.TruthGroups.Single().WeftEvidenceConditionCounts,
            item => Assert.Equal(2, item.Count));
    }

    [Fact]
    public void SameSourceObservationIsCountedOnceAndCrossProvenanceSurvivesPropagation()
    {
        var result = RunBatch([0]);
        var cross = Assert.Single(result.Runs, run =>
            run.StrategyId == SearchStrategyIds.CrossThreadConstrained);

        Assert.Equal(1, cross.CrossThreadConstraintAvailableCount.Value);
        Assert.Equal(1, cross.CrossThreadUniqueSourceObservationCount.Value);
        Assert.Equal(1, cross.CrossThreadConstraintAppliedCount.Value);
        Assert.Contains(cross.CandidateRegionProvenance, item =>
            item.SourceKind == CandidateSourceKind.CrossThreadObservation);
        Assert.All(cross.CrossThreadConstraintAudits, audit =>
            Assert.Equal(InteractionRelationKind.PersonPersonEncounter, audit.RelationKind));
    }

    [Fact]
    public async Task S3SingleRunAndBatchAreByteDeterministicWithStableCsvShape()
    {
        var scenario = LoadS3();
        Assert.Equal(
            JsonDefaults.Serialize(new SimulationEngine().Run(scenario, 17)),
            JsonDefaults.Serialize(new SimulationEngine().Run(scenario, 17)));
        var first = RunBatch([5, 0, 2, 1, 4, 3]);
        var second = RunBatch([0, 1, 2, 3, 4, 5]);
        var firstDirectory = TemporaryDirectory();
        var secondDirectory = TemporaryDirectory();
        try
        {
            var writer = new EvaluationOutputWriter();
            await writer.WriteAsync(firstDirectory, first);
            await writer.WriteAsync(secondDirectory, second);
            Assert.Equal(3, first.Manifest.OutputSchemaVersion);
            foreach (var name in EvaluationOutputNames.All)
            {
                Assert.Equal(
                    File.ReadAllBytes(Path.Combine(firstDirectory, name)),
                    File.ReadAllBytes(Path.Combine(secondDirectory, name)));
            }

            AssertJsonLines(Path.Combine(firstDirectory, EvaluationOutputNames.RunsJsonLines));
            AssertStableCsv(Path.Combine(firstDirectory, EvaluationOutputNames.RunsCsv));
            AssertStableCsv(Path.Combine(firstDirectory, EvaluationOutputNames.AggregateCsv));
            AssertStableCsv(Path.Combine(firstDirectory, EvaluationOutputNames.ConditionCsv));
            AssertStableCsv(Path.Combine(firstDirectory, EvaluationOutputNames.PairedCsv));
        }
        finally
        {
            Directory.Delete(firstDirectory, recursive: true);
            Directory.Delete(secondDirectory, recursive: true);
        }
    }

    private static SearchStrategyInput Input(
        ScenarioDefinition scenario,
        SimulationRun run,
        long tick) => new()
        {
            ScenarioId = scenario.Id,
            Seed = run.Seed,
            CurrentTick = tick,
            PublicContext = new RescueContext
            {
                ScenarioId = scenario.Id,
                TargetPersonId = scenario.TargetPersonId,
                NormalWalkingRadius = scenario.NormalWalkingRadius,
                PublicRegions = scenario.Regions,
                DeclaredHazardEnvelopeRegionIds = scenario.HazardTransportEnvelopeRegionIds,
                PublicHazardEnvelope = PublicHazardEnvelopeBuilder.Create(scenario.StochasticHazard!),
                PublicDeviceIds = scenario.Entities.Devices.Select(device => device.Id).ToArray(),
                PublicCandidateWarp = new PublicCandidateWarpDefinition
                {
                    HistoricalTimeWindow = new InteractionTimeWindow(
                    scenario.WeftEvaluation!.HistoricalStartTick,
                    scenario.WeftEvaluation.HistoricalEndTick),
                    AvailableAtTick = scenario.WeftEvaluation.NormalAvailableAtTick,
                    HistoricalCandidateRegionIds = scenario.WeftEvaluation.HistoricalCandidateRegionIds
                }
            },
            AvailableEvidence = new ObservationSet
            {
                ScenarioId = scenario.Id,
                Seed = run.Seed,
                Observations = run.ObservationSet.Observations
                .Where(observation => observation.AvailableTick <= tick)
                .ToArray()
            },
            AvailableCrossThreadConstraints = run.CrossThreadConstraints!.PubliclyAvailableAt(tick)
        };

    private static BatchEvaluationResult RunBatch(IReadOnlyList<int> seeds) =>
        new BatchEvaluationRunner().Run(new BatchEvaluationRequest
        {
            Scenarios = [new EvaluationScenarioInput
            {
                Scenario = LoadS3(),
                ContentSha256 = "s3-fixture-hash"
            }],
            Seeds = seeds,
            Strategies = SearchStrategyRegistry.All,
            SourceCommitSha = "fixture-commit",
            DirtyWorkingTree = true
        });

    private static ScenarioDefinition LoadS3() => ScenarioLoader.Load(Path.Combine(
        RepositoryRoot,
        "scenarios",
        "s3_cross_thread_encounter",
        "scenario.json"));

    private static void AssertJsonLines(string path)
    {
        Assert.All(File.ReadLines(path), line =>
        {
            using var document = JsonDocument.Parse(line);
            Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        });
    }

    private static void AssertStableCsv(string path)
    {
        var rows = File.ReadLines(path).Select(CountCsvColumns).ToArray();
        Assert.NotEmpty(rows);
        Assert.All(rows, count => Assert.Equal(rows[0], count));
    }

    private static int CountCsvColumns(string row)
    {
        var count = 1;
        var quoted = false;
        for (var index = 0; index < row.Length; index++)
        {
            if (row[index] == '"')
            {
                if (quoted && index + 1 < row.Length && row[index + 1] == '"')
                {
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (row[index] == ',' && !quoted)
            {
                count++;
            }
        }

        Assert.False(quoted);
        return count;
    }

    private static string TemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"footprint-m1c3-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string RepositoryRoot
    {
        get
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null && !File.Exists(Path.Combine(current.FullName, "FootprintSim.sln")))
            {
                current = current.Parent;
            }

            Assert.NotNull(current);
            return current.FullName;
        }
    }
}
