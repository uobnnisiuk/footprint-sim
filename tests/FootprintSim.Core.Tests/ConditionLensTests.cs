using FootprintSim.Cli;
using FootprintSim.Evaluation;
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
        Assert.Equal(["02", "04", "06", "08", "09", "10", "12", "22"], report.Index.Where(x =>
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
            Assert.Equal(6, savedReport.RootElement.GetProperty("schemaVersion").GetInt32());
            var firstBytes = await File.ReadAllBytesAsync(Path.Combine(output, "condition-lenses.json"));
            Assert.Equal(0, await CliApplication.RunAsync(
                ["evaluate-condition-lenses", "--fixture", FixturePath(), "--output", output]));
            Assert.Equal(firstBytes,
                await File.ReadAllBytesAsync(Path.Combine(output, "condition-lenses.json")));
            Assert.Equal(24, expected.Index.Count);
            Assert.Equal(52, expected.Cases.Count);
            Assert.Equal(6, expected.SchemaVersion);
            Assert.All(expected.Cases, c => Assert.Equal(10, c.Modes.Count));
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
            Assert.Equal(["none", "02", "04", "06", "08", "09", "10", "12", "22", "combined"], row.Modes.Select(x => x.Mode));
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
            Assert.Equal(6, report.SchemaVersion);
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
            Assert.Equal(10, row.Modes.Count);
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
        Assert.Equal(52, report.Cases.Count);
        Assert.All(report.Cases, row => Assert.Equal(9, row.Modes.Count));
        Assert.Equal(JsonDefaults.Serialize(report), JsonDefaults.Serialize(
            ConditionLensAnalysis.Run(Fixture())));
    }


    [Fact]
    public void NoReceivedEvidenceDoesNotProvePersonAbsence()
    {
        var report = ConditionLensAnalysis.Run(Fixture());
        Assert.Equal(52, report.Cases.Count);
        Assert.Equal(45, report.Cases.Count(c =>
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
            Assert.Equal(6, root.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(52, root.GetProperty("cases").GetArrayLength());
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
