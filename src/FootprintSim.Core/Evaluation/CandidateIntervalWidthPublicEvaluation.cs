using FootprintSim.Evidence;
using FootprintSim.Inference.Derivation;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;

namespace FootprintSim.Evaluation;

public sealed class CandidateIntervalWidthTransform
{
    public const string RuleId = "candidate-point-at-existing-start-v1";

    public IReadOnlyList<CandidateIntervalWidthPublicCandidate> Apply(
        IReadOnlyList<CandidateIntervalWidthPublicCandidate> candidates, string armId)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (armId is not ("original" or "point-at-start") || candidates.Count == 0 ||
            candidates.Any(item => string.IsNullOrWhiteSpace(item.CandidateKey) ||
                string.IsNullOrWhiteSpace(item.PublicSourceIdentifier) || item.State.RegionIds.Count != 1 ||
                !Enum.IsDefined(item.State.TemporalKind) || item.State.TimeWindow.StartTick < 0 ||
                item.State.TimeWindow.EndTick < item.State.TimeWindow.StartTick ||
                item.State.TemporalKind == CandidateWarpTemporalKind.EventPoint &&
                item.State.TimeWindow.StartTick != item.State.TimeWindow.EndTick ||
                item.State.TemporalKind == CandidateWarpTemporalKind.StateInterval &&
                item.State.TimeWindow.StartTick == item.State.TimeWindow.EndTick ||
                item.State.EvidenceBoundaryState != EvidenceState.Confirmed || item.State.Provenance.Count == 0 ||
                !item.State.Provenance.Any(source => source.SourceIdentifier == item.PublicSourceIdentifier) ||
                item.State.Provenance.Any(source => source.Visibility != InteractionVisibility.PublicEvidence ||
                    source.EvidenceBoundaryState != EvidenceState.Confirmed)) ||
            candidates.Select(item => item.CandidateKey).Distinct(StringComparer.Ordinal).Count() != candidates.Count ||
            candidates.Select(item => item.PublicSourceIdentifier).Distinct(StringComparer.Ordinal).Count() != candidates.Count ||
            candidates.Select(item => (item.State.TimeWindow.StartTick, item.State.RegionIds.Single())).Distinct().Count() != candidates.Count)
        {
            throw new InvalidDataException("Candidate transform requires unique, confirmed public candidate keys, sources, and start/Region pairs.");
        }

        return candidates.OrderBy(item => item.CandidateKey, StringComparer.Ordinal).Select(item =>
            armId == "original" ? item : item with
            {
                State = item.State with
                {
                    TimeWindow = new InteractionTimeWindow(item.State.TimeWindow.StartTick, item.State.TimeWindow.StartTick),
                    TemporalKind = CandidateWarpTemporalKind.EventPoint
                }
            }).ToArray();
    }
}

