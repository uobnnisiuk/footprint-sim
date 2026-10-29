using System.Globalization;
using FootprintSim.Inference.Derivation;
using FootprintSim.Interactions;
using FootprintSim.Simulation;

namespace FootprintSim.Evaluation;

public static class TimeResolutionPairedExperimentValidator
{
    public static void Validate(
        TimeResolutionPairedExperimentDefinition experiment,
        IReadOnlyList<int>? seeds = null)
    {
        ArgumentNullException.ThrowIfNull(experiment);
        RequireIdentifier(experiment.Id, nameof(experiment.Id));
        RequireIdentifier(experiment.ScenarioId, nameof(experiment.ScenarioId));
        RequireIdentifier(experiment.TargetEntityId, nameof(experiment.TargetEntityId));
        RequireIdentifier(experiment.SourceEvidenceIdentifier, nameof(experiment.SourceEvidenceIdentifier));
        RequireIdentifier(experiment.LaterEventKind, nameof(experiment.LaterEventKind));
        RequireIdentifier(experiment.PredecessorStateKind, nameof(experiment.PredecessorStateKind));
        RequireIdentifier(experiment.DependencyId, nameof(experiment.DependencyId));
        RequireIdentifier(experiment.DurationSourceId, nameof(experiment.DurationSourceId));
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

        var generator = experiment.Generator;
        RequirePositiveRange(
            generator.CandidateCountMinimum,
            generator.CandidateCountMaximum,
            "candidate count");
        RequireNonNegativeRange(
            generator.LaterEventTickMinimum,
            generator.LaterEventTickMaximum,
            "later-event tick");
        RequireNonNegativeRange(
            generator.DurationMinimumTickMinimum,
            generator.DurationMinimumTickMaximum,
            "duration minimum");
        RequireNonNegativeRange(
            generator.DurationWidthMinimum,
            generator.DurationWidthMaximum,
            "duration width");
        if (generator.CandidatePaddingOutsideBroadTicks < 0 ||
            generator.AvailabilityLagAfterBroadEndTicks < 0)
        {
            throw new InvalidDataException("Generator padding and availability lag cannot be negative.");
        }

        if (!generator.CandidateTickCollisionsAllowed)
        {
            throw new InvalidDataException(
                "Generator version m1f9-simple-predecessor-layer-v1 requires declared tick collisions.");
        }

        var profiles = experiment.Profiles.OrderBy(item => item.InformationOrder).ToArray();
        if (!profiles.Select(item => item.Id).SequenceEqual(
                ["exact", "medium", "broad", "unknown"],
                StringComparer.Ordinal) ||
            !profiles.Select(item => item.InformationOrder).SequenceEqual([0, 1, 2, 3]))
        {
            throw new InvalidDataException(
                "The registered profiles must be exact, medium, broad, unknown in orders 0..3.");
        }

        var finite = profiles[..3];
        if (finite.Any(item =>
                item.OccurrenceTimeUnknown ||
                !item.ObservedStartOffsetFromLaterTick.HasValue ||
                !item.ObservedEndOffsetFromLaterTick.HasValue ||
                item.ObservedStartOffsetFromLaterTick > item.ObservedEndOffsetFromLaterTick) ||
            !profiles[3].OccurrenceTimeUnknown ||
            profiles[3].ObservedStartOffsetFromLaterTick.HasValue ||
            profiles[3].ObservedEndOffsetFromLaterTick.HasValue)
        {
            throw new InvalidDataException("Finite and unknown profile time content is invalid.");
        }

        if (finite[0].ObservedStartOffsetFromLaterTick != 0 ||
            finite[0].ObservedEndOffsetFromLaterTick != 0)
        {
            throw new InvalidDataException("Exact must be the generated later-event point [L,L].");
        }

        for (var index = 1; index < finite.Length; index++)
        {
            if (finite[index].ObservedStartOffsetFromLaterTick >
                    finite[index - 1].ObservedStartOffsetFromLaterTick ||
                finite[index].ObservedEndOffsetFromLaterTick <
                    finite[index - 1].ObservedEndOffsetFromLaterTick)
            {
                throw new InvalidDataException("Finite observation profiles must be nested.");
            }
        }

        var broadStart = finite[2].ObservedStartOffsetFromLaterTick!.Value;
        var earliestGeneratedTick = generator.LaterEventTickMinimum + broadStart -
            generator.DurationMinimumTickMaximum - generator.DurationWidthMaximum -
            generator.CandidatePaddingOutsideBroadTicks;
        if (earliestGeneratedTick < 0)
        {
            throw new InvalidDataException("Registered ranges can generate a negative candidate tick.");
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

public sealed class TimeResolutionSeededWorldGenerator
{
    public GeneratedTimeResolutionWorld Generate(
        TimeResolutionPairedExperimentDefinition experiment,
        int seed)
    {
        TimeResolutionPairedExperimentValidator.Validate(experiment);
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
        var laterEventTick = Draw(
            experiment,
            seed,
            "later-event-tick",
            definition.LaterEventTickMinimum,
            definition.LaterEventTickMaximum);
        var durationMinimum = Draw(
            experiment,
            seed,
            "duration-minimum",
            definition.DurationMinimumTickMinimum,
            definition.DurationMinimumTickMaximum);
        var durationWidth = Draw(
            experiment,
            seed,
            "duration-width",
            definition.DurationWidthMinimum,
            definition.DurationWidthMaximum);
        var durationMaximum = checked(durationMinimum + durationWidth);
        var actualDuration = Draw(
            experiment,
            seed,
            "actual-truth-duration",
            durationMinimum,
            durationMaximum);
        var truthSlot = Draw(
            experiment,
            seed,
            "truth-slot",
            minimum: 0,
            maximum: candidateCount - 1);
        var broad = experiment.Profiles.Single(item => item.Id == "broad");
        var broadStart = checked(
            laterEventTick + broad.ObservedStartOffsetFromLaterTick!.Value - durationMaximum);
        var broadEnd = checked(
            laterEventTick + broad.ObservedEndOffsetFromLaterTick!.Value - durationMinimum);
        var candidateTickMinimum = checked(
            broadStart - definition.CandidatePaddingOutsideBroadTicks);
        var candidateTickMaximum = checked(
            broadEnd + definition.CandidatePaddingOutsideBroadTicks);
        var truthTick = checked(laterEventTick - actualDuration);
        var tickRandom = Random(experiment, seed, "candidate-ticks");
        var candidateStates = Enumerable.Range(0, candidateCount).Select(slot =>
        {
            var key = $"candidate-{slot.ToString("D2", CultureInfo.InvariantCulture)}";
            var region = string.Join(
                '-',
                "seed",
                seed.ToString("D3", CultureInfo.InvariantCulture),
                "candidate",
                slot.ToString("D2", CultureInfo.InvariantCulture));
            return new GeneratedTimeResolutionCandidateState
            {
                CandidateKey = key,
                Tick = slot == truthSlot
                    ? truthTick
                    : tickRandom.NextInclusive(
                        checked((int)candidateTickMinimum),
                        checked((int)candidateTickMaximum)),
                RegionId = region,
                IsTruthCandidate = slot == truthSlot
            };
        }).ToArray();
        var truthCandidate = candidateStates.Single(item => item.IsTruthCandidate);
        var broadObservationEnd = checked(
            laterEventTick + broad.ObservedEndOffsetFromLaterTick.Value);
        var availableAtTick = checked(
            broadObservationEnd + definition.AvailabilityLagAfterBroadEndTicks);
        var durationBounds = new PublicDurationBound(durationMinimum, durationMaximum);
        var candidateIdentity = string.Join(
            "|",
            candidateStates.Select(item =>
                $"{item.CandidateKey}:{item.Tick}:{item.RegionId}"));
        var publicWorldId = InteractionStableIdentifier.Create(
            "tpw",
            experiment.Id,
            definition.Id,
            InteractionStableIdentifier.Seed(seed),
            InteractionStableIdentifier.Tick(laterEventTick),
            InteractionStableIdentifier.Tick(durationMinimum),
            InteractionStableIdentifier.Tick(durationMaximum),
            InteractionStableIdentifier.Tick(candidateTickMinimum),
            InteractionStableIdentifier.Tick(candidateTickMaximum),
            InteractionStableIdentifier.Tick(availableAtTick),
            candidateIdentity);
        var truthWorldId = InteractionStableIdentifier.Create(
            "ttw",
            publicWorldId,
            truthCandidate.CandidateKey,
            InteractionStableIdentifier.Tick(truthTick),
            InteractionStableIdentifier.Tick(actualDuration));

        return new GeneratedTimeResolutionWorld
        {
            GeneratorId = definition.Id,
            PublicWorldId = publicWorldId,
            TruthWorldId = truthWorldId,
            Seed = seed,
            CandidateCount = candidateCount,
            LaterEventTick = laterEventTick,
            DurationBounds = durationBounds,
            DurationWidthTicks = durationWidth,
            ActualTruthDurationTicks = actualDuration,
            CandidateTickMinimum = candidateTickMinimum,
            CandidateTickMaximum = candidateTickMaximum,
            AvailableAtTick = availableAtTick,
            EvidenceAvailabilityCutoff = availableAtTick,
            TruthCandidateKey = truthCandidate.CandidateKey,
            TruthRegionId = truthCandidate.RegionId,
            TruthTick = truthTick,
            CandidateStates = candidateStates
        };
    }

    private static int Draw(
        TimeResolutionPairedExperimentDefinition experiment,
        int seed,
        string purpose,
        int minimum,
        int maximum) => Random(experiment, seed, purpose).NextInclusive(minimum, maximum);

    private static StableRandom Random(
        TimeResolutionPairedExperimentDefinition experiment,
        int seed,
        string purpose) => new(StableSeed.Derive(
            unchecked((ulong)(long)seed),
            experiment.Id,
            experiment.Generator.Id,
            purpose));
}
