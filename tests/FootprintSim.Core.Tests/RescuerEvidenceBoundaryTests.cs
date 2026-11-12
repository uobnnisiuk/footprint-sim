using FootprintSim.Evaluation;
using FootprintSim.Evidence;
using FootprintSim.Hazards;
using FootprintSim.Inference;
using FootprintSim.Inference.SearchProjection;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;
using FootprintSim.Interactions.Tracing;
using FootprintSim.Model;
using FootprintSim.Serialization;
using Xunit;

namespace FootprintSim.Core.Tests;

public sealed class RescuerEvidenceBoundaryTests
{
    private const string ScenarioId = "rescuer-receipt-fixture";
    private const int Seed = 7;
    private const string HolderId = "held-device";
    private const string RescuerId = "rescuer-a";
    private const string TargetId = "person-a";
    private const string RecordId = "original-encounter-record";


    // #112: questions about a separately published branch are NOT evidence
    // that any Person took that route or that safe search units decreased.
    private static (RescuerEvidenceInput U, RescuerEvidenceInput K)
        RelationUtilityInputs(bool reversed = false, bool duplicateReceipt = false,
            string laterAncestry = "weft-b", bool deviceOnly = false,
            EvidenceState firstState = EvidenceState.Confirmed,
            bool receiveSecond = true, IReadOnlyList<string>? firstRegions = null,
            long laterTick = 5)
    {
        var uWorld = ComplementaryPublicContext();
        var kWorld = uWorld with
        {
            PublicHazardEnvelope = uWorld.PublicHazardEnvelope! with
            {
                Branches = [.. uWorld.PublicHazardEnvelope!.Branches,
                    Branch("B", "D") with
                    {
                        MinimumTravelTicks = 3,
                        MaximumTravelTicks = 3,
                        PublishedAtTick = 7,
                        ProvenanceId = "separate-public-BD"
                    }]
            }
        };
        var early = ComplementaryOriginal(RecordId, HolderId, 2,
            firstRegions ?? ["A", "B", "C"], 3, "weft-a", firstState, deviceOnly);
        var later = ComplementaryOriginal("second-original", "holder-b", laterTick,
            ["D"], laterTick + 1, laterAncestry);
        var originals = reversed ? new[] { later, early } : new[] { early, later };
        var receipts = new List<InteractionRecordReceipt> { Receipt(6) };
        if (receiveSecond)
            receipts.Add(LaterComparisonReceipt(7));
        if (duplicateReceipt)
            receipts.Add(Receipt(6));
        if (reversed)
            receipts.Reverse();
        var builder = new RescuerEvidenceInputBuilder();
        return (
            builder.BuildDistinctOriginals(uWorld, Seed, 7, RescuerId, originals, receipts),
            builder.BuildDistinctOriginals(kWorld, Seed, 7, RescuerId, originals, receipts));
    }

    private static PublicSearchUnitMapping[] RelationUtilityMappings() =>
    [
        new("A", 2, ["U1"], true, "catalog-a", 0),
        new("B", 2, ["U2"], true, "catalog-b", 0),
        new("C", 2, ["U1"], true, "catalog-c", 0)
    ];

    private static RescuerRelationQuestionUtilityReport RelationUtility(
        RescuerEvidenceInput u, RescuerEvidenceInput k,
        IReadOnlyList<PublicSearchUnitMapping>? mappings = null) =>
        RescuerEvidenceMethodComparison.AnalyzeRelationQuestionUtility(
            u, k, InteractionTimeWindow.At(2), ["A", "B", "C"],
            ["U1", "U2"], mappings ?? RelationUtilityMappings());


    // #114: a formal witness is per candidate, not an endorsement of a
    // whole public unit or a new hard exclusion. All four methods keep their
    // actual outer while the separately published B->D declaration adds one
    // sourced explanation in U2.
    private static RescuerDeclaredWitnessUnitReport DeclaredUnitCoverage(
        RescuerEvidenceInput u, RescuerEvidenceInput k,
        IReadOnlyList<PublicSearchUnitMapping>? mappings = null,
        IReadOnlyList<string>? unitIds = null) =>
        RescuerEvidenceMethodComparison.AnalyzeDeclaredWitnessUnitCoverage(
            u, k, InteractionTimeWindow.At(2), ["A", "B", "C"],
            unitIds ?? ["U1", "U2"], mappings ?? RelationUtilityMappings());


    // #116: Actual BuildDistinctOriginals receipts are the only input source;
    // no manually fabricated F20 PastStateCandidate objects.
    private static RescuerEvidenceInput PastToCurrentInput(
        long cutoff, bool firstReceived, bool secondReceived,
        bool reversed = false, bool duplicate = false,
        string laterAncestry = "weft-b", bool deviceOnly = false,
        EvidenceState firstState = EvidenceState.Confirmed,
        IReadOnlyList<string>? firstRegions = null)
    {
        var first = ComplementaryOriginal(RecordId, HolderId, 2,
            firstRegions ?? ["A", "B", "C"], 3, "weft-a", firstState, deviceOnly);
        var second = ComplementaryOriginal("second-original", "holder-b", 5,
            ["D"], 6, laterAncestry);
        var originals = reversed ? new[] { second, first } : new[] { first, second };
        var ledger = new List<InteractionRecordReceipt>();
        if (firstReceived)
            ledger.Add(Receipt(6));
        if (secondReceived)
            ledger.Add(LaterComparisonReceipt(7));
        if (duplicate && firstReceived)
            ledger.Add(Receipt(6));
        if (reversed)
            ledger.Reverse();
        return new RescuerEvidenceInputBuilder().BuildDistinctOriginals(
            ComplementaryPublicContext(), Seed, cutoff, RescuerId,
            originals, ledger);
    }

    private static PublicRegionProjection[] CurrentDeclaredTransitions() =>
    [
        new("A", 2, 2, 7, ["D", "E"], true, "published-A-current", 0),
        new("B", 2, 2, 7, ["E"], true, "published-B-current", 0),
        new("C", 2, 2, 7, ["F"], false, "published-C-partial", 0)
    ];

    private static PublicSearchUnitMapping[] CurrentDeclaredUnits() =>
    [
        new("D", 7, ["U1"], true, "published-D-units", 0),
        new("E", 7, ["U2"], true, "published-E-units", 0),
        new("F", 7, ["U1", "U3"], false, "published-F-partial", 0)
    ];

    private static RescuerReceivedPastProjectionReport ProjectReceivedCurrent(
        RescuerEvidenceInput received,
        IReadOnlyList<PublicRegionProjection>? transitions = null,
        IReadOnlyList<PublicSearchUnitMapping>? mappings = null) =>
        RescuerEvidenceMethodComparison.ProjectReceivedPastCandidates(
            received, InteractionTimeWindow.At(2), 7,
            ["A", "B", "C", "D", "E", "F"], ["U1", "U2", "U3"],
            transitions ?? CurrentDeclaredTransitions(),
            mappings ?? CurrentDeclaredUnits(),
            [new PublicActionFootprint("inspect-U2", 7, ["U2"], "published-action", 0)]);

    // #124: four real methods and one published current catalog. Neither
    // direct Person history nor synthetic truth is available to inference.
    private static RescuerCurrentUnitMethodComparison CompareCurrentMethods(
        RescuerEvidenceInput received,
        IReadOnlyList<PublicRegionProjection>? transitions = null,
        IReadOnlyList<PublicSearchUnitMapping>? mappings = null) =>
        RescuerEvidenceMethodComparison.CompareReceivedCurrentUnits(
            received, InteractionTimeWindow.At(2), 7,
            ["A", "B", "C", "D", "E", "F"], ["U1", "U2", "U3"],
            transitions ?? CurrentDeclaredTransitions(),
            mappings ?? CurrentDeclaredUnits(),
            [new PublicActionFootprint("inspect-U2", 7, ["U2"], "published-action", 0)]);

    // #128 / AR-23: a second received Person original can directly observe
    // the queried tick without being used to narrow the historical outer.
    private static RescuerEvidenceInput TargetTickOriginalInput(
        long observationTick, IReadOnlyList<string> regions,
        long cutoff = 7, bool deviceOnly = false,
        bool unrelatedTarget = false, bool secondReceived = true,
        bool reversed = false, bool duplicateReceipt = false,
        EvidenceState secondState = EvidenceState.Confirmed,
        string secondAncestry = "weft-b",
        InteractionTimeWindow? observationWindow = null,
        IReadOnlyList<string>? firstRegions = null,
        bool firstReceived = true,
        EvidenceState firstState = EvidenceState.Confirmed,
        IReadOnlyList<string>? publicHistoricalRegions = null)
    {
        var first = ComplementaryOriginal(
            RecordId, HolderId, 2, firstRegions ?? ["A", "B"], 3, "weft-a",
            firstState);
        var second = ComplementaryOriginal(
            "second-original", "holder-b", observationTick, regions,
            observationTick <= 5 ? 6 : 7, secondAncestry, secondState,
            deviceOnly, unrelatedTarget, observationWindow);
        var originals = reversed ? new[] { second, first } : new[] { first, second };
        var receipts = new List<InteractionRecordReceipt>();
        if (firstReceived)
            receipts.Add(Receipt(6));
        if (secondReceived)
            receipts.Add(LaterComparisonReceipt(7));
        if (duplicateReceipt && firstReceived)
            receipts.Add(Receipt(6));
        if (reversed)
            receipts.Reverse();
        var context = ComplementaryPublicContext();
        if (publicHistoricalRegions is not null)
        {
            context = context with
            {
                PublicCausalTracingModel = new PublicCausalTracingModel
                {
                    CandidateStates =
                    [
                        State(2, publicHistoricalRegions),
                        State(5, ["D", "E", "F"])
                    ]
                }
            };
        }
        return new RescuerEvidenceInputBuilder().BuildDistinctOriginals(
            context, Seed, cutoff, RescuerId, originals, receipts);
    }

    // #136: every snapshot uses the SAME two original objects, public world,
    // seed and declaration catalog. Only the second receipt tick changes.
    private static (RescuerProvisionalUnitTimingReport Report,
        RescuerEvidenceInput LateAt8) ProvisionalReceiptTiming(
        string secondAncestry = "weft-b",
        EvidenceState firstState = EvidenceState.Confirmed,
        EvidenceState secondState = EvidenceState.Confirmed,
        bool deviceOnly = false,
        bool unrelatedTarget = false,
        bool reversed = false,
        bool duplicateReceipt = false,
        IReadOnlyList<string>? secondRegions = null,
        IReadOnlyList<PublicRegionProjection>? transitions = null,
        IReadOnlyList<PublicSearchUnitMapping>? mappings = null)
    {
        var world = ComplementaryPublicContext();
        var first = ComplementaryOriginal(RecordId, HolderId, 2,
            ["B"], 3, "weft-a", firstState);
        var second = ComplementaryOriginal("second-original", "holder-b", 7,
            secondRegions ?? ["D", "E"], 7, secondAncestry, secondState,
            deviceOnly, unrelatedTarget);
        var originals = reversed ? new[] { second, first } : new[] { first, second };
        var earlyLedger = new List<InteractionRecordReceipt>
        {
            Receipt(6), LaterComparisonReceipt(7)
        };
        var lateLedger = new List<InteractionRecordReceipt>
        {
            Receipt(6), LaterComparisonReceipt(8)
        };
        if (duplicateReceipt)
        {
            earlyLedger.Add(Receipt(6));
            lateLedger.Add(LaterComparisonReceipt(8));
        }
        if (reversed)
        {
            earlyLedger.Reverse();
            lateLedger.Reverse();
        }
        var originalBytes = JsonDefaults.Serialize(originals);
        var publicBytes = JsonDefaults.Serialize(world);
        var builder = new RescuerEvidenceInputBuilder();
        var early7 = builder.BuildDistinctOriginals(
            world, Seed, 7, RescuerId, originals, earlyLedger);
        var early8 = builder.BuildDistinctOriginals(
            world, Seed, 8, RescuerId, originals, earlyLedger);
        var late7 = builder.BuildDistinctOriginals(
            world, Seed, 7, RescuerId, originals, lateLedger);
        var late8 = builder.BuildDistinctOriginals(
            world, Seed, 8, RescuerId, originals, lateLedger);
        Assert.Equal([RecordId, "second-original"],
            early7.ReceivedRecords.Select(record => record.OriginalRecordId));
        Assert.Equal([RecordId, "second-original"],
            early8.ReceivedRecords.Select(record => record.OriginalRecordId));
        Assert.Equal([RecordId],
            late7.ReceivedRecords.Select(record => record.OriginalRecordId));
        Assert.Equal([RecordId, "second-original"],
            late8.ReceivedRecords.Select(record => record.OriginalRecordId));
        Assert.Equal([6L, 7L],
            early7.ReceivedRecords.Select(record => record.FirstReceivedAtTick));
        Assert.Equal([6L, 8L],
            late8.ReceivedRecords.Select(record => record.FirstReceivedAtTick));

        var report =
            RescuerEvidenceMethodComparison.AnalyzeRetrospectiveProvisionalUnitTiming(
                world, Seed, RescuerId, originals, earlyLedger, lateLedger,
                InteractionTimeWindow.At(2), 7,
                ["A", "B", "C", "D", "E", "F"], ["U1", "U2", "U3"],
                transitions ?? CurrentDeclaredTransitions(),
                mappings ?? CurrentDeclaredUnits(),
                [new PublicActionFootprint("inspect-U2", 7,
                    ["U2"], "published-action", 0)]);
        Assert.Equal(originalBytes, JsonDefaults.Serialize(originals));
        Assert.Equal(publicBytes, JsonDefaults.Serialize(world));
        return (report, late8);
    }

    [Fact]
    public void ProvisionalReceiptTimingUsesRealForwardAndSeparateRetrospectivePaths()
    {
        var (report, late8) = ProvisionalReceiptTiming();
        Assert.Equal(7, report.TargetTick);
        Assert.Equal(2, report.HistoricalWindow.StartTick);
        Assert.Equal(["early-second-receipt", "late-second-receipt"],
            report.Series.Select(series => series.ReceiptCondition));
        var early = report.Series[0];
        var late = report.Series[1];
        foreach (var series in report.Series)
        {
            Assert.Equal([7L, 8L], series.Timeline.Select(tick => tick.QueryTick));
            Assert.Equal(["forward-f20", "retrospective-evaluation"],
                series.Timeline.Select(tick => tick.ComparisonKind));
            Assert.NotNull(series.Timeline[0].ForwardF20Projection);
            Assert.Null(series.Timeline[1].ForwardF20Projection);
            Assert.All(series.Timeline, tick =>
                Assert.Equal(4, tick.Methods.Count));
            Assert.Equal(["public-prior", "known-relation-backward",
                "distributed-reconstruction", "distributed-reconstruction-local"],
                series.Timeline[0].Methods.Select(row => row.HistoricalMethod.Method));
        }
        Assert.Equal([RecordId, "second-original"],
            early.Timeline[0].ReceivedOriginals.Select(record => record.OriginalRecordId));
        Assert.Equal([RecordId],
            late.Timeline[0].ReceivedOriginals.Select(record => record.OriginalRecordId));
        Assert.Equal([RecordId, "second-original"],
            late.Timeline[1].ReceivedOriginals.Select(record => record.OriginalRecordId));
        // AR-29: receiving the target sighting at tick 8 does not delay
        // the actual tick-7 F20 unit set in this fixed synthetic world.
        Assert.Equal(["U2"], early.Timeline[0].ForwardF20Projection!.KnownUnitIds);
        Assert.Equal(["U2"], late.Timeline[0].ForwardF20Projection!.KnownUnitIds);
        Assert.Equal(early.Timeline[0].ForwardF20Projection!.KnownUnitIds,
            late.Timeline[0].ForwardF20Projection!.KnownUnitIds);
        Assert.Empty(late.Timeline[0].ForwardF20Projection!.UnresolvedCandidateIds);
        RescuerProvisionalOverlapAssessment Assessment(
            RescuerProvisionalUnitTimingTick tick) =>
            tick.Methods.Single(row =>
                row.HistoricalMethod.Method == "distributed-reconstruction")
                .ProvisionalOverlapAssessment!;
        Assert.Equal("eligible-unit-reduction", Assessment(early.Timeline[0]).Status);
        Assert.Equal("withheld", Assessment(late.Timeline[0]).Status);
        Assert.Null(Assessment(late.Timeline[0]).UnitReductionFromDirect);
        foreach (var series in report.Series)
        {
            var retrospective = Assessment(series.Timeline[1]);
            Assert.Equal("eligible-unit-reduction", retrospective.Status);
            Assert.Equal(["E"], retrospective.OverlapRegionIds);
            Assert.Equal(["U2"], retrospective.OverlapUnitIds);
            Assert.Equal(1, retrospective.RegionReductionFromDirect);
            Assert.Equal(1, retrospective.UnitReductionFromDirect);
        }
        Assert.Equal(7, early.FirstObservedEligibleTicks.Single(first =>
            first.Method == "distributed-reconstruction").FirstObservedEligibleTick);
        Assert.Equal(8, late.FirstObservedEligibleTicks.Single(first =>
            first.Method == "distributed-reconstruction").FirstObservedEligibleTick);

        // Old F20 time protection remains: cutoff 8 cannot project target 7.
        Assert.Throws<InvalidDataException>(() => CompareCurrentMethods(late8));
        var trueForward = CompareCurrentMethods(TargetTickOriginalInput(
            7, ["D", "E"], firstRegions: ["B"]));
        Assert.Equal(JsonDefaults.Serialize(trueForward.Methods),
            JsonDefaults.Serialize(CompareCurrentMethods(TargetTickOriginalInput(
                7, ["D", "E"], firstRegions: ["B"])).Methods));
        Assert.Equal(["U2"], trueForward.ActualReconstructionF20Projection.KnownUnitIds);
    }

    [Fact]
    public void ProvisionalReceiptTimingKeepsAncestryAndUncertaintyGates()
    {
        var shared = ProvisionalReceiptTiming(secondAncestry: "weft-a").Report;
        var sharedRow = shared.Series[1].Timeline[1].Methods.Single(row =>
            row.HistoricalMethod.Method == "distributed-reconstruction");
        Assert.Equal("withheld", sharedRow.ProvisionalOverlapAssessment!.Status);
        Assert.Equal("shared-history-target-ancestry",
            sharedRow.ProvisionalOverlapAssessment.ReasonCode);
        Assert.Null(sharedRow.ProvisionalOverlapAssessment.UnitReductionFromDirect);

        foreach (var firstState in new[] {
            EvidenceState.SupportedButUncertain, EvidenceState.Conflicting })
        {
            var report = ProvisionalReceiptTiming(firstState: firstState).Report;
            var assessment = report.Series[1].Timeline[1].Methods.Single(row =>
                row.HistoricalMethod.Method == "distributed-reconstruction")
                .ProvisionalOverlapAssessment!;
            Assert.Contains(assessment.Status, new[] { "withheld", "conflict" });
            Assert.Null(assessment.UnitReductionFromDirect);
        }
        foreach (var report in new[] {
            ProvisionalReceiptTiming(secondState: EvidenceState.SupportedButUncertain).Report,
            ProvisionalReceiptTiming(deviceOnly: true).Report,
            ProvisionalReceiptTiming(unrelatedTarget: true).Report })
        {
            var assessment = report.Series[1].Timeline[1].Methods.Single(row =>
                row.HistoricalMethod.Method == "distributed-reconstruction")
                .ProvisionalOverlapAssessment!;
            Assert.Equal("withheld", assessment.Status);
            Assert.Null(assessment.UnitReductionFromDirect);
        }
        var conflict = ProvisionalReceiptTiming(secondRegions: ["D"]).Report;
        Assert.Equal("conflict", conflict.Series[1].Timeline[1].Methods.Single(row =>
            row.HistoricalMethod.Method == "distributed-reconstruction")
            .ProvisionalOverlapAssessment!.Status);
    }

    [Fact]
    public void ProvisionalReceiptTimingSeparatesUnitZeroFromUnknownAndFutureCatalog()
    {
        var manyToOne = ProvisionalReceiptTiming(mappings:
            CurrentDeclaredUnits().Select(mapping => mapping.RegionId == "D"
                ? mapping with { UnitIds = ["U2"] } : mapping).ToArray()).Report;
        var onlyRegion = manyToOne.Series[1].Timeline[1].Methods.Single(row =>
            row.HistoricalMethod.Method == "distributed-reconstruction")
            .ProvisionalOverlapAssessment!;
        Assert.Equal("region-only-change", onlyRegion.Status);
        Assert.Equal(1, onlyRegion.RegionReductionFromDirect);
        Assert.Equal(0, onlyRegion.UnitReductionFromDirect);
        var incomplete = ProvisionalReceiptTiming(transitions:
            CurrentDeclaredTransitions().Select(transition =>
                transition.HistoricalRegionId == "B"
                    ? transition with { IsComplete = false } : transition).ToArray()).Report;
        var missing = incomplete.Series[1].Timeline[1].Methods.Single(row =>
            row.HistoricalMethod.Method == "distributed-reconstruction")
            .ProvisionalOverlapAssessment!;
        Assert.Equal("withheld", missing.Status);
        Assert.Null(missing.UnitReductionFromDirect);

        Assert.Throws<InvalidDataException>(() => ProvisionalReceiptTiming(
            transitions: CurrentDeclaredTransitions().Select(transition =>
                transition with { AvailableTick = 8 }).ToArray()));
        Assert.Throws<InvalidDataException>(() => ProvisionalReceiptTiming(
            mappings: CurrentDeclaredUnits().Select(mapping =>
                mapping with { AvailableTick = 8 }).ToArray()));
        Assert.Equal(JsonDefaults.Serialize(ProvisionalReceiptTiming().Report),
            JsonDefaults.Serialize(ProvisionalReceiptTiming(
                reversed: true, duplicateReceipt: true).Report));
    }

    [Fact]
    public void ProvisionalReceiptTimingRejectsReceiptAndPublicDomainDrift()
    {
        var world = ComplementaryPublicContext();
        DeviceHeldInteractionRecord[] originals =
        [
            ComplementaryOriginal(RecordId, HolderId, 2, ["B"], 3, "weft-a"),
            ComplementaryOriginal("second-original", "holder-b", 7,
                ["D", "E"], 7, "weft-b")
        ];
        RescuerProvisionalUnitTimingReport Run(
            IReadOnlyList<InteractionRecordReceipt> early,
            IReadOnlyList<InteractionRecordReceipt> late) =>
            RescuerEvidenceMethodComparison.AnalyzeRetrospectiveProvisionalUnitTiming(
                world, Seed, RescuerId, originals, early, late,
                InteractionTimeWindow.At(2), 7,
                ["A", "B", "C", "D", "E", "F"], ["U1", "U2", "U3"],
                CurrentDeclaredTransitions(), CurrentDeclaredUnits(),
                [new PublicActionFootprint("inspect-U2", 7, ["U2"],
                    "published-action", 0)]);
        Assert.Throws<ArgumentException>(() => Run(
            [Receipt(6), LaterComparisonReceipt(8)],
            [Receipt(6), LaterComparisonReceipt(8)]));
        Assert.Throws<ArgumentException>(() => Run(
            [Receipt(6), LaterComparisonReceipt(7)],
            [Receipt(6), LaterComparisonReceipt(7)]));
        Assert.Throws<ArgumentException>(() => Run(
            [Receipt(6), LaterComparisonReceipt(7)],
            [Receipt(5), LaterComparisonReceipt(8)]));
        Assert.Throws<ArgumentException>(() => Run(
            [Receipt(6), LaterComparisonReceipt(7)],
            [Receipt(6), LaterComparisonReceipt(8),
                new InteractionRecordReceipt(ScenarioId, Seed, "third-original",
                    HolderId, RescuerId, 8)]));
        Assert.Throws<ArgumentException>(() => Run(
            [Receipt(6), LaterComparisonReceipt(7)],
            [Receipt(6), LaterComparisonReceipt(8), LaterComparisonReceipt(9)]));
    }

    [Fact]
    public void TargetTickSourceAncestryGroupingIsTransitiveAndDeterministic()
    {
        // The receipt bridge currently accepts exactly two originals; this
        // isolates the three-observation ancestry rule without inventing
        // an unsupported third original or widening the receipt API.
        var groupMethod = typeof(RescuerEvidenceMethodComparison).GetMethod(
            "GroupTransitiveSourceAncestry",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Static);
        Assert.NotNull(groupMethod);
        IReadOnlyList<IReadOnlyList<string>> ancestry =
        [
            new[] { "chain-a" },
            new[] { "chain-a", "chain-b" },
            new[] { "chain-b" },
            new[] { "unrelated" }
        ];
        var grouped = Assert.IsType<int[][]>(groupMethod.Invoke(null, [ancestry]));
        Assert.Equal(2, grouped.Length);
        Assert.Equal([0, 1, 2], grouped[0]);
        Assert.Equal([3], grouped[1]);

        // A source group with a compatible alternative cannot yield an
        // independent definitive conflict from its other observations.
        var allowed = new[] { new[] { "F" }, new[] { "F" },
            new[] { "D" }, new[] { "F" } };
        var published = new HashSet<string>(["D", "E"], StringComparer.Ordinal);
        Assert.False(grouped[0].All(index =>
            allowed[index].All(region => !published.Contains(region))));
        Assert.True(grouped[1].All(index =>
            allowed[index].All(region => !published.Contains(region))));

        var reversed = ancestry.Reverse().ToArray();
        var reversedGroups = Assert.IsType<int[][]>(
            groupMethod.Invoke(null, [reversed]));
        Assert.Equal([0], reversedGroups[0]);
        Assert.Equal([1, 2, 3], reversedGroups[1]);
    }

    [Fact]
    public void TargetTickPersonConflictIsSourcedAndDoesNotRewriteMethodOutersOrF20()
    {
        var received = TargetTickOriginalInput(7, ["F"]);
        var before = JsonDefaults.Serialize(received);
        var result = CompareCurrentMethods(received);
        Assert.Equal(4, result.Methods.Count);
        foreach (var name in new[] {
            "distributed-reconstruction", "distributed-reconstruction-local" })
        {
            var row = result.Methods.Single(m => m.HistoricalMethod.Method == name);
            Assert.Equal(["A", "B"], row.HistoricalMethod.OuterCandidateRegionIds);
            Assert.Equal(["D", "E"], row.KnownCurrentRegionIds);
            Assert.Equal(["U1", "U2"], row.KnownUnitIds);
            Assert.Empty(row.UnresolvedHistoricalRegionIds!);
            Assert.Equal("conflict", row.ProjectionStatus);
            var conflict = Assert.Single(row.TargetTickConflicts);
            Assert.Equal(
                "confirmed-target-observation-outside-complete-declaration",
                conflict.ReasonCode);
            Assert.Equal("second-original", conflict.DirectSourceId);
            Assert.NotEmpty(conflict.ConstraintId);
            Assert.NotEmpty(conflict.SourceObservationId);
            Assert.Contains("weft-b", conflict.SourceAncestryIds);
            Assert.Equal(7, conflict.ObservationWindow.StartTick);
            Assert.Equal(7, conflict.ObservationWindow.EndTick);
            Assert.Equal(["F"], conflict.AllowedRegionIds);
        }
        foreach (var name in new[] { "public-prior", "known-relation-backward" })
        {
            var row = result.Methods.Single(m => m.HistoricalMethod.Method == name);
            Assert.Equal("partial", row.ProjectionStatus);
            Assert.Equal(["D", "E", "F"], row.KnownCurrentRegionIds);
            Assert.Empty(row.TargetTickConflicts);
        }
        var m1e = result.Methods.Single(m =>
            m.HistoricalMethod.Method == "distributed-reconstruction");
        Assert.Equal(m1e.KnownUnitIds,
            result.ActualReconstructionF20Projection.KnownUnitIds);
        Assert.Equal(m1e.KnownCurrentRegionIds,
            result.ActualReconstructionF20Projection.KnownCurrentRegionIds);
        Assert.Empty(result.ActualReconstructionF20Projection.UnresolvedCandidateIds);
        Assert.Equal(before, JsonDefaults.Serialize(received));
        Assert.Equal(JsonDefaults.Serialize(result),
            JsonDefaults.Serialize(CompareCurrentMethods(TargetTickOriginalInput(
                7, ["F"], reversed: true, duplicateReceipt: true))));
    }

    // AR-24: a time-uncertain occurrence in [5,7] or [6,7] might
    // have occurred before tick 7, even when the region is F.
    [Fact]
    public void TargetTickUncertainIntervalDoesNotProveDefiniteConflict()
    {
        foreach (var window in new[]
        {
            new InteractionTimeWindow(5, 7),
            new InteractionTimeWindow(6, 7)
        })
        {
            var input = TargetTickOriginalInput(
                7, ["F"], observationWindow: window);
            var result = CompareCurrentMethods(input);
            foreach (var methodName in new[]
            {
                "distributed-reconstruction",
                "distributed-reconstruction-local"
            })
            {
                var row = result.Methods.Single(m => m.HistoricalMethod.Method == methodName);
                Assert.Equal("available", row.ProjectionStatus);
                Assert.Empty(row.TargetTickConflicts);
                Assert.Equal(["D", "E"], row.KnownCurrentRegionIds);
                Assert.Equal(["U1", "U2"], row.KnownUnitIds);
            }
        }

        // Exact F@7 still conflicts; a less precise observation cannot
        // establish a stronger finding than the precise F@5 alternative.
        var exact = CompareCurrentMethods(TargetTickOriginalInput(7, ["F"]));
        Assert.Equal("conflict", exact.Methods.Single(m =>
            m.HistoricalMethod.Method == "distributed-reconstruction").ProjectionStatus);
    }

    [Fact]
    public void TargetTickConflictRequiresCompletePublicAndConfirmedReceivedPersonEvidence()
    {
        var matching = CompareCurrentMethods(TargetTickOriginalInput(7, ["D"]));
        var overlap = CompareCurrentMethods(TargetTickOriginalInput(7, ["D", "F"]));
        var differentTick = CompareCurrentMethods(TargetTickOriginalInput(5, ["E"]));
        foreach (var report in new[] { matching, overlap, differentTick })
        {
            Assert.All(report.Methods, m => Assert.Empty(m.TargetTickConflicts));
            Assert.Equal("available", report.Methods.Single(m =>
                m.HistoricalMethod.Method == "distributed-reconstruction")
                .ProjectionStatus);
            Assert.Equal(["U1", "U2"], report.Methods.Single(m =>
                m.HistoricalMethod.Method == "distributed-reconstruction")
                .KnownUnitIds); // Matching D does not shrink units to U1.
        }
        foreach (var input in new[]
        {
            TargetTickOriginalInput(7, ["F"], deviceOnly: true),
            TargetTickOriginalInput(7, ["F"], unrelatedTarget: true),
            TargetTickOriginalInput(7, ["F"], secondReceived: false),
            TargetTickOriginalInput(7, ["F"], cutoff: 6),
            TargetTickOriginalInput(7, ["F"],
                secondState: EvidenceState.SupportedButUncertain)
        })
        {
            Assert.All(CompareCurrentMethods(input).Methods,
                m => Assert.Empty(m.TargetTickConflicts));
        }

        var partial = CompareCurrentMethods(TargetTickOriginalInput(7, ["F"]),
            transitions: CurrentDeclaredTransitions().Select(row =>
                row.HistoricalRegionId == "A"
                    ? row with { IsComplete = false } : row).ToArray());
        var unmapped = CompareCurrentMethods(TargetTickOriginalInput(7, ["F"]),
            transitions: CurrentDeclaredTransitions().Where(row =>
                row.HistoricalRegionId != "A").ToArray());
        foreach (var report in new[] { partial, unmapped })
        {
            var m1e = report.Methods.Single(m =>
                m.HistoricalMethod.Method == "distributed-reconstruction");
            Assert.Equal("partial", m1e.ProjectionStatus);
            Assert.Empty(m1e.TargetTickConflicts);
        }
        var sameAncestry = CompareCurrentMethods(TargetTickOriginalInput(
            7, ["F"], secondAncestry: "weft-a"));
        Assert.Single(sameAncestry.Methods.Single(m =>
            m.HistoricalMethod.Method == "distributed-reconstruction")
            .TargetTickConflicts);
    }

    // #130: precommitted comparison uses the same received originals and
    // public world. B is NOT a true F20 candidate or a safe hard exclusion.
    [Fact]
    public void ConditionalTargetUnitsNarrowOnlyTheSeparateCompleteMethodProposal()
    {
        var received = TargetTickOriginalInput(7, ["D"]);
        var original = JsonDefaults.Serialize(received);
        var report = CompareCurrentMethods(received);
        foreach (var name in new[]
        {
            "distributed-reconstruction", "distributed-reconstruction-local"
        })
        {
            var row = report.Methods.Single(m => m.HistoricalMethod.Method == name);
            var conditional = Assert.IsType<RescuerConditionalCurrentUnitProposal>(
                row.ConditionalTargetUnitProposal);
            Assert.Equal("available", row.ProjectionStatus);
            Assert.Equal(["A", "B"], row.HistoricalMethod.OuterCandidateRegionIds);
            Assert.Equal(["D", "E"], row.KnownCurrentRegionIds);
            Assert.Equal(["U1", "U2"], row.KnownUnitIds);
            Assert.Equal("available", conditional.Status);
            Assert.Equal(["D"], conditional.RegionIds);
            Assert.Equal(["U1"], conditional.UnitIds);
            Assert.Equal(1, conditional.UnitCountReduction);
            Assert.Contains("second-original", conditional.ObservationDirectSourceIds);
            Assert.NotEmpty(conditional.ObservationConstraintIds);
            Assert.NotEmpty(conditional.ObservationSourceAncestryIds);
            Assert.Contains("published-A-current", conditional.PublicTransitionSourceIds);
            Assert.Equal(["published-D-units"], conditional.PublicUnitMappingSourceIds);
            Assert.Equal(1, conditional.InspectedTargetObservationCount);
            Assert.Equal(1, conditional.InspectedAncestryGroupCount);
            Assert.Empty(row.TargetTickConflicts);
        }
        foreach (var name in new[] { "public-prior", "known-relation-backward" })
        {
            var row = report.Methods.Single(m => m.HistoricalMethod.Method == name);
            Assert.Equal("partial", row.ProjectionStatus);
            Assert.Equal("partial", row.ConditionalTargetUnitProposal!.Status);
            Assert.Null(row.ConditionalTargetUnitProposal.UnitIds);
        }
        Assert.Equal(["U1", "U2"], report.ActualReconstructionF20Projection.KnownUnitIds);
        Assert.Equal(original, JsonDefaults.Serialize(received));
    }

    [Fact]
    public void ConditionalTargetUnitsRetainZeroBenefitAndWithholdAmbiguousOrConflictingInputs()
    {
        var noGain = CompareCurrentMethods(TargetTickOriginalInput(7, ["D", "E"]));
        var equal = noGain.Methods.Single(m =>
            m.HistoricalMethod.Method == "distributed-reconstruction");
        Assert.Equal("available", equal.ConditionalTargetUnitProposal!.Status);
        Assert.Equal(["U1", "U2"], equal.ConditionalTargetUnitProposal.UnitIds);
        Assert.Equal(0, equal.ConditionalTargetUnitProposal.UnitCountReduction);

        var conflicted = CompareCurrentMethods(TargetTickOriginalInput(7, ["F"]));
        var conflict = conflicted.Methods.Single(m =>
            m.HistoricalMethod.Method == "distributed-reconstruction");
        Assert.Equal("conflict", conflict.ProjectionStatus);
        Assert.Single(conflict.TargetTickConflicts);
        Assert.Equal("conflict", conflict.ConditionalTargetUnitProposal!.Status);
        Assert.Null(conflict.ConditionalTargetUnitProposal.UnitIds);
        Assert.Equal(["U1", "U2"], conflict.KnownUnitIds);

        var mixed = CompareCurrentMethods(TargetTickOriginalInput(7, ["D", "F"]));
        var mixedRow = mixed.Methods.Single(m =>
            m.HistoricalMethod.Method == "distributed-reconstruction");
        Assert.Empty(mixedRow.TargetTickConflicts);
        Assert.Equal("partial", mixedRow.ConditionalTargetUnitProposal!.Status);
        Assert.Null(mixedRow.ConditionalTargetUnitProposal.UnitIds);

        var partialUnit = CompareCurrentMethods(TargetTickOriginalInput(7, ["D"]),
            mappings: CurrentDeclaredUnits().Select(row => row.RegionId == "D"
                ? row with { IsComplete = false } : row).ToArray());
        Assert.Equal("partial", partialUnit.Methods.Single(m =>
            m.HistoricalMethod.Method == "distributed-reconstruction")
            .ConditionalTargetUnitProposal!.Status);

        var incompleteTransition = CompareCurrentMethods(
            TargetTickOriginalInput(7, ["D"]),
            transitions: CurrentDeclaredTransitions().Select(row =>
                row.HistoricalRegionId == "A" ? row with { IsComplete = false } : row)
                .ToArray());
        Assert.Equal("partial", incompleteTransition.Methods.Single(m =>
            m.HistoricalMethod.Method == "distributed-reconstruction")
            .ConditionalTargetUnitProposal!.Status);
    }

    [Fact]
    public void ConditionalTargetUnitsDoNotPromoteUnreceivedOrNonexactOrUncertainPersonSources()
    {
        var ineligible = new[]
        {
            TargetTickOriginalInput(5, ["D"]),
            TargetTickOriginalInput(7, ["F"],
                observationWindow: new InteractionTimeWindow(5, 7)),
            TargetTickOriginalInput(7, ["F"],
                observationWindow: new InteractionTimeWindow(6, 7)),
            TargetTickOriginalInput(7, ["D"], deviceOnly: true),
            TargetTickOriginalInput(7, ["D"], unrelatedTarget: true),
            TargetTickOriginalInput(7, ["D"], secondReceived: false),
            TargetTickOriginalInput(7, ["D"], cutoff: 6),
            TargetTickOriginalInput(7, ["D"],
                secondState: EvidenceState.SupportedButUncertain),
            TargetTickOriginalInput(7, ["D"],
                secondState: EvidenceState.Conflicting)
        };
        foreach (var input in ineligible)
        {
            var report = CompareCurrentMethods(input);
            var row = report.Methods.Single(m =>
                m.HistoricalMethod.Method == "distributed-reconstruction");
            Assert.Equal("unavailable", row.ConditionalTargetUnitProposal!.Status);
            Assert.Null(row.ConditionalTargetUnitProposal.UnitIds);
            Assert.Empty(row.TargetTickConflicts);
        }

        var forward = CompareCurrentMethods(TargetTickOriginalInput(7, ["D"],
            duplicateReceipt: true));
        var reverse = CompareCurrentMethods(TargetTickOriginalInput(7, ["D"],
            reversed: true, duplicateReceipt: true));
        Assert.Equal(JsonDefaults.Serialize(forward), JsonDefaults.Serialize(reverse));
        var shared = CompareCurrentMethods(TargetTickOriginalInput(7, ["D"],
            secondAncestry: "weft-a"));
        var sharedMethod = shared.Methods.Single(m =>
            m.HistoricalMethod.Method == "distributed-reconstruction");
        Assert.Equal("available", sharedMethod.ConditionalTargetUnitProposal!.Status);
        Assert.Equal(1, sharedMethod.ConditionalTargetUnitProposal.InspectedAncestryGroupCount);
        Assert.Equal(["U1", "U2"], shared.ActualReconstructionF20Projection.KnownUnitIds);
    }


    // #132 / AR-26: when B is available it only gates the regions from
    // direct A. Equality is a structural invariant, not a measured
    // negative finding about the historical-weft hypothesis.
    [Fact]
    public void DirectTargetBaselineIsIndependentAndEqualToAvailableConditionalGate()
    {
        foreach (var sighting in new[] { new[] { "D" }, new[] { "D", "E" } })
        {
            var input = TargetTickOriginalInput(7, sighting);
            var original = JsonDefaults.Serialize(input);
            var result = CompareCurrentMethods(input);
            var direct = Assert.IsType<RescuerDirectTargetUnitBaseline>(
                result.DirectTargetUnitBaseline);
            Assert.Equal("available", direct.Status);
            Assert.Equal(sighting.Select(region => region == "D" ? "U1" : "U2"),
                direct.UnitIds);
            Assert.Contains("second-original", direct.ObservationDirectSourceIds);
            Assert.NotEmpty(direct.ObservationConstraintIds);
            Assert.NotEmpty(direct.ObservationSourceObservationIds);
            Assert.NotEmpty(direct.ObservationSourceAncestryIds);
            foreach (var name in new[] {
                "distributed-reconstruction", "distributed-reconstruction-local" })
            {
                var method = result.Methods.Single(row =>
                    row.HistoricalMethod.Method == name);
                var conditional = method.ConditionalTargetUnitProposal!;
                Assert.Equal("available", conditional.Status);
                Assert.Equal(direct.UnitIds, conditional.UnitIds);
                var audit = Assert.IsType<RescuerDirectWeftUnitAudit>(
                    method.DirectVsWeftAudit);
                Assert.Equal("both-available-identical-by-design", audit.Status);
                Assert.Equal(0, audit.ConditionalUnitDeltaFromDirect);
                Assert.Null(audit.HypotheticalIntersectionUnitIds);
                Assert.Equal(["U1", "U2"], method.KnownUnitIds);
            }
            Assert.Equal(original, JsonDefaults.Serialize(input));
            Assert.Equal(JsonDefaults.Serialize(result),
                JsonDefaults.Serialize(CompareCurrentMethods(
                    TargetTickOriginalInput(7, sighting,
                        reversed: true, duplicateReceipt: true))));
        }
        // Evidence-quantity ablation, NOT a same-evidence method comparison:
        // remove only the historical @2 receipt, retaining the same D@7.
        var bothReceipts = CompareCurrentMethods(TargetTickOriginalInput(7, ["D"]));
        var onlyCurrent = CompareCurrentMethods(TargetTickOriginalInput(
            7, ["D"], firstReceived: false));
        Assert.Equal([RecordId, "second-original"],
            bothReceipts.ReceivedOriginals.Select(r => r.OriginalRecordId));
        Assert.Equal(["second-original"],
            onlyCurrent.ReceivedOriginals.Select(r => r.OriginalRecordId));
        Assert.Equal([7L],
            onlyCurrent.ReceivedOriginals.Select(r => r.FirstReceivedAtTick));
        Assert.Equal("available", onlyCurrent.DirectTargetUnitBaseline!.Status);
        Assert.Equal(["U1"], onlyCurrent.DirectTargetUnitBaseline.UnitIds);
        Assert.Equal(JsonDefaults.Serialize(bothReceipts.DirectTargetUnitBaseline),
            JsonDefaults.Serialize(onlyCurrent.DirectTargetUnitBaseline));
    }

    // R2: a complete B->E historical outer could narrow an ambiguous
    // latest sighting, but current B explicitly WITHHOLDS that proposal.
    // Hypothetical U2 is an evaluation-only counterfactual, never F20.
    [Fact]
    public void DirectVsWeftAuditPreservesR2WithoutPromotingHypotheticalExclusion()
    {
        var report = CompareCurrentMethods(TargetTickOriginalInput(
            7, ["D", "E"], firstRegions: ["B"]));
        var direct = report.DirectTargetUnitBaseline!;
        Assert.Equal("available", direct.Status);
        Assert.Equal(["D", "E"], direct.RegionIds);
        Assert.Equal(["U1", "U2"], direct.UnitIds);
        var method = report.Methods.Single(row =>
            row.HistoricalMethod.Method == "distributed-reconstruction");
        Assert.Equal("available", method.ProjectionStatus);
        Assert.Equal(["E"], method.KnownCurrentRegionIds);
        Assert.Equal(["U2"], method.KnownUnitIds);
        Assert.Equal("partial", method.ConditionalTargetUnitProposal!.Status);
        Assert.Equal("observation-has-alternatives-outside-declared-outer",
            method.ConditionalTargetUnitProposal.ReasonCode);
        Assert.Null(method.ConditionalTargetUnitProposal.UnitIds);
        var audit = method.DirectVsWeftAudit!;
        Assert.Equal("direct-available-conditional-withheld", audit.Status);
        Assert.Null(audit.ConditionalUnitDeltaFromDirect);
        Assert.Equal(["E"], audit.HypotheticalIntersectionRegionIds);
        Assert.Equal(["U2"], audit.HypotheticalIntersectionUnitIds);
        Assert.Equal(1, audit.HypotheticalUnitReductionFromDirect);
        Assert.Equal(["U2"], report.ActualReconstructionF20Projection.KnownUnitIds);
        Assert.Equal(["B"], method.HistoricalMethod.OuterCandidateRegionIds);
    }

    // #134 / AR-28: sharing an upstream source between historical generation
    // and direct target sighting must not be counted as independent reduction.
    [Fact]
    public void ProvisionalOverlapDistinguishesIndependentAndSharedAncestry()
    {
        var independentInput = TargetTickOriginalInput(
            7, ["D", "E"], firstRegions: ["B"]);
        var original = JsonDefaults.Serialize(independentInput);
        var independent = CompareCurrentMethods(independentInput);
        Assert.Equal(4, independent.Methods.Count);
        foreach (var name in new[] { "distributed-reconstruction",
            "distributed-reconstruction-local" })
        {
            var row = independent.Methods.Single(m => m.HistoricalMethod.Method == name);
            var assessment = Assert.IsType<RescuerProvisionalOverlapAssessment>(
                row.ProvisionalOverlapAssessment);
            Assert.Equal("eligible-unit-reduction", assessment.Status);
            Assert.Equal(["E"], assessment.OverlapRegionIds);
            Assert.Equal(["U2"], assessment.OverlapUnitIds);
            Assert.Equal(2, assessment.DirectRegionCount);
            Assert.Equal(1, assessment.OverlapRegionCount);
            Assert.Equal(1, assessment.RegionReductionFromDirect);
            Assert.Equal(2, assessment.DirectUnitCount);
            Assert.Equal(1, assessment.OverlapUnitCount);
            Assert.Equal(1, assessment.UnitReductionFromDirect);
            Assert.Contains(RecordId, assessment.HistoricalGenerationDirectSourceIds);
            Assert.Contains("second-original", assessment.ObservationDirectSourceIds);
            Assert.Empty(assessment.SharedSourceAncestryIds);
            Assert.Null(row.ConditionalTargetUnitProposal!.UnitIds);
            Assert.Equal(["U2"], row.DirectVsWeftAudit!.HypotheticalIntersectionUnitIds);
        }
        Assert.Equal(original, JsonDefaults.Serialize(independentInput));

        var shared = CompareCurrentMethods(TargetTickOriginalInput(
            7, ["D", "E"], firstRegions: ["B"], secondAncestry: "weft-a"));
        foreach (var name in new[] { "distributed-reconstruction",
            "distributed-reconstruction-local" })
        {
            var row = shared.Methods.Single(m => m.HistoricalMethod.Method == name);
            var assessment = row.ProvisionalOverlapAssessment!;
            Assert.Equal("withheld", assessment.Status);
            Assert.Equal("shared-history-target-ancestry", assessment.ReasonCode);
            Assert.Equal(["weft-a"], assessment.SharedSourceAncestryIds);
            Assert.Null(assessment.OverlapRegionIds);
            Assert.Null(assessment.OverlapUnitIds);
            Assert.Null(assessment.UnitReductionFromDirect);
            // The old unguarded #132 audit is unchanged, but not counted as
            // an independent new contribution in the #134 assessment.
            Assert.Equal(1, row.DirectVsWeftAudit!.HypotheticalUnitReductionFromDirect);
        }
        Assert.Equal(1, shared.Methods.Single(m =>
            m.HistoricalMethod.Method == "distributed-reconstruction")
            .HistoricalMethod.AvailableSourceGroupCount);
        Assert.Equal(JsonDefaults.Serialize(independent),
            JsonDefaults.Serialize(CompareCurrentMethods(TargetTickOriginalInput(
                7, ["D", "E"], firstRegions: ["B"],
                reversed: true, duplicateReceipt: true))));
        Assert.Equal(JsonDefaults.Serialize(shared),
            JsonDefaults.Serialize(CompareCurrentMethods(TargetTickOriginalInput(
                7, ["D", "E"], firstRegions: ["B"], secondAncestry: "weft-a",
                reversed: true, duplicateReceipt: true))));
        Assert.Equal(["U2"], shared.ActualReconstructionF20Projection.KnownUnitIds);
    }

    // Ready-state Codex P2: a complete projection cannot make an uncertain
    // or conflicting historical original into a confirmed independent source.
    // The public prior is intentionally B-only here, so the B->E projection
    // stays complete even when historical evidence is not confirmed.
    [Fact]
    public void ProvisionalOverlapDoesNotCountUncertainHistoricalGeneration()
    {
        RescuerCurrentUnitMethodComparison Run(EvidenceState firstState) =>
            CompareCurrentMethods(TargetTickOriginalInput(
                7, ["D", "E"], firstRegions: ["B"], firstState: firstState,
                publicHistoricalRegions: ["B"]));

        var confirmed = Run(EvidenceState.Confirmed);
        foreach (var name in new[] {
            "distributed-reconstruction", "distributed-reconstruction-local" })
        {
            var row = confirmed.Methods.Single(m =>
                m.HistoricalMethod.Method == name);
            Assert.Equal("available", row.ProjectionStatus);
            Assert.Equal("eligible-unit-reduction",
                row.ProvisionalOverlapAssessment!.Status);
            Assert.Equal(1,
                row.ProvisionalOverlapAssessment.UnitReductionFromDirect);
        }

        foreach (var state in new[] {
            EvidenceState.SupportedButUncertain, EvidenceState.Conflicting })
        {
            var actual = Run(state);
            Assert.Equal("available", actual.DirectTargetUnitBaseline!.Status);
            foreach (var name in new[] {
                "distributed-reconstruction", "distributed-reconstruction-local" })
            {
                var row = actual.Methods.Single(m =>
                    m.HistoricalMethod.Method == name);
                var assessment = row.ProvisionalOverlapAssessment!;
                // Even a fully mapped public B->E is not proof that the
                // conflicted or uncertain original constrains the past.
                Assert.Equal("available", row.ProjectionStatus);
                Assert.Contains(assessment.Status, new[] { "withheld", "conflict" });
                Assert.StartsWith("historical-", assessment.ReasonCode);
                Assert.Null(assessment.OverlapUnitIds);
                Assert.Null(assessment.UnitReductionFromDirect);
            }
            Assert.Equal(JsonDefaults.Serialize(actual),
                JsonDefaults.Serialize(CompareCurrentMethods(
                    TargetTickOriginalInput(7, ["D", "E"],
                        firstRegions: ["B"], firstState: state,
                        publicHistoricalRegions: ["B"],
                        reversed: true, duplicateReceipt: true))));
        }
    }

    [Fact]
    public void ProvisionalOverlapSeparatesRegionFromUnitReductionAndWithholdsUnknown()
    {
        var baseInput = TargetTickOriginalInput(7, ["D", "E"], firstRegions: ["B"]);
        var ordinary = CompareCurrentMethods(baseInput);
        var manyToOne = CompareCurrentMethods(baseInput,
            mappings: CurrentDeclaredUnits().Select(m => m.RegionId == "D"
                ? m with { UnitIds = ["U2"] } : m).ToArray());
        var one = manyToOne.Methods.Single(m =>
            m.HistoricalMethod.Method == "distributed-reconstruction");
        Assert.Equal(["U2"], manyToOne.DirectTargetUnitBaseline!.UnitIds);
        Assert.Equal("region-only-change", one.ProvisionalOverlapAssessment!.Status);
        Assert.Equal(["E"], one.ProvisionalOverlapAssessment.OverlapRegionIds);
        Assert.Equal(["U2"], one.ProvisionalOverlapAssessment.OverlapUnitIds);
        Assert.Equal(1, one.ProvisionalOverlapAssessment.RegionReductionFromDirect);
        Assert.Equal(0, one.ProvisionalOverlapAssessment.UnitReductionFromDirect);
        Assert.Equal(["U2"], manyToOne.ActualReconstructionF20Projection.KnownUnitIds);
        Assert.Equal("eligible-unit-reduction", ordinary.Methods.Single(m =>
            m.HistoricalMethod.Method == "distributed-reconstruction")
            .ProvisionalOverlapAssessment!.Status);

        var partialTransition = CompareCurrentMethods(baseInput,
            transitions: CurrentDeclaredTransitions().Select(m =>
                m.HistoricalRegionId == "B" ? m with { IsComplete = false } : m).ToArray());
        var p = partialTransition.Methods.Single(m =>
            m.HistoricalMethod.Method == "distributed-reconstruction")
            .ProvisionalOverlapAssessment!;
        Assert.Equal("withheld", p.Status);
        Assert.Equal("transition-incomplete", p.ReasonCode);
        Assert.Null(p.UnitReductionFromDirect);
        var unmapped = CompareCurrentMethods(baseInput,
            transitions: CurrentDeclaredTransitions().Where(m =>
                m.HistoricalRegionId != "B").ToArray());
        var u = unmapped.Methods.Single(m =>
            m.HistoricalMethod.Method == "distributed-reconstruction")
            .ProvisionalOverlapAssessment!;
        Assert.Equal("withheld", u.Status);
        Assert.Equal("transition-unmapped", u.ReasonCode);

        var partialUnit = CompareCurrentMethods(baseInput,
            mappings: CurrentDeclaredUnits().Select(m => m.RegionId == "E"
                ? m with { IsComplete = false } : m).ToArray());
        Assert.Equal("withheld", partialUnit.Methods.Single(m =>
            m.HistoricalMethod.Method == "distributed-reconstruction")
            .ProvisionalOverlapAssessment!.Status);
        var missingUnit = CompareCurrentMethods(baseInput,
            mappings: CurrentDeclaredUnits().Where(m => m.RegionId != "E").ToArray());
        Assert.Equal("withheld", missingUnit.Methods.Single(m =>
            m.HistoricalMethod.Method == "distributed-reconstruction")
            .ProvisionalOverlapAssessment!.Status);

        foreach (var input in new[] {
            TargetTickOriginalInput(7, ["D", "E"], firstRegions: ["B"],
                secondReceived: false),
            TargetTickOriginalInput(7, ["D", "E"], firstRegions: ["B"],
                secondState: EvidenceState.SupportedButUncertain),
            TargetTickOriginalInput(7, ["D", "E"], firstRegions: ["B"],
                observationWindow: new InteractionTimeWindow(6, 7)),
            TargetTickOriginalInput(7, ["D", "E"], firstRegions: ["B"],
                cutoff: 6) })
        {
            var result = CompareCurrentMethods(input);
            var assessment = result.Methods.Single(m =>
                m.HistoricalMethod.Method == "distributed-reconstruction")
                .ProvisionalOverlapAssessment!;
            Assert.Equal("withheld", assessment.Status);
            Assert.Null(assessment.OverlapUnitIds);
            Assert.Null(assessment.UnitReductionFromDirect);
        }

        var contradictory = CompareCurrentMethods(TargetTickOriginalInput(
            7, ["D"], firstRegions: ["B"]));
        Assert.Equal("conflict", contradictory.Methods.Single(m =>
            m.HistoricalMethod.Method == "distributed-reconstruction")
            .ProvisionalOverlapAssessment!.Status);

        // Future declarations are rejected by the existing F20 validator.
        // Removing them at the cutoff instead yields withheld/unknown.
        Assert.Throws<InvalidDataException>(() => CompareCurrentMethods(baseInput,
            CurrentDeclaredTransitions().Select(m => m with
            { AvailableTick = 8 }).ToArray()));
        Assert.Throws<InvalidDataException>(() => CompareCurrentMethods(baseInput,
            mappings: CurrentDeclaredUnits().Select(m => m with
            { AvailableTick = 8 }).ToArray()));
        Assert.Equal(4, ordinary.Methods.Count);
        Assert.Equal(["U2"], ordinary.ActualReconstructionF20Projection.KnownUnitIds);
        Assert.All(ordinary.Methods, m => Assert.NotNull(m.ProvisionalOverlapAssessment));
    }

    [Fact]
    public void DirectUnavailableDoesNotHideHistoricalOuterOrBecomeZeroReduction()
    {
        foreach (var input in new[] {
            TargetTickOriginalInput(7, ["D"], secondReceived: false),
            TargetTickOriginalInput(7, ["D"],
                observationWindow: new InteractionTimeWindow(5, 7)),
            TargetTickOriginalInput(7, ["D"], deviceOnly: true),
            TargetTickOriginalInput(7, ["D"], unrelatedTarget: true),
            TargetTickOriginalInput(7, ["D"], cutoff: 6),
            TargetTickOriginalInput(7, ["D"],
                secondState: EvidenceState.SupportedButUncertain) })
        {
            var report = CompareCurrentMethods(input);
            Assert.NotEqual("available", report.DirectTargetUnitBaseline!.Status);
            Assert.Null(report.DirectTargetUnitBaseline.UnitIds);
            var method = report.Methods.Single(row =>
                row.HistoricalMethod.Method == "distributed-reconstruction");
            if (method.ProjectionStatus == "available")
            {
                Assert.Equal(["U1", "U2"], method.KnownUnitIds);
                var audit = Assert.IsType<RescuerDirectWeftUnitAudit>(
                    method.DirectVsWeftAudit);
                if (report.DirectTargetUnitBaseline.ReasonCode ==
                    "no-received-exact-target-person-observation")
                {
                    Assert.Equal("historical-outer-only", audit.Status);
                    Assert.Equal(
                        "no-direct-exact-observation-but-published-historical-outer-remains",
                        audit.ReasonCode);
                }
                else
                {
                    Assert.Equal(
                        $"direct-{report.DirectTargetUnitBaseline.Status}-historical-outer-available",
                        audit.Status);
                    Assert.Equal(report.DirectTargetUnitBaseline.ReasonCode,
                        audit.ReasonCode);
                }
                Assert.Null(audit.ConditionalUnitDeltaFromDirect);
            }
            Assert.Null(method.DirectVsWeftAudit!.HypotheticalIntersectionUnitIds);
        }
        var partial = CompareCurrentMethods(TargetTickOriginalInput(7, ["F"]));
        Assert.Equal("partial", partial.DirectTargetUnitBaseline!.Status);
        Assert.Null(partial.DirectTargetUnitBaseline.UnitIds);
        Assert.Null(partial.Methods.Single(row =>
            row.HistoricalMethod.Method == "distributed-reconstruction")
            .DirectVsWeftAudit!.ConditionalUnitDeltaFromDirect);
        var mixed = CompareCurrentMethods(TargetTickOriginalInput(7, ["D", "F"]));
        Assert.Equal("partial", mixed.DirectTargetUnitBaseline!.Status);
        Assert.Null(mixed.DirectTargetUnitBaseline.UnitIds);
    }

    // AR-27: an exact sighting is present but incomplete/uncertain. Its
    // historical outer is not evidence that no exact sighting was received.
    [Fact]
    public void ExactUnresolvedSightingIsNotMisclassifiedAsMissing()
    {
        foreach (var (input, expectedStatus, expectedReason, expectedOuter) in new[]
        {
            (TargetTickOriginalInput(7, ["D", "F"]), "partial",
                "direct-unit-mapping-not-complete", new[] { "U1", "U2" }),
            (TargetTickOriginalInput(7, ["E", "F"], firstRegions: ["B"]), "partial",
                "direct-unit-mapping-not-complete", new[] { "U2" }),
            (TargetTickOriginalInput(7, ["D"],
                secondState: EvidenceState.SupportedButUncertain), "unavailable",
                "target-observation-not-fully-confirmed-or-sourced", new[] { "U1", "U2" })
        })
        {
            var result = CompareCurrentMethods(input);
            var direct = Assert.IsType<RescuerDirectTargetUnitBaseline>(
                result.DirectTargetUnitBaseline);
            Assert.Equal(expectedStatus, direct.Status);
            Assert.Equal(expectedReason, direct.ReasonCode);
            Assert.Contains("second-original", direct.ObservationDirectSourceIds);
            var method = result.Methods.Single(row =>
                row.HistoricalMethod.Method == "distributed-reconstruction");
            Assert.Equal("available", method.ProjectionStatus);
            Assert.Equal(expectedOuter, method.KnownUnitIds);
            Assert.Equal(expectedOuter, result.ActualReconstructionF20Projection.KnownUnitIds);
            var audit = Assert.IsType<RescuerDirectWeftUnitAudit>(
                method.DirectVsWeftAudit);
            Assert.Equal($"direct-{expectedStatus}-historical-outer-available", audit.Status);
            Assert.Equal(expectedReason, audit.ReasonCode);
            Assert.Null(audit.ConditionalUnitDeltaFromDirect);
            Assert.Null(audit.HypotheticalIntersectionUnitIds);
        }
    }

    [Fact]
    public void FourReceivedMethodsShareActualEvidenceAndOneCurrentPublicCatalog()
    {
        var received = PastToCurrentInput(7, true, true, firstRegions: ["A", "B"]);
        var original = JsonDefaults.Serialize(received);
        var compared = CompareCurrentMethods(received);
        var historical = RescuerEvidenceMethodComparison.Compare(
            received, InteractionTimeWindow.At(2));
        Assert.Equal(7, compared.QueryTick);
        Assert.Equal(7, compared.TargetTick);
        Assert.Equal(2, compared.HistoricalWindow.EndTick);
        Assert.Equal([RecordId, "second-original"],
            compared.ReceivedOriginals.Select(row => row.OriginalRecordId));
        Assert.Equal([6L, 7L],
            compared.ReceivedOriginals.Select(row => row.FirstReceivedAtTick));
        Assert.Equal(4, compared.Methods.Count);
        // Frozen same-world/current-tick measurements, not four aliases for
        // one algorithm. Known relation alone cannot use the early weft.
        var expectedOuters = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["public-prior"] = ["A", "B", "C"],
            ["known-relation-backward"] = ["A", "B", "C"],
            ["distributed-reconstruction"] = ["A", "B"],
            ["distributed-reconstruction-local"] = ["A", "B"]
        };
        foreach (var method in compared.Methods)
        {
            Assert.Equal(expectedOuters[method.HistoricalMethod.Method],
                method.HistoricalMethod.OuterCandidateRegionIds);
            Assert.Equal(expectedOuters[method.HistoricalMethod.Method].Length == 3
                    ? new[] { "U1", "U2", "U3" } : ["U1", "U2"],
                method.KnownUnitIds);
            Assert.Equal(expectedOuters[method.HistoricalMethod.Method].Length == 3
                    ? "partial" : "available", method.ProjectionStatus);
        }
        Assert.Equal(historical.Methods.Select(row => row.Method),
            compared.Methods.Select(row => row.HistoricalMethod.Method));
        foreach (var method in compared.Methods)
        {
            var source = historical.Methods.Single(row =>
                row.Method == method.HistoricalMethod.Method);
            Assert.Equal(JsonDefaults.Serialize(source),
                JsonDefaults.Serialize(method.HistoricalMethod));
            Assert.Equal(source.OuterCandidateRegionIds,
                method.HistoricalRegions.Select(row => row.HistoricalRegionId));
            Assert.True(source.Steps >= 0);
            if (method.ProjectionStatus != "unavailable")
            {
                Assert.NotNull(method.KnownUnitIds);
                Assert.Equal(method.KnownUnitIds!.Distinct(StringComparer.Ordinal).Count(),
                    method.KnownUnitIds.Count);
            }
        }

        var prior = compared.Methods.Single(row =>
            row.HistoricalMethod.Method == "public-prior");
        Assert.Equal(["A", "B", "C"], prior.HistoricalMethod.OuterCandidateRegionIds);
        Assert.Equal(["D", "E", "F"], prior.KnownCurrentRegionIds);
        Assert.Equal(["U1", "U2", "U3"], prior.KnownUnitIds);
        Assert.Equal("partial", prior.ProjectionStatus);
        Assert.Equal(["C"], prior.UnresolvedHistoricalRegionIds);
        var m1e = compared.Methods.Single(row =>
            row.HistoricalMethod.Method == "distributed-reconstruction");
        Assert.Equal(["A", "B"], m1e.HistoricalMethod.OuterCandidateRegionIds);
        Assert.Equal(["D", "E"], m1e.KnownCurrentRegionIds);
        Assert.Equal(["U1", "U2"], m1e.KnownUnitIds);
        Assert.Empty(m1e.UnresolvedHistoricalRegionIds!);
        Assert.Equal("available", m1e.ProjectionStatus);
        Assert.Equal(m1e.KnownUnitIds, compared.ActualReconstructionF20Projection.KnownUnitIds);
        Assert.Equal(m1e.KnownCurrentRegionIds,
            compared.ActualReconstructionF20Projection.KnownCurrentRegionIds);
        Assert.Empty(compared.ActualReconstructionF20Projection.UnresolvedCandidateIds);
        // Synthetic truth A is checked only AFTER inference returned;
        // this fixed scenario must not incorrectly exclude the truth.
        Assert.All(compared.Methods, row =>
            Assert.Contains("A", row.HistoricalMethod.OuterCandidateRegionIds));
        Assert.Equal(original, JsonDefaults.Serialize(received));
    }

    [Fact]
    public void CurrentMethodComparisonSeparatesReceivedEvidenceQuantityFromMethodChoice()
    {
        var none = CompareCurrentMethods(PastToCurrentInput(
            7, false, false, firstRegions: ["A", "B"]));
        var first = CompareCurrentMethods(PastToCurrentInput(
            7, true, false, firstRegions: ["A", "B"]));
        var both = CompareCurrentMethods(PastToCurrentInput(
            7, true, true, firstRegions: ["A", "B"]));
        Assert.Equal([0, 1, 2],
            new[] { none.ReceivedOriginals.Count, first.ReceivedOriginals.Count,
                both.ReceivedOriginals.Count });
        var noM1e = none.Methods.Single(row =>
            row.HistoricalMethod.Method == "distributed-reconstruction");
        var yesM1e = both.Methods.Single(row =>
            row.HistoricalMethod.Method == "distributed-reconstruction");
        Assert.Equal(["A", "B", "C"], noM1e.HistoricalMethod.OuterCandidateRegionIds);
        Assert.Equal(["U1", "U2", "U3"], noM1e.KnownUnitIds);
        Assert.Equal(["A", "B"], yesM1e.HistoricalMethod.OuterCandidateRegionIds);
        Assert.Equal(["U1", "U2"], yesM1e.KnownUnitIds);
        Assert.Equal([RecordId], first.ReceivedOriginals.Select(r => r.OriginalRecordId));
        Assert.Equal(["A", "B"], first.Methods.Single(row =>
            row.HistoricalMethod.Method == "distributed-reconstruction")
                .HistoricalMethod.OuterCandidateRegionIds);
        Assert.Equal(["A", "B", "C"], both.Methods.Single(row =>
            row.HistoricalMethod.Method == "public-prior")
                .HistoricalMethod.OuterCandidateRegionIds);

        var duplicateAndReordered = CompareCurrentMethods(PastToCurrentInput(
            7, true, true, reversed: true, duplicate: true, firstRegions: ["A", "B"]));
        Assert.Equal(JsonDefaults.Serialize(both), JsonDefaults.Serialize(duplicateAndReordered));
        var early = CompareCurrentMethods(PastToCurrentInput(
            6, true, true, firstRegions: ["A", "B"]));
        Assert.Equal(6, early.QueryTick);
        Assert.Single(early.ReceivedOriginals);
        Assert.DoesNotContain("second-original", JsonDefaults.Serialize(early));
        var deviceOnly = CompareCurrentMethods(PastToCurrentInput(
            7, true, true, deviceOnly: true, firstRegions: ["A", "B"]));
        Assert.Equal(0, deviceOnly.Methods.Single(row =>
            row.HistoricalMethod.Method == "distributed-reconstruction")
                .HistoricalMethod.UsedSourceGroupCount);
        var shared = CompareCurrentMethods(PastToCurrentInput(
            7, true, true, laterAncestry: "weft-a", firstRegions: ["A", "B"]));
        Assert.Equal(1, shared.Methods.Single(row =>
            row.HistoricalMethod.Method == "distributed-reconstruction-local")
                .HistoricalMethod.AvailableSourceGroupCount);
    }

    [Fact]
    public void CurrentMethodPublicDeclarationsKeepUnknownAndUnitCardinality()
    {
        var received = PastToCurrentInput(7, true, true);
        var normal = CompareCurrentMethods(received);
        var prior = normal.Methods.Single(row =>
            row.HistoricalMethod.Method == "public-prior");
        var c = prior.HistoricalRegions.Single(row => row.HistoricalRegionId == "C");
        Assert.Equal(["F"], c.KnownCurrentRegionIds);
        Assert.Equal(["U1", "U3"], c.KnownUnitIds);
        Assert.Equal(["transition-incomplete", "unit-mapping-incomplete"],
            c.UnresolvedReasons);
        Assert.Equal(["published-C-partial"], c.TransitionSourceIds);
        Assert.Equal(["published-F-partial"], c.UnitMappingSourceIds);
        var missing = CompareCurrentMethods(received, transitions:
            CurrentDeclaredTransitions().Where(row => row.HistoricalRegionId != "C").ToArray());
        var unknown = missing.Methods.Single(row =>
            row.HistoricalMethod.Method == "public-prior").HistoricalRegions.Single(row =>
                row.HistoricalRegionId == "C");
        Assert.Empty(unknown.KnownCurrentRegionIds);
        Assert.Equal("partial", missing.Methods.Single(row =>
            row.HistoricalMethod.Method == "public-prior").ProjectionStatus);
        Assert.Contains("transition-unmapped", unknown.UnresolvedReasons);

        var sameUnit = CompareCurrentMethods(received, mappings:
            CurrentDeclaredUnits().Select(row => row.RegionId == "D"
                ? row with { UnitIds = ["U2"] } : row).ToArray());
        var a = sameUnit.Methods.Single(row =>
            row.HistoricalMethod.Method == "distributed-reconstruction")
                .HistoricalRegions.Single(row => row.HistoricalRegionId == "A");
        Assert.Equal(["U2"], a.KnownUnitIds);
        var multiple = CompareCurrentMethods(received, mappings:
            CurrentDeclaredUnits().Select(row => row.RegionId == "D"
                ? row with { UnitIds = ["U1", "U3"] } : row).ToArray());
        Assert.Equal(["U1", "U2", "U3"],
            multiple.Methods.Single(row =>
                row.HistoricalMethod.Method == "distributed-reconstruction").KnownUnitIds);
        var allUnmapped = CompareCurrentMethods(received, transitions: []);
        Assert.All(allUnmapped.Methods, row =>
        {
            Assert.Equal("unavailable", row.ProjectionStatus);
            Assert.Null(row.KnownUnitIds);
            Assert.Null(row.KnownCurrentRegionIds);
            Assert.All(row.HistoricalRegions, historic =>
                Assert.Contains("transition-unmapped", historic.UnresolvedReasons));
        });
        Assert.Throws<InvalidDataException>(() => CompareCurrentMethods(received,
            CurrentDeclaredTransitions().Select(row => row with
            { AvailableTick = 8 }).ToArray()));
        Assert.Throws<InvalidDataException>(() => CompareCurrentMethods(received,
            mappings: CurrentDeclaredUnits().Select(row => row with
            { AvailableTick = 8 }).ToArray()));
        Assert.Throws<InvalidDataException>(() => CompareCurrentMethods(received,
            CurrentDeclaredTransitions().Select(row => row.HistoricalRegionId == "A"
                ? row with { IsComplete = true, CurrentRegionIds = [] } : row).ToArray()));
    }

    [Fact]
    public void ReceiptGatedPastReconstructionReachesDeclaredCurrentUnitsWithoutFalseCertainty()
    {
        var input = PastToCurrentInput(7, true, true);
        var before = JsonDefaults.Serialize(input);
        var result = ProjectReceivedCurrent(input);
        var method = RescuerEvidenceMethodComparison.Compare(
            input, InteractionTimeWindow.At(2)).Methods.Single(row =>
            row.Method == "distributed-reconstruction");
        Assert.Equal(JsonDefaults.Serialize(method),
            JsonDefaults.Serialize(result.DistributedReconstructionMethod));
        Assert.Equal([RecordId, "second-original"], result.ReceivedOriginalIds);
        Assert.Equal(7, result.QueryTick);
        Assert.Equal(7, result.TargetTick);
        Assert.Equal(7, result.Projection.EvidenceCutoff);
        Assert.Equal(["A", "B", "C"],
            result.Projection.Candidates.Select(row => row.SourceCandidate.RegionId)
                .OrderBy(id => id, StringComparer.Ordinal));
        Assert.Equal(["D", "E", "F"], result.Projection.KnownCurrentRegionIds);
        Assert.Equal(["U1", "U2", "U3"], result.Projection.KnownUnitIds);

        var a = result.Projection.Candidates.Single(row =>
            row.SourceCandidate.RegionId == "A");
        var b = result.Projection.Candidates.Single(row =>
            row.SourceCandidate.RegionId == "B");
        var c = result.Projection.Candidates.Single(row =>
            row.SourceCandidate.RegionId == "C");
        Assert.Equal(["D", "E"], a.KnownCurrentRegionIds);
        Assert.Equal(["U1", "U2"], a.KnownUnitIds);
        Assert.Empty(a.UnresolvedReasons);
        Assert.Equal(["E"], b.KnownCurrentRegionIds);
        Assert.Equal(["U2"], b.KnownUnitIds);
        Assert.Empty(b.UnresolvedReasons);
        Assert.Equal(["F"], c.KnownCurrentRegionIds);
        Assert.Equal(["U1", "U3"], c.KnownUnitIds);
        Assert.Contains("transition-incomplete", c.UnresolvedReasons);
        Assert.Contains("unit-mapping-incomplete", c.UnresolvedReasons);
        Assert.Contains(c.SourceCandidate.CandidateId, result.Projection.UnresolvedCandidateIds);
        Assert.All(result.Projection.Candidates, row =>
        {
            Assert.Equal(result.Projection.QueryId, row.SourceCandidate.QueryId);
            Assert.Equal(TargetId, row.SourceCandidate.TargetEntityId);
            Assert.Equal(2, row.SourceCandidate.TargetTimeWindow.StartTick);
            Assert.Equal(2, row.SourceCandidate.TargetTimeWindow.EndTick);
            Assert.True(Enum.IsDefined(row.SourceCandidate.Disposition));
        });
        Assert.Equal(["published-A-current"], a.TransitionSourceIds);
        Assert.Equal(["published-D-units", "published-E-units"], a.UnitMappingSourceIds);
        var action = Assert.Single(result.Projection.Actions);
        Assert.Equal(["U2"], action.KnownIntersectingUnitIds);
        Assert.Contains(a.SourceCandidate.CandidateId, action.KnownIntersectingCandidateIds);
        Assert.Contains(b.SourceCandidate.CandidateId, action.KnownIntersectingCandidateIds);
        Assert.DoesNotContain(a.SourceCandidate.CandidateId, action.WholeSupportContainedCandidateIds);
        Assert.Contains(b.SourceCandidate.CandidateId, action.WholeSupportContainedCandidateIds);
        Assert.DoesNotContain(c.SourceCandidate.CandidateId, action.WholeSupportContainedCandidateIds);
        Assert.Equal(before, JsonDefaults.Serialize(input));
    }

    [Fact]
    public void ReceiptGatedCurrentProjectionSeparatesSameCutoffAblationFromActualArrival()
    {
        var zero = ProjectReceivedCurrent(PastToCurrentInput(7, false, false));
        var first = ProjectReceivedCurrent(PastToCurrentInput(7, true, false));
        var second = ProjectReceivedCurrent(PastToCurrentInput(7, false, true));
        var both = ProjectReceivedCurrent(PastToCurrentInput(7, true, true));
        Assert.Equal(new[] { 0, 1, 1, 2 },
            new[] { zero.ReceivedOriginalIds.Count, first.ReceivedOriginalIds.Count,
                second.ReceivedOriginalIds.Count, both.ReceivedOriginalIds.Count });
        foreach (var condition in new[] { zero, first, second, both })
        {
            Assert.Equal(["A", "B", "C"],
                condition.DistributedReconstructionMethod.OuterCandidateRegionIds);
            Assert.Equal(["A", "B", "C"],
                condition.Projection.Candidates.Select(row => row.SourceCandidate.RegionId)
                    .OrderBy(id => id, StringComparer.Ordinal));
            Assert.Equal(["U1", "U2", "U3"], condition.Projection.KnownUnitIds);
            Assert.Single(condition.Projection.UnresolvedCandidateIds);
            Assert.True(condition.ReconstructionSteps >= 0);
        }

        // AR-20: the original early observation equals the public prior.
        // Later traversal evidence is not a tick-2 reconstruction source.
        foreach (var (report, expectedDisposition, expectedGroups) in new[]
                 {
                     (zero, "Unavailable", 0),
                     (first, "Supported", 1),
                     (second, "Unavailable", 0),
                     (both, "Supported", 1)
                 })
        {
            Assert.Equal(expectedGroups,
                report.DistributedReconstructionMethod.UsedSourceGroupCount);
            Assert.All(report.Projection.Candidates, row =>
            {
                Assert.Equal(expectedDisposition, row.SourceCandidate.Disposition.ToString());
                Assert.DoesNotContain("weft-b", row.SourceCandidate.SourceAncestryIds);
                Assert.DoesNotContain("second-original", row.SourceCandidate.SourceAncestryIds);
                if (expectedGroups == 0)
                    Assert.DoesNotContain("weft-a", row.SourceCandidate.SourceAncestryIds);
                else
                    Assert.Contains("weft-a", row.SourceCandidate.SourceAncestryIds);
            });
        }
        Assert.Equal(JsonDefaults.Serialize(zero.Projection.Candidates),
            JsonDefaults.Serialize(second.Projection.Candidates));
        Assert.Equal(JsonDefaults.Serialize(first.Projection.Candidates),
            JsonDefaults.Serialize(both.Projection.Candidates));
        var secondMethods = RescuerEvidenceMethodComparison.Compare(
            PastToCurrentInput(7, false, true), InteractionTimeWindow.At(2)).Methods;
        var bothMethods = RescuerEvidenceMethodComparison.Compare(
            PastToCurrentInput(7, true, true), InteractionTimeWindow.At(2)).Methods;
        Assert.Equal(1, secondMethods.Single(row =>
            row.Method == "known-relation-backward").UsedSourceGroupCount);
        Assert.Equal(2, bothMethods.Single(row =>
            row.Method == "distributed-reconstruction-local").UsedSourceGroupCount);

        // Arrival at cutoff six is not a same-cutoff-seven ablation.
        var early = ProjectReceivedCurrent(PastToCurrentInput(6, true, true));
        Assert.Equal(6, early.QueryTick);
        Assert.Equal(6, early.Projection.EvidenceCutoff);
        Assert.Equal([RecordId], early.ReceivedOriginalIds);
        Assert.DoesNotContain("second-original", JsonDefaults.Serialize(early.Projection));
        var reordered = ProjectReceivedCurrent(PastToCurrentInput(
            7, true, true, reversed: true, duplicate: true));
        Assert.Equal(JsonDefaults.Serialize(both), JsonDefaults.Serialize(reordered));
        Assert.Equal(2, reordered.AvailableSourceGroupCount);
    }

    // AR-20: same public world, but a distinguishing received early observation.
    [Fact]
    public void ReceiptGatedCurrentProjectionDiscriminatesInformativeEarlyWeft()
    {
        var zero = ProjectReceivedCurrent(PastToCurrentInput(
            7, false, false, firstRegions: ["A", "B"]));
        var first = ProjectReceivedCurrent(PastToCurrentInput(
            7, true, false, firstRegions: ["A", "B"]));
        var both = ProjectReceivedCurrent(PastToCurrentInput(
            7, true, true, firstRegions: ["A", "B"]));

        Assert.Equal(["A", "B", "C"],
            zero.DistributedReconstructionMethod.OuterCandidateRegionIds);
        Assert.Equal(["A", "B", "C"],
            zero.Projection.Candidates.Select(row => row.SourceCandidate.RegionId)
                .OrderBy(id => id, StringComparer.Ordinal));
        Assert.Equal(["D", "E", "F"], zero.Projection.KnownCurrentRegionIds);
        Assert.Equal(["U1", "U2", "U3"], zero.Projection.KnownUnitIds);
        Assert.Single(zero.Projection.UnresolvedCandidateIds);
        Assert.Equal(0, zero.DistributedReconstructionMethod.UsedSourceGroupCount);
        Assert.All(zero.Projection.Candidates, row =>
            Assert.Equal("Unavailable", row.SourceCandidate.Disposition.ToString()));

        foreach (var result in new[] { first, both })
        {
            Assert.Equal(["A", "B"],
                result.DistributedReconstructionMethod.OuterCandidateRegionIds);
            Assert.Equal(["A", "B"],
                result.Projection.Candidates.Select(row => row.SourceCandidate.RegionId)
                    .OrderBy(id => id, StringComparer.Ordinal));
            Assert.Equal(["D", "E"], result.Projection.KnownCurrentRegionIds);
            Assert.Equal(["U1", "U2"], result.Projection.KnownUnitIds);
            Assert.Empty(result.Projection.UnresolvedCandidateIds);
            Assert.Equal(1, result.DistributedReconstructionMethod.UsedSourceGroupCount);
            Assert.All(result.Projection.Candidates, row =>
            {
                Assert.Equal("Supported", row.SourceCandidate.Disposition.ToString());
                Assert.Contains("weft-a", row.SourceCandidate.SourceAncestryIds);
                Assert.DoesNotContain("weft-b", row.SourceCandidate.SourceAncestryIds);
                Assert.DoesNotContain("second-original", row.SourceCandidate.SourceAncestryIds);
            });
        }

        Assert.NotEqual(JsonDefaults.Serialize(zero.Projection.Candidates),
            JsonDefaults.Serialize(first.Projection.Candidates));
        Assert.NotEqual(JsonDefaults.Serialize(zero.Projection.Candidates),
            JsonDefaults.Serialize(both.Projection.Candidates));
        Assert.Equal(JsonDefaults.Serialize(first.Projection.Candidates),
            JsonDefaults.Serialize(both.Projection.Candidates));
        var reordered = ProjectReceivedCurrent(PastToCurrentInput(
            7, true, true, reversed: true, duplicate: true,
            firstRegions: ["A", "B"]));
        Assert.Equal(JsonDefaults.Serialize(both), JsonDefaults.Serialize(reordered));
    }

    [Fact]
    public void ReceiptGatedCurrentProjectionKeepsPartialAndUnavailablePublicDeclarationsUnknown()
    {
        var input = PastToCurrentInput(7, true, true);
        var missing = ProjectReceivedCurrent(input,
            CurrentDeclaredTransitions().Where(row => row.HistoricalRegionId != "C").ToArray());
        var c = missing.Projection.Candidates.Single(row =>
            row.SourceCandidate.RegionId == "C");
        Assert.Empty(c.KnownCurrentRegionIds);
        Assert.Contains("transition-unmapped", c.UnresolvedReasons);
        Assert.Contains(c.SourceCandidate.CandidateId, missing.Projection.UnresolvedCandidateIds);

        var noE = ProjectReceivedCurrent(input, mappings:
            CurrentDeclaredUnits().Where(row => row.RegionId != "E").ToArray());
        var a = noE.Projection.Candidates.Single(row =>
            row.SourceCandidate.RegionId == "A");
        Assert.Contains("unit-mapping-unmapped", a.UnresolvedReasons);
        Assert.DoesNotContain(a.SourceCandidate.CandidateId,
            Assert.Single(noE.Projection.Actions).WholeSupportContainedCandidateIds);
        Assert.Throws<InvalidDataException>(() => ProjectReceivedCurrent(input,
            CurrentDeclaredTransitions().Select(row =>
                row.HistoricalRegionId == "B" ? row with { AvailableTick = 8 } : row)
                .ToArray()));
        Assert.Throws<InvalidDataException>(() => ProjectReceivedCurrent(input,
            mappings: CurrentDeclaredUnits().Select(row =>
                row.RegionId == "D" ? row with { AvailableTick = 8 } : row)
                .ToArray()));
        Assert.Throws<InvalidDataException>(() => ProjectReceivedCurrent(input,
            CurrentDeclaredTransitions().Select(row =>
                row.HistoricalRegionId == "B" ? row with
                { IsComplete = true, CurrentRegionIds = [] } : row).ToArray()));
    }

    [Fact]
    public void ReceiptGatedCurrentProjectionDoesNotPromoteSharedOrDeviceOnlyOrigins()
    {
        var shared = ProjectReceivedCurrent(PastToCurrentInput(
            7, true, true, laterAncestry: "weft-a"));
        var device = ProjectReceivedCurrent(PastToCurrentInput(
            7, true, true, deviceOnly: true));
        var conflict = ProjectReceivedCurrent(PastToCurrentInput(
            7, true, true, firstState: EvidenceState.Conflicting));
        Assert.Equal(1, shared.AvailableSourceGroupCount);
        foreach (var result in new[] { shared, device, conflict })
        {
            Assert.Equal(result.DistributedReconstructionMethod.OuterCandidateRegionIds,
                result.Projection.Candidates.Select(row => row.SourceCandidate.RegionId)
                    .OrderBy(id => id, StringComparer.Ordinal));
            Assert.All(result.Projection.Candidates, candidate =>
                Assert.True(Enum.IsDefined(candidate.SourceCandidate.Disposition)));
            Assert.Contains("transition-incomplete",
                result.Projection.Candidates.Single(row =>
                    row.SourceCandidate.RegionId == "C").UnresolvedReasons);
        }
        var ordinary = ProjectReceivedCurrent(PastToCurrentInput(7, true, true));
        Assert.True(device.DistributedReconstructionMethod.UsedSourceGroupCount <=
            ordinary.DistributedReconstructionMethod.UsedSourceGroupCount);
    }

    [Fact]
    public void DeclaredWitnessUnitsKeepMixedCandidateStatusesAndDirectProvenance()
    {
        var (u, k) = RelationUtilityInputs();
        var before = JsonDefaults.Serialize(u);
        var after = JsonDefaults.Serialize(k);
        var report = DeclaredUnitCoverage(u, k);
        Assert.Equal("available", report.Status);
        Assert.Equal(7, report.QueryTick);
        Assert.Equal(2, report.HistoricalTick);
        Assert.Equal([RecordId, "second-original"], report.ReceivedOriginalIds);
        Assert.Equal(4, report.Methods.Count);
        // Real counter values on this fixed U/K fixture, not CPU time or
        // rescue workload. The branch-specific diagnostic counters remain
        // separately exposed and are not inferred from these values.
        var expectedPublicInspections = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["public-prior"] = 0,
            ["known-relation-backward"] = 5,
            ["distributed-reconstruction"] = 1,
            ["distributed-reconstruction-local"] = 5
        };
        foreach (var method in report.Methods)
        {
            foreach (var condition in new[]
                     { method.WithoutDeclaration, method.WithDeclaration })
            {
                Assert.Equal("available", condition.Status);
                Assert.Equal(["A", "B", "C"],
                    condition.PublicMethod.OuterCandidateRegionIds);
                Assert.Equal(["U1", "U2"], condition.UnitProjection.KnownUnitIds);
                Assert.Empty(condition.UnresolvedRegions);
                Assert.Equal(2, condition.Units.Count);
                Assert.Equal(expectedPublicInspections[method.Method],
                    condition.PublicMethod.PublicRelationsInspected);
                Assert.Equal(2, condition.PublicMethod.AvailableSourceGroupCount);
                Assert.All(condition.Units.SelectMany(unit => unit.Regions),
                    region => Assert.Contains(region.RegionId,
                        condition.PublicMethod.OuterCandidateRegionIds));
            }
            Assert.Equal(method.WithoutDeclaration.PublicMethod.Steps,
                method.WithDeclaration.PublicMethod.Steps);
            var u1Before = method.WithoutDeclaration.Units.Single(unit => unit.UnitId == "U1");
            var u1After = method.WithDeclaration.Units.Single(unit => unit.UnitId == "U1");
            Assert.Equal(["A"], u1Before.WitnessedRegionIds);
            Assert.Equal(["C"], u1Before.UnknownRegionIds);
            Assert.Equal(["A"], u1After.WitnessedRegionIds);
            Assert.Equal(["C"], u1After.UnknownRegionIds);
            var u2Before = method.WithoutDeclaration.Units.Single(unit => unit.UnitId == "U2");
            var u2After = method.WithDeclaration.Units.Single(unit => unit.UnitId == "U2");
            Assert.Empty(u2Before.WitnessedRegionIds);
            Assert.Equal(["B"], u2Before.UnknownRegionIds);
            Assert.Equal(["B"], u2After.WitnessedRegionIds);
            Assert.Empty(u2After.UnknownRegionIds);
            var witnessA = Assert.Single(u1Before.Regions.Single(
                region => region.RegionId == "A").DirectWitnesses);
            Assert.Equal("declared-A-D", witnessA.BranchProvenanceId);
            Assert.Equal(0, witnessA.BranchPublishedAtTick);
            Assert.Equal(2, witnessA.CompatibleEarlyTick);
            Assert.Equal(5, witnessA.CompatibleLaterTick);
            Assert.Equal(RecordId, witnessA.EarlyDirectSourceId);
            Assert.Equal("second-original", witnessA.LaterDirectSourceId);
            Assert.Contains("weft-a", witnessA.EarlySourceAncestryIds);
            Assert.Contains("weft-b", witnessA.LaterSourceAncestryIds);
            Assert.True(witnessA.IndependentSourceAncestry);
            Assert.Equal("unknown", witnessA.PersonTraversability);
            var witnessB = Assert.Single(u2After.Regions.Single(
                region => region.RegionId == "B").DirectWitnesses);
            Assert.Equal("separate-public-BD", witnessB.BranchProvenanceId);
            Assert.Equal(7, witnessB.BranchPublishedAtTick);
            Assert.Equal(2, witnessB.CompatibleEarlyTick);
            Assert.Equal(5, witnessB.CompatibleLaterTick);
            Assert.Equal(RecordId, witnessB.EarlyDirectSourceId);
            Assert.Equal("second-original", witnessB.LaterDirectSourceId);
            Assert.Contains("weft-a", witnessB.EarlySourceAncestryIds);
            Assert.Contains("weft-b", witnessB.LaterSourceAncestryIds);
            Assert.True(witnessB.IndependentSourceAncestry);
            Assert.Equal("unknown", witnessB.PersonTraversability);
            // One witness does not certify the whole many-to-one unit.
            Assert.Equal(2, u1Before.Regions.Count);
        }
        Assert.Equal(before, JsonDefaults.Serialize(u));
        Assert.Equal(after, JsonDefaults.Serialize(k));
        var (revU, revK) = RelationUtilityInputs(
            reversed: true, duplicateReceipt: true);
        Assert.Equal(JsonDefaults.Serialize(report),
            JsonDefaults.Serialize(DeclaredUnitCoverage(revU, revK)));
    }

    [Fact]
    public void DeclaredWitnessUnitsHoldUnreceivedAndKeepAncestrySeparate()
    {
        var (earlyU, earlyK) = RelationUtilityInputs(receiveSecond: false);
        var early = DeclaredUnitCoverage(earlyU, earlyK);
        Assert.Equal("unavailable", early.Status);
        Assert.Empty(early.Methods);

        var (deviceU, deviceK) = RelationUtilityInputs(deviceOnly: true);
        var device = DeclaredUnitCoverage(deviceU, deviceK);
        Assert.All(device.Methods.SelectMany(method => new[]
                   { method.WithoutDeclaration, method.WithDeclaration })
                   .SelectMany(condition => condition.Units)
                   .SelectMany(unit => unit.Regions),
            region => Assert.Empty(region.DirectWitnesses));

        var (sharedU, sharedK) = RelationUtilityInputs(laterAncestry: "weft-a");
        var shared = DeclaredUnitCoverage(sharedU, sharedK);
        var b = shared.Methods[0].WithDeclaration.Units.Single(
            unit => unit.UnitId == "U2").Regions.Single(
                region => region.RegionId == "B");
        Assert.Equal("witnessed", b.CompatibilityStatus);
        Assert.False(Assert.Single(b.DirectWitnesses).IndependentSourceAncestry);

        var (conflictU, conflictK) = RelationUtilityInputs(
            firstState: EvidenceState.Conflicting);
        var conflict = DeclaredUnitCoverage(conflictU, conflictK);
        Assert.DoesNotContain(conflict.Methods.SelectMany(method =>
                method.WithDeclaration.Units).SelectMany(unit => unit.Regions),
            region => region.CompatibilityStatus == "witnessed");
    }

    // #118: AR-19 V0/N10/N11 use the actual received input builder.
    // The new reason codes may explain "unknown", but may not eliminate
    // any candidate or give a whole unit a positive Person interpretation.
    [Fact]
    public void DeclaredWitnessUnitsDistinguishUnwitnessedReasonsWithoutExclusion()
    {
        var (vU, vK) = RelationUtilityInputs();
        var (nU, nK) = RelationUtilityInputs(firstRegions: ["A", "B"]);
        var (tU, tK) = RelationUtilityInputs(firstRegions: ["A", "B"], laterTick: 6);
        var v0 = DeclaredUnitCoverage(vU, vK);
        var n10 = DeclaredUnitCoverage(nU, nK);
        var n11 = DeclaredUnitCoverage(tU, tK);

        static RescuerDeclaredWitnessUnitRegion Region(
            RescuerDeclaredWitnessUnitReport report, string method,
            string unit, string region, bool knownDeclaration = false)
        {
            var row = report.Methods.Single(m => m.Method == method);
            var condition = knownDeclaration ? row.WithDeclaration : row.WithoutDeclaration;
            return condition.Units.Single(u => u.UnitId == unit).Regions.Single(
                r => r.RegionId == region);
        }

        foreach (var report in new[] { v0, n10, n11 })
        {
            Assert.Equal("available", report.Status);
            foreach (var method in report.Methods)
            {
                Assert.Equal(method.WithoutDeclaration.PublicMethod.OuterCandidateRegionIds,
                    method.WithDeclaration.PublicMethod.OuterCandidateRegionIds);
                Assert.Equal(method.WithoutDeclaration.UnitProjection.KnownUnitIds,
                    method.WithDeclaration.UnitProjection.KnownUnitIds);
                Assert.Equal(["U1", "U2"], method.WithoutDeclaration.UnitProjection.KnownUnitIds);
                Assert.All(method.WithoutDeclaration.Units
                    .Concat(method.WithDeclaration.Units)
                    .SelectMany(u => u.Regions)
                    .Where(r => r.CompatibilityStatus != "unknown"),
                    r => Assert.Empty(r.UnwitnessedReasonCodes));
            }
        }
        foreach (var method in v0.Methods)
        {
            Assert.Equal(["endpoint-pair-unpublished"],
                Region(v0, method.Method, "U1", "C").UnwitnessedReasonCodes);
            Assert.Equal(["endpoint-pair-unpublished"],
                Region(v0, method.Method, "U1", "C", true).UnwitnessedReasonCodes);
            Assert.Empty(Region(v0, method.Method, "U1", "A").UnwitnessedReasonCodes);
        }

        foreach (var method in new[] { "public-prior", "known-relation-backward" })
        {
            Assert.Equal(["A", "B", "C"], n10.Methods.Single(m =>
                m.Method == method).WithoutDeclaration.PublicMethod.OuterCandidateRegionIds);
            Assert.Equal(["early-person-not-supported"],
                Region(n10, method, "U1", "C").UnwitnessedReasonCodes);
            Assert.Equal(["early-person-not-supported"],
                Region(n10, method, "U1", "C", true).UnwitnessedReasonCodes);
            Assert.Equal(["A", "C"],
                n11.Methods.Single(m => m.Method == method).WithoutDeclaration.Units
                    .Single(u => u.UnitId == "U1").UnknownRegionIds);
            Assert.Equal(["published-pair-shorter-dwell-unverified",
                    "published-pair-timing-incompatible"],
                Region(n11, method, "U1", "A").UnwitnessedReasonCodes);
            Assert.Equal(["early-person-not-supported"],
                Region(n11, method, "U1", "C").UnwitnessedReasonCodes);
        }
        foreach (var method in new[] { "distributed-reconstruction",
                     "distributed-reconstruction-local" })
        {
            Assert.DoesNotContain("C", n10.Methods.Single(m =>
                m.Method == method).WithoutDeclaration.PublicMethod.OuterCandidateRegionIds);
            Assert.DoesNotContain(n10.Methods.Single(m => m.Method == method)
                .WithoutDeclaration.Units.SelectMany(u => u.Regions),
                r => r.RegionId == "C");
        }
        var (revU, revK) = RelationUtilityInputs(
            reversed: true, duplicateReceipt: true, firstRegions: ["A", "B"]);
        Assert.Equal(JsonDefaults.Serialize(n10),
            JsonDefaults.Serialize(DeclaredUnitCoverage(revU, revK)));

        var (deviceU, deviceK) = RelationUtilityInputs(deviceOnly: true);
        var device = DeclaredUnitCoverage(deviceU, deviceK);
        Assert.All(device.Methods.SelectMany(m => m.WithoutDeclaration.Units
            .Concat(m.WithDeclaration.Units)).SelectMany(u => u.Regions),
            r => Assert.Empty(r.UnwitnessedReasonCodes));
        var (softU, softK) = RelationUtilityInputs(
            firstState: EvidenceState.SupportedButUncertain);
        var soft = DeclaredUnitCoverage(softU, softK);
        Assert.All(soft.Methods.SelectMany(m => m.WithoutDeclaration.Units
            .Concat(m.WithDeclaration.Units)).SelectMany(u => u.Regions)
            .Where(r => r.CompatibilityStatus == "unknown"),
            r => Assert.Equal(["insufficient-public-evidence"],
                r.UnwitnessedReasonCodes));
    }

    // #122 / AR-21: the same #106 receipt/cutoff candidates and public
    // alternatives must explain #118's unknown labels, without promoting
    // formal paths or stays to witnessed Person movement or hard exclusions.
    [Fact]
    public void DeclaredUnknownReasonsReusePublicStayPathsTimingAndIncompleteSearch()
    {
        var original = ComplementaryPublicContext();
        var builder = new RescuerEvidenceInputBuilder();
        (RescuerEvidenceInput U, RescuerEvidenceInput K) Inputs(
            RescueContext world, long lateTick = 5,
            IReadOnlyList<string>? lateRegions = null, bool reverse = false,
            bool duplicate = false, bool receiveSecond = true)
        {
            var kWorld = world with
            {
                PublicHazardEnvelope = world.PublicHazardEnvelope! with
                {
                    Branches = [.. world.PublicHazardEnvelope!.Branches,
                        Branch("B", "D") with
                        {
                            MinimumTravelTicks = 3, MaximumTravelTicks = 3,
                            PublishedAtTick = 7,
                            ProvenanceId = "separate-public-BD"
                        }]
                }
            };
            var first = ComplementaryOriginal(RecordId, HolderId, 2,
                ["A", "B", "C"], 3, "weft-a");
            var later = ComplementaryOriginal("second-original", "holder-b",
                lateTick, lateRegions ?? ["D"], lateTick + 1, "weft-b");
            var records = reverse ? new[] { later, first } : new[] { first, later };
            var receipts = new List<InteractionRecordReceipt> { Receipt(6) };
            if (receiveSecond)
                receipts.Add(LaterComparisonReceipt(7));
            if (duplicate)
                receipts.Add(Receipt(6));
            if (reverse)
                receipts.Reverse();
            return (
                builder.BuildDistinctOriginals(world, Seed, 7, RescuerId,
                    records, receipts),
                builder.BuildDistinctOriginals(kWorld, Seed, 7, RescuerId,
                    records, receipts));
        }

        static RescuerDeclaredWitnessUnitRegion Region(
            RescuerDeclaredWitnessUnitReport report, string id)
            => report.Methods.Single(m => m.Method == "public-prior")
                .WithoutDeclaration.Units.SelectMany(u => u.Regions)
                .Single(r => r.RegionId == id);

        RescuerDeclaredWitnessUnitReport Verify(
            (RescuerEvidenceInput U, RescuerEvidenceInput K) inputs)
        {
            var (u, k) = inputs;
            var before = JsonDefaults.Serialize(u);
            var diagnostics = RescuerEvidenceMethodComparison
                .AnalyzeDeclaredPathCompatibility(u, InteractionTimeWindow.At(2));
            var questions = RescuerEvidenceMethodComparison
                .AnalyzeMissingDeclaredPathQuestions(u, InteractionTimeWindow.At(2));
            var compared = RescuerEvidenceMethodComparison.Compare(
                u, InteractionTimeWindow.At(2));
            var result = DeclaredUnitCoverage(u, k);
            Assert.Equal("available", result.Status);
            Assert.Equal(4, result.Methods.Count);
            Assert.Equal([RecordId, "second-original"],
                result.ReceivedOriginalIds);
            Assert.Equal(JsonDefaults.Serialize(u), before);
            Assert.All(result.Methods, method =>
            {
                var reference = compared.Methods.Single(row =>
                    row.Method == method.Method);
                Assert.Equal(reference.OuterCandidateRegionIds,
                    method.WithoutDeclaration.PublicMethod.OuterCandidateRegionIds);
                Assert.Equal(method.WithoutDeclaration.PublicMethod.OuterCandidateRegionIds,
                    method.WithDeclaration.PublicMethod.OuterCandidateRegionIds);
                Assert.Equal(method.WithoutDeclaration.UnitProjection.KnownUnitIds,
                    method.WithDeclaration.UnitProjection.KnownUnitIds);
                Assert.All(method.WithoutDeclaration.Units.SelectMany(unit => unit.Regions),
                    item =>
                    {
                        Assert.Equal(diagnostics.Candidates.Single(candidate =>
                            candidate.RegionId == item.RegionId).Status,
                            item.CompatibilityStatus);
                        Assert.Equal(item.UnwitnessedReasonCodes.Distinct(
                            StringComparer.Ordinal).OrderBy(code => code,
                            StringComparer.Ordinal), item.UnwitnessedReasonCodes);
                        if (item.CompatibilityStatus != "unknown")
                            Assert.Empty(item.UnwitnessedReasonCodes);
                    });
            });
            Assert.Equal(diagnostics.Candidates.Select(row => row.RegionId),
                questions.Candidates.Select(row => row.RegionId));
            return result;
        }

        var stayWorld = original with
        {
            PublicCausalTracingModel = new PublicCausalTracingModel
            {
                CandidateStates = [State(2, ["A", "B", "C"]),
                    State(5, ["C", "D", "E", "F"])]
            }
        };
        var stInputs = Inputs(stayWorld, lateRegions: ["C"]);
        var st = Verify(stInputs);
        Assert.Equal("unknown", Region(st, "C").CompatibilityStatus);
        Assert.Equal(["formal-stay-public-alternative"],
            Region(st, "C").UnwitnessedReasonCodes);
        Assert.Empty(Region(st, "C").DirectWitnesses);
        Assert.Contains("formal-stay:C", RescuerEvidenceMethodComparison
            .AnalyzeMissingDeclaredPathQuestions(stInputs.U,
                InteractionTimeWindow.At(2)).Candidates
            .Single(row => row.RegionId == "C").PublicAlternativeExplanations);

        var st2Inputs = Inputs(stayWorld, lateRegions: ["C", "D"]);
        var st2 = Verify(st2Inputs);
        Assert.Equal(["endpoint-pair-unpublished",
                "formal-stay-public-alternative"],
            Region(st2, "C").UnwitnessedReasonCodes);
        Assert.Single(RescuerEvidenceMethodComparison.AnalyzeMissingDeclaredPathQuestions(
            st2Inputs.U, InteractionTimeWindow.At(2)).Candidates
            .Single(row => row.RegionId == "C").Questions);

        var hopWorld = original with
        {
            PublicHazardEnvelope = original.PublicHazardEnvelope! with
            {
                Branches = [.. original.PublicHazardEnvelope!.Branches,
                    Branch("C", "E") with
                    {
                        MinimumTravelTicks = 1, MaximumTravelTicks = 1,
                        ProvenanceId = "AR21-C-E"
                    },
                    Branch("E", "D") with
                    {
                        MinimumTravelTicks = 2, MaximumTravelTicks = 2,
                        ProvenanceId = "AR21-E-D"
                    }]
            }
        };
        var h2Inputs = Inputs(hopWorld);
        var h2 = Verify(h2Inputs);
        Assert.Equal(["formal-two-hop-public-alternative"],
            Region(h2, "C").UnwitnessedReasonCodes);
        Assert.Empty(Region(h2, "C").DirectWitnesses);
        Assert.Contains("formal-two-hop:C>E>D", RescuerEvidenceMethodComparison
            .AnalyzeMissingDeclaredPathQuestions(h2Inputs.U,
                InteractionTimeWindow.At(2)).Candidates
            .Single(row => row.RegionId == "C").PublicAlternativeExplanations);
        Assert.Equal(JsonDefaults.Serialize(h2),
            JsonDefaults.Serialize(Verify(Inputs(hopWorld,
                reverse: true, duplicate: true))));

        var later6 = RelationUtilityInputs(firstRegions: ["A", "B"], laterTick: 6);
        var n11 = DeclaredUnitCoverage(later6.U, later6.K);
        Assert.Equal(["published-pair-shorter-dwell-unverified",
                "published-pair-timing-incompatible"],
            Region(n11, "A").UnwitnessedReasonCodes);
        var later4 = Inputs(original, lateTick: 4);
        var tooLong = Verify(later4);
        Assert.Equal(["published-pair-timing-incompatible", "published-pair-too-long"],
            Region(tooLong, "A").UnwitnessedReasonCodes);

        var threeHopWorld = original with
        {
            PublicHazardEnvelope = original.PublicHazardEnvelope! with
            {
                Branches = [.. original.PublicHazardEnvelope!.Branches,
                    Branch("C", "E") with
                    {
                        MinimumTravelTicks = 1, MaximumTravelTicks = 1
                    },
                    Branch("E", "F") with
                    {
                        MinimumTravelTicks = 1, MaximumTravelTicks = 1
                    },
                    Branch("F", "D") with
                    {
                        MinimumTravelTicks = 1, MaximumTravelTicks = 1
                    }]
            }
        };
        Assert.Contains("formal-multihop-public-alternative",
            Region(Verify(Inputs(threeHopWorld)), "C").UnwitnessedReasonCodes);

        var unpublishedHop = hopWorld with
        {
            PublicHazardEnvelope = hopWorld.PublicHazardEnvelope! with
            {
                Branches = hopWorld.PublicHazardEnvelope!.Branches.Select(branch =>
                    branch.ProvenanceId.StartsWith("AR21-", StringComparison.Ordinal)
                        ? branch with { PublishedAtTick = 8 } : branch).ToArray()
            }
        };
        var futureInputs = Inputs(unpublishedHop);
        // #112's four-method comparison rejects future-dated public branches;
        // independently, #106 must not count them as published explanations.
        Assert.Throws<ArgumentException>(() =>
            DeclaredUnitCoverage(futureInputs.U, futureInputs.K));
        var futureQuestions = RescuerEvidenceMethodComparison
            .AnalyzeMissingDeclaredPathQuestions(futureInputs.U,
                InteractionTimeWindow.At(2));
        var futureC = futureQuestions.Candidates.Single(row => row.RegionId == "C");
        Assert.Single(futureC.Questions);
        Assert.Empty(futureC.PublicAlternativeExplanations);

        var chainNames = Enumerable.Range(0, 9).Select(i => "T" + i).ToArray();
        var chain = new List<PublicHazardBranch>();
        var source = "C";
        foreach (var destination in chainNames.Append("D"))
        {
            chain.Add(Branch(source, destination) with
            {
                MinimumTravelTicks = 0,
                MaximumTravelTicks = 0
            });
            source = destination;
        }
        var deepWorld = original with
        {
            PublicRegions = [.. original.PublicRegions,
                .. chainNames.Select((id, i) => new RegionDefinition
                {
                    Id = id, Type = RegionType.Outdoor,
                    Center = new Position(40 + i, 0, 0, id)
                })],
            PublicHazardEnvelope = original.PublicHazardEnvelope! with
            {
                Branches = [.. original.PublicHazardEnvelope!.Branches, .. chain]
            }
        };
        var deep = Verify(Inputs(deepWorld));
        Assert.Contains("public-path-search-incomplete",
            Region(deep, "C").UnwitnessedReasonCodes);

        var beforeSecond = RelationUtilityInputs(receiveSecond: false);
        Assert.All(DeclaredUnitCoverage(beforeSecond.U, beforeSecond.K)
            .Methods.SelectMany(m => m.WithoutDeclaration.Units)
            .SelectMany(u => u.Regions),
            r => Assert.Empty(r.UnwitnessedReasonCodes));
    }


    // #126 / AR-22: an incompatible published direct edge describes that
    // edge, not every possible published route. Adding public information
    // must not erase an already valid formal multi-hop explanation.
    [Fact]
    public void PublishedDirectDoesNotHideCompatibleMultihopAlternatives()
    {
        var original = ComplementaryPublicContext();
        var builder = new RescuerEvidenceInputBuilder();
        (RescuerEvidenceInput U, RescuerEvidenceInput K) Inputs(
            RescueContext world, long lateTick, bool reversed = false,
            bool duplicate = false)
        {
            var kWorld = world with
            {
                PublicHazardEnvelope = world.PublicHazardEnvelope! with
                {
                    Branches = [.. world.PublicHazardEnvelope!.Branches,
                        Branch("B", "D") with
                        {
                            PublishedAtTick = 7,
                        ProvenanceId = "AR22-separate-public-BD"
                        }]
                }
            };
            var early = ComplementaryOriginal(RecordId, HolderId,
                2, ["A", "B", "C"], 3, "weft-a");
            var late = ComplementaryOriginal("second-original", "holder-b",
                lateTick, ["D"], lateTick + 1, "weft-b");
            DeviceHeldInteractionRecord[] originals = reversed
                ? [late, early] : [early, late];
            var receipts = new List<InteractionRecordReceipt>
                { Receipt(6), LaterComparisonReceipt(7) };
            if (duplicate)
                receipts.Add(Receipt(6));
            if (reversed)
                receipts.Reverse();
            return (
                builder.BuildDistinctOriginals(world, Seed, 7, RescuerId,
                    originals, receipts),
                builder.BuildDistinctOriginals(kWorld, Seed, 7, RescuerId,
                    originals, receipts));
        }

        static RescuerMissingDeclaredPathReport Questions(RescuerEvidenceInput input) =>
            RescuerEvidenceMethodComparison.AnalyzeMissingDeclaredPathQuestions(
                input, InteractionTimeWindow.At(2));
        static RescuerMissingDeclaredPathCandidate Candidate(
            RescuerMissingDeclaredPathReport report) =>
            report.Candidates.Single(row => row.RegionId == "A");
        static RescuerDeclaredWitnessUnitRegion UnitA(
            RescuerDeclaredWitnessUnitReport report, string method) =>
            report.Methods.Single(row => row.Method == method)
                .WithoutDeclaration.Units.SelectMany(unit => unit.Regions)
                .Single(region => region.RegionId == "A");

        var tlhWorld = original with
        {
            PublicHazardEnvelope = original.PublicHazardEnvelope! with
            {
                Branches = [.. original.PublicHazardEnvelope!.Branches,
                    Branch("A", "E") with
                    {
                        MinimumTravelTicks = 1, MaximumTravelTicks = 1,
                        ProvenanceId = "AR22-A-E"
                    },
                    Branch("E", "D") with
                    {
                        MinimumTravelTicks = 1, MaximumTravelTicks = 1,
                        ProvenanceId = "AR22-E-D"
                    }]
            }
        };
        var noDirectWorld = tlhWorld with
        {
            PublicHazardEnvelope = tlhWorld.PublicHazardEnvelope! with
            {
                Branches = tlhWorld.PublicHazardEnvelope!.Branches
                    .Where(edge => !(edge.SourceRegionId == "A" &&
                        edge.DestinationRegionId == "D")).ToArray()
            }
        };
        var (tlhU, tlhK) = Inputs(tlhWorld, 4);
        var (withoutU, withoutK) = Inputs(noDirectWorld, 4);
        var tlh = Questions(tlhU);
        var noDirect = Questions(withoutU);
        var tlhA = Candidate(tlh);
        var noDirectA = Candidate(noDirect);
        Assert.Equal("available", tlh.Status);
        Assert.Equal(["original-encounter-record", "second-original"],
            tlh.ReceivedOriginalIds);
        Assert.Empty(tlhA.Questions);
        Assert.Empty(noDirectA.Questions);
        Assert.Equal("unknown", tlhA.DeclaredPathStatus);
        Assert.Equal("unknown", noDirectA.DeclaredPathStatus);
        Assert.Contains("formal-two-hop:A>E>D",
            tlhA.PublicAlternativeExplanations);
        Assert.Contains("formal-two-hop:A>E>D",
            noDirectA.PublicAlternativeExplanations);
        Assert.Contains(tlhA.PublicAlternativeExplanations, explanation =>
            explanation.StartsWith("direct-pair-published-too-long:A>D:",
                StringComparison.Ordinal));
        Assert.DoesNotContain(noDirectA.PublicAlternativeExplanations,
            explanation => explanation.StartsWith("direct-pair-published-",
                StringComparison.Ordinal));
        // Direct-edge inspection is additional work, not free evidence.
        Assert.True(tlh.PublicTraversalBranchesInspected >
            noDirect.PublicTraversalBranchesInspected);

        var tlhCompatibility = RescuerEvidenceMethodComparison
            .AnalyzeDeclaredPathCompatibility(tlhU, InteractionTimeWindow.At(2));
        var noDirectCompatibility = RescuerEvidenceMethodComparison
            .AnalyzeDeclaredPathCompatibility(withoutU, InteractionTimeWindow.At(2));
        Assert.Equal(tlhCompatibility.Candidates.Select(row =>
            (row.RegionId, row.Status)), noDirectCompatibility.Candidates.Select(row =>
            (row.RegionId, row.Status)));

        var tlhUnits = DeclaredUnitCoverage(tlhU, tlhK);
        var noDirectUnits = DeclaredUnitCoverage(withoutU, withoutK);
        Assert.Equal(4, tlhUnits.Methods.Count);
        Assert.Equal(4, noDirectUnits.Methods.Count);
        foreach (var method in tlhUnits.Methods)
        {
            var other = noDirectUnits.Methods.Single(row => row.Method == method.Method);
            Assert.Equal(method.WithoutDeclaration.PublicMethod.OuterCandidateRegionIds,
                other.WithoutDeclaration.PublicMethod.OuterCandidateRegionIds);
            Assert.Equal(method.WithoutDeclaration.UnitProjection.KnownUnitIds,
                other.WithoutDeclaration.UnitProjection.KnownUnitIds);
            Assert.Equal(method.WithoutDeclaration.PublicMethod.OuterCandidateRegionIds,
                method.WithDeclaration.PublicMethod.OuterCandidateRegionIds);
            var region = UnitA(tlhUnits, method.Method);
            Assert.Equal("unknown", region.CompatibilityStatus);
            Assert.Empty(region.DirectWitnesses);
            Assert.Contains("formal-two-hop-public-alternative",
                region.UnwitnessedReasonCodes);
            Assert.Contains("published-pair-too-long",
                region.UnwitnessedReasonCodes);
            Assert.Contains("formal-two-hop-public-alternative",
                UnitA(noDirectUnits, method.Method).UnwitnessedReasonCodes);
        }
        var (reversedU, reversedK) = Inputs(tlhWorld, 4,
            reversed: true, duplicate: true);
        Assert.Equal(JsonDefaults.Serialize(tlh),
            JsonDefaults.Serialize(Questions(reversedU)));
        Assert.Equal(JsonDefaults.Serialize(tlhUnits),
            JsonDefaults.Serialize(DeclaredUnitCoverage(reversedU, reversedK)));

        // N11H: the direct 3-tick branch arrives before D@6 (dwell
        // unverified), while a published 2+2-tick route fits exactly.
        var n11World = original with
        {
            PublicHazardEnvelope = original.PublicHazardEnvelope! with
            {
                Branches = [.. original.PublicHazardEnvelope!.Branches,
                    Branch("A", "E") with
                    {
                        MinimumTravelTicks = 2, MaximumTravelTicks = 2,
                        ProvenanceId = "AR22-N11-A-E"
                    },
                    Branch("E", "D") with
                    {
                        MinimumTravelTicks = 2, MaximumTravelTicks = 2,
                        ProvenanceId = "AR22-N11-E-D"
                    }]
            }
        };
        var (n11U, n11K) = Inputs(n11World, 6);
        var n11 = Candidate(Questions(n11U));
        Assert.Empty(n11.Questions);
        Assert.Contains("formal-two-hop:A>E>D",
            n11.PublicAlternativeExplanations);
        Assert.Contains(n11.PublicAlternativeExplanations, explanation =>
            explanation.StartsWith(
                "direct-pair-published-shorter-dwell-unverified:A>D:",
                StringComparison.Ordinal));
        var n11Units = DeclaredUnitCoverage(n11U, n11K);
        Assert.All(n11Units.Methods, method =>
        {
            var region = UnitA(n11Units, method.Method);
            Assert.Equal("unknown", region.CompatibilityStatus);
            Assert.Empty(region.DirectWitnesses);
            Assert.Contains("formal-two-hop-public-alternative",
                region.UnwitnessedReasonCodes);
            Assert.Contains("published-pair-shorter-dwell-unverified",
                region.UnwitnessedReasonCodes);
            Assert.Equal(method.WithoutDeclaration.PublicMethod.OuterCandidateRegionIds,
                method.WithDeclaration.PublicMethod.OuterCandidateRegionIds);
        });
    }

    [Fact]
    public void DeclaredWitnessUnitsPreserveFuturePartialAndManyToManyMapping()
    {
        var (u, k) = RelationUtilityInputs();
        var partialMap = new PublicSearchUnitMapping[]
        {
            new("A", 2, ["U1"], true, "source-a", 0),
            new("B", 2, ["U2"], false, "source-b", 0),
            new("C", 2, ["U1"], true, "source-c", 8)
        };
        var partial = DeclaredUnitCoverage(u, k, partialMap);
        Assert.Equal("partial", partial.Status);
        Assert.All(partial.Methods, method =>
        {
            foreach (var condition in new[]
                     { method.WithoutDeclaration, method.WithDeclaration })
            {
                Assert.Equal("partial", condition.Status);
                Assert.Equal(["B", "C"], condition.UnitProjection.UnresolvedRegionIds);
                Assert.Contains(condition.UnresolvedRegions,
                    region => region.RegionId == "B" &&
                        region.Reason == "incomplete-public-mapping");
                Assert.Contains(condition.UnresolvedRegions,
                    region => region.RegionId == "C" &&
                        region.Reason == "future-public-mapping");
                Assert.Equal(["A"], condition.Units.Single(unit =>
                    unit.UnitId == "U1").Regions.Select(region => region.RegionId));
            }
        });

        var many = DeclaredUnitCoverage(u, k, [
            new("A", 2, ["U1", "U2"], true, "source-a", 0),
            new("B", 2, ["U2"], true, "source-b", 0),
            new("C", 2, ["U1"], true, "source-c", 0)
        ]);
        Assert.Equal("available", many.Status);
        Assert.All(many.Methods, method =>
        {
            var u2 = method.WithDeclaration.Units.Single(unit => unit.UnitId == "U2");
            Assert.Equal(["A", "B"], u2.Regions.Select(region => region.RegionId));
            Assert.Equal(2, u2.WitnessedRegionIds.Count);
            Assert.Equal(2, method.WithDeclaration.Units.Count);
        });
        var missing = DeclaredUnitCoverage(u, k, [
            new("A", 2, ["U1"], true, "source-a", 0)
        ]);
        Assert.Equal("partial", missing.Status);
        Assert.All(missing.Methods, method =>
            Assert.Contains(method.WithDeclaration.UnresolvedRegions,
                region => region.RegionId == "B" &&
                    region.Reason == "missing-public-mapping"));
        Assert.Throws<InvalidDataException>(() => DeclaredUnitCoverage(u, k, [
            new("A", 2, ["U1", "U1"], true, "invalid", 0)
        ]));
    }

    [Fact]
    public void RelationQuestionUtilitySeparatesAnswerFromActualSearchUnitChange()
    {
        var (u, k) = RelationUtilityInputs();
        var beforeJson = JsonDefaults.Serialize(u);
        var afterJson = JsonDefaults.Serialize(k);
        var result = RelationUtility(u, k);
        Assert.Equal("available", result.Status);
        Assert.Equal(7, result.QueryTick);
        Assert.Equal(2, result.HistoricalTick);
        Assert.Equal([RecordId, "second-original"], result.ReceivedOriginalIds);
        Assert.Contains(result.QuestionsWithoutDeclaration, question =>
            question.SourceRegionId == "B" &&
            question.DestinationRegionId == "D" &&
            question.IndependentSourceAncestry);
        Assert.Contains(result.QuestionsWithoutDeclaration, question =>
            question.SourceRegionId == "C" && question.DestinationRegionId == "D");
        Assert.DoesNotContain(result.QuestionsWithDeclaration, question =>
            question.SourceRegionId == "B" && question.DestinationRegionId == "D");
        Assert.Contains(result.QuestionsWithDeclaration, question =>
            question.SourceRegionId == "C" && question.DestinationRegionId == "D");
        Assert.Single(result.ResolvedQuestionIds);
        Assert.Empty(result.NewlyUnconfirmedQuestionIds);
        Assert.Equal(4, result.Methods.Count);
        // Independent fixture probe: U and K inspect 0/5/1/5 existing
        // public relations respectively; an added declaration is not an
        // additional measured historical-outer reduction.
        var expectedInspections = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["public-prior"] = 0,
            ["known-relation-backward"] = 5,
            ["distributed-reconstruction"] = 1,
            ["distributed-reconstruction-local"] = 5
        };
        Assert.All(result.Methods, method =>
        {
            Assert.Equal("available", method.UnitDifferenceStatus);
            Assert.Empty(method.ExcludedOuterRegionIds);
            Assert.Empty(method.ReappearedOuterRegionIds);
            Assert.Empty(method.RemovedKnownUnitIds!);
            Assert.Empty(method.AddedKnownUnitIds!);
            Assert.Equal(["U1", "U2"], method.UnitsWithoutDeclaration.KnownUnitIds);
            Assert.Equal(["U1", "U2"], method.UnitsWithDeclaration.KnownUnitIds);
            Assert.Equal(["A", "B", "C"], method.WithoutDeclaration.OuterCandidateRegionIds);
            Assert.Equal(["A", "B", "C"], method.WithDeclaration.OuterCandidateRegionIds);
            Assert.Null(method.WithoutDeclaration.PreferredCandidateRegionIds);
            Assert.Null(method.WithDeclaration.PreferredCandidateRegionIds);
            Assert.Empty(method.UnitsWithoutDeclaration.UnresolvedRegionIds);
            Assert.Empty(method.UnitsWithDeclaration.UnresolvedRegionIds);
            Assert.Equal(expectedInspections[method.Method],
                method.WithoutDeclaration.PublicRelationsInspected);
            Assert.Equal(expectedInspections[method.Method],
                method.WithDeclaration.PublicRelationsInspected);
            Assert.Equal(method.WithoutDeclaration.Steps, method.WithDeclaration.Steps);
            Assert.Equal(2, method.WithoutDeclaration.AvailableSourceGroupCount);
            Assert.Equal(2, method.WithDeclaration.AvailableSourceGroupCount);
            Assert.All(method.WithoutDeclaration.OuterCandidateRegionIds,
                id => Assert.Contains(id, method.WithDeclaration.OuterCandidateRegionIds));
        });
        // Truth-free evaluation leaves both input snapshots unchanged.
        Assert.Equal(beforeJson, JsonDefaults.Serialize(u));
        Assert.Equal(afterJson, JsonDefaults.Serialize(k));
        var (uReversed, kReversed) = RelationUtilityInputs(reversed: true,
            duplicateReceipt: true);
        Assert.Equal(JsonDefaults.Serialize(result),
            JsonDefaults.Serialize(RelationUtility(uReversed, kReversed)));
    }

    [Fact]
    public void RelationQuestionUtilityRetainsUnavailablePartialAndNonindependentOutcomes()
    {
        var (u, k) = RelationUtilityInputs(receiveSecond: false);
        var unavailable = RelationUtility(u, k);
        Assert.Equal("unavailable", unavailable.Status);
        Assert.Empty(unavailable.Methods);
        Assert.Empty(unavailable.ResolvedQuestionIds);

        var (fullU, fullK) = RelationUtilityInputs();
        var partial = RelationUtility(fullU, fullK,
            [new PublicSearchUnitMapping("A", 2, ["U1"], true, "only-a", 0)]);
        Assert.Equal("partial", partial.Status);
        Assert.All(partial.Methods, method =>
        {
            Assert.Equal("partial", method.UnitDifferenceStatus);
            Assert.Null(method.RemovedKnownUnitIds);
            Assert.Null(method.AddedKnownUnitIds);
            Assert.NotEmpty(method.UnitsWithoutDeclaration.UnresolvedRegionIds);
        });
        var (sharedU, sharedK) = RelationUtilityInputs(laterAncestry: "weft-a");
        var shared = RelationUtility(sharedU, sharedK);
        Assert.All(shared.QuestionsWithoutDeclaration,
            question => Assert.False(question.IndependentSourceAncestry));
        Assert.All(shared.Methods, method =>
            Assert.Equal(1, method.WithoutDeclaration.AvailableSourceGroupCount));
    }

    [Fact]
    public void RelationQuestionUtilityRejectsModelAndEvidenceDrift()
    {
        var (u, k) = RelationUtilityInputs();
        var tamperedModel = k.Input.PublicContext with
        {
            PublicCausalTracingModel = new PublicCausalTracingModel
            {
                CandidateStates = [State(2, ["A", "B"]), State(5, ["D", "E", "F"])]
            }
        };
        var other = new RescuerEvidenceInputBuilder().BuildDistinctOriginals(
            tamperedModel, Seed, 7, RescuerId,
            [
                ComplementaryOriginal(RecordId, HolderId, 2,
                    ["A", "B", "C"], 3, "weft-a"),
                ComplementaryOriginal("second-original", "holder-b", 5,
                    ["D"], 6, "weft-b")
            ], [Receipt(6), LaterComparisonReceipt(7)]);
        Assert.Throws<InvalidDataException>(() => RelationUtility(u, other));
        Assert.Throws<InvalidDataException>(() => RelationUtility(k, u));
        var (deviceU, deviceK) = RelationUtilityInputs(deviceOnly: true);
        var device = RelationUtility(deviceU, deviceK);
        Assert.DoesNotContain(device.QuestionsWithoutDeclaration,
            question => question.EarlyDirectSourceId == RecordId);
        var (conflictU, conflictK) = RelationUtilityInputs(
            firstState: EvidenceState.Conflicting);
        var conflict = RelationUtility(conflictU, conflictK);
        Assert.Empty(conflict.ResolvedQuestionIds);
    }

    [Fact]
    public void RelationQuestionUtilityRejectsSearchDomainAndReceiptDrift()
    {
        var (u, k) = RelationUtilityInputs();
        var originals = new[]
        {
            ComplementaryOriginal(RecordId, HolderId, 2,
                ["A", "B", "C"], 3, "weft-a"),
            ComplementaryOriginal("second-original", "holder-b", 5,
                ["D"], 6, "weft-b")
        };
        var builder = new RescuerEvidenceInputBuilder();
        var restricted = builder.BuildDistinctOriginals(k.Input.PublicContext,
            Seed, 7, RescuerId, originals, [Receipt(6), LaterComparisonReceipt(7)],
            publicSearchDomainRegionIds: ["A", "B"]);
        Assert.Throws<InvalidDataException>(() => RelationUtility(u, restricted));

        var receivedOne = builder.BuildDistinctOriginals(k.Input.PublicContext,
            Seed, 7, RescuerId, originals, [Receipt(6)]);
        Assert.Throws<InvalidDataException>(() => RelationUtility(u, receivedOne));

        var early = builder.BuildDistinctOriginals(u.Input.PublicContext,
            Seed, 6, RescuerId, originals, [Receipt(6)]);
        Assert.Throws<InvalidDataException>(() => RelationUtility(early, k));
    }

    [Fact]
    public void RelationQuestionUtilityDocumentsCurrentBranchInsensitiveOuters()
    {
        // Three PUBLIC envelope branch sets with fixed source evidence/query.
        // This is a CURRENT method limitation, not measured rescue utility.
        var template = ComplementaryPublicContext();
        var complete = new[] { "A", "B", "C" }
            .SelectMany(source => new[] { "D", "E", "F" }
                .Select(destination => Branch(source, destination))).ToArray();
        var branchSets = new IReadOnlyList<PublicHazardBranch>[]
        {
            Array.Empty<PublicHazardBranch>(),
            template.PublicHazardEnvelope!.Branches,
            complete
        };
        var originals = new[]
        {
            ComplementaryOriginal(RecordId, HolderId, 2,
                ["A", "B", "C"], 3, "weft-a"),
            ComplementaryOriginal("second-original", "holder-b", 5,
                ["D"], 6, "weft-b")
        };
        var builder = new RescuerEvidenceInputBuilder();
        var conditions = branchSets.Select(branches =>
        {
            var world = template with
            {
                PublicHazardEnvelope = template.PublicHazardEnvelope! with
                {
                    Branches = branches
                }
            };
            var input = builder.BuildDistinctOriginals(world, Seed, 7, RescuerId,
                originals, [Receipt(6), LaterComparisonReceipt(7)]);
            return RescuerEvidenceMethodComparison.Compare(
                input, InteractionTimeWindow.At(2));
        }).ToArray();
        foreach (var condition in conditions)
        {
            Assert.Equal(4, condition.Methods.Count);
            Assert.All(condition.Methods, method =>
            {
                Assert.Equal(["A", "B", "C"], method.OuterCandidateRegionIds);
                Assert.Null(method.PreferredCandidateRegionIds);
            });
            Assert.Equal(JsonDefaults.Serialize(conditions[0].Methods.Select(
                method => new
                {
                    method.Method,
                    method.OuterCandidateRegionIds,
                    method.PreferredCandidateRegionIds
                })),
                JsonDefaults.Serialize(condition.Methods.Select(
                    method => new
                    {
                        method.Method,
                        method.OuterCandidateRegionIds,
                        method.PreferredCandidateRegionIds
                    })));
        }
    }

    [Fact]
    public void ReceiptGatesInputsAndConnectsExistingCandidateAndTracingImplementations()
    {
        var record = Record();
        var before = Build(record, [Receipt(5)], currentTick: 4);
        var notReceived = Build(record, [], currentTick: 5);
        AssertNoReceivedEvidence(before);
        AssertNoReceivedEvidence(notReceived);
        Assert.DoesNotContain(record.Observation.ObservationId, JsonDefaults.Serialize(before));
        Assert.DoesNotContain(RecordId, JsonDefaults.Serialize(before));

        var received = Build(record, [Receipt(5)], currentTick: 5);
        Assert.Single(received.ReceivedRecords);
        Assert.Single(received.Input.AvailableCrossThreadConstraints!.Constraints);
        Assert.Single(received.Input.AvailableCausalTracingConstraints!.Constraints);
        Assert.Empty(received.Input.AvailableEvidence.Observations);

        var baseline = new MultiWeftTracingStrategy().Evaluate(notReceived.Input);
        var result = new MultiWeftTracingStrategy().Evaluate(received.Input);
        Assert.Equal(["C", "D"], baseline.CandidateRegionIds);
        // Received evidence may support preferred C, but unpublished
        // alternatives cannot be hard-excluded from the outer set.
        Assert.Equal(["C", "D"], result.CandidateRegionIds);
        Assert.Equal(["C"], result.PreferredCandidateRegionIds);
        Assert.Single(result.CausalTracingResult!.SourceGroups);
        Assert.Contains(result.CausalTracingResult.Steps, step =>
            step.Direction == CausalTracingDirection.CrossThread &&
            step.Disposition == TracingStepDisposition.Applied);
        Assert.Contains(result.CausalTracingResult.Steps, step =>
            step.Direction == CausalTracingDirection.Forward);

        // Exercise the existing historical applicator without inventing the own
        // point-position evidence required by CrossThreadConstrainedStrategy.
        var context = Context();
        var historical = PublicCandidateWarpFactory.Create(
            ScenarioId, Seed, TargetId,
            context.PublicCausalTracingModel!.CandidateStates.Take(1),
            CandidateWarpStanding.Preferred);
        var applied = new CrossThreadConstraintApplicator().Apply(
            historical,
            received.Input.AvailableCrossThreadConstraints.Constraints,
            new CandidateWarpAccessContext
            {
                ScenarioId = ScenarioId,
                Seed = Seed,
                TargetEntityId = TargetId,
                CurrentTick = 5,
                QueryContextId = "receipt-candidate-check"
            },
            context.PublicHazardEnvelope);
        Assert.Equal(["C"], applied.CandidateWarps.PreferredRegionIdsAt(5));
    }

    [Fact]
    public void ReceiptPreservesOriginalIdentityAndAncestryWhileDelayingAccess()
    {
        var record = Record();
        var originalJson = JsonDefaults.Serialize(record.Observation);
        var snapshot = Build(record, [Receipt(5)]);
        var received = Assert.Single(snapshot.ReceivedRecords);
        var cross = Assert.Single(snapshot.Input.AvailableCrossThreadConstraints!.Constraints);
        var tracing = Assert.Single(snapshot.Input.AvailableCausalTracingConstraints!.Constraints);

        Assert.Same(record.Observation, received.OriginalObservation);
        Assert.Equal(record.Observation.ObservationId, received.OriginalObservationId);
        Assert.Equal(RecordId, received.OriginalRecordId);
        Assert.Equal(5, received.FirstReceivedAtTick);
        Assert.Equal(3, record.Observation.AvailableAtTick);
        Assert.Equal(InteractionVisibility.PrivateEvidence, record.Observation.Visibility);
        Assert.Equal(originalJson, JsonDefaults.Serialize(record.Observation));
        Assert.Equal(record.Observation.ObservationId, cross.SourceObservationId);
        Assert.Equal(record.Observation.ObservationId, tracing.SourceObservationId);
        Assert.Equal(RecordId, cross.SourceEvidenceIdentifier);
        Assert.Equal(RecordId, tracing.DirectSourceId);
        Assert.Equal(5, cross.AvailableAtTick);
        Assert.Equal(5, tracing.AvailableAtTick);
        Assert.Equal(InteractionVisibility.PublicEvidence, cross.Visibility);
        Assert.Contains(RecordId, tracing.SourceAncestryIds);
        Assert.Contains(record.Observation.ObservationId, tracing.SourceAncestryIds);
        Assert.Contains("upstream-evidence", tracing.SourceAncestryIds);
        Assert.DoesNotContain(HolderId, tracing.SourceAncestryIds);
        Assert.DoesNotContain(RescuerId, tracing.SourceAncestryIds);
        Assert.DoesNotContain(CrossThreadConstraintDeriver.PersonEncounterCoLocationRule, tracing.SourceAncestryIds);
        Assert.Empty(snapshot.Input.AvailableCrossThreadConstraints.PubliclyAvailableAt(4).Constraints);
        Assert.Empty(snapshot.Input.AvailableCausalTracingConstraints.PubliclyAvailableAt(4).Constraints);
    }

    [Fact]
    public void RedeliveryAndReceiptOrderingDoNotIncreaseEvidenceOrIndependentSupport()
    {
        var record = Record();
        var firstOnly = Build(record, [Receipt(5)], currentTick: 10);
        var deliveries = new[] { Receipt(9), Receipt(5), Receipt(7), Receipt(5) };
        var repeated = Build(record, deliveries, currentTick: 10);
        var reversed = Build(record, deliveries.Reverse(), currentTick: 10);

        Assert.Equal(JsonDefaults.Serialize(firstOnly), JsonDefaults.Serialize(repeated));
        Assert.Equal(JsonDefaults.Serialize(firstOnly), JsonDefaults.Serialize(reversed));
        var expected = new MultiWeftTracingStrategy().Evaluate(firstOnly.Input);
        var actual = new MultiWeftTracingStrategy().Evaluate(repeated.Input);
        Assert.Equal(JsonDefaults.Serialize(expected), JsonDefaults.Serialize(actual));
        Assert.Single(actual.CausalTracingResult!.SourceGroups);
        Assert.Single(actual.CausalTracingResult.SourceGroups[0].SourceObservationIds);
        Assert.Single(actual.CausalTracingResult.SourceGroups[0].ConstraintIds);
    }

    [Fact]
    public void FutureReceiptsDoNotAffectEarlierQueriesOrCausePrematureValidationFailure()
    {
        var record = Record(availableAtTick: 20, storedAtTick: 20);
        var withoutReceipt = Build(record, [], currentTick: 5);
        // This receipt is temporally invalid, but it is not yet in the query's ledger view.
        var earlier = Build(record, [Receipt(10)], currentTick: 5);
        AssertNoReceivedEvidence(earlier);
        Assert.Equal(JsonDefaults.Serialize(withoutReceipt), JsonDefaults.Serialize(earlier));
        Assert.Throws<ArgumentException>(() => Build(record, [Receipt(10)], currentTick: 10));
    }

    [Theory]
    [InlineData("scenario")]
    [InlineData("seed")]
    [InlineData("record")]
    [InlineData("holder")]
    [InlineData("rescuer")]
    public void UnrelatedReceiptsDoNotGrantAccess(string mismatch)
    {
        var receipt = new InteractionRecordReceipt(
            mismatch == "scenario" ? "another-scenario" : ScenarioId,
            mismatch == "seed" ? Seed + 1 : Seed,
            mismatch == "record" ? "another-record" : RecordId,
            mismatch == "holder" ? "another-device" : HolderId,
            mismatch == "rescuer" ? "rescuer-b" : RescuerId,
            5);
        var snapshot = Build(Record(), [receipt]);
        AssertNoReceivedEvidence(snapshot);
        Assert.Equal(["C", "D"], new MultiWeftTracingStrategy().Evaluate(snapshot.Input).CandidateRegionIds);
    }

    [Theory]
    [InlineData(3, 6, 5)]
    [InlineData(6, 6, 5)]
    [InlineData(2, 2, 1)]
    public void MatchingReceiptCannotPrecedeStorageAvailabilityOrObservation(
        long available, long stored, long received)
    {
        var record = Record(availableAtTick: available, storedAtTick: stored);
        Assert.Throws<ArgumentException>(() => Build(record, [Receipt(received)], currentTick: 10));
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(1, 2)]
    public void ReceiptKeepsExactAndIntervalObservationSemantics(long start, long end)
    {
        var record = Record(window: new InteractionTimeWindow(start, end));
        var snapshot = Build(record, [Receipt(5)]);
        var cross = Assert.Single(snapshot.Input.AvailableCrossThreadConstraints!.Constraints);
        var tracing = Assert.Single(snapshot.Input.AvailableCausalTracingConstraints!.Constraints);
        Assert.Equal(record.Observation.ObservedTimeWindow, cross.SourceObservedTimeWindow);
        Assert.Equal(record.Observation.ObservedTimeWindow, cross.ConstrainedTimeWindow);
        Assert.Equal(record.Observation.ObservedTimeWindow, tracing.SourceObservedTimeWindow);
        Assert.Equal(record.Observation.ObservedTimeWindow, tracing.ConstrainedTimeWindow);
        Assert.Equal(record.Observation.ObservedRelationKind, cross.SourceRelationKind);
        Assert.Equal(record.Observation.ObservedParticipants, tracing.Participants);
    }

    [Theory]
    [InlineData(EvidenceState.SupportedButUncertain)]
    [InlineData(EvidenceState.Conflicting)]
    public void ReceiptDoesNotStrengthenUncertaintyOrDiscardConflictingAlternatives(EvidenceState state)
    {
        var snapshot = Build(Record(state: state), [Receipt(5)]);
        Assert.Equal(state, snapshot.Input.AvailableCrossThreadConstraints!.Constraints[0].EvidenceBoundaryState);
        Assert.Equal(state, snapshot.Input.AvailableCausalTracingConstraints!.Constraints[0].EvidenceBoundaryState);
        var result = new MultiWeftTracingStrategy().Evaluate(snapshot.Input);
        Assert.Equal(["C", "D"], result.OuterPlausibleCandidateRegionIds);
        if (state == EvidenceState.SupportedButUncertain)
        {
            Assert.Equal(["C"], result.PreferredCandidateRegionIds);
        }
        else
        {
            Assert.Contains(result.CausalTracingResult!.Steps, step =>
                step.Disposition == TracingStepDisposition.MarkConflict && step.ConflictDetected);
        }
    }

    [Fact]
    public void ReceivedDeviceOnlyRecordDoesNotBecomePersonEvidence()
    {
        var snapshot = Build(Record(deviceOnly: true), [Receipt(5)]);
        var received = Assert.Single(snapshot.ReceivedRecords);
        Assert.True(received.OriginalObservation.IsDeviceOnlyEvidence);
        Assert.False(received.OriginalObservation.IsPersonEvidence);
        Assert.Empty(snapshot.Input.AvailableEvidence.Observations);
        Assert.Empty(snapshot.Input.AvailableCrossThreadConstraints!.Constraints);
        Assert.Empty(snapshot.Input.AvailableCausalTracingConstraints!.Constraints);
        var result = new MultiWeftTracingStrategy().Evaluate(snapshot.Input);
        Assert.Equal(["C", "D"], result.CandidateRegionIds);
        Assert.Empty(result.CausalTracingResult!.SourceGroups);
    }

    [Fact]
    public void HoldingContractRejectsPublicOriginalTruthProvenanceAndPrematureStorage()
    {
        Assert.Throws<ArgumentException>(() => new DeviceHeldInteractionRecord(
            HolderId, 3, Observation(visibility: InteractionVisibility.PublicEvidence)));
        Assert.Throws<ArgumentException>(() => new DeviceHeldInteractionRecord(
            HolderId, 3, Observation(provenanceKind: InteractionProvenanceKind.WorldEvent)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DeviceHeldInteractionRecord(
            HolderId, 2, Observation()));
    }

    [Fact]
    public void QueryCannotMixRunsOrTreatAnUndeclaredHolderAsADevice()
    {
        var builder = new RescuerEvidenceInputBuilder();
        var record = Record();
        Assert.Throws<ArgumentException>(() => builder.Build(
            Context() with { ScenarioId = "another-scenario" }, Seed, 5, RescuerId, record, [Receipt(5)]));
        Assert.Throws<ArgumentException>(() => builder.Build(
            Context(), Seed + 1, 5, RescuerId, record, [Receipt(5)]));
        Assert.Throws<ArgumentException>(() => builder.Build(
            Context() with { PublicDeviceIds = [] }, Seed, 5, RescuerId, record, [Receipt(5)]));
    }


    [Fact]
    public void MultipleHoldersGrantOneOriginalOnlyAfterFirstReceipt()
    {
        var heldA = Record();
        var heldB = new DeviceHeldInteractionRecord("holder-b", 4, heldA.Observation);
        var holders = new[] { heldA, heldB };
        var receiptB = HolderReceipt("holder-b", 5);
        var receiptA = HolderReceipt(HolderId, 7);
        var before = BuildMulti(holders, [receiptB, receiptA], 4);
        AssertNoReceivedEvidence(before);

        var publicOnly = BuildMulti(holders, [], 5);
        AssertNoReceivedEvidence(publicOnly);
        Assert.Equal(["C", "D"], new MultiWeftTracingStrategy().Evaluate(publicOnly.Input).CandidateRegionIds);

        var first = BuildMulti(holders, [receiptB, receiptA], 5);
        Assert.Equal("holder-b", Assert.Single(first.ReceivedRecords).HolderDeviceId);
        Assert.Equal(5, first.ReceivedRecords[0].FirstReceivedAtTick);
        var cross = Assert.Single(first.Input.AvailableCrossThreadConstraints!.Constraints);
        var tracing = Assert.Single(first.Input.AvailableCausalTracingConstraints!.Constraints);
        Assert.Equal(heldA.Observation.ObservationId, cross.SourceObservationId);
        Assert.Equal(RecordId, tracing.DirectSourceId);
        Assert.DoesNotContain("holder-b", tracing.SourceAncestryIds);
        Assert.DoesNotContain(HolderId, tracing.SourceAncestryIds);
        Assert.DoesNotContain(RescuerId, tracing.SourceAncestryIds);
        Assert.Empty(first.Input.AvailableEvidence.Observations);
        var constrained = new MultiWeftTracingStrategy().Evaluate(first.Input);
        Assert.Equal(["C", "D"], constrained.CandidateRegionIds);
        Assert.Single(constrained.CausalTracingResult!.SourceGroups);
        Assert.Single(constrained.CausalTracingResult.SourceGroups[0].SourceObservationIds);

        // Compare at the same query tick: B alone versus B and later A.
        var oneReceipt = BuildMulti(holders, [receiptB], 7);
        var repeated = BuildMulti(holders, [receiptB, receiptA, receiptA], 7);
        Assert.Equal(JsonDefaults.Serialize(oneReceipt), JsonDefaults.Serialize(repeated));
        Assert.Equal(
            JsonDefaults.Serialize(new MultiWeftTracingStrategy().Evaluate(oneReceipt.Input)),
            JsonDefaults.Serialize(new MultiWeftTracingStrategy().Evaluate(repeated.Input)));
    }

    [Fact]
    public void MultipleHoldersAreDeterministicForEqualReceiptTimesAndReorderedInputs()
    {
        var a = Record();
        var b = new DeviceHeldInteractionRecord("holder-b", 3, a.Observation);
        var receiptA = HolderReceipt(HolderId, 5);
        var receiptB = HolderReceipt("holder-b", 5);
        var direct = BuildMulti([a, b], [receiptB, receiptA], 5);
        var reversed = BuildMulti([b, a, a], [receiptA, receiptB, receiptB], 5);
        Assert.Equal(HolderId, Assert.Single(direct.ReceivedRecords).HolderDeviceId);
        Assert.Equal(JsonDefaults.Serialize(direct), JsonDefaults.Serialize(reversed));
        Assert.Equal(
            JsonDefaults.Serialize(new MultiWeftTracingStrategy().Evaluate(direct.Input)),
            JsonDefaults.Serialize(new MultiWeftTracingStrategy().Evaluate(reversed.Input)));
    }

    [Fact]
    public void MultipleHoldersRejectInconsistentOriginalsStorageAndRuns()
    {
        var original = Record();
        var anotherCopy = new DeviceHeldInteractionRecord(
            "holder-b", 3, Observation(state: EvidenceState.Conflicting));
        Assert.Throws<ArgumentException>(() => BuildMulti([original, anotherCopy], [], 5));
        Assert.Throws<ArgumentException>(() => BuildMulti(
            [original, new DeviceHeldInteractionRecord(HolderId, 4, original.Observation)], [], 5));
        Assert.Throws<ArgumentException>(() => BuildMulti([], [], 5));

        var otherHolder = new DeviceHeldInteractionRecord("holder-b", 3, original.Observation);
        var context = MultiHolderContext();
        var builder = new RescuerEvidenceInputBuilder();
        Assert.Throws<ArgumentException>(() => builder.Build(
            context with { ScenarioId = "another-run" }, Seed, 5, RescuerId,
            new[] { original, otherHolder }, Array.Empty<InteractionRecordReceipt>()));
        Assert.Throws<ArgumentException>(() => builder.Build(
            context, Seed + 1, 5, RescuerId,
            new[] { original, otherHolder }, Array.Empty<InteractionRecordReceipt>()));
        Assert.Throws<ArgumentException>(() => builder.Build(
            context with { PublicDeviceIds = [HolderId] }, Seed, 5, RescuerId,
            new[] { original, otherHolder }, Array.Empty<InteractionRecordReceipt>()));
    }

    [Fact]
    public void InvalidMatchingReceiptCannotHideBehindValidReceiptFromAnotherHolder()
    {
        var lateHolder = Record(storedAtTick: 6);
        var otherHolder = new DeviceHeldInteractionRecord("holder-b", 3, lateHolder.Observation);
        Assert.Throws<ArgumentException>(() => BuildMulti(
            [lateHolder, otherHolder],
            [HolderReceipt("holder-b", 5), HolderReceipt(HolderId, 5)], 5));
        AssertNoReceivedEvidence(BuildMulti(
            [lateHolder, otherHolder], [HolderReceipt(HolderId, 5)], 4));
    }

    [Fact]
    public void UnrelatedAndFutureMultiHolderReceiptsDoNotGrantOrChangeCurrentAccess()
    {
        var a = Record();
        var b = new DeviceHeldInteractionRecord("holder-b", 3, a.Observation);
        var valid = HolderReceipt("holder-b", 5);
        var foreign = new InteractionRecordReceipt(ScenarioId, Seed, RecordId,
            "unlisted-holder", RescuerId, 4);
        var otherRescuer = new InteractionRecordReceipt(ScenarioId, Seed, RecordId,
            HolderId, "someone-else", 4);
        var otherRun = new InteractionRecordReceipt(ScenarioId, Seed + 1, RecordId,
            HolderId, RescuerId, 4);
        var future = HolderReceipt(HolderId, 10);
        var baseline = BuildMulti([a, b], [valid], 5);
        var mixed = BuildMulti([a, b], [foreign, otherRescuer, otherRun, future, valid], 5);
        Assert.Equal(JsonDefaults.Serialize(baseline), JsonDefaults.Serialize(mixed));
        AssertNoReceivedEvidence(BuildMulti(
            [a, b], [foreign, otherRescuer, otherRun, future], 5));
    }

    [Theory]
    [InlineData(EvidenceState.SupportedButUncertain)]
    [InlineData(EvidenceState.Conflicting)]
    public void MultipleHoldersPreserveUncertainAndConflictingAlternatives(EvidenceState state)
    {
        var a = Record(state: state, window: new InteractionTimeWindow(1, 2));
        var b = new DeviceHeldInteractionRecord("holder-b", 3, a.Observation);
        var single = Build(a, [Receipt(5)], 5);
        var multi = BuildMulti([a, b], [HolderReceipt("holder-b", 5)], 5);
        // PublicDeviceIds legitimately differs because the multi-holder query
        // declares an additional device; compare evidence and actual inference.
        Assert.Equal(
            JsonDefaults.Serialize(single.Input.AvailableCrossThreadConstraints),
            JsonDefaults.Serialize(multi.Input.AvailableCrossThreadConstraints));
        Assert.Equal(
            JsonDefaults.Serialize(single.Input.AvailableCausalTracingConstraints),
            JsonDefaults.Serialize(multi.Input.AvailableCausalTracingConstraints));
        Assert.Equal(
            JsonDefaults.Serialize(new MultiWeftTracingStrategy().Evaluate(single.Input)),
            JsonDefaults.Serialize(new MultiWeftTracingStrategy().Evaluate(multi.Input)));
        var actual = new MultiWeftTracingStrategy().Evaluate(multi.Input);
        Assert.Equal(["C", "D"], actual.OuterPlausibleCandidateRegionIds);
        Assert.Equal(state,
            Assert.Single(multi.Input.AvailableCrossThreadConstraints!.Constraints).EvidenceBoundaryState);
    }

    [Fact]
    public void MultipleHoldersDoNotPromoteDeviceOnlyEvidenceToPersonEvidence()
    {
        var a = Record(deviceOnly: true);
        var b = new DeviceHeldInteractionRecord("holder-b", 3, a.Observation);
        var received = BuildMulti([a, b], [HolderReceipt("holder-b", 5)], 5);
        Assert.Single(received.ReceivedRecords);
        Assert.Empty(received.Input.AvailableCrossThreadConstraints!.Constraints);
        Assert.Empty(received.Input.AvailableCausalTracingConstraints!.Constraints);
        Assert.Equal(["C", "D"],
            new MultiWeftTracingStrategy().Evaluate(received.Input).CandidateRegionIds);
    }

    [Fact]
    public void DistinctOriginalsEnterOneInferenceOnlyWhenReceived()
    {
        var narrow = Record();
        var broad = DistinctRecord(["A", "B"]);
        var originals = new[] { narrow, broad };
        var first = DistinctReceipt(3);
        var second = Receipt(5);
        var before = BuildDistinct(originals, [first, second], 2);
        AssertNoReceivedEvidence(before);
        Assert.DoesNotContain(RecordId, JsonDefaults.Serialize(before));
        Assert.DoesNotContain(broad.OriginalRecordId, JsonDefaults.Serialize(before));

        var atFirstCutoff = BuildDistinct(originals, [first, second], 4);
        Assert.Equal(broad.OriginalRecordId, Assert.Single(atFirstCutoff.ReceivedRecords).OriginalRecordId);
        Assert.Single(atFirstCutoff.Input.AvailableCausalTracingConstraints!.Constraints);
        Assert.DoesNotContain(narrow.Observation.ObservationId, JsonDefaults.Serialize(atFirstCutoff));

        // Hold query time and public model fixed when testing information gain.
        var none = BuildDistinct(originals, [], 5);
        var onlyBroad = BuildDistinct(originals, [first], 5);
        var both = BuildDistinct(originals, [first, second], 5);
        Assert.Equal(2, both.ReceivedRecords.Count);
        Assert.Equal(2, both.Input.AvailableCrossThreadConstraints!.Constraints.Count);
        Assert.Equal(2, both.Input.AvailableCausalTracingConstraints!.Constraints.Count);
        Assert.Empty(both.Input.AvailableEvidence.Observations);
        Assert.Equal(["C", "D"], new MultiWeftTracingStrategy().Evaluate(none.Input).CandidateRegionIds);
        Assert.Equal(["C", "D"], new MultiWeftTracingStrategy().Evaluate(onlyBroad.Input).CandidateRegionIds);
        var result = new MultiWeftTracingStrategy().Evaluate(both.Input);
        // Received evidence may support preferred C, but unpublished
        // alternatives cannot be hard-excluded from the outer set.
        Assert.Equal(["C", "D"], result.CandidateRegionIds);
        Assert.Equal(["C"], result.PreferredCandidateRegionIds);
        Assert.Equal(2, result.CausalTracingResult!.SourceGroups.Count);
        Assert.Contains(result.CausalTracingResult.Steps, step =>
            step.Direction == CausalTracingDirection.Forward);
        Assert.All(both.Input.AvailableCausalTracingConstraints.Constraints, constraint =>
            Assert.Contains(result.CausalTracingResult.Steps, step =>
                step.ConstraintId == constraint.ConstraintId &&
                step.Direction == CausalTracingDirection.CrossThread &&
                step.Disposition == TracingStepDisposition.Applied));
    }

    [Fact]
    public void DistinctOriginalOrderingAndRedeliveryAreDeterministic()
    {
        var narrow = Record();
        var broad = DistinctRecord(["A", "B"]);
        var originals = new[] { narrow, broad };
        var receipts = new[] { DistinctReceipt(3), Receipt(5) };
        var expected = BuildDistinct(originals, receipts, 7);
        var actual = BuildDistinct(originals.Reverse(),
            new[] { Receipt(7), Receipt(5), DistinctReceipt(6), DistinctReceipt(3), Receipt(5) }, 7);
        Assert.Equal(JsonDefaults.Serialize(expected), JsonDefaults.Serialize(actual));
        Assert.Equal(
            JsonDefaults.Serialize(new MultiWeftTracingStrategy().Evaluate(expected.Input)),
            JsonDefaults.Serialize(new MultiWeftTracingStrategy().Evaluate(actual.Input)));
        Assert.Equal(new[] { 5L, 3L }, actual.ReceivedRecords.Select(record => record.FirstReceivedAtTick));

        var invertedArrival = BuildDistinct(originals, [Receipt(3), DistinctReceipt(5)], 5);
        var atSecond = new MultiWeftTracingStrategy().Evaluate(invertedArrival.Input);
        Assert.Equal(["C", "D"], atSecond.CandidateRegionIds);
        Assert.Equal(2, atSecond.CausalTracingResult!.SourceGroups.Count);
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    public void DistinctRootsPreserveOriginalsAndSharedAncestry(bool sharedAncestry, int expectedGroups)
    {
        var first = Record();
        var second = DistinctRecord(["A"], ancestry: sharedAncestry ? "upstream-evidence" : "other-upstream");
        var firstBytes = JsonDefaults.Serialize(first.Observation);
        var secondBytes = JsonDefaults.Serialize(second.Observation);
        var snapshot = BuildDistinct([first, second], [Receipt(5), DistinctReceipt(5)], 5);
        var trace = new MultiWeftTracingStrategy().Evaluate(snapshot.Input).CausalTracingResult!;
        Assert.Equal(expectedGroups, trace.SourceGroups.Count);
        Assert.Equal(2, trace.SourceGroups.Sum(group => group.SourceObservationIds.Count));
        Assert.Equal(2, trace.SourceGroups.Sum(group => group.ConstraintIds.Count));
        Assert.Same(first.Observation, snapshot.ReceivedRecords[0].OriginalObservation);
        Assert.Same(second.Observation, snapshot.ReceivedRecords[1].OriginalObservation);
        Assert.Equal(firstBytes, JsonDefaults.Serialize(first.Observation));
        Assert.Equal(secondBytes, JsonDefaults.Serialize(second.Observation));
        Assert.All(snapshot.ReceivedRecords, record =>
            Assert.Equal(InteractionVisibility.PrivateEvidence, record.OriginalObservation.Visibility));
        Assert.All(snapshot.Input.AvailableCausalTracingConstraints!.Constraints, constraint =>
        {
            Assert.DoesNotContain(HolderId, constraint.SourceAncestryIds);
            Assert.DoesNotContain("holder-b", constraint.SourceAncestryIds);
            Assert.DoesNotContain(RescuerId, constraint.SourceAncestryIds);
            Assert.Equal(InteractionTimeWindow.At(2), constraint.ConstrainedTimeWindow);
        });
    }

    [Theory]
    [InlineData(EvidenceState.SupportedButUncertain)]
    [InlineData(EvidenceState.Conflicting)]
    public void DistinctUncertainOrConflictingOriginalDoesNotHardExclude(EvidenceState state)
    {
        var broad = DistinctRecord(["A", "B"]);
        var narrow = Record(state: state, window: new InteractionTimeWindow(1, 2));
        var snapshot = BuildDistinct([narrow, broad], [Receipt(5), DistinctReceipt(3)], 5);
        var constraint = snapshot.Input.AvailableCausalTracingConstraints!.Constraints
            .Single(item => item.DirectSourceId == RecordId);
        Assert.Equal(state, constraint.EvidenceBoundaryState);
        Assert.Equal(new InteractionTimeWindow(1, 2), constraint.SourceObservedTimeWindow);
        Assert.Equal(new InteractionTimeWindow(1, 2), constraint.ConstrainedTimeWindow);
        var result = new MultiWeftTracingStrategy().Evaluate(snapshot.Input);
        Assert.Equal(["C", "D"], result.OuterPlausibleCandidateRegionIds);
        if (state == EvidenceState.SupportedButUncertain)
        {
            Assert.Equal(["C"], result.PreferredCandidateRegionIds);
        }
        else
        {
            Assert.Contains(result.CausalTracingResult!.Steps, step =>
                step.Disposition == TracingStepDisposition.MarkConflict && step.ConflictDetected);
        }
    }

    [Fact]
    public void ContradictoryConfirmedOriginalsRetainOuterAlternativesAndConflict()
    {
        var snapshot = BuildDistinct([Record(), DistinctRecord(["B"])],
            [Receipt(5), DistinctReceipt(5)], 5);
        var result = new MultiWeftTracingStrategy().Evaluate(snapshot.Input);
        Assert.Equal(["C", "D"], result.OuterPlausibleCandidateRegionIds);
        Assert.Equal(2, result.CausalTracingResult!.SourceGroups.Count);
        Assert.Contains(result.CausalTracingResult.Steps, step =>
            step.Disposition == TracingStepDisposition.MarkConflict && step.ConflictDetected);
    }

    [Fact]
    public void DistinctDeviceOnlyOriginalAddsNoPersonConstraint()
    {
        var snapshot = BuildDistinct([Record(deviceOnly: true), DistinctRecord(["A", "B"])],
            [Receipt(5), DistinctReceipt(3)], 5);
        Assert.Equal(2, snapshot.ReceivedRecords.Count);
        Assert.True(snapshot.ReceivedRecords[0].OriginalObservation.IsDeviceOnlyEvidence);
        Assert.Single(snapshot.Input.AvailableCrossThreadConstraints!.Constraints);
        Assert.Single(snapshot.Input.AvailableCausalTracingConstraints!.Constraints);
        var result = new MultiWeftTracingStrategy().Evaluate(snapshot.Input);
        Assert.Equal(["C", "D"], result.CandidateRegionIds);
        Assert.Single(result.CausalTracingResult!.SourceGroups);
    }

    [Theory]
    [InlineData("scenario")]
    [InlineData("seed")]
    [InlineData("record")]
    [InlineData("holder")]
    [InlineData("rescuer")]
    public void UnrelatedReceiptCannotAddSecondOriginal(string mismatch)
    {
        var records = new[] { Record(), DistinctRecord(["A", "B"]) };
        var unrelated = new InteractionRecordReceipt(
            mismatch == "scenario" ? "other-run" : ScenarioId,
            mismatch == "seed" ? Seed + 1 : Seed,
            mismatch == "record" ? "unknown-record" : RecordId,
            mismatch == "holder" ? "holder-b" : HolderId,
            mismatch == "rescuer" ? "other-rescuer" : RescuerId, 5);
        var expected = BuildDistinct(records, [DistinctReceipt(3)], 5);
        var actual = BuildDistinct(records, [DistinctReceipt(3), unrelated, Receipt(10)], 5);
        Assert.Equal(JsonDefaults.Serialize(expected), JsonDefaults.Serialize(actual));
        Assert.Equal(["C", "D"], new MultiWeftTracingStrategy().Evaluate(actual.Input).CandidateRegionIds);
    }

    [Theory]
    [InlineData(3, 6, 5)]
    [InlineData(6, 6, 5)]
    [InlineData(2, 2, 1)]
    public void InvalidMatchingReceiptCannotHideBehindAnotherValidOriginal(long available, long stored, long received)
    {
        var late = Record(availableAtTick: available, storedAtTick: stored);
        var other = DistinctRecord(["A", "B"]);
        Assert.Throws<ArgumentException>(() => BuildDistinct([late, other],
            [DistinctReceipt(3), Receipt(received)], 10));
        // A future invalid assertion is not consumed by the earlier query.
        var future = Record(availableAtTick: 20, storedAtTick: 20);
        var expected = BuildDistinct([future, other], [DistinctReceipt(3)], 5);
        var actual = BuildDistinct([future, other], [DistinctReceipt(3), Receipt(10)], 5);
        Assert.Equal(JsonDefaults.Serialize(expected), JsonDefaults.Serialize(actual));
    }

    [Fact]
    public void DistinctCompositionRejectsInvalidOriginalCollectionsAndNullReceipts()
    {
        var first = Record();
        var second = DistinctRecord(["A"]);
        Assert.Throws<ArgumentNullException>(() => BuildDistinct(null!, [], 5));
        Assert.Throws<ArgumentNullException>(() => BuildDistinct([first, second], null!, 5));
        Assert.Throws<ArgumentException>(() => BuildDistinct([], [], 5));
        Assert.Throws<ArgumentException>(() => BuildDistinct([first], [], 5));
        Assert.Throws<ArgumentException>(() => BuildDistinct([first, second, first], [], 5));
        Assert.Throws<ArgumentException>(() => BuildDistinct([first, null!], [], 5));
        Assert.Throws<ArgumentException>(() => BuildDistinct([first, first], [], 5));
        Assert.Throws<ArgumentException>(() => BuildDistinct(
            [first, new DeviceHeldInteractionRecord("holder-b", 3, first.Observation)], [], 5));
        Assert.Throws<ArgumentException>(() => BuildDistinct(
            [first, Record(state: EvidenceState.Conflicting)], [], 5));
        Assert.Throws<ArgumentException>(() => BuildDistinct([first, second], [null!], 5));
    }

    [Fact]
    public void DistinctCompositionValidatesEveryOriginalRunAndHolder()
    {
        var first = Record();
        var otherRun = DistinctRecord(["A"], seed: Seed + 1);
        Assert.Throws<ArgumentException>(() => BuildDistinct([first, otherRun], [Receipt(5)], 5));
        var undeclared = new DeviceHeldInteractionRecord("unlisted-holder", 3,
            DistinctRecord(["A"]).Observation);
        Assert.Throws<ArgumentException>(() => BuildDistinct([first, undeclared], [Receipt(5)], 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => BuildDistinct(
            [first, DistinctRecord(["A"])], [], -1));
    }

    private static RescuerEvidenceInput BuildDistinct(
        IEnumerable<DeviceHeldInteractionRecord> records,
        IEnumerable<InteractionRecordReceipt> receipts,
        long currentTick) => new RescuerEvidenceInputBuilder().BuildDistinctOriginals(
            MultiHolderContext(), Seed, currentTick, RescuerId, records, receipts);

    private static InteractionRecordReceipt DistinctReceipt(long tick) =>
        new(ScenarioId, Seed, "second-original", "holder-b", RescuerId, tick);

    private static DeviceHeldInteractionRecord DistinctRecord(
        IReadOnlyList<string> regions,
        string ancestry = "other-upstream",
        int seed = Seed) => new("holder-b", 3, InteractionObservation.Create(
            ScenarioId, seed, "second-original", null,
            InteractionRelationKind.PersonPersonEncounter,
            [
                new InteractionParticipant(TargetId, InteractionEntityKind.Person,
                    InteractionParticipantRole.EncounterParticipant),
                new InteractionParticipant("person-c", InteractionEntityKind.Person,
                    InteractionParticipantRole.EncounterParticipant)
            ],
            InteractionTimeWindow.At(2), regions, 3, EvidenceState.Confirmed,
            isPersonEvidence: true, isDeviceOnlyEvidence: false,
            InteractionVisibility.PrivateEvidence,
            [new InteractionProvenance(InteractionProvenanceKind.EvidenceRecord, ancestry)]));

    private static void AssertNoReceivedEvidence(RescuerEvidenceInput snapshot)
    {
        Assert.Empty(snapshot.ReceivedRecords);
        Assert.Empty(snapshot.Input.AvailableEvidence.Observations);
        Assert.Empty(snapshot.Input.AvailableCrossThreadConstraints!.Constraints);
        Assert.Empty(snapshot.Input.AvailableCausalTracingConstraints!.Constraints);
    }

    private static RescuerEvidenceInput Build(
        DeviceHeldInteractionRecord record,
        IEnumerable<InteractionRecordReceipt> receipts,
        long currentTick = 5) => new RescuerEvidenceInputBuilder().Build(
            Context(), Seed, currentTick, RescuerId, record, receipts);


    private static RescuerEvidenceInput BuildMulti(
        IEnumerable<DeviceHeldInteractionRecord> heldRecords,
        IEnumerable<InteractionRecordReceipt> receipts,
        long currentTick) => new RescuerEvidenceInputBuilder().Build(
            MultiHolderContext(), Seed, currentTick, RescuerId, heldRecords, receipts);

    private static RescueContext MultiHolderContext() =>
        Context() with { PublicDeviceIds = [HolderId, "holder-b"] };

    private static InteractionRecordReceipt HolderReceipt(string holderId, long tick) =>
        new(ScenarioId, Seed, RecordId, holderId, RescuerId, tick);

    private static InteractionRecordReceipt Receipt(long tick) =>
        new(ScenarioId, Seed, RecordId, HolderId, RescuerId, tick);

    private static DeviceHeldInteractionRecord Record(
        EvidenceState state = EvidenceState.Confirmed,
        bool deviceOnly = false,
        long availableAtTick = 3,
        long storedAtTick = 3,
        InteractionTimeWindow? window = null) => new(
            HolderId,
            storedAtTick,
            Observation(state, deviceOnly, availableAtTick, window));

    private static InteractionObservation Observation(
        EvidenceState state = EvidenceState.Confirmed,
        bool deviceOnly = false,
        long availableAtTick = 3,
        InteractionTimeWindow? window = null,
        InteractionVisibility visibility = InteractionVisibility.PrivateEvidence,
        InteractionProvenanceKind provenanceKind = InteractionProvenanceKind.EvidenceRecord) =>
        InteractionObservation.Create(
            ScenarioId, Seed, RecordId, null,
            InteractionRelationKind.PersonPersonEncounter,
            [
                new InteractionParticipant(
                    deviceOnly ? HolderId : TargetId,
                    deviceOnly ? InteractionEntityKind.Device : InteractionEntityKind.Person,
                    InteractionParticipantRole.EncounterParticipant),
                new InteractionParticipant("person-b", InteractionEntityKind.Person,
                    InteractionParticipantRole.EncounterParticipant)
            ],
            window ?? InteractionTimeWindow.At(2),
            ["A"],
            availableAtTick,
            state,
            isPersonEvidence: !deviceOnly,
            isDeviceOnlyEvidence: deviceOnly,
            visibility,
            [new InteractionProvenance(provenanceKind, "upstream-evidence")]);

    private static RescueContext Context() => new()
    {
        ScenarioId = ScenarioId,
        TargetPersonId = TargetId,
        NormalWalkingRadius = 0,
        PublicDeviceIds = [HolderId],
        PublicRegions = new[] { "A", "B", "C", "D" }.Select((id, index) => new RegionDefinition
        {
            Id = id,
            Type = RegionType.Outdoor,
            Center = new Position(index, 0, 0, id)
        }).ToArray(),
        PublicCausalTracingModel = new PublicCausalTracingModel
        {
            CandidateStates = [State(2, ["A", "B"]), State(5, ["C", "D"])]
        },
        PublicHazardEnvelope = new PublicHazardEnvelope
        {
            HazardId = "declared-public-model",
            SourceRegionId = "A",
            PublishedAtTick = 0,
            Branches = [Branch("A", "C"), Branch("B", "D")]
        }
    };

    private static PublicCandidateWarpStateDefinition State(long tick, IReadOnlyList<string> regions) => new()
    {
        TimeWindow = InteractionTimeWindow.At(tick),
        TemporalKind = CandidateWarpTemporalKind.EventPoint,
        RegionIds = regions,
        EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
        Provenance = [CandidateWarpProvenance.Create(
            CandidateWarpSourceKind.PublicCausalModel, "declared-public-model", 0,
            false, false, EvidenceState.SupportedButUncertain, regions)]
    };

    private static PublicHazardBranch Branch(string source, string destination) => new()
    {
        SourceRegionId = source,
        DestinationRegionId = destination,
        MinimumTravelTicks = 3,
        MaximumTravelTicks = 3,
        TransportType = HazardTransportType.HazardDriven,
        ProvenanceId = $"declared-{source}-{destination}",
        PublishedAtTick = 0
    };

    [Fact]
    public void ReceiptGatedComparisonUsesTheSamePublicWorldAndSeparatesTimelineFromAblations()
    {
        var target = Record();
        var later = LaterComparisonRecord();
        var receipts = new[] { Receipt(6), LaterComparisonReceipt(7) };
        var report = RescuerEvidenceMethodComparison.Run(
            MultiHolderContext(), Seed, RescuerId, [target, later], receipts,
            InteractionTimeWindow.At(2), [5, 6, 7], 7);
        Assert.Equal(3, report.ActualTimeline.Count);
        Assert.Equal(4, report.SameCutoffAblations.Count);
        Assert.Equal([0, 1, 2], report.ActualTimeline.Select(row => row.ReceivedOriginalCount));
        Assert.Equal([0, 1, 1, 2],
            report.SameCutoffAblations.Select(row => row.ReceivedOriginalCount));
        Assert.All(report.ActualTimeline, row =>
        {
            Assert.Equal(4, row.Methods.Count);
            Assert.Equal(["A", "B"], row.Methods[0].OuterCandidateRegionIds);
            Assert.Equal(2, row.Methods[0].BaselineCandidateCount);
            Assert.Equal(row.ReceivedOriginalCount, row.AvailableConstraintCount);
        });
        Assert.Empty(report.ActualTimeline[0].ReceivedOriginalIds);
        Assert.DoesNotContain(target.Observation.ObservationId,
            JsonDefaults.Serialize(report.ActualTimeline[0]));
        Assert.DoesNotContain(later.Observation.ObservationId,
            JsonDefaults.Serialize(report.ActualTimeline[1]));
        var first = report.ActualTimeline[1];
        Assert.Equal(["A", "B"], first.Methods[1].OuterCandidateRegionIds);
        Assert.Equal(["A"], first.Methods[2].OuterCandidateRegionIds);
        Assert.Equal(1, first.Methods[2].CandidateReduction);
        Assert.Equal([RecordId], first.Methods[2].GenerationDirectSourceIds);
        Assert.Empty(first.Methods[2].TraversalDirectSourceIds);
        Assert.Empty(first.Methods[1].TraversalDirectSourceIds);
        Assert.Equal(6, report.FirstDifferences.Single(
            item => item.Method == "distributed-reconstruction").FirstDifferentTick);

        var second = report.ActualTimeline[2];
        Assert.Equal(2, second.AvailableSourceGroupCount);
        Assert.Equal(["second-original"], second.Methods[1].TraversalDirectSourceIds);
        Assert.Equal([RecordId], second.Methods[3].GenerationDirectSourceIds);
        Assert.Equal(["second-original"], second.Methods[3].TraversalDirectSourceIds);
        Assert.True(second.Methods[3].ForwardTransitionSteps +
            second.Methods[3].BackwardTransitionSteps > 0);
        Assert.Contains(second.Methods[3].PublicTransitionIds,
            id => !string.IsNullOrWhiteSpace(id));
        Assert.Equal(JsonDefaults.Serialize(second.Methods),
            JsonDefaults.Serialize(report.SameCutoffAblations[3].Methods));
        Assert.Equal(JsonDefaults.Serialize(report),
            JsonDefaults.Serialize(RescuerEvidenceMethodComparison.Run(
                MultiHolderContext(), Seed, RescuerId, [later, target],
                [LaterComparisonReceipt(7), Receipt(6), Receipt(6)],
                InteractionTimeWindow.At(2), [7, 5, 6], 7)));
    }


    [Fact]
    public void ReceiptGatedComparisonPinsAllMethodOutcomesAndWorkAtFixedCutoffs()
    {
        var report = RescuerEvidenceMethodComparison.Run(
            MultiHolderContext(), Seed, RescuerId,
            [Record(), LaterComparisonRecord()],
            [Receipt(6), LaterComparisonReceipt(7)],
            InteractionTimeWindow.At(2), [5, 6, 7], 7);

        // A = public-prior, B = known-relation-backward,
        // C = distributed-reconstruction, C-local = local traversal.
        // Work string is generation / forward / backward /
        // inspected observation records / inspected public relations.
        static void AssertRows(
            RescuerEvidenceMethodQueryRow row, int availableGroups,
            string[] outer, int[] usedGroups, string[] work)
        {
            Assert.Equal(availableGroups, row.AvailableSourceGroupCount);
            Assert.Equal(outer, row.Methods.Select(item =>
                string.Join(",", item.OuterCandidateRegionIds)));
            Assert.Equal(usedGroups, row.Methods.Select(item => item.UsedSourceGroupCount));
            Assert.All(row.Methods, item =>
                Assert.Equal(availableGroups, item.AvailableSourceGroupCount));
            Assert.Equal(outer.Select(item => item == "A,B" ? 0 : 1),
                row.Methods.Select(item => item.CandidateReduction));
            Assert.Equal(work, row.Methods.Select(item =>
                $"{item.GenerationSteps}/{item.ForwardTransitionSteps}/" +
                $"{item.BackwardTransitionSteps}/{item.EvidenceRecordsInspected}/" +
                $"{item.PublicRelationsInspected}"));
            Assert.Null(row.Methods[0].UncertainCandidateRegionIds);
            Assert.Null(row.Methods[1].UncertainCandidateRegionIds);
            Assert.All(row.Methods, item => Assert.Equal(0, item.VerificationSteps));
        }

        AssertRows(report.ActualTimeline[0], 0,
            ["A,B", "A,B", "A,B", "A,B"], [0, 0, 0, 0],
            ["0/0/0/0/0", "0/1/1/0/2", "3/0/0/0/0", "3/1/1/0/2"]);
        AssertRows(report.ActualTimeline[1], 1,
            ["A,B", "A,B", "A", "A"], [0, 0, 1, 1],
            ["0/0/0/0/0", "0/1/1/0/2", "3/0/0/1/1", "3/2/2/0/4"]);
        AssertRows(report.ActualTimeline[2], 2,
            ["A,B", "A,B", "A", "A"], [0, 1, 1, 2],
            ["0/0/0/0/0", "0/2/2/1/5", "3/0/0/1/1", "3/1/1/1/3"]);

        // Same query tick: varying evidence, not later availability.
        AssertRows(report.SameCutoffAblations[0], 0,
            ["A,B", "A,B", "A,B", "A,B"], [0, 0, 0, 0],
            ["0/0/0/0/0", "0/1/1/0/2", "3/0/0/0/0", "3/1/1/0/2"]);
        AssertRows(report.SameCutoffAblations[1], 1,
            ["A,B", "A,B", "A", "A"], [0, 0, 1, 1],
            ["0/0/0/0/0", "0/1/1/0/2", "3/0/0/1/1", "3/2/2/0/4"]);
        // With no published completeness guarantee, traversal evidence alone
        // cannot turn A,B into A; all four outers remain A,B here.
        AssertRows(report.SameCutoffAblations[2], 1,
            ["A,B", "A,B", "A,B", "A,B"], [0, 1, 0, 1],
            ["0/0/0/0/0", "0/2/2/1/5", "3/0/0/0/0", "3/2/2/1/5"]);
        AssertRows(report.SameCutoffAblations[3], 2,
            ["A,B", "A,B", "A", "A"], [0, 1, 1, 2],
            ["0/0/0/0/0", "0/2/2/1/5", "3/0/0/1/1", "3/1/1/1/3"]);

        var both = report.ActualTimeline[2].Methods;
        Assert.Equal(["second-original"], both[1].TraversalDirectSourceIds);
        Assert.Equal([RecordId], both[2].GenerationDirectSourceIds);
        Assert.Equal([RecordId], both[3].GenerationDirectSourceIds);
        Assert.Equal(["second-original"], both[3].TraversalDirectSourceIds);
        Assert.Empty(both[0].GenerationDirectSourceIds);
        Assert.Empty(both[0].TraversalDirectSourceIds);
        Assert.Equal(2, both[0].UnusedDirectSourceIds.Count);
        Assert.Equal(["A", "B"], report.ActualTimeline[0].Methods[2].UncertainCandidateRegionIds);
        Assert.Empty(both[2].UncertainCandidateRegionIds!);
        Assert.All(both, item => Assert.Empty(item.ConflictedCandidateRegionIds ?? []));
        // Receipt of the later weft changes traversal work, but cannot
        // hard-exclude candidates via a non-exhaustive public path list.
        Assert.Null(report.FirstDifferences.Single(
            item => item.Method == "known-relation-backward").FirstDifferentTick);
        Assert.Equal(6, report.FirstDifferences.Single(
            item => item.Method == "distributed-reconstruction-local").FirstDifferentTick);
    }

    [Fact]
    public void ReceiptGatedComparisonDoesNotDoubleCountSharedAncestry()
    {
        var report = RescuerEvidenceMethodComparison.Run(
            MultiHolderContext(), Seed, RescuerId,
            [Record(), LaterComparisonRecord("upstream-evidence")],
            [Receipt(6), LaterComparisonReceipt(7), LaterComparisonReceipt(7)],
            InteractionTimeWindow.At(2), [5, 6, 7], 7);
        var row = report.ActualTimeline[2];
        Assert.Equal(2, row.ReceivedOriginalCount);
        Assert.Equal(2, row.AvailableConstraintCount);
        Assert.Equal(1, row.AvailableSourceGroupCount);
        Assert.Equal([0, 1, 1, 1],
            row.Methods.Select(item => item.UsedSourceGroupCount));
        Assert.All(row.Methods, item => Assert.Equal(1, item.AvailableSourceGroupCount));
        Assert.Equal(JsonDefaults.Serialize(row.Methods),
            JsonDefaults.Serialize(report.SameCutoffAblations[3].Methods));
    }

    [Fact]
    public void ReceiptGatedMethodComparisonKeepsUncertaintyConflictAndDeviceBoundary()
    {
        foreach (var state in new[] {
            EvidenceState.SupportedButUncertain, EvidenceState.Conflicting })
        {
            var record = Record(state: state);
            var snapshot = BuildDistinct(
                [record, LaterComparisonRecord()],
                [Receipt(6), LaterComparisonReceipt(7)], 7);
            var result = RescuerEvidenceMethodComparison.Compare(
                snapshot, InteractionTimeWindow.At(2));
            Assert.Equal(["A", "B"],
                result.Methods.Single(item => item.Method == "distributed-reconstruction")
                    .OuterCandidateRegionIds);
            Assert.Equal(2, result.ReceivedOriginalCount);
            Assert.All(result.Methods, method =>
                Assert.True(method.CandidateCount >= 0));
        }

        var device = BuildDistinct(
            [Record(deviceOnly: true), LaterComparisonRecord()],
            [Receipt(6), LaterComparisonReceipt(7)], 7);
        var deviceResult = RescuerEvidenceMethodComparison.Compare(
            device, InteractionTimeWindow.At(2));
        Assert.Empty(deviceResult.Methods[2].GenerationDirectSourceIds);
        Assert.Equal(["A", "B"], deviceResult.Methods[2].OuterCandidateRegionIds);
    }

    [Fact]
    public void MethodComparisonNeverUsesFutureUnreceivedPrivateOrOtherRescuerEvidence()
    {
        var originals = new[] { Record(), LaterComparisonRecord() };
        var before = BuildDistinct(originals,
            [Receipt(6), LaterComparisonReceipt(7)], 5);
        var withUnrelated = BuildDistinct(originals,
            [Receipt(6), LaterComparisonReceipt(7),
                new InteractionRecordReceipt(ScenarioId, Seed, RecordId, HolderId,
                    "another-rescuer", 4)], 5);
        var prior = RescuerEvidenceMethodComparison.Compare(
            before, InteractionTimeWindow.At(2));
        Assert.Equal(JsonDefaults.Serialize(prior),
            JsonDefaults.Serialize(RescuerEvidenceMethodComparison.Compare(
                withUnrelated, InteractionTimeWindow.At(2))));
        Assert.Empty(prior.ReceivedSourceObservationIds);
        Assert.Equal(0, prior.AvailableSourceGroupCount);
        Assert.All(prior.Methods, method => Assert.Equal(["A", "B"],
            method.OuterCandidateRegionIds));
        Assert.Throws<ArgumentException>(() => RescuerEvidenceMethodComparison.Run(
            MultiHolderContext(), Seed, RescuerId, originals,
            [Receipt(6), LaterComparisonReceipt(9)],
            InteractionTimeWindow.At(2), [5, 6, 7], 7));
    }

    [Fact]
    public void SyntheticTruthIsEvaluatedOnlyAfterThePublicComparison()
    {
        var report = RescuerEvidenceMethodComparison.Run(
            MultiHolderContext(), Seed, RescuerId,
            [Record(), LaterComparisonRecord()],
            [Receipt(6), LaterComparisonReceipt(7)],
            InteractionTimeWindow.At(2), [5, 6, 7], 7);
        var originalJson = JsonDefaults.Serialize(report);
        var truthA = RescuerEvidenceMethodComparison.EvaluateSyntheticTruth(report, "A");
        var truthB = RescuerEvidenceMethodComparison.EvaluateSyntheticTruth(report, "B");
        Assert.Contains(truthA.Evaluations, item =>
            item.QueryTick == 6 && item.Method == "distributed-reconstruction" &&
            item.ContainsSyntheticTruth && !item.FalseExclusion);
        Assert.Contains(truthB.Evaluations, item =>
            item.QueryTick == 6 && item.Method == "distributed-reconstruction" &&
            !item.ContainsSyntheticTruth && item.FalseExclusion);
        Assert.Equal(originalJson, JsonDefaults.Serialize(report));
        Assert.DoesNotContain("SyntheticTruth", originalJson);
    }

    private static DeviceHeldInteractionRecord LaterComparisonRecord(
        string ancestry = "other-upstream") =>
        new("holder-b", 6, InteractionObservation.Create(
            ScenarioId, Seed, "second-original", null,
            InteractionRelationKind.PersonPersonEncounter,
            [
                new InteractionParticipant(TargetId, InteractionEntityKind.Person,
                    InteractionParticipantRole.EncounterParticipant),
                new InteractionParticipant("person-c", InteractionEntityKind.Person,
                    InteractionParticipantRole.EncounterParticipant)
            ],
            InteractionTimeWindow.At(5), ["C"], 6, EvidenceState.Confirmed,
            isPersonEvidence: true, isDeviceOnlyEvidence: false,
            InteractionVisibility.PrivateEvidence,
            [new InteractionProvenance(InteractionProvenanceKind.EvidenceRecord,
                ancestry)]));

    private static InteractionRecordReceipt LaterComparisonReceipt(long tick) =>
        new(ScenarioId, Seed, "second-original", "holder-b", RescuerId, tick);


    private static RescueContext ComplementaryPublicContext()
    {
        var context = MultiHolderContext();
        return context with
        {
            PublicRegions = new[] { "A", "B", "C", "D", "E", "F" }
                .Select((id, index) => new RegionDefinition
                {
                    Id = id,
                    Type = RegionType.Outdoor,
                    Center = new Position(index, 0, 0, id)
                }).ToArray(),
            PublicCausalTracingModel = new PublicCausalTracingModel
            {
                CandidateStates = [State(2, ["A", "B", "C"]), State(5, ["D", "E", "F"])]
            },
            PublicHazardEnvelope = new PublicHazardEnvelope
            {
                HazardId = "declared-public-model",
                SourceRegionId = "A",
                PublishedAtTick = 0,
                Branches = [Branch("A", "D"), Branch("B", "E"), Branch("C", "F")]
            }
        };
    }

    private static DeviceHeldInteractionRecord ComplementaryOriginal(
        string originalId, string holder, long atTick, IReadOnlyList<string> regions,
        long available, string upstream, EvidenceState state = EvidenceState.Confirmed,
        bool deviceOnly = false, bool unrelatedTarget = false,
        InteractionTimeWindow? observationWindow = null) =>
        new(holder, available, InteractionObservation.Create(
            ScenarioId, Seed, originalId, null,
            InteractionRelationKind.PersonPersonEncounter,
            [
                new InteractionParticipant(deviceOnly ? holder :
                    unrelatedTarget ? "person-c" : TargetId,
                    deviceOnly ? InteractionEntityKind.Device : InteractionEntityKind.Person,
                    InteractionParticipantRole.EncounterParticipant),
                new InteractionParticipant("person-b", InteractionEntityKind.Person,
                    InteractionParticipantRole.EncounterParticipant)
            ],
            observationWindow ?? InteractionTimeWindow.At(atTick), regions, available, state,
            isPersonEvidence: !deviceOnly, isDeviceOnlyEvidence: deviceOnly,
            InteractionVisibility.PrivateEvidence,
            [new InteractionProvenance(InteractionProvenanceKind.EvidenceRecord, upstream)]));

    private static RescuerEvidenceMethodReport ComplementaryRun(
        EvidenceState firstState = EvidenceState.Confirmed,
        bool deviceOnly = false,
        string secondAncestry = "weft-b",
        bool reversed = false)
    {
        var first = ComplementaryOriginal(
            RecordId, HolderId, 2, ["A", "B"], 3, "weft-a", firstState, deviceOnly);
        var second = ComplementaryOriginal(
            "second-original", "holder-b", 5, ["E", "F"], 6, secondAncestry);
        var records = reversed ? new[] { second, first } : new[] { first, second };
        var receipts = reversed
            ? new[] { LaterComparisonReceipt(7), Receipt(6), Receipt(6) }
            : new[] { Receipt(6), LaterComparisonReceipt(7) };
        return RescuerEvidenceMethodComparison.Run(
            ComplementaryPublicContext(), Seed, RescuerId, records, receipts,
            InteractionTimeWindow.At(2), reversed ? [7, 5, 6] : [5, 6, 7], 7);
    }

    [Fact]
    public void ComplementaryWeftComparisonMeasuresFourSameCutoffEvidenceConditions()
    {
        var report = ComplementaryRun();
        var result = RescuerEvidenceMethodComparison.AnalyzePairedContributions(report);
        Assert.Equal("available", result.Status);
        Assert.Equal(7, result.QueryTick);
        Assert.Equal([RecordId, "second-original"], result.OriginalIds);
        Assert.Equal(4, result.Methods.Count);
        Assert.All(result.Methods, method =>
        {
            Assert.Equal("available", method.Status);
            Assert.Equal(["neither", "first-only", "second-only", "both"],
                method.Conditions.Select(row => row.Label));
            Assert.Equal([0, 1, 1, 2],
                method.Conditions.Select(row => row.ReceivedOriginalCount));
            Assert.Equal([0, 1, 1, 2],
                method.Conditions.Select(row => row.AvailableSourceGroupCount));
            Assert.All(method.Conditions, row => Assert.Equal(7, row.QueryTick));
        });
        var local = result.Methods.Single(method =>
            method.Method == "distributed-reconstruction-local");
        Assert.Equal(["A", "B", "C"], local.Conditions[0].OuterCandidateRegionIds);
        // #96: public transition absence cannot hard-exclude a missing
        // route. This preserves the same four ablation inputs and records
        // the lack of further hard exclusion from the second original.
        Assert.Equal(["A", "B"], local.Conditions[1].OuterCandidateRegionIds);
        Assert.Equal(["A", "B", "C"], local.Conditions[2].OuterCandidateRegionIds);
        Assert.Equal(["A", "B"], local.Conditions[3].OuterCandidateRegionIds);
        Assert.False(local.BothStrictlyNarrowerThanEachSingle);
        Assert.Empty(local.ExcludedWhenAddingSecond);
        Assert.Equal(["C"], local.ExcludedWhenAddingFirst);
        Assert.Empty(local.AdditionalExclusionsBeyondSingles!);
        Assert.Empty(local.ReappearedComparedWithEitherSingle!);
        Assert.Equal(0, local.CandidateReductionFromFirst);
        Assert.Equal(1, local.CandidateReductionFromSecond);
        Assert.Equal([RecordId], local.Conditions[3].GenerationDirectSourceIds);
        Assert.Equal(["second-original"], local.Conditions[3].TraversalDirectSourceIds);
        Assert.Equal(2, local.Conditions[3].UsedSourceGroupCount);
        Assert.True(local.Conditions[3].ForwardTransitionSteps > 0);
        Assert.True(local.Conditions[3].BackwardTransitionSteps > 0);
        var backward = result.Methods.Single(method =>
            method.Method == "known-relation-backward");
        Assert.Equal(["A", "B", "C"], backward.Conditions[0].OuterCandidateRegionIds);
        Assert.Equal(["A", "B", "C"], backward.Conditions[1].OuterCandidateRegionIds);
        Assert.Equal(["A", "B", "C"], backward.Conditions[2].OuterCandidateRegionIds);
        Assert.Equal(["A", "B", "C"], backward.Conditions[3].OuterCandidateRegionIds);
        var reconstructed = result.Methods.Single(method =>
            method.Method == "distributed-reconstruction");
        Assert.Equal(["A", "B", "C"], reconstructed.Conditions[0].OuterCandidateRegionIds);
        Assert.Equal(["A", "B"], reconstructed.Conditions[1].OuterCandidateRegionIds);
        Assert.Equal(["A", "B", "C"], reconstructed.Conditions[2].OuterCandidateRegionIds);
        Assert.Equal(["A", "B"], reconstructed.Conditions[3].OuterCandidateRegionIds);

        // Snapshot of public simulator work counters from the fixed two-weft world.
        // Keep separate from wall-clock runtime and from actual rescue outcomes.
        var metricsSnapshot = string.Join("\n", result.Methods.SelectMany(method =>
            method.Conditions.Select(row =>
                $"FP-WEFT86 {method.Method} {row.Label}: " +
                $"usedGroups={row.UsedSourceGroupCount}, " +
                $"generation={row.GenerationSteps}, verification={row.VerificationSteps}, " +
                $"forward={row.ForwardTransitionSteps}, backward={row.BackwardTransitionSteps}, " +
                $"evidenceInspected={row.EvidenceRecordsInspected}, " +
                $"relationsInspected={row.PublicRelationsInspected}")));
        const string expectedMetricsSnapshot = """
            FP-WEFT86 distributed-reconstruction neither: usedGroups=0, generation=4, verification=0, forward=0, backward=0, evidenceInspected=0, relationsInspected=0
            FP-WEFT86 distributed-reconstruction first-only: usedGroups=1, generation=4, verification=0, forward=0, backward=0, evidenceInspected=1, relationsInspected=1
            FP-WEFT86 distributed-reconstruction second-only: usedGroups=0, generation=4, verification=0, forward=0, backward=0, evidenceInspected=0, relationsInspected=0
            FP-WEFT86 distributed-reconstruction both: usedGroups=1, generation=4, verification=0, forward=0, backward=0, evidenceInspected=1, relationsInspected=1
            FP-WEFT86 distributed-reconstruction-local neither: usedGroups=0, generation=4, verification=0, forward=1, backward=1, evidenceInspected=0, relationsInspected=2
            FP-WEFT86 distributed-reconstruction-local first-only: usedGroups=1, generation=4, verification=0, forward=2, backward=2, evidenceInspected=0, relationsInspected=4
            FP-WEFT86 distributed-reconstruction-local second-only: usedGroups=1, generation=4, verification=0, forward=2, backward=2, evidenceInspected=1, relationsInspected=5
            FP-WEFT86 distributed-reconstruction-local both: usedGroups=2, generation=4, verification=0, forward=2, backward=2, evidenceInspected=1, relationsInspected=5
            FP-WEFT86 known-relation-backward neither: usedGroups=0, generation=0, verification=0, forward=1, backward=1, evidenceInspected=0, relationsInspected=2
            FP-WEFT86 known-relation-backward first-only: usedGroups=0, generation=0, verification=0, forward=1, backward=1, evidenceInspected=0, relationsInspected=2
            FP-WEFT86 known-relation-backward second-only: usedGroups=1, generation=0, verification=0, forward=2, backward=2, evidenceInspected=1, relationsInspected=5
            FP-WEFT86 known-relation-backward both: usedGroups=1, generation=0, verification=0, forward=2, backward=2, evidenceInspected=1, relationsInspected=5
            FP-WEFT86 public-prior neither: usedGroups=0, generation=0, verification=0, forward=0, backward=0, evidenceInspected=0, relationsInspected=0
            FP-WEFT86 public-prior first-only: usedGroups=0, generation=0, verification=0, forward=0, backward=0, evidenceInspected=0, relationsInspected=0
            FP-WEFT86 public-prior second-only: usedGroups=0, generation=0, verification=0, forward=0, backward=0, evidenceInspected=0, relationsInspected=0
            FP-WEFT86 public-prior both: usedGroups=0, generation=0, verification=0, forward=0, backward=0, evidenceInspected=0, relationsInspected=0
            """;
        Assert.Equal(expectedMetricsSnapshot, metricsSnapshot);

        var prior = result.Methods.Single(method => method.Method == "public-prior");
        Assert.False(prior.BothStrictlyNarrowerThanEachSingle);
        Assert.All(prior.Conditions, row =>
        {
            Assert.Empty(row.GenerationDirectSourceIds);
            Assert.Empty(row.TraversalDirectSourceIds);
            Assert.Equal(0, row.UsedSourceGroupCount);
        });
        Assert.Equal(JsonDefaults.Serialize(result),
            JsonDefaults.Serialize(RescuerEvidenceMethodComparison.AnalyzePairedContributions(
                ComplementaryRun(reversed: true))));
        Assert.Equal(JsonDefaults.Serialize(report), JsonDefaults.Serialize(ComplementaryRun(reversed: true)));
    }

    [Fact]
    public void ComplementaryWeftComparisonMarksAbsentMethodResultsUnavailable()
    {
        var report = ComplementaryRun();
        var missingAll = report with
        {
            SameCutoffAblations = report.SameCutoffAblations
                .Select(row => row with { Methods = [] }).ToArray()
        };
        var noMethods = RescuerEvidenceMethodComparison.AnalyzePairedContributions(missingAll);
        Assert.Equal("unavailable", noMethods.Status);
        Assert.Equal(4, noMethods.Methods.Count);
        Assert.All(noMethods.Methods, method =>
        {
            Assert.Equal("unavailable", method.Status);
            Assert.Empty(method.Conditions);
            Assert.Null(method.CandidateReductionFromFirst);
        });

        var absentEverywhere = report with
        {
            SameCutoffAblations = report.SameCutoffAblations
                .Select(row => row with
                {
                    Methods = row.Methods.Where(method =>
                        method.Method != "known-relation-backward").ToArray()
                }).ToArray()
        };
        var missingMethod = RescuerEvidenceMethodComparison.AnalyzePairedContributions(
            absentEverywhere);
        Assert.Equal("partial", missingMethod.Status);
        Assert.Equal("unavailable", missingMethod.Methods.Single(method =>
            method.Method == "known-relation-backward").Status);
        Assert.Equal(3, missingMethod.Methods.Count(method =>
            method.Status == "available"));

        var missingOne = report with
        {
            SameCutoffAblations = report.SameCutoffAblations
                .Select((row, index) => index == 2 ? row with
                {
                    Methods = row.Methods.Where(method =>
                        method.Method != "distributed-reconstruction-local").ToArray()
                } : row).ToArray()
        };
        var partial = RescuerEvidenceMethodComparison.AnalyzePairedContributions(missingOne);
        Assert.Equal("partial", partial.Status);
        Assert.Equal("unavailable", partial.Methods.Single(method =>
            method.Method == "distributed-reconstruction-local").Status);
        Assert.Null(partial.Methods.Single(method =>
            method.Method == "distributed-reconstruction-local").BothStrictlyNarrowerThanEachSingle);
    }

    [Fact]
    public void ComplementaryWeftComparisonRetainsSharedAncestryAndUncertainty()
    {
        var unavailable = RescuerEvidenceMethodComparison.AnalyzePairedContributions(
            ComplementaryRun() with { SameCutoffAblations = [] });
        Assert.Equal("unavailable", unavailable.Status);
        Assert.Null(unavailable.QueryTick);
        Assert.Empty(unavailable.Methods);

        var shared = RescuerEvidenceMethodComparison.AnalyzePairedContributions(
            ComplementaryRun(secondAncestry: "weft-a"));
        Assert.Equal("available", shared.Status);
        Assert.All(shared.Methods, item =>
            Assert.Equal(1, item.Conditions[3].AvailableSourceGroupCount));
        Assert.Equal(1, shared.Methods.Single(item =>
            item.Method == "distributed-reconstruction-local")
            .Conditions[3].UsedSourceGroupCount);
        foreach (var state in new[]
            { EvidenceState.SupportedButUncertain, EvidenceState.Conflicting })
        {
            var analysis = RescuerEvidenceMethodComparison.AnalyzePairedContributions(
                ComplementaryRun(firstState: state));
            Assert.Equal("available", analysis.Status);
            var method = analysis.Methods.Single(item =>
                item.Method == "distributed-reconstruction");
            Assert.NotNull(method.Conditions[3].UncertainCandidateRegionIds);
            Assert.NotNull(method.Conditions[3].ConflictedCandidateRegionIds);
        }
        var device = RescuerEvidenceMethodComparison.AnalyzePairedContributions(
            ComplementaryRun(deviceOnly: true));
        Assert.Equal("available", device.Status);
        Assert.Empty(device.Methods.Single(item =>
            item.Method == "distributed-reconstruction").Conditions[3]
            .GenerationDirectSourceIds);
    }

    private static (RescuerEvidenceMethodReport Early, RescuerEvidenceMethodReport Late)
        ComplementaryTimingRuns(
            bool reverse = false,
            string ancestry = "weft-b",
            EvidenceState firstState = EvidenceState.Confirmed,
            bool deviceOnly = false)
    {
        var context = ComplementaryPublicContext();
        var first = ComplementaryOriginal(
            RecordId, HolderId, 2, ["A", "B"], 3, "weft-a", firstState, deviceOnly);
        var second = ComplementaryOriginal(
            "second-original", "holder-b", 5, ["E", "F"], 6, ancestry);
        var originals = reverse ? new[] { second, first } : new[] { first, second };
        var receipt1 = Receipt(6);
        var early2 = LaterComparisonReceipt(7);
        var late2 = LaterComparisonReceipt(8);
        // Same exact objects/world/observations; only second receipt tick differs.
        Assert.Equal(early2.OriginalRecordId, late2.OriginalRecordId);
        Assert.Equal(early2.HolderDeviceId, late2.HolderDeviceId);
        Assert.Equal(early2.RescuerId, late2.RescuerId);
        Assert.Equal(7, early2.ReceivedAtTick);
        Assert.Equal(8, late2.ReceivedAtTick);
        var earlyReceipts = reverse
            ? new[] { early2, receipt1, receipt1 }
            : new[] { receipt1, early2 };
        var lateReceipts = reverse
            ? new[] { late2, receipt1, receipt1 }
            : new[] { receipt1, late2 };
        RescuerEvidenceMethodReport Run(InteractionRecordReceipt[] receipts) =>
            RescuerEvidenceMethodComparison.Run(
                context, Seed, RescuerId, originals, receipts,
                InteractionTimeWindow.At(2), reverse ? [8, 7, 6, 5] : [5, 6, 7, 8], 8);
        return (Run(earlyReceipts), Run(lateReceipts));
    }


    [Fact]
    public void HistoricalUnitComparisonCountsActualFourMethodsWithFixedManyToOne()
    {
        var report = ComplementaryRun();
        // Predeclare A,C→U1 and B→U2. {A,B,C}→{A,B} is 3→2
        // regions, yet 2→2 known public units, not a rescue-site result.
        var map = new[]
        {
            new PublicSearchUnitMapping("A", 2, ["U1"], true, "catalog", 0),
            new PublicSearchUnitMapping("B", 2, ["U2"], true, "catalog", 0),
            new PublicSearchUnitMapping("C", 2, ["U1"], true, "catalog", 0)
        };
        var original = JsonDefaults.Serialize(report);
        var result = RescuerEvidenceMethodComparison.AnalyzeHistoricalSearchUnits(
            report, ["A", "B", "C"], ["U1", "U2"], map);
        Assert.Equal(original, JsonDefaults.Serialize(report));
        Assert.Equal("available", result.Status);
        Assert.Equal(2, result.HistoricalTick);
        Assert.Equal([5L, 6L, 7L], result.ActualTimeline.Select(t => t.QueryTick));
        Assert.Equal([0, 1, 2], result.ActualTimeline.Select(t => t.ReceivedOriginalCount));
        Assert.All(result.ActualTimeline, t =>
        {
            Assert.Equal(4, t.Methods.Count);
            Assert.All(t.Methods, m =>
            {
                Assert.True(m.ComparisonEligible);
                Assert.Equal(["U1", "U2"], m.KnownUnitIds);
                Assert.Empty(m.UnresolvedRegionIds);
            });
        });
        Assert.Equal([3, 3, 3, 3],
            result.ActualTimeline[0].Methods.Select(m => m.CandidateRegionCount));
        Assert.Equal([3, 3, 2, 2],
            result.ActualTimeline[1].Methods.Select(m => m.CandidateRegionCount));
        Assert.Equal([3, 3, 2, 2],
            result.ActualTimeline[2].Methods.Select(m => m.CandidateRegionCount));
        Assert.All(result.ActualTimeline.SelectMany(t => t.Methods),
            m => Assert.Equal(2, m.KnownUnitCount));
        Assert.Equal(["A", "B"],
            result.ActualTimeline[1].Methods[2].CandidateRegionIds);
        Assert.Equal(["catalog"],
            result.ActualTimeline[1].Methods[2].MappingSourceIds);

        var reversed = RescuerEvidenceMethodComparison.AnalyzeHistoricalSearchUnits(
            ComplementaryRun(reversed: true), ["C", "A", "B"], ["U2", "U1"],
            map.Reverse().ToArray());
        Assert.Equal(JsonDefaults.Serialize(result), JsonDefaults.Serialize(reversed));

        // Different predeclared catalog: A,B→U1; C→U2 has 2→1 unit reduction.
        var other = new[]
        {
            map[0], map[1] with { UnitIds = ["U1"] },
            map[2] with { UnitIds = ["U2"] }
        };
        var contrast = RescuerEvidenceMethodComparison.AnalyzeHistoricalSearchUnits(
            report, ["A", "B", "C"], ["U1", "U2"], other);
        Assert.Equal(2, contrast.ActualTimeline[0].Methods[2].KnownUnitCount);
        Assert.Equal(1, contrast.ActualTimeline[1].Methods[2].KnownUnitCount);
    }

    [Fact]
    public void HistoricalUnitComparisonPreservesPartialFutureAndOneToManyUnknown()
    {
        var report = ComplementaryRun();
        var map = new[]
        {
            new PublicSearchUnitMapping("A", 2, ["U2", "U1"], true, "early", 0),
            new PublicSearchUnitMapping("B", 2, ["U2"], false, "partial", 0),
            new PublicSearchUnitMapping("C", 2, ["U3"], true, "late", 6)
        };
        var result = RescuerEvidenceMethodComparison.AnalyzeHistoricalSearchUnits(
            report, ["A", "B", "C"], ["U1", "U2", "U3"], map);
        Assert.Equal("unavailable", result.Status);
        var first = result.ActualTimeline[0].Methods[0];
        Assert.Equal("partial", first.Status);
        Assert.Equal(["U1", "U2"], first.KnownUnitIds);
        Assert.Equal(["B", "C"], first.UnresolvedRegionIds);
        Assert.Equal(["early", "partial"], first.MappingSourceIds);
        Assert.False(first.ComparisonEligible);
        var second = result.ActualTimeline[1].Methods[2];
        Assert.Equal(2, second.CandidateRegionCount);
        Assert.Equal(["B"], second.UnresolvedRegionIds);
        Assert.Equal(["U1", "U2"], second.KnownUnitIds);
        Assert.All(result.ActualTimeline.SelectMany(t => t.Methods),
            m => Assert.False(m.ComparisonEligible));

        var none = RescuerEvidenceMethodComparison.AnalyzeHistoricalSearchUnits(
            report, ["A", "B", "C"], [], []);
        Assert.Equal(["A", "B", "C"], none.ActualTimeline[0]
            .Methods[0].UnresolvedRegionIds);
        Assert.Empty(none.ActualTimeline[0].Methods[0].KnownUnitIds);
        Assert.Equal("partial", none.ActualTimeline[0].Methods[0].Status);
        var complete = RescuerEvidenceMethodComparison.AnalyzeHistoricalSearchUnits(
            report, ["A", "B", "C"], ["U1", "U2", "U3"],
            map.Select(m => m with { IsComplete = true, AvailableTick = 0 }).ToArray());
        Assert.Equal("available", complete.Status);
        Assert.Equal(["U1", "U2", "U3"],
            complete.ActualTimeline[0].Methods[0].KnownUnitIds);
        Assert.Equal(["U1", "U2"],
            complete.ActualTimeline[1].Methods[2].KnownUnitIds);
    }

    [Fact]
    public void HistoricalUnitComparisonSeparatesMissingDuplicateAndEmptyCandidate()
    {
        var report = ComplementaryRun();
        var map = new[]
        {
            new PublicSearchUnitMapping("A", 2, ["U1"], true, "catalog", 0),
            new PublicSearchUnitMapping("B", 2, ["U2"], true, "catalog", 0),
            new PublicSearchUnitMapping("C", 2, ["U1"], true, "catalog", 0)
        };
        var missing = report with
        {
            ActualTimeline = report.ActualTimeline.Select((row, i) =>
                i == 0 ? row with { Methods = row.Methods.Skip(1).ToArray() } : row).ToArray()
        };
        var result = RescuerEvidenceMethodComparison.AnalyzeHistoricalSearchUnits(
            missing, ["A", "B", "C"], ["U1", "U2"], map);
        Assert.Equal("partial", result.Status);
        Assert.Equal("unavailable", result.ActualTimeline[0].Methods[0].Status);
        Assert.Null(result.ActualTimeline[0].Methods[0].KnownUnitCount);
        var duplicated = report with
        {
            ActualTimeline = report.ActualTimeline.Select((row, i) =>
                i == 0 ? row with
                {
                    Methods = row.Methods.Concat([row.Methods[0]]).ToArray()
                } : row).ToArray()
        };
        Assert.Equal("unavailable",
            RescuerEvidenceMethodComparison.AnalyzeHistoricalSearchUnits(
                duplicated, ["A", "B", "C"], ["U1", "U2"], map)
            .ActualTimeline[0].Methods[0].Status);

        var empty = report with
        {
            ActualTimeline = report.ActualTimeline.Select((row, i) =>
                i == 0 ? row with
                {
                    Methods = row.Methods.Select((m, j) => j == 0 ? m with
                    {
                        OuterCandidateRegionIds = [],
                        CandidateCount = 0,
                        CandidateReduction = m.BaselineCandidateCount
                    } : m).ToArray()
                } : row).ToArray()
        };
        var trulyEmpty = RescuerEvidenceMethodComparison.AnalyzeHistoricalSearchUnits(
            empty, ["A", "B", "C"], ["U1", "U2"], map)
            .ActualTimeline[0].Methods[0];
        Assert.Equal("empty-candidates", trulyEmpty.Status);
        Assert.Equal(0, trulyEmpty.CandidateRegionCount);
        Assert.Equal(0, trulyEmpty.KnownUnitCount);
        Assert.False(trulyEmpty.ComparisonEligible);
        Assert.Throws<ArgumentNullException>(() =>
            RescuerEvidenceMethodComparison.AnalyzeHistoricalSearchUnits(
                report, ["A", "B", "C"], ["U1", "U2"], null!));
        Assert.Throws<InvalidDataException>(() =>
            RescuerEvidenceMethodComparison.AnalyzeHistoricalSearchUnits(
                report, ["A", "B", "C"], ["U1", "U1"], map));
        Assert.Throws<InvalidDataException>(() =>
            RescuerEvidenceMethodComparison.AnalyzeHistoricalSearchUnits(
                report, ["A", "B", "C"], ["U1", "U2"],
                map.Concat([map[0]]).ToArray()));
        Assert.Throws<InvalidDataException>(() =>
            RescuerEvidenceMethodComparison.AnalyzeHistoricalSearchUnits(
                report, ["A", "B", "C"], ["U1", "U2"],
                [map[0] with { TargetTick = 7 }]));
        Assert.Equal("available",
            RescuerEvidenceMethodComparison.AnalyzeHistoricalSearchUnits(
                ComplementaryRun(secondAncestry: "weft-a"),
                ["A", "B", "C"], ["U1", "U2"], map).Status);
    }

    [Fact]
    public void PairedHistoricalUnitsExposeSourceMarginalsWithoutInventingSiteBenefit()
    {
        var report = ComplementaryRun();
        var map = new[]
        {
            new PublicSearchUnitMapping("A", 2, ["U1"], true, "catalog", 0),
            new PublicSearchUnitMapping("B", 2, ["U2"], true, "catalog", 0),
            new PublicSearchUnitMapping("C", 2, ["U1"], true, "catalog", 0)
        };
        var original = JsonDefaults.Serialize(report);
        var result = RescuerEvidenceMethodComparison.AnalyzePairedHistoricalSearchUnits(
            report, ["A", "B", "C"], ["U1", "U2"], map);
        Assert.Equal(original, JsonDefaults.Serialize(report));
        Assert.Equal("available", result.Status);
        Assert.Equal(2, result.HistoricalTick);
        Assert.Equal(7, result.QueryTick);
        Assert.Equal([RecordId, "second-original"], result.OriginalIds);
        Assert.Equal(4, result.Methods.Count);
        Assert.All(result.Methods, method =>
        {
            Assert.Equal(["neither", "first-only", "second-only", "both"],
                method.Conditions.Select(condition => condition.Label));
            Assert.All(method.Conditions, condition =>
            {
                Assert.Equal("available", condition.Status);
                Assert.True(condition.UnitProjection.ComparisonEligible);
                Assert.Equal(["U1", "U2"], condition.UnitProjection.KnownUnitIds);
                Assert.Empty(condition.UnitProjection.UnresolvedRegionIds);
            });
            Assert.Equal(["second-original", RecordId],
                method.Marginals.Select(m => m.AddedOriginalId));
            Assert.All(method.Marginals, margin =>
            {
                Assert.Equal("available", margin.UnitComparisonStatus);
                Assert.Empty(margin.RemovedKnownUnitIds!);
                Assert.Empty(margin.AddedKnownUnitIds!);
            });
        });
        var local = result.Methods.Single(method =>
            method.Method == "distributed-reconstruction-local");
        Assert.Equal(["A", "B", "C"],
            local.Conditions[2].UnitProjection.CandidateRegionIds);
        Assert.Equal(["A", "B"],
            local.Conditions[3].UnitProjection.CandidateRegionIds);
        Assert.Empty(local.Marginals[0].ExcludedOuterRegionIds!);
        Assert.Equal(["C"], local.Marginals[1].ExcludedOuterRegionIds);
        Assert.Equal(1, local.Marginals[0].UsedSourceGroupDelta);
        Assert.Null(local.Marginals[0].SamePreferredCandidates);
        var prior = result.Methods.Single(method => method.Method == "public-prior");
        Assert.All(prior.Marginals, margin =>
        {
            Assert.Empty(margin.ExcludedOuterRegionIds!);
            Assert.Empty(margin.ReappearedOuterRegionIds!);
            Assert.Equal(0, margin.UsedSourceGroupDelta);
        });
        Assert.Equal(JsonDefaults.Serialize(result),
            JsonDefaults.Serialize(RescuerEvidenceMethodComparison.AnalyzePairedHistoricalSearchUnits(
                ComplementaryRun(reversed: true), ["C", "A", "B"], ["U2", "U1"],
                map.Reverse().ToArray())));

        // The other fixed catalog is an information-model sensitivity check,
        // not a different evidence set or an empirical rescue result.
        var contrastMap = new[]
        {
            map[0], map[1] with { UnitIds = ["U1"] },
            map[2] with { UnitIds = ["U2"] }
        };
        var contrast = RescuerEvidenceMethodComparison.AnalyzePairedHistoricalSearchUnits(
            report, ["A", "B", "C"], ["U1", "U2"], contrastMap);
        var contrastLocal = contrast.Methods.Single(method =>
            method.Method == "distributed-reconstruction-local");
        Assert.Empty(contrastLocal.Marginals[0].RemovedKnownUnitIds!);
        Assert.Equal(["U2"], contrastLocal.Marginals[1].RemovedKnownUnitIds);
        Assert.Equal("available", contrast.Status);
    }


    // #106: Ask which absent public relations could change a formal
    // witnessed/unknown status, without assuming those relations exist.
    [Fact]
    public void MissingDeclaredPathQuestionsRespectReceiptsAndExplicitWhatIf()
    {
        var context = ComplementaryPublicContext();
        var first = ComplementaryOriginal(
            RecordId, HolderId, 2, ["A", "B", "C"], 3, "weft-a");
        var second = ComplementaryOriginal(
            "second-original", "holder-b", 5, ["D"], 6, "weft-b");
        var builder = new RescuerEvidenceInputBuilder();
        var before = builder.BuildDistinctOriginals(
            context, Seed, 6, RescuerId, [first, second],
            [Receipt(6), LaterComparisonReceipt(7)]);
        var after = builder.BuildDistinctOriginals(
            context, Seed, 7, RescuerId, [first, second],
            [Receipt(6), LaterComparisonReceipt(7)]);
        var beforeQuestions =
            RescuerEvidenceMethodComparison.AnalyzeMissingDeclaredPathQuestions(
                before, InteractionTimeWindow.At(2));
        Assert.All(beforeQuestions.Candidates, row =>
        {
            Assert.Equal("unavailable", row.DeclaredPathStatus);
            Assert.Empty(row.Questions);
        });
        var afterQuestions =
            RescuerEvidenceMethodComparison.AnalyzeMissingDeclaredPathQuestions(
                after, InteractionTimeWindow.At(2));
        Assert.Equal(["A", "B", "C"],
            afterQuestions.Candidates.Select(row => row.RegionId));
        Assert.Equal(["witnessed", "unknown", "unknown"],
            afterQuestions.Candidates.Select(row => row.DeclaredPathStatus));
        Assert.Empty(afterQuestions.Candidates[0].Questions);
        var b = Assert.Single(afterQuestions.Candidates[1].Questions);
        var c = Assert.Single(afterQuestions.Candidates[2].Questions);
        Assert.Equal(("B", "D"), (b.SourceRegionId, b.DestinationRegionId));
        Assert.Equal(("C", "D"), (c.SourceRegionId, c.DestinationRegionId));
        Assert.Equal(3, b.MinimumCompatibleDifferenceTicks);
        Assert.Equal(3, b.MaximumCompatibleDifferenceTicks);
        Assert.Equal(RecordId, b.EarlyDirectSourceId);
        Assert.Equal("second-original", b.LaterDirectSourceId);
        Assert.NotEqual(b.EarlyConstraintId, b.LaterConstraintId);
        Assert.NotEqual(b.EarlySourceObservationId, b.LaterSourceObservationId);
        Assert.Contains("weft-a", b.EarlySourceAncestryIds);
        Assert.Contains("weft-b", b.LaterSourceAncestryIds);
        Assert.True(b.IndependentSourceAncestry);
        Assert.Equal("unknown", b.PersonTraversability);
        Assert.StartsWith("unconfirmed:", b.QuestionId);
        Assert.Equal(3, c.MaximumCompatibleDifferenceTicks);
        var originalBytes = JsonDefaults.Serialize(after);
        var originalDiagnostic = RescuerEvidenceMethodComparison
            .AnalyzeDeclaredPathCompatibility(after, InteractionTimeWindow.At(2));
        Assert.Equal(["witnessed", "unknown", "unknown"],
            originalDiagnostic.Candidates.Select(row => row.Status));

        // A different PUBLIC INFORMATION condition, not evidence acquisition,
        // truth discovery or mutation of the baseline received snapshot.
        var whatIf = context with
        {
            PublicHazardEnvelope = context.PublicHazardEnvelope! with
            {
                Branches = [.. context.PublicHazardEnvelope!.Branches,
                    Branch("B", "D") with { PublishedAtTick = 7 }]
            }
        };
        var changed = builder.BuildDistinctOriginals(
            whatIf, Seed, 7, RescuerId, [first, second],
            [Receipt(6), LaterComparisonReceipt(7)]);
        var changedDiagnostic = RescuerEvidenceMethodComparison
            .AnalyzeDeclaredPathCompatibility(changed, InteractionTimeWindow.At(2));
        Assert.Equal(["witnessed", "witnessed", "unknown"],
            changedDiagnostic.Candidates.Select(row => row.Status));
        var changedQuestions =
            RescuerEvidenceMethodComparison.AnalyzeMissingDeclaredPathQuestions(
                changed, InteractionTimeWindow.At(2));
        Assert.Empty(changedQuestions.Candidates[1].Questions);
        Assert.Single(changedQuestions.Candidates[2].Questions);
        Assert.Equal(originalBytes, JsonDefaults.Serialize(after));
        Assert.Equal(JsonDefaults.Serialize(afterQuestions),
            JsonDefaults.Serialize(RescuerEvidenceMethodComparison
                .AnalyzeMissingDeclaredPathQuestions(after, InteractionTimeWindow.At(2))));
        Assert.All(RescuerEvidenceMethodComparison.Compare(
            after, InteractionTimeWindow.At(2)).Methods, row =>
                Assert.Equal(["A", "B", "C"], row.OuterCandidateRegionIds));
    }

    [Fact]
    public void MissingDeclaredPathQuestionsDoNotPromoteUnavailableOrSoftEvidence()
    {
        var context = ComplementaryPublicContext();
        var first = ComplementaryOriginal(
            RecordId, HolderId, 2, ["A", "B", "C"], 3, "weft-a");
        var second = ComplementaryOriginal(
            "second-original", "holder-b", 5, ["D"], 6, "weft-b");
        var builder = new RescuerEvidenceInputBuilder();
        RescuerMissingDeclaredPathReport Questions(
            RescueContext world, DeviceHeldInteractionRecord early,
            DeviceHeldInteractionRecord late, bool reverse = false)
        {
            var originals = reverse ? new[] { late, early } : new[] { early, late };
            var receipts = reverse
                ? new[] { LaterComparisonReceipt(7), Receipt(6), Receipt(6) }
                : new[] { Receipt(6), LaterComparisonReceipt(7) };
            return RescuerEvidenceMethodComparison.AnalyzeMissingDeclaredPathQuestions(
                builder.BuildDistinctOriginals(
                    world, Seed, 7, RescuerId, originals, receipts),
                InteractionTimeWindow.At(2));
        }
        var normal = Questions(context, first, second);
        Assert.Equal(JsonDefaults.Serialize(normal),
            JsonDefaults.Serialize(Questions(context, first, second, reverse: true)));

        var shared = Questions(context, first, ComplementaryOriginal(
            "second-original", "holder-b", 5, ["D"], 6, "weft-a"));
        Assert.All(shared.Candidates.SelectMany(row => row.Questions),
            question => Assert.False(question.IndependentSourceAncestry));
        Assert.Equal(2, shared.Candidates.Sum(row => row.Questions.Count));
        foreach (var soft in new[]
        {
            EvidenceState.SupportedButUncertain, EvidenceState.Conflicting
        })
        {
            var questions = Questions(context, first, ComplementaryOriginal(
                "second-original", "holder-b", 5, ["D"], 6, "weft-b", soft));
            Assert.All(questions.Candidates, row => Assert.Empty(row.Questions));
        }
        var device = Questions(context, first, ComplementaryOriginal(
            "second-original", "holder-b", 5, ["D"], 6, "weft-b",
            deviceOnly: true));
        Assert.All(device.Candidates, row =>
        {
            Assert.Equal("unavailable", row.DeclaredPathStatus);
            Assert.Empty(row.Questions);
        });
        var otherPerson = Questions(context, first, ComplementaryOriginal(
            "second-original", "holder-b", 5, ["D"], 6, "weft-b",
            unrelatedTarget: true));
        Assert.All(otherPerson.Candidates, row => Assert.Empty(row.Questions));
        var firstDevice = Questions(context,
            ComplementaryOriginal(RecordId, HolderId, 2,
                ["A", "B", "C"], 3, "weft-a", deviceOnly: true), second);
        Assert.All(firstDevice.Candidates, row => Assert.Empty(row.Questions));
        var lateTooEarly = Questions(context, first, ComplementaryOriginal(
            "second-original", "holder-b", 1, ["D"], 6, "weft-b"));
        Assert.All(lateTooEarly.Candidates, row => Assert.Empty(row.Questions));

        // Future declaration must not count as a present witness or as an
        // already answered question; no truth is imported.
        var future = context with
        {
            PublicHazardEnvelope = context.PublicHazardEnvelope! with
            {
                Branches = [.. context.PublicHazardEnvelope!.Branches,
                    Branch("B", "D") with { PublishedAtTick = 8 }]
            }
        };
        Assert.Equal(["B", "C"], Questions(future, first, second).Candidates
            .Where(row => row.Questions.Count > 0).Select(row => row.RegionId));
        var onlyLaterB = context with
        {
            PublicCausalTracingModel = new PublicCausalTracingModel
            {
                CandidateStates = [
                    State(2, ["A", "C"]), State(4, ["B"]),
                    State(5, ["D", "E", "F"])]
            }
        };
        var builderAtInterval = new RescuerEvidenceInputBuilder();
        var missingTick = RescuerEvidenceMethodComparison
            .AnalyzeMissingDeclaredPathQuestions(
                builderAtInterval.BuildDistinctOriginals(
                    onlyLaterB, Seed, 7, RescuerId, [first, second],
                    [Receipt(6), LaterComparisonReceipt(7)]),
                new InteractionTimeWindow(2, 4));
        Assert.Empty(missingTick.Candidates.Single(row => row.RegionId == "B").Questions);
    }


    // AR-11/12: a direct edge is not the only formal public explanation.
    // All controls use two actually received Person originals at tick 7.
    [Fact]
    public void MissingRelationQuestionsDistinguishStayAndTwoHopFromAbsentDirectEdge()
    {
        var baseline = ComplementaryPublicContext();
        var first = ComplementaryOriginal(
            RecordId, HolderId, 2, ["A", "B", "C"], 3, "weft-a");
        var laterD = ComplementaryOriginal(
            "second-original", "holder-b", 5, ["D"], 6, "weft-b");
        var builder = new RescuerEvidenceInputBuilder();
        RescuerEvidenceInput At(RescueContext context,
            DeviceHeldInteractionRecord later, long tick) =>
            builder.BuildDistinctOriginals(context, Seed, tick, RescuerId,
                [first, later], [Receipt(6), LaterComparisonReceipt(7)]);
        static RescuerMissingDeclaredPathCandidate Row(
            RescuerMissingDeclaredPathReport report, string id) =>
            report.Candidates.Single(candidate => candidate.RegionId == id);

        // Published B->E at one tick and E->D at two ticks compose
        // into a formal three-tick path. Neither proves Person movement.
        var twoHop = baseline with
        {
            PublicHazardEnvelope = baseline.PublicHazardEnvelope! with
            {
                Branches = [.. baseline.PublicHazardEnvelope!.Branches,
                    Branch("B", "E") with
                    {
                        MinimumTravelTicks = 1, MaximumTravelTicks = 1,
                        ProvenanceId = "AR11-B-E"
                    },
                    Branch("E", "D") with
                    {
                        MinimumTravelTicks = 2, MaximumTravelTicks = 2,
                        ProvenanceId = "AR11-E-D"
                    }]
            }
        };
        var onlyFirst = RescuerEvidenceMethodComparison
            .AnalyzeMissingDeclaredPathQuestions(
                At(twoHop, laterD, 6), InteractionTimeWindow.At(2));
        Assert.All(onlyFirst.Candidates, candidate =>
        {
            Assert.Empty(candidate.Questions);
            Assert.Empty(candidate.PublicAlternativeExplanations);
        });
        var twoHopInput = At(twoHop, laterD, 7);
        var hop = RescuerEvidenceMethodComparison
            .AnalyzeMissingDeclaredPathQuestions(
                twoHopInput, InteractionTimeWindow.At(2));
        Assert.Empty(Row(hop, "B").Questions);
        Assert.Contains("formal-two-hop:B>E>D",
            Row(hop, "B").PublicAlternativeExplanations);
        Assert.Single(Row(hop, "C").Questions);
        // #104 intentionally remains a *direct-branch* diagnostic.
        Assert.Equal("unknown", Row(hop, "B").DeclaredPathStatus);
        Assert.All(RescuerEvidenceMethodComparison.Compare(
            twoHopInput, InteractionTimeWindow.At(2)).Methods,
            method => Assert.Equal(["A", "B", "C"], method.OuterCandidateRegionIds));
        var hopReversed = builder.BuildDistinctOriginals(twoHop, Seed, 7, RescuerId,
            [laterD, first], [LaterComparisonReceipt(7), Receipt(6), Receipt(6)]);
        Assert.Equal(JsonDefaults.Serialize(hop),
            JsonDefaults.Serialize(RescuerEvidenceMethodComparison
                .AnalyzeMissingDeclaredPathQuestions(
                    hopReversed, InteractionTimeWindow.At(2))));

        // Public future edges cannot suppress the currently unanswered pair.
        var futureHop = twoHop with
        {
            PublicHazardEnvelope = twoHop.PublicHazardEnvelope! with
            {
                Branches = twoHop.PublicHazardEnvelope!.Branches.Select(b =>
                    b.ProvenanceId.StartsWith("AR11-", StringComparison.Ordinal)
                        ? b with { PublishedAtTick = 8 } : b).ToArray()
            }
        };
        var future = RescuerEvidenceMethodComparison
            .AnalyzeMissingDeclaredPathQuestions(
                At(futureHop, laterD, 7), InteractionTimeWindow.At(2));
        Assert.Single(Row(future, "B").Questions);
        Assert.Empty(Row(future, "B").PublicAlternativeExplanations);

        // Same-region persistence is a formal possibility even with no B->B.
        var sameRegion = baseline with
        {
            PublicCausalTracingModel = new PublicCausalTracingModel
            {
                CandidateStates = [
                    State(2, ["A", "B", "C"]), State(5, ["B", "E", "F"])]
            }
        };
        var laterB = ComplementaryOriginal(
            "second-original", "holder-b", 5, ["B"], 6, "weft-b");
        var stayInput = At(sameRegion, laterB, 7);
        var stay = RescuerEvidenceMethodComparison
            .AnalyzeMissingDeclaredPathQuestions(
                stayInput, InteractionTimeWindow.At(2));
        Assert.Empty(Row(stay, "B").Questions);
        Assert.Contains("formal-stay:B", Row(stay, "B").PublicAlternativeExplanations);
        Assert.All(RescuerEvidenceMethodComparison.Compare(
            stayInput, InteractionTimeWindow.At(2)).Methods,
            method => Assert.Contains("B", method.OuterCandidateRegionIds));
    }

    [Fact]
    public void PublishedEndpointWithWrongTimingIsNotCalledAnAbsentRelation()
    {
        var context = ComplementaryPublicContext();
        var first = ComplementaryOriginal(
            RecordId, HolderId, 2, ["A", "B", "C"], 3, "weft-a");
        var later = ComplementaryOriginal(
            "second-original", "holder-b", 5, ["D"], 6, "weft-b");
        var timingMismatch = context with
        {
            PublicHazardEnvelope = context.PublicHazardEnvelope! with
            {
                Branches = [.. context.PublicHazardEnvelope!.Branches,
                    Branch("B", "D") with
                    {
                        MinimumTravelTicks = 10, MaximumTravelTicks = 10
                    }]
            }
        };
        var input = new RescuerEvidenceInputBuilder().BuildDistinctOriginals(
            timingMismatch, Seed, 7, RescuerId,
            [first, later], [Receipt(6), LaterComparisonReceipt(7)]);
        var report = RescuerEvidenceMethodComparison
            .AnalyzeMissingDeclaredPathQuestions(input, InteractionTimeWindow.At(2));
        var b = report.Candidates.Single(row => row.RegionId == "B");
        Assert.Equal("unknown", b.DeclaredPathStatus);
        Assert.Empty(b.Questions);
        Assert.Contains("direct-pair-published-timing-unverified:B>D",
            b.PublicAlternativeExplanations);
        Assert.Single(report.Candidates.Single(row => row.RegionId == "C").Questions);
        Assert.All(RescuerEvidenceMethodComparison.Compare(
            input, InteractionTimeWindow.At(2)).Methods,
            method => Assert.Contains("B", method.OuterCandidateRegionIds));
    }


    // #108 / AR-13: same receipt-gated Person inputs, with published
    // three-/four-hop structure. No truth, private or future branch is used.
    [Fact]
    public void PublishedMultihopQuestionsDistinguishFormalOverlapFromUnconfirmed()
    {
        var baseWorld = ComplementaryPublicContext();
        var threeHop = baseWorld with
        {
            PublicHazardEnvelope = baseWorld.PublicHazardEnvelope! with
            {
                Branches = [.. baseWorld.PublicHazardEnvelope!.Branches,
                    Branch("B", "E") with
                    {
                        MinimumTravelTicks = 1, MaximumTravelTicks = 1,
                        ProvenanceId = "three-BE"
                    },
                    Branch("E", "F") with
                    {
                        MinimumTravelTicks = 1, MaximumTravelTicks = 1,
                        ProvenanceId = "three-EF"
                    },
                    Branch("F", "D") with
                    {
                        MinimumTravelTicks = 1, MaximumTravelTicks = 1,
                        ProvenanceId = "three-FD"
                    }]
            }
        };
        var first = ComplementaryOriginal(RecordId, HolderId,
            2, ["A", "B", "C"], 3, "weft-a");
        var later = ComplementaryOriginal("second-original", "holder-b",
            5, ["D"], 6, "weft-b");
        var builder = new RescuerEvidenceInputBuilder();
        RescuerEvidenceInput At(RescueContext world, bool reverse = false)
        {
            var originals = reverse ? new[] { later, first } : new[] { first, later };
            var receipts = reverse
                ? new[] { LaterComparisonReceipt(7), Receipt(6), Receipt(6) }
                : new[] { Receipt(6), LaterComparisonReceipt(7) };
            return builder.BuildDistinctOriginals(world, Seed, 7,
                RescuerId, originals, receipts);
        }
        // World without the three extra declarations is an information
        // ablation, not a same-world processing comparison.
        var withoutThreeHop = RescuerEvidenceMethodComparison
            .AnalyzeMissingDeclaredPathQuestions(
                At(baseWorld), InteractionTimeWindow.At(2));
        Assert.Equal(["B", "C"], withoutThreeHop.Candidates
            .Where(row => row.Questions.Count > 0).Select(row => row.RegionId));
        var sameEvidence = At(threeHop);
        // A separate, fixed legacy-two-hop reference on the SAME projected
        // receipt/public model; the test's B/C at tick 2 and D at tick 5
        // are the only endpoint pairs to compare. Count inspected edges.
        static (string[] Questions, int Inspected) LegacyTwoHop(
            RescuerEvidenceInput received)
        {
            var edges = received.Input.PublicContext.PublicHazardEnvelope!.Branches
                .Where(edge => edge.PublishedAtTick <= received.Input.CurrentTick)
                .ToArray();
            var questions = new List<string>();
            var inspected = 0;
            foreach (var start in new[] { "B", "C" })
            {
                var explained = edges.Any(edge =>
                    edge.SourceRegionId == start &&
                    edge.DestinationRegionId == "D");
                foreach (var firstEdge in edges.Where(edge =>
                    edge.SourceRegionId == start))
                {
                    inspected++;
                    foreach (var secondEdge in edges.Where(edge =>
                        edge.SourceRegionId == firstEdge.DestinationRegionId))
                    {
                        inspected++;
                        if (secondEdge.DestinationRegionId == "D" &&
                            (long)firstEdge.MinimumTravelTicks +
                                secondEdge.MinimumTravelTicks <= 3 &&
                            (long)firstEdge.MaximumTravelTicks +
                                secondEdge.MaximumTravelTicks >= 3)
                            explained = true;
                    }
                }
                if (!explained)
                    questions.Add(start);
            }
            return (questions.ToArray(), inspected);
        }
        var legacy = LegacyTwoHop(sameEvidence);
        Assert.Equal(["B", "C"], legacy.Questions);
        // Counts only the legacy one-/two-hop loop body; the direct-pair
        // Any pre-scan is not included in this narrow operation counter.
        Assert.Equal(6, legacy.Inspected);
        var report = RescuerEvidenceMethodComparison
            .AnalyzeMissingDeclaredPathQuestions(
                sameEvidence, InteractionTimeWindow.At(2));
        var b = report.Candidates.Single(row => row.RegionId == "B");
        var c = report.Candidates.Single(row => row.RegionId == "C");
        Assert.Empty(b.Questions);
        Assert.Single(c.Questions);
        Assert.Equal("unknown", b.DeclaredPathStatus);
        Assert.Contains(b.PublicAlternativeExplanations,
            reason => reason.StartsWith("formal-multihop:B>E>F>D:",
                StringComparison.Ordinal) &&
                reason.Contains("three-BE,three-EF,three-FD", StringComparison.Ordinal));
        // Includes expanded-route edge visits for feasible and overlong paths.
        Assert.Equal(8, report.PublicTraversalBranchesInspected);
        Assert.Equal(2, legacy.Questions.Length);
        Assert.Equal(1, report.Candidates.Sum(row => row.Questions.Count));
        Assert.Equal(JsonDefaults.Serialize(report), JsonDefaults.Serialize(
            RescuerEvidenceMethodComparison.AnalyzeMissingDeclaredPathQuestions(
                At(threeHop, reverse: true), InteractionTimeWindow.At(2))));
        Assert.All(RescuerEvidenceMethodComparison.Compare(
            sameEvidence, InteractionTimeWindow.At(2)).Methods,
            method => Assert.Equal(["A", "B", "C"], method.OuterCandidateRegionIds));

        var future = threeHop with
        {
            PublicHazardEnvelope = threeHop.PublicHazardEnvelope! with
            {
                Branches = threeHop.PublicHazardEnvelope!.Branches.Select(edge =>
                    edge.ProvenanceId == "three-EF"
                        ? edge with { PublishedAtTick = 8 } : edge).ToArray()
            }
        };
        var futureReport = RescuerEvidenceMethodComparison
            .AnalyzeMissingDeclaredPathQuestions(At(future), InteractionTimeWindow.At(2));
        Assert.Single(futureReport.Candidates.Single(row => row.RegionId == "B").Questions);

        // An extra published cycle must not cause unbounded traversal or
        // change a valid existing three-hop witness.
        var cycle = threeHop with
        {
            PublicHazardEnvelope = threeHop.PublicHazardEnvelope! with
            {
                Branches = [.. threeHop.PublicHazardEnvelope!.Branches,
                    Branch("F", "B") with
                    {
                        MinimumTravelTicks = 1, MaximumTravelTicks = 1,
                        ProvenanceId = "cycle-FB"
                    }]
            }
        };
        var cycleReport = RescuerEvidenceMethodComparison
            .AnalyzeMissingDeclaredPathQuestions(At(cycle), InteractionTimeWindow.At(2));
        Assert.Empty(cycleReport.Candidates.Single(row => row.RegionId == "B").Questions);
        Assert.InRange(cycleReport.PublicTraversalBranchesInspected, 1, 4096 * 3);
    }

    [Fact]
    public void PublishedTravelClassifiesShortAndLongWithoutClaimingDwell()
    {
        var world = ComplementaryPublicContext();
        var early = ComplementaryOriginal(RecordId, HolderId,
            2, ["A", "B", "C"], 3, "weft-a");
        var later = ComplementaryOriginal("second-original", "holder-b",
            5, ["D"], 6, "weft-b");
        var builder = new RescuerEvidenceInputBuilder();

        RescuerMissingDeclaredPathCandidate Direct(int travel)
        {
            var directWorld = world with
            {
                PublicHazardEnvelope = world.PublicHazardEnvelope! with
                {
                    Branches = [.. world.PublicHazardEnvelope!.Branches,
                        Branch("B", "D") with
                        {
                            MinimumTravelTicks = travel, MaximumTravelTicks = travel,
                            ProvenanceId = "direct-calibration"
                        }]
                }
            };
            var input = builder.BuildDistinctOriginals(directWorld, Seed, 7, RescuerId,
                [early, later], [Receipt(6), LaterComparisonReceipt(7)]);
            return RescuerEvidenceMethodComparison
                .AnalyzeMissingDeclaredPathQuestions(input, InteractionTimeWindow.At(2))
                .Candidates.Single(row => row.RegionId == "B");
        }

        var shortDirect = Direct(1);
        var longDirect = Direct(10);
        Assert.Empty(shortDirect.Questions);
        Assert.Empty(longDirect.Questions);
        Assert.Equal("unknown", shortDirect.DeclaredPathStatus);
        Assert.Equal("unknown", longDirect.DeclaredPathStatus);
        Assert.Contains(shortDirect.PublicAlternativeExplanations,
            reason => reason.StartsWith(
                "direct-pair-published-shorter-dwell-unverified:B>D:",
                StringComparison.Ordinal));
        Assert.Contains(longDirect.PublicAlternativeExplanations,
            reason => reason.StartsWith(
                "direct-pair-published-too-long:B>D:", StringComparison.Ordinal));

        // The same three-edge travel sum 3 is shorter than an observation
        // difference 4. The result must not say that a stay was established.
        var three = world with
        {
            PublicHazardEnvelope = world.PublicHazardEnvelope! with
            {
                Branches = [.. world.PublicHazardEnvelope!.Branches,
                    Branch("B", "E") with
                    {
                        MinimumTravelTicks = 1,
                    MaximumTravelTicks = 1
                    },
                    Branch("E", "F") with
                    {
                        MinimumTravelTicks = 1, MaximumTravelTicks = 1
                    },
                    Branch("F", "D") with
                    {
                        MinimumTravelTicks = 1, MaximumTravelTicks = 1
                    }]
            }
        };
        var laterAtSix = ComplementaryOriginal("second-original", "holder-b",
            6, ["D"], 6, "weft-b");
        var inputSix = builder.BuildDistinctOriginals(three, Seed, 7, RescuerId,
            [early, laterAtSix], [Receipt(6), LaterComparisonReceipt(7)]);
        var shortPath = RescuerEvidenceMethodComparison
            .AnalyzeMissingDeclaredPathQuestions(inputSix, InteractionTimeWindow.At(2))
            .Candidates.Single(row => row.RegionId == "B");
        Assert.Single(shortPath.Questions);
        Assert.DoesNotContain(shortPath.PublicAlternativeExplanations,
            reason => reason.StartsWith("formal-multihop:", StringComparison.Ordinal));
        Assert.Contains(shortPath.PublicAlternativeExplanations,
            reason => reason.StartsWith(
                "published-path-shorter-dwell-unverified:B>E>F>D:",
                StringComparison.Ordinal));

        // A four-hop path with four explicit ticks can explain the later tick 6
        // without using an unrealized movement trace or a direct B->D edge.
        var four = three with
        {
            PublicHazardEnvelope = three.PublicHazardEnvelope! with
            {
                Branches = three.PublicHazardEnvelope!.Branches
                    .Where(edge => !(edge.SourceRegionId == "F" &&
                        edge.DestinationRegionId == "D"))
                    .Append(Branch("F", "C") with
                    {
                        MinimumTravelTicks = 1,
                        MaximumTravelTicks = 1
                    })
                    .Append(Branch("C", "D") with
                    {
                        MinimumTravelTicks = 1,
                        MaximumTravelTicks = 1
                    })
                    .ToArray()
            }
        };
        var inputFour = builder.BuildDistinctOriginals(four, Seed, 7, RescuerId,
            [early, laterAtSix], [Receipt(6), LaterComparisonReceipt(7)]);
        var fourReport = RescuerEvidenceMethodComparison
            .AnalyzeMissingDeclaredPathQuestions(inputFour, InteractionTimeWindow.At(2));
        Assert.Empty(fourReport.Candidates.Single(row => row.RegionId == "B").Questions);
        Assert.Contains(fourReport.Candidates.Single(row => row.RegionId == "B")
            .PublicAlternativeExplanations,
            reason => reason.StartsWith("formal-multihop:B>E>F>C>D:",
                StringComparison.Ordinal));
    }



    // #110 / AR-15: the publication is identical in two possible truth
    // worlds. The new result is a separately labeled hypothesis, not a hard
    // candidate change or an unconditional reduction of questions.
    [Fact]
    public void ConditionalPublishedDwellKeepsQuestionsInIndistinguishableWorlds()
    {
        static StochasticHazardDefinition Definition(
            bool hiddenDeparture, bool deviceOnlyMiddle = false)
        {
            var nodes = new List<HazardNodeDefinition>();
            foreach (var (id, tick) in new[]
                { ("B", 2L), ("E", 3L), ("F", 4L), ("D", 5L) })
            {
                nodes.Add(new HazardNodeDefinition
                {
                    RegionId = id,
                    MinimumArrivalTick = tick,
                    MaximumArrivalTick = tick,
                    PublishedAtTick = 0,
                    PublicProvenanceId = "public-node-" + id,
                    IsTerminal = id == "D" && !hiddenDeparture
                });
            }
            if (hiddenDeparture)
                nodes.Add(new HazardNodeDefinition
                {
                    RegionId = "G",
                    MinimumArrivalTick = 6,
                    MaximumArrivalTick = 6,
                    PubliclyVisible = false,
                    IsTerminal = true
                });
            var transitions = new List<HazardTransitionDefinition>();
            foreach (var (from, to) in new[]
                { ("B", "E"), ("E", "F"), ("F", "D") })
            {
                transitions.Add(new HazardTransitionDefinition
                {
                    Id = "public-" + from + to,
                    SourceRegionId = from,
                    DestinationRegionId = to,
                    Weight = 1,
                    MinimumTravelTicks = 1,
                    MaximumTravelTicks = 1,
                    TransportType = HazardTransportType.HazardDriven,
                    Activation = deviceOnlyMiddle && from == "E" && to == "F"
                        ? HazardTransitionActivation.DeviceOnly
                        : HazardTransitionActivation.AnyEntity,
                    ProvenanceId = "public-" + from + to
                });
            }
            if (hiddenDeparture)
                transitions.Add(new HazardTransitionDefinition
                {
                    Id = "hidden-DG",
                    SourceRegionId = "D",
                    DestinationRegionId = "G",
                    Weight = 1,
                    MinimumTravelTicks = 1,
                    MaximumTravelTicks = 1,
                    TransportType = HazardTransportType.HazardDriven,
                    ProvenanceId = "private-DG",
                    PubliclyVisible = false
                });
            return new StochasticHazardDefinition
            {
                HazardId = "same-public-hazard",
                SourceRegionId = "B",
                StartTick = 2,
                Nodes = nodes,
                Transitions = transitions
            };
        }
        var baseDefinition = Definition(hiddenDeparture: false);
        var variantDefinition = Definition(hiddenDeparture: true);
        var deviceOnlyDefinition = Definition(hiddenDeparture: false,
            deviceOnlyMiddle: true);
        Assert.Equal(HazardTransitionActivation.DeviceOnly,
            deviceOnlyDefinition.Transitions.Single(t =>
                t.SourceRegionId == "E" && t.DestinationRegionId == "F").Activation);
        Assert.True(baseDefinition.Nodes.Single(n => n.RegionId == "D").IsTerminal);
        Assert.False(variantDefinition.Nodes.Single(n => n.RegionId == "D").IsTerminal);
        Assert.Single(variantDefinition.Transitions, t => !t.PubliclyVisible);
        var baseProjection = PublicHazardEnvelopeBuilder.Create(baseDefinition);
        var variantProjection = PublicHazardEnvelopeBuilder.Create(variantDefinition);
        var deviceOnlyProjection = PublicHazardEnvelopeBuilder.Create(deviceOnlyDefinition);
        Assert.Equal(JsonDefaults.Serialize(baseProjection),
            JsonDefaults.Serialize(variantProjection));
        Assert.Equal(JsonDefaults.Serialize(baseProjection),
            JsonDefaults.Serialize(deviceOnlyProjection));

        var first = ComplementaryOriginal(RecordId, HolderId,
            2, ["A", "B", "C"], 3, "weft-a");
        var later = ComplementaryOriginal("second-original", "holder-b",
            6, ["D"], 6, "weft-b");
        var context = ComplementaryPublicContext();
        var builder = new RescuerEvidenceInputBuilder();
        RescuerEvidenceInput Input(PublicHazardEnvelope projected,
            long queryTick = 8, bool reverse = false,
            long firstReceiptTick = 7, long secondReceiptTick = 8)
        {
            var world = context with { PublicHazardEnvelope = projected };
            return builder.BuildDistinctOriginals(world, Seed, queryTick, RescuerId,
                reverse ? [later, first] : [first, later],
                reverse ? [LaterComparisonReceipt(secondReceiptTick),
                    Receipt(firstReceiptTick), Receipt(firstReceiptTick)]
                    : [Receipt(firstReceiptTick),
                        LaterComparisonReceipt(secondReceiptTick)]);
        }
        var a = Input(baseProjection);
        var b = Input(variantProjection);
        var deviceOnly = Input(deviceOnlyProjection);
        // Both the public projection and the actual inputs supplied to the
        // rescuer/candidate methods are byte-identical. Truth is not an input.
        Assert.Equal(JsonDefaults.Serialize(a.Input), JsonDefaults.Serialize(b.Input));
        Assert.Equal(JsonDefaults.Serialize(a.Input),
            JsonDefaults.Serialize(deviceOnly.Input));
        var baseline = RescuerEvidenceMethodComparison.AnalyzeMissingDeclaredPathQuestions(
            a, InteractionTimeWindow.At(2));
        Assert.Single(baseline.Candidates.Single(c => c.RegionId == "B").Questions);
        Assert.Single(baseline.Candidates.Single(c => c.RegionId == "C").Questions);
        var result = RescuerEvidenceMethodComparison.AnalyzeConditionalPublishedDwell(
            a, InteractionTimeWindow.At(2), 6);
        Assert.Equal("conditional-only", result.Status);
        Assert.Single(result.ConditionalPublicCandidates);
        Assert.StartsWith("public-conditional-early-arrival-with-stay-assumption:",
            result.ConditionalPublicCandidates[0]);
        Assert.Contains("public-BE,public-EF,public-FD",
            result.ConditionalPublicCandidates[0], StringComparison.Ordinal);
        Assert.Contains("no-unpublished-forced-departure-from-D+" +
            "public-edges-person-applicable", result.ConditionalPublicCandidates[0],
            StringComparison.Ordinal);
        // A→D also stays unresolved: the two B→D / C→D questions
        // are the selected comparison, not the entire question set.
        Assert.Single(baseline.Candidates.Single(c => c.RegionId == "A").Questions);
        Assert.Equal(3, result.BaselineQuestionCount);
        Assert.Equal(3, result.RemainingQuestionCount);
        Assert.Equal(0, result.UnconditionallyResolvedQuestionCount);
        Assert.Equal(1, result.PropagateCalls);
        Assert.Equal(true, result.PublicGraphIsDag);
        Assert.Equal(true, result.LongArithmeticBoundVerified);
        Assert.DoesNotContain("private-DG", JsonDefaults.Serialize(result));
        Assert.Equal(JsonDefaults.Serialize(result), JsonDefaults.Serialize(
            RescuerEvidenceMethodComparison.AnalyzeConditionalPublishedDwell(
                b, InteractionTimeWindow.At(2), 6)));
        // A truth-only E->F DeviceOnly transition cannot verify a Person path.
        Assert.Equal(JsonDefaults.Serialize(result), JsonDefaults.Serialize(
            RescuerEvidenceMethodComparison.AnalyzeConditionalPublishedDwell(
                deviceOnly, InteractionTimeWindow.At(2), 6)));
        Assert.Equal(JsonDefaults.Serialize(result), JsonDefaults.Serialize(
            RescuerEvidenceMethodComparison.AnalyzeConditionalPublishedDwell(
                Input(baseProjection, reverse: true), InteractionTimeWindow.At(2), 6)));
        // At tick 7 the second original exists but has not been received.
        var before = RescuerEvidenceMethodComparison.AnalyzeConditionalPublishedDwell(
            Input(baseProjection, queryTick: 7), InteractionTimeWindow.At(2), 6);
        Assert.Empty(before.ConditionalPublicCandidates);
        Assert.Equal(0, before.PropagateCalls);
        Assert.Equal("fixed-two-independent-originals-not-yet-eligible",
            before.HoldReason);
        // An already received pair that falls outside this synthetic slice
        // is not "still awaiting" its second original.
        var laterQuery = RescuerEvidenceMethodComparison.AnalyzeConditionalPublishedDwell(
            Input(baseProjection, queryTick: 9), InteractionTimeWindow.At(2), 6);
        var alteredReceipts = RescuerEvidenceMethodComparison.AnalyzeConditionalPublishedDwell(
            Input(baseProjection, secondReceiptTick: 7), InteractionTimeWindow.At(2), 6);
        foreach (var held in new[] { laterQuery, alteredReceipts })
        {
            Assert.Empty(held.ConditionalPublicCandidates);
            Assert.Equal(0, held.PropagateCalls);
            Assert.Equal("outside-fixed-calibration-slice", held.HoldReason);
            Assert.Equal(held.BaselineQuestionCount, held.RemainingQuestionCount);
            Assert.Equal(0, held.UnconditionallyResolvedQuestionCount);
        }
        // The old public method is not modified by calling the new one.
        Assert.Equal(JsonDefaults.Serialize(baseline),
            JsonDefaults.Serialize(RescuerEvidenceMethodComparison
                .AnalyzeMissingDeclaredPathQuestions(a, InteractionTimeWindow.At(2))));
    }

    [Fact]
    public void ConditionalPublishedDwellPreflightNeverExecutesUnboundedPropagation()
    {
        var original = ComplementaryPublicContext();
        var nodes = new[] { "A", "B", "C", "D", "E", "F" }
            .Select(id => new PublicHazardRegion
            {
                RegionId = id,
                PublishedAtTick = 0,
                MinimumArrivalTick = 0,
                MaximumArrivalTick = 20
            }).ToArray();
        var clean = original.PublicHazardEnvelope! with
        {
            Regions = nodes,
            Branches =
            [
                Branch("B", "E") with { MinimumTravelTicks = 1, MaximumTravelTicks = 1 },
                Branch("E", "F") with { MinimumTravelTicks = 1, MaximumTravelTicks = 1 },
                Branch("F", "D") with { MinimumTravelTicks = 1, MaximumTravelTicks = 1 }
            ]
        };
        var early = ComplementaryOriginal(RecordId, HolderId,
            2, ["A", "B", "C"], 3, "weft-a");
        var late = ComplementaryOriginal("second-original", "holder-b",
            6, ["D"], 6, "weft-b");
        var builder = new RescuerEvidenceInputBuilder();
        RescuerConditionalPublishedDwellReport Run(
            PublicHazardEnvelope envelope,
            EvidenceState firstState = EvidenceState.Confirmed,
            bool deviceOnly = false, bool sharedAncestry = false)
        {
            var world = original with { PublicHazardEnvelope = envelope };
            var first = firstState == EvidenceState.Confirmed && !deviceOnly
                ? early : ComplementaryOriginal(RecordId, HolderId,
                    2, ["A", "B", "C"], 3, "weft-a", firstState, deviceOnly);
            var second = sharedAncestry
                ? ComplementaryOriginal("second-original", "holder-b",
                    6, ["D"], 6, "weft-a") : late;
            var input = builder.BuildDistinctOriginals(world, Seed, 8, RescuerId,
                [first, second], [Receipt(7), LaterComparisonReceipt(8)]);
            return RescuerEvidenceMethodComparison.AnalyzeConditionalPublishedDwell(
                input, InteractionTimeWindow.At(2), 6);
        }
        var good = Run(clean);
        Assert.Single(good.ConditionalPublicCandidates);
        Assert.Equal(1, good.PropagateCalls);
        Assert.Equal(clean.Regions.Count, good.PreflightRegionsInspected);
        Assert.Equal(clean.Branches.Count, good.PreflightBranchesInspected);

        // A later public B minimum forces Propagate to start at B@3.
        // Reaching D@6 that way cannot certify the advertised B@2 path.
        var shiftedOrigin = clean with
        {
            Regions = clean.Regions.Select(node => node.RegionId == "B"
                ? node with { MinimumArrivalTick = 3 } : node).ToArray()
        };
        var shifted = Run(shiftedOrigin);
        Assert.Empty(shifted.ConditionalPublicCandidates);
        Assert.Equal("selected-route-origin-window-incompatible", shifted.HoldReason);
        Assert.Equal(0, shifted.PropagateCalls);
        Assert.Equal(0, shifted.UnconditionallyResolvedQuestionCount);
        Assert.Equal(shifted.BaselineQuestionCount, shifted.RemainingQuestionCount);
        Assert.Single(shifted.Baseline.Candidates.Single(c => c.RegionId == "B").Questions);
        Assert.Single(shifted.Baseline.Candidates.Single(c => c.RegionId == "C").Questions);
        Assert.Equal(JsonDefaults.Serialize(shifted),
            JsonDefaults.Serialize(Run(shiftedOrigin)));

        var cycle = clean with
        {
            Branches = [.. clean.Branches,
                Branch("F", "B") with { MinimumTravelTicks = 0,
                    MaximumTravelTicks = 0 }]
        };
        var tooWide = clean with
        {
            Branches = [.. clean.Branches,
                .. Enumerable.Range(0, 6).Select(i => Branch("B", "E") with
                { ProvenanceId = "extra-" + i,
                    MinimumTravelTicks = 10, MaximumTravelTicks = 10 })]
        };
        var overflow = clean with
        {
            Regions = [.. clean.Regions, new PublicHazardRegion
            {
                RegionId = "Z", PublishedAtTick = 0,
                MinimumArrivalTick = long.MaxValue,
                MaximumArrivalTick = long.MaxValue
            }],
            Branches = [.. clean.Branches, Branch("Z", "D") with
            { MinimumTravelTicks = 1, MaximumTravelTicks = 1 }]
        };
        var latePublic = clean with
        {
            Branches = clean.Branches.Select(edge => edge.SourceRegionId == "E"
                ? edge with { PublishedAtTick = 7 } : edge).ToArray()
        };
        var outbound = clean with
        {
            Regions = [.. clean.Regions, new PublicHazardRegion
            {
                RegionId = "G", PublishedAtTick = 0,
                MinimumArrivalTick = 0, MaximumArrivalTick = 20
            }],
            Branches = [.. clean.Branches, Branch("D", "G")]
        };
        foreach (var scenario in new[] { cycle, tooWide, overflow, latePublic, outbound })
        {
            var held = Run(scenario);
            Assert.Empty(held.ConditionalPublicCandidates);
            Assert.Equal(0, held.PropagateCalls);
            Assert.Equal(0, held.UnconditionallyResolvedQuestionCount);
            Assert.Equal(held.BaselineQuestionCount, held.RemainingQuestionCount);
            Assert.Single(held.Baseline.Candidates.Single(c => c.RegionId == "B").Questions);
            Assert.Equal(JsonDefaults.Serialize(held), JsonDefaults.Serialize(Run(scenario)));
        }
        Assert.Equal("preflight-public-cycle", Run(cycle).HoldReason);
        Assert.Equal("preflight-size-or-publication", Run(tooWide).HoldReason);
        // The 9-branch cap rejects before entry-wise validation.
        Assert.Equal(0, Run(tooWide).PreflightRegionsInspected);
        Assert.Equal(0, Run(tooWide).PreflightBranchesInspected);
        var invalidRegion = clean with
        {
            Regions = clean.Regions.Select((node, index) => index == 1
                ? node with { PublishedAtTick = 7 } : node).ToArray()
        };
        var regionHold = Run(invalidRegion);
        Assert.Equal("preflight-invalid-region", regionHold.HoldReason);
        Assert.Equal(2, regionHold.PreflightRegionsInspected);
        Assert.Equal(0, regionHold.PreflightBranchesInspected);
        Assert.Equal(0, regionHold.PropagateCalls);
        Assert.Equal(regionHold.BaselineQuestionCount, regionHold.RemainingQuestionCount);
        // Late E->F is the second entry; F->D is never inspected.
        var branchHold = Run(latePublic);
        Assert.Equal(clean.Regions.Count, branchHold.PreflightRegionsInspected);
        Assert.Equal(2, branchHold.PreflightBranchesInspected);
        Assert.Equal(0, branchHold.PropagateCalls);
        Assert.Equal(branchHold.BaselineQuestionCount, branchHold.RemainingQuestionCount);
        Assert.Equal("preflight-long-overflow-bound", Run(overflow).HoldReason);
        Assert.Equal("preflight-invalid-or-late-branch", Run(latePublic).HoldReason);
        Assert.Equal("published-departure-from-D", Run(outbound).HoldReason);
        Assert.Empty(Run(clean, firstState: EvidenceState.Conflicting)
            .ConditionalPublicCandidates);
        Assert.Empty(Run(clean, deviceOnly: true).ConditionalPublicCandidates);
        Assert.Empty(Run(clean, sharedAncestry: true).ConditionalPublicCandidates);
    }

    // A complete published route may exceed the gap at an intermediate edge.
    // Only that route is too long; other routes and Person movement stay unknown.
    [Fact]
    public void PublishedThreeHopTooLongExplainsIntermediateAndTerminalOverruns()
    {
        var original = ComplementaryPublicContext();
        var first = ComplementaryOriginal(RecordId, HolderId,
            2, ["A", "B", "C"], 3, "weft-a");
        var later = ComplementaryOriginal("second-original", "holder-b",
            5, ["D"], 6, "weft-b");
        var builder = new RescuerEvidenceInputBuilder();

        RescuerMissingDeclaredPathReport Run(bool earlyOverrun, bool reverse)
        {
            var world = original with
            {
                PublicHazardEnvelope = original.PublicHazardEnvelope! with
                {
                    Branches = [.. original.PublicHazardEnvelope!.Branches.Where(
                        edge => edge.SourceRegionId != "B" ||
                            edge.DestinationRegionId != "E"),
                        Branch("B", "E") with
                        {
                            MinimumTravelTicks = earlyOverrun ? 4 : 1,
                            MaximumTravelTicks = earlyOverrun ? 4 : 1
                        },
                        Branch("E", "F") with
                        {
                            MinimumTravelTicks = 1, MaximumTravelTicks = 1
                        },
                        Branch("F", "D") with
                        {
                            MinimumTravelTicks = earlyOverrun ? 1 : 4,
                            MaximumTravelTicks = earlyOverrun ? 1 : 4
                        }]
                }
            };
            var input = builder.BuildDistinctOriginals(world, Seed, 7,
                RescuerId, reverse ? [later, first] : [first, later],
                reverse
                    ? [LaterComparisonReceipt(7), Receipt(6), Receipt(6)]
                    : [Receipt(6), LaterComparisonReceipt(7)]);
            return RescuerEvidenceMethodComparison
                .AnalyzeMissingDeclaredPathQuestions(input, InteractionTimeWindow.At(2));
        }

        foreach (var earlyOverrun in new[] { true, false })
        {
            var report = Run(earlyOverrun, false);
            var b = report.Candidates.Single(row => row.RegionId == "B");
            Assert.Equal("unknown", b.DeclaredPathStatus);
            Assert.Single(b.Questions);
            Assert.Contains(b.PublicAlternativeExplanations,
                explanation => explanation.StartsWith(
                    "published-path-too-long:B>E>F>D:",
                    StringComparison.Ordinal) &&
                    explanation.Contains("travel=6-6:gap=3-3",
                        StringComparison.Ordinal));
            Assert.DoesNotContain(b.PublicAlternativeExplanations,
                explanation => explanation.StartsWith("formal-multihop:",
                    StringComparison.Ordinal) ||
                    explanation.StartsWith("public-path-search-incomplete:",
                        StringComparison.Ordinal));
            Assert.Equal(["A", "B", "C"],
                report.Candidates.Select(row => row.RegionId));
            Assert.Equal(JsonDefaults.Serialize(report),
                JsonDefaults.Serialize(Run(earlyOverrun, true)));
        }
    }

    // AR-14: an irrelevant dense alphabetically early subtree must not hide
    // an already published B->Y->D two-hop explanation.
    [Fact]
    public void DensePublicGraphRetainsTwoHopRegardlessOfRegionNames()
    {
        var original = ComplementaryPublicContext();
        var first = ComplementaryOriginal(RecordId, HolderId,
            2, ["A", "B", "C"], 3, "weft-a");
        var second = ComplementaryOriginal("second-original", "holder-b",
            5, ["D"], 6, "weft-b");
        var builder = new RescuerEvidenceInputBuilder();
        RescuerEvidenceInput Input(string prefix, bool reverse)
        {
            var names = Enumerable.Range(0, 7)
                .Select(i => prefix + (char)('A' + i)).ToArray();
            var clique = names.SelectMany(a => names.Where(b => a != b)
                .Select(b => Branch(a, b) with
                {
                    MinimumTravelTicks = 1,
                    MaximumTravelTicks = 1
                })).ToArray();
            var world = original with
            {
                PublicRegions = [.. original.PublicRegions,
                    .. names.Append("Y").Select((id, i) => new RegionDefinition
                    {
                        Id = id, Type = RegionType.Outdoor,
                        Center = new Position(20 + i, 0, 0, id)
                    })],
                PublicHazardEnvelope = original.PublicHazardEnvelope! with
                {
                    Branches = [.. original.PublicHazardEnvelope!.Branches,
                        Branch("B", names[0]) with
                        {
                            MinimumTravelTicks = 1, MaximumTravelTicks = 1
                        },
                        Branch("B", "Y") with
                        {
                            MinimumTravelTicks = 1, MaximumTravelTicks = 1
                        },
                        Branch("Y", "D") with
                        {
                            MinimumTravelTicks = 2, MaximumTravelTicks = 2
                        }, .. clique]
                }
            };
            return builder.BuildDistinctOriginals(world, Seed, 7, RescuerId,
                reverse ? [second, first] : [first, second],
                reverse
                    ? [LaterComparisonReceipt(7), Receipt(6), Receipt(6)]
                    : [Receipt(6), LaterComparisonReceipt(7)]);
        }
        var a = Input("A", false);
        var b = Input("Z", true);
        var report = RescuerEvidenceMethodComparison
            .AnalyzeMissingDeclaredPathQuestions(a, InteractionTimeWindow.At(2));
        var renamed = RescuerEvidenceMethodComparison
            .AnalyzeMissingDeclaredPathQuestions(b, InteractionTimeWindow.At(2));
        foreach (var item in new[] { report, renamed })
        {
            var candidate = item.Candidates.Single(row => row.RegionId == "B");
            Assert.Empty(candidate.Questions);
            Assert.Contains("formal-two-hop:B>Y>D", candidate.PublicAlternativeExplanations);
            Assert.DoesNotContain(candidate.PublicAlternativeExplanations,
                x => x.StartsWith("public-path-search-incomplete:B>D",
                    StringComparison.Ordinal));
            Assert.InRange(item.PublicTraversalBranchesInspected, 1, 4096 * 3);
            Assert.Equal(["A", "B", "C"],
                item.Candidates.Select(row => row.RegionId));
        }
        Assert.Equal(report.Candidates.Select(row => row.Questions.Count),
            renamed.Candidates.Select(row => row.Questions.Count));
        Assert.All(RescuerEvidenceMethodComparison.Compare(
            a, InteractionTimeWindow.At(2)).Methods,
            row => Assert.Equal(["A", "B", "C"], row.OuterCandidateRegionIds));
    }

    // Depth, edge budget and near-long-limit observation times are separate
    // negative cases. Unknown is never reinterpreted as an excluded candidate.
    [Fact]
    public void PublicRouteCapsAndLargeTicksDoNotInventPositivePaths()
    {
        var original = ComplementaryPublicContext();
        var first = ComplementaryOriginal(RecordId, HolderId,
            2, ["A", "B", "C"], 3, "weft-a");
        var second = ComplementaryOriginal("second-original", "holder-b",
            5, ["D"], 6, "weft-b");
        var builder = new RescuerEvidenceInputBuilder();
        RescuerMissingDeclaredPathReport Run(RescueContext world, bool reverse = false)
        {
            var input = builder.BuildDistinctOriginals(world, Seed, 7, RescuerId,
                reverse ? [second, first] : [first, second],
                reverse
                    ? [LaterComparisonReceipt(7), Receipt(6), Receipt(6)]
                    : [Receipt(6), LaterComparisonReceipt(7)]);
            return RescuerEvidenceMethodComparison
                .AnalyzeMissingDeclaredPathQuestions(input, InteractionTimeWindow.At(2));
        }

        var chainNames = Enumerable.Range(0, 9).Select(i => "T" + i).ToArray();
        var chain = new List<PublicHazardBranch>();
        var source = "B";
        foreach (var dest in chainNames.Append("D"))
        {
            chain.Add(Branch(source, dest) with
            {
                MinimumTravelTicks = 0,
                MaximumTravelTicks = 0
            });
            source = dest;
        }
        var chainWorld = original with
        {
            PublicRegions = [.. original.PublicRegions,
                .. chainNames.Select((id, i) => new RegionDefinition
                {
                    Id = id, Type = RegionType.Outdoor,
                    Center = new Position(40 + i, 0, 0, id)
                })],
            PublicHazardEnvelope = original.PublicHazardEnvelope! with
            {
                Branches = [.. original.PublicHazardEnvelope!.Branches, .. chain]
            }
        };
        var depth = Run(chainWorld);
        var depthCandidate = depth.Candidates.Single(row => row.RegionId == "B");
        Assert.Single(depthCandidate.Questions);
        Assert.Contains(depthCandidate.PublicAlternativeExplanations,
            x => x.StartsWith("public-path-search-incomplete:B>D",
                StringComparison.Ordinal));
        Assert.Equal(JsonDefaults.Serialize(depth),
            JsonDefaults.Serialize(Run(chainWorld, true)));

        var cliqueNames = Enumerable.Range(0, 7).Select(i => "Q" + i).ToArray();
        var clique = cliqueNames.SelectMany(a => cliqueNames.Where(b => a != b)
            .Select(b => Branch(a, b) with
            {
                MinimumTravelTicks = 0,
                MaximumTravelTicks = 0
            })).ToArray();
        var cappedWorld = original with
        {
            PublicRegions = [.. original.PublicRegions,
                .. cliqueNames.Select((id, i) => new RegionDefinition
                {
                    Id = id, Type = RegionType.Outdoor,
                    Center = new Position(60 + i, 0, 0, id)
                })],
            PublicHazardEnvelope = original.PublicHazardEnvelope! with
            {
                Branches = [.. original.PublicHazardEnvelope!.Branches,
                    Branch("B", cliqueNames[0]) with
                    {
                        MinimumTravelTicks = 0, MaximumTravelTicks = 0
                    }, .. clique]
            }
        };
        var cap = Run(cappedWorld);
        var capCandidate = cap.Candidates.Single(row => row.RegionId == "B");
        Assert.Single(capCandidate.Questions);
        Assert.Contains(capCandidate.PublicAlternativeExplanations,
            x => x.StartsWith("public-path-search-incomplete:B>D",
                StringComparison.Ordinal));
        Assert.InRange(cap.PublicTraversalBranchesInspected, 4096, 4096 * 3);
        Assert.Equal(JsonDefaults.Serialize(cap),
            JsonDefaults.Serialize(Run(cappedWorld, true)));
        Assert.Equal(["A", "B", "C"], cap.Candidates.Select(row => row.RegionId));

        var nearMax = long.MaxValue - 12;
        var highWorld = original with
        {
            PublicRegions = [.. original.PublicRegions, new RegionDefinition
            {
                Id = "X", Type = RegionType.Outdoor,
                Center = new Position(80, 0, 0, "X")
            }],
            PublicCausalTracingModel = new PublicCausalTracingModel
            {
                CandidateStates = [State(nearMax, ["A", "B", "C"]),
                    State(nearMax + 3, ["D", "E", "F"])]
            },
            PublicHazardEnvelope = original.PublicHazardEnvelope! with
            {
                Branches = [.. original.PublicHazardEnvelope!.Branches,
                    Branch("B", "X") with
                    {
                        MinimumTravelTicks = int.MaxValue,
                        MaximumTravelTicks = int.MaxValue
                    },
                    Branch("X", "D") with
                    {
                        MinimumTravelTicks = int.MaxValue,
                        MaximumTravelTicks = int.MaxValue
                    }]
            }
        };
        var highInput = builder.BuildDistinctOriginals(highWorld, Seed, nearMax + 6,
            RescuerId,
            [ComplementaryOriginal(RecordId, HolderId, nearMax,
                ["A", "B", "C"], nearMax + 1, "weft-a"),
             ComplementaryOriginal("second-original", "holder-b", nearMax + 3,
                ["D"], nearMax + 4, "weft-b")],
            [Receipt(nearMax + 2), LaterComparisonReceipt(nearMax + 5)]);
        var highReport = RescuerEvidenceMethodComparison
            .AnalyzeMissingDeclaredPathQuestions(highInput,
                InteractionTimeWindow.At(nearMax));
        var highCandidate = highReport.Candidates.Single(row => row.RegionId == "B");
        Assert.Single(highCandidate.Questions);
        Assert.DoesNotContain(highCandidate.PublicAlternativeExplanations,
            x => x.StartsWith("formal-two-hop:", StringComparison.Ordinal) ||
                x.StartsWith("formal-multihop:", StringComparison.Ordinal));
        // A prefix already beyond gapMax is pruned before the endpoint.
        // No positive path or false exclusion may be inferred from that cap.
        Assert.DoesNotContain(highCandidate.PublicAlternativeExplanations,
            x => x.StartsWith("formal-", StringComparison.Ordinal));
        Assert.Equal(["A", "B", "C"],
            highReport.Candidates.Select(row => row.RegionId));
    }

    // #104: the late, distinct Person trace can witness formal compatibility
    // with one *published* branch without hard-excluding unwitnessed regions.
    [Fact]
    public void DeclaredPathWitnessIsReceiptGatedAndDoesNotChangeSafeOuter()
    {
        var context = ComplementaryPublicContext();
        var first = ComplementaryOriginal(
            RecordId, HolderId, 2, ["A", "B", "C"], 3, "weft-a");
        var second = ComplementaryOriginal(
            "second-original", "holder-b", 5, ["D"], 6, "weft-b");
        var builder = new RescuerEvidenceInputBuilder();
        var before = builder.BuildDistinctOriginals(
            context, Seed, 6, RescuerId, [first, second],
            [Receipt(6), LaterComparisonReceipt(7)]);
        var after = builder.BuildDistinctOriginals(
            context, Seed, 7, RescuerId, [first, second],
            [Receipt(6), LaterComparisonReceipt(7)]);
        var earlier = RescuerEvidenceMethodComparison.AnalyzeDeclaredPathCompatibility(
            before, InteractionTimeWindow.At(2));
        var later = RescuerEvidenceMethodComparison.AnalyzeDeclaredPathCompatibility(
            after, InteractionTimeWindow.At(2));
        Assert.Equal([RecordId], earlier.ReceivedOriginalIds);
        Assert.DoesNotContain("second-original", earlier.ReceivedOriginalIds);
        Assert.All(earlier.Candidates, candidate =>
        {
            Assert.Equal("unavailable", candidate.Status);
            Assert.Empty(candidate.Witnesses);
        });
        Assert.Equal([RecordId, "second-original"],
            later.ReceivedOriginalIds.OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal(["A", "B", "C"], later.Candidates.Select(c => c.RegionId));
        Assert.Equal(["witnessed", "unknown", "unknown"],
            later.Candidates.Select(c => c.Status));
        var witness = Assert.Single(later.Candidates[0].Witnesses);
        Assert.Equal("A", witness.CandidateRegionId);
        Assert.Equal("D", witness.DestinationRegionId);
        Assert.Equal("declared-A-D", witness.BranchProvenanceId);
        Assert.Equal(2, witness.CompatibleEarlyTick);
        Assert.Equal(5, witness.CompatibleLaterTick);
        Assert.Equal(3, witness.MinimumTravelTicks);
        Assert.Equal(3, witness.MaximumTravelTicks);
        Assert.Equal(0, witness.BranchPublishedAtTick);
        Assert.Equal(RecordId, witness.EarlyDirectSourceId);
        Assert.Equal("second-original", witness.LaterDirectSourceId);
        Assert.NotEqual(witness.EarlyConstraintId, witness.LaterConstraintId);
        Assert.NotEqual(witness.EarlySourceObservationId, witness.LaterSourceObservationId);
        Assert.Contains("weft-a", witness.EarlySourceAncestryIds);
        Assert.Contains("weft-b", witness.LaterSourceAncestryIds);
        Assert.True(witness.IndependentSourceAncestry);
        Assert.Equal("unknown", witness.PersonTraversability);

        var oldBefore = RescuerEvidenceMethodComparison.Compare(
            before, InteractionTimeWindow.At(2));
        var oldAfter = RescuerEvidenceMethodComparison.Compare(
            after, InteractionTimeWindow.At(2));
        Assert.Equal(4, oldAfter.Methods.Count);
        Assert.All(oldAfter.Methods, method =>
        {
            Assert.Equal(["A", "B", "C"], method.OuterCandidateRegionIds);
            Assert.Null(method.PreferredCandidateRegionIds);
        });
        Assert.All(oldBefore.Methods, method =>
            Assert.Equal(["A", "B", "C"], method.OuterCandidateRegionIds));
    }

    [Fact]
    public void DeclaredPathPartialFutureAndDeviceBranchesRemainUnknown()
    {
        var context = ComplementaryPublicContext();
        var first = ComplementaryOriginal(
            RecordId, HolderId, 2, ["A", "B", "C"], 3, "weft-a");
        var second = ComplementaryOriginal(
            "second-original", "holder-b", 5, ["D"], 6, "weft-b");
        var builder = new RescuerEvidenceInputBuilder();
        RescuerDeclaredPathCompatibilityReport Diagnostic(RescueContext source) =>
            RescuerEvidenceMethodComparison.AnalyzeDeclaredPathCompatibility(
                builder.BuildDistinctOriginals(source, Seed, 7, RescuerId,
                    [first, second], [Receipt(6), LaterComparisonReceipt(7)]),
                InteractionTimeWindow.At(2));
        var partial = context with
        {
            PublicHazardEnvelope = context.PublicHazardEnvelope! with
            {
                Branches = [Branch("B", "E")]
            }
        };
        Assert.All(Diagnostic(partial).Candidates, c =>
        {
            Assert.Equal("unknown", c.Status);
            Assert.Empty(c.Witnesses);
        });
        var future = context with
        {
            PublicHazardEnvelope = context.PublicHazardEnvelope! with
            {
                Branches = [Branch("A", "D") with { PublishedAtTick = 8 }]
            }
        };
        var atSeven = Diagnostic(future);
        Assert.Equal(0, atSeven.PublicBranchesInspected);
        Assert.All(atSeven.Candidates, c => Assert.Empty(c.Witnesses));
        // The existing Compare future-publication guard is not weakened.
        var notProjected = builder.BuildDistinctOriginals(
            future, Seed, 7, RescuerId, [first, second],
            [Receipt(6), LaterComparisonReceipt(7)]);
        Assert.Throws<ArgumentException>(() =>
            RescuerEvidenceMethodComparison.Compare(
                notProjected, InteractionTimeWindow.At(2)));
        var projected = future with
        {
            PublicHazardEnvelope = context.PublicHazardEnvelope! with { Branches = [] }
        };
        var comparison = RescuerEvidenceMethodComparison.Compare(
            builder.BuildDistinctOriginals(projected, Seed, 7, RescuerId,
                [first, second], [Receipt(6), LaterComparisonReceipt(7)]),
            InteractionTimeWindow.At(2));
        Assert.All(comparison.Methods, row =>
            Assert.Contains("B", row.OuterCandidateRegionIds));

        // HazardDriven's public projection omits Activation, which can have
        // been DeviceOnly. A matching branch never proves Person traversability.
        Assert.Equal("unknown", Assert.Single(
            Diagnostic(context).Candidates[0].Witnesses).PersonTraversability);
        var device = ComplementaryOriginal(
            "second-original", "holder-b", 5, ["D"], 6, "weft-b",
            deviceOnly: true);
        var deviceInput = builder.BuildDistinctOriginals(
            context, Seed, 7, RescuerId, [first, device],
            [Receipt(6), LaterComparisonReceipt(7)]);
        Assert.All(RescuerEvidenceMethodComparison.AnalyzeDeclaredPathCompatibility(
            deviceInput, InteractionTimeWindow.At(2)).Candidates,
            candidate =>
            {
                Assert.Equal("unavailable", candidate.Status);
                Assert.Empty(candidate.Witnesses);
            });

        // Receiving an unrelated Person observation is not target evidence.
        var unrelated = ComplementaryOriginal(
            "second-original", "holder-b", 5, ["D"], 6, "weft-b",
            unrelatedTarget: true);
        var unrelatedInput = builder.BuildDistinctOriginals(
            context, Seed, 7, RescuerId, [first, unrelated],
            [Receipt(6), LaterComparisonReceipt(7)]);
        Assert.Equal(2, unrelatedInput.ReceivedRecords.Count);
        Assert.All(RescuerEvidenceMethodComparison.AnalyzeDeclaredPathCompatibility(
            unrelatedInput, InteractionTimeWindow.At(2)).Candidates,
            candidate => Assert.Equal("unavailable", candidate.Status));

        // Even two receipts cannot imply two usable target-Person sources
        // when the earlier original contains only Device evidence.
        var earlyDevice = ComplementaryOriginal(
            RecordId, HolderId, 2, ["A", "B", "C"], 3, "weft-a",
            deviceOnly: true);
        var firstDeviceInput = builder.BuildDistinctOriginals(
            context, Seed, 7, RescuerId, [earlyDevice, second],
            [Receipt(6), LaterComparisonReceipt(7)]);
        Assert.All(RescuerEvidenceMethodComparison.AnalyzeDeclaredPathCompatibility(
            firstDeviceInput, InteractionTimeWindow.At(2)).Candidates,
            candidate => Assert.Equal("unavailable", candidate.Status));
    }

    [Fact]
    public void DeclaredPathCandidateStateWindowMustContainWitnessTick()
    {
        var context = ComplementaryPublicContext();
        var early = ComplementaryOriginal(
            RecordId, HolderId, 2, ["A", "B", "C"], 3, "weft-a");
        var late = ComplementaryOriginal(
            "second-original", "holder-b", 5, ["D"], 6, "weft-b");
        var target = new InteractionTimeWindow(2, 4);
        var builder = new RescuerEvidenceInputBuilder();

        var onlyLaterA = context with
        {
            PublicCausalTracingModel = new PublicCausalTracingModel
            {
                CandidateStates = [
                    State(2, ["B", "C"]),
                    State(4, ["A"]),
                    State(5, ["D", "E", "F"])]
            }
        };
        RescuerDeclaredPathCompatibilityReport Analyze(RescueContext source) =>
            RescuerEvidenceMethodComparison.AnalyzeDeclaredPathCompatibility(
                builder.BuildDistinctOriginals(source, Seed, 7, RescuerId,
                    [early, late], [Receipt(6), LaterComparisonReceipt(7)]),
                target);

        var incompatible = Analyze(onlyLaterA);
        Assert.Equal(["A", "B", "C"],
            incompatible.Candidates.Select(candidate => candidate.RegionId));
        Assert.All(incompatible.Candidates, candidate =>
        {
            Assert.Equal("unknown", candidate.Status);
            Assert.Empty(candidate.Witnesses);
        });

        var earlyA = onlyLaterA with
        {
            PublicCausalTracingModel = new PublicCausalTracingModel
            {
                CandidateStates = [
                    State(2, ["A", "B", "C"]),
                    State(4, ["A"]),
                    State(5, ["D", "E", "F"])]
            }
        };
        var compatible = Analyze(earlyA);
        var a = compatible.Candidates.Single(candidate => candidate.RegionId == "A");
        Assert.Equal("witnessed", a.Status);
        var witness = Assert.Single(a.Witnesses);
        Assert.Equal(2, witness.CompatibleEarlyTick);
        Assert.Equal(5, witness.CompatibleLaterTick);
    }

    [Fact]
    public void DeclaredPathPreservesAncestryConflictOrderAndTime()
    {
        var context = ComplementaryPublicContext();
        var first = ComplementaryOriginal(
            RecordId, HolderId, 2, ["A", "B", "C"], 3, "weft-a");
        var builder = new RescuerEvidenceInputBuilder();
        RescuerDeclaredPathCompatibilityReport Diagnostic(
            DeviceHeldInteractionRecord second, bool reverse = false)
        {
            var originals = reverse
                ? new[] { second, first } : new[] { first, second };
            var receipts = reverse
                ? new[] { LaterComparisonReceipt(7), Receipt(6), Receipt(6) }
                : new[] { Receipt(6), LaterComparisonReceipt(7) };
            return RescuerEvidenceMethodComparison.AnalyzeDeclaredPathCompatibility(
                builder.BuildDistinctOriginals(
                    context, Seed, 7, RescuerId, originals, receipts),
                InteractionTimeWindow.At(2));
        }
        var second = ComplementaryOriginal(
            "second-original", "holder-b", 5, ["D"], 6, "weft-b");
        var normal = Diagnostic(second);
        var reversed = Diagnostic(second, true);
        Assert.Equal(
            System.Text.Json.JsonSerializer.Serialize(normal),
            System.Text.Json.JsonSerializer.Serialize(reversed));
        var shared = Diagnostic(ComplementaryOriginal(
            "second-original", "holder-b", 5, ["D"], 6, "weft-a"));
        Assert.False(Assert.Single(shared.Candidates[0].Witnesses)
            .IndependentSourceAncestry);
        Assert.Equal("witnessed", shared.Candidates[0].Status);

        var uncertain = Diagnostic(ComplementaryOriginal(
            "second-original", "holder-b", 5, ["D"], 6, "weft-b",
            EvidenceState.SupportedButUncertain));
        Assert.Empty(uncertain.Candidates[0].Witnesses);
        Assert.Equal("unknown", uncertain.Candidates[0].Status);
        var conflicting = Diagnostic(ComplementaryOriginal(
            "second-original", "holder-b", 5, ["D"], 6, "weft-b",
            EvidenceState.Conflicting));
        Assert.Empty(conflicting.Candidates[0].Witnesses);
        Assert.Equal("conflict", conflicting.Candidates[0].Status);
        var tooEarly = Diagnostic(ComplementaryOriginal(
            "second-original", "holder-b", 4, ["D"], 6, "weft-b"));
        Assert.Empty(tooEarly.Candidates[0].Witnesses);
        Assert.Equal("unknown", tooEarly.Candidates[0].Status);
    }

    // AR-9: a later observation that would inform a closed-world traversal
    // must not be declared useless because the public output has no preferred
    // standing. The published envelope is not a complete transition graph.
    [Fact]
    public void LaterTraceRetainsUnmeasuredSupportDespiteUnchangedHardCandidates()
    {
        var first = ComplementaryOriginal(
            RecordId, HolderId, 2, ["A", "B", "C"], 3, "weft-a");
        var second = ComplementaryOriginal(
            "second-original", "holder-b", 5, ["D"], 6, "weft-b");
        var report = RescuerEvidenceMethodComparison.Run(
            ComplementaryPublicContext(), Seed, RescuerId, [first, second],
            [Receipt(6), LaterComparisonReceipt(7)],
            InteractionTimeWindow.At(2), [5, 6, 7], 7);
        var map = new[]
        {
            new PublicSearchUnitMapping("A", 2, ["U1"], true, "catalog", 0),
            new PublicSearchUnitMapping("B", 2, ["U2"], true, "catalog", 0),
            new PublicSearchUnitMapping("C", 2, ["U1"], true, "catalog", 0)
        };
        var result = RescuerEvidenceMethodComparison.AnalyzePairedHistoricalSearchUnits(
            report, ["A", "B", "C"], ["U1", "U2"], map);
        Assert.Equal("available", result.Status);
        Assert.All(result.Methods, method =>
        {
            var addedLater = method.Marginals.Single(margin =>
                margin.AddedOriginalId == "second-original");
            Assert.Equal("available", addedLater.UnitComparisonStatus);
            Assert.Empty(addedLater.ExcludedOuterRegionIds!);
            Assert.Empty(addedLater.ReappearedOuterRegionIds!);
            Assert.Empty(addedLater.RemovedKnownUnitIds!);
            Assert.Empty(addedLater.AddedKnownUnitIds!);
            Assert.NotEqual(false, addedLater.SameUncertainCandidates);
            Assert.Null(addedLater.SamePreferredCandidates);
            Assert.Null(method.Conditions[1].PublicMethod!.PreferredCandidateRegionIds);
            Assert.Null(method.Conditions[3].PublicMethod!.PreferredCandidateRegionIds);
        });
    }

    // AR-9 positive control: addition must be capable of reducing hard outer
    // and published units when the second observation directly covers target tick.
    [Fact]
    public void SameTickSecondTraceCanReduceOuterAndKnownUnits()
    {
        var first = ComplementaryOriginal(
            RecordId, HolderId, 2, ["A", "B"], 3, "weft-a");
        var second = ComplementaryOriginal(
            "second-original", "holder-b", 2, ["B", "C"], 6, "weft-b");
        var report = RescuerEvidenceMethodComparison.Run(
            ComplementaryPublicContext(), Seed, RescuerId, [first, second],
            [Receipt(6), LaterComparisonReceipt(7)],
            InteractionTimeWindow.At(2), [5, 6, 7], 7);
        var map = new[]
        {
            new PublicSearchUnitMapping("A", 2, ["U1"], true, "catalog", 0),
            new PublicSearchUnitMapping("B", 2, ["U2"], true, "catalog", 0),
            new PublicSearchUnitMapping("C", 2, ["U1"], true, "catalog", 0)
        };
        var result = RescuerEvidenceMethodComparison.AnalyzePairedHistoricalSearchUnits(
            report, ["A", "B", "C"], ["U1", "U2"], map);
        Assert.Equal("available", result.Status);
        foreach (var name in new[]
            { "distributed-reconstruction", "distributed-reconstruction-local" })
        {
            var method = result.Methods.Single(row => row.Method == name);
            var addedSecond = method.Marginals.Single(margin =>
                margin.AddedOriginalId == "second-original");
            Assert.Equal("available", addedSecond.UnitComparisonStatus);
            Assert.Equal(["A"], addedSecond.ExcludedOuterRegionIds);
            Assert.Equal(["U1"], addedSecond.RemovedKnownUnitIds);
            Assert.Empty(addedSecond.ReappearedOuterRegionIds!);
            Assert.Empty(addedSecond.AddedKnownUnitIds!);
            Assert.Equal(["B"], method.Conditions[3].PublicMethod!.OuterCandidateRegionIds);
            Assert.Null(addedSecond.SamePreferredCandidates);
        }
    }

    [Fact]
    public void PairedHistoricalUnitsPreserveMissingPartialEmptyAndSourceBoundaries()
    {
        var report = ComplementaryRun();
        var map = new[]
        {
            new PublicSearchUnitMapping("A", 2, ["U1", "U2"], true, "catalog", 0),
            new PublicSearchUnitMapping("B", 2, ["U2"], false, "partial", 0),
            new PublicSearchUnitMapping("C", 2, ["U3"], true, "late", 8)
        };
        var partial = RescuerEvidenceMethodComparison.AnalyzePairedHistoricalSearchUnits(
            report, ["A", "B", "C"], ["U1", "U2", "U3"], map);
        Assert.Equal("partial", partial.Status);
        var local = partial.Methods.Single(method =>
            method.Method == "distributed-reconstruction-local");
        Assert.Equal("partial", local.Conditions[3].Status);
        Assert.Equal(["B"], local.Conditions[3].UnitProjection.UnresolvedRegionIds);
        Assert.Equal("partial", local.Marginals[0].UnitComparisonStatus);
        Assert.Null(local.Marginals[0].RemovedKnownUnitIds);
        Assert.Empty(local.Marginals[0].ExcludedOuterRegionIds!);

        var missing = report with
        {
            SameCutoffAblations = report.SameCutoffAblations.Select((row, index) =>
                index == 1 ? row with
                {
                    Methods = row.Methods.Where(method =>
                        method.Method != "distributed-reconstruction-local").ToArray()
                } : row).ToArray()
        };
        var validMap = map.Select(mapping => mapping with
        {
            IsComplete = true,
            AvailableTick = 0
        }).ToArray();
        var missingResult = RescuerEvidenceMethodComparison.AnalyzePairedHistoricalSearchUnits(
            missing, ["A", "B", "C"], ["U1", "U2", "U3"], validMap);
        var missingLocal = missingResult.Methods.Single(method =>
            method.Method == "distributed-reconstruction-local");
        Assert.Equal("unavailable", missingLocal.Conditions[1].Status);
        Assert.Null(missingLocal.Marginals[0].ExcludedOuterRegionIds);
        Assert.Equal("available", missingLocal.Marginals[1].UnitComparisonStatus);
        var duplicate = report with
        {
            SameCutoffAblations = report.SameCutoffAblations.Select((row, index) =>
                index == 1 ? row with
                {
                    Methods = row.Methods.Concat([row.Methods[0]]).ToArray()
                } : row).ToArray()
        };
        Assert.Equal("unavailable",
            RescuerEvidenceMethodComparison.AnalyzePairedHistoricalSearchUnits(
                duplicate, ["A", "B", "C"], ["U1", "U2", "U3"], validMap)
            .Methods.Single(method => method.Method == "public-prior")
            .Conditions[1].Status);
        Assert.Equal("unavailable",
            RescuerEvidenceMethodComparison.AnalyzePairedHistoricalSearchUnits(
                report with { SameCutoffAblations = report.SameCutoffAblations.Skip(1).ToArray() },
                ["A", "B", "C"], ["U1", "U2", "U3"], validMap).Status);
        var empty = report with
        {
            SameCutoffAblations = report.SameCutoffAblations.Select((row, index) =>
                index == 3 ? row with
                {
                    Methods = row.Methods.Select(method =>
                        method.Method == "public-prior" ? method with
                        {
                            OuterCandidateRegionIds = [],
                            CandidateCount = 0,
                            CandidateReduction = method.BaselineCandidateCount
                        } : method).ToArray()
                } : row).ToArray()
        };
        var emptyResult = RescuerEvidenceMethodComparison.AnalyzePairedHistoricalSearchUnits(
            empty, ["A", "B", "C"], ["U1", "U2", "U3"], validMap);
        var emptyPrior = emptyResult.Methods.Single(method => method.Method == "public-prior");
        Assert.Equal("empty-candidates", emptyPrior.Conditions[3].Status);
        Assert.Empty(emptyPrior.Conditions[3].UnitProjection.KnownUnitIds);
        Assert.Equal("empty-candidates", emptyPrior.Marginals[0].UnitComparisonStatus);
        Assert.Null(emptyPrior.Marginals[0].RemovedKnownUnitIds);

        var shared = RescuerEvidenceMethodComparison.AnalyzePairedHistoricalSearchUnits(
            ComplementaryRun(secondAncestry: "weft-a"),
            ["A", "B", "C"], ["U1", "U2", "U3"], validMap);
        var sharedBoth = shared.Methods.Single(method =>
            method.Method == "distributed-reconstruction-local").Conditions[3];
        Assert.True(sharedBoth.PublicMethod!.UsedSourceGroupCount <=
            sharedBoth.PublicMethod.AvailableSourceGroupCount);
        Assert.Equal(1, sharedBoth.PublicMethod.AvailableSourceGroupCount);

        var uncertain = RescuerEvidenceMethodComparison.AnalyzePairedHistoricalSearchUnits(
            ComplementaryRun(firstState: EvidenceState.SupportedButUncertain),
            ["A", "B", "C"], ["U1", "U2", "U3"], validMap);
        Assert.NotNull(uncertain.Methods.Single(method =>
            method.Method == "distributed-reconstruction").Conditions[3]
            .PublicMethod!.UncertainCandidateRegionIds);
        var device = RescuerEvidenceMethodComparison.AnalyzePairedHistoricalSearchUnits(
            ComplementaryRun(deviceOnly: true),
            ["A", "B", "C"], ["U1", "U2", "U3"], validMap);
        Assert.Equal("available", device.Status);
        Assert.Throws<InvalidDataException>(() =>
            RescuerEvidenceMethodComparison.AnalyzePairedHistoricalSearchUnits(
                report, ["A", "B", "C"], ["U1", "U1"], validMap));
    }

    [Fact]
    public void ComplementaryReceiptTimingSeparatesTemporaryLossFromFinalConvergence()
    {
        var (early, late) = ComplementaryTimingRuns();
        var comparison = RescuerEvidenceMethodComparison.AnalyzeReceiptDecisionTiming(early, late);
        Assert.Equal("available", comparison.Status);
        Assert.Equal([5L, 6L, 7L, 8L], comparison.QueryTicks);
        Assert.Equal(4, comparison.Methods.Count);
        Assert.Equal([0, 1, 2, 2], early.ActualTimeline.Select(row => row.ReceivedOriginalCount));
        Assert.Equal([0, 1, 1, 2], late.ActualTimeline.Select(row => row.ReceivedOriginalCount));
        Assert.Equal(JsonDefaults.Serialize(early.ActualTimeline[0]),
            JsonDefaults.Serialize(late.ActualTimeline[0]));
        Assert.Equal(JsonDefaults.Serialize(early.ActualTimeline[1]),
            JsonDefaults.Serialize(late.ActualTimeline[1]));
        Assert.DoesNotContain("second-original", late.ActualTimeline[2].ReceivedOriginalIds);
        Assert.Equal(JsonDefaults.Serialize(early.ActualTimeline[3]),
            JsonDefaults.Serialize(late.ActualTimeline[3]));
        Assert.Equal(JsonDefaults.Serialize(early.SameCutoffAblations),
            JsonDefaults.Serialize(late.SameCutoffAblations));

        var prior = comparison.Methods.Single(row => row.Method == "public-prior");
        Assert.Null(prior.FirstCandidateDifferentTick);
        Assert.All(prior.Timeline, row => Assert.True(row.SameOuterCandidates));
        var generation = comparison.Methods.Single(row =>
            row.Method == "distributed-reconstruction");
        Assert.Null(generation.FirstCandidateDifferentTick);
        var local = comparison.Methods.Single(row =>
            row.Method == "distributed-reconstruction-local");
        // Old tick-7 candidate loss relied on an incomplete path list.
        // Different receipt times still change the sources and work.
        Assert.Null(local.FirstCandidateDifferentTick);
        Assert.Null(local.FirstCandidateConvergenceTick);
        Assert.Equal(7, local.FirstMethodResultDifferentTick);
        Assert.Equal(8, local.FirstMethodResultConvergenceTick);
        var tick7 = local.Timeline.Single(row => row.QueryTick == 7);
        Assert.Equal(["A", "B"], tick7.EarlyMethod!.OuterCandidateRegionIds);
        Assert.Equal(["A", "B"], tick7.LateMethod!.OuterCandidateRegionIds);
        Assert.Empty(tick7.LateOnlyCandidateRegionIds!);
        Assert.Empty(tick7.EarlyOnlyCandidateRegionIds!);
        Assert.Equal(0, tick7.EarlyMinusLateCandidateCount);
        Assert.True(tick7.EarlyCandidatesSubsetOfLate);
        Assert.True(tick7.LateCandidatesSubsetOfEarly);
        Assert.Equal([RecordId, "second-original"], tick7.EarlyReceivedOriginalIds);
        Assert.Equal([RecordId], tick7.LateReceivedOriginalIds);
        Assert.Equal(2, tick7.EarlyAvailableSourceGroupCount);
        Assert.Equal(1, tick7.LateAvailableSourceGroupCount);
        Assert.Equal(2, tick7.EarlyMethod.UsedSourceGroupCount);
        Assert.Equal(1, tick7.LateMethod.UsedSourceGroupCount);
        Assert.Equal([RecordId], tick7.EarlyMethod.GenerationDirectSourceIds);
        Assert.Equal(["second-original"], tick7.EarlyMethod.TraversalDirectSourceIds);
        Assert.Empty(tick7.LateMethod.TraversalDirectSourceIds);
        Assert.Equal(4, tick7.EarlyMethod.GenerationSteps);
        Assert.Equal(2, tick7.EarlyMethod.ForwardTransitionSteps);
        Assert.Equal(2, tick7.EarlyMethod.BackwardTransitionSteps);
        Assert.Equal(1, tick7.EarlyMethod.EvidenceRecordsInspected);
        Assert.Equal(5, tick7.EarlyMethod.PublicRelationsInspected);
        Assert.Equal(0, tick7.LateMethod.EvidenceRecordsInspected);
        Assert.Equal(4, tick7.LateMethod.PublicRelationsInspected);
        var backward = comparison.Methods.Single(row =>
            row.Method == "known-relation-backward");
        Assert.Equal(["A", "B", "C"], backward.Timeline[2].EarlyMethod!.OuterCandidateRegionIds);
        Assert.Equal(["A", "B", "C"], backward.Timeline[2].LateMethod!.OuterCandidateRegionIds);
        Assert.Null(backward.FirstCandidateDifferentTick);
        Assert.Null(backward.FirstCandidateConvergenceTick);
        Assert.All(comparison.Methods, item =>
            Assert.True(item.Timeline[3].SameMethodResult));
        Assert.DoesNotContain("SyntheticTruth", JsonDefaults.Serialize(comparison));
    }

    [Fact]
    public void ComplementaryReceiptTimingIsDeterministicAndPreservesAncestryAndUncertainty()
    {
        var (a, b) = ComplementaryTimingRuns();
        var (reversedA, reversedB) = ComplementaryTimingRuns(reverse: true);
        Assert.Equal(JsonDefaults.Serialize(
                RescuerEvidenceMethodComparison.AnalyzeReceiptDecisionTiming(a, b)),
            JsonDefaults.Serialize(
                RescuerEvidenceMethodComparison.AnalyzeReceiptDecisionTiming(
                    reversedA, reversedB)));
        var (sharedEarly, sharedLate) = ComplementaryTimingRuns(ancestry: "weft-a");
        var shared = RescuerEvidenceMethodComparison.AnalyzeReceiptDecisionTiming(
            sharedEarly, sharedLate);
        var sharedLocal = shared.Methods.Single(row =>
            row.Method == "distributed-reconstruction-local").Timeline[2];
        Assert.Equal(1, sharedLocal.EarlyAvailableSourceGroupCount);
        Assert.Equal(1, sharedLocal.EarlyMethod!.UsedSourceGroupCount);
        foreach (var state in new[] { EvidenceState.SupportedButUncertain, EvidenceState.Conflicting })
        {
            var (uncertainEarly, uncertainLate) = ComplementaryTimingRuns(firstState: state);
            var result = RescuerEvidenceMethodComparison.AnalyzeReceiptDecisionTiming(
                uncertainEarly, uncertainLate);
            Assert.Equal("available", result.Status);
            var row = result.Methods.Single(item =>
                item.Method == "distributed-reconstruction").Timeline[3];
            Assert.NotNull(row.EarlyMethod!.UncertainCandidateRegionIds);
            Assert.NotNull(row.EarlyMethod.ConflictedCandidateRegionIds);
        }
        var (deviceEarly, deviceLate) = ComplementaryTimingRuns(deviceOnly: true);
        var device = RescuerEvidenceMethodComparison.AnalyzeReceiptDecisionTiming(
            deviceEarly, deviceLate);
        Assert.Empty(device.Methods.Single(item =>
            item.Method == "distributed-reconstruction").Timeline[2]
            .EarlyMethod!.GenerationDirectSourceIds);
    }

    [Fact]
    public void ComplementaryReceiptTimingMarksMissingAndIncompatibleRowsUnavailable()
    {
        var (early, late) = ComplementaryTimingRuns();
        var tick7 = late.ActualTimeline[2];
        var missing = late with
        {
            ActualTimeline = late.ActualTimeline.Select((row, index) =>
                index == 2 ? row with
                {
                    Methods = row.Methods.Where(method =>
                        method.Method != "distributed-reconstruction-local").ToArray()
                } : row).ToArray()
        };
        var result = RescuerEvidenceMethodComparison.AnalyzeReceiptDecisionTiming(
            early, missing);
        Assert.Equal("partial", result.Status);
        var local = result.Methods.Single(method =>
            method.Method == "distributed-reconstruction-local");
        Assert.Equal("partial", local.Status);
        Assert.Equal("unavailable", local.Timeline[2].Status);
        Assert.Null(local.Timeline[2].EarlyMinusLateCandidateCount);
        Assert.Null(local.Timeline[2].SameOuterCandidates);
        Assert.Null(local.Timeline[2].EarlyMethod);
        Assert.Null(local.Timeline[2].LateMethod);
        Assert.Equal("unavailable",
            RescuerEvidenceMethodComparison.AnalyzeReceiptDecisionTiming(
                early, late with { Seed = late.Seed + 1 }).Status);
        Assert.Equal("unavailable",
            RescuerEvidenceMethodComparison.AnalyzeReceiptDecisionTiming(
                early, late with
                {
                    ActualTimeline = late.ActualTimeline
                        .Where(row => row.QueryTick != 7).ToArray()
                }).Status);
        var emptySet = tick7 with
        {
            Methods = tick7.Methods.Select(item =>
                item.Method == "public-prior"
                    ? item with { OuterCandidateRegionIds = [], CandidateCount = 0 }
                    : item).ToArray()
        };
        var withEmpty = late with
        {
            ActualTimeline = late.ActualTimeline.Select((row, index) =>
                index == 2 ? emptySet : row).ToArray()
        };
        var zero = RescuerEvidenceMethodComparison.AnalyzeReceiptDecisionTiming(
            early, withEmpty).Methods.Single(row => row.Method == "public-prior").Timeline[2];
        Assert.Equal("available", zero.Status);
        Assert.Equal(0, zero.LateMethod!.CandidateCount);
        Assert.Equal(3, zero.EarlyMethod!.CandidateCount);
    }

    // #92: preserve both original objects/world; vary only second receipt tick.
    private static (RescuerEvidenceMethodReport Early, RescuerEvidenceMethodReport Late)
        ContradictoryRevisionRuns(
            bool reversed = false,
            string secondAncestry = "other-upstream",
            EvidenceState firstState = EvidenceState.Confirmed,
            bool deviceOnly = false)
    {
        var context = MultiHolderContext();
        var first = Record(state: firstState, deviceOnly: deviceOnly);
        var second = DistinctRecord(["B"], ancestry: secondAncestry);
        var originals = reversed ? new[] { second, first } : new[] { first, second };
        var firstReceipt = Receipt(6);
        var earlySecond = DistinctReceipt(8);
        var lateSecond = DistinctReceipt(9);
        Assert.Equal(earlySecond.OriginalRecordId, lateSecond.OriginalRecordId);
        Assert.Equal(earlySecond.HolderDeviceId, lateSecond.HolderDeviceId);
        Assert.Equal(earlySecond.RescuerId, lateSecond.RescuerId);
        Assert.Equal(8, earlySecond.ReceivedAtTick);
        Assert.Equal(9, lateSecond.ReceivedAtTick);
        var early = reversed ? new[] { earlySecond, firstReceipt, firstReceipt } :
            new[] { firstReceipt, earlySecond };
        var late = reversed ? new[] { lateSecond, firstReceipt, firstReceipt } :
            new[] { firstReceipt, lateSecond };
        RescuerEvidenceMethodReport Run(InteractionRecordReceipt[] ledger) =>
            RescuerEvidenceMethodComparison.Run(context, Seed, RescuerId, originals, ledger,
                InteractionTimeWindow.At(2),
                reversed ? [9, 8, 7, 6, 5] : [5, 6, 7, 8, 9], 9);
        return (Run(early), Run(late));
    }

    [Fact]
    public void ContradictoryWeftsCompareSequentialActualDecisionsSeparateFromSameTickControl()
    {
        var (early, late) = ContradictoryRevisionRuns();
        var revised = RescuerEvidenceMethodComparison.AnalyzeDecisionRevision(early);
        var delayed = RescuerEvidenceMethodComparison.AnalyzeDecisionRevision(late);
        var sameTick = RescuerEvidenceMethodComparison.AnalyzeReceiptDecisionTiming(early, late);
        Assert.Equal("available", revised.Status);
        Assert.Equal("available", delayed.Status);
        Assert.Equal("available", sameTick.Status);
        Assert.Equal(InteractionTimeWindow.At(2), revised.TargetWindow);
        Assert.Equal([5L, 6L, 7L, 8L, 9L], revised.QueryTicks);
        Assert.Equal([0, 1, 1, 2, 2],
            early.ActualTimeline.Select(x => x.ReceivedOriginalCount));
        Assert.Equal([0, 1, 1, 1, 2],
            late.ActualTimeline.Select(x => x.ReceivedOriginalCount));
        Assert.Equal(4, revised.Methods.Count);
        Assert.All(revised.Methods, method =>
        {
            Assert.Equal("available", method.Status);
            Assert.Equal(4, method.Timeline.Count);
            Assert.All(method.Timeline, change =>
            {
                Assert.True(change.BeforeQueryTick < change.AfterQueryTick);
                Assert.Equal(change.BeforeMethod!.OuterCandidateRegionIds.Except(
                    change.AfterMethod!.OuterCandidateRegionIds, StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal), change.ExcludedCandidateRegionIds);
                Assert.Equal(change.AfterMethod.OuterCandidateRegionIds.Except(
                    change.BeforeMethod.OuterCandidateRegionIds, StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal), change.ReappearedCandidateRegionIds);
                Assert.Equal(change.AfterMethod.CandidateCount - change.BeforeMethod.CandidateCount,
                    change.AfterMinusBeforeCandidateCount);
            });
        });
        var change8 = revised.Methods.Single(x => x.Method == "distributed-reconstruction")
            .Timeline.Single(x => x.AfterQueryTick == 8);
        Assert.Equal([RecordId], change8.BeforeReceivedOriginalIds);
        Assert.Equal([RecordId, "second-original"], change8.AfterReceivedOriginalIds);
        Assert.Equal(1, change8.BeforeAvailableSourceGroupCount);
        Assert.Equal(2, change8.AfterAvailableSourceGroupCount);
        // Pinned observations from the real fixed-world Run at past target tick 2.
        Assert.Equal(["A"], change8.BeforeMethod!.OuterCandidateRegionIds);
        Assert.Equal(["A", "B"], change8.AfterMethod!.OuterCandidateRegionIds);
        Assert.Equal(["B"], change8.ReappearedCandidateRegionIds);
        Assert.Empty(change8.ExcludedCandidateRegionIds!);
        Assert.Equal(1, change8.AfterMinusBeforeCandidateCount);
        Assert.True(change8.BeforeCandidatesSubsetOfAfter);
        Assert.False(change8.AfterCandidatesSubsetOfBefore);
        Assert.Equal(["A", "B"], change8.AfterMethod.ConflictedCandidateRegionIds);
        Assert.Empty(change8.BeforeMethod.ConflictedCandidateRegionIds!);
        Assert.Equal(3, change8.BeforeMethod.GenerationSteps);
        Assert.Equal(5, change8.AfterMethod.GenerationSteps);
        Assert.Equal(1, change8.BeforeMethod.EvidenceRecordsInspected);
        Assert.Equal(2, change8.AfterMethod.EvidenceRecordsInspected);
        Assert.Equal([RecordId, "second-original"],
            change8.AfterMethod.GenerationDirectSourceIds);
        Assert.Equal(8, revised.Methods.Single(x =>
            x.Method == "distributed-reconstruction").FirstReappearedTick);
        Assert.Equal(9, delayed.Methods.Single(x =>
            x.Method == "distributed-reconstruction").FirstReappearedTick);
        var localChange = revised.Methods.Single(x =>
            x.Method == "distributed-reconstruction-local").Timeline.Single(x =>
                x.AfterQueryTick == 8);
        Assert.Equal(["A"], localChange.BeforeMethod!.OuterCandidateRegionIds);
        Assert.Equal(["A", "B"], localChange.AfterMethod!.OuterCandidateRegionIds);
        Assert.Equal(["B"], localChange.ReappearedCandidateRegionIds);
        Assert.Equal(["A", "B"], localChange.AfterMethod.ConflictedCandidateRegionIds);
        Assert.Equal(3, localChange.BeforeMethod.GenerationSteps);
        Assert.Equal(5, localChange.AfterMethod.GenerationSteps);
        Assert.Equal(2, localChange.BeforeMethod.ForwardTransitionSteps);
        Assert.Equal(1, localChange.AfterMethod.ForwardTransitionSteps);
        Assert.Equal(2, localChange.BeforeMethod.BackwardTransitionSteps);
        Assert.Equal(1, localChange.AfterMethod.BackwardTransitionSteps);
        Assert.All(revised.Methods.Where(x => x.Method is
            "public-prior" or "known-relation-backward"),
            method => Assert.Null(method.FirstReappearedTick));

        Assert.DoesNotContain("second-original", change8.BeforeMethod!.GenerationDirectSourceIds);
        Assert.DoesNotContain("second-original", change8.BeforeMethod.TraversalDirectSourceIds);
        Assert.Null(change8.SamePreferredCandidates);
        Assert.NotNull(change8.SameUncertainCandidates);
        Assert.NotNull(change8.SameConflictedCandidates);
        var late8 = delayed.Methods.Single(x => x.Method == "distributed-reconstruction")
            .Timeline.Single(x => x.AfterQueryTick == 8);
        Assert.Equal([RecordId], late8.AfterReceivedOriginalIds);
        Assert.NotEqual(JsonDefaults.Serialize(early.ActualTimeline[3]),
            JsonDefaults.Serialize(late.ActualTimeline[3]));
        Assert.Equal(JsonDefaults.Serialize(early.ActualTimeline[4]),
            JsonDefaults.Serialize(late.ActualTimeline[4]));
        Assert.Equal(2, sameTick.Methods.Single(x =>
            x.Method == "distributed-reconstruction").Timeline[3].EarlyReceivedOriginalIds.Count);
        Assert.Single(sameTick.Methods.Single(x =>
            x.Method == "distributed-reconstruction").Timeline[3].LateReceivedOriginalIds);
        Assert.DoesNotContain("SyntheticTruth", JsonDefaults.Serialize(revised));
        Assert.NotEmpty(RescuerEvidenceMethodComparison.EvaluateSyntheticTruth(early, "B").Evaluations);
    }

    [Fact]
    public void ContradictoryWeftsReopenOuterInExistingTracingWithoutAssumingM1EAgreement()
    {
        var context = MultiHolderContext();
        var originals = new[] { Record(), DistinctRecord(["B"]) };
        var before = new RescuerEvidenceInputBuilder().BuildDistinctOriginals(
            context, Seed, 8, RescuerId, originals, [Receipt(6)]);
        var after = new RescuerEvidenceInputBuilder().BuildDistinctOriginals(
            context, Seed, 8, RescuerId, originals, [Receipt(6), DistinctReceipt(8)]);
        var narrow = new MultiWeftTracingStrategy().Evaluate(before.Input);
        var conflicting = new MultiWeftTracingStrategy().Evaluate(after.Input);
        Assert.Equal(["C", "D"], narrow.OuterPlausibleCandidateRegionIds);
        Assert.Equal(["C", "D"], conflicting.OuterPlausibleCandidateRegionIds);
        Assert.Equal(2, conflicting.CausalTracingResult!.SourceGroups.Count);
        Assert.Contains(conflicting.CausalTracingResult.Steps, step =>
            step.Disposition == TracingStepDisposition.MarkConflict && step.ConflictDetected);
        Assert.DoesNotContain("second-original", JsonDefaults.Serialize(before));
    }

    [Fact]
    public void DecisionRevisionPreservesAncestryOrderingUncertaintyAndDeviceBoundary()
    {
        var (early, late) = ContradictoryRevisionRuns();
        var (ordered, delayed) = ContradictoryRevisionRuns(reversed: true);
        Assert.Equal(JsonDefaults.Serialize(early), JsonDefaults.Serialize(ordered));
        Assert.Equal(JsonDefaults.Serialize(late), JsonDefaults.Serialize(delayed));
        Assert.Equal(JsonDefaults.Serialize(
            RescuerEvidenceMethodComparison.AnalyzeDecisionRevision(early)),
            JsonDefaults.Serialize(
                RescuerEvidenceMethodComparison.AnalyzeDecisionRevision(ordered)));
        var (shared, _) = ContradictoryRevisionRuns(secondAncestry: "upstream-evidence");
        var sharedChange = RescuerEvidenceMethodComparison.AnalyzeDecisionRevision(shared)
            .Methods.Single(x => x.Method == "distributed-reconstruction")
            .Timeline.Single(x => x.AfterQueryTick == 8);
        Assert.Equal(1, sharedChange.AfterAvailableSourceGroupCount);
        foreach (var state in new[] { EvidenceState.SupportedButUncertain, EvidenceState.Conflicting })
        {
            var (soft, _) = ContradictoryRevisionRuns(firstState: state);
            Assert.Equal("available",
                RescuerEvidenceMethodComparison.AnalyzeDecisionRevision(soft).Status);
        }
        var (device, _) = ContradictoryRevisionRuns(deviceOnly: true);
        var deviceChange = RescuerEvidenceMethodComparison.AnalyzeDecisionRevision(device)
            .Methods.Single(x => x.Method == "distributed-reconstruction")
            .Timeline.Single(x => x.AfterQueryTick == 6);
        Assert.Empty(deviceChange.AfterMethod!.GenerationDirectSourceIds);
    }

    [Fact]
    public void DecisionRevisionDistinguishesMissingAmbiguousAndRealEmptyCandidateSet()
    {
        var (early, _) = ContradictoryRevisionRuns();
        var absent = early with
        {
            ActualTimeline = early.ActualTimeline.Select((row, index) => index == 3
                ? row with
                {
                    Methods = row.Methods.Where(x =>
                        x.Method != "distributed-reconstruction-local").ToArray()
                }
                : row).ToArray()
        };
        var partial = RescuerEvidenceMethodComparison.AnalyzeDecisionRevision(absent);
        Assert.Equal("partial", partial.Status);
        var missing = partial.Methods.Single(x =>
            x.Method == "distributed-reconstruction-local").Timeline.Single(x =>
                x.AfterQueryTick == 8);
        Assert.Equal("unavailable", missing.Status);
        Assert.Null(missing.AfterMinusBeforeCandidateCount);
        Assert.Null(missing.AfterMethod);
        Assert.Null(missing.ReappearedCandidateRegionIds);
        var tick8 = early.ActualTimeline[3];
        var explicitEmpty = early with
        {
            ActualTimeline = early.ActualTimeline.Select((row, index) => index == 3
                ? row with
                {
                    Methods = row.Methods.Select(x => x.Method == "public-prior"
                        ? x with { OuterCandidateRegionIds = [], CandidateCount = 0 }
                        : x).ToArray()
                } : row).ToArray()
        };
        var emptyStep = RescuerEvidenceMethodComparison.AnalyzeDecisionRevision(explicitEmpty)
            .Methods.Single(x => x.Method == "public-prior").Timeline.Single(x =>
                x.AfterQueryTick == 8);
        Assert.Equal("available", emptyStep.Status);
        Assert.Empty(emptyStep.AfterMethod!.OuterCandidateRegionIds);
        Assert.NotNull(emptyStep.ExcludedCandidateRegionIds);
        Assert.Equal("unavailable",
            RescuerEvidenceMethodComparison.AnalyzeDecisionRevision(early with
            { ActualTimeline = [.. early.ActualTimeline, tick8] }).Status);
        Assert.Equal("unavailable",
            RescuerEvidenceMethodComparison.AnalyzeDecisionRevision(early with
            { ActualTimeline = early.SameCutoffAblations }).Status);
    }


    // #94: One synthetic world and the SAME two originals / receipts / ticks.
    // Null means no public transition declaration was supplied to the rescuer.
    private static (RescuerEvidenceMethodReport Known, RescuerEvidenceMethodReport Unprovided)
        PublicRelationAvailabilityRuns(
            bool reversed = false,
            string secondAncestry = "weft-b",
            EvidenceState firstState = EvidenceState.Confirmed,
            bool deviceOnly = false)
    {
        var context = ComplementaryPublicContext();
        var unprovided = context with { PublicHazardEnvelope = null };
        Assert.NotNull(context.PublicHazardEnvelope);
        Assert.Null(unprovided.PublicHazardEnvelope);
        Assert.Equal(JsonDefaults.Serialize(context.PublicCausalTracingModel),
            JsonDefaults.Serialize(unprovided.PublicCausalTracingModel));
        Assert.Equal(JsonDefaults.Serialize(context.PublicRegions),
            JsonDefaults.Serialize(unprovided.PublicRegions));
        var first = ComplementaryOriginal(
            RecordId, HolderId, 2, ["A", "B"], 3, "weft-a", firstState, deviceOnly);
        var second = ComplementaryOriginal(
            "second-original", "holder-b", 5, ["E", "F"], 6, secondAncestry);
        var originals = reversed ? new[] { second, first } : new[] { first, second };
        var receipts = reversed
            ? new[] { LaterComparisonReceipt(7), Receipt(6), Receipt(6) }
            : new[] { Receipt(6), LaterComparisonReceipt(7) };
        RescuerEvidenceMethodReport Run(RescueContext publicContext) =>
            RescuerEvidenceMethodComparison.Run(
                publicContext, Seed, RescuerId, originals, receipts,
                InteractionTimeWindow.At(2), reversed ? [7, 6, 5] : [5, 6, 7], 7);
        return (Run(context), Run(unprovided));
    }

    [Fact]
    public void MissingPublicTransitionsCompareTheSameReceivedWeftsWithoutClosingUnknownPaths()
    {
        var (known, unprovided) = PublicRelationAvailabilityRuns();
        var comparison = RescuerEvidenceMethodComparison.AnalyzePublicRelationAvailability(
            known, unprovided);
        Assert.Equal("available", comparison.Status);
        Assert.Equal([5L, 6L, 7L], comparison.QueryTicks);
        Assert.Equal(InteractionTimeWindow.At(2), comparison.TargetWindow);
        Assert.Equal(4, comparison.Methods.Count);
        Assert.Equal([0, 1, 2], known.ActualTimeline.Select(row => row.ReceivedOriginalCount));
        Assert.Equal([0, 1, 2], unprovided.ActualTimeline.Select(row => row.ReceivedOriginalCount));
        Assert.All(comparison.Methods, method =>
        {
            Assert.Equal("available", method.Status);
            Assert.Equal(3, method.Timeline.Count);
            Assert.All(method.Timeline, tick =>
            {
                Assert.Equal("available", tick.Status);
                Assert.NotNull(tick.KnownRelationMethod);
                Assert.NotNull(tick.UnprovidedRelationMethod);
                Assert.Equal(tick.ReceivedOriginalCount, tick.ReceivedOriginalIds.Count);
                Assert.Equal(tick.KnownRelationMethod!.CandidateCount -
                    tick.UnprovidedRelationMethod!.CandidateCount,
                    tick.KnownMinusUnprovidedCandidateCount);
                Assert.Equal(tick.KnownRelationMethod.OuterCandidateRegionIds.Except(
                    tick.UnprovidedRelationMethod.OuterCandidateRegionIds, StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal), tick.KnownOnlyCandidateRegionIds);
                Assert.Equal(tick.UnprovidedRelationMethod.OuterCandidateRegionIds.Except(
                    tick.KnownRelationMethod.OuterCandidateRegionIds, StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal), tick.UnprovidedOnlyCandidateRegionIds);
                Assert.Equal(tick.KnownOnlyCandidateRegionIds!.Count == 0,
                    tick.KnownCandidatesSubsetOfUnprovided);
                Assert.Equal(tick.UnprovidedOnlyCandidateRegionIds!.Count == 0,
                    tick.UnprovidedCandidatesSubsetOfKnown);
                Assert.True(tick.KnownRelationMethod.AvailableSourceGroupCount >=
                    tick.KnownRelationMethod.UsedSourceGroupCount);
                Assert.True(tick.UnprovidedRelationMethod.AvailableSourceGroupCount >=
                    tick.UnprovidedRelationMethod.UsedSourceGroupCount);
            });
        });
        var prior = comparison.Methods.Single(m => m.Method == "public-prior");
        Assert.All(prior.Timeline, tick =>
        {
            Assert.True(tick.SameOuterCandidates);
            Assert.Equal(["A", "B", "C"], tick.KnownRelationMethod!.OuterCandidateRegionIds);
            Assert.Equal(["A", "B", "C"], tick.UnprovidedRelationMethod!.OuterCandidateRegionIds);
            Assert.Empty(tick.KnownRelationMethod.GenerationDirectSourceIds);
            Assert.Empty(tick.UnprovidedRelationMethod.TraversalDirectSourceIds);
            Assert.Null(tick.SameConflictedCandidates);
        });
        var final = comparison.Methods.Single(m =>
            m.Method == "distributed-reconstruction-local").Timeline[2];
        Assert.Equal([RecordId, "second-original"], final.ReceivedOriginalIds);
        Assert.Equal(2, final.AvailableSourceGroupCount);
        Assert.Equal(["A", "B"], final.KnownRelationMethod!.OuterCandidateRegionIds);
        Assert.NotNull(final.UnprovidedRelationMethod!.UncertainCandidateRegionIds);
        Assert.Equal(4, final.KnownRelationMethod.GenerationSteps);
        Assert.DoesNotContain("SyntheticTruth", JsonDefaults.Serialize(comparison));
        Assert.Equal(JsonDefaults.Serialize(comparison), JsonDefaults.Serialize(
            RescuerEvidenceMethodComparison.AnalyzePublicRelationAvailability(
                PublicRelationAvailabilityRuns(reversed: true).Known,
                PublicRelationAvailabilityRuns(reversed: true).Unprovided)));
    }

    // #94: measured Run inputs, not the hypothetical same-cutoff ablations.
    // Existing #86's fixed 'none'/'first-only' results provide an independent
    // cross-check for the declared condition. Null is separately asserted.
    [Fact]
    public void PublicRelationAvailabilityTicksFiveAndSixPinTheActualOutcomes()
    {
        var (known, unprovided) = PublicRelationAvailabilityRuns();
        var comparison = RescuerEvidenceMethodComparison.AnalyzePublicRelationAvailability(
            known, unprovided);
        Assert.Equal("available", comparison.Status);
        var expectations = new[]
        {
            (Method: "public-prior", Tick5: new[] { "A", "B", "C" },
                Tick6: new[] { "A", "B", "C" }),
            (Method: "known-relation-backward", Tick5: new[] { "A", "B", "C" },
                Tick6: new[] { "A", "B", "C" }),
            (Method: "distributed-reconstruction", Tick5: new[] { "A", "B", "C" },
                Tick6: new[] { "A", "B" }),
            (Method: "distributed-reconstruction-local", Tick5: new[] { "A", "B", "C" },
                Tick6: new[] { "A", "B" })
        };
        foreach (var (methodName, tick5, tick6) in expectations)
        {
            var method = comparison.Methods.Single(m => m.Method == methodName);
            foreach (var (tick, expected, received) in new[]
            {
                (Tick: method.Timeline[0], Expected: tick5, Received: 0),
                (Tick: method.Timeline[1], Expected: tick6, Received: 1)
            })
            {
                Assert.Equal("available", tick.Status);
                Assert.Equal(received, tick.ReceivedOriginalCount);
                Assert.Equal(received, tick.AvailableSourceGroupCount);
                Assert.Equal(expected, tick.KnownRelationMethod!.OuterCandidateRegionIds);
                Assert.Equal(expected, tick.UnprovidedRelationMethod!.OuterCandidateRegionIds);
                Assert.True(tick.SameOuterCandidates);
                Assert.Equal(0, tick.KnownMinusUnprovidedCandidateCount);
                Assert.Empty(tick.KnownOnlyCandidateRegionIds!);
                Assert.Empty(tick.UnprovidedOnlyCandidateRegionIds!);
                var expectedStatus = methodName == "public-prior"
                    ? "last-confirmed-unavailable-public-prior"
                    : received == 0 || methodName == "known-relation-backward"
                        ? "public-model-only" : "method-executed";
                Assert.Equal(expectedStatus, tick.KnownRelationMethod.EvidenceStatus);
                Assert.Equal(tick.KnownRelationMethod.EvidenceStatus,
                    tick.UnprovidedRelationMethod.EvidenceStatus);
            }
        }

        var backward = comparison.Methods.Single(m =>
            m.Method == "known-relation-backward").Timeline;
        var local = comparison.Methods.Single(m =>
            m.Method == "distributed-reconstruction-local").Timeline;
        var generation = comparison.Methods.Single(m =>
            m.Method == "distributed-reconstruction").Timeline;
        Assert.All(backward.Take(2), tick =>
        {
            Assert.Equal(1, tick.KnownRelationMethod!.ForwardTransitionSteps);
            Assert.Equal(1, tick.KnownRelationMethod.BackwardTransitionSteps);
            Assert.Equal(2, tick.KnownRelationMethod.PublicRelationsInspected);
            Assert.Equal(0, tick.UnprovidedRelationMethod!.ForwardTransitionSteps);
            Assert.Equal(0, tick.UnprovidedRelationMethod.BackwardTransitionSteps);
        });
        Assert.Equal(1, local[0].KnownRelationMethod!.ForwardTransitionSteps);
        Assert.Equal(0, local[0].UnprovidedRelationMethod!.ForwardTransitionSteps);
        Assert.Equal(2, local[1].KnownRelationMethod!.ForwardTransitionSteps);
        Assert.Equal(0, local[1].UnprovidedRelationMethod!.ForwardTransitionSteps);
        Assert.Equal(0, local[0].KnownRelationMethod.UsedSourceGroupCount);
        Assert.Equal(1, local[1].KnownRelationMethod.UsedSourceGroupCount);
        Assert.Equal(1, local[1].UnprovidedRelationMethod.UsedSourceGroupCount);
        Assert.Equal(4, local[1].KnownRelationMethod.GenerationSteps);
        Assert.Equal(4, local[1].UnprovidedRelationMethod.GenerationSteps);
        Assert.Equal(4, generation[1].KnownRelationMethod!.GenerationSteps);
        Assert.Equal(4, generation[1].UnprovidedRelationMethod!.GenerationSteps);
        Assert.Equal([RecordId], generation[1].KnownRelationMethod.GenerationDirectSourceIds);
        Assert.Equal([RecordId], generation[1].UnprovidedRelationMethod.GenerationDirectSourceIds);
        Assert.Empty(backward[1].KnownRelationMethod!.TraversalDirectSourceIds);
        Assert.Empty(backward[1].UnprovidedRelationMethod!.TraversalDirectSourceIds);
        Assert.All(comparison.Methods, method => Assert.Equal(0,
            method.Timeline[0].KnownRelationMethod!.UsedSourceGroupCount));
    }

    // #96: preserve the original AR-1 fixture but assert the corrected
    // open-world behavior instead of the former unsafe candidate reduction.
    [Fact]
    public void PartiallyPublishedTransitionsDoNotHardExcludeUnknownOuterCandidates()
    {
        var context = ComplementaryPublicContext();
        var partial = context with
        {
            PublicHazardEnvelope = context.PublicHazardEnvelope! with
            {
                Branches = [Branch("A", "D"), Branch("B", "E")]
            }
        };
        var first = ComplementaryOriginal(
            RecordId, HolderId, 2, ["A", "B"], 3, "weft-a");
        var second = ComplementaryOriginal(
            "second-original", "holder-b", 5, ["E", "F"], 6, "weft-b");
        var records = new[] { first, second };
        var receipts = new[] { Receipt(6), LaterComparisonReceipt(7) };
        RescuerEvidenceMethodReport Run(RescueContext publicContext) =>
            RescuerEvidenceMethodComparison.Run(
                publicContext, Seed, RescuerId, records, receipts,
                InteractionTimeWindow.At(2), [5, 6, 7], 7);

        var declared = Run(context).ActualTimeline[2].Methods;
        var incomplete = Run(partial).ActualTimeline[2].Methods;
        var declaredBackward = declared.Single(m => m.Method == "known-relation-backward");
        var partialBackward = incomplete.Single(m => m.Method == "known-relation-backward");
        var partialLocal = incomplete.Single(m =>
            m.Method == "distributed-reconstruction-local");
        Assert.Equal(["A", "B", "C"], declaredBackward.OuterCandidateRegionIds);
        Assert.Equal(["A", "B", "C"], partialBackward.OuterCandidateRegionIds);
        Assert.Equal(["A", "B"], partialLocal.OuterCandidateRegionIds);
        Assert.Equal("method-executed", partialBackward.EvidenceStatus);
        Assert.Equal("method-executed", partialLocal.EvidenceStatus);
        Assert.Empty(partialLocal.ConflictedCandidateRegionIds!);
        Assert.Empty(partialLocal.UncertainCandidateRegionIds!);
        Assert.True(declaredBackward.OuterCandidateRegionIds.All(
            id => partialBackward.OuterCandidateRegionIds.Contains(id)));
        // An unprovided physical A->E path would make A plausible.
        // Neither synthetic truth nor that unprovided path is passed to Run.
        Assert.Contains("A", partialLocal.OuterCandidateRegionIds);
    }

    // AR-4: use the former unsafe zero/one/two-receipt fixture
    // as a direct safe-outer regression, without changing the inputs.
    [Fact]
    public void PartialPublicBranchesCannotNarrowWithoutEvidenceOrDiscardConfirmedRegions()
    {
        var context = ComplementaryPublicContext();
        var first = ComplementaryOriginal(
            RecordId, HolderId, 2, ["A", "B"], 3, "weft-a");
        var second = ComplementaryOriginal(
            "second-original", "holder-b", 5, ["E", "F"], 6, "weft-b");
        var originals = new[] { first, second };
        var receipts = new[] { Receipt(6), LaterComparisonReceipt(7) };
        RescuerEvidenceMethodReport Run(RescueContext publicContext) =>
            RescuerEvidenceMethodComparison.Run(
                publicContext, Seed, RescuerId, originals, receipts,
                InteractionTimeWindow.At(2), [5, 6, 7], 7);
        RescuerEvidenceMethodRow At(
            RescuerEvidenceMethodReport report, int index, string method) =>
            report.ActualTimeline[index].Methods.Single(row => row.Method == method);

        var twoBranches = Run(context with
        {
            PublicHazardEnvelope = context.PublicHazardEnvelope! with
            {
                Branches = [Branch("A", "D"), Branch("B", "E")]
            }
        });
        var aOnly = Run(context with
        {
            PublicHazardEnvelope = context.PublicHazardEnvelope! with
            {
                Branches = [Branch("A", "D")]
            }
        });
        var aAndC = Run(context with
        {
            PublicHazardEnvelope = context.PublicHazardEnvelope! with
            {
                Branches = [Branch("A", "D"), Branch("C", "F")]
            }
        });

        // No originals have arrived at tick 5: public paths cannot invent
        // evidence or produce a false 'public-model-only' reduction.
        Assert.Empty(twoBranches.ActualTimeline[0].ReceivedOriginalIds);
        Assert.Equal(0, twoBranches.ActualTimeline[0].AvailableSourceGroupCount);
        foreach (var method in new[]
        {
            "known-relation-backward",
            "distributed-reconstruction-local"
        })
        {
            var beforeReceipt = At(twoBranches, 0, method);
            Assert.Equal(["A", "B", "C"], beforeReceipt.OuterCandidateRegionIds);
            Assert.Equal(3, beforeReceipt.BaselineCandidateCount);
            Assert.Equal(0, beforeReceipt.CandidateReduction);
            Assert.Equal("public-model-only", beforeReceipt.EvidenceStatus);
            Assert.Equal(0, beforeReceipt.UsedSourceGroupCount);
            var oneBranchBeforeReceipt = At(aOnly, 0, method);
            Assert.Equal(["A", "B", "C"], oneBranchBeforeReceipt.OuterCandidateRegionIds);
            Assert.Equal(0, oneBranchBeforeReceipt.CandidateReduction);
            Assert.Equal("public-model-only", oneBranchBeforeReceipt.EvidenceStatus);
        }

        // Tick-2 {A,B} is a confirmed received observation at query tick 6;
        // missing published B routes cannot safely rule B out.
        Assert.Equal(1, aOnly.ActualTimeline[1].ReceivedOriginalCount);
        var localAtSix = At(aOnly, 1, "distributed-reconstruction-local");
        Assert.Equal(["A", "B"], localAtSix.OuterCandidateRegionIds);
        Assert.Contains("B", localAtSix.OuterCandidateRegionIds);
        Assert.Empty(localAtSix.ConflictedCandidateRegionIds!);
        var alternateLocalAtSix = At(aAndC, 1, "distributed-reconstruction-local");
        Assert.Equal(["A", "B"], alternateLocalAtSix.OuterCandidateRegionIds);
        Assert.Contains("B", alternateLocalAtSix.OuterCandidateRegionIds);
        Assert.Empty(alternateLocalAtSix.ConflictedCandidateRegionIds!);

        // Do not mistake a reversal of the prior false exclusion for
        // meaningful information arriving with the second original.
        Assert.Equal(2, aOnly.ActualTimeline[2].ReceivedOriginalCount);
        var backwardAtSix = At(aOnly, 1, "known-relation-backward");
        var backwardAtSeven = At(aOnly, 2, "known-relation-backward");
        Assert.Equal(["A", "B", "C"], backwardAtSix.OuterCandidateRegionIds);
        Assert.Equal(["A", "B", "C"], backwardAtSeven.OuterCandidateRegionIds);
        Assert.Equal(backwardAtSix.CandidateCount, backwardAtSeven.CandidateCount);
    }

    [Fact]
    public void MissingPublicTransitionsRemainSeparateFromReceiptAndAncestryBoundaries()
    {
        var (known, unprovided) = PublicRelationAvailabilityRuns(secondAncestry: "weft-a");
        var shared = RescuerEvidenceMethodComparison.AnalyzePublicRelationAvailability(
            known, unprovided);
        Assert.Equal("available", shared.Status);
        Assert.All(shared.Methods, method =>
            Assert.Equal(1, method.Timeline[2].AvailableSourceGroupCount));
        Assert.All(shared.Methods, method =>
            Assert.True(method.Timeline[2].KnownRelationMethod!.UsedSourceGroupCount <= 1));
        foreach (var state in new[] { EvidenceState.SupportedButUncertain, EvidenceState.Conflicting })
        {
            var (softKnown, softUnprovided) = PublicRelationAvailabilityRuns(firstState: state);
            var result = RescuerEvidenceMethodComparison.AnalyzePublicRelationAvailability(
                softKnown, softUnprovided);
            Assert.Equal("available", result.Status);
            Assert.NotNull(result.Methods.Single(method =>
                method.Method == "distributed-reconstruction").Timeline[2]
                .UnprovidedRelationMethod!.ConflictedCandidateRegionIds);
        }
        var (deviceKnown, deviceUnprovided) = PublicRelationAvailabilityRuns(deviceOnly: true);
        var device = RescuerEvidenceMethodComparison.AnalyzePublicRelationAvailability(
            deviceKnown, deviceUnprovided);
        Assert.Empty(device.Methods.Single(method =>
            method.Method == "distributed-reconstruction").Timeline[2]
            .KnownRelationMethod!.GenerationDirectSourceIds);
        Assert.Empty(device.Methods.Single(method =>
            method.Method == "distributed-reconstruction").Timeline[2]
            .UnprovidedRelationMethod!.GenerationDirectSourceIds);
        Assert.Empty(device.Methods[0].Timeline[0].ReceivedOriginalIds);
        Assert.DoesNotContain("second-original", JsonDefaults.Serialize(
            device.Methods[0].Timeline[1].ReceivedOriginalIds));
    }

    [Fact]
    public void MissingPublicRelationsDoNotTurnAbsentResultsIntoEmptyCandidateSets()
    {
        var (known, unprovided) = PublicRelationAvailabilityRuns();
        var missing = unprovided with
        {
            ActualTimeline = unprovided.ActualTimeline.Select((row, index) =>
                index == 2 ? row with
                {
                    Methods = row.Methods.Where(m =>
                        m.Method != "known-relation-backward").ToArray()
                } : row).ToArray()
        };
        var partial = RescuerEvidenceMethodComparison.AnalyzePublicRelationAvailability(
            known, missing);
        Assert.Equal("partial", partial.Status);
        var absent = partial.Methods.Single(m => m.Method == "known-relation-backward")
            .Timeline[2];
        Assert.Equal("unavailable", absent.Status);
        Assert.Null(absent.KnownRelationMethod);
        Assert.Null(absent.KnownMinusUnprovidedCandidateCount);
        Assert.Null(absent.KnownOnlyCandidateRegionIds);
        var duplicated = unprovided with
        {
            ActualTimeline = unprovided.ActualTimeline.Select((row, index) => index == 1
                ? row with { Methods = [.. row.Methods, row.Methods[0]] } : row).ToArray()
        };
        Assert.Equal("partial", RescuerEvidenceMethodComparison
            .AnalyzePublicRelationAvailability(known, duplicated).Status);
        var empty = unprovided with
        {
            ActualTimeline = unprovided.ActualTimeline.Select((row, index) =>
                index == 2 ? row with
                {
                    Methods = row.Methods.Select(m => m.Method == "public-prior"
                        ? m with
                        {
                            OuterCandidateRegionIds = [],
                            CandidateCount = 0,
                            CandidateReduction = m.BaselineCandidateCount
                        } : m).ToArray()
                } : row).ToArray()
        };
        var genuineEmpty = RescuerEvidenceMethodComparison.AnalyzePublicRelationAvailability(
            known, empty).Methods.Single(m => m.Method == "public-prior").Timeline[2];
        Assert.Equal("available", genuineEmpty.Status);
        Assert.Empty(genuineEmpty.UnprovidedRelationMethod!.OuterCandidateRegionIds);
        Assert.NotNull(genuineEmpty.KnownOnlyCandidateRegionIds);
        Assert.Equal("unavailable", RescuerEvidenceMethodComparison
            .AnalyzePublicRelationAvailability(known, unprovided with { Seed = Seed + 1 }).Status);
        Assert.Equal("unavailable", RescuerEvidenceMethodComparison
            .AnalyzePublicRelationAvailability(known, unprovided with
            {
                ActualTimeline = unprovided.ActualTimeline.Skip(1).ToArray()
            }).Status);
        var changedReceipt = unprovided with
        {
            ActualTimeline = unprovided.ActualTimeline.Select((row, index) => index == 2
                ? row with { ReceivedOriginalIds = [RecordId, "not-the-same-record"] }
                : row).ToArray()
        };
        Assert.Equal("unavailable", RescuerEvidenceMethodComparison
            .AnalyzePublicRelationAvailability(known, changedReceipt).Status);
    }


    // #96: same #94 Run inputs, corrected tracer; old measurements remain
    // separately in the research document. Counters are not rescue metrics.
    [Fact]
    public void PublicRelationAvailabilityDiagnosticSnapshot()
    {
        var (known, unprovided) = PublicRelationAvailabilityRuns();
        static string Standing(IReadOnlyList<string>? items) =>
            items is null ? "null" : string.Join(",", items);
        var measured = string.Join("\n", new[]
        {
            (Label: "known", Report: known),
            (Label: "unprovided", Report: unprovided)
        }.SelectMany(condition => condition.Report.ActualTimeline
            .Where(row => row.QueryTick == 7)
            .SelectMany(row => row.Methods.Select(method =>
                $"FP94 {condition.Label} {method.Method}: " +
                $"outer={Standing(method.OuterCandidateRegionIds)}; " +
                $"status={method.EvidenceStatus}; " +
                $"uncertain={Standing(method.UncertainCandidateRegionIds)}; " +
                $"conflict={Standing(method.ConflictedCandidateRegionIds)}; " +
                $"genSources={Standing(method.GenerationDirectSourceIds)}; " +
                $"traversalSources={Standing(method.TraversalDirectSourceIds)}; " +
                $"groups={row.AvailableSourceGroupCount}/{method.UsedSourceGroupCount}; " +
                $"work={method.GenerationSteps}/{method.VerificationSteps}/" +
                $"{method.ForwardTransitionSteps}/{method.BackwardTransitionSteps}/" +
                $"{method.EvidenceRecordsInspected}/{method.PublicRelationsInspected}"))));
        const string expected = """
            FP94 known public-prior: outer=A,B,C; status=last-confirmed-unavailable-public-prior; uncertain=null; conflict=null; genSources=; traversalSources=; groups=2/0; work=0/0/0/0/0/0
            FP94 known known-relation-backward: outer=A,B,C; status=method-executed; uncertain=null; conflict=null; genSources=; traversalSources=second-original; groups=2/1; work=0/0/2/2/1/5
            FP94 known distributed-reconstruction: outer=A,B; status=method-executed; uncertain=; conflict=; genSources=original-encounter-record; traversalSources=; groups=2/1; work=4/0/0/0/1/1
            FP94 known distributed-reconstruction-local: outer=A,B; status=method-executed; uncertain=; conflict=; genSources=original-encounter-record; traversalSources=second-original; groups=2/2; work=4/0/2/2/1/5
            FP94 unprovided public-prior: outer=A,B,C; status=last-confirmed-unavailable-public-prior; uncertain=null; conflict=null; genSources=; traversalSources=; groups=2/0; work=0/0/0/0/0/0
            FP94 unprovided known-relation-backward: outer=A,B,C; status=method-executed; uncertain=null; conflict=null; genSources=; traversalSources=second-original; groups=2/1; work=0/0/0/0/1/1
            FP94 unprovided distributed-reconstruction: outer=A,B; status=method-executed; uncertain=; conflict=; genSources=original-encounter-record; traversalSources=; groups=2/1; work=4/0/0/0/1/1
            FP94 unprovided distributed-reconstruction-local: outer=A,B; status=method-executed; uncertain=; conflict=; genSources=original-encounter-record; traversalSources=second-original; groups=2/2; work=4/0/0/0/1/1
            """;
        Assert.Equal(expected, measured);
    }

    // #139 / AR-31: diagnostic of a catalog-wide depth bound, not a desired
    // contract. A future tracer fix MAY eliminate the difference; this test
    // deliberately does not require that known defect to persist.
    [Fact]
    public void DisconnectedPublishedBranchCanAlterCyclicTracerSupportWithoutHardExclusion()
    {
        var warps = PublicCandidateWarpFactory.Create(
            ScenarioId, Seed, TargetId,
            [State(0, ["A", "Z"]), State(5, ["B", "C"])],
            CandidateWarpStanding.Preferred);
        var upstream = CausalTracingConstraint.Create(
            ScenarioId, Seed, "catalog-up-observation", "catalog-up-weft",
            ["catalog-up-weft"], TargetId,
            TracingConstraintSourceKind.CrossThreadObservation,
            "catalog-received-person-encounter",
            [
                new InteractionParticipant(TargetId, InteractionEntityKind.Person,
                    InteractionParticipantRole.EncounterParticipant),
                new InteractionParticipant("catalog-witness", InteractionEntityKind.Person,
                    InteractionParticipantRole.EncounterParticipant)
            ],
            TracingConstraintRole.Upstream, InteractionTimeWindow.At(0),
            InteractionTimeWindow.At(0), ["A"], 0, EvidenceState.Confirmed,
            true, false, InteractionVisibility.PublicEvidence,
            "catalog-fixture-derivation",
            [new InteractionProvenance(
                InteractionProvenanceKind.ExternalSource, "catalog-up-weft")]);
        var original = new PublicHazardEnvelope
        {
            HazardId = "catalog-depth-public",
            SourceRegionId = "A",
            PublishedAtTick = 0,
            Regions = [],
            Branches =
            [
                Branch("A", "Q") with {
                    MinimumTravelTicks = 1, MaximumTravelTicks = 1 },
                Branch("Q", "A") with {
                    MinimumTravelTicks = 1, MaximumTravelTicks = 1 },
                Branch("A", "B") with {
                    MinimumTravelTicks = 1, MaximumTravelTicks = 1 }
            ]
        };
        var expanded = original with
        {
            Branches = [.. original.Branches, Branch("X", "Y") with {
                MinimumTravelTicks = 1, MaximumTravelTicks = 1 }]
        };
        var query = new CandidateWarpAccessContext
        {
            ScenarioId = ScenarioId,
            Seed = Seed,
            TargetEntityId = TargetId,
            CurrentTick = 5,
            QueryContextId = "catalog-depth-boundary"
        };
        CausalTracingResult Trace(PublicHazardEnvelope envelope) =>
            new OperationalCausalTracer().Trace(warps, [upstream], query, envelope);
        var p0 = Trace(original);
        var p1 = Trace(expanded);

        // X has no incoming published edge from the subject-side component.
        Assert.DoesNotContain(original.Branches,
            edge => edge.SourceRegionId is "X" or "Y");
        Assert.Single(expanded.Branches, edge => edge.SourceRegionId == "X");
        Assert.Equal(JsonDefaults.Serialize(original.Branches),
            JsonDefaults.Serialize(expanded.Branches.Take(3).ToArray()));
        Assert.Equal(["B", "C"], p0.CandidateWarps.OuterPlausibleRegionIdsAt(5));
        Assert.Equal(["B", "C"], p1.CandidateWarps.OuterPlausibleRegionIdsAt(5));

        // The disconnected branch must never change tracer support,
        // preferred, audits or path IDs. No hard outer exclusion is added.
        Assert.Equal(["B", "C"], p0.CandidateWarps.PreferredRegionIdsAt(5));
        Assert.Equal(p0.CandidateWarps.PreferredRegionIdsAt(5),
            p1.CandidateWarps.PreferredRegionIdsAt(5));
        Assert.Equal(JsonDefaults.Serialize(p0), JsonDefaults.Serialize(p1));
        Assert.DoesNotContain(p0.Steps, step => step.RealTransitionTraversalOccurred);
        Assert.Equal(JsonDefaults.Serialize(p1), JsonDefaults.Serialize(Trace(expanded)));
    }

    // #141 / AR-35: one timed, received public Person constraint. This helper
    // uses the real tracer, not a duplicate path-search implementation.
    private static CausalTracingResult TraceCyclicCatalog(
        PublicHazardEnvelope envelope, long targetTick,
        string sourceRegion, IReadOnlyList<string> destinationRegions)
    {
        var warps = PublicCandidateWarpFactory.Create(
            ScenarioId, Seed, TargetId,
            [State(0, [sourceRegion, "Z"]), State(targetTick, destinationRegions)],
            CandidateWarpStanding.Preferred);
        var constraint = CausalTracingConstraint.Create(
            ScenarioId, Seed, "cycle-up-observation", "cycle-up-weft",
            ["cycle-up-weft"], TargetId,
            TracingConstraintSourceKind.CrossThreadObservation,
            "cycle-received-person-encounter",
            [
                new InteractionParticipant(TargetId, InteractionEntityKind.Person,
                    InteractionParticipantRole.EncounterParticipant),
                new InteractionParticipant("cycle-witness", InteractionEntityKind.Person,
                    InteractionParticipantRole.EncounterParticipant)
            ],
            TracingConstraintRole.Upstream, InteractionTimeWindow.At(0),
            InteractionTimeWindow.At(0), [sourceRegion], 0, EvidenceState.Confirmed,
            true, false, InteractionVisibility.PublicEvidence,
            "cycle-fixture-derivation",
            [new InteractionProvenance(
                InteractionProvenanceKind.ExternalSource, "cycle-up-weft")]);
        var query = new CandidateWarpAccessContext
        {
            ScenarioId = ScenarioId,
            Seed = Seed,
            TargetEntityId = TargetId,
            CurrentTick = targetTick,
            QueryContextId = "cycle-scope-boundary"
        };
        return new OperationalCausalTracer().Trace(warps, [constraint], query, envelope);
    }

    [Theory]
    [InlineData(5L)]
    [InlineData(6L)]
    public void UnobservedCyclicTimeFillingCannotPromotePreferredAtEitherParity(long tick)
    {
        var baseModel = new PublicHazardEnvelope
        {
            HazardId = "cycle-parity-public",
            SourceRegionId = "A",
            PublishedAtTick = 0,
            Regions = [],
            Branches =
            [
                Branch("A", "Q") with { MinimumTravelTicks = 1, MaximumTravelTicks = 1 },
                Branch("Q", "A") with { MinimumTravelTicks = 1, MaximumTravelTicks = 1 },
                Branch("A", "B") with { MinimumTravelTicks = 1, MaximumTravelTicks = 1 },
                Branch("A", "C") with { MinimumTravelTicks = 2, MaximumTravelTicks = 2 }
            ]
        };
        var unrelated = baseModel with
        {
            Branches = [.. baseModel.Branches,
                Branch("X", "Y") with { MinimumTravelTicks = 1, MaximumTravelTicks = 1 }]
        };
        var noCycle = baseModel with
        {
            Branches = baseModel.Branches.Skip(2).ToArray()
        };
        var initial = TraceCyclicCatalog(baseModel, tick, "A", ["B", "C"]);
        var expanded = TraceCyclicCatalog(unrelated, tick, "A", ["B", "C"]);
        var acyclic = TraceCyclicCatalog(noCycle, tick, "A", ["B", "C"]);
        Assert.Equal(["B", "C"], initial.CandidateWarps.OuterPlausibleRegionIdsAt(tick));
        Assert.Equal(["B", "C"], initial.CandidateWarps.PreferredRegionIdsAt(tick));
        Assert.Equal(JsonDefaults.Serialize(initial), JsonDefaults.Serialize(expanded));
        Assert.Equal(initial.CandidateWarps.PreferredRegionIdsAt(tick),
            acyclic.CandidateWarps.PreferredRegionIdsAt(tick));
        Assert.DoesNotContain(initial.Steps, step => step.RealTransitionTraversalOccurred);
    }

    [Fact]
    public void ZeroMinimumCycleTerminatesWithoutInventingArrivalAtFive()
    {
        var publicModel = new PublicHazardEnvelope
        {
            HazardId = "cycle-zero-public",
            SourceRegionId = "A",
            PublishedAtTick = 0,
            Regions = [],
            Branches =
            [
                Branch("A", "Q") with { MinimumTravelTicks = 0, MaximumTravelTicks = 1 },
                Branch("Q", "A") with { MinimumTravelTicks = 0, MaximumTravelTicks = 1 },
                Branch("A", "B") with { MinimumTravelTicks = 1, MaximumTravelTicks = 1 }
            ]
        };
        var unsupported = TraceCyclicCatalog(publicModel, 5, "A", ["B", "C"]);
        Assert.Equal(["B", "C"], unsupported.CandidateWarps.OuterPlausibleRegionIdsAt(5));
        Assert.Equal(["B", "C"], unsupported.CandidateWarps.PreferredRegionIdsAt(5));
        Assert.DoesNotContain(unsupported.Steps, step => step.RealTransitionTraversalOccurred);
        var direct = TraceCyclicCatalog(publicModel, 1, "A", ["B", "C"]);
        Assert.Equal(["B"], direct.CandidateWarps.PreferredRegionIdsAt(1));
        Assert.Contains(direct.Steps, step => step.RealTransitionTraversalOccurred &&
            step.TransitionIds.Count > 0);
    }

    [Fact]
    public void TwoPathsToSameTimedRegionRetainDifferentVisitedHistory()
    {
        // S-A-X arrives at X@2 first but has already used A. The only legal
        // simple path to T@4 is S-B-X-A-T, with four published transitions.
        var publicModel = new PublicHazardEnvelope
        {
            HazardId = "cycle-path-state-public",
            SourceRegionId = "S",
            PublishedAtTick = 0,
            Regions = [],
            Branches =
            [
                Branch("S", "A") with { MinimumTravelTicks = 1, MaximumTravelTicks = 1 },
                Branch("A", "X") with { MinimumTravelTicks = 1, MaximumTravelTicks = 1 },
                Branch("S", "B") with { MinimumTravelTicks = 1, MaximumTravelTicks = 1 },
                Branch("B", "X") with { MinimumTravelTicks = 1, MaximumTravelTicks = 1 },
                Branch("X", "A") with { MinimumTravelTicks = 1, MaximumTravelTicks = 1 },
                Branch("A", "T") with { MinimumTravelTicks = 1, MaximumTravelTicks = 1 }
            ]
        };
        var positive = TraceCyclicCatalog(publicModel, 4, "S", ["T", "U"]);
        Assert.Equal(["T", "U"], positive.CandidateWarps.OuterPlausibleRegionIdsAt(4));
        Assert.Equal(["T"], positive.CandidateWarps.PreferredRegionIdsAt(4));
        Assert.Contains(positive.Steps, step => step.RealTransitionTraversalOccurred &&
            step.TransitionIds.Count >= 4);
        var negative = publicModel with
        {
            Branches = publicModel.Branches.Where(edge =>
                !(edge.SourceRegionId == "B" && edge.DestinationRegionId == "X")).ToArray()
        };
        var missing = TraceCyclicCatalog(negative, 4, "S", ["T", "U"]);
        Assert.Equal(["T", "U"], missing.CandidateWarps.OuterPlausibleRegionIdsAt(4));
        Assert.Equal(["T", "U"], missing.CandidateWarps.PreferredRegionIdsAt(4));
    }

    // #139 / AR-32: a separate one-hop control tests four genuinely
    // receipt-gated method outputs, NOT the cyclic tracer case or all-edge
    // enumeration cost. Unrelated declarations change the public catalog
    // world but not the two received originals.
    [Fact]
    public void DisconnectedBranchOneHopControlPreservesReceivedFourMethodResults()
    {
        var p0 = ComplementaryPublicContext();
        var p1 = p0 with
        {
            PublicRegions = [.. p0.PublicRegions,
                new RegionDefinition {
                    Id = "X", Type = RegionType.Outdoor,
                    Center = new Position(6, 0, 0, "X") },
                new RegionDefinition {
                    Id = "Y", Type = RegionType.Outdoor,
                    Center = new Position(7, 0, 0, "Y") }],
            PublicHazardEnvelope = p0.PublicHazardEnvelope! with
            {
                Branches = [.. p0.PublicHazardEnvelope!.Branches,
                    Branch("X", "Y") with {
                        MinimumTravelTicks = 1, MaximumTravelTicks = 1 }]
            }
        };
        var originals = new[]
        {
            ComplementaryOriginal(RecordId, HolderId, 2,
                ["A", "B"], 3, "weft-a"),
            ComplementaryOriginal("second-original", "holder-b", 5,
                ["D"], 6, "weft-b")
        };
        var receipts = new[] { Receipt(6), LaterComparisonReceipt(7) };
        var builder = new RescuerEvidenceInputBuilder();
        var before = JsonDefaults.Serialize(originals);
        var p0Input = builder.BuildDistinctOriginals(
            p0, Seed, 7, RescuerId, originals, receipts);
        var p1Input = builder.BuildDistinctOriginals(
            p1, Seed, 7, RescuerId, originals, receipts);
        Assert.Equal(before, JsonDefaults.Serialize(originals));
        Assert.Equal(JsonDefaults.Serialize(p0Input.ReceivedRecords),
            JsonDefaults.Serialize(p1Input.ReceivedRecords));
        Assert.Equal(3, p0.PublicHazardEnvelope!.Branches.Count);
        Assert.Equal(4, p1.PublicHazardEnvelope!.Branches.Count);
        Assert.All(p0.PublicHazardEnvelope.Branches, edge =>
            Assert.Contains(p1.PublicHazardEnvelope.Branches, other =>
                other.SourceRegionId == edge.SourceRegionId &&
                other.DestinationRegionId == edge.DestinationRegionId &&
                other.ProvenanceId == edge.ProvenanceId));
        Assert.DoesNotContain(p0.PublicHazardEnvelope.Branches,
            edge => edge.SourceRegionId == "X" || edge.DestinationRegionId == "Y");

        var baseline = CompareCurrentMethods(p0Input);
        var expanded = CompareCurrentMethods(p1Input);
        Assert.Equal(4, baseline.Methods.Count);
        Assert.Equal([RecordId, "second-original"],
            baseline.ReceivedOriginals.Select(row => row.OriginalRecordId));
        Assert.Equal(JsonDefaults.Serialize(baseline.Methods),
            JsonDefaults.Serialize(expanded.Methods));
        Assert.Equal(JsonDefaults.Serialize(baseline.ActualReconstructionF20Projection),
            JsonDefaults.Serialize(expanded.ActualReconstructionF20Projection));
        // These existing step/inspected counters do NOT enumerate all
        // branches or count F20 declaration validation work. No inference
        // about wall time, total operations, or rescue efficiency follows.
    }
    // #145 / AR-36: reflection calls the private per-query work counter. It
    // measures preprocessing too, without extending any inference API.
    private static (bool Supported, long Work, IReadOnlyList<string> Witness)
        ProbePublicTracer(PublicHazardEnvelope envelope, string origin,
            string destination, long targetTick, long? cutoffTick = null)
    {
        var probe = typeof(OperationalCausalTracer)
            .GetMethods(System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static)
            .Single(method => method.Name == "TryFindPath" &&
                method.GetParameters().Length == 8);
        object?[] args =
        [
            origin, InteractionTimeWindow.At(0), destination,
            InteractionTimeWindow.At(targetTick), envelope, cutoffTick ?? targetTick,
            null, null
        ];
        var supported = (bool)probe.Invoke(null, args)!;
        return (supported, (long)args[7]!, (IReadOnlyList<string>)args[6]!);
    }

    private static PublicHazardEnvelope LayeredMergeCatalog(int layers)
    {
        // AR-36: every intermediate layer has two DISTINCT regions; complete
        // cross-links form 2^(L-1) simple S-to-T paths of exactly L ticks.
        // Parallel branches with identical endpoints do not test this case.
        if (layers < 2)
            throw new ArgumentOutOfRangeException(nameof(layers));
        var branches = new List<PublicHazardBranch>();
        foreach (var side in new[] { "a", "b" })
            branches.Add(Branch("S", $"L1{side}") with
            {
                MinimumTravelTicks = 1,
                MaximumTravelTicks = 1,
                ProvenanceId = $"layer-0-{side}"
            });
        for (var i = 1; i < layers - 1; i++)
        {
            foreach (var from in new[] { "a", "b" })
                foreach (var to in new[] { "a", "b" })
                    branches.Add(Branch($"L{i}{from}", $"L{i + 1}{to}") with
                    {
                        MinimumTravelTicks = 1,
                        MaximumTravelTicks = 1,
                        ProvenanceId = $"layer-{i}-{from}-{to}"
                    });
        }
        foreach (var side in new[] { "a", "b" })
            branches.Add(Branch($"L{layers - 1}{side}", "T") with
            {
                MinimumTravelTicks = 1,
                MaximumTravelTicks = 1,
                ProvenanceId = $"layer-{layers - 1}-{side}-end"
            });
        return new PublicHazardEnvelope
        {
            HazardId = "ar36-layered-public",
            SourceRegionId = "S",
            PublishedAtTick = 0,
            Regions = [],
            Branches = branches
        };
    }

    [Fact]
    public void LayeredDagHasExactPositiveAndTwoDistinctNegativeControlsWithBoundedWork()
    {
        long? timingWorkAt10 = null;
        foreach (var layers in new[] { 10, 14, 18 })
        {
            var world = LayeredMergeCatalog(layers);
            // Compare the three questions under one received-evidence cutoff.
            var cutoffTick = layers + 1;
            var positive = ProbePublicTracer(world, "S", "T", layers, cutoffTick);
            var unreachable = ProbePublicTracer(world, "S", "U", layers, cutoffTick);
            var late = ProbePublicTracer(world, "S", "T", layers + 1, cutoffTick);
            Assert.True(positive.Supported);
            Assert.Equal(layers, positive.Witness.Count);
            Assert.Equal(positive.Witness,
                ProbePublicTracer(world, "S", "T", layers, cutoffTick).Witness);
            Assert.False(unreachable.Supported);
            Assert.Empty(unreachable.Witness);
            Assert.False(late.Supported);
            Assert.Empty(late.Witness);
            foreach (var work in new[] { positive.Work, unreachable.Work, late.Work })
            {
                Assert.InRange(work, 1L, 32L * layers * layers);
            }
            // The negative time control must not grow as a 2^L path list.
            if (layers == 10)
                timingWorkAt10 = late.Work;
            if (layers == 18)
                Assert.True(late.Work <= 8L * Math.Max(1L, timingWorkAt10!.Value),
                    $"Time-incompatible layer growth: C10={timingWorkAt10}, C18={late.Work}");
        }
    }

    [Fact]
    public void SingleBackEdgeDoesNotRestoreExponentialLatePathEnumeration()
    {
        long? lateWorkAt10 = null;
        foreach (var layers in new[] { 10, 14, 18 })
        {
            var dag = LayeredMergeCatalog(layers);
            var cyclic = dag with
            {
                Branches = [.. dag.Branches,
                    Branch("T", "S") with {
                        MinimumTravelTicks = 1, MaximumTravelTicks = 1,
                        ProvenanceId = "ar38-return" }]
            };
            var cutoff = layers + 1;
            var positive = ProbePublicTracer(cyclic, "S", "T", layers, cutoff);
            var unreachable = ProbePublicTracer(cyclic, "S", "U", layers, cutoff);
            var late = ProbePublicTracer(cyclic, "S", "T", layers + 1, cutoff);
            Assert.True(positive.Supported);
            Assert.Equal(layers, positive.Witness.Count);
            Assert.False(unreachable.Supported);
            Assert.Empty(unreachable.Witness);
            Assert.False(late.Supported);
            Assert.Empty(late.Witness);
            foreach (var current in new[] { positive.Work, unreachable.Work, late.Work })
                Assert.InRange(current, 1L, 32L * layers * layers);
            if (layers == 10)
                lateWorkAt10 = late.Work;
            if (layers == 18)
                Assert.True(late.Work <= 8L * Math.Max(1L, lateWorkAt10!.Value),
                    $"AR-38 cyclic timing work: C10={lateWorkAt10}, C18={late.Work}");
        }
    }

    [Fact]
    public void DirectPublicWitnessInCyclicLayeredCatalogIsFoundWithoutDeepFailures()
    {
        foreach (var layers in new[] { 10, 14, 18, 30 })
        {
            var dag = LayeredMergeCatalog(layers);
            var cyclic = dag with
            {
                Branches = [.. dag.Branches,
                    Branch("T", "S") with {
                        MinimumTravelTicks = 1, MaximumTravelTicks = 1,
                        ProvenanceId = "ar38-return" },
                    Branch("S", "T") with {
                        MinimumTravelTicks = layers + 1,
                        MaximumTravelTicks = layers + 1,
                        ProvenanceId = "p1-direct-witness" }]
            };
            var original = ProbePublicTracer(cyclic, "S", "T",
                layers + 1, layers + 1);
            var shuffled = ProbePublicTracer(cyclic with
            {
                Branches = cyclic.Branches.Reverse().ToArray()
            }, "S", "T", layers + 1, layers + 1);
            Assert.True(original.Supported);
            Assert.Single(original.Witness);
            Assert.Equal(original.Witness, shuffled.Witness);
            Assert.True(shuffled.Supported);
            Assert.InRange(original.Work, 1L, 64L * layers * layers);
            Assert.InRange(shuffled.Work, 1L, 64L * layers * layers);
        }
    }

    private static PublicHazardEnvelope BidirectionalGridCatalog(
        int side, int minimumTick, int maximumTick)
    {
        var branches = new List<PublicHazardBranch>();
        for (var row = 0; row < side; row++)
        {
            for (var col = 0; col < side; col++)
            {
                var here = $"R{row}{col}";
                if (col + 1 < side)
                {
                    var right = $"R{row}{col + 1}";
                    branches.Add(Branch(here, right) with
                    { MinimumTravelTicks = minimumTick, MaximumTravelTicks = maximumTick });
                    branches.Add(Branch(right, here) with
                    { MinimumTravelTicks = minimumTick, MaximumTravelTicks = maximumTick });
                }
                if (row + 1 < side)
                {
                    var down = $"R{row + 1}{col}";
                    branches.Add(Branch(here, down) with
                    { MinimumTravelTicks = minimumTick, MaximumTravelTicks = maximumTick });
                    branches.Add(Branch(down, here) with
                    { MinimumTravelTicks = minimumTick, MaximumTravelTicks = maximumTick });
                }
            }
        }
        return new PublicHazardEnvelope
        {
            HazardId = "ar38-grid-public",
            SourceRegionId = "R00",
            PublishedAtTick = 0,
            Regions = [],
            Branches = branches
        };
    }

    [Fact]
    public void CyclicGridParityAndSimplePathTimeCeilingDoNotInventSupport()
    {
        var parityGrid = BidirectionalGridCatalog(6, 1, 1);
        var parity = ProbePublicTracer(parityGrid, "R00", "R55", 21);
        Assert.False(parity.Supported);
        Assert.Empty(parity.Witness);
        Assert.InRange(parity.Work, 1L, 4096L * 36);

        foreach (var (side, tick) in new[] { (5, 51L), (6, 73L) })
        {
            var grid = BidirectionalGridCatalog(side, 1, 2);
            var target = $"R{side - 1}{side - 1}";
            var result = ProbePublicTracer(grid, "R00", target, tick);
            Assert.False(result.Supported);
            Assert.Empty(result.Witness);
            Assert.InRange(result.Work, 1L, 4096L * side * side);
        }
    }

    [Fact]
    public void DiscreteArrivalHoleIsNotPromotedByDagStateCoalescing()
    {
        var world = new PublicHazardEnvelope
        {
            HazardId = "ar36-arrival-hole-public",
            SourceRegionId = "S",
            PublishedAtTick = 0,
            Regions = [],
            Branches =
            [
                Branch("S", "T") with { MinimumTravelTicks = 0,
                    MaximumTravelTicks = 0 },
                Branch("S", "A") with { MinimumTravelTicks = 1,
                    MaximumTravelTicks = 1 },
                Branch("A", "T") with { MinimumTravelTicks = 1,
                    MaximumTravelTicks = 1 }
            ]
        };
        Assert.True(ProbePublicTracer(world, "S", "T", 0).Supported);
        Assert.False(ProbePublicTracer(world, "S", "T", 1).Supported);
        Assert.True(ProbePublicTracer(world, "S", "T", 2).Supported);
        Assert.Equal(2, ProbePublicTracer(world, "S", "T", 2).Witness.Count);
    }

    [Fact]
    public void FourByFourCyclicGridHasFinitePositiveAndUnreachableControls()
    {
        var branches = new List<PublicHazardBranch>();
        for (var row = 0; row < 4; row++)
        {
            for (var col = 0; col < 4; col++)
            {
                var here = $"R{row}{col}";
                if (col < 3)
                {
                    var right = $"R{row}{col + 1}";
                    branches.Add(Branch(here, right) with
                    { MinimumTravelTicks = 1, MaximumTravelTicks = 2 });
                    branches.Add(Branch(right, here) with
                    { MinimumTravelTicks = 1, MaximumTravelTicks = 2 });
                }
                if (row < 3)
                {
                    var down = $"R{row + 1}{col}";
                    branches.Add(Branch(here, down) with
                    { MinimumTravelTicks = 1, MaximumTravelTicks = 2 });
                    branches.Add(Branch(down, here) with
                    { MinimumTravelTicks = 1, MaximumTravelTicks = 2 });
                }
            }
        }
        var grid = new PublicHazardEnvelope
        {
            HazardId = "ar36-grid-public",
            SourceRegionId = "R00",
            PublishedAtTick = 0,
            Regions = [],
            Branches = branches
        };
        foreach (var tick in new long[] { 10, 20 })
        {
            var positive = ProbePublicTracer(grid, "R00", "R33", tick);
            var negative = ProbePublicTracer(grid, "R00", "U", tick);
            Assert.True(positive.Supported);
            Assert.NotEmpty(positive.Witness);
            Assert.False(negative.Supported);
            Assert.Empty(negative.Witness);
            Assert.True(positive.Work > 0 && negative.Work > 0);
        }

        var zeroCycle = new PublicHazardEnvelope
        {
            HazardId = "ar36-zero-cycle-public",
            SourceRegionId = "A",
            PublishedAtTick = 0,
            Regions = [],
            Branches =
            [
                Branch("A", "Q") with { MinimumTravelTicks = 0,
                    MaximumTravelTicks = 1 },
                Branch("Q", "A") with { MinimumTravelTicks = 0,
                    MaximumTravelTicks = 1 },
                Branch("A", "B") with { MinimumTravelTicks = 1,
                    MaximumTravelTicks = 1 }
            ]
        };
        Assert.False(ProbePublicTracer(zeroCycle, "A", "B", 5).Supported);
        Assert.True(ProbePublicTracer(zeroCycle, "A", "B", 1).Supported);
        Assert.Equal(["B", "C"],
            TraceCyclicCatalog(zeroCycle, 5, "A", ["B", "C"])
                .CandidateWarps.OuterPlausibleRegionIdsAt(5));
    }


    // #148 / AR-37. This is a small mathematical reference, NOT a Joint runtime.
    // Unary region constraints on a Markov chain are sufficient when both
    // forward reachability and backward continuability are considered.
    [Fact]
    public void UnaryMarkovReferenceMatchesFiniteHistoryEnumeration()
    {
        var moves = new (string From, string To)[]
        {
            ("a", "b"), ("a", "d"), ("c", "d"), ("b", "e"), ("d", "f")
        };
        var histories = (
            from first in new[] { "a", "c" }
            from middle in new[] { "b", "d" }
            from last in new[] { "e", "f" }
            where moves.Contains((first, middle)) && moves.Contains((middle, last))
            select (First: first, Middle: middle, Last: last)).ToArray();

        var admitted = histories.Where(history =>
            history.First == "a" && history.Last == "e").ToArray();
        var exactMiddle = admitted.Select(history => history.Middle)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(region => region, StringComparer.Ordinal).ToArray();

        string[] Forward(IEnumerable<string> regions) => moves
            .Where(move => regions.Contains(move.From, StringComparer.Ordinal))
            .Select(move => move.To)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(region => region, StringComparer.Ordinal).ToArray();
        string[] Backward(IEnumerable<string> regions) => moves
            .Where(move => regions.Contains(move.To, StringComparer.Ordinal))
            .Select(move => move.From)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(region => region, StringComparer.Ordinal).ToArray();

        var forwardMiddle = Forward(["a"]);
        var backwardMiddle = Backward(["e"]);
        var filteredMiddle = forwardMiddle.Intersect(
            backwardMiddle, StringComparer.Ordinal)
            .OrderBy(region => region, StringComparer.Ordinal).ToArray();
        Assert.Equal(["b"], exactMiddle);
        Assert.Equal(exactMiddle, filteredMiddle);
        Assert.Equal(["b", "d"], forwardMiddle);
        Assert.Equal(["b"], backwardMiddle);
        Assert.Equal(["U1"], filteredMiddle.Select(region =>
            region == "b" ? "U1" : "U2"));
    }

    // #148 reviewer requirement: compare the *real* historical applicator
    // to a finite unary reference at the same receipt cutoff and target tick.
    // The reference also identifies b as the exact intermediate region at
    // tick 1; this runtime API's CurrentTick is the query cutoff/target tick,
    // so the two receipts at tick 2 cannot be retroactively used at tick 1.
    [Fact]
    public void ReceivedUnaryMarkovConstraintsExerciseActualApplicatorAgainstReference()
    {
        var edges = new (string From, string To)[]
        {
            ("a", "b"), ("a", "d"), ("c", "d"), ("b", "e"), ("d", "f")
        };
        var world = MultiHolderContext() with
        {
            PublicRegions = new[] { "a", "b", "c", "d", "e", "f" }
                .Select((id, index) => new RegionDefinition
                {
                    Id = id,
                    Type = RegionType.Outdoor,
                    Center = new Position(index, 0, 0, id)
                }).ToArray(),
            PublicCausalTracingModel = new PublicCausalTracingModel
            {
                CandidateStates =
                [
                    State(0, ["a", "c"]),
                    State(1, ["b", "d"]),
                    State(2, ["e", "f"])
                ]
            },
            PublicHazardEnvelope = new PublicHazardEnvelope
            {
                HazardId = "unary-markov-public",
                SourceRegionId = "a",
                PublishedAtTick = 0,
                Regions = [],
                Branches = edges.Select(edge => Branch(edge.From, edge.To) with
                {
                    MinimumTravelTicks = 1,
                    MaximumTravelTicks = 1
                }).ToArray()
            }
        };

        DeviceHeldInteractionRecord Original(
            string originalId, string holder, long observedTick, string region) =>
            new(holder, observedTick, InteractionObservation.Create(
                ScenarioId, Seed, originalId, null,
                InteractionRelationKind.PersonPersonEncounter,
                [
                    new InteractionParticipant(TargetId, InteractionEntityKind.Person,
                        InteractionParticipantRole.EncounterParticipant),
                    new InteractionParticipant("person-q", InteractionEntityKind.Person,
                        InteractionParticipantRole.EncounterParticipant)
                ],
                InteractionTimeWindow.At(observedTick), [region],
                observedTick, EvidenceState.Confirmed,
                isPersonEvidence: true, isDeviceOnlyEvidence: false,
                InteractionVisibility.PrivateEvidence,
                [new InteractionProvenance(
                    InteractionProvenanceKind.EvidenceRecord, originalId)]));
        var originals = new[]
        {
            Original("unary-at-zero", HolderId, 0, "a"),
            Original("unary-at-two", "holder-b", 2, "e")
        };
        var receipts = new[]
        {
            new InteractionRecordReceipt(ScenarioId, Seed,
                "unary-at-zero", HolderId, RescuerId, 2),
            new InteractionRecordReceipt(ScenarioId, Seed,
                "unary-at-two", "holder-b", RescuerId, 2)
        };
        var builder = new RescuerEvidenceInputBuilder();
        var firstOnly = builder.BuildDistinctOriginals(
            world, Seed, 2, RescuerId, originals, receipts.Take(1));
        var received = builder.BuildDistinctOriginals(
            world, Seed, 2, RescuerId, originals, receipts);
        Assert.Single(firstOnly.Input.AvailableCrossThreadConstraints!.Constraints);
        var accepted = received.Input.AvailableCrossThreadConstraints!.Constraints;
        Assert.Equal(2, accepted.Count);
        Assert.Equal([0L, 2L], accepted
            .Select(c => c.ConstrainedTimeWindow.StartTick).Order().ToArray());
        Assert.All(accepted, c =>
        {
            Assert.Equal(2, c.AvailableAtTick);
            Assert.Equal(EvidenceState.Confirmed, c.EvidenceBoundaryState);
            Assert.True(c.IsPersonEvidence);
            Assert.Equal(TargetId, c.TargetEntityId);
        });

        var historical = PublicCandidateWarpFactory.Create(
            ScenarioId, Seed, TargetId,
            world.PublicCausalTracingModel!.CandidateStates,
            CandidateWarpStanding.Preferred);
        var query = new CandidateWarpAccessContext
        {
            ScenarioId = ScenarioId,
            Seed = Seed,
            TargetEntityId = TargetId,
            CurrentTick = 2,
            QueryContextId = "ar37-unary-runtime-check"
        };
        var applicator = new CrossThreadConstraintApplicator();
        var onlyFirst = applicator.Apply(historical,
            firstOnly.Input.AvailableCrossThreadConstraints.Constraints,
            query, world.PublicHazardEnvelope);
        var actual = applicator.Apply(historical,
            accepted, query, world.PublicHazardEnvelope);

        var finitePaths = (
            from first in new[] { "a", "c" }
            from middle in new[] { "b", "d" }
            from last in new[] { "e", "f" }
            where edges.Contains((first, middle)) && edges.Contains((middle, last))
            select (First: first, Middle: middle, Last: last)).ToArray();
        var exact = finitePaths.Where(path =>
            path.First == "a" && path.Last == "e").ToArray();
        Assert.Equal(["b"], exact.Select(path => path.Middle).Distinct().ToArray());
        var exactLast = exact.Select(path => path.Last).Distinct().ToArray();
        Assert.Equal(["e"], exactLast);

        // Real runtime output at t=2 under the *same* received unary E,
        // not an invented backward pass or a simulated joint-history API.
        Assert.Equal(["e", "f"], onlyFirst.CandidateWarps.PreferredRegionIdsAt(2));
        Assert.Equal(exactLast, actual.CandidateWarps.PreferredRegionIdsAt(2));
        // The API's outer-only bucket is empty; the admitted e is Preferred.
        // An empty outer-only bucket is NOT evidence that P cannot be at e.
        Assert.Empty(actual.CandidateWarps.OuterPlausibleRegionIdsAt(2));
        Assert.DoesNotContain(actual.Audits, audit => audit.ConflictDetected);
        Assert.Contains(actual.Audits, audit =>
            audit.ConstrainedTimeWindow.StartTick == 0 &&
            audit.Disposition == ConstraintApplicationDisposition.Narrow);
        Assert.Contains(actual.Audits, audit =>
            audit.ConstrainedTimeWindow.StartTick == 2 &&
            audit.Disposition == ConstraintApplicationDisposition.Narrow);
        Assert.Equal(JsonDefaults.Serialize(actual),
            JsonDefaults.Serialize(applicator.Apply(
                historical, accepted.Reverse(), query, world.PublicHazardEnvelope)));
        // Region e has no b/d public unit mapping in this fixed example.
        // No confirmation unit at t=2 is invented for an unmapped region.
    }

    // #148. The same Q history must satisfy BOTH distinct co-location
    // propositions; using a different Q history at each tick is a relaxation.
    // The co-location JointRequirement is assumed in this reference ONLY.
    [Fact]
    public void SharedOtherPersonHistoryHasAReferenceOnlyJointGap()
    {
        var qHistories = new[]
        {
            (First: "a", Second: "b"),
            (First: "c", Second: "d")
        };
        // Public P movement admits all four pairs; its own first-tick
        // received sighting restricts P to a.
        var pHistories = new[]
        {
            (First: "a", Second: "b"),
            (First: "a", Second: "d"),
            (First: "c", Second: "b"),
            (First: "c", Second: "d")
        };
        string[] Sorted(IEnumerable<string> regions) => regions
            .Distinct(StringComparer.Ordinal)
            .OrderBy(region => region, StringComparer.Ordinal).ToArray();

        var exactJoint = Sorted(
            from p in pHistories
            from q in qHistories
            where p.First == "a" && p.First == q.First &&
                p.Second == q.Second
            select p.Second);
        // Relaxation: the witness for the first and second encounters
        // may be chosen from DIFFERENT candidate histories of Q.
        var unaryMarginals = Sorted(pHistories
            .Where(p => p.First == "a" &&
                qHistories.Any(q => q.First == p.First) &&
                qHistories.Any(q => q.Second == p.Second))
            .Select(p => p.Second));

        Assert.Equal(["b"], exactJoint);
        Assert.Equal(["b", "d"], unaryMarginals);
        string[] Units(IEnumerable<string> regions) =>
            Sorted(regions.Select(region => region == "b" ? "U1" : "U2"));
        Assert.Equal(["U1"], Units(exactJoint));
        Assert.Equal(["U1", "U2"], Units(unaryMarginals));
        Assert.Contains("b", exactJoint); // synthetic truth; evaluation only
        Assert.DoesNotContain("d", exactJoint);
        // No call to a production Joint solver was made by this test.
    }

    // #148. This exercises the real receipt-to-CrossThreadConstraint
    // contract. It is unary in target/time/region, not a representation
    // of a latent history choice shared between P and Q.
    [Fact]
    public void ReceivedEncounterConstraintRemainsCutoffGatedUnaryPersonEvidence()
    {
        var original = Record();
        var before = Build(original, [], currentTick: 4);
        Assert.Empty(before.Input.AvailableCrossThreadConstraints!.Constraints);

        var received = Build(original, [Receipt(5)], currentTick: 5);
        var constraint = Assert.Single(
            received.Input.AvailableCrossThreadConstraints!.Constraints);
        Assert.Equal(TargetId, constraint.TargetEntityId);
        Assert.Equal(InteractionRelationKind.PersonPersonEncounter,
            constraint.SourceRelationKind);
        Assert.Equal(["A"], constraint.AllowedRegionIds);
        Assert.Equal(2, constraint.ConstrainedTimeWindow.StartTick);
        Assert.Equal(2, constraint.ConstrainedTimeWindow.EndTick);
        Assert.Equal(5, constraint.AvailableAtTick);
        Assert.True(constraint.IsPersonEvidence);
        Assert.False(constraint.IsDeviceOnlyEvidence);
        Assert.Equal(EvidenceState.Confirmed, constraint.EvidenceBoundaryState);
        Assert.Contains(constraint.SourceParticipants, participant =>
            participant.EntityId == "person-b" &&
            participant.EntityKind == InteractionEntityKind.Person);
        Assert.Empty(received.Input.AvailableCrossThreadConstraints
            .PubliclyAvailableAt(4).Constraints);

        var duplicate = Build(original, [Receipt(5), Receipt(5)], currentTick: 5);
        Assert.Equal(
            received.Input.AvailableCrossThreadConstraints.Constraints
                .Select(item => item.ConstraintId),
            duplicate.Input.AvailableCrossThreadConstraints!.Constraints
                .Select(item => item.ConstraintId));
        var alternative = Build(
            Record(state: EvidenceState.SupportedButUncertain), [Receipt(5)]);
        Assert.Equal(EvidenceState.SupportedButUncertain,
            Assert.Single(alternative.Input.AvailableCrossThreadConstraints!
                .Constraints).EvidenceBoundaryState);
        var device = Build(Record(deviceOnly: true), [Receipt(5)]);
        Assert.Empty(device.Input.AvailableCrossThreadConstraints!.Constraints);
        // This API has not received a two-tick shared-Q-history variable;
        // therefore neither this test nor the reference above may hard-drop U2.
    }

}
