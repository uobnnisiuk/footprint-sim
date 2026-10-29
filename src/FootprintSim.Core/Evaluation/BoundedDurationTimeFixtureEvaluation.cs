using FootprintSim.Evidence;
using FootprintSim.Inference.Derivation;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;

namespace FootprintSim.Evaluation;

public sealed class BoundedDurationTimeFixtureEvaluationRunner
{
    public BoundedDurationTimeFixtureEvaluationResult Run(
        BoundedDurationTimeFixtureEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Fixture);
        Validate(request.Fixture);

        var fixture = request.Fixture;
        var dependency = PublicBoundedDurationDependency.Create(
            fixture.ScenarioId,
            fixture.PublicDependency.Id,
            fixture.PublicDependency.PredecessorStateKind,
            fixture.PublicDependency.LaterEventKind,
            fixture.PublicDependency.TargetEntityId,
            new PublicDurationBound(
                fixture.PublicDependency.MinimumDurationTicks,
                fixture.PublicDependency.MaximumDurationTicks),
            fixture.PublicDependency.DurationSourceId,
            fixture.PublicDependency.AllowedPredecessorRegionIds);
        var publicCandidates = fixture.CandidateStates.Select(item =>
            new PublicCandidateWarpStateDefinition
            {
                TimeWindow = new InteractionTimeWindow(item.StartTick, item.EndTick),
                TemporalKind = item.TemporalKind,
                RegionIds = item.RegionIds,
                EvidenceBoundaryState = EvidenceState.Confirmed,
                Provenance =
                [
                    CandidateWarpProvenance.Create(
                        CandidateWarpSourceKind.PublicCausalModel,
                        dependency.PublicInputId,
                        availableAtTick: 0,
                        isPersonEvidence: false,
                        isDeviceOnlyEvidence: false,
                        EvidenceState.Confirmed,
                        item.RegionIds)
                ]
            }).ToArray();
        var deriver = new BoundedDurationObservationConstraintDeriver();
        var reconstructor = new ObservationDerivedHistoricalCandidateReconstructor();
        var profiles = fixture.Profiles
            .OrderBy(item => item.InformationOrder)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        var runs = profiles.Select(profile =>
        {
            var occurrenceTime = profile.ObservedStartTick.HasValue
                ? ObservedOccurrenceTime.Known(new InteractionTimeWindow(
                    profile.ObservedStartTick.Value,
                    profile.ObservedEndTick!.Value))
                : ObservedOccurrenceTime.Unknown();
            var observation = PublicEventObservation.Create(
                fixture.ScenarioId,
                fixture.Seed,
                fixture.SourceEvidenceIdentifier,
                fixture.LaterEventKind,
                fixture.TargetEntityId,
                occurrenceTime,
                fixture.AvailableAtTick,
                fixture.EvidenceState,
                InteractionVisibility.PublicEvidence,
                fixture.SourceAncestryIds);
            var derivation = deriver.Derive(
                observation,
                dependency,
                fixture.EvidenceAvailabilityCutoff,
                PastStateEvidenceUsageRole.CandidateGeneration);
            var reconstruction = reconstructor.Reconstruct(
                fixture.ScenarioId,
                fixture.Seed,
                fixture.TargetEntityId,
                publicCandidates,
                derivation.Constraint);
            var containsTruth = reconstruction.Candidates.Any(item =>
                string.Equals(item.RegionId, fixture.TruthRegionId, StringComparison.Ordinal));

            return new BoundedDurationTimeFixtureRun
            {
                ProfileId = profile.Id,
                InformationOrder = profile.InformationOrder,
                SourceObservationPresent = true,
                SourceObservationId = observation.ObservationId,
                OccurrenceTimeKnowledge = occurrenceTime.Knowledge,
                TimeWindowKind = occurrenceTime.WindowKind,
                ObservedOccurrenceWindow = occurrenceTime.Window,
                AvailableAtTick = observation.AvailableAtTick,
                ConstraintGenerated = derivation.Constraint is not null,
                ConstraintId = derivation.Constraint?.ConstraintId,
                DerivedPastWindow = derivation.Constraint?.ConstrainedTimeWindow,
                DerivationAudit = derivation.Audit,
                ObservationSourceGroupCount = 1,
                GeneratedConstraintSourceGroupCount = derivation.Constraint is null ? 0 : 1,
                InitialCandidateCount = reconstruction.InitialCandidateCount,
                Candidates = reconstruction.Candidates,
                CandidateCount = reconstruction.Candidates.Count,
                ContainsTruth = containsTruth,
                FalseExclusion = !containsTruth,
                Reduction = reconstruction.InitialCandidateCount == 0
                    ? 0m
                    : (reconstruction.InitialCandidateCount - reconstruction.Candidates.Count) /
                        (decimal)reconstruction.InitialCandidateCount,
                CandidateGenerationWork = reconstruction.CandidateGenerationWork,
                EvidenceRecordsInspected = reconstruction.EvidenceRecordsInspected,
                SourceGroupsInspected = reconstruction.SourceGroupsInspected
            };
        }).ToArray();
        var monotonicity = runs.Zip(runs.Skip(1), (morePrecise, coarser) =>
        {
            var preciseIds = morePrecise.Candidates.Select(item => item.CandidateId)
                .OrderBy(item => item, StringComparer.Ordinal).ToArray();
            var coarserIds = coarser.Candidates.Select(item => item.CandidateId)
                .OrderBy(item => item, StringComparer.Ordinal).ToArray();
            return new BoundedDurationTimeMonotonicityAudit
            {
                MorePreciseProfileId = morePrecise.ProfileId,
                CoarserProfileId = coarser.ProfileId,
                MorePreciseCandidateIds = preciseIds,
                CoarserCandidateIds = coarserIds,
                DerivedWindowIsSubset = IsWindowSubset(
                    morePrecise.DerivedPastWindow,
                    coarser.DerivedPastWindow),
                CandidateSetIsSubset = preciseIds.All(item =>
                    coarserIds.Contains(item, StringComparer.Ordinal))
            };
        }).ToArray();