public sealed class CandidateIntervalWidthPublicEvaluator
{
    public CandidateIntervalWidthArm Evaluate(CandidateIntervalWidthPublicRequest request, string armId)
    {
        ArgumentNullException.ThrowIfNull(request);
        var candidates = new CandidateIntervalWidthTransform().Apply(request.Candidates, armId);
        var regions = CommonWorldPlateauDomainClassifier.Set(request.RegisteredRegionIds);
        var axes = request.AxisComponents.OrderBy(item => item.Axis).ToArray();
        var profiles = request.Profiles.OrderBy(item => item.Order).ToArray();
        var expected = new[]
        {
            ("none", false, false, false), ("location", true, false, false),
            ("time", false, true, false), ("participant", false, false, true),
            ("location-time", true, true, false), ("location-participant", true, false, true),
            ("time-participant", false, true, true), ("location-time-participant", true, true, true)
        };
        if (axes.Length != 3 || !axes.Select(item => item.Axis).SequenceEqual(Enum.GetValues<CommonWorldObservationAxis>()) ||
            profiles.Length != 8 || profiles.Where((profile, index) => profile.Id != expected[index].Item1 ||
                profile.Order != index || profile.SelectLocation != expected[index].Item2 ||
                profile.SelectTime != expected[index].Item3 || profile.SelectParticipant != expected[index].Item4).Any() ||
            request.EvidenceAvailabilityCutoff < 0 || candidates.Any(item =>
                !regions.Contains(item.State.RegionIds.Single(), StringComparer.Ordinal) ||
                item.State.Provenance.Any(source => source.AvailableAtTick > request.EvidenceAvailabilityCutoff)) ||
            Signature(request.PrimarySetInclusions) != Signature(CommonWorldFieldCombinationExperimentValidator.RequiredSetInclusions) ||
            Signature(request.ConditionalComparisons) != Signature(CommonWorldFieldCombinationExperimentValidator.RequiredComparisons))
        {
            throw new InvalidDataException("Public F18 inference requires the registered profiles, comparisons, domain, and cutoff.");
        }

        var location = axes.Single(item => item.Axis == CommonWorldObservationAxis.Location).Component.Predicate;
        var time = axes.Single(item => item.Axis == CommonWorldObservationAxis.Time).Component.Predicate;
        var participant = axes.Single(item => item.Axis == CommonWorldObservationAxis.Participant).Component.Predicate;
        if (location.RegionRestriction != CandidateConstraintDimensionRestriction.Restricted ||
            location.TimeRestriction != CandidateConstraintDimensionRestriction.Unrestricted ||
            participant.RegionRestriction != CandidateConstraintDimensionRestriction.Restricted ||
            participant.TimeRestriction != CandidateConstraintDimensionRestriction.Unrestricted ||
            time.RegionRestriction != CandidateConstraintDimensionRestriction.Unrestricted ||
            time.TimeRestriction != CandidateConstraintDimensionRestriction.Restricted ||
            location.AllowedRegionIds.Concat(participant.AllowedRegionIds).Any(region => !regions.Contains(region, StringComparer.Ordinal)))
        {
            throw new InvalidDataException("F18 requires unchanged Region-only L/P and Time-only T components.");
        }

        var native = candidates.Select(candidate =>
        {
            var item = new ObservationDerivedHistoricalCandidateReconstructor().Reconstruct(request.ScenarioId,
                request.Seed, request.TargetEntityId, [candidate.State], constraint: null).Candidates.Single();
            return new CandidateIntervalWidthNativeCandidate(candidate.CandidateKey, candidate.PublicSourceIdentifier,
                item.RegionId, item.TimeWindow, candidate.State.TemporalKind, item.StateId, item.CandidateId,
                Signature(candidate.State.Provenance));
        }).ToArray();
        if (native.Select(item => item.CandidateId).Distinct(StringComparer.Ordinal).Count() != candidates.Count)
        {
            throw new InvalidDataException("Native candidate identity collision lost multiplicity.");
        }

        var keyById = native.ToDictionary(item => item.CandidateId, item => item.CandidateKey, StringComparer.Ordinal);
        var ids = Set(native.Select(item => item.CandidateId));
        var universeId = InteractionStableIdentifier.Create("cdu", request.ScenarioId,
            InteractionStableIdentifier.Seed(request.Seed), request.TargetEntityId, request.TargetStateContextId,
            request.CandidateDomainId, string.Join("|", ids));
        var runs = profiles.Select(profile =>
        {
            var active = ActiveAxes(profile);
            var groups = active.Select(axis => CandidateGenerationConstraintGroup.Create(
                CandidateConstraintLogicalRole.JointRequirement,
                [axes.Single(item => item.Axis == axis).Component])).ToArray();
            var result = new CandidateGenerationConstraintComposer().Compose(new CandidateGenerationConstraintCompositionRequest
            {
                ScenarioId = request.ScenarioId,
                Seed = request.Seed,
                TargetEntityId = request.TargetEntityId,
                TargetStateContextId = request.TargetStateContextId,
                CandidateDomainId = request.CandidateDomainId,
                EvidenceAvailabilityCutoff = request.EvidenceAvailabilityCutoff,
                UsageRole = PastStateEvidenceUsageRole.CandidateGeneration,
                PublicCandidateStates = candidates.Select(item => item.State).ToArray(),
                ConstraintGroups = groups
            });
            if (result.Status is not (CandidateConstraintCompositionStatus.Applied or CandidateConstraintCompositionStatus.NonRestricting) ||
                result.Candidates.Count == 0 || result.InitialCandidateCount != native.Length ||
                result.Candidates.Any(item => !keyById.ContainsKey(item.CandidateId)))
            {
                throw new InvalidDataException("A public F18 profile failed or returned a foreign/empty candidate set.");
            }

            return new CandidateIntervalWidthProfile(profile.Id, profile.Order, active,
                Set(result.Candidates.Select(item => item.CandidateId)),
                Set(result.Candidates.Select(item => keyById[item.CandidateId])), result.Candidates.Count,
                result.CandidateGenerationWork, result.CompositionId, result.Audit);
        }).ToArray();
        if (!runs[0].CandidateIds.SequenceEqual(ids))
        {
            throw new InvalidDataException("None changed the arm's candidate universe.");
        }

        var inclusions = request.PrimarySetInclusions.Select(item => new CandidateIntervalWidthSetInclusion(
            item.SubsetProfileId, item.SupersetProfileId,
            Subset(Profile(item.SubsetProfileId).CandidateKeys, Profile(item.SupersetProfileId).CandidateKeys))).ToArray();
        if (inclusions.Any(item => !item.CandidateKeysAreSubset))
        {
            throw new InvalidDataException("Joint inclusion failed within an F18 arm.");
        }

        var timeTop = native.All(candidate => Overlaps(candidate.TimeWindow, time.ConstrainedTimeWindow!));
        var conditionals = request.ConditionalComparisons.Select(item =>
        {
            var baseline = Profile(item.BaselineProfileId);
            var combined = Profile(item.CombinedProfileId);
            var diagnostic = CommonWorldPlateauDomainClassifier.Diagnose(item.AddedAxis,
                location.AllowedRegionIds, participant.AllowedRegionIds, timeTop, baseline.ActiveAxes,
                baseline.CandidateIds, combined.CandidateIds, "registered", regions, regions);
            return new CandidateIntervalWidthConditional(item.Id, item.BaselineProfileId, item.CombinedProfileId,
                item.AddedAxis, baseline.CandidateCount, combined.CandidateCount,
                baseline.CandidateCount - combined.CandidateCount, diagnostic);
        }).ToArray();
        var equivalences = runs.SelectMany((left, index) => runs.Skip(index + 1).Select(right =>
            new CandidateIntervalWidthEquivalence(left.ProfileId, right.ProfileId,
                left.CandidateKeys.SequenceEqual(right.CandidateKeys)))).ToArray();
        var minimumCount = runs.Min(item => item.CandidateCount);
        var atMinimum = runs.Where(item => item.CandidateCount == minimumCount).ToArray();
        var minimal = atMinimum.Where(candidate => !atMinimum.Any(other =>
            other.ActiveAxes.Count < candidate.ActiveAxes.Count && other.ActiveAxes.All(candidate.ActiveAxes.Contains))).ToArray();
        var minimum = new CandidateIntervalWidthMinimum(minimumCount, atMinimum.Select(item => item.ProfileId).ToArray(),
            minimal.Select(item => item.ProfileId).ToArray(), atMinimum.All(item => item.CandidateKeys.SequenceEqual(atMinimum[0].CandidateKeys)));
        return new CandidateIntervalWidthArm(armId,
            Signature(new { request.Seed, ArmId = armId, Candidates = candidates }), universeId,
            Signature(axes), Signature(candidates), native, runs, conditionals, inclusions, equivalences, minimum);

        CandidateIntervalWidthProfile Profile(string id) => runs.Single(item => item.ProfileId == id);
    }

    internal static CommonWorldObservationAxis[] ActiveAxes(CommonWorldProfileDefinition profile) => new[]
        {
            (profile.SelectLocation, CommonWorldObservationAxis.Location),
            (profile.SelectTime, CommonWorldObservationAxis.Time),
            (profile.SelectParticipant, CommonWorldObservationAxis.Participant)
        }.Where(item => item.Item1).Select(item => item.Item2).ToArray();

    internal static bool Overlaps(InteractionTimeWindow left, InteractionTimeWindow right) =>
        left.StartTick <= right.EndTick && right.StartTick <= left.EndTick;
    internal static string[] Set(IEnumerable<string> values) => CommonWorldPlateauDomainClassifier.Set(values);
    internal static bool Subset(IEnumerable<string> subset, IEnumerable<string> superset) =>
        !subset.Except(superset, StringComparer.Ordinal).Any();
    internal static string Signature<T>(T value) => CommonWorldPlateauDomainClassifier.Signature(value);
}
