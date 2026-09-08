using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using FootprintSim.Evaluation;
using FootprintSim.Hazards;
using FootprintSim.Inference;
using FootprintSim.Scenarios;
using FootprintSim.Serialization;
using FootprintSim.Simulation;
using FootprintSim.Telemetry;

namespace FootprintSim.Cli;

public static class CliApplication
{
    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            if (args.Length > 0 && string.Equals(
                args[0],
                "audit-common-world-plateau-domain",
                StringComparison.Ordinal))
            {
                return await RunCommonWorldPlateauDomainAuditAsync(args[1..]);
            }

            if (args.Length > 0 && string.Equals(
                args[0],
                "evaluate-common-world-field-combination",
                StringComparison.Ordinal))
            {
                return await RunCommonWorldFieldCombinationAsync(args[1..]);
            }

            if (args.Length > 0 && string.Equals(
                args[0],
                "evaluate-common-world-observation",
                StringComparison.Ordinal))
            {
                return await RunCommonWorldObservationAsync(args[1..]);
            }

            if (args.Length > 0 && string.Equals(
                args[0],
                "evaluate-constraint-composition",
                StringComparison.Ordinal))
            {
                return await RunConstraintCompositionAsync(args[1..]);
            }

            if (args.Length > 0 && string.Equals(
                args[0],
                "evaluate-relation-dependent-derivation",
                StringComparison.Ordinal))
            {
                return await RunRelationDependentDerivationAsync(args[1..]);
            }

            if (args.Length > 0 && string.Equals(
                args[0],
                "evaluate-participant-ambiguity-paired",
                StringComparison.Ordinal))
            {
                return await RunParticipantAmbiguityPairedAsync(args[1..]);
            }

            if (args.Length > 0 && string.Equals(
                args[0],
                "evaluate-participant-ambiguity-derivation",
                StringComparison.Ordinal))
            {
                return await RunParticipantAmbiguityDerivationAsync(args[1..]);
            }

            if (args.Length > 0 && string.Equals(
                args[0],
                "evaluate-time-resolution-paired",
                StringComparison.Ordinal))
            {
                return await RunTimeResolutionPairedAsync(args[1..]);
            }

            if (args.Length > 0 && string.Equals(
                args[0],
                "evaluate-observation-constraint-derivation",
                StringComparison.Ordinal))
            {
                return await RunObservationConstraintDerivationAsync(args[1..]);
            }

            if (args.Length > 0 && string.Equals(
                args[0],
                "evaluate-time-resolution",
                StringComparison.Ordinal))
            {
                return await RunTimeResolutionAsync(args[1..]);
            }

            if (args.Length > 0 && string.Equals(
                args[0],
                "evaluate-location-resolution",
                StringComparison.Ordinal))
            {
                return await RunLocationResolutionAsync(args[1..]);
            }

            if (args.Length > 0 && string.Equals(
                args[0],
                "evaluate-later-trace-field-ablation",
                StringComparison.Ordinal))
            {
                return await RunLaterTraceFieldAblationAsync(args[1..]);
            }

            if (args.Length > 0 && string.Equals(
                args[0],
                "evaluate-observation-ablation",
                StringComparison.Ordinal))
            {
                return await RunObservationAblationAsync(args[1..]);
            }

            if (args.Length > 0 && string.Equals(args[0], "evaluate", StringComparison.Ordinal))
            {
                return await RunEvaluationAsync(args[1..]);
            }

