using System.Text.Json;
using FootprintSim.Evidence;
using FootprintSim.Inference.PastStates;
using FootprintSim.Inference.SearchProjection;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;
using FootprintSim.Interactions.Tracing;
using FootprintSim.Serialization;
using Xunit;

namespace FootprintSim.Core.Tests;

public sealed class PublicSearchProjectionTests
{
    [Fact]
    public void ManyCandidatesCanShareOneUnitWithoutLosingIdentityOrDisposition()
    {
        var input = Fixture();
        var result = PublicSearchProjector.Project(input);

        Assert.Equal(2, result.Candidates.Count);
        Assert.Equal(new[] { "u1" }, result.KnownUnitIds);
        Assert.Empty(result.UnresolvedCandidateIds);
        Assert.Equal(new[] { "c1", "c2" }, result.Actions[0].WholeSupportContainedCandidateIds);
        Assert.Equal(PastStateCandidateDisposition.Conflicting, result.Candidates[1].SourceCandidate.Disposition);
        Assert.Equal(new[] { "ancestry-a", "ancestry-b" }, result.Candidates[0].SourceCandidate.SourceAncestryIds);
        Assert.Equal(new[] { "transition-r0" }, result.Candidates[0].TransitionSourceIds);
        Assert.Equal(new[] { "map-r2" }, result.Candidates[0].UnitMappingSourceIds);
    }

    [Fact]
    public void OneCandidateMayIntersectAnActionWithoutWholeSupportContainment()
    {
        var input = Fixture() with
        {
            Candidates = [Candidate("c1", "r0")],
            RegionProjections = [Transition("r0", ["r2", "r3"])],
            UnitMappings = [Mapping("r2", ["u1"]), Mapping("r3", ["u2"])],
            Actions = [Action("all", ["u1", "u2"]), Action("partial", ["u1"])]
        };
        var result = PublicSearchProjector.Project(input);

        Assert.Equal(new[] { "r2", "r3" }, result.KnownCurrentRegionIds);
        Assert.Equal(new[] { "u1", "u2" }, result.KnownUnitIds);
        Assert.Equal(new[] { "c1" }, result.Actions[0].WholeSupportContainedCandidateIds);
        Assert.Equal(new[] { "c1" }, result.Actions[1].KnownIntersectingCandidateIds);
        Assert.Equal(new[] { "u1" }, result.Actions[1].KnownIntersectingUnitIds);
        Assert.Empty(result.Actions[1].WholeSupportContainedCandidateIds);
    }

    [Theory]
    [InlineData("missing-transition", "transition-unmapped")]
    [InlineData("wrong-target", "transition-unmapped")]
    [InlineData("partial-transition", "transition-incomplete")]
    [InlineData("missing-unit-map", "unit-mapping-unmapped")]
    [InlineData("partial-unit-map", "unit-mapping-incomplete")]
    [InlineData("wrong-unit-time", "unit-mapping-unmapped")]
    public void MissingAndPartialKnowledgeNeverBecomesWholeSupportCoverage(string mode, string reason)
    {
        var input = Fixture() with { Candidates = [Candidate("c1", "r0")] };
        input = mode switch
        {
            "missing-transition" => input with { RegionProjections = [] },
            "wrong-target" => input with { RegionProjections = [Transition("r0", ["r2"]) with { TargetTick = 9 }] },
            "partial-transition" => input with { RegionProjections = [Transition("r0", ["r2"]) with { IsComplete = false }] },
            "missing-unit-map" => input with { UnitMappings = [] },
            "partial-unit-map" => input with { UnitMappings = [Mapping("r2", ["u1"]) with { IsComplete = false }] },
            _ => input with { UnitMappings = [Mapping("r2", ["u1"]) with { TargetTick = 9 }] }
        };

        var result = PublicSearchProjector.Project(input);

        Assert.Single(result.Candidates);
        Assert.Equal(new[] { "c1" }, result.UnresolvedCandidateIds);
        Assert.Contains(reason, result.Candidates[0].UnresolvedReasons);
        Assert.Empty(result.Actions[0].WholeSupportContainedCandidateIds);
        if (mode.StartsWith("partial", StringComparison.Ordinal))
        {
            Assert.Equal(new[] { "u1" }, result.KnownUnitIds);
            Assert.Equal(new[] { "c1" }, result.Actions[0].KnownIntersectingCandidateIds);
        }
    }

    [Fact]
    public void EmptyIncompleteTransitionRetainsAnUnknownCandidate()
    {
        var input = Fixture() with
        {
            Candidates = [Candidate("c1", "r0")],
            RegionProjections = [Transition("r0", []) with { IsComplete = false }]
        };
        var result = PublicSearchProjector.Project(input);

        Assert.Single(result.Candidates);
        Assert.Empty(result.KnownUnitIds);
        Assert.Equal(new[] { "c1" }, result.UnresolvedCandidateIds);
        Assert.All(result.Actions, action => Assert.Empty(action.WholeSupportContainedCandidateIds));
    }

