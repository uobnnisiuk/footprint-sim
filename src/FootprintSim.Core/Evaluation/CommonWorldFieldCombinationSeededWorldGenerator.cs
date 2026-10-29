using FootprintSim.Interactions;
using FootprintSim.Simulation;

namespace FootprintSim.Evaluation;

public static class CommonWorldFieldCombinationExperimentValidator
{
    public static IReadOnlyList<CommonWorldSetInclusionDefinition> RequiredSetInclusions { get; } =
    [
        Inclusion("location", "none"),
        Inclusion("time", "none"),
        Inclusion("participant", "none"),
        Inclusion("location-time", "location"),
        Inclusion("location-time", "time"),
        Inclusion("location-participant", "location"),
        Inclusion("location-participant", "participant"),
        Inclusion("time-participant", "time"),
        Inclusion("time-participant", "participant"),
        Inclusion("location-time-participant", "location-time"),
        Inclusion("location-time-participant", "location-participant"),
        Inclusion("location-time-participant", "time-participant")
    ];

    public static IReadOnlyList<CommonWorldConditionalComparisonDefinition> RequiredComparisons { get; } =
    [
        Comparison("time-added-to-location", "location", "location-time", CommonWorldObservationAxis.Time),
        Comparison("participant-added-to-location", "location", "location-participant", CommonWorldObservationAxis.Participant),
        Comparison("location-added-to-time", "time", "location-time", CommonWorldObservationAxis.Location),
        Comparison("participant-added-to-time", "time", "time-participant", CommonWorldObservationAxis.Participant),
        Comparison("location-added-to-participant", "participant", "location-participant", CommonWorldObservationAxis.Location),
        Comparison("time-added-to-participant", "participant", "time-participant", CommonWorldObservationAxis.Time),
        Comparison("participant-added-to-location-time", "location-time", "location-time-participant", CommonWorldObservationAxis.Participant),
        Comparison("time-added-to-location-participant", "location-participant", "location-time-participant", CommonWorldObservationAxis.Time),
        Comparison("location-added-to-time-participant", "time-participant", "location-time-participant", CommonWorldObservationAxis.Location)
    ];

    public static IReadOnlyList<string> RequiredFailureConditions { get; } =
    [
        "truth-leakage",
        "profile-world-change",
        "profile-candidate-universe-change",
        "off-axis-converted-to-absent-false-unknown-or-empty",
        "source-group-selects-logical-operator",
        "new-location-time-participant-interaction-rule",
        "joint-set-inclusion-violation",
        "false-exclusion",
        "private-or-future-evidence-use",
        "available-at-used-in-time-arithmetic",
        "participant-alternatives-intersected",
        "result-dependent-generator-tuning",
        "seed-skip-or-regenerate",
        "nondeterministic-identity-or-output"
    ];

