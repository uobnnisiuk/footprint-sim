using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using FootprintSim.Cli;
using FootprintSim.Evaluation;
using FootprintSim.Events;
using FootprintSim.Interactions.Truth;
using FootprintSim.Scenarios;
using FootprintSim.Telemetry;
using Xunit;

namespace FootprintSim.Core.Tests;

public sealed class M1T0Tests
{
    [Fact]
    public void S3TelemetrySeparatesStreamsAndPreservesNeutralStructure()
    {
        var telemetry = Assert.IsType<NeutralTelemetryBundle>(RunBatch(
            [Scenario("s3_cross_thread_encounter")],
            [0],
            collectTelemetry: true).Telemetry);

        Assert.Equal(TelemetrySchema.Version, telemetry.Manifest.TelemetrySchemaVersion);
        Assert.Equal(3, Enum.GetValues<TelemetryStreamKind>().Length);
        Assert.All(telemetry.Truth.WorldEvents, record =>
            Assert.Equal(TelemetryVisibility.PrivateTruth, record.Visibility));
        Assert.All(telemetry.Truth.Interactions, record =>
            Assert.Equal(TelemetryVisibility.PrivateTruth, record.Visibility));
        Assert.All(telemetry.Evidence.Observations, record =>
            Assert.Equal(TelemetryVisibility.PublicEvidence, record.Visibility));
        Assert.DoesNotContain(telemetry.Evidence.InteractionObservations, record =>
            record.Visibility == TelemetryVisibility.PrivateTruth);
        Assert.DoesNotContain(telemetry.Evidence.Constraints, record =>
            record.Visibility == TelemetryVisibility.PrivateTruth);
        Assert.All(telemetry.Evaluation.ConstraintApplications, record =>
            Assert.Equal(TelemetryVisibility.EvaluationContext, record.Visibility));
        Assert.All(telemetry.Evaluation.CandidateTransitions, record =>
            Assert.Equal(TelemetryVisibility.EvaluationContext, record.Visibility));

        AssertNoPropertyType<SimulationEvent>(typeof(EvidenceTelemetryStream));
        AssertNoPropertyType<InteractionOccurrence>(typeof(EvidenceTelemetryStream));
        AssertNoPropertyType<SimulationEvent>(typeof(ObservationTelemetryRecord));
        AssertNoPropertyType<InteractionOccurrence>(typeof(ObservationTelemetryRecord));
        AssertNoPropertyType<SimulationEvent>(typeof(InteractionObservationTelemetryRecord));
        AssertNoPropertyType<InteractionOccurrence>(typeof(InteractionObservationTelemetryRecord));
        AssertNoPropertyType<SimulationEvent>(typeof(ConstraintTelemetryRecord));
        AssertNoPropertyType<InteractionOccurrence>(typeof(ConstraintTelemetryRecord));
        AssertNoPropertyType<SimulationEvent>(typeof(EvaluationTelemetryStream));
        AssertNoPropertyType<InteractionOccurrence>(typeof(EvaluationTelemetryStream));
        AssertNoPropertyType<SimulationEvent>(typeof(ConstraintApplicationTelemetryRecord));
        AssertNoPropertyType<InteractionOccurrence>(typeof(ConstraintApplicationTelemetryRecord));
        AssertNoPropertyType<SimulationEvent>(typeof(CandidateTransitionTelemetryRecord));
        AssertNoPropertyType<InteractionOccurrence>(typeof(CandidateTransitionTelemetryRecord));
        Assert.DoesNotContain("GoodScore", JsonSerializer.Serialize(telemetry));
        Assert.DoesNotContain("CausalShadowScore", JsonSerializer.Serialize(telemetry));
        Assert.DoesNotContain("NexusResolutionScore", JsonSerializer.Serialize(telemetry));
        Assert.DoesNotContain("Absent", typeof(NeutralTelemetryBundle)
            .Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(NeutralTelemetryBundle).Namespace)
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Select(property => property.Name));
    }

    [Fact]
    public void S3TelemetryPreservesRelationsIntervalsProvenanceAndApplications()
    {
        var telemetry = Assert.IsType<NeutralTelemetryBundle>(RunBatch(
            [Scenario("s3_cross_thread_encounter")],
            [0, 1, 3, 6],
            collectTelemetry: true).Telemetry);

        Assert.Contains(telemetry.Truth.WorldEvents, record =>
            record.OccurrenceTimeWindow.TemporalKind == TelemetryTemporalKind.ExactEventPoint &&
            record.CausalReferenceIds.Count > 0);
        Assert.Contains(telemetry.Evidence.InteractionObservations, record =>
            record.ObservedTimeWindow.TemporalKind == TelemetryTemporalKind.ExactEventPoint);
        Assert.Contains(telemetry.Evidence.InteractionObservations, record =>
            record.ObservedTimeWindow.TemporalKind == TelemetryTemporalKind.InclusiveStateInterval &&
            record.ObservedTimeWindow.StartTick == 8 &&
            record.ObservedTimeWindow.EndTick == 9);
        Assert.All(telemetry.Truth.Interactions, record =>
        {
            Assert.Equal("PersonPersonEncounter", record.RelationKind);
            Assert.All(record.Participants, participant =>
                Assert.Equal("EncounterParticipant", participant.Role));
            Assert.NotEmpty(record.Provenance);
        });
        Assert.All(telemetry.Evidence.Constraints, record =>
        {
            Assert.Equal("PersonPersonEncounter", record.SourceRelationKind);
            Assert.NotEmpty(record.SourceAncestryIds);
            Assert.NotEmpty(record.Provenance);
        });
        Assert.NotEmpty(telemetry.Evaluation.ConstraintApplications);
        Assert.All(telemetry.Evaluation.ConstraintApplications, record =>
        {
            Assert.True(record.CandidateCountBefore >= record.CandidateCountAfter);
            Assert.False(string.IsNullOrWhiteSpace(record.QueryAccessContextId));
            Assert.NotEmpty(record.SourceAncestryIds);
            Assert.NotEmpty(record.Provenance);
        });
        Assert.Contains(telemetry.Evaluation.ConstraintApplications, record =>
            record.Disposition == Interactions.CandidateWarps.ConstraintApplicationDisposition.Narrow);
        Assert.Contains(telemetry.Evaluation.ConstraintApplications, record =>
            record.Disposition == Interactions.CandidateWarps.ConstraintApplicationDisposition.Branch &&
            record.AlternativesRetained);
        Assert.Contains(telemetry.Evaluation.ConstraintApplications, record =>
            record.Disposition == Interactions.CandidateWarps.ConstraintApplicationDisposition.MarkConflict &&
            record.ConflictDetected &&
            record.CandidateCountAfter > 0);
        Assert.All(telemetry.Evaluation.CandidateTransitions, record =>
        {
            Assert.False(string.IsNullOrWhiteSpace(record.CandidateSetIdAfter));
            Assert.Equal(
                TelemetryTemporalKind.ExactEventPoint,
                record.TransitionTimeWindow.TemporalKind);
            Assert.NotNull(record.PreferredRegionsAfter);
            Assert.NotNull(record.OuterPlausibleRegionsAfter);
        });
    }

    [Fact]
    public void S3TelemetryCountsRawConstraintsAndUniqueSourcesSeparately()
    {
        var telemetry = Assert.IsType<NeutralTelemetryBundle>(RunBatch(
            [Scenario("s3_cross_thread_encounter")],
            [0],
            collectTelemetry: true).Telemetry);

        Assert.Equal(1, telemetry.Manifest.RawConstraintRecordCount);
        Assert.Equal(1, telemetry.Manifest.UniqueConstraintSourceObservationCount);
        Assert.Equal(248, telemetry.Evaluation.CandidateTransitions.Count);
        Assert.Equal(21, telemetry.Evaluation.ConstraintApplications.Count);
        Assert.Single(telemetry.Truth.Interactions);
        Assert.Single(telemetry.Evidence.InteractionObservations);
        Assert.Single(telemetry.Evidence.Constraints);
        Assert.Equal(
            telemetry.Manifest.RawConstraintRecordCount,
            telemetry.Manifest.RecordCategories.Single(category =>
                category.Category == "constraints").RecordCount);
    }

    [Fact]
    public async Task TelemetrySerializationIsByteStableValidAndPathFree()
    {
        var first = Assert.IsType<NeutralTelemetryBundle>(RunBatch(
            [Scenario("s3_cross_thread_encounter")],
            [6, 0],
            collectTelemetry: true,
            strategies: SearchStrategyRegistry.All.Reverse().ToArray()).Telemetry);
        var second = Assert.IsType<NeutralTelemetryBundle>(RunBatch(
            [Scenario("s3_cross_thread_encounter")],
            [0, 6],
            collectTelemetry: true).Telemetry);
        var firstDirectory = TemporaryDirectory();
        var secondDirectory = TemporaryDirectory();
        try
        {
            var writer = new TelemetryOutputWriter();
            await writer.WriteAsync(firstDirectory, first);
            await writer.WriteAsync(secondDirectory, second);
            foreach (var path in TelemetryOutputNames.All)
            {
                var firstPath = Path.Combine(firstDirectory, PathFromRelative(path));
                var secondPath = Path.Combine(secondDirectory, PathFromRelative(path));
                Assert.Equal(File.ReadAllBytes(firstPath), File.ReadAllBytes(secondPath));
                var text = File.ReadAllText(firstPath);
                Assert.DoesNotContain(RepositoryRoot, text, StringComparison.Ordinal);
                Assert.DoesNotContain("generatedAt", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("timestamp", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("GoodScore", text, StringComparison.Ordinal);
            }

            foreach (var path in TelemetryOutputNames.JsonLines)
            {
                Assert.All(
                    File.ReadLines(Path.Combine(firstDirectory, PathFromRelative(path))),
                    line =>
                    {
                        using var document = JsonDocument.Parse(line);
                        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
                    });
            }

            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(
                firstDirectory,
                TelemetryOutputNames.Manifest)));
            Assert.Equal(
                TelemetrySchema.Version,
                manifest.RootElement.GetProperty("telemetrySchemaVersion").GetInt32());
        }
        finally
        {
            Directory.Delete(firstDirectory, recursive: true);
            Directory.Delete(secondDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task TelemetryFlagDoesNotChangeExistingEvaluationOutputs()
    {
        var outputWithout = TemporaryDirectory();
        var outputWith = TemporaryDirectory();
        try
        {
            var baseArguments = new[]
            {
                "evaluate",
                "--scenario", Scenario("s3_cross_thread_encounter"),
                "--seeds", "0",
                "--strategies", "all"
            };
            Assert.Equal(0, await CliApplication.RunAsync(
                [.. baseArguments, "--output", outputWithout]));
            Assert.Equal(0, await CliApplication.RunAsync(
                [.. baseArguments, "--output", outputWith, "--telemetry"]));

            Assert.False(Directory.Exists(Path.Combine(
                outputWithout,
                TelemetryOutputNames.RootDirectory)));
            Assert.True(Directory.Exists(Path.Combine(
                outputWith,
                TelemetryOutputNames.RootDirectory)));
            foreach (var name in EvaluationOutputNames.All)
            {
                Assert.Equal(
                    File.ReadAllBytes(Path.Combine(outputWithout, name)),
                    File.ReadAllBytes(Path.Combine(outputWith, name)));
            }

            Assert.Equal(2, await CliApplication.RunAsync(
                [.. baseArguments, "--output", outputWith, "--telemetry", "--telemetry"]));
        }
        finally
        {
            Directory.Delete(outputWithout, recursive: true);
            Directory.Delete(outputWith, recursive: true);
        }
    }

    [Fact]
    public void S0ThroughS3ProduceDomainGeneralTelemetry()
    {
        var scenarios = new[]
        {
            Scenario("s0_walking_baseline"),
            Scenario("s1_debris_transport"),
            Scenario("s2_branching_debris_transport"),
            Scenario("s3_cross_thread_encounter")
        };
        var telemetry = Assert.IsType<NeutralTelemetryBundle>(RunBatch(
            scenarios,
            [0],
            collectTelemetry: true).Telemetry);

        Assert.Equal(4, telemetry.Manifest.ScenarioInputs.Count);
        Assert.Equal(32, telemetry.Manifest.EvaluationRunCount);
        Assert.NotEmpty(telemetry.Truth.WorldEvents);
        Assert.NotEmpty(telemetry.Evidence.Observations);
        Assert.NotEmpty(telemetry.Evaluation.CandidateTransitions);
        Assert.All(telemetry.Manifest.RecordCategories, category =>
            Assert.DoesNotContain('\\', category.RelativePath));

        var forbiddenDomainTerms = new[] { "Debris", "River", "Building", "Mountain", "Mine", "Sea", "Plant" };
        var telemetryTypes = typeof(NeutralTelemetryBundle).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(NeutralTelemetryBundle).Namespace)
            .ToArray();
        foreach (var term in forbiddenDomainTerms)
        {
            Assert.DoesNotContain(telemetryTypes, type =>
                type.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(telemetryTypes.SelectMany(type =>
                type.GetProperties(BindingFlags.Public | BindingFlags.Instance)), property =>
                property.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static BatchEvaluationResult RunBatch(
        IReadOnlyList<string> scenarioPaths,
        IReadOnlyList<int> seeds,
        bool collectTelemetry,
        IReadOnlyList<ISearchStrategy>? strategies = null) =>
        new BatchEvaluationRunner().Run(new BatchEvaluationRequest
        {
            Scenarios = scenarioPaths.Select(path => new EvaluationScenarioInput
            {
                Scenario = ScenarioLoader.Load(path),
                ContentSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))
                    .ToLowerInvariant()
            }).ToArray(),
            Seeds = seeds,
            Strategies = strategies ?? SearchStrategyRegistry.All,
            SourceCommitSha = "m1t0-fixture-commit",
            DirtyWorkingTree = true,
            CollectTelemetry = collectTelemetry
        });

    private static void AssertNoPropertyType<T>(Type root)
    {
        Assert.DoesNotContain(
            root.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => ContainsType(property.PropertyType, typeof(T)));
    }

    private static bool ContainsType(Type candidate, Type forbidden)
    {
        if (candidate == forbidden)
        {
            return true;
        }

        return candidate.IsGenericType && candidate.GetGenericArguments()
            .Any(argument => ContainsType(argument, forbidden));
    }

    private static string PathFromRelative(string path) =>
        path.Replace('/', Path.DirectorySeparatorChar);

    private static string Scenario(string directory) => Path.Combine(
        RepositoryRoot,
        "scenarios",
        directory,
        "scenario.json");

    private static string TemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"footprint-m1t0-{Guid.NewGuid():N}");
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