    [Fact]
    public void HistoricalIntervalsRequireAnExactDeclaredWindowRatherThanOneEndpoint()
    {
        var input = Fixture() with
        {
            Candidates = [Candidate("c1", "r0") with { TargetTimeWindow = new InteractionTimeWindow(1, 3) }]
        };
        Assert.Equal(new[] { "c1" }, PublicSearchProjector.Project(input).UnresolvedCandidateIds);

        var declared = input with { RegionProjections = [Transition("r0", ["r2"]) with { EndTick = 3 }] };
        var result = PublicSearchProjector.Project(declared);
        Assert.Empty(result.UnresolvedCandidateIds);
        Assert.Equal(1L, result.Candidates[0].SourceCandidate.TargetTimeWindow.StartTick);
        Assert.Equal(3L, result.Candidates[0].SourceCandidate.TargetTimeWindow.EndTick);
        Assert.Equal(10L, result.Candidates[0].TargetTick);
    }

    [Fact]
    public void UnknownBranchSurvivesAlongsideAMappedBranch()
    {
        var input = Fixture() with
        {
            Candidates = [Candidate("c1", "r0")],
            RegionProjections = [Transition("r0", ["r2", "r3"])],
            UnitMappings = [Mapping("r2", ["u1"])]
        };
        var result = PublicSearchProjector.Project(input);

        Assert.Equal(new[] { "r2", "r3" }, result.KnownCurrentRegionIds);
        Assert.Equal(new[] { "u1" }, result.KnownUnitIds);
        Assert.Equal(new[] { "c1" }, result.UnresolvedCandidateIds);
        Assert.Equal(new[] { "c1" }, result.Actions[0].KnownIntersectingCandidateIds);
        Assert.Empty(result.Actions[0].WholeSupportContainedCandidateIds);
    }

    [Fact]
    public void RepeatedAndPermutedInputsProduceIdenticalOutputWithoutInputMutation()
    {
        var input = Fixture();
        var before = JsonDefaults.Serialize(input);
        var result = JsonDefaults.Serialize(PublicSearchProjector.Project(input));
        var permuted = input with
        {
            Candidates = input.Candidates.Reverse().Select(candidate => candidate with
            {
                SourceAncestryIds = candidate.SourceAncestryIds.Reverse().ToArray()
            }).ToArray(),
            RegionIds = input.RegionIds.Reverse().ToArray(),
            SearchUnitIds = input.SearchUnitIds.Reverse().ToArray(),
            RegionProjections = input.RegionProjections.Reverse().ToArray(),
            UnitMappings = input.UnitMappings.Reverse().ToArray(),
            Actions = input.Actions.Reverse().Select(action => action with
            {
                UnitIds = action.UnitIds.Reverse().ToArray()
            }).ToArray()
        };

        Assert.Equal(result, JsonDefaults.Serialize(PublicSearchProjector.Project(permuted)));
        Assert.Equal(result, JsonDefaults.Serialize(PublicSearchProjector.Project(input)));
        Assert.Equal(before, JsonDefaults.Serialize(input));
    }

    [Fact]
    public void EmptyCandidateSetDoesNotMakeAnActionVacuouslySuccessful()
    {
        var result = PublicSearchProjector.Project(Fixture() with { Candidates = [] });

        Assert.Empty(result.Candidates);
        Assert.Empty(result.KnownUnitIds);
        Assert.Empty(result.UnresolvedCandidateIds);
        Assert.All(result.Actions, action =>
        {
            Assert.Empty(action.KnownIntersectingCandidateIds);
            Assert.Empty(action.WholeSupportContainedCandidateIds);
        });
    }

    [Theory]
    [InlineData("foreign-query")]
    [InlineData("foreign-entity")]
    [InlineData("duplicate-candidate")]
    [InlineData("invalid-disposition")]
    [InlineData("future-candidate-window")]
    [InlineData("negative-cutoff")]
    [InlineData("cutoff-after-target")]
    [InlineData("future-transition")]
    [InlineData("future-mapping")]
    [InlineData("future-action")]
    [InlineData("unknown-region")]
    [InlineData("unknown-unit")]
    [InlineData("empty-complete-transition")]
    [InlineData("empty-complete-mapping")]
    [InlineData("duplicate-transition")]
    [InlineData("duplicate-mapping")]
    [InlineData("duplicate-action")]
    [InlineData("wrong-action-time")]
    [InlineData("invalid-transition-window")]
    [InlineData("invalid-source")]
    [InlineData("duplicate-catalogue")]
    public void MalformedOrUnavailableInputsFailBeforeReturningProjection(string mode)
    {
        var input = Fixture();
        var candidate = input.Candidates[0];
        var transition = input.RegionProjections[0];
        var mapping = input.UnitMappings[0];
        var action = input.Actions[0];
        input = mode switch
        {
            "foreign-query" => input with { Candidates = [candidate with { QueryId = "other-query" }] },
            "foreign-entity" => input with { Candidates = [candidate with { TargetEntityId = "device-1" }] },
            "duplicate-candidate" => input with { Candidates = [candidate, candidate] },
            "invalid-disposition" => input with { Candidates = [candidate with { Disposition = (PastStateCandidateDisposition)99 }] },
            "future-candidate-window" => input with { Candidates = [candidate with { TargetTimeWindow = InteractionTimeWindow.At(11) }] },
            "negative-cutoff" => input with { EvidenceCutoff = -1 },
            "cutoff-after-target" => input with { EvidenceCutoff = 11 },
            "future-transition" => input with { RegionProjections = [transition with { AvailableTick = 9 }] },
            "future-mapping" => input with { UnitMappings = [mapping with { AvailableTick = 9 }] },
            "future-action" => input with { Actions = [action with { AvailableTick = 9 }] },
            "unknown-region" => input with { RegionProjections = [transition with { CurrentRegionIds = ["unknown"] }] },
            "unknown-unit" => input with { UnitMappings = [mapping with { UnitIds = ["unknown"] }] },
            "empty-complete-transition" => input with { RegionProjections = [transition with { CurrentRegionIds = [] }] },
            "empty-complete-mapping" => input with { UnitMappings = [mapping with { UnitIds = [] }] },
            "duplicate-transition" => input with { RegionProjections = [transition, transition] },
            "duplicate-mapping" => input with { UnitMappings = [mapping, mapping] },
            "duplicate-action" => input with { Actions = [action, action] },
            "wrong-action-time" => input with { Actions = [action with { TargetTick = 11 }] },
            "invalid-transition-window" => input with { RegionProjections = [transition with { EndTick = 0 }] },
            "invalid-source" => input with { RegionProjections = [transition with { SourceId = " " }] },
            _ => input with { RegionIds = ["r0", "r0"] }
        };

        Assert.Throws<InvalidDataException>(() => PublicSearchProjector.Project(input));
    }