        return new BoundedDurationTimeFixtureEvaluationResult
        {
            Manifest = new BoundedDurationTimeFixtureManifest
            {
                FixtureId = fixture.Id,
                ScenarioId = fixture.ScenarioId,
                FixtureContentSha256 = request.FixtureContentSha256,
                SourceCommitSha = request.SourceCommitSha,
                DirtyWorkingTree = request.DirtyWorkingTree,
                ProfileIds = profiles.Select(item => item.Id).ToArray(),
                ProfileRunCount = runs.Length
            },
            IntervalFormula = "[o_min - d_max, o_max - d_min]",
            PublicDependency = dependency,
            Profiles = profiles,
            Runs = runs,
            MonotonicityAudits = monotonicity,
            MonotonicityViolationCount = monotonicity.Count(item =>
                !item.DerivedWindowIsSubset || !item.CandidateSetIsSubset),
            ExistingS5Changed = false,
            InterpretationBoundary =
                "This deterministic fixture validates one bounded-duration derivation path; " +
                "it does not validate S5 time semantics or a general temporal rule engine."
        };
    }

    private static void Validate(BoundedDurationTimeFixtureDefinition fixture)
    {
        RequireIdentifier(fixture.Id, nameof(fixture.Id));
        RequireIdentifier(fixture.ScenarioId, nameof(fixture.ScenarioId));
        RequireIdentifier(fixture.TargetEntityId, nameof(fixture.TargetEntityId));
        RequireIdentifier(fixture.SourceEvidenceIdentifier, nameof(fixture.SourceEvidenceIdentifier));
        RequireIdentifier(fixture.LaterEventKind, nameof(fixture.LaterEventKind));
        RequireIdentifier(fixture.TruthRegionId, nameof(fixture.TruthRegionId));
        if (fixture.AvailableAtTick < 0 || fixture.EvidenceAvailabilityCutoff < 0)
        {
            throw new InvalidDataException("Availability ticks cannot be negative.");
        }

        if (fixture.AvailableAtTick > fixture.EvidenceAvailabilityCutoff)
        {
            throw new InvalidDataException("The fixture observation must be available at the cutoff.");
        }

        if (fixture.EvidenceState != EvidenceState.Confirmed)
        {
            throw new InvalidDataException("The acceptance fixture requires confirmed public evidence.");
        }

        if (!string.Equals(
                fixture.PublicDependency.LaterEventKind,
                fixture.LaterEventKind,
                StringComparison.Ordinal) ||
            !string.Equals(
                fixture.PublicDependency.TargetEntityId,
                fixture.TargetEntityId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The public dependency must target the fixture's observed later event and entity.");
        }

        _ = new PublicDurationBound(
            fixture.PublicDependency.MinimumDurationTicks,
            fixture.PublicDependency.MaximumDurationTicks);
        if (fixture.CandidateStates.Count < 2)
        {
            throw new InvalidDataException("At least two predecessor candidates are required.");
        }

        foreach (var candidate in fixture.CandidateStates)
        {
            _ = new InteractionTimeWindow(candidate.StartTick, candidate.EndTick);
            if (candidate.RegionIds.Count == 0)
            {
                throw new InvalidDataException("Each predecessor candidate state requires a Region.");
            }

            if (candidate.TemporalKind == CandidateWarpTemporalKind.EventPoint &&
                candidate.StartTick != candidate.EndTick)
            {
                throw new InvalidDataException("Event-point predecessor candidates must use exact ticks.");
            }
        }

        var candidateRegions = fixture.CandidateStates.SelectMany(item => item.RegionIds)
            .Distinct(StringComparer.Ordinal).ToArray();
        if (!candidateRegions.Contains(fixture.TruthRegionId, StringComparer.Ordinal))
        {
            throw new InvalidDataException("The truth Region must be one of the public candidates.");
        }

        if (candidateRegions.Any(item =>
            !fixture.PublicDependency.AllowedPredecessorRegionIds.Contains(item, StringComparer.Ordinal)))
        {
            throw new InvalidDataException(
                "The acceptance dependency must cover every candidate Region so time is the only varied axis.");
        }

        var profiles = fixture.Profiles.OrderBy(item => item.InformationOrder).ToArray();
        if (profiles.Length < 2 || profiles.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() !=
            profiles.Length || profiles.Select(item => item.InformationOrder).Distinct().Count() != profiles.Length)
        {
            throw new InvalidDataException("Profiles require unique IDs and information orders.");
        }

        for (var index = 0; index < profiles.Length; index++)
        {
            var profile = profiles[index];
            RequireIdentifier(profile.Id, nameof(profile.Id));
            RequireIdentifier(profile.Description, nameof(profile.Description));
            RequireIdentifier(profile.BoundaryRationale, nameof(profile.BoundaryRationale));
            if (profile.ObservedStartTick.HasValue != profile.ObservedEndTick.HasValue)
            {
                throw new InvalidDataException("A profile must supply both observed window bounds or neither.");
            }

            if (!profile.ObservedStartTick.HasValue)
            {
                if (index != profiles.Length - 1)
                {
                    throw new InvalidDataException("The unknown-time profile must be last.");
                }

                continue;
            }

            var window = new InteractionTimeWindow(
                profile.ObservedStartTick.Value,
                profile.ObservedEndTick!.Value);
            if (fixture.AvailableAtTick < window.EndTick)
            {
                throw new InvalidDataException(
                    "Evidence cannot become available before a profile's occurrence window ends.");
            }

            if (index > 0)
            {
                var previous = profiles[index - 1];
                if (previous.ObservedStartTick.HasValue &&
                    (window.StartTick > previous.ObservedStartTick.Value ||
                        window.EndTick < previous.ObservedEndTick!.Value))
                {
                    throw new InvalidDataException("Finite profile windows must be nested from precise to broad.");
                }
            }
        }
    }

    private static bool IsWindowSubset(
        InteractionTimeWindow? morePrecise,
        InteractionTimeWindow? coarser)
    {
        if (morePrecise is null)
        {
            return coarser is null;
        }

        return coarser is null ||
            (coarser.StartTick <= morePrecise.StartTick &&
                morePrecise.EndTick <= coarser.EndTick);
    }

    private static void RequireIdentifier(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"Fixture field '{field}' cannot be empty.");
        }
    }
}
