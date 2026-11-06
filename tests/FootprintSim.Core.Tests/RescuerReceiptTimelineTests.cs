using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FootprintSim.Cli;
using FootprintSim.Evaluation;
using FootprintSim.Serialization;
using Xunit;

namespace FootprintSim.Core.Tests;

public sealed class RescuerReceiptTimelineTests
{
    [Fact]
    public async Task CliRunsRealReceiptBridgeAndWritesHandCheckableReport()
    {
        var directory = NewDirectory();
        try
        {
            var path = Path.Combine(directory, "report.json");
            Assert.Equal(0, await CliApplication.RunAsync(
                ["evaluate-rescuer-receipt-timeline", "--fixture", FixturePath(), "--output", path]));
            using var parsed = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            var rows = parsed.RootElement.GetProperty("queries").EnumerateArray().ToArray();
            Assert.Equal([4L, 5L, 7L],
                rows.Select(row => row.GetProperty("queryTick").GetInt64()).ToArray());
            Assert.Equal([0, 1, 1],
                rows.Select(row => row.GetProperty("receivedOriginalCount").GetInt32()).ToArray());
            Assert.Equal([0, 1, 1],
                rows.Select(row => row.GetProperty("crossThreadConstraintCount").GetInt32()).ToArray());
            Assert.Equal([0, 1, 1],
                rows.Select(row => row.GetProperty("causalTracingConstraintCount").GetInt32()).ToArray());
            Assert.Equal([0, 1, 1],
                rows.Select(row => row.GetProperty("sourceGroupCount").GetInt32()).ToArray());
            Assert.Equal(["A", "B"], Candidates(rows[0]));
            Assert.Equal(["C"], Candidates(rows[1]));
            Assert.Equal(["C"], Candidates(rows[2]));
            Assert.Empty(rows[0].GetProperty("receivedOriginals").EnumerateArray());
            Assert.DoesNotContain("original-encounter-record", rows[0].GetRawText());
            Assert.DoesNotContain("upstream-evidence", rows[0].GetRawText());
            Assert.DoesNotContain("original-encounter-record", rows[0].GetRawText());
            Assert.Equal(5, rows[1].GetProperty("receivedOriginals")[0]
                .GetProperty("firstReceivedAtTick").GetInt64());
            Assert.Equal(1, rows[1].GetProperty("sourceGroups").GetArrayLength());
            Assert.Equal(0, await CliApplication.RunAsync(
                ["evaluate-rescuer-receipt-timeline", "--help"]));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ReportsAreByteStableForSameFixtureAndDifferentOutputPaths()
    {
        var directory = NewDirectory();
        try
        {
            var first = Path.Combine(directory, "first.json");
            var second = Path.Combine(directory, "second.json");
            Assert.Equal(0, await CliApplication.RunAsync(
                ["evaluate-rescuer-receipt-timeline", "--fixture", FixturePath(), "--output", first]));
            Assert.Equal(0, await CliApplication.RunAsync(
                ["evaluate-rescuer-receipt-timeline", "--fixture", FixturePath(), "--output", second]));
            Assert.Equal(await File.ReadAllBytesAsync(first), await File.ReadAllBytesAsync(second));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RedeliveryAndOrderCannotCreateNewIndependentEvidence()
    {
        // The fixed fixture already includes tick-5 and tick-7 receipts.
        // Compare first delivery alone with first delivery plus redelivery at the same query tick.
        var firstOnlyFixture = JsonNode.Parse(File.ReadAllText(FixturePath()))!.AsObject();
        firstOnlyFixture["receipts"]!.AsArray().RemoveAt(1);
        firstOnlyFixture["queryTicks"] = new JsonArray(7);
        var firstOnly = RescuerReceiptTimeline.Run(
            Encoding.UTF8.GetBytes(firstOnlyFixture.ToJsonString()));
        var expected = Assert.Single(firstOnly.Queries);

        var redeliveredFixture = JsonNode.Parse(File.ReadAllText(FixturePath()))!.AsObject();
        redeliveredFixture["queryTicks"] = new JsonArray(7);
        var redelivered = RescuerReceiptTimeline.Run(
            Encoding.UTF8.GetBytes(redeliveredFixture.ToJsonString()));
        var actual = Assert.Single(redelivered.Queries);

        Assert.Equal(7L, expected.QueryTick);
        Assert.Equal(7L, actual.QueryTick);
        Assert.NotEqual(firstOnly.FixtureSha256, redelivered.FixtureSha256);
        Assert.Equal(1, expected.ReceivedOriginalCount);
        Assert.Equal(1, expected.CrossThreadConstraintCount);
        Assert.Equal(1, expected.CausalTracingConstraintCount);
        Assert.Equal(1, expected.SourceGroupCount);
        Assert.Equal(5L, Assert.Single(expected.ReceivedOriginals).FirstReceivedAtTick);
        Assert.Equal(["C"], expected.SearchResult.CandidateRegionIds);
        // Full serialized rows cover original observation IDs, first receipt, real
        // source-group identities/ancestry, and the complete strategy/tracing result.
        Assert.Equal(JsonDefaults.Serialize(expected.ReceivedOriginals),
            JsonDefaults.Serialize(actual.ReceivedOriginals));
        Assert.Equal(JsonDefaults.Serialize(expected.SourceGroups),
            JsonDefaults.Serialize(actual.SourceGroups));
        Assert.Equal(JsonDefaults.Serialize(expected.SearchResult),
            JsonDefaults.Serialize(actual.SearchResult));
        Assert.Equal(JsonDefaults.Serialize(expected), JsonDefaults.Serialize(actual));

        // Keep the separate receipt-duplication and ledger/query-order regression.
        var fixture = JsonNode.Parse(File.ReadAllText(FixturePath()))!.AsObject();
        var before = RescuerReceiptTimeline.Run(Encoding.UTF8.GetBytes(fixture.ToJsonString()));
        var receipts = fixture["receipts"]!.AsArray();
        receipts.Add(receipts[0]!.DeepClone());
        fixture["receipts"] = new JsonArray(
            receipts.Select(item => item!.DeepClone()).Reverse().ToArray());
        fixture["queryTicks"] = new JsonArray(7, 4, 5);
        var after = RescuerReceiptTimeline.Run(Encoding.UTF8.GetBytes(fixture.ToJsonString()));

        Assert.NotEqual(before.FixtureSha256, after.FixtureSha256);
        Assert.Equal(JsonDefaults.Serialize(before.Queries),
            JsonDefaults.Serialize(after.Queries));
    }

    [Fact]
    public void OtherRecipientAndFutureReceiptCannotExposeTheOriginal()
    {
        var fixture = JsonNode.Parse(File.ReadAllText(FixturePath()))!.AsObject();
        fixture["receipts"] = new JsonArray
        {
            new JsonObject
            {
                ["scenarioId"] = "rescuer-receipt-fixture",
                ["seed"] = 7,
                ["originalRecordId"] = "original-encounter-record",
                ["holderDeviceId"] = "held-device",
                ["rescuerId"] = "rescuer-b",
                ["receivedAtTick"] = 5
            },
            new JsonObject
            {
                ["scenarioId"] = "rescuer-receipt-fixture",
                ["seed"] = 7,
                ["originalRecordId"] = "original-encounter-record",
                ["holderDeviceId"] = "held-device",
                ["rescuerId"] = "rescuer-a",
                ["receivedAtTick"] = 10
            }
        };
        var rows = RescuerReceiptTimeline.Run(
            Encoding.UTF8.GetBytes(fixture.ToJsonString())).Queries;
        Assert.All(rows, row =>
        {
            Assert.Equal(0, row.ReceivedOriginalCount);
            Assert.Equal(0, row.SourceGroupCount);
            Assert.Empty(row.ReceivedOriginals);
        });
        Assert.Equal(["C", "D"], rows.Single(row => row.QueryTick == 5)
            .SearchResult.CandidateRegionIds);
    }

    [Fact]
    public async Task MalformedOptionsAndFixtureFailWithoutOverwritingReport()
    {
        var directory = NewDirectory();
        try
        {
            var report = Path.Combine(directory, "keep.json");
            await File.WriteAllTextAsync(report, "keep-existing");
            Assert.Equal(2, await CliApplication.RunAsync(
                ["evaluate-rescuer-receipt-timeline", "--fixture", FixturePath()]));
            Assert.Equal(2, await CliApplication.RunAsync(
                ["evaluate-rescuer-receipt-timeline", "--fixture", FixturePath(),
                    "--fixture", FixturePath(), "--output", report]));
            Assert.Equal(2, await CliApplication.RunAsync(
                ["evaluate-rescuer-receipt-timeline", "--fixture", FixturePath(),
                    "--output", FixturePath()]));
            var invalid = Path.Combine(directory, "invalid.json");
            var fixture = JsonNode.Parse(File.ReadAllText(FixturePath()))!.AsObject();
            fixture["privateTruth"] = new JsonObject();
            await File.WriteAllTextAsync(invalid, fixture.ToJsonString());
            Assert.Equal(2, await CliApplication.RunAsync(
                ["evaluate-rescuer-receipt-timeline", "--fixture", invalid, "--output", report]));
            Assert.Equal("keep-existing", await File.ReadAllTextAsync(report));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void InconsistentDeliveryIsNotCheckedBeforeItsQueryCutoff()
    {
        var fixture = JsonNode.Parse(File.ReadAllText(FixturePath()))!.AsObject();
        fixture["observation"]!["availableAtTick"] = 20;
        fixture["storedAtTick"] = 20;
        var receipts = fixture["receipts"]!.AsArray();
        receipts.Clear();
        receipts.Add(new JsonObject
        {
            ["scenarioId"] = "rescuer-receipt-fixture",
            ["seed"] = 7,
            ["originalRecordId"] = "original-encounter-record",
            ["holderDeviceId"] = "held-device",
            ["rescuerId"] = "rescuer-a",
            ["receivedAtTick"] = 10
        });
        Assert.All(RescuerReceiptTimeline.Run(
            Encoding.UTF8.GetBytes(fixture.ToJsonString())).Queries,
            row => Assert.Equal(0, row.ReceivedOriginalCount));
        fixture["queryTicks"] = new JsonArray(10);
        Assert.Throws<ArgumentException>(() => RescuerReceiptTimeline.Run(
            Encoding.UTF8.GetBytes(fixture.ToJsonString())));
    }

    [Theory]
    [InlineData("Confirmed", 2, 2)]
    [InlineData("SupportedButUncertain", 1, 2)]
    [InlineData("Conflicting", 2, 2)]
    public void JsonEvidencePreservesExactIntervalAndUncertainOrConflictAlternatives(
        string state, long startTick, long endTick)
    {
        var fixture = JsonNode.Parse(File.ReadAllText(FixturePath()))!.AsObject();
        fixture["observation"]!["evidenceState"] = state;
        fixture["observation"]!["timeWindow"]!["startTick"] = startTick;
        fixture["observation"]!["timeWindow"]!["endTick"] = endTick;
        var row = Assert.Single(RescuerReceiptTimeline.Run(
            Encoding.UTF8.GetBytes(fixture.ToJsonString())).Queries,
            item => item.QueryTick == 5);
        Assert.Equal(1, row.ReceivedOriginalCount);
        Assert.Equal(1, row.CrossThreadConstraintCount);
        Assert.Equal(1, row.CausalTracingConstraintCount);
        Assert.Single(row.ReceivedOriginals);
        Assert.Equal(5L, row.ReceivedOriginals[0].FirstReceivedAtTick);
        if (state != "Confirmed")
        {
            Assert.Equal(["C", "D"], row.SearchResult.OuterPlausibleCandidateRegionIds);
        }
    }

    [Fact]
    public void JsonDeviceOnlyEvidenceCannotCreatePersonConstraintOrSourceGroup()
    {
        var fixture = JsonNode.Parse(File.ReadAllText(FixturePath()))!.AsObject();
        var observation = fixture["observation"]!;
        observation["isPersonEvidence"] = false;
        observation["isDeviceOnlyEvidence"] = true;
        observation["participants"]![0]!["entityId"] = "held-device";
        observation["participants"]![0]!["entityKind"] = "Device";
        var row = Assert.Single(RescuerReceiptTimeline.Run(
            Encoding.UTF8.GetBytes(fixture.ToJsonString())).Queries,
            item => item.QueryTick == 5);
        Assert.Equal(1, row.ReceivedOriginalCount);
        Assert.Equal(0, row.CrossThreadConstraintCount);
        Assert.Equal(0, row.CausalTracingConstraintCount);
        Assert.Equal(0, row.SourceGroupCount);
        Assert.Equal(["C", "D"], row.SearchResult.CandidateRegionIds);
    }

    [Theory]
    [InlineData("scenarioId", "other-run")]
    [InlineData("seed", "8")]
    [InlineData("originalRecordId", "other-record")]
    [InlineData("holderDeviceId", "other-holder")]
    public void UnrelatedJsonDeliveryCannotExposeRecordAtQueryTickFive(
        string field, string replacement)
    {
        var fixture = JsonNode.Parse(File.ReadAllText(FixturePath()))!.AsObject();
        var receipt = fixture["receipts"]![0]!;
        if (field == "seed")
        {
            receipt[field] = 8;
        }
        else
        {
            receipt[field] = replacement;
        }

        var row = Assert.Single(RescuerReceiptTimeline.Run(
            Encoding.UTF8.GetBytes(fixture.ToJsonString())).Queries,
            item => item.QueryTick == 5);
        Assert.Equal(0, row.ReceivedOriginalCount);
        Assert.Equal(0, row.SourceGroupCount);
        Assert.Equal(["C", "D"], row.SearchResult.CandidateRegionIds);
    }

    [Fact]
    public void JsonStrictParserRejectsNumericEnumsUnknownNestedKeysAndDuplicateKeys()
    {
        var fixture = JsonNode.Parse(File.ReadAllText(FixturePath()))!.AsObject();
        fixture["observation"]!["evidenceState"] = 0;
        Assert.Throws<JsonException>(() => RescuerReceiptTimeline.Run(
            Encoding.UTF8.GetBytes(fixture.ToJsonString())));
        fixture = JsonNode.Parse(File.ReadAllText(FixturePath()))!.AsObject();
        fixture["observation"]!["untrustedValue"] = 4;
        Assert.Throws<JsonException>(() => RescuerReceiptTimeline.Run(
            Encoding.UTF8.GetBytes(fixture.ToJsonString())));

        var original = File.ReadAllText(FixturePath());
        var duplicated = original.Replace(
            "\"schemaVersion\": 1,",
            "\"schemaVersion\": 1, \"schemaVersion\": 1,",
            StringComparison.Ordinal);
        Assert.NotEqual(original, duplicated);
        Assert.Throws<JsonException>(() => RescuerReceiptTimeline.Run(
            Encoding.UTF8.GetBytes(duplicated)));
    }

    private static string[] Candidates(JsonElement row) =>
        row.GetProperty("searchResult").GetProperty("candidateRegionIds")
            .EnumerateArray().Select(item => item.GetString()!).ToArray();

    private static string NewDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "receipt-timeline-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string FixturePath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(
                current.FullName, "experiments", "rescuer_receipt_timeline", "fixture.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException("Repository receipt fixture could not be located.");
    }
}
