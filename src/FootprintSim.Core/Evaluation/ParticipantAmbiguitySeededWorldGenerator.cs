using System.Globalization;
using FootprintSim.Interactions;
using FootprintSim.Simulation;

namespace FootprintSim.Evaluation;

public static class ParticipantAmbiguityPairedExperimentValidator
{
    public static void Validate(
        ParticipantAmbiguityPairedExperimentDefinition experiment,
        IReadOnlyList<int>? seeds = null)
    {
        ArgumentNullException.ThrowIfNull(experiment);
        RequireIdentifier(experiment.Id, nameof(experiment.Id));
        RequireIdentifier(experiment.ScenarioId, nameof(experiment.ScenarioId));
        RequireIdentifier(experiment.TargetEntityId, nameof(experiment.TargetEntityId));
        RequireIdentifier(
            experiment.EncounterSourceEvidenceIdentifier,
            nameof(experiment.EncounterSourceEvidenceIdentifier));
        RequireIdentifier(experiment.Generator.Id, nameof(experiment.Generator.Id));
        if (experiment.RequiredSeedStart < 0 || experiment.RequiredSeedEnd < experiment.RequiredSeedStart)
        {
            throw new InvalidDataException("The required seed range must be non-negative and ascending.");
        }

        if (seeds is not null)
        {
            var expected = Enumerable.Range(
                experiment.RequiredSeedStart,
                checked(experiment.RequiredSeedEnd - experiment.RequiredSeedStart + 1)).ToArray();
            var actual = seeds.Distinct().OrderBy(item => item).ToArray();
            if (!actual.SequenceEqual(expected))
            {
                throw new InvalidDataException(
                    $"Experiment '{experiment.Id}' requires the exact seed range " +
                    $"{experiment.RequiredSeedStart}..{experiment.RequiredSeedEnd}.");
            }
        }

        if (experiment.TargetEntityKind != InteractionEntityKind.Person ||
            experiment.RelationKind != InteractionRelationKind.PersonPersonEncounter)
        {
            throw new InvalidDataException(
                "The paired participant experiment is Person-only and fixes PersonPersonEncounter.");
        }

        if (experiment.EncounterEvidenceState != Evidence.EvidenceState.Confirmed ||
            experiment.EncounterSourceAncestryIds.Count == 0)
        {
            throw new InvalidDataException(
                "The paired participant experiment requires confirmed encounter evidence and ancestry.");
        }

        var generator = experiment.Generator;
        RequirePositiveRange(
            generator.CandidateCountMinimum,
            generator.CandidateCountMaximum,
            "candidate count");
        RequirePositiveRange(
            generator.RegionUniverseSizeMinimum,
            generator.RegionUniverseSizeMaximum,
            "Region universe size");
        RequirePositiveRange(
            generator.ParticipantPoolSizeMinimum,
            generator.ParticipantPoolSizeMaximum,
            "participant pool size");
        RequirePositiveRange(
            generator.ParticipantRegionSetSizeMinimum,
            generator.ParticipantRegionSetSizeMaximum,
            "participant Region-set size");
        RequireNonNegativeRange(
            generator.EncounterTickMinimum,
            generator.EncounterTickMaximum,
            "encounter tick");
        if (generator.ParticipantPoolSizeMinimum < 3 ||
            generator.ParticipantRegionSetSizeMaximum > generator.RegionUniverseSizeMinimum ||
            generator.AvailabilityLagTicks < 0)
        {
            throw new InvalidDataException(
                "Generator pool, Region-set, or availability ranges cannot satisfy the registered profiles.");
        }

        if (!generator.CandidateTicksEqualEncounterTick ||
            !generator.CandidateRegionCollisionsAllowed ||
            !generator.ParticipantRegionsUniqueWithinParticipant ||
            !generator.ParticipantRegionSamplingIndependentAcrossParticipants ||
            !generator.TruthParticipantEvidenceIncludesTruthCandidateRegion ||
            generator.MissingEvidenceSecondaryConditionEnabled ||
            !string.Equals(
                generator.InvalidWorldPolicy,
                "fail-no-skip-no-regenerate",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Generator semantics differ from the preregistered primary participant family.");
        }

        var profiles = experiment.Profiles.OrderBy(item => item.InformationOrder).ToArray();
        if (!profiles.Select(item => item.Id).SequenceEqual(
                ["exact", "narrow", "broad", "unknown"],
                StringComparer.Ordinal) ||
            !profiles.Select(item => item.InformationOrder).SequenceEqual([0, 1, 2, 3]))
        {
            throw new InvalidDataException(
                "Profiles must be exact, narrow, broad, unknown in orders 0..3.");
        }

        if (profiles[0].ParticipantCount != 1 ||
            profiles[0].UsesFullParticipantPool ||
            profiles[0].IdentityUnknown ||
            !string.Equals(profiles[0].SelectionRule, "truth-participant-only", StringComparison.Ordinal) ||
            profiles[1].ParticipantCount != 2 ||
            profiles[1].UsesFullParticipantPool ||
            profiles[1].IdentityUnknown ||
            !string.Equals(
                profiles[1].SelectionRule,
                "truth-participant-plus-lowest-id-nontruth-participant",
                StringComparison.Ordinal) ||
            profiles[2].ParticipantCount.HasValue ||
            !profiles[2].UsesFullParticipantPool ||
            profiles[2].IdentityUnknown ||
            !string.Equals(profiles[2].SelectionRule, "all-generated-participants", StringComparison.Ordinal) ||
            profiles[3].ParticipantCount.HasValue ||
            profiles[3].UsesFullParticipantPool ||
            !profiles[3].IdentityUnknown ||
            !string.Equals(profiles[3].SelectionRule, "no-identity-candidates", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Profile construction differs from preregistration.");
        }
    }

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

    private static void RequireIdentifier(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"Experiment field '{field}' cannot be empty.");
        }
    }
}

