using FootprintSim.Evidence;
using FootprintSim.Evaluation;
using FootprintSim.Evaluation.Truth;
using FootprintSim.Inference;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;
using FootprintSim.Model;
using FootprintSim.Scenarios;
using FootprintSim.Serialization;
using Xunit;

namespace FootprintSim.Core.Tests;

public sealed class SearchDomainTests
{
    [Fact]
    public void ProjectionScopesEveryReportedSetWithoutAddingLogicalExclusions()
    {
        var input = Input() with { PublicSearchDomainRegionIds = ["inside"] };
        var result = Result();
        var projected = PublicSearchDomainProjection.Apply(input, result);

        Assert.Equal(["inside"], projected.CandidateRegionIds);
        Assert.Equal(["inside"], projected.PreferredCandidateRegionIds);
        Assert.Equal(["inside"], projected.OuterPlausibleCandidateRegionIds);
        Assert.Equal(["inside"], projected.ConflictedCandidateRegionIds);
        Assert.Empty(projected.ConfirmedPersonRegionIds);
        Assert.Empty(projected.ExplicitlyExcludedRegionIds);
        Assert.Equal(["inside"], projected.CandidateRegionProvenance.Select(item => item.RegionId));
        Assert.Equal(["fixture", PublicSearchDomainProjection.ScopeRationale], projected.RationaleCodes);
        Assert.Equal(["outside", "inside"], result.CandidateRegionIds);
        Assert.Same(projected, PublicSearchDomainProjection.Apply(input, projected));

        var evaluation = new PerRunEvaluator().Evaluate(new GroundTruthEvaluationContext
        {
            ScenarioId = "domain-fixture",
            Seed = 7,
            TargetPersonId = "person",
            TruePersonRegionId = "inside",
            SearchableRegionIds = ["inside"]
        }, projected, [projected], input.AvailableEvidence);
        Assert.Equal(1, evaluation.SearchableRegionCount);
        Assert.Equal(false, evaluation.ExplicitFalseExclusion.Value);
        Assert.Equal(true, evaluation.GroundTruthContainment.Value);
    }

    [Fact]
    public void MissingOrUnrestrictiveDomainPreservesTheOriginalResultAndOrdering()
    {
        var input = Input();
        var result = Result();
        Assert.Same(result, PublicSearchDomainProjection.Apply(input, result));
        Assert.Same(result, PublicSearchDomainProjection.Apply(
            input with { PublicSearchDomainRegionIds = ["inside", "outside"] }, result));
    }

    [Theory]
    [InlineData("candidate")]
    [InlineData("preferred")]
    [InlineData("outer")]
    [InlineData("conflicted")]
    [InlineData("confirmed")]
    [InlineData("excluded")]
    [InlineData("provenance")]
    public void ProjectionRejectsUnknownMapRegionsInsteadOfHidingInvalidResults(string field)
    {
        var input = Input() with { PublicSearchDomainRegionIds = ["inside"] };
        var original = Result();
        var invalid = field switch
        {
            "candidate" => original with { CandidateRegionIds = ["missing"] },
            "preferred" => original with { PreferredCandidateRegionIds = ["missing"] },
            "outer" => original with { OuterPlausibleCandidateRegionIds = ["missing"] },
            "conflicted" => original with { ConflictedCandidateRegionIds = ["missing"] },
            "confirmed" => original with { ConfirmedPersonRegionIds = ["missing"] },
            "excluded" => original with { ExplicitlyExcludedRegionIds = ["missing"] },
            _ => original with
            {
                CandidateRegionProvenance = [original.CandidateRegionProvenance[0] with { RegionId = "missing" }]
            }
        };

        Assert.Throws<InvalidOperationException>(() => PublicSearchDomainProjection.Apply(input, invalid));
        Assert.Equal(["inside"], PublicSearchDomainProjection.Apply(input, original).CandidateRegionIds);
    }