    public static void Validate(
        CommonWorldFieldCombinationExperimentDefinition experiment,
        IReadOnlyList<int>? seeds = null)
    {
        ArgumentNullException.ThrowIfNull(experiment);
        RequireIdentifier(experiment.Id, nameof(experiment.Id));
        RequireIdentifier(experiment.ScenarioId, nameof(experiment.ScenarioId));
        RequireIdentifier(experiment.TargetEntityId, nameof(experiment.TargetEntityId));
        RequireIdentifier(experiment.TargetStateContextId, nameof(experiment.TargetStateContextId));
        RequireIdentifier(experiment.CandidateDomainId, nameof(experiment.CandidateDomainId));
        if (experiment.RequiredSeedStart != 0 || experiment.RequiredSeedEnd != 199)
        {
            throw new InvalidDataException("M1-F16 requires the exact preregistered seed range 0..199.");
        }

        if (seeds is not null)
        {
            var expected = Enumerable.Range(0, 200).ToArray();
            var actual = seeds.Distinct().OrderBy(item => item).ToArray();
            if (!actual.SequenceEqual(expected))
            {
                throw new InvalidDataException("M1-F16 evaluation requires every seed in 0..199.");
            }
        }

        if (experiment.TargetEntityKind != InteractionEntityKind.Person ||
            experiment.FixedRelationKind != InteractionRelationKind.PersonPersonEncounter)
        {
            throw new InvalidDataException(
                "M1-F16 is Person-only and fixes exact PersonPersonEncounter.");
        }

        var generator = experiment.Generator;
        RequireIdentifier(generator.Id, nameof(generator.Id));
        RequirePositiveRange(generator.CandidateCountMinimum, generator.CandidateCountMaximum, "candidate count");
        RequirePositiveRange(generator.RegionUniverseSizeMinimum, generator.RegionUniverseSizeMaximum, "Region universe");
        RequireNonNegativeRange(generator.CandidateBaseTickMinimum, generator.CandidateBaseTickMaximum, "candidate base tick");
        RequireAscendingRange(generator.CandidateStartOffsetMinimum, generator.CandidateStartOffsetMaximum, "candidate start offset");
        RequireNonNegativeRange(generator.CandidateIntervalWidthMinimum, generator.CandidateIntervalWidthMaximum, "candidate interval width");
        RequirePositiveRange(generator.LocationObservedRegionCountMinimum, generator.LocationObservedRegionCountMaximum, "location Region count");
        RequireNonNegativeRange(generator.DurationMinimumTickMinimum, generator.DurationMinimumTickMaximum, "duration minimum");
        RequireNonNegativeRange(generator.DurationWidthMinimum, generator.DurationWidthMaximum, "duration width");
        RequirePositiveRange(generator.ParticipantPoolSizeMinimum, generator.ParticipantPoolSizeMaximum, "participant pool");
        RequirePositiveRange(generator.ParticipantRegionSetSizeMinimum, generator.ParticipantRegionSetSizeMaximum, "participant Region count");
        var cellCount = checked(
            (generator.CandidateStartOffsetMaximum - generator.CandidateStartOffsetMinimum + 1) *
            generator.RegionUniverseSizeMinimum);
        if (generator.CandidateCountMaximum > cellCount ||
            generator.CandidateBaseTickMinimum + generator.CandidateStartOffsetMinimum < 0 ||
            generator.LocationObservedRegionCountMaximum > generator.RegionUniverseSizeMinimum ||
            generator.ParticipantRegionSetSizeMaximum > generator.RegionUniverseSizeMinimum ||
            generator.ParticipantPoolSizeMinimum < 2 ||
            generator.AvailabilityLagTicks < 1)
        {
            throw new InvalidDataException(
                "Generator ranges cannot produce the registered complete, non-negative common worlds.");
        }

        if (!generator.CandidateStartRegionPairsSampledWithoutReplacement ||
            !generator.CandidateTickCollisionsAllowed ||
            !generator.CandidateRegionCollisionsAllowed ||
            !generator.UnusedRegionsAllowed ||
            !generator.TruthSelectedAfterCandidateUniverse ||
            !generator.TruthCompatibleLocationGenerated ||
            !generator.TruthCompatibleDurationGenerated ||
            !generator.TruthParticipantEvidenceIncludesTruthRegion ||
            !generator.ParticipantRegionSamplingIndependentAcrossParticipants ||
            generator.MissingEvidenceEnabled ||
            generator.ConflictEnabled ||
            !string.Equals(generator.InvalidWorldPolicy, "fail-no-skip-no-regenerate", StringComparison.Ordinal) ||
            !string.Equals(generator.SeedStrategy, "stable-purpose-derived-streams-v1", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Generator semantics differ from the M1-F16 preregistration.");
        }

        var profiles = experiment.Profiles.OrderBy(item => item.Order).ToArray();
        var expectedProfiles = new[]
        {
            ("none", false, false, false),
            ("location", true, false, false),
            ("time", false, true, false),
            ("participant", false, false, true),
            ("location-time", true, true, false),
            ("location-participant", true, false, true),
            ("time-participant", false, true, true),
            ("location-time-participant", true, true, true)
        };
        if (profiles.Length != expectedProfiles.Length)
        {
            throw new InvalidDataException("M1-F16 requires exactly eight profiles.");
        }

        for (var index = 0; index < expectedProfiles.Length; index++)
        {
            var expected = expectedProfiles[index];
            var actual = profiles[index];
            if (actual.Order != index ||
                actual.Id != expected.Item1 ||
                actual.SelectLocation != expected.Item2 ||
                actual.SelectTime != expected.Item3 ||
                actual.SelectParticipant != expected.Item4)
            {
                throw new InvalidDataException("Profile definitions differ from preregistration.");
            }
        }

        if (!SameInclusions(experiment.PrimarySetInclusions, RequiredSetInclusions) ||
            !SameComparisons(experiment.ConditionalComparisons, RequiredComparisons) ||
            !experiment.FailureConditions.SequenceEqual(RequiredFailureConditions, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Primary hypotheses, conditional comparisons, or failure conditions changed.");
        }
    }

    private static bool SameInclusions(
        IReadOnlyList<CommonWorldSetInclusionDefinition> actual,
        IReadOnlyList<CommonWorldSetInclusionDefinition> expected) =>
        actual.Select(Identity).SequenceEqual(expected.Select(Identity), StringComparer.Ordinal);

    private static bool SameComparisons(
        IReadOnlyList<CommonWorldConditionalComparisonDefinition> actual,
        IReadOnlyList<CommonWorldConditionalComparisonDefinition> expected) =>
        actual.Select(Identity).SequenceEqual(expected.Select(Identity), StringComparer.Ordinal);

    private static string Identity(CommonWorldSetInclusionDefinition item) =>
        $"{item.SubsetProfileId}<={item.SupersetProfileId}";

    private static string Identity(CommonWorldConditionalComparisonDefinition item) =>
        $"{item.Id}:{item.BaselineProfileId}:{item.CombinedProfileId}:{item.AddedAxis}";

    private static CommonWorldSetInclusionDefinition Inclusion(string subset, string superset) => new()
    {
        SubsetProfileId = subset,
        SupersetProfileId = superset
    };

    private static CommonWorldConditionalComparisonDefinition Comparison(
        string id,
        string baseline,
        string combined,
        CommonWorldObservationAxis axis) => new()
        {
            Id = id,
            BaselineProfileId = baseline,
            CombinedProfileId = combined,
            AddedAxis = axis
        };

    private static void RequirePositiveRange(int minimum, int maximum, string name)
    {
        if (minimum <= 0 || maximum < minimum)
        {
            throw new InvalidDataException($"Generator {name} range is invalid.");
        }
    }

    private static void RequireNonNegativeRange(int minimum, int maximum, string name)
    {
        if (minimum < 0 || maximum < minimum)
        {
            throw new InvalidDataException($"Generator {name} range is invalid.");
        }
    }

    private static void RequireAscendingRange(int minimum, int maximum, string name)
    {
        if (maximum < minimum)
        {
            throw new InvalidDataException($"Generator {name} range is invalid.");
        }
    }

    private static void RequireIdentifier(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"Experiment field '{field}' cannot be empty.");
        }
    }
}

