using FootprintSim.Evidence;
using FootprintSim.Inference.Derivation;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;

namespace FootprintSim.Evaluation;

public sealed class ParticipantAmbiguityFixtureEvaluationRunner
{
    public ParticipantAmbiguityFixtureEvaluationResult Run(
        ParticipantAmbiguityFixtureEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Fixture);
        Validate(request.Fixture);

        var fixture = request.Fixture;
        var encounterWindow = new InteractionTimeWindow(
            fixture.EncounterStartTick,
            fixture.EncounterEndTick);
        var participantEvidence = fixture.ParticipantLocationEvidence
            .Select(item => PublicParticipantLocationEvidence.Create(
                fixture.ScenarioId,
                fixture.Seed,
                item.SourceEvidenceIdentifier,
                item.ParticipantId,
                item.EntityKind,
                new InteractionTimeWindow(item.ApplicableStartTick, item.ApplicableEndTick),
                item.AllowedRegionIds,
                item.AvailableAtTick,
                item.EvidenceState,
                item.UsageRole,
                item.Visibility,
                item.SourceAncestryIds))
            .OrderBy(item => item.ParticipantId, StringComparer.Ordinal)
            .ThenBy(item => item.PublicInputId, StringComparer.Ordinal)
            .ToArray();
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
                        "m1f10-public-candidate-layer",
                        availableAtTick: 0,
                        isPersonEvidence: false,
                        isDeviceOnlyEvidence: false,
                        EvidenceState.Confirmed,
                        item.RegionIds)
                ]
            }).ToArray();
        var reconstructor = new ObservationDerivedHistoricalCandidateReconstructor();
        var profiles = fixture.Profiles
            .OrderBy(item => item.InformationOrder)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        var deriver = new EncounterParticipantLocationConstraintDeriver();
        var preliminaryRuns = profiles.Select(profile =>
        {
            var otherParticipant = Information(profile);
            var observation = PublicEncounterParticipantObservation.Create(
                fixture.ScenarioId,
                fixture.Seed,
                fixture.EncounterSourceEvidenceIdentifier,
                fixture.TargetEntityId,
                fixture.TargetEntityKind,
                otherParticipant,
                fixture.RelationKind,
                encounterWindow,
                fixture.EncounterAvailableAtTick,
                fixture.EncounterEvidenceState,
                InteractionVisibility.PublicEvidence,
                fixture.EncounterSourceAncestryIds);
            var derivation = deriver.Derive(
                observation,
                participantEvidence,
                fixture.EvidenceAvailabilityCutoff,
                PastStateEvidenceUsageRole.CandidateGeneration);
            var reconstruction = reconstructor.Reconstruct(
                fixture.ScenarioId,
                fixture.Seed,
                fixture.TargetEntityId,
                publicCandidates,
                derivation.Constraint);
            var candidateIds = reconstruction.Candidates
                .Select(item => item.CandidateId)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            return new PreliminaryRun(
                profile,
                observation,
                derivation,
                reconstruction,
                candidateIds);
        }).ToArray();
        var unknown = preliminaryRuns.Single(item =>
            item.Profile.IdentityKnowledge == ParticipantIdentityKnowledge.UnknownIdentity);
        var truthCandidates = unknown.Reconstruction.Candidates.Where(item =>
            item.TimeWindow.StartTick == fixture.TruthCandidateStartTick &&
            item.TimeWindow.EndTick == fixture.TruthCandidateEndTick &&
            string.Equals(item.RegionId, fixture.TruthCandidateRegionId, StringComparison.Ordinal))
            .ToArray();
        if (truthCandidates.Length != 1)
        {
            throw new InvalidDataException(
                "The evaluation-only truth address must match exactly one reconstructed candidate.");
        }

        var truthCandidateId = truthCandidates[0].CandidateId;
        var runs = preliminaryRuns.Select(item =>
        {
            var reductionCount = unknown.Reconstruction.Candidates.Count -
                item.Reconstruction.Candidates.Count;
            var containsTruth = item.CandidateIds.Contains(truthCandidateId, StringComparer.Ordinal);
            return new ParticipantAmbiguityFixtureRun
            {
                ProfileId = item.Profile.Id,
                InformationOrder = item.Profile.InformationOrder,
                EncounterObservationPresent = item.Observation.EncounterObservationPresent,
                SourceObservationId = item.Observation.ObservationId,
                EncounterSourceGroupId = item.Derivation.Audit.EncounterSourceGroupId,
                IdentityKnowledge = item.Observation.OtherParticipant.IdentityKnowledge,
                ParticipantEntityKind = item.Observation.OtherParticipant.EntityKind,
                PossibleParticipantIds = item.Observation.OtherParticipant.PossibleParticipantIds,
                ConstraintGenerated = item.Derivation.Constraint is not null,
                ConstraintId = item.Derivation.Constraint?.ConstraintId,
                DerivedAllowedRegionIds = item.Derivation.Constraint?.AllowedRegionIds ?? [],
                DerivationAudit = item.Derivation.Audit,
                EncounterSourceGroupCount = 1,
                ParticipantEvidenceSourceGroupCount =
                    item.Derivation.Audit.ParticipantEvidenceSourceGroupIds.Count,
                GeneratedConstraintSourceGroupCount = item.Derivation.Constraint is null ? 0 : 1,
                InitialCandidateCount = item.Reconstruction.InitialCandidateCount,
                Candidates = item.Reconstruction.Candidates,
                CandidateIds = item.CandidateIds,
                CandidateCount = item.Reconstruction.Candidates.Count,
                TruthCandidateId = truthCandidateId,
                ContainsTruth = containsTruth,
                FalseExclusion = !containsTruth,
                CandidateReductionVsUnknownCount = reductionCount,
                CandidateReductionVsUnknownFraction = unknown.Reconstruction.Candidates.Count == 0
                    ? 0m
                    : reductionCount / (decimal)unknown.Reconstruction.Candidates.Count,
                ParticipantAlternativesEvaluated =
                    item.Derivation.Audit.ParticipantAlternativesEvaluated,
                ParticipantLocationEvidenceRecordsInspected =
                    item.Derivation.Audit.ParticipantLocationEvidenceRecordsInspected,
                CandidateGenerationWork = item.Reconstruction.CandidateGenerationWork,
                ReconstructionEvidenceRecordsInspected =
                    item.Reconstruction.EvidenceRecordsInspected,
                ReconstructionSourceGroupsInspected = item.Reconstruction.SourceGroupsInspected
            };
        }).ToArray();
        var monotonicity = runs.Zip(runs.Skip(1), (morePrecise, coarser) =>
            new ParticipantAmbiguityMonotonicityAudit
            {
                MorePreciseProfileId = morePrecise.ProfileId,
                CoarserProfileId = coarser.ProfileId,
                ParticipantInformationIsSubset = InformationSubset(morePrecise, coarser),
                DerivedRegionSetIsSubset = RegionSubset(morePrecise, coarser),
                CandidateSetIsSubset = morePrecise.CandidateIds.All(item =>
                    coarser.CandidateIds.Contains(item, StringComparer.Ordinal)),
                MorePreciseCandidateIds = morePrecise.CandidateIds,
                CoarserCandidateIds = coarser.CandidateIds
            }).ToArray();

        return new ParticipantAmbiguityFixtureEvaluationResult
        {
            Manifest = new ParticipantAmbiguityFixtureManifest
            {
                FixtureId = fixture.Id,
                ScenarioId = fixture.ScenarioId,
                FixtureContentSha256 = request.FixtureContentSha256,
                SourceCommitSha = request.SourceCommitSha,
                DirtyWorkingTree = request.DirtyWorkingTree,
                RuleId = EncounterParticipantLocationConstraintDeriver.RuleId,
                ProfileIds = profiles.Select(item => item.Id).ToArray(),
                ProfileRunCount = runs.Length,
                TruthUsedForDerivation = false
            },
            SemanticRule =
                "At the reported PersonPersonEncounter time, the target and actual other person " +
                "are co-located; public same-time Regions for every possible other person " +
                "therefore constrain the target to their conservative union.",
            ConservativeUnionFormula =
                "AllowedRegions(target) = union(AllowedRegions(p) for p in PossibleParticipants)",
            TargetEntityId = fixture.TargetEntityId,
            RelationKind = fixture.RelationKind,
            EncounterTimeWindow = encounterWindow,
            PublicParticipantLocationEvidence = participantEvidence,
            TruthOtherParticipantId = fixture.TruthOtherParticipantId,
            TruthCandidateId = truthCandidateId,
            Profiles = profiles,
            Runs = runs,
            MonotonicityAudits = monotonicity,
            MonotonicityViolationCount = monotonicity.Count(item =>
                !item.ParticipantInformationIsSubset ||
                !item.DerivedRegionSetIsSubset ||
                !item.CandidateSetIsSubset),
            ExistingS5Changed = false,
            InterpretationBoundary =
                "This deterministic Person-only fixture validates one public same-time " +
                "participant-location union path. It does not measure population performance, " +
                "resolve identities, define kind-unknown semantics, or migrate S5."
        };
    }

    private static OtherParticipantInformation Information(
        ParticipantAmbiguityProfileDefinition profile) => profile.IdentityKnowledge switch
        {
            ParticipantIdentityKnowledge.Exact => OtherParticipantInformation.Exact(
                profile.PossibleParticipantIds.Single(),
                profile.EntityKind),
            ParticipantIdentityKnowledge.FiniteAlternatives =>
                OtherParticipantInformation.FiniteAlternatives(
                    profile.PossibleParticipantIds,
                    profile.EntityKind),
            ParticipantIdentityKnowledge.UnknownIdentity =>
                OtherParticipantInformation.UnknownIdentity(profile.EntityKind),
            _ => throw new InvalidDataException(
                $"Profile '{profile.Id}' has unsupported participant knowledge.")
        };

    private static bool InformationSubset(
        ParticipantAmbiguityFixtureRun precise,
        ParticipantAmbiguityFixtureRun coarse) =>
        coarse.IdentityKnowledge == ParticipantIdentityKnowledge.UnknownIdentity ||
        precise.PossibleParticipantIds.All(item =>
            coarse.PossibleParticipantIds.Contains(item, StringComparer.Ordinal));

    private static bool RegionSubset(
        ParticipantAmbiguityFixtureRun precise,
        ParticipantAmbiguityFixtureRun coarse) =>
        !coarse.ConstraintGenerated ||
        precise.DerivedAllowedRegionIds.All(item =>
            coarse.DerivedAllowedRegionIds.Contains(item, StringComparer.Ordinal));

    private static void Validate(ParticipantAmbiguityFixtureDefinition fixture)
    {
        RequireIdentifier(fixture.Id, nameof(fixture.Id));
        RequireIdentifier(fixture.ScenarioId, nameof(fixture.ScenarioId));
        RequireIdentifier(fixture.TargetEntityId, nameof(fixture.TargetEntityId));
        RequireIdentifier(
            fixture.EncounterSourceEvidenceIdentifier,
            nameof(fixture.EncounterSourceEvidenceIdentifier));
        RequireIdentifier(fixture.TruthOtherParticipantId, nameof(fixture.TruthOtherParticipantId));
        RequireIdentifier(fixture.TruthCandidateRegionId, nameof(fixture.TruthCandidateRegionId));
        if (fixture.RelationKind != InteractionRelationKind.PersonPersonEncounter)
        {
            throw new InvalidDataException(
                "The acceptance fixture keeps the exact PersonPersonEncounter relation fixed.");
        }

        if (fixture.TargetEntityKind != InteractionEntityKind.Person)
        {
            throw new InvalidDataException("The acceptance fixture target must be a person.");
        }

        var encounterWindow = new InteractionTimeWindow(
            fixture.EncounterStartTick,
            fixture.EncounterEndTick);
        if (fixture.EncounterAvailableAtTick < encounterWindow.EndTick ||
            fixture.EncounterAvailableAtTick > fixture.EvidenceAvailabilityCutoff)
        {
            throw new InvalidDataException(
                "The confirmed encounter observation must be available at the cutoff.");
        }

        if (fixture.EncounterEvidenceState != EvidenceState.Confirmed)
        {
            throw new InvalidDataException("The acceptance fixture requires a confirmed encounter.");
        }

        if (fixture.ParticipantLocationEvidence.Count < 2)
        {
            throw new InvalidDataException(
                "At least two public participant-location evidence records are required.");
        }

        foreach (var item in fixture.ParticipantLocationEvidence)
        {
            RequireIdentifier(item.SourceEvidenceIdentifier, nameof(item.SourceEvidenceIdentifier));
            RequireIdentifier(item.ParticipantId, nameof(item.ParticipantId));
            _ = new InteractionTimeWindow(item.ApplicableStartTick, item.ApplicableEndTick);
            if (item.AllowedRegionIds.Count == 0)
            {
                throw new InvalidDataException(
                    $"Participant '{item.ParticipantId}' requires at least one public Region.");
            }

            if (item.EntityKind != InteractionEntityKind.Person ||
                item.Visibility != InteractionVisibility.PublicEvidence ||
                item.EvidenceState != EvidenceState.Confirmed ||
                item.UsageRole != PastStateEvidenceUsageRole.CandidateGeneration)
            {
                throw new InvalidDataException(
                    "Acceptance participant evidence must be confirmed public person evidence " +
                    "for CandidateGeneration.");
            }

            if (item.AvailableAtTick > fixture.EvidenceAvailabilityCutoff)
            {
                throw new InvalidDataException(
                    $"Participant evidence '{item.SourceEvidenceIdentifier}' is unavailable at cutoff.");
            }

            if (item.ApplicableStartTick > encounterWindow.StartTick ||
                item.ApplicableEndTick < encounterWindow.EndTick)
            {
                throw new InvalidDataException(
                    $"Participant evidence '{item.SourceEvidenceIdentifier}' must cover the encounter window.");
            }
        }

        var duplicateParticipant = fixture.ParticipantLocationEvidence
            .GroupBy(item => item.ParticipantId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateParticipant is not null)
        {
            throw new InvalidDataException(
                $"Participant '{duplicateParticipant.Key}' has multiple location evidence records.");
        }

        if (!fixture.ParticipantLocationEvidence.Any(item => string.Equals(
            item.ParticipantId,
            fixture.TruthOtherParticipantId,
            StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                "The evaluation-only truth participant must exist in the public fixture population.");
        }

        if (fixture.CandidateStates.Count < 2)
        {
            throw new InvalidDataException("At least two historical candidates are required.");
        }

        foreach (var candidate in fixture.CandidateStates)
        {
            _ = new InteractionTimeWindow(candidate.StartTick, candidate.EndTick);
            if (candidate.RegionIds.Count == 0)
            {
                throw new InvalidDataException("Every candidate state requires a Region.");
            }
        }

        _ = new InteractionTimeWindow(
            fixture.TruthCandidateStartTick,
            fixture.TruthCandidateEndTick);
        var profiles = fixture.Profiles
            .OrderBy(item => item.InformationOrder)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        if (profiles.Length != 4 ||
            !profiles.Select(item => item.Id).SequenceEqual(
                new[] { "exact", "narrow", "broad", "unknown" },
                StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "The acceptance fixture requires exact, narrow, broad, and unknown profiles.");
        }

        if (profiles.Select(item => item.InformationOrder).Distinct().Count() != profiles.Length)
        {
            throw new InvalidDataException("Profile information order values must be unique.");
        }

        foreach (var profile in profiles)
        {
            if (profile.EntityKind != InteractionEntityKind.Person)
            {
                throw new InvalidDataException("All acceptance profiles must remain Person-only.");
            }

            _ = Information(profile);
            if (profile.PossibleParticipantIds.Any(item => string.Equals(
                item,
                fixture.TargetEntityId,
                StringComparison.Ordinal)))
            {
                throw new InvalidDataException(
                    "The fixed target cannot appear among other-participant alternatives.");
            }
        }

        for (var index = 0; index < profiles.Length - 2; index++)
        {
            if (!profiles[index].PossibleParticipantIds.All(item =>
                profiles[index + 1].PossibleParticipantIds.Contains(item, StringComparer.Ordinal)))
            {
                throw new InvalidDataException(
                    "Exact, narrow, and broad participant alternatives must be nested.");
            }
        }

        var knownEvidenceIds = fixture.ParticipantLocationEvidence
            .Select(item => item.ParticipantId)
            .ToHashSet(StringComparer.Ordinal);
        if (profiles.Take(3).SelectMany(item => item.PossibleParticipantIds)
            .Any(item => !knownEvidenceIds.Contains(item)))
        {
            throw new InvalidDataException(
                "Every finite acceptance alternative requires fixed public location evidence.");
        }
    }

    private static string RequireIdentifier(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"{name} cannot be empty.");
        }

        return value;
    }

    private sealed record PreliminaryRun(
        ParticipantAmbiguityProfileDefinition Profile,
        PublicEncounterParticipantObservation Observation,
        ParticipantConstraintDerivationResult Derivation,
        ObservationDerivedHistoricalCandidateResult Reconstruction,
        IReadOnlyList<string> CandidateIds);
}
