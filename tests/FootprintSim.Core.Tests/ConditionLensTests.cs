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
        Assert.Equal(["02", "06"], report.Index.Where(x =>
            x.Status == "limited-analogy-implemented").Select(x => x.Id));
        var a = report.Cases.Single(x => x.Id == "A-none");
        var b = report.Cases.Single(x => x.Id == "B-none");
        var c = report.Cases.Single(x => x.Id == "C-none");
        Assert.Equal(["02"], a.Modes[3].Questions.SelectMany(q => q.LensIds));
        Assert.Equal(["06"], b.Modes[3].Questions.SelectMany(q => q.LensIds));
        Assert.Equal(["02", "06"], c.Modes[3].Questions.SelectMany(q => q.LensIds)
            .OrderBy(x => x, StringComparer.Ordinal));
        Assert.Empty(a.ExistingDeclaredPathDiagnostic.Candidates
            .SelectMany(candidate => candidate.Questions));
        Assert.Empty(b.ExistingDeclaredPathDiagnostic.Candidates
            .SelectMany(candidate => candidate.Questions));
        Assert.NotEqual(a.Modes[3].Questions[0].ReasonCode, b.Modes[3].Questions[0].ReasonCode);
        Assert.Equal("P", c.Modes[3].Questions[0].TargetPersonId);
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
        Assert.DoesNotContain(missing.QuestionId, after.Modes[3].Questions.Select(q => q.QuestionId));
        var difference = report.AnswerDifferences.Single(d =>
            d.FromCase == "A-none" && d.ToCase == "A-eligible");
        Assert.Contains(missing.QuestionId, difference.ResolvedQuestionIds);
        Assert.Contains(link.QuestionId, difference.NewQuestionIds);

        var cLink = Assert.Single(c.Modes[1].Questions);
        Assert.Equal(link.QuestionId, cLink.QuestionId);
        Assert.Equal(link.RequiredInformation, cLink.RequiredInformation);
        var cCorroboration = Assert.Single(c.Modes[2].Questions);
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
            Assert.Equal(9, expected.Cases.Count);
            Assert.All(expected.Cases, c => Assert.Equal(4, c.Modes.Count));
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
            Assert.Equal(["none", "02", "06", "combined"], row.Modes.Select(x => x.Mode));
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
        Assert.Equal(JsonDefaults.Serialize(aNone.Modes[3].Questions),
            JsonDefaults.Serialize(aFuture.Modes[3].Questions));
        Assert.Equal(JsonDefaults.Serialize(bNone.Modes[3].Questions),
            JsonDefaults.Serialize(bFuture.Modes[3].Questions));
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
}