            return await RunSimulationAsync(args);
        }
        catch (Exception exception) when (exception is
            ArgumentException or IOException or InvalidOperationException or JsonException or
            UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"error: {exception.Message}");
            return 2;
        }
    }

    private static async Task<int> RunCommonWorldPlateauDomainAuditAsync(string[] args)
    {
        if (args.Contains("--help", StringComparer.Ordinal))
        {
            PrintCommonWorldPlateauDomainAuditUsage();
            return 0;
        }

        var options = ParseOptions(args, new HashSet<string>(
            ["--experiment", "--baseline-experiment", "--seeds", "--output"],
            StringComparer.Ordinal));
        var protocolPath = Path.GetFullPath(Require(options, "--experiment"));
        var baselinePath = Path.GetFullPath(Require(options, "--baseline-experiment"));
        // The shared parser intentionally deduplicates seed expressions. This protocol
        // requires the literal complete range, so duplicate/extra input cannot disappear.
        var seedExpression = Require(options, "--seeds");
        if (!string.Equals(seedExpression, "0..199", StringComparison.Ordinal))
        {
            throw new ArgumentException("M1-F17 requires --seeds 0..199 exactly.");
        }

        var outputPath = Path.GetFullPath(Require(options, "--output"));
        var git = GitMetadataReader.Read(Directory.GetCurrentDirectory());
        var result = new CommonWorldPlateauDomainAuditRunner().Run(
            new CommonWorldPlateauDomainAuditRequest
            {
                Protocol = CommonWorldPlateauDomainAuditProtocolLoader.Load(protocolPath),
                ProtocolContentSha256 = HashFile(protocolPath),
                BaselineExperiment = CommonWorldFieldCombinationExperimentLoader.Load(baselinePath),
                BaselineExperimentContentSha256 = HashFile(baselinePath),
                Seeds = SeedExpressionParser.Parse(seedExpression),
                SourceCommitSha = git.CommitSha,
                DirtyWorkingTree = git.DirtyWorkingTree
            });
        await new CommonWorldPlateauDomainAuditOutputWriter().WriteAsync(result, outputPath);
        Console.WriteLine($"Common-world plateau domain audit completed: {result.Rows.Count} comparisons.");
        Console.WriteLine($"Results: {outputPath}");
        return 0;
    }

    private static async Task<int> RunCommonWorldFieldCombinationAsync(string[] args)
    {
        if (args.Contains("--help", StringComparer.Ordinal))
        {
            PrintCommonWorldFieldCombinationUsage();
            return 0;
        }

        var options = ParseOptions(
            args,
            new HashSet<string>(["--experiment", "--seeds", "--output"], StringComparer.Ordinal));
        var experimentPath = Path.GetFullPath(Require(options, "--experiment"));
        var experiment = CommonWorldFieldCombinationExperimentLoader.Load(experimentPath);
        var seeds = SeedExpressionParser.Parse(Require(options, "--seeds"));
        var outputPath = Path.GetFullPath(Require(options, "--output"));
        var git = GitMetadataReader.Read(Directory.GetCurrentDirectory());
        var result = new CommonWorldFieldCombinationEvaluationRunner().Run(
            new CommonWorldFieldCombinationEvaluationRequest
            {
                Experiment = experiment,
                Seeds = seeds,
                ExperimentContentSha256 = HashFile(experimentPath),
                SourceCommitSha = git.CommitSha,
                DirtyWorkingTree = git.DirtyWorkingTree
            });
        await new CommonWorldFieldCombinationPairedOutputWriter().WriteAsync(outputPath, result);

        Console.WriteLine(
            $"Common-world field-combination evaluation completed: " +
            $"{result.Summary.WorldCount} worlds, " +
            $"{result.Summary.ProfileEvaluationCount} profile runs, " +
            $"{result.Summary.JointSetInclusionViolationCount} set-inclusion violations.");
        Console.WriteLine($"Results: {outputPath}");
        return 0;
    }

    private static async Task<int> RunCommonWorldObservationAsync(string[] args)
    {
        if (args.Contains("--help", StringComparer.Ordinal))
        {
            PrintCommonWorldObservationUsage();
            return 0;
        }

        var options = ParseOptions(
            args,
            new HashSet<string>(["--fixture", "--output"], StringComparer.Ordinal));
        var fixturePath = Path.GetFullPath(Require(options, "--fixture"));
        var fixture = CommonWorldObservationFixtureLoader.Load(fixturePath);
        var outputPath = Path.GetFullPath(Require(options, "--output"));
        var git = GitMetadataReader.Read(Directory.GetCurrentDirectory());
        var result = new CommonWorldObservationFixtureEvaluationRunner().Run(
            new CommonWorldObservationFixtureEvaluationRequest
            {
                Fixture = fixture,
                FixtureContentSha256 = HashFile(fixturePath),
                SourceCommitSha = git.CommitSha,
                DirtyWorkingTree = git.DirtyWorkingTree
            });
        await new CommonWorldObservationFixtureOutputWriter().WriteAsync(outputPath, result);

        Console.WriteLine(
            $"Common-world observation fixture completed: {result.Manifest.ProfileRunCount} " +
            $"profiles, {result.IntersectionViolationCount} intersection violations, " +
            $"{result.FalseExclusionCount} false exclusions.");
        Console.WriteLine($"Results: {outputPath}");
        return 0;
    }

    private static async Task<int> RunConstraintCompositionAsync(string[] args)
    {
        if (args.Contains("--help", StringComparer.Ordinal))
        {
            PrintConstraintCompositionUsage();
            return 0;
        }

        var options = ParseOptions(
            args,
            new HashSet<string>(["--fixture", "--output"], StringComparer.Ordinal));
        var fixturePath = Path.GetFullPath(Require(options, "--fixture"));
        var fixture = ConstraintCompositionFixtureLoader.Load(fixturePath);
        var outputPath = Path.GetFullPath(Require(options, "--output"));
        var git = GitMetadataReader.Read(Directory.GetCurrentDirectory());
        var result = new ConstraintCompositionFixtureEvaluationRunner().Run(
            new ConstraintCompositionFixtureEvaluationRequest
            {
                Fixture = fixture,
                FixtureContentSha256 = HashFile(fixturePath),
                SourceCommitSha = git.CommitSha,
                DirtyWorkingTree = git.DirtyWorkingTree
            });
        await new ConstraintCompositionFixtureOutputWriter().WriteAsync(outputPath, result);

        Console.WriteLine(
            $"Constraint-composition fixture completed: {result.Manifest.ProfileRunCount} profiles, " +
            $"{result.IntersectionViolationCount} intersection violations, " +
            $"{result.FalseExclusionCount} false exclusions.");
        Console.WriteLine($"Results: {outputPath}");
        return 0;
    }

    private static async Task<int> RunRelationDependentDerivationAsync(string[] args)
    {
        if (args.Contains("--help", StringComparer.Ordinal))
        {
            PrintRelationDependentDerivationUsage();
            return 0;
        }

        var options = ParseOptions(
            args,
            new HashSet<string>(["--fixture", "--output"], StringComparer.Ordinal));
        var fixturePath = Path.GetFullPath(Require(options, "--fixture"));
        var fixture = RelationDependentFixtureLoader.Load(fixturePath);
        var outputPath = Path.GetFullPath(Require(options, "--output"));
        var git = GitMetadataReader.Read(Directory.GetCurrentDirectory());
        var result = new RelationDependentFixtureEvaluationRunner().Run(
            new RelationDependentFixtureEvaluationRequest
            {
                Fixture = fixture,
                FixtureContentSha256 = HashFile(fixturePath),
                SourceCommitSha = git.CommitSha,
                DirtyWorkingTree = git.DirtyWorkingTree
            });
        await new RelationDependentFixtureOutputWriter().WriteAsync(outputPath, result);

        Console.WriteLine(
            $"Relation-dependent derivation fixture completed: " +
            $"{result.Manifest.ProfileRunCount} profiles, " +
            $"{result.MonotonicityViolationCount} monotonicity violations.");
        Console.WriteLine($"Results: {outputPath}");
        return 0;
    }

    private static async Task<int> RunParticipantAmbiguityPairedAsync(string[] args)
    {
        if (args.Contains("--help", StringComparer.Ordinal))
        {
            PrintParticipantAmbiguityPairedUsage();
            return 0;
        }

        var options = ParseOptions(
            args,
            new HashSet<string>(["--experiment", "--seeds", "--output"], StringComparer.Ordinal));
        var experimentPath = Path.GetFullPath(Require(options, "--experiment"));
        var experiment = ParticipantAmbiguityPairedExperimentLoader.Load(experimentPath);
        var seeds = SeedExpressionParser.Parse(Require(options, "--seeds"));
        var outputPath = Path.GetFullPath(Require(options, "--output"));
        var git = GitMetadataReader.Read(Directory.GetCurrentDirectory());
        var result = new ParticipantAmbiguityPairedEvaluationRunner().Run(
            new ParticipantAmbiguityPairedEvaluationRequest
            {
                Experiment = experiment,
                Seeds = seeds,
                ExperimentContentSha256 = HashFile(experimentPath),
                SourceCommitSha = git.CommitSha,
                DirtyWorkingTree = git.DirtyWorkingTree
            });
        await new ParticipantAmbiguityPairedOutputWriter().WriteAsync(outputPath, result);

        Console.WriteLine(
            $"Paired participant-ambiguity evaluation completed: {result.Summary.WorldCount} worlds, " +
            $"{result.Summary.PairedProfileRunCount} profile runs, " +
            $"{result.Summary.CandidateSetMonotonicityViolationCount} candidate-set violations.");
        Console.WriteLine($"Results: {outputPath}");
        return 0;
    }

    private static async Task<int> RunParticipantAmbiguityDerivationAsync(string[] args)
    {
        if (args.Contains("--help", StringComparer.Ordinal))
        {
            PrintParticipantAmbiguityDerivationUsage();
            return 0;
        }

        var options = ParseOptions(
            args,
            new HashSet<string>(["--fixture", "--output"], StringComparer.Ordinal));
        var fixturePath = Path.GetFullPath(Require(options, "--fixture"));
        var fixture = ParticipantAmbiguityFixtureLoader.Load(fixturePath);
        var outputPath = Path.GetFullPath(Require(options, "--output"));
        var git = GitMetadataReader.Read(Directory.GetCurrentDirectory());
        var result = new ParticipantAmbiguityFixtureEvaluationRunner().Run(
            new ParticipantAmbiguityFixtureEvaluationRequest
            {
                Fixture = fixture,
                FixtureContentSha256 = HashFile(fixturePath),
                SourceCommitSha = git.CommitSha,
                DirtyWorkingTree = git.DirtyWorkingTree
            });
        await new ParticipantAmbiguityFixtureOutputWriter().WriteAsync(outputPath, result);

        Console.WriteLine(
            $"Participant-ambiguity derivation fixture completed: " +
            $"{result.Manifest.ProfileRunCount} profiles, " +
            $"{result.MonotonicityViolationCount} monotonicity violations.");
        Console.WriteLine($"Results: {outputPath}");
        return 0;
    }

    private static async Task<int> RunTimeResolutionPairedAsync(string[] args)
    {
        if (args.Contains("--help", StringComparer.Ordinal))
        {
            PrintTimeResolutionPairedUsage();
            return 0;
        }

        var options = ParseOptions(
            args,
            new HashSet<string>(["--experiment", "--seeds", "--output"], StringComparer.Ordinal));
        var experimentPath = Path.GetFullPath(Require(options, "--experiment"));
        var experiment = TimeResolutionPairedExperimentLoader.Load(experimentPath);
        var seeds = SeedExpressionParser.Parse(Require(options, "--seeds"));
        var outputPath = Path.GetFullPath(Require(options, "--output"));
        var git = GitMetadataReader.Read(Directory.GetCurrentDirectory());
        var result = new TimeResolutionPairedEvaluationRunner().Run(
            new TimeResolutionPairedEvaluationRequest
            {
                Experiment = experiment,
                Seeds = seeds,
                ExperimentContentSha256 = HashFile(experimentPath),
                SourceCommitSha = git.CommitSha,
                DirtyWorkingTree = git.DirtyWorkingTree
            });
        await new TimeResolutionPairedOutputWriter().WriteAsync(outputPath, result);

        Console.WriteLine(
            $"Paired time-resolution evaluation completed: {result.Summary.WorldCount} worlds, " +
            $"{result.Summary.PairedProfileRunCount} profile runs, " +
            $"{result.Summary.CandidateSetMonotonicityViolationCount} candidate-set violations.");
        Console.WriteLine($"Results: {outputPath}");
        return 0;
    }

    private static async Task<int> RunObservationConstraintDerivationAsync(string[] args)
    {
        if (args.Contains("--help", StringComparer.Ordinal))
        {
            PrintObservationConstraintDerivationUsage();
            return 0;
        }

        var options = ParseOptions(
            args,
            new HashSet<string>(["--fixture", "--output"], StringComparer.Ordinal));
        var fixturePath = Path.GetFullPath(Require(options, "--fixture"));
        var fixture = BoundedDurationTimeFixtureLoader.Load(fixturePath);
        var outputPath = Path.GetFullPath(Require(options, "--output"));
        var git = GitMetadataReader.Read(Directory.GetCurrentDirectory());
        var result = new BoundedDurationTimeFixtureEvaluationRunner().Run(
            new BoundedDurationTimeFixtureEvaluationRequest
            {
                Fixture = fixture,
                FixtureContentSha256 = HashFile(fixturePath),
                SourceCommitSha = git.CommitSha,
                DirtyWorkingTree = git.DirtyWorkingTree
            });
        await new BoundedDurationTimeFixtureOutputWriter().WriteAsync(outputPath, result);

        Console.WriteLine(
            $"Observation-to-constraint fixture completed: {result.Manifest.ProfileRunCount} profiles, " +
            $"{result.MonotonicityViolationCount} monotonicity violations.");
        Console.WriteLine($"Results: {outputPath}");
        return 0;
    }

    private static async Task<int> RunSimulationAsync(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help", StringComparer.Ordinal))
        {
            PrintUsage();
            return args.Length == 0 ? 2 : 0;
        }

        var options = ParseOptions(
            args,
            new HashSet<string>(["--scenario", "--seed", "--output"], StringComparer.Ordinal));
        var scenarioPath = Path.GetFullPath(Require(options, "--scenario"));
        var outputPath = Path.GetFullPath(Require(options, "--output"));
        var seedText = Require(options, "--seed");
        if (!int.TryParse(
            seedText,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var seed))
        {
            throw new ArgumentException("--seed must be a 32-bit integer.");
        }

        var scenario = ScenarioLoader.Load(scenarioPath);
        var run = new SimulationEngine().Run(scenario, seed);
        var summary = new BasicRescueInference().Infer(
            new RescueContext
            {
                ScenarioId = scenario.Id,
                TargetPersonId = scenario.TargetPersonId,
                NormalWalkingRadius = scenario.NormalWalkingRadius,
                PublicRegions = scenario.Regions,
                DeclaredHazardEnvelopeRegionIds = scenario.HazardTransportEnvelopeRegionIds,
                PublicHazardEnvelope = scenario.StochasticHazard is null
                    ? null
                    : PublicHazardEnvelopeBuilder.Create(scenario.StochasticHazard),
                PublicDeviceIds = scenario.Entities.Devices
                    .Select(device => device.Id)
                    .OrderBy(deviceId => deviceId, StringComparer.Ordinal)
                    .ToArray()
            },
            run.ObservationSet);

        Directory.CreateDirectory(outputPath);
        var metadata = new RunMetadata
        {
            SchemaVersion = 1,
            EngineVersion = scenario.StochasticHazard is null ? "m0" : "m1b",
            ScenarioId = scenario.Id,
            Seed = seed,
            ScenarioSha256 = HashFile(scenarioPath),
            GroundTruthEventCount = run.GroundTruthEvents.Count,
            ObservationCount = run.ObservationSet.Observations.Count,
            Outputs =
            [
                "ground-truth.events.json",
                "observations.json",
                "search-zone.summary.json",
                "run.metadata.json"
            ]
        };

        await WriteJsonAsync(
            Path.Combine(outputPath, "ground-truth.events.json"),
            new GroundTruthEventLog
            {
                ScenarioId = scenario.Id,
                Seed = seed,
                Events = run.GroundTruthEvents
            });
        await WriteJsonAsync(Path.Combine(outputPath, "observations.json"), run.ObservationSet);
        await WriteJsonAsync(Path.Combine(outputPath, "search-zone.summary.json"), summary);
        await WriteJsonAsync(Path.Combine(outputPath, "run.metadata.json"), metadata);

        Console.WriteLine(
            $"Scenario '{scenario.Id}' completed with seed {seed}: " +
            $"{run.GroundTruthEvents.Count} truth events, " +
            $"{run.ObservationSet.Observations.Count} observations.");
        Console.WriteLine($"Results: {outputPath}");
        return 0;
    }

    private static async Task<int> RunObservationAblationAsync(string[] args)
    {
        if (args.Contains("--help", StringComparer.Ordinal))
        {
            PrintObservationAblationUsage();
            return 0;
        }

        var options = ParseOptions(
            args,
            new HashSet<string>(["--experiment", "--seeds", "--output"], StringComparer.Ordinal));
        var experimentPath = Path.GetFullPath(Require(options, "--experiment"));
        var experiment = ObservationAblationExperimentLoader.Load(experimentPath);
        var experimentDirectory = Path.GetDirectoryName(experimentPath) ??
            throw new InvalidOperationException("Experiment path has no containing directory.");
        var scenarioPath = Path.GetFullPath(experiment.ScenarioPath, experimentDirectory);
        var scenario = ScenarioLoader.Load(scenarioPath);
        var seeds = SeedExpressionParser.Parse(Require(options, "--seeds"));
        var outputPath = Path.GetFullPath(Require(options, "--output"));
        var git = GitMetadataReader.Read(Directory.GetCurrentDirectory());
        var result = new ObservationAblationEvaluationRunner().Run(
            new ObservationAblationEvaluationRequest
            {
                Experiment = experiment,
                Scenario = scenario,
                Seeds = seeds,
                ExperimentContentSha256 = HashFile(experimentPath),
                ScenarioContentSha256 = HashFile(scenarioPath),
                SourceCommitSha = git.CommitSha,
                DirtyWorkingTree = git.DirtyWorkingTree
            });
        await new ObservationAblationOutputWriter().WriteAsync(outputPath, result);

        Console.WriteLine(
            $"Observation ablation completed: {result.Manifest.ProfileRunCount} paired profile runs, " +
            $"{result.Manifest.MethodRunCount} method runs, {result.Manifest.Seeds.Count} seeds, " +
            $"and {result.Manifest.ProfileIds.Count} profiles.");
        Console.WriteLine($"Results: {outputPath}");
        return 0;
    }

    private static async Task<int> RunLaterTraceFieldAblationAsync(string[] args)
    {
        if (args.Contains("--help", StringComparer.Ordinal))
        {
            PrintLaterTraceFieldAblationUsage();
            return 0;
        }

        var options = ParseOptions(
            args,
            new HashSet<string>(["--experiment", "--seeds", "--output"], StringComparer.Ordinal));
        var experimentPath = Path.GetFullPath(Require(options, "--experiment"));
        var experiment = LaterTraceFieldAblationExperimentLoader.Load(experimentPath);
        var experimentDirectory = Path.GetDirectoryName(experimentPath) ??
            throw new InvalidOperationException("Experiment path has no containing directory.");
        var scenarioPath = Path.GetFullPath(experiment.ScenarioPath, experimentDirectory);
        var baseExperimentPath = Path.GetFullPath(
            experiment.BaseObservationAblationExperimentPath,
            experimentDirectory);
        var scenario = ScenarioLoader.Load(scenarioPath);
        var baseExperiment = ObservationAblationExperimentLoader.Load(baseExperimentPath);
        var seeds = SeedExpressionParser.Parse(Require(options, "--seeds"));
        var outputPath = Path.GetFullPath(Require(options, "--output"));
        var git = GitMetadataReader.Read(Directory.GetCurrentDirectory());
        var result = new LaterTraceFieldAblationEvaluationRunner().Run(
            new LaterTraceFieldAblationEvaluationRequest
            {
                Experiment = experiment,
                BaseExperiment = baseExperiment,
                Scenario = scenario,
                Seeds = seeds,
                ExperimentContentSha256 = HashFile(experimentPath),
                BaseExperimentContentSha256 = HashFile(baseExperimentPath),
                ScenarioContentSha256 = HashFile(scenarioPath),
                SourceCommitSha = git.CommitSha,
                DirtyWorkingTree = git.DirtyWorkingTree
            });
        await new LaterTraceFieldAblationOutputWriter().WriteAsync(outputPath, result);

        Console.WriteLine(
            $"Later-trace field ablation completed: {result.Manifest.ProfileRunCount} paired profile runs, " +
            $"{result.Manifest.MethodRunCount} method runs, {result.Manifest.Seeds.Count} seeds, " +
            $"and {result.Manifest.ProfileIds.Count} profiles.");
        Console.WriteLine($"Results: {outputPath}");
        return 0;
    }

    private static async Task<int> RunTimeResolutionAsync(string[] args)
    {
        if (args.Contains("--help", StringComparer.Ordinal))
        {
            PrintTimeResolutionUsage();
            return 0;
        }

        var options = ParseOptions(
            args,
            new HashSet<string>(["--experiment", "--seeds", "--output"], StringComparer.Ordinal));
        var experimentPath = Path.GetFullPath(Require(options, "--experiment"));
        var experiment = TimeResolutionExperimentLoader.Load(experimentPath);
        var experimentDirectory = Path.GetDirectoryName(experimentPath) ??
            throw new InvalidOperationException("Experiment path has no containing directory.");
        var scenarioPath = Path.GetFullPath(experiment.ScenarioPath, experimentDirectory);
        var scenario = ScenarioLoader.Load(scenarioPath);
        var seeds = SeedExpressionParser.Parse(Require(options, "--seeds"));
        var outputPath = Path.GetFullPath(Require(options, "--output"));
        var git = GitMetadataReader.Read(Directory.GetCurrentDirectory());
        var result = new TimeResolutionEvaluationRunner().Run(
            new TimeResolutionEvaluationRequest
            {
                Experiment = experiment,
                Scenario = scenario,
                Seeds = seeds,
                ExperimentContentSha256 = HashFile(experimentPath),
                ScenarioContentSha256 = HashFile(scenarioPath),
                SourceCommitSha = git.CommitSha,
                DirtyWorkingTree = git.DirtyWorkingTree
            });
        await new TimeResolutionOutputWriter().WriteAsync(outputPath, result);

        Console.WriteLine(
            $"Time-resolution evaluation completed: {result.Manifest.ProfileRunCount} paired profile runs, " +
            $"{result.Manifest.MethodRunCount} method runs, {result.Manifest.Seeds.Count} seeds, " +
            $"and {result.Manifest.LevelIds.Count} levels.");
        Console.WriteLine($"Results: {outputPath}");
        return 0;
    }

    private static async Task<int> RunLocationResolutionAsync(string[] args)
    {
        if (args.Contains("--help", StringComparer.Ordinal))
        {
            PrintLocationResolutionUsage();
            return 0;
        }

        var options = ParseOptions(
            args,
            new HashSet<string>(["--experiment", "--seeds", "--output"], StringComparer.Ordinal));
        var experimentPath = Path.GetFullPath(Require(options, "--experiment"));
        var experiment = LocationResolutionExperimentLoader.Load(experimentPath);
        var experimentDirectory = Path.GetDirectoryName(experimentPath) ??
            throw new InvalidOperationException("Experiment path has no containing directory.");
        var scenarioPath = Path.GetFullPath(experiment.ScenarioPath, experimentDirectory);
        var scenario = ScenarioLoader.Load(scenarioPath);
        var seeds = SeedExpressionParser.Parse(Require(options, "--seeds"));
        var outputPath = Path.GetFullPath(Require(options, "--output"));
        var git = GitMetadataReader.Read(Directory.GetCurrentDirectory());
        var result = new LocationResolutionEvaluationRunner().Run(
            new LocationResolutionEvaluationRequest
            {
                Experiment = experiment,
                Scenario = scenario,
                Seeds = seeds,
                ExperimentContentSha256 = HashFile(experimentPath),
                ScenarioContentSha256 = HashFile(scenarioPath),
                SourceCommitSha = git.CommitSha,
                DirtyWorkingTree = git.DirtyWorkingTree
            });
        await new LocationResolutionOutputWriter().WriteAsync(outputPath, result);

        Console.WriteLine(
            $"Location-resolution evaluation completed: {result.Manifest.ProfileRunCount} paired profile runs, " +
            $"{result.Manifest.MethodRunCount} method runs, {result.Manifest.Seeds.Count} seeds, " +
            $"and {result.Manifest.LevelIds.Count} levels.");
        Console.WriteLine($"Results: {outputPath}");
        return 0;
    }

    private static async Task<int> RunEvaluationAsync(string[] args)
    {
        if (args.Contains("--help", StringComparer.Ordinal))
        {
            PrintEvaluationUsage();
            return 0;
        }

        var telemetryFlagCount = args.Count(argument =>
            string.Equals(argument, "--telemetry", StringComparison.Ordinal));
        if (telemetryFlagCount > 1)
        {
            throw new ArgumentException("Option '--telemetry' was specified more than once.");
        }

        var telemetryEnabled = telemetryFlagCount == 1;
        var optionArguments = args
            .Where(argument => !string.Equals(
                argument,
                "--telemetry",
                StringComparison.Ordinal))
            .ToArray();
        var options = ParseOptions(
            optionArguments,
            new HashSet<string>(
                ["--scenario", "--scenarios", "--seeds", "--strategies", "--output"],
                StringComparer.Ordinal));
        if (options.ContainsKey("--scenario") && options.ContainsKey("--scenarios"))
        {
            throw new ArgumentException("Use either --scenario or --scenarios, not both.");
        }

        var scenarioSource = options.TryGetValue("--scenario", out var singleScenario)
            ? singleScenario
            : Require(options, "--scenarios");
        var seeds = SeedExpressionParser.Parse(Require(options, "--seeds"));
        var strategies = SearchStrategyRegistry.Resolve(
            Require(options, "--strategies").Split(',', StringSplitOptions.TrimEntries));
        var outputPath = Path.GetFullPath(Require(options, "--output"));
        var scenarios = LoadEvaluationScenarios(scenarioSource);
        var git = GitMetadataReader.Read(Directory.GetCurrentDirectory());

        var result = new BatchEvaluationRunner().Run(new BatchEvaluationRequest
        {
            Scenarios = scenarios,
            Seeds = seeds,
            Strategies = strategies,
            SourceCommitSha = git.CommitSha,
            DirtyWorkingTree = git.DirtyWorkingTree,
            CollectTelemetry = telemetryEnabled
        });
        await new EvaluationOutputWriter().WriteAsync(outputPath, result);
        if (telemetryEnabled)
        {
            var telemetry = result.Telemetry ?? throw new InvalidOperationException(
                "Telemetry collection was requested but produced no telemetry bundle.");
            await new TelemetryOutputWriter().WriteAsync(
                Path.Combine(outputPath, TelemetryOutputNames.RootDirectory),
                telemetry);
        }

        Console.WriteLine(
            $"Evaluation completed: {result.Manifest.RunCount} strategy runs across " +
            $"{result.Manifest.ScenarioIdentifiers.Count} scenarios, " +
            $"{result.Manifest.Seeds.Count} seeds, and " +
            $"{result.Manifest.StrategyIdentifiers.Count} strategies.");
        Console.WriteLine($"Results: {outputPath}");
        return 0;
    }

    private static IReadOnlyList<EvaluationScenarioInput> LoadEvaluationScenarios(string source)
    {
        var fullPath = Path.GetFullPath(source);
        string[] files;
        if (File.Exists(fullPath))
        {
            files = [fullPath];
        }
        else if (Directory.Exists(fullPath))
        {
            files = Directory.GetFiles(fullPath, "scenario.json", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }
        else
        {
            throw new ArgumentException($"Scenario source '{source}' does not exist.");
        }

        if (files.Length == 0)
        {
            throw new ArgumentException($"Scenario source '{source}' contains no scenario.json files.");
        }

        return files.Select(file => new EvaluationScenarioInput
        {
            Scenario = ScenarioLoader.Load(file),
            ContentSha256 = HashFile(file)
        }).ToArray();
    }

    private static Dictionary<string, string> ParseOptions(
        IReadOnlyList<string> args,
        IReadOnlySet<string> allowed)
    {
        if (args.Count == 0 || args.Count % 2 != 0)
        {
            throw new ArgumentException("Options must be provided as name/value pairs.");
        }

        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Count; index += 2)
        {
            var name = args[index];
            if (!allowed.Contains(name))
            {
                throw new ArgumentException($"Unknown option '{name}'.");
            }

            if (!options.TryAdd(name, args[index + 1]))
            {
                throw new ArgumentException($"Option '{name}' was specified more than once.");
            }
        }

        return options;
    }

    private static string Require(IReadOnlyDictionary<string, string> options, string name)
    {
        return options.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException($"Required option '{name}' is missing.");
    }

    private static string HashFile(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static async Task WriteJsonAsync<T>(string path, T value)
    {
        await File.WriteAllTextAsync(path, JsonDefaults.Serialize(value) + "\n");
    }

    private static void PrintUsage()
    {
        Console.WriteLine(
            "Simulation: dotnet run --project src/FootprintSim.Cli -- " +
            "--scenario <scenario.json> --seed <integer> --output <directory>");
        PrintEvaluationUsage();
    }

    private static void PrintEvaluationUsage()
    {
        Console.WriteLine(
            "Evaluation: dotnet run --project src/FootprintSim.Cli -- evaluate " +
            "--scenario <file> | --scenarios <file-or-directory> --seeds <n|a..b|list> " +
            "--strategies <all|id-list> --output <directory> [--telemetry]");
        PrintObservationAblationUsage();
    }

    private static void PrintObservationAblationUsage()
    {
        Console.WriteLine(
            "Observation ablation: dotnet run --project src/FootprintSim.Cli -- " +
            "evaluate-observation-ablation --experiment <experiment.json> " +
            "--seeds <n|a..b|list> --output <directory>");
        PrintLaterTraceFieldAblationUsage();
    }

    private static void PrintLaterTraceFieldAblationUsage()
    {
        Console.WriteLine(
            "Later-trace field ablation: dotnet run --project src/FootprintSim.Cli -- " +
            "evaluate-later-trace-field-ablation --experiment <experiment.json> " +
            "--seeds <n|a..b|list> --output <directory>");
        PrintLocationResolutionUsage();
    }

    private static void PrintLocationResolutionUsage()
    {
        Console.WriteLine(
            "Location resolution: dotnet run --project src/FootprintSim.Cli -- " +
            "evaluate-location-resolution --experiment <experiment.json> " +
            "--seeds <n|a..b|list> --output <directory>");
        PrintTimeResolutionUsage();
    }

    private static void PrintTimeResolutionUsage()
    {
        Console.WriteLine(
            "Time resolution: dotnet run --project src/FootprintSim.Cli -- " +
            "evaluate-time-resolution --experiment <experiment.json> " +
            "--seeds <n|a..b|list> --output <directory>");
        PrintObservationConstraintDerivationUsage();
    }

    private static void PrintObservationConstraintDerivationUsage()
    {
        Console.WriteLine(
            "Observation-to-constraint derivation: dotnet run --project src/FootprintSim.Cli -- " +
            "evaluate-observation-constraint-derivation --fixture <fixture.json> " +
            "--output <directory>");
        PrintTimeResolutionPairedUsage();
    }

    private static void PrintTimeResolutionPairedUsage()
    {
        Console.WriteLine(
            "Paired time resolution: dotnet run --project src/FootprintSim.Cli -- " +
            "evaluate-time-resolution-paired --experiment <experiment.json> " +
            "--seeds 0..199 --output <directory>");
        PrintParticipantAmbiguityDerivationUsage();
    }

    private static void PrintParticipantAmbiguityDerivationUsage()
    {
        Console.WriteLine(
            "Participant ambiguity derivation: dotnet run --project src/FootprintSim.Cli -- " +
            "evaluate-participant-ambiguity-derivation --fixture <fixture.json> " +
            "--output <directory>");
        PrintParticipantAmbiguityPairedUsage();
    }

    private static void PrintParticipantAmbiguityPairedUsage()
    {
        Console.WriteLine(
            "Paired participant ambiguity: dotnet run --project src/FootprintSim.Cli -- " +
            "evaluate-participant-ambiguity-paired --experiment <experiment.json> " +
            "--seeds 0..199 --output <directory>");
        PrintRelationDependentDerivationUsage();
    }

    private static void PrintRelationDependentDerivationUsage()
    {
        Console.WriteLine(
            "Relation-dependent derivation: dotnet run --project src/FootprintSim.Cli -- " +
            "evaluate-relation-dependent-derivation --fixture <fixture.json> " +
            "--output <directory>");
        PrintConstraintCompositionUsage();
    }

    private static void PrintConstraintCompositionUsage()
    {
        Console.WriteLine(
            "Constraint composition: dotnet run --project src/FootprintSim.Cli -- " +
            "evaluate-constraint-composition --fixture <fixture.json> " +
            "--output <directory>");
        PrintCommonWorldObservationUsage();
    }

    private static void PrintCommonWorldObservationUsage()
    {
        Console.WriteLine(
            "Common-world observation vertical slice: dotnet run --project " +
            "src/FootprintSim.Cli -- evaluate-common-world-observation " +
            "--fixture <fixture.json> --output <directory>");
        PrintCommonWorldFieldCombinationUsage();
    }

    private static void PrintCommonWorldFieldCombinationUsage()
    {
        Console.WriteLine(
            "Common-world field combination: dotnet run --project " +
            "src/FootprintSim.Cli -- evaluate-common-world-field-combination " +
            "--experiment <experiment.json> --seeds 0..199 --output <directory>");
        PrintCommonWorldPlateauDomainAuditUsage();
    }

    private static void PrintCommonWorldPlateauDomainAuditUsage()
    {
        Console.WriteLine(
            "Plateau domain audit: dotnet run --project src/FootprintSim.Cli -- " +
            "audit-common-world-plateau-domain --experiment <experiment.json> " +
            "--baseline-experiment <f16-experiment.json> --seeds 0..199 --output <directory>");
    }
}

