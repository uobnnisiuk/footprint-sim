using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FootprintSim.Evidence;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;
using static FootprintSim.Evaluation.CandidateIntervalWidthPublicEvaluator;

namespace FootprintSim.Evaluation;

/// <summary>Fill missing public model cells without accessing evidence, profiles, or truth.</summary>
public sealed class CandidateDensityTransform
{
    public const string SupportRuleId = "m1f19-seven-starts-times-registered-regions-v1";
    public const string OrderingRuleId = "m1f19-point-support-order-v1";
    public static IReadOnlyList<string> ArmIds { get; } = Array.AsReadOnly(new[] { "point-baseline", "half-missing", "full-support" });

    public CandidateDensitySupport Apply(CandidateDensityPublicSupportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Seed < 0 || request.BaseTick < 3 || request.RegisteredRegionIds is null ||
            request.RegisteredRegionIds.Count == 0 || request.RegisteredRegionIds.Any(string.IsNullOrWhiteSpace) ||
            request.RegisteredRegionIds.Distinct(StringComparer.Ordinal).Count() != request.RegisteredRegionIds.Count ||
            request.BaselinePointCandidates is null || request.BaselinePointCandidates.Count == 0)
        {
            throw new InvalidDataException("Density support requires nonnegative seed, base tick at least 3, unique Regions and baseline points.");
        }

        var regions = Set(request.RegisteredRegionIds);
        var baseline = new CandidateIntervalWidthTransform().Apply(request.BaselinePointCandidates, "original");
        if (baseline.Any(candidate => candidate.State.TemporalKind != CandidateWarpTemporalKind.EventPoint ||
            candidate.State.TimeWindow.StartTick < (long)request.BaseTick - 3 ||
            candidate.State.TimeWindow.StartTick > (long)request.BaseTick + 3 ||
            !regions.Contains(candidate.State.RegionIds.Single(), StringComparer.Ordinal) ||
            candidate.State.Provenance.Count != 1 || candidate.State.Provenance.Any(source =>
                source.SourceKind != CandidateWarpSourceKind.PublicCausalModel || source.AvailableAtTick != 0 ||
                source.IsPersonEvidence || source.IsDeviceOnlyEvidence || source.SourceObservationId is not null ||
                source.RelationKind is not null || source.SourceTimeWindow is not null || source.Participants.Count != 0 ||
                source.InteractionProvenance.Count != 0 ||
                !source.RegionIds.SequenceEqual(candidate.State.RegionIds))))
        {
            throw new InvalidDataException("Density baseline must contain only in-support F18 points with unchanged public model provenance.");
        }

        var occupied = baseline.Select(candidate => (candidate.State.TimeWindow.StartTick, candidate.State.RegionIds.Single())).ToHashSet();
        var cells = Enumerable.Range(-3, 7).SelectMany(offset => regions.Select((region, index) =>
        {
            var tick = (long)request.BaseTick + offset;
            var key = string.Create(CultureInfo.InvariantCulture, $"density-t{tick}-r{index:D2}");
            var source = string.Create(CultureInfo.InvariantCulture, $"seed-{request.Seed:D3}-{key}-public-model");
            var input = string.Create(CultureInfo.InvariantCulture, $"{OrderingRuleId}|{request.Seed}|{request.BaseTick}|{offset}|{region}");
            return new CandidateDensityCell(tick, offset, region, index,
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant(), key, source);
        })).ToArray();
        var missing = cells.Where(cell => !occupied.Contains((cell.StartTick, cell.RegionId)))
            .OrderBy(cell => cell.OrderHash, StringComparer.Ordinal).ThenBy(cell => cell.StartTick)
            .ThenBy(cell => cell.RegionId, StringComparer.Ordinal).ToArray();
        if (missing.Any(cell => baseline.Any(candidate => candidate.CandidateKey == cell.CandidateKey ||
            candidate.PublicSourceIdentifier == cell.PublicSourceIdentifier)))
        {
            throw new InvalidDataException("Baseline identity collides with an added support candidate.");
        }

        var ancestry = string.Create(CultureInfo.InvariantCulture, $"m1f19-seed-{request.Seed:D3}-point-support-model");
        var additions = missing.Select(cell => new CandidateIntervalWidthPublicCandidate
        {
            CandidateKey = cell.CandidateKey,
            PublicSourceIdentifier = cell.PublicSourceIdentifier,
            State = new PublicCandidateWarpStateDefinition
            {
                TimeWindow = new InteractionTimeWindow(cell.StartTick, cell.StartTick),
                TemporalKind = CandidateWarpTemporalKind.EventPoint,
                RegionIds = [cell.RegionId],
                EvidenceBoundaryState = EvidenceState.Confirmed,
                Provenance = [CandidateWarpProvenance.Create(CandidateWarpSourceKind.PublicCausalModel,
                    cell.PublicSourceIdentifier, 0, false, false, EvidenceState.Confirmed, [cell.RegionId],
                    interactionProvenance: [new InteractionProvenance(InteractionProvenanceKind.ExternalSource, ancestry)])]
            }
        }).ToArray();
        var arms = ArmIds.Select((armId, index) =>
        {
            var count = index * missing.Length / 2;
            return new CandidateDensityTransformArm(armId, count,
                baseline.Concat(additions.Take(count)).OrderBy(candidate => candidate.CandidateKey, StringComparer.Ordinal).ToArray());
        }).ToArray();
        return new CandidateDensitySupport(request.Seed, request.BaseTick, regions, cells.Length,
            Signature(new
            {
                RuleId = SupportRuleId,
                request.Seed,
                request.BaseTick,
                RegisteredRegionIds = regions,
                Cells = cells.Select(cell => new { cell.StartTick, cell.RegionId }).ToArray()
            }), missing, arms);
    }
}
