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
        Assert.Equal(["02", "04", "06", "09"], report.Index.Where(x =>
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
            Assert.Equal(24, expected.Index.Count);
            Assert.Equal(17, expected.Cases.Count);
            Assert.Equal(2, expected.SchemaVersion);
            Assert.All(expected.Cases, c => Assert.Equal(6, c.Modes.Count));
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
            Assert.Equal(["none", "02", "04", "06", "09", "combined"], row.Modes.Select(x => x.Mode));
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
            Assert.Equal(2, report.SchemaVersion);
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

}