public static class SeedExpressionParser
{
    private const int MaximumSeedCount = 1_000_000;

    public static IReadOnlyList<int> Parse(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        var seeds = new SortedSet<int>();
        foreach (var component in expression.Split(',', StringSplitOptions.TrimEntries))
        {
            if (string.IsNullOrWhiteSpace(component))
            {
                throw new ArgumentException("Seed expressions cannot contain empty components.");
            }

            var separator = component.IndexOf("..", StringComparison.Ordinal);
            if (separator < 0)
            {
                Add(ParseSeed(component), seeds);
                continue;
            }

            if (component.IndexOf("..", separator + 2, StringComparison.Ordinal) >= 0)
            {
                throw new ArgumentException($"Invalid seed range '{component}'.");
            }

            var start = ParseSeed(component[..separator]);
            var end = ParseSeed(component[(separator + 2)..]);
            if (start > end)
            {
                throw new ArgumentException($"Seed range '{component}' is descending.");
            }

            var count = ((long)end - start) + 1;
            if (count > MaximumSeedCount)
            {
                throw new ArgumentException(
                    $"Seed range '{component}' exceeds {MaximumSeedCount} values.");
            }

            for (var seed = (long)start; seed <= end; seed++)
            {
                Add((int)seed, seeds);
            }
        }

        return seeds.ToArray();
    }