public sealed class CommonWorldFieldCombinationSeededWorldGenerator
{
    public GeneratedCommonFieldCombinationWorld Generate(
        CommonWorldFieldCombinationExperimentDefinition experiment,
        int seed)
    {
        CommonWorldFieldCombinationExperimentValidator.Validate(experiment);
        if (seed < experiment.RequiredSeedStart || seed > experiment.RequiredSeedEnd)
        {
            throw new ArgumentOutOfRangeException(nameof(seed));
        }

        var definition = experiment.Generator;
        var candidateCount = Draw(experiment, seed, "candidate-count",
            definition.CandidateCountMinimum, definition.CandidateCountMaximum);
        var regionCount = Draw(experiment, seed, "region-universe-size",
            definition.RegionUniverseSizeMinimum, definition.RegionUniverseSizeMaximum);
        var baseTick = Draw(experiment, seed, "candidate-base-tick",
            definition.CandidateBaseTickMinimum, definition.CandidateBaseTickMaximum);
        var regions = Enumerable.Range(0, regionCount)
            .Select(slot => $"seed-{seed:D3}-region-{slot:D2}")
            .ToArray();
        var cells = Enumerable.Range(
                definition.CandidateStartOffsetMinimum,
                definition.CandidateStartOffsetMaximum - definition.CandidateStartOffsetMinimum + 1)
            .SelectMany(offset => regions.Select(region => new CandidateCell(offset, region)))
            .ToArray();
        var selectedCells = Sample(cells, candidateCount, Random(experiment, seed, "candidate-cells"));
        var candidates = selectedCells.Select((cell, slot) =>
        {
            var width = Draw(experiment, seed, $"candidate-{slot:D2}-interval-width",
                definition.CandidateIntervalWidthMinimum,
                definition.CandidateIntervalWidthMaximum);
            return new GeneratedCommonWorldCandidate
            {
                CandidateKey = $"candidate-{slot:D2}",
                PublicSourceIdentifier = $"seed-{seed:D3}-candidate-{slot:D2}-public-source",
                StartTick = checked(baseTick + cell.StartOffset),
                EndTick = checked(baseTick + cell.StartOffset + width),
                RegionId = cell.RegionId,
                IsTruthCandidate = false
            };
        }).ToArray();
        var truthSlot = Draw(experiment, seed, "truth-candidate-slot", 0, candidates.Length - 1);
        candidates[truthSlot] = candidates[truthSlot] with { IsTruthCandidate = true };
        var truthCandidate = candidates[truthSlot];

        var locationCount = Draw(experiment, seed, "location-observed-region-count",
            definition.LocationObservedRegionCountMinimum,
            definition.LocationObservedRegionCountMaximum);
        var locationRegions = SampleIncluding(
            regions,
            truthCandidate.RegionId,
            locationCount,
            Random(experiment, seed, "location-observed-regions"));

        var durationMinimum = Draw(experiment, seed, "duration-minimum",
            definition.DurationMinimumTickMinimum,
            definition.DurationMinimumTickMaximum);
        var durationWidth = Draw(experiment, seed, "duration-width",
            definition.DurationWidthMinimum,
            definition.DurationWidthMaximum);
        var durationMaximum = checked(durationMinimum + durationWidth);
        var actualDuration = Draw(experiment, seed, "truth-duration", durationMinimum, durationMaximum);
        var encounterTick = truthCandidate.StartTick;
        var laterEventTick = checked(encounterTick + actualDuration);

        var participantCount = Draw(experiment, seed, "participant-pool-size",
            definition.ParticipantPoolSizeMinimum,
            definition.ParticipantPoolSizeMaximum);
        var truthParticipantSlot = Draw(experiment, seed, "truth-participant-slot", 0, participantCount - 1);
        var participantIds = Enumerable.Range(0, participantCount)
            .Select(slot => $"seed-{seed:D3}-person-{slot:D2}")
            .ToArray();
        var participantEvidence = participantIds.Select((participantId, slot) =>
        {
            var count = Draw(experiment, seed, $"participant-{slot:D2}-region-count",
                definition.ParticipantRegionSetSizeMinimum,
                definition.ParticipantRegionSetSizeMaximum);
            var random = Random(experiment, seed, $"participant-{slot:D2}-regions");
            var participantRegions = slot == truthParticipantSlot
                ? SampleIncluding(regions, truthCandidate.RegionId, count, random)
                : Sample(regions, count, random)
                    .OrderBy(item => item, StringComparer.Ordinal)
                    .ToArray();
            return new GeneratedCommonWorldParticipantEvidence
            {
                ParticipantId = participantId,
                SourceEvidenceIdentifier = $"public-location-{participantId}",
                SourceAncestryId = $"{participantId}-location-source-group",
                AllowedRegionIds = participantRegions,
                IsTruthParticipant = slot == truthParticipantSlot
            };
        }).OrderBy(item => item.ParticipantId, StringComparer.Ordinal).ToArray();
        var truthParticipant = participantEvidence.Single(item => item.IsTruthParticipant);
        var observedParticipants = participantEvidence.Select(item => item.ParticipantId)
            .OrderBy(item => item, StringComparer.Ordinal).ToArray();
        var availabilityCutoff = checked(laterEventTick + definition.AvailabilityLagTicks);
        var locationObservationSource = $"seed-{seed:D3}-public-location-observation";
        var locationSourceGroup = $"seed-{seed:D3}-location-source-group";
        var encounterObservationSource = $"seed-{seed:D3}-public-encounter-observation";
        var encounterSourceGroup = $"seed-{seed:D3}-encounter-source-group";
        var laterObservationSource = $"seed-{seed:D3}-public-later-event-observation";
        var laterSourceGroup = $"seed-{seed:D3}-later-event-source-group";
        var dependencyId = $"seed-{seed:D3}-public-bounded-dependency";
        var durationSource = $"seed-{seed:D3}-public-duration-source";
        var durationSourceGroup = InteractionStableIdentifier.Create("srcg", durationSource);
        var usedRegions = candidates.Select(item => item.RegionId).ToHashSet(StringComparer.Ordinal);
        var candidateIdentity = string.Join("|", candidates.Select(item =>
            $"{item.CandidateKey}:{item.StartTick}:{item.EndTick}:{item.RegionId}:" +
            item.PublicSourceIdentifier));
        var participantIdentity = string.Join("|", participantEvidence.Select(item =>
            $"{item.ParticipantId}:{string.Join("+", item.AllowedRegionIds)}:" +
            $"{item.SourceEvidenceIdentifier}:{item.SourceAncestryId}"));
        var publicWorldId = InteractionStableIdentifier.Create(
            "cfw",
            experiment.Id,
            definition.Id,
            InteractionStableIdentifier.Seed(seed),
            string.Join("|", regions),
            candidateIdentity,
            string.Join("|", locationRegions),
            InteractionStableIdentifier.Tick(encounterTick),
            InteractionStableIdentifier.Tick(laterEventTick),
            InteractionStableIdentifier.Tick(durationMinimum),
            InteractionStableIdentifier.Tick(durationMaximum),
            participantIdentity,
            string.Join("|", observedParticipants),
            experiment.FixedRelationKind.ToString(),
            InteractionStableIdentifier.Tick(availabilityCutoff));
        var truthWorldId = InteractionStableIdentifier.Create(
            "ctw",
            publicWorldId,
            truthCandidate.CandidateKey,
            truthParticipant.ParticipantId,
            InteractionStableIdentifier.Tick(actualDuration));

        return new GeneratedCommonFieldCombinationWorld
        {
            GeneratorId = definition.Id,
            PublicWorldId = publicWorldId,
            TruthWorldId = truthWorldId,
            Seed = seed,
            RegionUniverse = regions,
            UnusedRegionCount = regions.Count(item => !usedRegions.Contains(item)),
            CandidateCount = candidateCount,
            CandidateBaseTick = baseTick,
            CandidateStates = candidates,
            TruthCandidateKey = truthCandidate.CandidateKey,
            TruthCandidateStartTick = truthCandidate.StartTick,
            TruthCandidateEndTick = truthCandidate.EndTick,
            TruthRegionId = truthCandidate.RegionId,
            LocationObservedRegionIds = locationRegions,
            LocationObservationSourceId = locationObservationSource,
            LocationSourceAncestryId = locationSourceGroup,
            EncounterTick = encounterTick,
            EncounterObservationSourceId = encounterObservationSource,
            EncounterSourceAncestryId = encounterSourceGroup,
            LaterEventTick = laterEventTick,
            LaterEventObservationSourceId = laterObservationSource,
            LaterEventSourceAncestryId = laterSourceGroup,
            DurationMinimumTicks = durationMinimum,
            DurationMaximumTicks = durationMaximum,
            ActualTruthDurationTicks = actualDuration,
            DependencyId = dependencyId,
            DurationSourceId = durationSource,
            DurationSourceGroupId = durationSourceGroup,
            Participants = participantEvidence,
            ParticipantEvidenceOverlapKind = ClassifyOverlap(participantEvidence),
            ObservedPossibleParticipantIds = observedParticipants,
            TruthParticipantId = truthParticipant.ParticipantId,
            EvidenceAvailabilityCutoff = availabilityCutoff
        };
    }