    [Fact]
    public void SourceAtExactCutoffIsUsableAndSharedLineageIsNotCountedAsMoreLocations()
    {
        var input = Fixture() with
        {
            RegionProjections = [Transition("r0", ["r2"]) with { AvailableTick = 8, SourceId = "shared" },
                Transition("r1", ["r2"]) with { AvailableTick = 8, SourceId = "shared" }],
            UnitMappings = [Mapping("r2", ["u1"]) with { AvailableTick = 8 }]
        };
        var result = PublicSearchProjector.Project(input);

        Assert.Equal(2, result.Candidates.Count);
        Assert.Single(result.KnownUnitIds);
        Assert.All(result.Candidates, item => Assert.Equal(new[] { "shared" }, item.TransitionSourceIds));
    }

    [Theory]
    [InlineData("many-to-one")]
    [InlineData("one-to-many")]
    [InlineData("missing-transition")]
    [InlineData("partial-mapping")]
    public async Task CliWritesDeterministicPublicReports(string mode)
    {
        var input = Fixture();
        input = mode switch
        {
            "one-to-many" => input with
            {
                Candidates = [Candidate("c1", "r0")],
                RegionProjections = [Transition("r0", ["r2", "r3"])],
                UnitMappings = [Mapping("r2", ["u1"]), Mapping("r3", ["u2"])],
                Actions = [Action("all", ["u1", "u2"]), Action("partial", ["u1"])]
            },
            "missing-transition" => input with { RegionProjections = [] },
            "partial-mapping" => input with
            {
                UnitMappings = [Mapping("r2", ["u1"]) with { IsComplete = false }]
            },
            _ => input
        };
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var inputPath = Path.Combine(directory, "public.json");
            var outputPath = Path.Combine(directory, "report.json");
            var expected = JsonDefaults.Serialize(PublicSearchProjector.Project(input)) + "\n";
            byte[]? first = null;
            for (var iteration = 0; iteration < 3; iteration++)
            {
                var supplied = iteration == 2 ? input with
                {
                    Candidates = input.Candidates.Reverse().Select(candidate => candidate with
                    {
                        SourceAncestryIds = candidate.SourceAncestryIds.Reverse().ToArray()
                    }).ToArray(),
                    RegionIds = input.RegionIds.Reverse().ToArray(),
                    SearchUnitIds = input.SearchUnitIds.Reverse().ToArray(),
                    RegionProjections = input.RegionProjections.Reverse().Select(row => row with
                    {
                        CurrentRegionIds = row.CurrentRegionIds.Reverse().ToArray()
                    }).ToArray(),
                    UnitMappings = input.UnitMappings.Reverse().Select(row => row with
                    {
                        UnitIds = row.UnitIds.Reverse().ToArray()
                    }).ToArray(),
                    Actions = input.Actions.Reverse().Select(action => action with
                    {
                        UnitIds = action.UnitIds.Reverse().ToArray()
                    }).ToArray()
                } : input;
                await File.WriteAllTextAsync(inputPath, JsonDefaults.Serialize(supplied));
                Assert.Equal(0, await FootprintSim.Cli.CliApplication.RunAsync(
                    ["evaluate-public-search-projection", "--input", inputPath, "--output", outputPath]));
                Assert.Equal(expected, await File.ReadAllTextAsync(outputPath));
                var bytes = await File.ReadAllBytesAsync(outputPath);
                Assert.Equal(System.Text.Encoding.UTF8.GetBytes(expected), bytes);
                if (first is not null)
                {
                    Assert.Equal(first, bytes);
                }

                first = bytes;
            }

            Assert.Equal(2, Directory.GetFiles(directory).Length);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("broken-json")]
    [InlineData("null")]
    [InlineData("future-row")]
    [InlineData("foreign-query")]
    [InlineData("foreign-entity")]
    [InlineData("invalid-id")]
    [InlineData("invalid-time")]
    public async Task CliRejectsInvalidPublicInputWithoutPublishingAReport(string mode)
    {
        var input = Fixture();
        input = mode switch
        {
            "future-row" => input with
            {
                RegionProjections = [input.RegionProjections[0] with { AvailableTick = 9 }]
            },
            "foreign-query" => input with
            {
                Candidates = [input.Candidates[0] with { QueryId = "foreign" }]
            },
            "foreign-entity" => input with
            {
                Candidates = [input.Candidates[0] with { TargetEntityId = "device-1" }]
            },
            "invalid-id" => input with { QueryId = " " },
            "invalid-time" => input with { EvidenceCutoff = -1 },
            _ => input
        };
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var inputPath = Path.Combine(directory, "public.json");
            var outputPath = Path.Combine(directory, "report.json");
            var json = mode switch
            {
                "broken-json" => "{",
                "null" => "null",
                _ => JsonDefaults.Serialize(input)
            };
            await File.WriteAllTextAsync(inputPath, json);
            Assert.Equal(2, await FootprintSim.Cli.CliApplication.RunAsync(
                ["evaluate-public-search-projection", "--input", inputPath, "--output", outputPath]));
            Assert.False(File.Exists(outputPath));
            Assert.Single(Directory.GetFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AdapterConnectsPublicReconstructionAndSnapshotsDeclarations()
    {
        var reconstruction = ReconstructPublicFixture();
        var query = reconstruction.Query;
        var original = JsonDefaults.Serialize(reconstruction);
        var fixture = Fixture();
        var targets = new List<string> { "r2" };
        var units = new List<string> { "u1" };
        var rows = new List<PublicRegionProjection>
        {
            Transition("r0", targets), Transition("r1", targets)
        };
        var mappings = new List<PublicSearchUnitMapping> { Mapping("r2", units) };
        var actions = new List<PublicActionFootprint> { Action("all", units) };
        var input = PublicPastStateProjectionAdapter.CreateInput(
            reconstruction, 10, fixture.RegionIds, fixture.SearchUnitIds, rows, mappings, actions);
        var result = PublicSearchProjector.Project(input);
        var expected = JsonDefaults.Serialize(result);

        Assert.Equal(query.QueryId, input.QueryId);
        Assert.Equal(query.TargetEntityId, input.TargetEntityId);
        Assert.Equal(query.EvidenceAvailabilityCutoff, input.EvidenceCutoff);
        var ids = reconstruction.Candidates.Select(candidate => candidate.CandidateId)
            .OrderBy(id => id, StringComparer.Ordinal).ToArray();
        Assert.Equal(2, ids.Length);
        Assert.Equal(ids, result.Candidates.Select(candidate => candidate.SourceCandidate.CandidateId));
        Assert.Equal(new[] { "r2" }, result.KnownCurrentRegionIds);
        Assert.Equal(new[] { "u1" }, result.KnownUnitIds);
        Assert.Empty(result.UnresolvedCandidateIds);
        Assert.Equal(ids, result.Actions[0].KnownIntersectingCandidateIds);
        Assert.Equal(ids, result.Actions[0].WholeSupportContainedCandidateIds);

        targets.Clear();
        units.Clear();
        rows.Clear();
        mappings.Clear();
        actions.Clear();
        Assert.Equal(expected, JsonDefaults.Serialize(PublicSearchProjector.Project(input)));
        Assert.Equal(original, JsonDefaults.Serialize(reconstruction));

        Assert.Throws<InvalidDataException>(() => PublicPastStateProjectionAdapter.CreateInput(
            reconstruction, 7, fixture.RegionIds, fixture.SearchUnitIds, [], [], []));
        Assert.Throws<InvalidDataException>(() => PublicPastStateProjectionAdapter.CreateInput(
            reconstruction with { EvidenceSet = reconstruction.EvidenceSet with { QueryId = "foreign" } },
            10, fixture.RegionIds, fixture.SearchUnitIds, [], [], []));
        Assert.Throws<InvalidDataException>(() => PublicPastStateProjectionAdapter.CreateInput(
            reconstruction with
            {
                Candidates = [reconstruction.Candidates[0] with { TargetTimeWindow = new InteractionTimeWindow(1, 2) }]
            },
            10, fixture.RegionIds, fixture.SearchUnitIds, [], [], []));
    }

    [Theory]
    [InlineData("many-to-one")]
    [InlineData("one-to-many")]
    [InlineData("partial-transition")]
    [InlineData("partial-mapping")]
    [InlineData("missing-mapping")]
    [InlineData("all-unknown")]
    [InlineData("empty-partial-transition")]
    [InlineData("wrong-window")]
    public async Task AdapterConnectsObservedReconstructionToCliWithIndependentExpectedSets(string mode)
    {
        var reconstruction = ReconstructPublicFixture();
        var original = JsonDefaults.Serialize(reconstruction);
        var declarations = AdapterDeclarations(mode);
        var declaredBefore = JsonDefaults.Serialize(declarations);
        var ids = reconstruction.Candidates.Select(candidate => candidate.CandidateId)
            .OrderBy(id => id, StringComparer.Ordinal).ToArray();
        var noKnown = mode is "all-unknown" or "empty-partial-transition" or "wrong-window";
        var complete = mode is "many-to-one" or "one-to-many";
        var twoUnits = mode is "one-to-many" or "partial-transition" or "partial-mapping";
        string[] expectedRegions = noKnown ? [] : mode == "many-to-one" ? ["r2"] : ["r2", "r3"];
        string[] expectedUnits = noKnown ? [] : twoUnits ? ["u1", "u2"] : ["u1"];
        string[] knownIds = noKnown ? [] : ids;
        string[] containedIds = complete ? ids : [];
        string[] unresolvedIds = complete ? [] : ids;
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var inputPath = Path.Combine(directory, "public.json");
            var outputPath = Path.Combine(directory, "report.json");
            byte[]? first = null;
            for (var iteration = 0; iteration < 3; iteration++)
            {
                var source = iteration == 2 ? reconstruction with
                {
                    Candidates = reconstruction.Candidates.Reverse().Select(ReverseMetadata).ToArray()
                } : reconstruction;
                var maps = iteration == 2 ? PermuteDeclarations(declarations) : declarations;
                var input = Adapt(source, maps);
                var expectedBytes = System.Text.Encoding.UTF8.GetBytes(
                    JsonDefaults.Serialize(PublicSearchProjector.Project(input)) + "\n");
                await File.WriteAllTextAsync(inputPath, JsonDefaults.Serialize(input));
                Assert.Equal(0, await FootprintSim.Cli.CliApplication.RunAsync(
                    ["evaluate-public-search-projection", "--input", inputPath, "--output", outputPath]));
                var bytes = await File.ReadAllBytesAsync(outputPath);
                Assert.Equal(expectedBytes, bytes);
                if (first is not null)
                {
                    Assert.Equal(first, bytes);
                }

                first = bytes;
                var report = JsonSerializer.Deserialize<PublicSearchProjectionResult>(bytes, JsonDefaults.Options);
                Assert.NotNull(report);
                Assert.Equal(reconstruction.Query.QueryId, report.QueryId);
                Assert.Equal(reconstruction.Query.TargetEntityId, report.TargetEntityId);
                Assert.Equal(8L, report.EvidenceCutoff);
                Assert.Equal(10L, report.TargetTick);
                Assert.Equal(ids, report.Candidates.Select(item => item.SourceCandidate.CandidateId));
                Assert.Equal(expectedRegions, report.KnownCurrentRegionIds);
                Assert.Equal(expectedUnits, report.KnownUnitIds);
                Assert.Equal(unresolvedIds, report.UnresolvedCandidateIds);
                Assert.Equal(JsonDefaults.Serialize(input.Candidates),
                    JsonDefaults.Serialize(report.Candidates.Select(item => item.SourceCandidate).ToArray()));
                Assert.All(report.Candidates, item =>
                {
                    Assert.Equal(1L, item.SourceCandidate.TargetTimeWindow.StartTick);
                    Assert.Equal(1L, item.SourceCandidate.TargetTimeWindow.EndTick);
                    Assert.Contains("adapter-observation", item.SourceCandidate.GenerationEvidenceIds);
                    Assert.Contains("adapter-source", item.SourceCandidate.SourceAncestryIds);
                    if (!complete)
                    {
                        Assert.True(item.HasUnresolvedPossibilities);
                    }
                });
                var all = Assert.Single(report.Actions, action => action.ActionId == "all");
                var partial = Assert.Single(report.Actions, action => action.ActionId == "partial");
                var outside = Assert.Single(report.Actions, action => action.ActionId == "outside");
                Assert.Equal(expectedUnits, all.KnownIntersectingUnitIds);
                Assert.Equal(knownIds, all.KnownIntersectingCandidateIds);
                Assert.Equal(containedIds, all.WholeSupportContainedCandidateIds);
                Assert.Equal(noKnown ? Array.Empty<string>() : new[] { "u1" }, partial.KnownIntersectingUnitIds);
                Assert.Equal(knownIds, partial.KnownIntersectingCandidateIds);
                Assert.Equal(mode == "many-to-one" ? ids : Array.Empty<string>(), partial.WholeSupportContainedCandidateIds);
                Assert.Empty(outside.KnownIntersectingCandidateIds);
                Assert.Empty(outside.WholeSupportContainedCandidateIds);
            }

            Assert.Equal(2, Directory.GetFiles(directory).Length);
            Assert.Equal(original, JsonDefaults.Serialize(reconstruction));
            Assert.Equal(declaredBefore, JsonDefaults.Serialize(declarations));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("foreign-evidence")]
    [InlineData("foreign-query")]
    [InlineData("foreign-entity")]
    [InlineData("different-window")]
    [InlineData("duplicate-candidate")]
    [InlineData("target-before-cutoff")]
    [InlineData("future-transition")]
    [InlineData("future-mapping")]
    [InlineData("future-action")]
    [InlineData("duplicate-transition")]
    [InlineData("duplicate-mapping")]
    [InlineData("duplicate-action")]
    [InlineData("complete-empty-transition")]
    [InlineData("complete-empty-mapping")]
    [InlineData("unknown-region")]
    [InlineData("unknown-unit")]
    [InlineData("invalid-source")]
    public void AdapterRejectsInconsistentReconstructionAndDeclarations(string mode)
    {
        var reconstruction = ReconstructPublicFixture();
        var declarations = AdapterDeclarations("many-to-one");
        var candidate = reconstruction.Candidates[0];
        reconstruction = mode switch
        {
            "foreign-evidence" => reconstruction with { EvidenceSet = reconstruction.EvidenceSet with { QueryId = "foreign" } },
            "foreign-query" => reconstruction with { Candidates = [candidate with { QueryId = "foreign" }] },
            "foreign-entity" => reconstruction with { Candidates = [candidate with { TargetEntityId = "device-1" }] },
            "different-window" => reconstruction with { Candidates = [candidate with { TargetTimeWindow = new InteractionTimeWindow(1, 2) }] },
            "duplicate-candidate" => reconstruction with { Candidates = [candidate, candidate] },
            _ => reconstruction
        };
        var transition = declarations.RegionProjections[0];
        var mapping = declarations.UnitMappings[0];
        var action = declarations.Actions[0];
        declarations = mode switch
        {
            "target-before-cutoff" => declarations with { TargetTick = 7 },
            "future-transition" => declarations with { RegionProjections = [transition with { AvailableTick = 9 }] },
            "future-mapping" => declarations with { UnitMappings = [mapping with { AvailableTick = 9 }] },
            "future-action" => declarations with { Actions = [action with { AvailableTick = 9 }] },
            "duplicate-transition" => declarations with { RegionProjections = [transition, transition with { CurrentRegionIds = ["r3"] }] },
            "duplicate-mapping" => declarations with { UnitMappings = [mapping, mapping with { UnitIds = ["u2"] }] },
            "duplicate-action" => declarations with { Actions = [action, action] },
            "complete-empty-transition" => declarations with { RegionProjections = [transition with { CurrentRegionIds = [] }] },
            "complete-empty-mapping" => declarations with { UnitMappings = [mapping with { UnitIds = [] }] },
            "unknown-region" => declarations with { RegionProjections = [transition with { CurrentRegionIds = ["unknown"] }] },
            "unknown-unit" => declarations with { UnitMappings = [mapping with { UnitIds = ["unknown"] }] },
            "invalid-source" => declarations with { RegionProjections = [transition with { SourceId = " " }] },
            _ => declarations
        };
        Assert.Throws<InvalidDataException>(() => Adapt(reconstruction, declarations));
    }

    [Fact]
    public void AdapterSnapshotsEveryCandidateMetadataAndNestedMappingList()
    {
        var source = ReconstructPublicFixture();
        var candidates = source.Candidates.Select(candidate => candidate with
        {
            GenerationEvidenceIds = candidate.GenerationEvidenceIds.ToList(),
            GenerationSourceGroupIds = candidate.GenerationSourceGroupIds.ToList(),
            SourceAncestryIds = candidate.SourceAncestryIds.ToList(),
            AppliedConstraintIds = candidate.AppliedConstraintIds.ToList(),
            UnresolvedConstraintIds = candidate.UnresolvedConstraintIds.ToList(),
            ConflictingConstraintIds = candidate.ConflictingConstraintIds.ToList(),
            PublicRelationReferences = candidate.PublicRelationReferences.ToList()
        }).ToList();
        var reconstruction = source with { Candidates = candidates };
        var declarations = AdapterDeclarations("one-to-many");
        declarations = declarations with
        {
            RegionIds = declarations.RegionIds.ToList(),
            SearchUnitIds = declarations.SearchUnitIds.ToList(),
            RegionProjections = declarations.RegionProjections.Select(row => row with { CurrentRegionIds = row.CurrentRegionIds.ToList() }).ToList(),
            UnitMappings = declarations.UnitMappings.Select(row => row with { UnitIds = row.UnitIds.ToList() }).ToList(),
            Actions = declarations.Actions.Select(row => row with { UnitIds = row.UnitIds.ToList() }).ToList()
        };
        var sourceBefore = JsonDefaults.Serialize(reconstruction);
        var mapsBefore = JsonDefaults.Serialize(declarations);
        var input = Adapt(reconstruction, declarations);
        var snapshot = JsonDefaults.Serialize(input);
        var expected = JsonDefaults.Serialize(PublicSearchProjector.Project(input));
        Assert.Equal(sourceBefore, JsonDefaults.Serialize(reconstruction));
        Assert.Equal(mapsBefore, JsonDefaults.Serialize(declarations));
        foreach (var candidate in candidates)
        {
            ((List<string>)candidate.GenerationEvidenceIds).Add("mutation");
            ((List<string>)candidate.GenerationSourceGroupIds).Add("mutation");
            ((List<string>)candidate.SourceAncestryIds).Add("mutation");
            ((List<string>)candidate.AppliedConstraintIds).Add("mutation");
            ((List<string>)candidate.UnresolvedConstraintIds).Add("mutation");
            ((List<string>)candidate.ConflictingConstraintIds).Add("mutation");
            ((List<string>)candidate.PublicRelationReferences).Add("mutation");
        }

        foreach (var row in declarations.RegionProjections)
        {
            ((List<string>)row.CurrentRegionIds).Clear();
        }

        foreach (var row in declarations.UnitMappings)
        {
            ((List<string>)row.UnitIds).Clear();
        }

        foreach (var action in declarations.Actions)
        {
            ((List<string>)action.UnitIds).Clear();
        }

        candidates.Clear();
        ((List<string>)declarations.RegionIds).Clear();
        ((List<string>)declarations.SearchUnitIds).Clear();
        ((List<PublicRegionProjection>)declarations.RegionProjections).Clear();
        ((List<PublicSearchUnitMapping>)declarations.UnitMappings).Clear();
        ((List<PublicActionFootprint>)declarations.Actions).Clear();
        Assert.Equal(snapshot, JsonDefaults.Serialize(input));
        Assert.Equal(expected, JsonDefaults.Serialize(PublicSearchProjector.Project(input)));
    }

    [Fact]
    public void AdapterKeepsAllDispositionsAndEmptyReconstructionHasNoContainment()
    {
        var reconstruction = ReconstructPublicFixture();
        var declarations = AdapterDeclarations("many-to-one");
        foreach (var disposition in Enum.GetValues<PastStateCandidateDisposition>())
        {
            var supplied = reconstruction with
            {
                Candidates = reconstruction.Candidates.Select(item => item with { Disposition = disposition }).ToArray()
            };
            var result = PublicSearchProjector.Project(Adapt(supplied, declarations));
            Assert.Equal(reconstruction.Candidates.Count, result.Candidates.Count);
            Assert.All(result.Candidates, item => Assert.Equal(disposition, item.SourceCandidate.Disposition));
        }

        var empty = PublicSearchProjector.Project(Adapt(reconstruction with { Candidates = [] }, declarations));
        Assert.Empty(empty.Candidates);
        Assert.Empty(empty.KnownUnitIds);
        Assert.All(empty.Actions, item => Assert.Empty(item.WholeSupportContainedCandidateIds));
        Assert.Throws<ArgumentNullException>(() => Adapt(null!, declarations));
        Assert.Throws<ArgumentNullException>(() => Adapt(reconstruction with { Query = null! }, declarations));
        Assert.Throws<ArgumentNullException>(() => Adapt(reconstruction with { EvidenceSet = null! }, declarations));
        Assert.Throws<ArgumentNullException>(() => Adapt(reconstruction with { Candidates = null! }, declarations));
    }

    private static PastStateReconstructionResult ReconstructPublicFixture()
    {
        var query = PastStateQuery.Create(
            "adapter-fixture", 0, "person-1", InteractionTimeWindow.At(1), "public-query", 8);
        var observation = new ObservationRecord
        {
            ObservationId = "adapter-observation",
            ObservedTick = 1,
            AvailableTick = 4,
            EvidenceType = "synthetic-public-region-alternatives",
            SubjectIds = ["person-1"],
            State = EvidenceState.Confirmed
        };
        var constraint = CausalTracingConstraint.Create(
            query.ScenarioId, query.Seed, observation.ObservationId, "adapter-source", ["adapter-root"],
            query.TargetEntityId, TracingConstraintSourceKind.PublicEnvironmentalObservation, "public-location",
            [new InteractionParticipant("person-1", InteractionEntityKind.Person, InteractionParticipantRole.EncounterParticipant)],
            TracingConstraintRole.Local, query.TargetTimeWindow, query.TargetTimeWindow, ["r0", "r1"],
            4, EvidenceState.Confirmed, true, false, InteractionVisibility.PublicEvidence,
            "explicit-synthetic-alternatives", [new InteractionProvenance(InteractionProvenanceKind.EvidenceRecord, observation.ObservationId)]);
        var context = new PastStatePublicContext
        {
            Query = query,
            PublicCandidateStates =
            [
                new PublicCandidateWarpStateDefinition
                {
                    TimeWindow = query.TargetTimeWindow,
                    TemporalKind = CandidateWarpTemporalKind.EventPoint,
                    RegionIds = ["r0", "r1", "r3"],
                    EvidenceBoundaryState = EvidenceState.Confirmed,
                    Provenance =
                    [
                        CandidateWarpProvenance.Create(CandidateWarpSourceKind.PublicCausalModel,
                            "adapter-public-model", 0, false, false, EvidenceState.Confirmed, ["r0", "r1", "r3"])
                    ]
                }
            ],
            GenerationDirectSourceIds = ["adapter-source"],
            LocalTraversalWindowPaddingTicks = 0
        };
        var result = new PastStateReconstructor().Compare(context,
            new ObservationSet { ScenarioId = query.ScenarioId, Seed = query.Seed, Observations = [observation] },
            new CausalTracingConstraintSet(query.ScenarioId, query.Seed, [constraint])).Reconstruction;
        // This fixture actually reconstructs and narrows three public regions to two.
        Assert.Equal(3, result.InitialCandidateCount);
        Assert.Equal(new[] { "r0", "r1" }, result.Candidates.Select(item => item.RegionId));
        Assert.Equal(new[] { observation.ObservationId }, result.EvidenceSet.ObservationIds);
        Assert.Single(result.EvidenceSet.Constraints);
        Assert.All(result.Candidates, item => Assert.Contains(constraint.ConstraintId, item.AppliedConstraintIds));
        return result;
    }

    private static PublicSearchProjectionInput Adapt(
        PastStateReconstructionResult reconstruction, PublicSearchProjectionInput declarations) =>
        PublicPastStateProjectionAdapter.CreateInput(reconstruction, declarations.TargetTick,
            declarations.RegionIds, declarations.SearchUnitIds, declarations.RegionProjections,
            declarations.UnitMappings, declarations.Actions);

    private static PublicSearchProjectionInput AdapterDeclarations(string mode)
    {
        var input = Fixture() with
        {
            RegionProjections = [Transition("r0", ["r2", "r3"]), Transition("r1", ["r2", "r3"])],
            UnitMappings = [Mapping("r2", ["u1"]), Mapping("r3", ["u2"])],
            Actions = [Action("all", ["u1", "u2"]), Action("partial", ["u1"]), Action("outside", [])]
        };
        return mode switch
        {
            "many-to-one" => input with { RegionProjections = [Transition("r0", ["r2"]), Transition("r1", ["r2"])] },
            "partial-transition" => input with { RegionProjections = input.RegionProjections.Select(row => row with { IsComplete = false }).ToArray() },
            "partial-mapping" => input with { UnitMappings = [Mapping("r2", ["u1"]), Mapping("r3", ["u2"]) with { IsComplete = false }] },
            "missing-mapping" => input with { UnitMappings = [Mapping("r2", ["u1"])] },
            "all-unknown" => input with { RegionProjections = [] },
            "empty-partial-transition" => input with { RegionProjections = input.RegionProjections.Select(row => row with { CurrentRegionIds = [], IsComplete = false }).ToArray() },
            "wrong-window" => input with { RegionProjections = input.RegionProjections.Select(row => row with { EndTick = 2 }).ToArray() },
            _ => input
        };
    }

    private static PastStateCandidate ReverseMetadata(PastStateCandidate candidate) => candidate with
    {
        GenerationEvidenceIds = candidate.GenerationEvidenceIds.Reverse().ToArray(),
        GenerationSourceGroupIds = candidate.GenerationSourceGroupIds.Reverse().ToArray(),
        SourceAncestryIds = candidate.SourceAncestryIds.Reverse().ToArray(),
        AppliedConstraintIds = candidate.AppliedConstraintIds.Reverse().ToArray(),
        UnresolvedConstraintIds = candidate.UnresolvedConstraintIds.Reverse().ToArray(),
        ConflictingConstraintIds = candidate.ConflictingConstraintIds.Reverse().ToArray(),
        PublicRelationReferences = candidate.PublicRelationReferences.Reverse().ToArray()
    };

    private static PublicSearchProjectionInput PermuteDeclarations(PublicSearchProjectionInput input) => input with
    {
        RegionIds = input.RegionIds.Reverse().ToArray(),
        SearchUnitIds = input.SearchUnitIds.Reverse().ToArray(),
        RegionProjections = input.RegionProjections.Reverse().Select(row => row with { CurrentRegionIds = row.CurrentRegionIds.Reverse().ToArray() }).ToArray(),
        UnitMappings = input.UnitMappings.Reverse().Select(row => row with { UnitIds = row.UnitIds.Reverse().ToArray() }).ToArray(),
        Actions = input.Actions.Reverse().Select(action => action with { UnitIds = action.UnitIds.Reverse().ToArray() }).ToArray()
    };

    private static PublicSearchProjectionInput Fixture() => new(
        "query-1", "person-1", 10, 8,
        [Candidate("c1", "r0"), Candidate("c2", "r1") with { Disposition = PastStateCandidateDisposition.Conflicting }],
        ["r0", "r1", "r2", "r3"], ["u1", "u2"],
        [Transition("r0", ["r2"]), Transition("r1", ["r2"])],
        [Mapping("r2", ["u1"])],
        [Action("all", ["u1"]), Action("outside", ["u2"])]);

    private static PastStateCandidate Candidate(string id, string region) => new()
    {
        CandidateId = id,
        QueryId = "query-1",
        TargetEntityId = "person-1",
        TargetTimeWindow = InteractionTimeWindow.At(1),
        RegionId = region,
        Disposition = PastStateCandidateDisposition.Uncertain,
        GenerationEvidenceIds = ["public-observation-1"],
        SourceAncestryIds = ["ancestry-b", "ancestry-a"]
    };

    private static PublicRegionProjection Transition(string region, IReadOnlyList<string> targets) =>
        new(region, 1, 1, 10, targets, true, "transition-" + region, 0);

    private static PublicSearchUnitMapping Mapping(string region, IReadOnlyList<string> units) =>
        new(region, 10, units, true, "map-" + region, 0);

    private static PublicActionFootprint Action(string id, IReadOnlyList<string> units) =>
        new(id, 10, units, "action-" + id, 0);
}
