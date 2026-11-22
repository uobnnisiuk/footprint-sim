using FootprintSim.Cli;
using FootprintSim.Evaluation;
using FootprintSim.Evidence;
using FootprintSim.Hazards;
using FootprintSim.Inference;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;
using FootprintSim.Model;
using FootprintSim.Serialization;
using Xunit;

namespace FootprintSim.Core.Tests;

public sealed class ConditionLensTests
{
    private static string FixturePath()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "FootprintSim.sln")))
            root = root.Parent;
        Assert.NotNull(root);
        return Path.Combine(root.FullName, "experiments", "condition_lenses", "fixture.json");
    }

    private static byte[] Fixture() => File.ReadAllBytes(FixturePath());

    [Fact]
    public void TwoDifferentQuestionsAreNotPublicPathDeclarations()
    {
        var report = ConditionLensAnalysis.Run(Fixture());
        Assert.Equal(24, report.Index.Count);
        Assert.Equal(["02", "04", "06", "08", "09", "10", "12", "21", "22"], report.Index.Where(x =>
            x.Status == "limited-analogy-implemented").Select(x => x.Id));
        var a = report.Cases.Single(x => x.Id == "A-none");
        var b = report.Cases.Single(x => x.Id == "B-none");
        var c = report.Cases.Single(x => x.Id == "C-none");
        Assert.Equal(["02"], a.Modes.Single(m => m.Mode == "combined").Questions.SelectMany(q => q.LensIds));
        Assert.Equal(["06"], b.Modes.Single(m => m.Mode == "combined").Questions.SelectMany(q => q.LensIds));
        Assert.Equal(["02", "06"], c.Modes.Single(m => m.Mode == "combined").Questions.SelectMany(q => q.LensIds)
            .OrderBy(x => x, StringComparer.Ordinal));
        Assert.Empty(a.ExistingDeclaredPathDiagnostic.Candidates
            .SelectMany(candidate => candidate.Questions));
        Assert.Empty(b.ExistingDeclaredPathDiagnostic.Candidates
            .SelectMany(candidate => candidate.Questions));
        Assert.NotEqual(a.Modes.Single(m => m.Mode == "combined").Questions[0].ReasonCode, b.Modes.Single(m => m.Mode == "combined").Questions[0].ReasonCode);
        Assert.Equal("P", c.Modes.Single(m => m.Mode == "combined").Questions[0].TargetPersonId);
    }

    [Fact]
    public void SahajataQuestionDoesNotCertifyDoctrinalCondition()
    {
        var report = ConditionLensAnalysis.Run(Fixture());
        Assert.Equal(9, report.SchemaVersion);
        Assert.Equal(24, report.CorrespondenceAudits.Count);
        Assert.Equal(report.Index.Select(x => x.Id),
            report.CorrespondenceAudits.Select(x => x.LensId));
        var audit = report.CorrespondenceAudits.Single(x => x.LensId == "06");
        Assert.Equal("limited-analogy-implemented", audit.QuestionImplementationStatus);
        Assert.Equal("doctrinal-correspondence-unverified", audit.DoctrinalCorrespondenceStatus);
        Assert.Equal("search-effect-unverified", audit.SearchEffectStatus);
        Assert.Equal("co-arising", audit.TemporalRelation);
        Assert.Contains("conditioning-to-conditioned", audit.ConditioningDirection);
        Assert.Contains("conditioning-and-conditioned-phenomena-pair",
            audit.UnrepresentedDoctrinalRelations);
        Assert.Contains("directed-or-mutual-conditional-relation",
            audit.UnrepresentedDoctrinalRelations);
        Assert.Contains("received-external-source-root", audit.ReceivedEngineeringInputs);
        Assert.Equal("https://www.aimwell.org/patthana.html", audit.DoctrinalSource);
        Assert.All(report.CorrespondenceAudits.Where(x => x.LensId != "06"), x =>
        {
            Assert.Equal("doctrinal-correspondence-unassessed",
                x.DoctrinalCorrespondenceStatus);
            Assert.Equal("search-effect-unverified", x.SearchEffectStatus);
        });

        // The question toggles on valid receipt/source-root contrasts, while
        // no conditioning/conditioned phenomena or mutual edge is represented.
        foreach (var (name, expectedQuestion) in new[]
        {
            ("B-none", true), ("B-eligible", false),
            ("S3", true), ("S4", false)
        })
        {
            var row = report.Cases.Single(x => x.Id == name);
            Assert.Equal(expectedQuestion,
                row.Modes.Single(x => x.Mode == "06").Questions.Count != 0);
            Assert.Equal(11, row.Modes.Count);
            var baseline = row.Modes[0].MethodComparison.Methods;
            Assert.Equal(4, baseline.Count);
            foreach (var mode in row.Modes)
            {
                var actual = mode.MethodComparison.Methods;
                Assert.Equal(4, actual.Count);
                for (var i = 0; i < 4; i++)
                {
                    Assert.Equal(baseline[i].Method, actual[i].Method);
                    Assert.Equal(baseline[i].OuterCandidateRegionIds,
                        actual[i].OuterCandidateRegionIds);
                    Assert.Equal(baseline[i].PreferredCandidateRegionIds,
                        actual[i].PreferredCandidateRegionIds);
                    Assert.Equal(baseline[i].UncertainCandidateRegionIds,
                        actual[i].UncertainCandidateRegionIds);
                    Assert.Equal(baseline[i].ConflictedCandidateRegionIds,
                        actual[i].ConflictedCandidateRegionIds);
                    Assert.Equal(baseline[i].AvailableSourceGroupCount,
                        actual[i].AvailableSourceGroupCount);
                }
            }
        }
        Assert.Equal(["root-s-first"],
            Assert.Single(report.Cases.Single(x => x.Id == "S3")
                .Modes.Single(x => x.Mode == "06").Questions).SourceAncestryIds);
    }

    private static readonly (string Id, string Direction, string Time,
        string Conditioning, string Conditioned, string Case, string Missing)[] Rev14Audits =
    [
        ("05", "preceding-to-immediately-succeeding", "immediately-successive-mental-moments",
            "preceding mental phenomenon", "immediately succeeding mental phenomenon",
            "E-prior-none/E-prior-future vs F-followup", "immediate-mental-succession-conditional-link"),
        ("07", "reciprocal-conditioning-in-both-directions", "simultaneous-mutual-conditioning",
            "mutually conditioning mental aggregates", "counterpart mental aggregates",
            "S1/S3/S4/B-eligible", "bidirectional-mutual-conditional-links"),
        ("11", "later-mental-to-earlier-material", "later-arising-conditions-earlier-material",
            "later-arisen mental phenomena", "earlier-arisen material body phenomena",
            "D-none/D-eligible", "later-to-earlier-conditional-effect"),
        ("23", "ceased-predecessor-to-successor", "cessation-just-before-succeeding-mental-moment",
            "just-ceased preceding mental phenomenon", "immediately succeeding mental phenomenon",
            "U-none/U-future", "verified-cessation-of-preceding-mental-phenomenon"),
        ("24", "persisting-to-conditioned", "non-cessation-during-conditioning",
            "not-ceased persisting phenomena", "other phenomena conditioned",
            "H-none/H-eligible", "verified-persistence-or-non-cessation")
    ];

    [Fact]
    public void FiveUnimplementedRelationsHaveDistinctOriginalRolesAndMissingLinks()
    {
        var report = ConditionLensAnalysis.Run(Fixture());
        Assert.Equal(9, report.SchemaVersion);
        Assert.Equal(24, report.CorrespondenceAudits.Count);
        foreach (var entry in Rev14Audits)
        {
            var audit = report.CorrespondenceAudits.Single(x => x.LensId == entry.Id);
            Assert.Equal("unimplemented-unassessed", audit.QuestionImplementationStatus);
            Assert.Equal("doctrinal-correspondence-unassessed", audit.DoctrinalCorrespondenceStatus);
            Assert.Equal("search-effect-unverified", audit.SearchEffectStatus);
            Assert.Equal(entry.Direction, audit.ConditioningDirection);
            Assert.Equal(entry.Time, audit.TemporalRelation);
            Assert.Contains(entry.Conditioning, audit.ConditioningPhenomena);
            Assert.Contains(entry.Conditioned, audit.ConditionedPhenomena);
            Assert.Contains(entry.Case, audit.EngineeringQuestionTrigger);
            Assert.Contains(entry.Missing, audit.UnrepresentedDoctrinalRelations);
            Assert.NotEmpty(audit.ReceivedEngineeringInputs);
            Assert.Equal("https://www.aimwell.org/patthana.html", audit.DoctrinalSource);
            Assert.DoesNotContain(report.Cases.SelectMany(c => c.Modes)
                .SelectMany(m => m.Questions).SelectMany(q => q.LensIds),
                id => id == entry.Id);
        }
        Assert.Equal(56, report.Cases.Count);
        Assert.All(report.Cases, c => Assert.Equal(11, c.Modes.Count));
    }

    [Fact]
    public void ReceivedPersonEvidenceResolvesMissingPersonButNotDeviceAssociation()
    {
        var report = ConditionLensAnalysis.Run(Fixture());
        var before = report.Cases.Single(c => c.Id == "A-none");
        var after = report.Cases.Single(c => c.Id == "A-eligible");
        var future = report.Cases.Single(c => c.Id == "A-future");
        var c = report.Cases.Single(x => x.Id == "C-none");
        var missing = Assert.Single(before.Modes[1].Questions);
        Assert.Equal("subject-person-verification:5", missing.QuestionId);
        Assert.Equal("separately-received-confirmed-person-evidence", missing.RequiredInformation);
        Assert.Equal(JsonDefaults.Serialize(before.Modes[1].Questions),
            JsonDefaults.Serialize(future.Modes[1].Questions));

        var link = Assert.Single(after.Modes[1].Questions);
        Assert.Equal("subject-device-person-link:5", link.QuestionId);
        Assert.Equal("unverified-device-person-association", link.ReasonCode);
        Assert.Equal("independently-established-device-person-association",
            link.RequiredInformation);
        Assert.Equal(["a-device", "a-person"], link.ReceivedOriginalIds);
        Assert.DoesNotContain(missing.QuestionId, after.Modes.Single(m => m.Mode == "combined").Questions.Select(q => q.QuestionId));
        var difference = report.AnswerDifferences.Single(d =>
            d.FromCase == "A-none" && d.ToCase == "A-eligible");
        Assert.Contains(missing.QuestionId, difference.ResolvedQuestionIds);
        Assert.Contains(link.QuestionId, difference.NewQuestionIds);

        var cLink = Assert.Single(c.Modes[1].Questions);
        Assert.Equal(link.QuestionId, cLink.QuestionId);
        Assert.Equal(link.RequiredInformation, cLink.RequiredInformation);
        var cCorroboration = Assert.Single(c.Modes[3].Questions);
        Assert.Equal("same-tick-person-corroboration:5", cCorroboration.QuestionId);
        Assert.NotEqual(cLink.ReasonCode, cCorroboration.ReasonCode);
    }

    [Fact]
    public async Task CliCommandWritesTheCompleteFiniteComparison()
    {
        var output = Path.Combine(Path.GetTempPath(),
            "footprint-condition-lens-" + Guid.NewGuid().ToString("N"));
        try
        {
            var exitCode = await CliApplication.RunAsync(
                ["evaluate-condition-lenses", "--fixture", FixturePath(), "--output", output]);
            Assert.Equal(0, exitCode);
            var actual = await File.ReadAllTextAsync(Path.Combine(output, "condition-lenses.json"));
            var expected = ConditionLensAnalysis.Run(Fixture());
            Assert.Equal(JsonDefaults.Serialize(expected) + "\n", actual);
            // Assert against bytes actually written by the public CLI, not only Run().
            using var savedReport = System.Text.Json.JsonDocument.Parse(actual);
            foreach (var caseName in new[] { "K2-conflict", "K3-conflict" })
            {
                var savedCase = savedReport.RootElement.GetProperty("cases")
                    .EnumerateArray().Single(c =>
                        c.GetProperty("id").GetString() == caseName);
                var savedQuestion = savedCase.GetProperty("modes").EnumerateArray()
                    .Single(m => m.GetProperty("mode").GetString() == "09")
                    .GetProperty("questions").EnumerateArray().Single();
                Assert.Equal("conflicted-outcome-depends-on-received-independent-person-root",
                    savedQuestion.GetProperty("reasonCode").GetString());
                Assert.Equal(2, savedQuestion.GetProperty("sourceAncestryIds").GetArrayLength());
                Assert.Equal(2, savedCase.GetProperty("rootRemovals").GetArrayLength());
                Assert.Equal(JsonDefaults.Serialize(savedCase.GetProperty("modes")
                    .EnumerateArray().First().GetProperty("methodComparison")),
                    JsonDefaults.Serialize(savedCase.GetProperty("modes")
                    .EnumerateArray().Single(m => m.GetProperty("mode").GetString() == "09")
                    .GetProperty("methodComparison")));
            }

            var savedRepeat = savedReport.RootElement.GetProperty("cases")
                .EnumerateArray().Single(row =>
                    row.GetProperty("id").GetString() == "T-repeat");
            var savedTwelve = savedRepeat.GetProperty("modes").EnumerateArray()
                .Single(mode => mode.GetProperty("mode").GetString() == "12");
            var twelveQuestion = savedTwelve.GetProperty("questions").EnumerateArray().Single();
            Assert.Equal("repeated-temporal-person-observations-share-source-root",
                twelveQuestion.GetProperty("reasonCode").GetString());
            Assert.Equal(2, twelveQuestion.GetProperty("receivedOriginalIds").GetArrayLength());
            Assert.Equal(2, twelveQuestion.GetProperty("receivedObservationIds").GetArrayLength());
            Assert.Equal("root-t-shared",
                twelveQuestion.GetProperty("sourceAncestryIds")[0].GetString());
            Assert.Equal(JsonDefaults.Serialize(savedRepeat.GetProperty("modes")
                .EnumerateArray().First().GetProperty("methodComparison")),
                JsonDefaults.Serialize(savedTwelve.GetProperty("methodComparison")));
            var savedRedundant = savedReport.RootElement.GetProperty("cases")
                .EnumerateArray().Single(row => row.GetProperty("id").GetString() == "N-redundant");
            var savedEight = savedRedundant.GetProperty("modes").EnumerateArray()
                .Single(mode => mode.GetProperty("mode").GetString() == "08");
            var savedEightQuestion = savedEight.GetProperty("questions").EnumerateArray().Single();
            Assert.Equal("received-distinct-roots-upstream-provenance-unverified-despite-redundant-outcome",
                savedEightQuestion.GetProperty("reasonCode").GetString());
            Assert.Equal(2, savedEightQuestion.GetProperty("receivedOriginalIds").GetArrayLength());
            Assert.Equal(2, savedEightQuestion.GetProperty("receivedObservationIds").GetArrayLength());
            Assert.Equal(2, savedEightQuestion.GetProperty("sourceAncestryIds").GetArrayLength());
            var savedRemovals = savedRedundant.GetProperty("rootRemovals")
                .EnumerateArray().ToArray();
            Assert.Equal(["root-n-left", "root-n-right"],
                savedRemovals.Select(r => r.GetProperty("sourceRootId").GetString()));
            foreach (var removal in savedRemovals)
            {
                var savedMethods = removal.GetProperty("methods").EnumerateArray().ToArray();
                Assert.Equal(4, savedMethods.Length);
                foreach (var method in savedMethods)
                {
                    Assert.Equal(2, method.GetProperty("beforeAvailableSourceGroups").GetInt32());
                    Assert.Equal(1, method.GetProperty("afterAvailableSourceGroups").GetInt32());
                    foreach (var name in new[] { "Outer", "Preferred", "Uncertain", "Conflicted" })
                        Assert.Equal(JsonDefaults.Serialize(method.GetProperty("before" + name)),
                            JsonDefaults.Serialize(method.GetProperty("after" + name)));
                }
            }
            Assert.Equal(JsonDefaults.Serialize(savedRedundant.GetProperty("modes")
                .EnumerateArray().First().GetProperty("methodComparison")),
                JsonDefaults.Serialize(savedEight.GetProperty("methodComparison")));
            Assert.Equal(9, savedReport.RootElement.GetProperty("schemaVersion").GetInt32());
            // These values were read from the CLI's actual saved JSON.
            var savedAudits = savedReport.RootElement.GetProperty("correspondenceAudits")
                .EnumerateArray().ToArray();
            Assert.Equal(24, savedAudits.Length);
            var savedSix = savedAudits.Single(x => x.GetProperty("lensId").GetString() == "06");
            Assert.Equal("limited-analogy-implemented",
                savedSix.GetProperty("questionImplementationStatus").GetString());
            Assert.Equal("doctrinal-correspondence-unverified",
                savedSix.GetProperty("doctrinalCorrespondenceStatus").GetString());
            Assert.Equal("search-effect-unverified",
                savedSix.GetProperty("searchEffectStatus").GetString());
            Assert.Equal("co-arising", savedSix.GetProperty("temporalRelation").GetString());
            Assert.Contains("conditioning-and-conditioned-phenomena-pair",
                savedSix.GetProperty("unrepresentedDoctrinalRelations").EnumerateArray()
                    .Select(x => x.GetString()));
            foreach (var entry in Rev14Audits)
            {
                var saved = savedAudits.Single(x =>
                    x.GetProperty("lensId").GetString() == entry.Id);
                Assert.Equal("unimplemented-unassessed",
                    saved.GetProperty("questionImplementationStatus").GetString());
                Assert.Equal("doctrinal-correspondence-unassessed",
                    saved.GetProperty("doctrinalCorrespondenceStatus").GetString());
                Assert.Equal("search-effect-unverified",
                    saved.GetProperty("searchEffectStatus").GetString());
                Assert.StartsWith(entry.Conditioning, saved.GetProperty("conditioningPhenomena").GetString());
                Assert.StartsWith(entry.Conditioned, saved.GetProperty("conditionedPhenomena").GetString());
                Assert.Equal(entry.Direction, saved.GetProperty("conditioningDirection").GetString());
                Assert.Equal(entry.Time, saved.GetProperty("temporalRelation").GetString());
                Assert.Contains(entry.Case, saved.GetProperty("engineeringQuestionTrigger").GetString()!);
                Assert.Contains(entry.Missing, saved.GetProperty("unrepresentedDoctrinalRelations")
                    .EnumerateArray().Select(v => v.GetString()));
            }
            foreach (var (caseId, mustAsk) in new[]
            {
                ("B-none", true), ("B-eligible", false),
                ("S3", true), ("S4", false)
            })
            {
                var row = savedReport.RootElement.GetProperty("cases")
                    .EnumerateArray().Single(x => x.GetProperty("id").GetString() == caseId);
                var modes = row.GetProperty("modes").EnumerateArray().ToArray();
                Assert.Equal(11, modes.Length);
                var sixMode = modes.Single(x => x.GetProperty("mode").GetString() == "06");
                Assert.Equal(mustAsk, sixMode.GetProperty("questions").GetArrayLength() != 0);
                var baseline = modes[0].GetProperty("methodComparison")
                    .GetProperty("methods").EnumerateArray().ToArray();
                Assert.Equal(4, baseline.Length);
                foreach (var mode in modes)
                {
                    var methods = mode.GetProperty("methodComparison")
                        .GetProperty("methods").EnumerateArray().ToArray();
                    Assert.Equal(4, methods.Length);
                    for (var i = 0; i < 4; i++)
                    {
                        foreach (var field in new[]
                        {
                            "outerCandidateRegionIds", "preferredCandidateRegionIds",
                            "uncertainCandidateRegionIds", "conflictedCandidateRegionIds",
                            "availableSourceGroupCount"
                        })
                            Assert.Equal(baseline[i].GetProperty(field).GetRawText(),
                                methods[i].GetProperty(field).GetRawText());
                    }
                }
            }
            var firstBytes = await File.ReadAllBytesAsync(Path.Combine(output, "condition-lenses.json"));
            Assert.Equal(0, await CliApplication.RunAsync(
                ["evaluate-condition-lenses", "--fixture", FixturePath(), "--output", output]));
            Assert.Equal(firstBytes,
                await File.ReadAllBytesAsync(Path.Combine(output, "condition-lenses.json")));
            Assert.Equal(24, expected.Index.Count);
            Assert.Equal(56, expected.Cases.Count);
            Assert.Equal(9, expected.SchemaVersion);
            Assert.All(expected.Cases, c => Assert.Equal(11, c.Modes.Count));
            Assert.Equal(["b"], expected.Cases.Single(c => c.Id == "B-eligible")
                .Modes[0].MethodComparison.Methods.Single(m =>
                    m.Method == "distributed-reconstruction").OuterCandidateRegionIds);
            Assert.Equal(JsonDefaults.Serialize(expected.Cases.Single(c => c.Id == "A-none").Modes),
                JsonDefaults.Serialize(expected.Cases.Single(c => c.Id == "A-future").Modes));
        }
        finally
        {
            if (Directory.Exists(output))
                Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public void QuestionsNeverAlterExistingMethodCandidates()
    {
        var report = ConditionLensAnalysis.Run(Fixture());
        foreach (var row in report.Cases)
        {
            Assert.Equal(["none", "02", "04", "06", "08", "09", "10", "12", "21", "22", "combined"], row.Modes.Select(x => x.Mode));
            Assert.Empty(row.Modes[0].Questions);
            foreach (var mode in row.Modes.Skip(1))
            {
                Assert.Equal(JsonDefaults.Serialize(row.Modes[0].MethodComparison),
                    JsonDefaults.Serialize(mode.MethodComparison));
                Assert.All(mode.Questions, question =>
                    Assert.All(question.ReceivedOriginalIds,
                        id => Assert.Contains(id, row.ReceivedOriginalIds)));
            }
        }
        Assert.Equal(JsonDefaults.Serialize(report), JsonDefaults.Serialize(
            ConditionLensAnalysis.Run(Fixture())));
    }

    [Fact]
    public void ReceiptAndSameTickBoundariesConstrainTheAnswerComparison()
    {
        var report = ConditionLensAnalysis.Run(Fixture());
        var aNone = report.Cases.Single(c => c.Id == "A-none");
        var aFuture = report.Cases.Single(c => c.Id == "A-future");
        var bNone = report.Cases.Single(c => c.Id == "B-none");
        var bFuture = report.Cases.Single(c => c.Id == "B-future");
        Assert.Equal(JsonDefaults.Serialize(aNone.Modes.Single(m => m.Mode == "combined").Questions),
            JsonDefaults.Serialize(aFuture.Modes.Single(m => m.Mode == "combined").Questions));
        Assert.Equal(JsonDefaults.Serialize(bNone.Modes.Single(m => m.Mode == "combined").Questions),
            JsonDefaults.Serialize(bFuture.Modes.Single(m => m.Mode == "combined").Questions));
        Assert.DoesNotContain("a-person", aFuture.ReceivedOriginalIds);
        Assert.DoesNotContain("b-second", bFuture.ReceivedOriginalIds);
        Assert.Empty(report.AnswerDifferences.Single(d =>
            d.FromCase == "B-none" && d.ToCase == "B-future").Methods
            .SelectMany(m => m.RemovedOuter));
        Assert.Equal(["b"], report.Cases.Single(c => c.Id == "B-eligible")
            .Modes[0].MethodComparison.Methods.Single(m =>
                m.Method == "distributed-reconstruction").OuterCandidateRegionIds);
        Assert.Contains("same-tick-person-corroboration:5",
            report.AnswerDifferences.Single(d =>
                d.FromCase == "B-none" && d.ToCase == "B-eligible").ResolvedQuestionIds);
    }

    [Fact]
    public void DuplicateReceiptDoesNotCreateAnotherQuestionOrMethodEvidence()
    {
        var report = ConditionLensAnalysis.Run(Fixture());
        var plain = report.Cases.Single(c => c.Id == "C-none");
        var repeated = report.Cases.Single(c => c.Id == "C-duplicate");
        Assert.Equal(JsonDefaults.Serialize(plain.Modes), JsonDefaults.Serialize(repeated.Modes));
        Assert.Equal(["c-device", "c-person"], repeated.ReceivedOriginalIds);
    }

    [Fact]
    public void AdjacentFollowupAsksForMissingTargetTickEvidenceWithoutReverseProjection()
    {
        var report = ConditionLensAnalysis.Run(Fixture());
        var missing = report.Cases.Single(c => c.Id == "D-none");
        var answered = report.Cases.Single(c => c.Id == "D-eligible");
        var future = report.Cases.Single(c => c.Id == "D-future");
        var offTick = report.Cases.Single(c => c.Id == "D-offtick");
        var question = Assert.Single(missing.Modes[2].Questions);
        Assert.Equal("adjacent-followup-person-evidence:5", question.QuestionId);
        Assert.Equal("received-adjacent-tick-not-target-tick", question.ReasonCode);
        Assert.Equal("received-person-evidence-at-target-tick-or-declared-public-transition",
            question.RequiredInformation);
        Assert.Equal(["d-later"], question.ReceivedOriginalIds);
        Assert.Equal(["root-d-later"], question.SourceAncestryIds);
        Assert.Empty(answered.Modes[2].Questions);
        Assert.Empty(offTick.Modes[2].Questions);
        Assert.Equal(JsonDefaults.Serialize(missing.Modes[2].Questions),
            JsonDefaults.Serialize(future.Modes[2].Questions));
        Assert.DoesNotContain("d-target", future.ReceivedOriginalIds);
        Assert.Equal(JsonDefaults.Serialize(missing.Modes[0].MethodComparison),
            JsonDefaults.Serialize(future.Modes[0].MethodComparison));

        var before = missing.Modes[0].MethodComparison.Methods.Single(m =>
            m.Method == "distributed-reconstruction");
        var after = answered.Modes[0].MethodComparison.Methods.Single(m =>
            m.Method == "distributed-reconstruction");
        Assert.Equal(["a", "b", "c"], before.OuterCandidateRegionIds);
        Assert.Equal(["b"], after.OuterCandidateRegionIds);
        var difference = report.AnswerDifferences.Single(d =>
            d.FromCase == "D-none" && d.ToCase == "D-eligible");
        Assert.Contains(question.QuestionId, difference.ResolvedQuestionIds);
        Assert.All(missing.Modes, mode =>
            Assert.Equal(JsonDefaults.Serialize(missing.Modes[0].MethodComparison),
                JsonDefaults.Serialize(mode.MethodComparison)));
    }

    [Fact]
    public void SingleRootCorroborationIsQuestionedEvenWhenOuterIsOneRegion()
    {
        var report = ConditionLensAnalysis.Run(Fixture());
        var single = report.Cases.Single(c => c.Id == "S1");
        var copy = report.Cases.Single(c => c.Id == "S3");
        var independent = report.Cases.Single(c => c.Id == "S4");
        foreach (var row in new[] { single, copy, independent })
        {
            Assert.Equal(["b"], row.Modes[0].MethodComparison.Methods.Single(m =>
                m.Method == "distributed-reconstruction").OuterCandidateRegionIds);
            Assert.All(row.Modes, mode =>
                Assert.Equal(JsonDefaults.Serialize(row.Modes[0].MethodComparison),
                    JsonDefaults.Serialize(mode.MethodComparison)));
        }
        var first = Assert.Single(single.Modes[3].Questions);
        var replicated = Assert.Single(copy.Modes[3].Questions);
        Assert.Equal("same-tick-person-corroboration:5", first.QuestionId);
        Assert.Equal(first.QuestionId, replicated.QuestionId);
        Assert.Equal(["root-s-first"], first.SourceAncestryIds);
        Assert.Equal(["root-s-first"], replicated.SourceAncestryIds);
        Assert.Equal(["s-first"], first.ReceivedOriginalIds);
        Assert.Equal(["s-first", "s-second"], replicated.ReceivedOriginalIds);
        Assert.Empty(independent.Modes[3].Questions);
        Assert.Contains(first.QuestionId, report.AnswerDifferences.Single(d =>
            d.FromCase == "S1" && d.ToCase == "S4").ResolvedQuestionIds);
    }

    [Fact]
    public async Task NinthLensAblatesReceivedRootsAndWritesActualMethodDifferences()
    {
        var output = Path.Combine(Path.GetTempPath(),
            "footprint-root-ablation-" + Guid.NewGuid().ToString("N"));
        try
        {
            var exit = await CliApplication.RunAsync(
                ["evaluate-condition-lenses", "--fixture", FixturePath(), "--output", output]);
            Assert.Equal(0, exit);
            var saved = await File.ReadAllTextAsync(Path.Combine(output, "condition-lenses.json"));
            var report = ConditionLensAnalysis.Run(Fixture());
            Assert.Equal(JsonDefaults.Serialize(report) + "\n", saved);
            Assert.Equal(9, report.SchemaVersion);
            var eligible = report.Cases.Single(c => c.Id == "B-eligible");
            Assert.Empty(eligible.Modes.Single(m => m.Mode == "06").Questions);
            var question = Assert.Single(eligible.Modes.Single(m => m.Mode == "09").Questions);
            Assert.Equal("independent-person-root-dependence:5", question.QuestionId);
            Assert.Equal("outer-depends-on-received-independent-person-root", question.ReasonCode);
            Assert.Equal(["b-first", "b-second"], question.ReceivedOriginalIds);
            Assert.Equal(["root-first", "root-second"], question.SourceAncestryIds);
            Assert.Equal(["root-first", "root-second"],
                eligible.RootRemovals.Select(r => r.SourceRootId));
            foreach (var removed in eligible.RootRemovals)
            {
                var reconstruction = removed.Methods.Single(m =>
                    m.Method == "distributed-reconstruction");
                Assert.Equal(["b"], reconstruction.BeforeOuter);
                Assert.Single(reconstruction.AddedOuter);
                Assert.Empty(reconstruction.RemovedOuter);
                Assert.Equal(2, reconstruction.BeforeAvailableSourceGroups);
                Assert.Equal(1, reconstruction.AfterAvailableSourceGroups);
                Assert.Single(removed.RemovedReceivedOriginalIds);
            }
            Assert.Equal(["b", "c"], eligible.RootRemovals.Single(r =>
                r.SourceRootId == "root-first").Methods.Single(m =>
                    m.Method == "distributed-reconstruction").AfterOuter);
            Assert.Equal(["a", "b"], eligible.RootRemovals.Single(r =>
                r.SourceRootId == "root-second").Methods.Single(m =>
                    m.Method == "distributed-reconstruction").AfterOuter);
            Assert.Equal(["b"], eligible.Modes[0].MethodComparison.Methods.Single(m =>
                m.Method == "distributed-reconstruction").OuterCandidateRegionIds);
        }
        finally
        {
            if (Directory.Exists(output))
                Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public void NinthLensRejectsNonIndependentAndNonApplicableEvidence()
    {
        var report = ConditionLensAnalysis.Run(Fixture());
        foreach (var id in new[]
        {
            "B-none", "B-future", "B-other-tick", "S1", "S3", "S4",
            "A-none", "A-eligible", "C-none", "D-none", "R-redundant"
        })
        {
            var row = report.Cases.Single(c => c.Id == id);
            Assert.Empty(row.Modes.Single(m => m.Mode == "09").Questions);
            Assert.Equal(JsonDefaults.Serialize(row.Modes[0].MethodComparison),
                JsonDefaults.Serialize(row.Modes.Single(m => m.Mode == "combined").MethodComparison));
        }
        var copied = report.Cases.Single(c => c.Id == "S3");
        var singleRoot = Assert.Single(copied.RootRemovals);
        Assert.Equal("root-s-first", singleRoot.SourceRootId);
        Assert.Equal(["s-first", "s-second"], singleRoot.RemovedReceivedOriginalIds);
        Assert.All(singleRoot.Methods, m => Assert.Equal(0, m.AfterAvailableSourceGroups));
        var redundant = report.Cases.Single(c => c.Id == "R-redundant");
        Assert.Equal(2, redundant.RootRemovals.Count);
        Assert.All(redundant.RootRemovals, removal =>
            Assert.All(removal.Methods, method => Assert.Empty(method.AddedOuter)));
        Assert.Equal(JsonDefaults.Serialize(report), JsonDefaults.Serialize(
            ConditionLensAnalysis.Run(Fixture())));
    }

    [Fact]
    public void PriorTickLensAsksOnlyForReceivedPersonEvidenceAndNeverPropagatesCandidates()
    {
        var report = ConditionLensAnalysis.Run(Fixture());
        var missing = report.Cases.Single(c => c.Id == "E-prior-none");
        var eligible = report.Cases.Single(c => c.Id == "E-prior-eligible");
        var future = report.Cases.Single(c => c.Id == "E-prior-future");
        var followup = report.Cases.Single(c => c.Id == "F-followup");
        var offTick = report.Cases.Single(c => c.Id == "G-offtick");
        var device = report.Cases.Single(c => c.Id == "G-device");

        var question = Assert.Single(missing.Modes.Single(m => m.Mode == "10").Questions);
        Assert.Equal("prior-tick-person-support:5", question.QuestionId);
        Assert.Equal("received-prior-tick-not-target-tick", question.ReasonCode);
        Assert.Equal("received-person-evidence-at-target-tick-or-declared-forward-public-reachability",
            question.RequiredInformation);
        Assert.Equal(["e-prior"], question.ReceivedOriginalIds);
        Assert.Equal(["root-e-prior"], question.SourceAncestryIds);
        Assert.Empty(missing.Modes.Single(m => m.Mode == "04").Questions);
        Assert.Empty(eligible.Modes.Single(m => m.Mode == "10").Questions);
        Assert.Empty(eligible.Modes.Single(m => m.Mode == "04").Questions);
        Assert.Equal(JsonDefaults.Serialize(missing.Modes),
            JsonDefaults.Serialize(future.Modes));
        Assert.DoesNotContain("e-target", future.ReceivedOriginalIds);
        Assert.Empty(offTick.Modes.Single(m => m.Mode == "10").Questions);
        Assert.Empty(device.Modes.Single(m => m.Mode == "10").Questions);
        Assert.Empty(followup.Modes.Single(m => m.Mode == "10").Questions);
        Assert.Single(followup.Modes.Single(m => m.Mode == "04").Questions);

        var before = missing.Modes[0].MethodComparison.Methods.Single(m =>
            m.Method == "distributed-reconstruction");
        var after = eligible.Modes[0].MethodComparison.Methods.Single(m =>
            m.Method == "distributed-reconstruction");
        Assert.Equal(["a", "b", "c"], before.OuterCandidateRegionIds);
        Assert.Equal(["b"], after.OuterCandidateRegionIds);
        Assert.Equal(JsonDefaults.Serialize(missing.Modes[0].MethodComparison),
            JsonDefaults.Serialize(future.Modes[0].MethodComparison));
        var difference = report.AnswerDifferences.Single(d =>
            d.FromCase == "E-prior-none" && d.ToCase == "E-prior-eligible");
        Assert.Contains(question.QuestionId, difference.ResolvedQuestionIds);
        Assert.All(missing.Modes, m =>
            Assert.Equal(JsonDefaults.Serialize(missing.Modes[0].MethodComparison),
                JsonDefaults.Serialize(m.MethodComparison)));
        Assert.Empty(missing.ExistingDeclaredPathDiagnostic.Candidates
            .SelectMany(candidate => candidate.Questions));
    }

    [Fact]
    public void PriorTickQuestionRemainsDeterministicAndNotAnIndependentRoot()
    {
        var report = ConditionLensAnalysis.Run(Fixture());
        foreach (var name in new[] { "E-prior-none", "E-prior-eligible", "E-prior-future",
            "F-followup", "G-offtick", "G-device" })
        {
            var row = report.Cases.Single(c => c.Id == name);
            Assert.Equal(JsonDefaults.Serialize(row.Modes[0].MethodComparison),
                JsonDefaults.Serialize(row.Modes.Single(m => m.Mode == "combined").MethodComparison));
            Assert.All(row.Modes.SelectMany(m => m.Questions), question =>
                Assert.All(question.ReceivedOriginalIds, id =>
                    Assert.Contains(id, row.ReceivedOriginalIds)));
        }
        Assert.Equal(JsonDefaults.Serialize(report), JsonDefaults.Serialize(
            ConditionLensAnalysis.Run(Fixture())));
    }

    [Fact]
    public void NinthLensQuestionsOnConflictingIndependentRootsWithoutChangingCandidates()
    {
        var report = ConditionLensAnalysis.Run(Fixture());
        foreach (var (caseId, left, right) in new[]
        {
            ("K2-conflict", new[] { "a" }, new[] { "c" }),
            ("K3-conflict", new[] { "a", "b" }, new[] { "c" })
        })
        {
            var row = report.Cases.Single(c => c.Id == caseId);
            var question = Assert.Single(row.Modes.Single(m => m.Mode == "09").Questions);
            Assert.Equal("independent-person-root-dependence:5", question.QuestionId);
            Assert.Equal("conflicted-outcome-depends-on-received-independent-person-root",
                question.ReasonCode);
            Assert.Equal("independent-confirmation-of-conflicting-person-source-roots",
                question.RequiredInformation);
            Assert.Equal([caseId == "K2-conflict" ? "k2-left" : "k3-left",
                caseId == "K2-conflict" ? "k2-right" : "k3-right"], question.ReceivedOriginalIds);
            Assert.Equal([caseId == "K2-conflict" ? "root-k2-left" : "root-k3-left",
                caseId == "K2-conflict" ? "root-k2-right" : "root-k3-right"],
                question.SourceAncestryIds);
            Assert.Equal(2, row.RootRemovals.Count);
            foreach (var root in row.RootRemovals)
            {
                Assert.Single(root.RemovedReceivedOriginalIds);
                var method = root.Methods.Single(m => m.Method == "distributed-reconstruction");
                Assert.Equal(["a", "b", "c"], method.BeforeOuter);
                Assert.Equal(["a", "b", "c"], method.BeforeConflicted);
                Assert.Equal(root.SourceRootId.EndsWith("-left", StringComparison.Ordinal)
                    ? right : left, method.AfterOuter);
                Assert.Empty(method.AfterConflicted ?? []);
                Assert.Empty(method.AddedOuter);
                Assert.NotEmpty(method.RemovedOuter);
                Assert.Equal(2, method.BeforeAvailableSourceGroups);
                Assert.Equal(1, method.AfterAvailableSourceGroups);
            }
            Assert.Equal(JsonDefaults.Serialize(row.Modes[0].MethodComparison),
                JsonDefaults.Serialize(row.Modes.Single(m => m.Mode == "09").MethodComparison));
            Assert.Equal(JsonDefaults.Serialize(row.Modes[0].MethodComparison),
                JsonDefaults.Serialize(row.Modes.Single(m => m.Mode == "combined").MethodComparison));
            Assert.Empty(row.Modes.Single(m => m.Mode == "06").Questions);
        }

        var widening = report.Cases.Single(c => c.Id == "B-eligible");
        var nested = report.Cases.Single(c => c.Id == "K4-nested");
        foreach (var row in new[] { widening, nested })
        {
            var question = Assert.Single(row.Modes.Single(m => m.Mode == "09").Questions);
            Assert.Equal("independent-person-root-dependence:5", question.QuestionId);
            Assert.Equal("outer-depends-on-received-independent-person-root", question.ReasonCode);
            Assert.Equal("independent-confirmation-of-root-dependent-person-constraint",
                question.RequiredInformation);
        }
        Assert.Empty(report.Cases.Single(c => c.Id == "R-redundant")
            .Modes.Single(m => m.Mode == "09").Questions);
        Assert.Empty(report.Cases.Single(c => c.Id == "S3")
            .Modes.Single(m => m.Mode == "09").Questions);
        Assert.Equal(JsonDefaults.Serialize(report),
            JsonDefaults.Serialize(ConditionLensAnalysis.Run(Fixture())));
    }


    [Fact]
    public void TwelfthLensSeparatesAdjacentPersonObservationsFromCopiesAndOtherRoots()
    {
        var report = ConditionLensAnalysis.Run(Fixture());
        var repeated = report.Cases.Single(c => c.Id == "T-repeat");
        var copied = report.Cases.Single(c => c.Id == "T-copy");
        var cutoff = report.Cases.Single(c => c.Id == "T-cutoff");
        var question = Assert.Single(repeated.Modes.Single(m => m.Mode == "12").Questions);
        Assert.Equal("repeated-temporal-person-observations:5", question.QuestionId);
        Assert.Equal("repeated-temporal-person-observations-share-source-root",
            question.ReasonCode);
        Assert.Equal("original-observation-generation-provenance-and-times-for-both-received-person-records",
            question.RequiredInformation);
        Assert.Equal(["t-current", "t-prior"], question.ReceivedOriginalIds);
        Assert.Equal(2, question.ReceivedObservationIds.Count);
        Assert.Equal(2, question.ReceivedObservationIds.Distinct(StringComparer.Ordinal).Count());
        Assert.All(question.ReceivedObservationIds, id => Assert.False(string.IsNullOrWhiteSpace(id)));
        Assert.Equal(["root-t-shared"], question.SourceAncestryIds);
        Assert.Contains("ticks 4 and 5", question.Explanation, StringComparison.Ordinal);
        Assert.Empty(repeated.Modes.Single(m => m.Mode == "10").Questions);
        Assert.Single(repeated.Modes.Single(m => m.Mode == "06").Questions);
        Assert.Equal(JsonDefaults.Serialize(repeated.Modes), JsonDefaults.Serialize(copied.Modes));
        Assert.Equal(JsonDefaults.Serialize(repeated.Modes), JsonDefaults.Serialize(cutoff.Modes));
        Assert.Equal(JsonDefaults.Serialize(repeated.ReceivedOriginalIds),
            JsonDefaults.Serialize(copied.ReceivedOriginalIds));
        Assert.Equal(JsonDefaults.Serialize(repeated.Modes[0].MethodComparison),
            JsonDefaults.Serialize(copied.Modes[0].MethodComparison));

        foreach (var id in new[] { "T-independent", "T-future-prior",
            "T-future-current", "T-device", "T-gap", "T-sametick" })
        {
            var row = report.Cases.Single(c => c.Id == id);
            Assert.Empty(row.Modes.Single(m => m.Mode == "12").Questions);
            Assert.Equal(JsonDefaults.Serialize(row.Modes[0].MethodComparison),
                JsonDefaults.Serialize(row.Modes.Single(m => m.Mode == "combined").MethodComparison));
        }
        var futurePrior = report.Cases.Single(c => c.Id == "T-future-prior");
        var futureCurrent = report.Cases.Single(c => c.Id == "T-future-current");
        Assert.DoesNotContain("t-prior", futurePrior.ReceivedOriginalIds);
        Assert.DoesNotContain("t-current", futureCurrent.ReceivedOriginalIds);
        Assert.Single(futureCurrent.Modes.Single(m => m.Mode == "10").Questions);
        Assert.Single(report.Cases.Single(c => c.Id == "T-sametick")
            .Modes.Single(m => m.Mode == "06").Questions);
        Assert.Empty(report.Cases.Single(c => c.Id == "T-device")
            .Modes.Single(m => m.Mode == "12").Questions);
        Assert.All(report.Cases, row =>
        {
            Assert.Equal(11, row.Modes.Count);
            Assert.Equal(JsonDefaults.Serialize(row.Modes[0].MethodComparison),
                JsonDefaults.Serialize(row.Modes.Single(m => m.Mode == "12").MethodComparison));
            Assert.All(row.Modes.SelectMany(m => m.Questions),
                q => Assert.All(q.ReceivedOriginalIds,
                    id => Assert.Contains(id, row.ReceivedOriginalIds)));
        });
        Assert.Equal(JsonDefaults.Serialize(report),
            JsonDefaults.Serialize(ConditionLensAnalysis.Run(Fixture())));
    }


    [Fact]
    public void EighthLensDocumentsExistingRedundantRootCasesAndSixToEightQuestionHandoff()
    {
        var report = ConditionLensAnalysis.Run(Fixture());
        foreach (var (id, originalIds, rootIds) in new[]
        {
            ("S4", new[] { "s-first", "s-second" },
                new[] { "root-s-first", "root-s-second" }),
            ("R-redundant", new[] { "r-first", "r-second" },
                new[] { "root-r-first", "root-r-second" })
        })
        {
            var row = report.Cases.Single(c => c.Id == id);
            var eight = Assert.Single(row.Modes.Single(m => m.Mode == "08").Questions);
            Assert.Equal("apparently-redundant-person-roots-upstream-check:5", eight.QuestionId);
            Assert.Equal("received-distinct-roots-upstream-provenance-unverified-despite-redundant-outcome",
                eight.ReasonCode);
            Assert.Equal(originalIds, eight.ReceivedOriginalIds);
            Assert.Equal(rootIds, eight.SourceAncestryIds);
            Assert.Empty(row.Modes.Single(m => m.Mode == "none").Questions);
            Assert.Empty(row.Modes.Single(m => m.Mode == "06").Questions);
            Assert.Empty(row.Modes.Single(m => m.Mode == "09").Questions);
            var combined = Assert.Single(row.Modes.Single(m => m.Mode == "combined").Questions);
            Assert.Equal(JsonDefaults.Serialize(eight), JsonDefaults.Serialize(combined));
            Assert.Equal(JsonDefaults.Serialize(row.Modes[0].MethodComparison),
                JsonDefaults.Serialize(row.Modes.Single(m => m.Mode == "combined").MethodComparison));
        }

        // Receiving a second claimed root resolves the 06 question, but does not
        // establish upstream independence: the distinct 08 question becomes visible.
        var difference = report.AnswerDifferences.Single(d =>
            d.FromCase == "S1" && d.ToCase == "S4");
        Assert.Equal(["same-tick-person-corroboration:5"], difference.ResolvedQuestionIds);
        Assert.Equal(["apparently-redundant-person-roots-upstream-check:5"],
            difference.NewQuestionIds);
    }

    [Fact]
    public void EighthLensAsksAboutUnknownUpstreamOnlyForRedundantDistinctReceivedRoots()
    {
        var report = ConditionLensAnalysis.Run(Fixture());
        var redundant = report.Cases.Single(c => c.Id == "N-redundant");
        var q = Assert.Single(redundant.Modes.Single(m => m.Mode == "08").Questions);
        Assert.Equal("apparently-redundant-person-roots-upstream-check:5", q.QuestionId);
        Assert.Equal("received-distinct-roots-upstream-provenance-unverified-despite-redundant-outcome",
            q.ReasonCode);
        Assert.Equal("original-observation-generation-sources-forwarding-paths-and-possible-shared-upstream",
            q.RequiredInformation);
        Assert.Equal(["n-left", "n-right"], q.ReceivedOriginalIds);
        Assert.Equal(2, q.ReceivedObservationIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(["root-n-left", "root-n-right"], q.SourceAncestryIds);
        Assert.Equal(["root-n-left", "root-n-right"],
            redundant.RootRemovals.Select(r => r.SourceRootId));
        Assert.Empty(redundant.Modes.Single(m => m.Mode == "06").Questions);
        Assert.Empty(redundant.Modes.Single(m => m.Mode == "09").Questions);
        foreach (var removal in redundant.RootRemovals)
        {
            Assert.Single(removal.RemovedReceivedOriginalIds);
            Assert.Equal(4, removal.Methods.Count);
            foreach (var method in removal.Methods)
            {
                Assert.Equal(2, method.BeforeAvailableSourceGroups);
                Assert.Equal(1, method.AfterAvailableSourceGroups);
                Assert.Equal(JsonDefaults.Serialize(method.BeforeOuter),
                    JsonDefaults.Serialize(method.AfterOuter));
                Assert.Equal(JsonDefaults.Serialize(method.BeforePreferred),
                    JsonDefaults.Serialize(method.AfterPreferred));
                Assert.Equal(JsonDefaults.Serialize(method.BeforeUncertain),
                    JsonDefaults.Serialize(method.AfterUncertain));
                Assert.Equal(JsonDefaults.Serialize(method.BeforeConflicted),
                    JsonDefaults.Serialize(method.AfterConflicted));
            }
        }

        var cutoff = report.Cases.Single(c => c.Id == "N-cutoff");
        var order = report.Cases.Single(c => c.Id == "N-order");
        Assert.Equal(JsonDefaults.Serialize(redundant.Modes), JsonDefaults.Serialize(cutoff.Modes));
        Assert.Equal(JsonDefaults.Serialize(redundant.Modes), JsonDefaults.Serialize(order.Modes));
        Assert.Equal(JsonDefaults.Serialize(redundant.RootRemovals),
            JsonDefaults.Serialize(order.RootRemovals));
        foreach (var id in new[] { "N-overlap", "N-conflict", "N-same-root",
            "N-copy", "N-future", "N-device", "N-offtick" })
        {
            var row = report.Cases.Single(c => c.Id == id);
            Assert.Empty(row.Modes.Single(m => m.Mode == "08").Questions);
            Assert.Equal(JsonDefaults.Serialize(row.Modes[0].MethodComparison),
                JsonDefaults.Serialize(row.Modes.Single(m => m.Mode == "combined").MethodComparison));
        }
        Assert.Single(report.Cases.Single(c => c.Id == "N-overlap")
            .Modes.Single(m => m.Mode == "09").Questions);
        Assert.Single(report.Cases.Single(c => c.Id == "N-conflict")
            .Modes.Single(m => m.Mode == "09").Questions);
        Assert.DoesNotContain("n-right", report.Cases.Single(c => c.Id == "N-future")
            .ReceivedOriginalIds);
        Assert.Single(report.Cases.Single(c => c.Id == "N-cutoff")
            .Modes.Single(m => m.Mode == "08").Questions);
        Assert.Equal(56, report.Cases.Count);
        Assert.All(report.Cases, row => Assert.Equal(11, row.Modes.Count));
        Assert.Equal(JsonDefaults.Serialize(report), JsonDefaults.Serialize(
            ConditionLensAnalysis.Run(Fixture())));
    }


    // Rev8 independent review: test the U-none receipt gate itself rather than
    // inferring it only from the report's empty received-original ID array.
    [Fact]
    public void NatthiUNonePassesReceiptBuilderWithNoReceivedRecords()
    {
        var fixture = System.Text.Json.JsonSerializer.Deserialize<ConditionLensFixture>(
            Fixture(), new System.Text.Json.JsonSerializerOptions(
                System.Text.Json.JsonSerializerDefaults.Web));
        Assert.NotNull(fixture);
        var unreceived = fixture.Cases.Single(c => c.Id == "U-none");
        Assert.Equal(2, unreceived.Originals.Count);
        Assert.All(unreceived.Originals, original => Assert.Null(original.ReceivedAtTick));

        var regions = fixture.PublicRegionIds.OrderBy(id => id, StringComparer.Ordinal).ToArray();
        var publicWorld = new RescueContext
        {
            ScenarioId = fixture.ScenarioId,
            TargetPersonId = fixture.TargetPersonId,
            NormalWalkingRadius = 0,
            PublicDeviceIds = fixture.Cases.SelectMany(c => c.Originals)
                .Select(o => o.HolderDeviceId).Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            PublicRegions = regions.Select((id, index) => new RegionDefinition
            {
                Id = id,
                Type = RegionType.Outdoor,
                Center = new Position(index, 0, 0, id)
            }).ToArray(),
            PublicCausalTracingModel = new PublicCausalTracingModel
            {
                CandidateStates =
                [
                    new PublicCandidateWarpStateDefinition
                    {
                        TimeWindow = InteractionTimeWindow.At(fixture.TargetTick),
                        TemporalKind = CandidateWarpTemporalKind.EventPoint,
                        RegionIds = regions,
                        EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
                        Provenance =
                        [
                            CandidateWarpProvenance.Create(
                                CandidateWarpSourceKind.PublicCausalModel,
                                "condition-lens-public-prior", 0, false, false,
                                EvidenceState.SupportedButUncertain, regions)
                        ]
                    }
                ]
            }
        };
        var held = unreceived.Originals.OrderBy(o => o.Id, StringComparer.Ordinal)
            .Select(o =>
            {
                var person = o.EvidenceKind == "person";
                var observation = InteractionObservation.Create(
                    fixture.ScenarioId, fixture.Seed, o.Id, null,
                    InteractionRelationKind.PersonPersonEncounter,
                    [
                        new InteractionParticipant(
                            person ? fixture.TargetPersonId : o.HolderDeviceId,
                            person ? InteractionEntityKind.Person : InteractionEntityKind.Device,
                            InteractionParticipantRole.EncounterParticipant),
                        new InteractionParticipant("synthetic-peer", InteractionEntityKind.Person,
                            InteractionParticipantRole.EncounterParticipant)
                    ],
                    InteractionTimeWindow.At(o.ObservedTick),
                    o.RegionIds, o.ObservedTick, EvidenceState.Confirmed,
                    isPersonEvidence: person, isDeviceOnlyEvidence: !person,
                    InteractionVisibility.PrivateEvidence,
                    [new InteractionProvenance(
                        InteractionProvenanceKind.ExternalSource, o.SourceAncestryId)]);
                return new DeviceHeldInteractionRecord(
                    o.HolderDeviceId, o.ObservedTick, observation);
            }).ToArray();
        var visible = new RescuerEvidenceInputBuilder().BuildDistinctOriginals(
            publicWorld, fixture.Seed, fixture.ReceiptCutoffTick, fixture.RescuerId,
            held, Array.Empty<InteractionRecordReceipt>(), regions);
        Assert.Empty(visible.ReceivedRecords);
        var methods = RescuerEvidenceMethodComparison.Compare(
            visible, InteractionTimeWindow.At(fixture.TargetTick)).Methods;
        Assert.Equal(4, methods.Count);
        Assert.All(methods, method =>
        {
            Assert.Equal(["a", "b", "c"], method.OuterCandidateRegionIds);
            Assert.Equal(0, method.AvailableSourceGroupCount);
        });
    }

    [Fact]
    public void NoReceivedEvidenceDoesNotProvePersonAbsence()
    {
        var report = ConditionLensAnalysis.Run(Fixture());
        Assert.Equal(56, report.Cases.Count);
        Assert.Equal(49, report.Cases.Count(c =>
            !c.Id.StartsWith("U-", StringComparison.Ordinal)));
        Assert.Equal("limited-analogy-implemented",
            report.Index.Single(x => x.Id == "22").Status);
        var before = report.Cases.Single(c => c.Id == "U-none");
        var future = report.Cases.Single(c => c.Id == "U-future");
        Assert.Empty(before.ReceivedOriginalIds);
        Assert.Empty(before.RootRemovals);
        var question = Assert.Single(before.Modes.Single(m => m.Mode == "22").Questions);
        Assert.Equal("no-received-records-person-availability:5", question.QuestionId);
        Assert.Equal("no-received-evidence-is-not-person-absence", question.ReasonCode);
        Assert.Equal(["22"], question.LensIds);
        Assert.Equal("P", question.TargetPersonId);
        Assert.Equal(5, question.TargetTick);
        Assert.Equal(10, question.ReceiptCutoffTick);
        Assert.Empty(question.ReceivedOriginalIds);
        Assert.Empty(question.ReceivedObservationIds);
        Assert.Empty(question.SourceAncestryIds);
        Assert.DoesNotContain("u-person", question.Explanation);
        Assert.DoesNotContain("u-held", question.Explanation);
        Assert.DoesNotContain("root-u", question.Explanation);
        Assert.All(before.Modes[0].MethodComparison.Methods,
            m => Assert.Equal(0, m.AvailableSourceGroupCount));
        Assert.Equal(JsonDefaults.Serialize(before.Modes),
            JsonDefaults.Serialize(future.Modes));
        Assert.Empty(future.ReceivedOriginalIds);
        Assert.Equal(JsonDefaults.Serialize(before.ExistingDeclaredPathDiagnostic),
            JsonDefaults.Serialize(future.ExistingDeclaredPathDiagnostic));
        Assert.All(report.Cases.Where(c => c.Id.StartsWith("U-", StringComparison.Ordinal)
            && c.Id is not ("U-none" or "U-future")), c =>
            Assert.Empty(c.Modes.Single(m => m.Mode == "22").Questions));

        var eligible = report.Cases.Single(c => c.Id == "U-eligible");
        Assert.Equal(["u-person"], eligible.ReceivedOriginalIds);
        Assert.Single(eligible.Modes.Single(m => m.Mode == "06").Questions);
        var duplicate = report.Cases.Single(c => c.Id == "U-duplicate");
        Assert.Equal(["u-person"], duplicate.ReceivedOriginalIds);
        Assert.Equal(JsonDefaults.Serialize(eligible.Modes),
            JsonDefaults.Serialize(duplicate.Modes));
        Assert.Single(report.Cases.Single(c => c.Id == "U-device").Modes
            .Single(m => m.Mode == "02").Questions);
        Assert.Single(report.Cases.Single(c => c.Id == "U-followup").Modes
            .Single(m => m.Mode == "04").Questions);
        Assert.Single(report.Cases.Single(c => c.Id == "U-prior").Modes
            .Single(m => m.Mode == "10").Questions);
        var diff = report.AnswerDifferences.Single(d =>
            d.FromCase == "U-none" && d.ToCase == "U-eligible");
        Assert.Contains(question.QuestionId, diff.ResolvedQuestionIds);
        Assert.Contains("same-tick-person-corroboration:5", diff.NewQuestionIds);
        Assert.Contains(diff.Methods, m =>
            !m.BeforeOuter.SequenceEqual(m.AfterOuter, StringComparer.Ordinal));
        var futureDiff = report.AnswerDifferences.Single(d =>
            d.FromCase == "U-none" && d.ToCase == "U-future");
        Assert.Empty(futureDiff.ResolvedQuestionIds);
        Assert.Empty(futureDiff.NewQuestionIds);
        Assert.All(futureDiff.Methods, m =>
        {
            Assert.Empty(m.AddedOuter);
            Assert.Empty(m.RemovedOuter);
            Assert.Equal(JsonDefaults.Serialize(m.BeforeOuter), JsonDefaults.Serialize(m.AfterOuter));
        });
        Assert.All(report.Cases, c =>
            Assert.All(c.Modes, m =>
                Assert.Equal(JsonDefaults.Serialize(c.Modes[0].MethodComparison),
                    JsonDefaults.Serialize(m.MethodComparison))));
    }

    [Fact]
    public async Task NoReceiptQuestionAppearsInSavedCliJsonWithoutPrivateIds()
    {
        var output = Path.Combine(Path.GetTempPath(),
            "footprint-natthi-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Equal(0, await CliApplication.RunAsync(
                ["evaluate-condition-lenses", "--fixture", FixturePath(), "--output", output]));
            var path = Path.Combine(output, "condition-lenses.json");
            var bytes = await File.ReadAllBytesAsync(path);
            using var doc = System.Text.Json.JsonDocument.Parse(bytes);
            var root = doc.RootElement;
            Assert.Equal(9, root.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(56, root.GetProperty("cases").GetArrayLength());
            var none = root.GetProperty("cases").EnumerateArray().Single(c =>
                c.GetProperty("id").GetString() == "U-none");
            Assert.Equal(0, none.GetProperty("receivedOriginalIds").GetArrayLength());
            var mode = none.GetProperty("modes").EnumerateArray().Single(m =>
                m.GetProperty("mode").GetString() == "22");
            var q = mode.GetProperty("questions").EnumerateArray().Single();
            Assert.Equal("no-received-evidence-is-not-person-absence",
                q.GetProperty("reasonCode").GetString());
            Assert.Equal(0, q.GetProperty("receivedOriginalIds").GetArrayLength());
            Assert.Equal(0, q.GetProperty("receivedObservationIds").GetArrayLength());
            Assert.Equal(0, q.GetProperty("sourceAncestryIds").GetArrayLength());
            Assert.DoesNotContain("u-person", q.GetRawText());
            Assert.DoesNotContain("root-u", q.GetRawText());
            var future = root.GetProperty("cases").EnumerateArray().Single(c =>
                c.GetProperty("id").GetString() == "U-future");
            Assert.Equal(JsonDefaults.Serialize(none.GetProperty("modes")),
                JsonDefaults.Serialize(future.GetProperty("modes")));
            var after = root.GetProperty("cases").EnumerateArray().Single(c =>
                c.GetProperty("id").GetString() == "U-eligible");
            Assert.Single(after.GetProperty("modes").EnumerateArray().Single(m =>
                m.GetProperty("mode").GetString() == "06").GetProperty("questions").EnumerateArray());
            // Assert the Rev8 answer transitions from the bytes saved by the CLI,
            // independent of in-memory AnswerDifferences and serialization equality.
            var noneMethods = none.GetProperty("modes").EnumerateArray()
                .Single(m => m.GetProperty("mode").GetString() == "none")
                .GetProperty("methodComparison");
            Assert.Equal(4, noneMethods.GetProperty("methods").GetArrayLength());
            foreach (var modeName in new[] { "none", "22", "combined" })
            {
                var methodRow = none.GetProperty("modes").EnumerateArray()
                    .Single(m => m.GetProperty("mode").GetString() == modeName)
                    .GetProperty("methodComparison");
                Assert.Equal(JsonDefaults.Serialize(noneMethods),
                    JsonDefaults.Serialize(methodRow));
            }
            foreach (var methodRow in noneMethods.GetProperty("methods").EnumerateArray())
            {
                Assert.Equal(["a", "b", "c"], methodRow
                    .GetProperty("outerCandidateRegionIds").EnumerateArray()
                    .Select(region => region.GetString()));
                Assert.Equal(0, methodRow.GetProperty("availableSourceGroupCount").GetInt32());
            }

            var differences = root.GetProperty("answerDifferences").EnumerateArray().ToArray();
            var eligibleDifference = differences.Single(d =>
                d.GetProperty("fromCase").GetString() == "U-none" &&
                d.GetProperty("toCase").GetString() == "U-eligible");
            Assert.Contains("no-received-records-person-availability:5",
                eligibleDifference.GetProperty("resolvedQuestionIds").EnumerateArray()
                    .Select(id => id.GetString()));
            Assert.Contains("same-tick-person-corroboration:5",
                eligibleDifference.GetProperty("newQuestionIds").EnumerateArray()
                    .Select(id => id.GetString()));
            var eligibleMethodDiffs = eligibleDifference.GetProperty("methods")
                .EnumerateArray().ToArray();
            Assert.Equal(4, eligibleMethodDiffs.Length);
            Assert.Contains(eligibleMethodDiffs, methodDiff =>
                !JsonDefaults.Serialize(methodDiff.GetProperty("beforeOuter")).Equals(
                    JsonDefaults.Serialize(methodDiff.GetProperty("afterOuter")),
                    StringComparison.Ordinal));

            var futureDifference = differences.Single(d =>
                d.GetProperty("fromCase").GetString() == "U-none" &&
                d.GetProperty("toCase").GetString() == "U-future");
            Assert.Empty(futureDifference.GetProperty("resolvedQuestionIds").EnumerateArray());
            Assert.Empty(futureDifference.GetProperty("newQuestionIds").EnumerateArray());
            var futureMethodDiffs = futureDifference.GetProperty("methods")
                .EnumerateArray().ToArray();
            Assert.Equal(4, futureMethodDiffs.Length);
            foreach (var methodDiff in futureMethodDiffs)
            {
                Assert.Empty(methodDiff.GetProperty("addedOuter").EnumerateArray());
                Assert.Empty(methodDiff.GetProperty("removedOuter").EnumerateArray());
                foreach (var part in new[] { "Outer", "Uncertain", "Conflicted" })
                    Assert.Equal(JsonDefaults.Serialize(methodDiff.GetProperty("before" + part)),
                        JsonDefaults.Serialize(methodDiff.GetProperty("after" + part)));
                Assert.Equal(methodDiff.GetProperty("beforeAvailableSourceGroups").GetInt32(),
                    methodDiff.GetProperty("afterAvailableSourceGroups").GetInt32());
            }
            var futureMethods = future.GetProperty("modes").EnumerateArray()
                .Single(m => m.GetProperty("mode").GetString() == "none")
                .GetProperty("methodComparison");
            Assert.Equal(JsonDefaults.Serialize(noneMethods),
                JsonDefaults.Serialize(futureMethods));
            Assert.Equal(0, await CliApplication.RunAsync(
                ["evaluate-condition-lenses", "--fixture", FixturePath(), "--output", output]));
            Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
        }
        finally
        {
            if (Directory.Exists(output))
                Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public void DistantReceivedPersonLensSeparatesTemporalApplicabilityFromAdjacentEvidence()
    {
        var report = ConditionLensAnalysis.Run(Fixture());
        Assert.Equal(56, report.Cases.Count);
        Assert.Equal(9, report.SchemaVersion);
        Assert.Equal("limited-analogy-implemented",
            report.Index.Single(e => e.Id == "21").Status);
        Assert.All(report.Cases, c => Assert.Equal(11, c.Modes.Count));

        var none = report.Cases.Single(c => c.Id == "H-none");
        var eligible = report.Cases.Single(c => c.Id == "H-eligible");
        var future = report.Cases.Single(c => c.Id == "H-future");
        var duplicate = report.Cases.Single(c => c.Id == "H-duplicate");
        var question = Assert.Single(none.Modes.Single(m => m.Mode == "21").Questions);
        Assert.Equal("distant-person-temporal-applicability:5", question.QuestionId);
        Assert.Equal(["21"], question.LensIds);
        Assert.Equal("received-distant-person-observation-not-target-tick", question.ReasonCode);
        Assert.Equal("direct-target-tick-person-evidence-or-declared-temporal-applicability",
            question.RequiredInformation);
        Assert.Equal("P", question.TargetPersonId);
        Assert.Equal(5, question.TargetTick);
        Assert.Equal(10, question.ReceiptCutoffTick);
        Assert.Equal(["h-distant"], question.ReceivedOriginalIds);
        Assert.Single(question.ReceivedObservationIds);
        Assert.All(question.ReceivedObservationIds, id => Assert.False(string.IsNullOrWhiteSpace(id)));
        Assert.Equal(["root-h-distant"], question.SourceAncestryIds);
        Assert.Contains("tick(s) 7", question.Explanation);
        Assert.DoesNotContain("h-target", JsonDefaults.Serialize(question));
        Assert.DoesNotContain("root-h-target", JsonDefaults.Serialize(question));
        Assert.Equal(["h-distant"], none.ReceivedOriginalIds);
        Assert.Empty(none.Modes.Single(m => m.Mode == "04").Questions);
        Assert.Empty(none.Modes.Single(m => m.Mode == "10").Questions);
        Assert.Empty(none.Modes.Single(m => m.Mode == "22").Questions);
        Assert.Empty(none.ExistingDeclaredPathDiagnostic.Candidates
            .SelectMany(candidate => candidate.Questions));

        Assert.Equal(["h-distant", "h-target"], eligible.ReceivedOriginalIds);
        Assert.Empty(eligible.Modes.Single(m => m.Mode == "21").Questions);
        Assert.Single(eligible.Modes.Single(m => m.Mode == "06").Questions);
        Assert.Equal(["h-distant"], future.ReceivedOriginalIds);
        Assert.Equal(["h-distant"], duplicate.ReceivedOriginalIds);
        Assert.Equal(JsonDefaults.Serialize(none.Modes), JsonDefaults.Serialize(future.Modes));
        Assert.Equal(JsonDefaults.Serialize(none.Modes), JsonDefaults.Serialize(duplicate.Modes));
        Assert.Equal(JsonDefaults.Serialize(none.ExistingDeclaredPathDiagnostic),
            JsonDefaults.Serialize(future.ExistingDeclaredPathDiagnostic));

        var diff = report.AnswerDifferences.Single(d =>
            d.FromCase == "H-none" && d.ToCase == "H-eligible");
        Assert.Contains(question.QuestionId, diff.ResolvedQuestionIds);
        Assert.Contains("same-tick-person-corroboration:5", diff.NewQuestionIds);
        Assert.Equal(4, diff.Methods.Count);
        // A legal new receipt may change different methods differently. Verify
        // every answer-difference field against the actual per-method Compare rows.
        var baselineRows = none.Modes[0].MethodComparison.Methods;
        var eligibleRows = eligible.Modes[0].MethodComparison.Methods;
        Assert.Equal(baselineRows.Select(m => m.Method).OrderBy(id => id, StringComparer.Ordinal),
            diff.Methods.Select(m => m.Method).OrderBy(id => id, StringComparer.Ordinal));
        foreach (var delta in diff.Methods)
        {
            var original = baselineRows.Single(m => m.Method == delta.Method);
            var received = eligibleRows.Single(m => m.Method == delta.Method);
            Assert.Equal(original.OuterCandidateRegionIds, delta.BeforeOuter);
            Assert.Equal(received.OuterCandidateRegionIds, delta.AfterOuter);
            Assert.Equal(JsonDefaults.Serialize(original.UncertainCandidateRegionIds),
                JsonDefaults.Serialize(delta.BeforeUncertain));
            Assert.Equal(JsonDefaults.Serialize(received.UncertainCandidateRegionIds),
                JsonDefaults.Serialize(delta.AfterUncertain));
            Assert.Equal(JsonDefaults.Serialize(original.ConflictedCandidateRegionIds),
                JsonDefaults.Serialize(delta.BeforeConflicted));
            Assert.Equal(JsonDefaults.Serialize(received.ConflictedCandidateRegionIds),
                JsonDefaults.Serialize(delta.AfterConflicted));
            Assert.Equal(original.AvailableSourceGroupCount, delta.BeforeAvailableSourceGroups);
            Assert.Equal(received.AvailableSourceGroupCount, delta.AfterAvailableSourceGroups);
            Assert.Equal(original.OuterCandidateRegionIds.Except(received.OuterCandidateRegionIds,
                StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal), delta.RemovedOuter);
            Assert.Equal(received.OuterCandidateRegionIds.Except(original.OuterCandidateRegionIds,
                StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal), delta.AddedOuter);
        }
        var before = none.Modes[0].MethodComparison.Methods.Single(m =>
            m.Method == "distributed-reconstruction");
        var after = eligible.Modes[0].MethodComparison.Methods.Single(m =>
            m.Method == "distributed-reconstruction");
        Assert.Equal(["a", "b", "c"], before.OuterCandidateRegionIds);
        Assert.Equal(["b"], after.OuterCandidateRegionIds);
        Assert.Contains(diff.Methods, m =>
            !m.BeforeOuter.SequenceEqual(m.AfterOuter, StringComparer.Ordinal));
        foreach (var condition in new[] { "H-future", "H-duplicate" })
        {
            var unchanged = report.AnswerDifferences.Single(d =>
                d.FromCase == "H-none" && d.ToCase == condition);
            Assert.Empty(unchanged.ResolvedQuestionIds);
            Assert.Empty(unchanged.NewQuestionIds);
            Assert.Equal(4, unchanged.Methods.Count);
            Assert.All(unchanged.Methods, m =>
            {
                Assert.Equal(JsonDefaults.Serialize(m.BeforeOuter), JsonDefaults.Serialize(m.AfterOuter));
                Assert.Equal(JsonDefaults.Serialize(m.BeforeUncertain), JsonDefaults.Serialize(m.AfterUncertain));
                Assert.Equal(JsonDefaults.Serialize(m.BeforeConflicted), JsonDefaults.Serialize(m.AfterConflicted));
                Assert.Equal(m.BeforeAvailableSourceGroups, m.AfterAvailableSourceGroups);
                Assert.Empty(m.AddedOuter);
                Assert.Empty(m.RemovedOuter);
            });
        }

        // The two earlier distant observations acquire only the new 21 question.
        // The old adjacent and same-tick modes remain independent of this lens.
        foreach (var id in new[] { "D-offtick", "G-offtick" })
        {
            var row = report.Cases.Single(c => c.Id == id);
            Assert.Single(row.Modes.Single(m => m.Mode == "21").Questions);
            Assert.Empty(row.Modes.Single(m => m.Mode == "04").Questions);
            Assert.Empty(row.Modes.Single(m => m.Mode == "10").Questions);
        }
        foreach (var id in new[] { "T-gap", "D-none", "E-prior-none",
            "F-followup", "U-none", "U-device", "U-followup", "U-prior" })
            Assert.Empty(report.Cases.Single(c => c.Id == id)
                .Modes.Single(m => m.Mode == "21").Questions);
        foreach (var row in report.Cases)
        {
            Assert.All(row.Modes, mode =>
                Assert.Equal(JsonDefaults.Serialize(row.Modes[0].MethodComparison),
                    JsonDefaults.Serialize(mode.MethodComparison)));
            foreach (var oldMode in new[] { "none", "02", "04", "06", "08",
                "09", "10", "12", "22" })
                Assert.DoesNotContain(row.Modes.Single(m => m.Mode == oldMode).Questions,
                    q => q.LensIds.Contains("21"));
        }
    }

    [Fact]
    public async Task AtthiQuestionAndFourMethodComparisonsAreVerifiedFromSavedCliJson()
    {
        var output = Path.Combine(Path.GetTempPath(),
            "footprint-atthi-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Equal(0, await CliApplication.RunAsync(
                ["evaluate-condition-lenses", "--fixture", FixturePath(), "--output", output]));
            var path = Path.Combine(output, "condition-lenses.json");
            var bytes = await File.ReadAllBytesAsync(path);
            using var doc = System.Text.Json.JsonDocument.Parse(bytes);
            var root = doc.RootElement;
            Assert.Equal(9, root.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(56, root.GetProperty("cases").GetArrayLength());
            var cases = root.GetProperty("cases").EnumerateArray().ToArray();
            System.Text.Json.JsonElement Case(string id) => cases.Single(c =>
                c.GetProperty("id").GetString() == id);
            System.Text.Json.JsonElement Mode(System.Text.Json.JsonElement c, string id) =>
                c.GetProperty("modes").EnumerateArray().Single(m =>
                    m.GetProperty("mode").GetString() == id);
            var none = Case("H-none");
            var eligible = Case("H-eligible");
            var future = Case("H-future");
            var duplicate = Case("H-duplicate");
            var q = Mode(none, "21").GetProperty("questions").EnumerateArray().Single();
            Assert.Equal("distant-person-temporal-applicability:5",
                q.GetProperty("questionId").GetString());
            Assert.Equal("received-distant-person-observation-not-target-tick",
                q.GetProperty("reasonCode").GetString());
            Assert.Equal("direct-target-tick-person-evidence-or-declared-temporal-applicability",
                q.GetProperty("requiredInformation").GetString());
            Assert.Equal("h-distant", q.GetProperty("receivedOriginalIds")[0].GetString());
            Assert.Single(q.GetProperty("receivedObservationIds").EnumerateArray());
            Assert.Equal("root-h-distant", q.GetProperty("sourceAncestryIds")[0].GetString());
            Assert.DoesNotContain("h-target", q.GetRawText());
            Assert.DoesNotContain("root-h-target", q.GetRawText());
            Assert.Equal(0, Mode(eligible, "21").GetProperty("questions").GetArrayLength());
            Assert.Single(Mode(eligible, "06").GetProperty("questions").EnumerateArray());
            Assert.Equal(JsonDefaults.Serialize(none.GetProperty("receivedOriginalIds")),
                JsonDefaults.Serialize(future.GetProperty("receivedOriginalIds")));
            Assert.Equal(JsonDefaults.Serialize(none.GetProperty("receivedOriginalIds")),
                JsonDefaults.Serialize(duplicate.GetProperty("receivedOriginalIds")));
            Assert.Equal(JsonDefaults.Serialize(none.GetProperty("modes")),
                JsonDefaults.Serialize(future.GetProperty("modes")));
            Assert.Equal(JsonDefaults.Serialize(none.GetProperty("modes")),
                JsonDefaults.Serialize(duplicate.GetProperty("modes")));

            var beforeMethods = Mode(none, "none").GetProperty("methodComparison")
                .GetProperty("methods").EnumerateArray().ToArray();
            var afterMethods = Mode(eligible, "none").GetProperty("methodComparison")
                .GetProperty("methods").EnumerateArray().ToArray();
            Assert.Equal(4, beforeMethods.Length);
            Assert.Equal(4, afterMethods.Length);
            foreach (var before in beforeMethods)
            {
                var method = before.GetProperty("method").GetString();
                var after = afterMethods.Single(m => m.GetProperty("method").GetString() == method);
                foreach (var prop in new[] { "outerCandidateRegionIds", "uncertainCandidateRegionIds",
                    "conflictedCandidateRegionIds", "availableSourceGroupCount" })
                {
                    // Different values are permitted on a NEW legal receipt, never by a question alone.
                    Assert.Equal(JsonDefaults.Serialize(before.GetProperty(prop)),
                        JsonDefaults.Serialize(Mode(none, "21").GetProperty("methodComparison")
                            .GetProperty("methods").EnumerateArray().Single(m =>
                                m.GetProperty("method").GetString() == method).GetProperty(prop)));
                    Assert.Equal(JsonDefaults.Serialize(before.GetProperty(prop)),
                        JsonDefaults.Serialize(Mode(future, "none").GetProperty("methodComparison")
                            .GetProperty("methods").EnumerateArray().Single(m =>
                                m.GetProperty("method").GetString() == method).GetProperty(prop)));
                    Assert.Equal(JsonDefaults.Serialize(before.GetProperty(prop)),
                        JsonDefaults.Serialize(Mode(duplicate, "none").GetProperty("methodComparison")
                            .GetProperty("methods").EnumerateArray().Single(m =>
                                m.GetProperty("method").GetString() == method).GetProperty(prop)));
                }
                Assert.Equal(JsonDefaults.Serialize(after),
                    JsonDefaults.Serialize(Mode(eligible, "21").GetProperty("methodComparison")
                        .GetProperty("methods").EnumerateArray().Single(m =>
                            m.GetProperty("method").GetString() == method)));
            }
            var differences = root.GetProperty("answerDifferences").EnumerateArray().ToArray();
            var eligibleDifference = differences.Single(d =>
                d.GetProperty("fromCase").GetString() == "H-none" &&
                d.GetProperty("toCase").GetString() == "H-eligible");
            Assert.Contains("distant-person-temporal-applicability:5",
                eligibleDifference.GetProperty("resolvedQuestionIds").EnumerateArray()
                    .Select(x => x.GetString()));
            Assert.Contains("same-tick-person-corroboration:5",
                eligibleDifference.GetProperty("newQuestionIds").EnumerateArray()
                    .Select(x => x.GetString()));
            var eligibleMethodDeltas = eligibleDifference.GetProperty("methods")
                .EnumerateArray().ToArray();
            Assert.Equal(4, eligibleMethodDeltas.Length);
            Assert.Equal(beforeMethods.Select(m => m.GetProperty("method").GetString())
                    .OrderBy(id => id, StringComparer.Ordinal),
                eligibleMethodDeltas.Select(m => m.GetProperty("method").GetString())
                    .OrderBy(id => id, StringComparer.Ordinal));
            // Read the CLI-saved JSON itself: all four method deltas must match
            // their own H-none and H-eligible Compare outputs, including no-change rows.
            foreach (var delta in eligibleMethodDeltas)
            {
                var method = delta.GetProperty("method").GetString();
                var original = beforeMethods.Single(m => m.GetProperty("method").GetString() == method);
                var received = afterMethods.Single(m => m.GetProperty("method").GetString() == method);
                foreach (var (rowField, deltaField) in new[]
                {
                    ("outerCandidateRegionIds", "Outer"),
                    ("uncertainCandidateRegionIds", "Uncertain"),
                    ("conflictedCandidateRegionIds", "Conflicted")
                })
                {
                    Assert.Equal(JsonDefaults.Serialize(original.GetProperty(rowField)),
                        JsonDefaults.Serialize(delta.GetProperty("before" + deltaField)));
                    Assert.Equal(JsonDefaults.Serialize(received.GetProperty(rowField)),
                        JsonDefaults.Serialize(delta.GetProperty("after" + deltaField)));
                }
                Assert.Equal(original.GetProperty("availableSourceGroupCount").GetInt32(),
                    delta.GetProperty("beforeAvailableSourceGroups").GetInt32());
                Assert.Equal(received.GetProperty("availableSourceGroupCount").GetInt32(),
                    delta.GetProperty("afterAvailableSourceGroups").GetInt32());
                var oldOuter = original.GetProperty("outerCandidateRegionIds").EnumerateArray()
                    .Select(region => region.GetString()!).ToArray();
                var newOuter = received.GetProperty("outerCandidateRegionIds").EnumerateArray()
                    .Select(region => region.GetString()!).ToArray();
                Assert.Equal(oldOuter.Except(newOuter, StringComparer.Ordinal)
                        .OrderBy(id => id, StringComparer.Ordinal),
                    delta.GetProperty("removedOuter").EnumerateArray().Select(region => region.GetString()));
                Assert.Equal(newOuter.Except(oldOuter, StringComparer.Ordinal)
                        .OrderBy(id => id, StringComparer.Ordinal),
                    delta.GetProperty("addedOuter").EnumerateArray().Select(region => region.GetString()));
            }
            foreach (var id in new[] { "H-future", "H-duplicate" })
            {
                var diff = differences.Single(d =>
                    d.GetProperty("fromCase").GetString() == "H-none" &&
                    d.GetProperty("toCase").GetString() == id);
                Assert.Empty(diff.GetProperty("resolvedQuestionIds").EnumerateArray());
                Assert.Empty(diff.GetProperty("newQuestionIds").EnumerateArray());
                Assert.Equal(4, diff.GetProperty("methods").GetArrayLength());
                foreach (var method in diff.GetProperty("methods").EnumerateArray())
                {
                    Assert.Empty(method.GetProperty("addedOuter").EnumerateArray());
                    Assert.Empty(method.GetProperty("removedOuter").EnumerateArray());
                    foreach (var name in new[] { "Outer", "Uncertain", "Conflicted" })
                        Assert.Equal(JsonDefaults.Serialize(method.GetProperty("before" + name)),
                            JsonDefaults.Serialize(method.GetProperty("after" + name)));
                    Assert.Equal(method.GetProperty("beforeAvailableSourceGroups").GetInt32(),
                        method.GetProperty("afterAvailableSourceGroups").GetInt32());
                }
            }
            Assert.Equal(0, await CliApplication.RunAsync(
                ["evaluate-condition-lenses", "--fixture", FixturePath(), "--output", output]));
            Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
        }
        finally
        {
            if (Directory.Exists(output))
                Directory.Delete(output, recursive: true);
        }
    }


    [Fact]
    public void OffTickDeviceReceiptCreatesOnlyTheMissingPersonQuestion()
    {
        var report = ConditionLensAnalysis.Run(Fixture());
        var fixture = System.Text.Json.JsonSerializer.Deserialize<ConditionLensFixture>(
            Fixture(), new System.Text.Json.JsonSerializerOptions(
                System.Text.Json.JsonSerializerDefaults.Web));
        Assert.NotNull(fixture);
        // Independently inventory every frozen case, rather than checking only
        // the known counterexample. Synthetic observations are exact/Confirmed.
        var matching = fixture.Cases.Where(c =>
        {
            var legal = c.Originals.Where(o => o.ReceivedAtTick is not null &&
                o.ReceivedAtTick.Value <= fixture.ReceiptCutoffTick).ToArray();
            return legal.Length > 0 && legal.All(o =>
                o.EvidenceKind == "device" && o.ObservedTick != fixture.TargetTick);
        }).Select(c => c.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        Assert.Equal(["G-device"], matching);
        Assert.Equal(56, report.Cases.Count);
        foreach (var row in report.Cases)
        {
            var questions = row.Modes.Single(m => m.Mode == "22").Questions
                .Where(q => q.QuestionId == "off-tick-device-only-person-availability:5")
                .ToArray();
            Assert.Equal(row.Id == "G-device" ? 1 : 0, questions.Length);
            Assert.Equal(11, row.Modes.Count);
            foreach (var mode in row.Modes)
            {
                // Verify every method's outer/preferred/uncertain/conflicted/source
                // group values, not just a candidate count.
                Assert.Equal(JsonDefaults.Serialize(row.Modes[0].MethodComparison),
                    JsonDefaults.Serialize(mode.MethodComparison));
            }
        }
        var device = report.Cases.Single(c => c.Id == "G-device");
        Assert.Equal(["g-device"], device.ReceivedOriginalIds);
        var q = Assert.Single(device.Modes.Single(m => m.Mode == "22").Questions);
        Assert.Equal(["22"], q.LensIds);
        Assert.Equal("received-offtick-device-without-target-person-evidence", q.ReasonCode);
        Assert.Equal("legitimately-received-target-person-evidence-or-availability-check",
            q.RequiredInformation);
        Assert.Equal("P", q.TargetPersonId);
        Assert.Equal(5, q.TargetTick);
        Assert.Equal(10, q.ReceiptCutoffTick);
        Assert.Equal(["g-device"], q.ReceivedOriginalIds);
        Assert.Single(q.ReceivedObservationIds);
        Assert.Equal(["root-g-device"], q.SourceAncestryIds);
        Assert.Contains("tick(s) 4", q.Explanation, StringComparison.Ordinal);
        Assert.Contains("cutoff 10", q.Explanation, StringComparison.Ordinal);
        Assert.DoesNotContain("g-target", JsonDefaults.Serialize(q), StringComparison.Ordinal);
        Assert.DoesNotContain("root-g-target", JsonDefaults.Serialize(q), StringComparison.Ordinal);
        Assert.Empty(device.Modes.Single(m => m.Mode == "02").Questions);
        Assert.Equal(JsonDefaults.Serialize(device.Modes.Single(m => m.Mode == "22").Questions),
            JsonDefaults.Serialize(device.Modes.Single(m => m.Mode == "combined").Questions));
        Assert.All(device.Modes[0].MethodComparison.Methods, m =>
            Assert.Equal(["a", "b", "c"], m.OuterCandidateRegionIds));

        foreach (var id in new[] { "U-none", "U-future" })
        {
            var row = report.Cases.Single(c => c.Id == id);
            var old = Assert.Single(row.Modes.Single(m => m.Mode == "22").Questions);
            Assert.Equal("no-received-records-person-availability:5", old.QuestionId);
            Assert.Empty(old.ReceivedOriginalIds);
            Assert.Empty(old.ReceivedObservationIds);
            Assert.Empty(old.SourceAncestryIds);
        }
        foreach (var (id, mode) in new[]
        {
            ("U-device", "02"), ("A-none", "02"),
            ("G-offtick", "21"), ("U-followup", "04"),
            ("U-prior", "10"), ("U-eligible", "06")
        })
        {
            var row = report.Cases.Single(c => c.Id == id);
            Assert.Single(row.Modes.Single(m => m.Mode == mode).Questions);
            Assert.Empty(row.Modes.Single(m => m.Mode == "22").Questions);
        }
        Assert.Equal(JsonDefaults.Serialize(report),
            JsonDefaults.Serialize(ConditionLensAnalysis.Run(Fixture())));
    }

    [Fact]
    public async Task OffTickDeviceQuestionIsDirectlyCheckedInCliSavedJson()
    {
        var output = Path.Combine(Path.GetTempPath(),
            "footprint-rev13-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Equal(0, await CliApplication.RunAsync(
                ["evaluate-condition-lenses", "--fixture", FixturePath(), "--output", output]));
            var path = Path.Combine(output, "condition-lenses.json");
            var bytes = await File.ReadAllBytesAsync(path);
            using var saved = System.Text.Json.JsonDocument.Parse(bytes);
            var root = saved.RootElement;
            Assert.Equal(9, root.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(24, root.GetProperty("index").GetArrayLength());
            Assert.Equal(24, root.GetProperty("correspondenceAudits").GetArrayLength());
            var cases = root.GetProperty("cases").EnumerateArray().ToArray();
            Assert.Equal(56, cases.Length);
            System.Text.Json.JsonElement Case(string id) => cases.Single(c =>
                c.GetProperty("id").GetString() == id);
            System.Text.Json.JsonElement Mode(System.Text.Json.JsonElement c, string mode) =>
                c.GetProperty("modes").EnumerateArray().Single(m =>
                    m.GetProperty("mode").GetString() == mode);
            var g = Case("G-device");
            Assert.Equal("g-device", g.GetProperty("receivedOriginalIds")[0].GetString());
            var q = Mode(g, "22").GetProperty("questions").EnumerateArray().Single();
            Assert.Equal("off-tick-device-only-person-availability:5",
                q.GetProperty("questionId").GetString());
            Assert.Equal("received-offtick-device-without-target-person-evidence",
                q.GetProperty("reasonCode").GetString());
            Assert.Equal("legitimately-received-target-person-evidence-or-availability-check",
                q.GetProperty("requiredInformation").GetString());
            Assert.Equal("P", q.GetProperty("targetPersonId").GetString());
            Assert.Equal(5, q.GetProperty("targetTick").GetInt64());
            Assert.Equal(10, q.GetProperty("receiptCutoffTick").GetInt64());
            Assert.Equal(["g-device"], q.GetProperty("receivedOriginalIds")
                .EnumerateArray().Select(x => x.GetString()));
            Assert.Single(q.GetProperty("receivedObservationIds").EnumerateArray());
            Assert.Equal(["root-g-device"], q.GetProperty("sourceAncestryIds")
                .EnumerateArray().Select(x => x.GetString()));
            Assert.Contains("tick(s) 4", q.GetProperty("explanation").GetString());
            Assert.DoesNotContain("g-target", q.GetRawText());
            Assert.DoesNotContain("root-g-target", q.GetRawText());
            Assert.Equal(0, Mode(g, "02").GetProperty("questions").GetArrayLength());
            Assert.Equal(1, Mode(g, "combined").GetProperty("questions").GetArrayLength());

            // Full frozen-case scan verifies there is no unintended new 22 question.
            foreach (var row in cases)
            {
                var id = row.GetProperty("id").GetString();
                var modes = row.GetProperty("modes").EnumerateArray().ToArray();
                Assert.Equal(11, modes.Length);
                var extra = Mode(row, "22").GetProperty("questions").EnumerateArray()
                    .Count(x => x.GetProperty("questionId").GetString() ==
                        "off-tick-device-only-person-availability:5");
                Assert.Equal(id == "G-device" ? 1 : 0, extra);
                var baseline = Mode(row, "none").GetProperty("methodComparison")
                    .GetProperty("methods").EnumerateArray().ToArray();
                Assert.Equal(4, baseline.Length);
                foreach (var mode in modes)
                {
                    var current = mode.GetProperty("methodComparison")
                        .GetProperty("methods").EnumerateArray().ToArray();
                    Assert.Equal(4, current.Length);
                    for (var i = 0; i < 4; i++)
                    {
                        Assert.Equal(baseline[i].GetProperty("method").GetString(),
                            current[i].GetProperty("method").GetString());
                        foreach (var field in new[] { "outerCandidateRegionIds",
                            "preferredCandidateRegionIds", "uncertainCandidateRegionIds",
                            "conflictedCandidateRegionIds", "availableSourceGroupCount" })
                            Assert.Equal(baseline[i].GetProperty(field).GetRawText(),
                                current[i].GetProperty(field).GetRawText());
                    }
                }
            }
            foreach (var id in new[] { "U-none", "U-future" })
            {
                var original = Mode(Case(id), "22").GetProperty("questions")
                    .EnumerateArray().Single();
                Assert.Equal("no-received-records-person-availability:5",
                    original.GetProperty("questionId").GetString());
                Assert.Empty(original.GetProperty("receivedOriginalIds").EnumerateArray());
                Assert.Empty(original.GetProperty("receivedObservationIds").EnumerateArray());
                Assert.Empty(original.GetProperty("sourceAncestryIds").EnumerateArray());
            }
            foreach (var (id, expectedMode) in new[]
            {
                ("U-device", "02"), ("A-none", "02"),
                ("G-offtick", "21"), ("U-followup", "04"),
                ("U-prior", "10"), ("U-eligible", "06")
            })
            {
                Assert.Single(Mode(Case(id), expectedMode).GetProperty("questions")
                    .EnumerateArray());
                Assert.Empty(Mode(Case(id), "22").GetProperty("questions").EnumerateArray());
            }
            Assert.Equal(0, await CliApplication.RunAsync(
                ["evaluate-condition-lenses", "--fixture", FixturePath(), "--output", output]));
            Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
        }
        finally
        {
            if (Directory.Exists(output))
                Directory.Delete(output, recursive: true);
        }
    }
}