    private static IReadOnlyList<string> SampleIncluding(
        IReadOnlyList<string> universe,
        string required,
        int count,
        StableRandom random) => new[] { required }
        .Concat(Sample(
            universe.Where(item => !string.Equals(item, required, StringComparison.Ordinal)).ToArray(),
            count - 1,
            random))
        .Distinct(StringComparer.Ordinal)
        .OrderBy(item => item, StringComparer.Ordinal)
        .ToArray();

    private static IReadOnlyList<T> Sample<T>(
        IReadOnlyList<T> universe,
        int count,
        StableRandom random)
    {
        if (count < 0 || count > universe.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        var available = universe.ToList();
        var selected = new List<T>(count);
        for (var index = 0; index < count; index++)
        {
            var selectedIndex = random.NextInclusive(0, available.Count - 1);
            selected.Add(available[selectedIndex]);
            available.RemoveAt(selectedIndex);
        }

        return selected;
    }

    private static ParticipantEvidenceOverlapKind ClassifyOverlap(
        IReadOnlyList<GeneratedCommonWorldParticipantEvidence> participants)
    {
        var first = participants[0].AllowedRegionIds;
        if (participants.Skip(1).All(item => first.SequenceEqual(item.AllowedRegionIds)))
        {
            return ParticipantEvidenceOverlapKind.Identical;
        }

        for (var left = 0; left < participants.Count; left++)
        {
            for (var right = left + 1; right < participants.Count; right++)
            {
                if (participants[left].AllowedRegionIds.Any(region =>
                    participants[right].AllowedRegionIds.Contains(region, StringComparer.Ordinal)))
                {
                    return ParticipantEvidenceOverlapKind.PartiallyOverlapping;
                }
            }
        }

        return ParticipantEvidenceOverlapKind.CompletelyDistinct;
    }

    private static int Draw(
        CommonWorldFieldCombinationExperimentDefinition experiment,
        int seed,
        string purpose,
        int minimum,
        int maximum) => Random(experiment, seed, purpose).NextInclusive(minimum, maximum);

    private static StableRandom Random(
        CommonWorldFieldCombinationExperimentDefinition experiment,
        int seed,
        string purpose) => new(StableSeed.Derive(
            unchecked((ulong)(long)seed),
            experiment.Id,
            experiment.Generator.Id,
            purpose));

    private sealed record CandidateCell(int StartOffset, string RegionId);
}