    private static int ParseSeed(string value)
    {
        return int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var seed)
            ? seed
            : throw new ArgumentException($"Invalid seed value '{value}'.");
    }

    private static void Add(int seed, ISet<int> seeds)
    {
        seeds.Add(seed);
        if (seeds.Count > MaximumSeedCount)
        {
            throw new ArgumentException($"Seed expression exceeds {MaximumSeedCount} unique values.");
        }
    }
}

internal static class GitMetadataReader
{
    public static GitMetadata Read(string workingDirectory)
    {
        var commit = RunGit(workingDirectory, "rev-parse", "HEAD");
        var status = RunGit(workingDirectory, "status", "--porcelain", "--untracked-files=normal");
        return new GitMetadata(commit.Trim(), !string.IsNullOrWhiteSpace(status));
    }

    private static string RunGit(string workingDirectory, params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        try
        {
            process.Start();
        }
        catch (Exception exception) when (exception is
            InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            throw new InvalidOperationException(
                "Git is required to write evaluation manifest metadata.",
                exception);
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Git metadata command failed: {error.Trim()}");
        }

        return output;
    }
}

internal sealed record GitMetadata(string CommitSha, bool DirtyWorkingTree);

internal sealed record GroundTruthEventLog
{
    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public required IReadOnlyList<FootprintSim.Events.SimulationEvent> Events { get; init; }
}

internal sealed record RunMetadata
{
    public required int SchemaVersion { get; init; }

    public required string EngineVersion { get; init; }

    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public required string ScenarioSha256 { get; init; }

    public required int GroundTruthEventCount { get; init; }

    public required int ObservationCount { get; init; }

    public required IReadOnlyList<string> Outputs { get; init; }
}