public sealed class ParticipantAmbiguitySeededWorldGenerator
{
    public GeneratedParticipantAmbiguityWorld Generate(
        ParticipantAmbiguityPairedExperimentDefinition experiment,
        int seed)
    {
        ParticipantAmbiguityPairedExperimentValidator.Validate(experiment);
        if (seed < experiment.RequiredSeedStart || seed > experiment.RequiredSeedEnd)
        {
            throw new ArgumentOutOfRangeException(nameof(seed));
        }

        var definition = experiment.Generator;
        var candidateCount = Draw(
            experiment,
            seed,
            "candidate-count",
            definition.CandidateCountMinimum,
            definition.CandidateCountMaximum);
        var regionUniverseSize = Draw(
            experiment,
            seed,
            "region-universe-size",
            definition.RegionUniverseSizeMinimum,
            definition.RegionUniverseSizeMaximum);
        var encounterTick = Draw(
            experiment,
            seed,
            "encounter-tick",
            definition.EncounterTickMinimum,
            definition.EncounterTickMaximum);
        var participantPoolSize = Draw(
            experiment,
            seed,
            "participant-pool-size",
            definition.ParticipantPoolSizeMinimum,
            definition.ParticipantPoolSizeMaximum);
        var truthCandidateSlot = Draw(
            experiment,
            seed,
            "truth-candidate-slot",
            minimum: 0,
            maximum: candidateCount - 1);
        var truthParticipantSlot = Draw(
            experiment,
            seed,
            "truth-participant-slot",
            minimum: 0,
            maximum: participantPoolSize - 1);
        var regionUniverse = Enumerable.Range(0, regionUniverseSize)
            .Select(slot => string.Join(
                '-',
                "seed",
                seed.ToString("D3", CultureInfo.InvariantCulture),
                "region",
                slot.ToString("D2", CultureInfo.InvariantCulture)))
            .ToArray();
        var candidateRegionRandom = Random(experiment, seed, "candidate-regions");
        var candidates = Enumerable.Range(0, candidateCount).Select(slot =>
        {
            var key = $"candidate-{slot.ToString("D2", CultureInfo.InvariantCulture)}";
            return new GeneratedParticipantAmbiguityCandidate
            {
                CandidateKey = key,
                PublicSourceIdentifier = string.Join(
                    '-',
                    "seed",
                    seed.ToString("D3", CultureInfo.InvariantCulture),
                    key,
                    "public-candidate-source"),
                Tick = encounterTick,
                RegionId = regionUniverse[candidateRegionRandom.NextInclusive(0, regionUniverse.Length - 1)],
                IsTruthCandidate = slot == truthCandidateSlot
            };
        }).ToArray();
        var truthCandidate = candidates.Single(item => item.IsTruthCandidate);
        var participantIds = Enumerable.Range(0, participantPoolSize)
            .Select(slot => string.Join(
                '-',
                "seed",
                seed.ToString("D3", CultureInfo.InvariantCulture),
                "person",
                slot.ToString("D2", CultureInfo.InvariantCulture)))
            .ToArray();
        var truthParticipantId = participantIds[truthParticipantSlot];
        var participants = participantIds.Select((participantId, slot) =>
        {
            var regionCount = Draw(
                experiment,
                seed,
                $"participant-{slot:D2}-region-count",
                definition.ParticipantRegionSetSizeMinimum,
                definition.ParticipantRegionSetSizeMaximum);
            var random = Random(experiment, seed, $"participant-{slot:D2}-regions");
            var regions = slot == truthParticipantSlot
                ? SampleIncluding(regionUniverse, truthCandidate.RegionId, regionCount, random)
                : Sample(regionUniverse, regionCount, random);
            return new GeneratedParticipantLocationEvidence
            {
                ParticipantId = participantId,
                SourceEvidenceIdentifier = $"public-location-{participantId}",
                SourceAncestryIds = [$"{participantId}-location-source-group"],
                AllowedRegionIds = regions,
                IsTruthParticipant = slot == truthParticipantSlot
            };
        }).OrderBy(item => item.ParticipantId, StringComparer.Ordinal).ToArray();
        var nonTruthParticipantId = participantIds
            .Where(item => !string.Equals(item, truthParticipantId, StringComparison.Ordinal))
            .OrderBy(item => item, StringComparer.Ordinal)
            .First();
        var exact = new[] { truthParticipantId };
        var narrow = exact.Append(nonTruthParticipantId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var broad = participantIds.OrderBy(item => item, StringComparer.Ordinal).ToArray();
        var availableAtTick = checked(encounterTick + definition.AvailabilityLagTicks);
        var overlapKind = ClassifyOverlap(participants);
        var candidateIdentity = string.Join(
            "|",
            candidates.Select(item =>
                $"{item.CandidateKey}:{item.Tick}:{item.RegionId}:{item.PublicSourceIdentifier}"));
        var participantIdentity = string.Join(
            "|",
            participants.Select(item =>
                $"{item.ParticipantId}:{string.Join("+", item.AllowedRegionIds)}:" +
                $"{item.SourceEvidenceIdentifier}:{string.Join("+", item.SourceAncestryIds)}"));
        var publicWorldId = InteractionStableIdentifier.Create(
            "ppw",
            experiment.Id,
            definition.Id,
            InteractionStableIdentifier.Seed(seed),
            InteractionStableIdentifier.Tick(encounterTick),
            InteractionStableIdentifier.Tick(availableAtTick),
            string.Join("|", regionUniverse),
            candidateIdentity,
            participantIdentity,
            string.Join("|", exact),
            string.Join("|", narrow),
            string.Join("|", broad));
        var truthWorldId = InteractionStableIdentifier.Create(
            "ptw",
            publicWorldId,
            truthParticipantId,
            truthCandidate.CandidateKey,
            truthCandidate.RegionId);

        return new GeneratedParticipantAmbiguityWorld
        {
            GeneratorId = definition.Id,
            PublicWorldId = publicWorldId,
            TruthWorldId = truthWorldId,
            Seed = seed,
            CandidateCount = candidateCount,
            RegionUniverseSize = regionUniverseSize,
            RegionUniverse = regionUniverse,
            EncounterTick = encounterTick,
            AvailableAtTick = availableAtTick,
            EvidenceAvailabilityCutoff = availableAtTick,
            ParticipantPoolSize = participantPoolSize,
            Participants = participants,
            EvidenceOverlapKind = overlapKind,
            ExactParticipantIds = exact,
            NarrowParticipantIds = narrow,
            BroadParticipantIds = broad,
            TruthParticipantId = truthParticipantId,
            TruthCandidateKey = truthCandidate.CandidateKey,
            TruthRegionId = truthCandidate.RegionId,
            CandidateStates = candidates
        };
    }

    private static IReadOnlyList<string> SampleIncluding(
        IReadOnlyList<string> universe,
        string required,
        int count,
        StableRandom random)
    {
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        return new[] { required }
            .Concat(Sample(
                universe.Where(item => !string.Equals(item, required, StringComparison.Ordinal)).ToArray(),
                count - 1,
                random))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> Sample(
        IReadOnlyList<string> universe,
        int count,
        StableRandom random)
    {
        if (count < 0 || count > universe.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        var available = universe.ToList();
        var selected = new List<string>(count);
        for (var index = 0; index < count; index++)
        {
            var selectedIndex = random.NextInclusive(0, available.Count - 1);
            selected.Add(available[selectedIndex]);
            available.RemoveAt(selectedIndex);
        }

        return selected.OrderBy(item => item, StringComparer.Ordinal).ToArray();
    }

    private static ParticipantEvidenceOverlapKind ClassifyOverlap(
        IReadOnlyList<GeneratedParticipantLocationEvidence> participants)
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
        ParticipantAmbiguityPairedExperimentDefinition experiment,
        int seed,
        string purpose,
        int minimum,
        int maximum) => Random(experiment, seed, purpose).NextInclusive(minimum, maximum);

    private static StableRandom Random(
        ParticipantAmbiguityPairedExperimentDefinition experiment,
        int seed,
        string purpose) => new(StableSeed.Derive(
            unchecked((ulong)(long)seed),
            experiment.Id,
            experiment.Generator.Id,
            purpose));
}