    [Fact]
    public void ExplicitEmptyDomainIsNotAnImplicitFullMapFallback()
    {
        var input = Input() with { PublicSearchDomainRegionIds = [] };
        var result = new VoluntaryReachabilityStrategy().Evaluate(input);
        Assert.Empty(result.CandidateRegionIds);
        Assert.Empty(result.OuterPlausibleCandidateRegionIds);
        Assert.Empty(result.ConfirmedPersonRegionIds);
        Assert.Empty(result.CandidateRegionProvenance);
        Assert.Empty(result.ExplicitlyExcludedRegionIds);
        Assert.Contains(PublicSearchDomainProjection.ScopeRationale, result.RationaleCodes);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    [InlineData("blank")]
    public void StrategiesRejectInvalidPublicDomainIdentifiers(string defect)
    {
        var input = Input() with
        {
            PublicSearchDomainRegionIds = defect switch
            {
                "unknown" => ["missing"],
                "duplicate" => ["inside", "inside"],
                _ => [""]
            }
        };
        foreach (var strategy in SearchStrategyRegistry.All)
        {
            Assert.Throws<ArgumentException>(() => strategy.Evaluate(input));
        }
    }

    [Fact]
    public void OutsideObservationAnchorStillDerivesInsideCandidatesUsingTheFullPublicMap()
    {
        var input = Input() with { PublicSearchDomainRegionIds = ["inside"] };
        var before = JsonDefaults.Serialize(input);
        var unconstrained = new VoluntaryReachabilityStrategy().Evaluate(input with { PublicSearchDomainRegionIds = null });
        var scoped = new VoluntaryReachabilityStrategy().Evaluate(input);
        Assert.Equal(["inside", "outside"], unconstrained.CandidateRegionIds);
        Assert.Equal(["inside"], scoped.CandidateRegionIds);
        Assert.Equal("outside", Assert.Single(input.AvailableEvidence.Observations).ReportedRegionId);
        Assert.Equal("inside", Assert.Single(input.PublicContext.PublicRegions.Single(region => region.Id == "outside").Connections).ToRegionId);
        Assert.Equal(before, JsonDefaults.Serialize(input));
        Assert.Empty(scoped.ExplicitlyExcludedRegionIds);
    }

    [Theory]
    [InlineData("own-evidence-constrained")]
    [InlineData("cross-thread-constrained")]
    [InlineData("single-weft")]
    [InlineData("multi-weft-tracing")]
    public void WeftAndTracingFinalStandingSetsAreScopedWithoutChangingTheirPublicModel(string strategyId)
    {
        var input = Input();
        var context = input.PublicContext with
        {
            PublicCandidateWarp = new PublicCandidateWarpDefinition
            {
                HistoricalTimeWindow = new InteractionTimeWindow(0, 0),
                AvailableAtTick = 0,
                HistoricalCandidateRegionIds = ["inside", "outside"]
            }
        };
        if (strategyId is "single-weft" or "multi-weft-tracing")
        {
            context = context with
            {
                PublicCausalTracingModel = new PublicCausalTracingModel
                {
                    CandidateStates = [new PublicCandidateWarpStateDefinition
                    {
                        TimeWindow = new InteractionTimeWindow(0, 0),
                        TemporalKind = CandidateWarpTemporalKind.EventPoint,
                        RegionIds = ["inside", "outside"],
                        EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
                        Provenance = [CandidateWarpProvenance.Create(CandidateWarpSourceKind.PublicCausalModel,
                            "public-model", 0, false, false, EvidenceState.SupportedButUncertain, ["inside", "outside"])]
                    }]
                }
            };
        }

        input = input with { PublicContext = context, PublicSearchDomainRegionIds = ["inside"] };
        var strategy = Assert.Single(SearchStrategyRegistry.Resolve([strategyId]));
        var unscoped = strategy.Evaluate(input with { PublicSearchDomainRegionIds = null });
        var scoped = strategy.Evaluate(input);
        Assert.Contains("outside", unscoped.OuterPlausibleCandidateRegionIds);
        Assert.Equal(["inside"], scoped.CandidateRegionIds);
        Assert.Equal(["inside"], scoped.PreferredCandidateRegionIds);
        Assert.Equal(["inside"], scoped.OuterPlausibleCandidateRegionIds);
        Assert.All(scoped.CandidateRegionProvenance, item => Assert.Equal("inside", item.RegionId));
        Assert.Equal(JsonDefaults.Serialize(unscoped.CausalTracingResult), JsonDefaults.Serialize(scoped.CausalTracingResult));
        Assert.Equal(JsonDefaults.Serialize(unscoped.CrossThreadConstraintAudits), JsonDefaults.Serialize(scoped.CrossThreadConstraintAudits));
        Assert.Empty(scoped.ExplicitlyExcludedRegionIds);
    }

    [Fact]
    public void S4AllStrategiesUseDeclaredDomainAndKeepTheThreeTracingStrategiesUnchanged()
    {
        var scenario = ScenarioLoader.Load(Path.Combine(RepositoryRoot, "scenarios", "s4_multi_weft_tracing", "scenario.json"));
        var originalScenario = JsonDefaults.Serialize(scenario);
        var strategies = SearchStrategyRegistry.All.Select(strategy => new InspectingStrategy(strategy, scenario)).ToArray();
        var result = new BatchEvaluationRunner().Run(new BatchEvaluationRequest
        {
            Scenarios = [new EvaluationScenarioInput { Scenario = scenario, ContentSha256 = "s4-domain-fixture" }],
            Seeds = Enumerable.Range(0, 10).ToArray(),
            Strategies = strategies,
            SourceCommitSha = "domain-test",
            DirtyWorkingTree = false
        });

        Assert.Equal(80, result.Runs.Count);
        Assert.Equal(11, scenario.EvaluationSearchableRegionIds.Count);
        Assert.All(result.Runs, run =>
        {
            Assert.Equal(11, run.SearchableRegionCount);
            Assert.Equal(11, run.SearchSpaceFraction.Denominator);
            Assert.All(run.CandidateRegionIds, region => Assert.Contains(region, scenario.EvaluationSearchableRegionIds));
        });
        Assert.All(strategies, strategy => Assert.True(strategy.Calls > 10));
        Assert.Equal(originalScenario, JsonDefaults.Serialize(scenario));
    }

    private sealed class InspectingStrategy(ISearchStrategy inner, ScenarioDefinition scenario) : ISearchStrategy
    {
        public string Id => inner.Id;
        public int Calls { get; private set; }

        public SearchStrategyResult Evaluate(SearchStrategyInput input)
        {
            Calls++;
            Assert.Equal(scenario.EvaluationSearchableRegionIds.Order(StringComparer.Ordinal), input.PublicSearchDomainRegionIds);
            Assert.Equal(JsonDefaults.Serialize(scenario.Regions), JsonDefaults.Serialize(input.PublicContext.PublicRegions));
            Assert.Contains(input.PublicContext.PublicRegions, region => region.Id == "person-b-home");
            Assert.Contains(input.PublicContext.PublicRegions, region => region.Id == "person-c-home");
            var result = inner.Evaluate(input);
            Assert.All(result.CandidateRegionIds, region => Assert.Contains(region, scenario.EvaluationSearchableRegionIds));
            if (Id is SearchStrategyIds.OwnEvidenceConstrained or SearchStrategyIds.SingleWeft or SearchStrategyIds.MultiWeftTracing)
            {
                var legacy = inner.Evaluate(input with { PublicSearchDomainRegionIds = null });
                Assert.Equal(JsonDefaults.Serialize(legacy), JsonDefaults.Serialize(result));
            }

            return result;
        }
    }

    private static SearchStrategyInput Input() => new()
    {
        ScenarioId = "domain-fixture",
        Seed = 7,
        CurrentTick = 0,
        PublicContext = new RescueContext
        {
            ScenarioId = "domain-fixture",
            TargetPersonId = "person",
            NormalWalkingRadius = 2,
            PublicRegions =
            [
                new RegionDefinition { Id = "outside", Type = RegionType.Building, Center = new Position(0, 0, 0, "outside"),
                    Connections = [new RegionConnection { ToRegionId = "inside" }] },
                new RegionDefinition { Id = "inside", Type = RegionType.Building, Center = new Position(1, 0, 0, "inside") }
            ]
        },
        AvailableEvidence = new ObservationSet
        {
            ScenarioId = "domain-fixture",
            Seed = 7,
            Observations = [new ObservationRecord
            {
                ObservationId = "public-person-observation",
                ObservedTick = 0,
                AvailableTick = 0,
                EvidenceType = "person-location",
                SubjectIds = ["person"],
                ReportedRegionId = "outside",
                ReportedPosition = new Position(0, 0, 0, "outside"),
                State = EvidenceState.Confirmed
            }]
        }
    };

    private static SearchStrategyResult Result() => new()
    {
        StrategyId = "domain-fixture",
        ScenarioId = "domain-fixture",
        Seed = 7,
        ProducedAtTick = 0,
        EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
        CandidateRegionIds = ["outside", "inside"],
        PreferredCandidateRegionIds = ["outside", "inside"],
        OuterPlausibleCandidateRegionIds = ["outside", "inside"],
        ConflictedCandidateRegionIds = ["outside", "inside"],
        ConfirmedPersonRegionIds = ["outside"],
        ExplicitlyExcludedRegionIds = ["outside"],
        HasCandidateStandingSets = true,
        RationaleCodes = ["fixture"],
        CandidateRegionProvenance = new[] { "outside", "inside" }.Select(region => new CandidateRegionProvenance
        {
            RegionId = region,
            SourceKind = CandidateSourceKind.PublicCausalModel,
            SourceIdentifier = "public-model",
            AvailableAtTick = 0,
            IsPersonEvidence = false,
            IsDeviceOnlyEvidence = false,
            EvidenceBoundaryState = EvidenceState.SupportedButUncertain
        }).ToArray()
    };

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FootprintSim.sln")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
        }
    }
}
