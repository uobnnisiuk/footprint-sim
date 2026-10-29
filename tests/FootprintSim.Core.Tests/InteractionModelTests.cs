using System.Reflection;
using FootprintSim.Evidence;
using FootprintSim.Evaluation;
using FootprintSim.Events;
using FootprintSim.Interactions;
using FootprintSim.Interactions.Truth;
using FootprintSim.Scenarios;
using FootprintSim.Serialization;
using FootprintSim.Simulation;
using Xunit;

namespace FootprintSim.Core.Tests;

public sealed class InteractionModelTests
{
    [Fact]
    public void S0PersonEncounterProjectsToStableTruthOccurrence()
    {
        var scenario = LoadScenario("s0_walking_baseline");
        var simulation = new SimulationEngine().Run(scenario, 42);
        var encounter = Assert.Single(simulation.GroundTruthEvents, item =>
            item.Kind == EventKind.Encounter);
        var personIds = scenario.Entities.Persons.Select(person => person.Id).ToArray();

        var first = InteractionOccurrenceProjector.FromPersonPersonEncounter(
            scenario.Id,
            42,
            encounter,
            personIds);
        var second = InteractionOccurrenceProjector.FromPersonPersonEncounter(
            scenario.Id,
            42,
            encounter,
            personIds.Reverse());

        Assert.Equal(first.InteractionId, second.InteractionId);
        Assert.Equal(JsonDefaults.Serialize(first), JsonDefaults.Serialize(second));
        Assert.Equal(InteractionRelationKind.PersonPersonEncounter, first.RelationKind);
        Assert.Equal(InteractionVisibility.PrivateTruth, first.Visibility);
        Assert.Equal(3, first.OccurrenceTimeWindow.StartTick);
        Assert.Equal(3, first.OccurrenceTimeWindow.EndTick);
        Assert.Equal(["outdoor-plaza"], first.RegionIds);
        Assert.Equal(encounter.Id, first.SourceEventId);
        Assert.Null(first.CausalParentEventId);
        Assert.Equal(
            ["person-aoi", "person-ren"],
            first.Participants.Select(item => item.EntityId));
        Assert.All(first.Participants, item =>
        {
            Assert.Equal(InteractionEntityKind.Person, item.EntityKind);
            Assert.Equal(InteractionParticipantRole.EncounterParticipant, item.Role);
        });
        Assert.Contains(first.Provenance, item =>
            item.Kind == InteractionProvenanceKind.WorldEvent &&
            item.SourceIdentifier == encounter.Id);
    }

    [Fact]
    public void OccurrenceProjectorRejectsAnEncounterWithoutExactlyTwoKnownPersons()
    {
        var scenario = LoadScenario("s0_walking_baseline");
        var simulation = new SimulationEngine().Run(scenario, 42);
        var encounter = Assert.Single(simulation.GroundTruthEvents, item =>
            item.Kind == EventKind.Encounter);

        Assert.Throws<ArgumentException>(() =>
            InteractionOccurrenceProjector.FromPersonPersonEncounter(
                scenario.Id,
                42,
                encounter,
                ["person-aoi"]));
    }

    [Fact]
    public void InteractionObservationIsIndependentStableEvidenceWithOrWithoutReference()
    {
        var participants = new[]
        {
            Participant("person-b", InteractionEntityKind.Person),
            Participant("person-a", InteractionEntityKind.Person)
        };
        var provenance = new[]
        {
            new InteractionProvenance(
                InteractionProvenanceKind.ExternalSource,
                "source-z"),
            new InteractionProvenance(
                InteractionProvenanceKind.ExternalSource,
                "source-a")
        };
        var first = CreateObservation(
            participants,
            ["region-z", "region-r3", "region-r3"],
            provenance,
            referencedInteractionId: null);
        var second = CreateObservation(
            participants.Reverse(),
            ["region-r3", "region-z"],
            provenance.Reverse(),
            referencedInteractionId: null);
        var referenced = CreateObservation(
            participants,
            ["region-r3"],
            provenance,
            referencedInteractionId: "external-interaction-reference");

        Assert.Equal(first.ObservationId, second.ObservationId);
        Assert.Equal(JsonDefaults.Serialize(first), JsonDefaults.Serialize(second));
        Assert.Null(first.ReferencedInteractionId);
        Assert.Equal(
            "external-interaction-reference",
            referenced.ReferencedInteractionId);
        Assert.Equal(20, first.ObservedTimeWindow.StartTick);
        Assert.Equal(23, first.AvailableAtTick);
        Assert.Equal(EvidenceState.SupportedButUncertain, first.EvidenceBoundaryState);
        Assert.True(first.IsPersonEvidence);
        Assert.False(first.IsDeviceOnlyEvidence);
        Assert.Equal(["person-a", "person-b"], first.ObservedParticipants.Select(
            item => item.EntityId));
        Assert.Equal(["region-r3", "region-z"], first.ObservedRegionIds);
        Assert.Equal(
            [
                InteractionProvenanceKind.EvidenceRecord,
                InteractionProvenanceKind.ExternalSource,
                InteractionProvenanceKind.ExternalSource
            ],
            first.Provenance.Select(item => item.Kind));
    }

    [Fact]
    public void ConstraintDerivesFromObservationWithoutTruthReference()
    {
        var observation = CreateObservation();

        var constraint = CrossThreadConstraintDeriver.DerivePersonEncounterCoLocation(
            observation,
            "person-a");

        Assert.NotNull(constraint);
        Assert.Equal(observation.ObservationId, constraint.SourceObservationId);
        Assert.Equal("person-a", constraint.TargetEntityId);
        Assert.Equal(20, constraint.ConstrainedTimeWindow.StartTick);
        Assert.Equal(20, constraint.ConstrainedTimeWindow.EndTick);
        Assert.Equal(["region-r3"], constraint.AllowedRegionIds);
        Assert.Equal(23, constraint.AvailableAtTick);
        Assert.Equal(EvidenceState.SupportedButUncertain, constraint.EvidenceBoundaryState);
        Assert.Equal(
            CrossThreadConstraintDerivationKind.PersonEncounterCoLocation,
            constraint.DerivationKind);
        Assert.Contains(constraint.Provenance, item =>
            item.Kind == InteractionProvenanceKind.InteractionObservation &&
            item.SourceIdentifier == observation.ObservationId);
        Assert.Contains(constraint.Provenance, item =>
            item.Kind == InteractionProvenanceKind.DerivationRule &&
            item.SourceIdentifier ==
                CrossThreadConstraintDeriver.PersonEncounterCoLocationRule);
        Assert.DoesNotContain(
            typeof(CrossThreadConstraint).GetProperties(),
            property => property.PropertyType == typeof(InteractionOccurrence));
    }

    [Fact]
    public void ObservationAndConstraintAreHiddenBeforeAvailableAtTick()
    {
        var observation = CreateObservation();
        var privateObservation = CreateObservation(
            sourceEvidenceIdentifier: "private-encounter-report",
            visibility: InteractionVisibility.PrivateEvidence);
        var constraint = Assert.IsType<CrossThreadConstraint>(
            CrossThreadConstraintDeriver.DerivePersonEncounterCoLocation(
                observation,
                "person-a"));
        var privateConstraint = Assert.IsType<CrossThreadConstraint>(
            CrossThreadConstraintDeriver.DerivePersonEncounterCoLocation(
                privateObservation,
                "person-a"));
        var observations = new InteractionObservationSet(
            "fixture-scenario",
            17,
            [privateObservation, observation]);
        var constraints = new CrossThreadConstraintSet(
            "fixture-scenario",
            17,
            [privateConstraint, constraint]);

        Assert.Empty(observations.PubliclyAvailableAt(22).Observations);
        Assert.Empty(constraints.PubliclyAvailableAt(22).Constraints);
        Assert.Single(observations.PubliclyAvailableAt(23).Observations);
        Assert.Single(constraints.PubliclyAvailableAt(23).Constraints);
        Assert.Equal(
            observation.ObservationId,
            observations.PubliclyAvailableAt(23).Observations[0].ObservationId);
        Assert.Equal(
            constraint.ConstraintId,
            constraints.PubliclyAvailableAt(23).Constraints[0].ConstraintId);
    }

    [Theory]
    [InlineData(EvidenceState.Confirmed)]
    [InlineData(EvidenceState.SupportedButUncertain)]
    [InlineData(EvidenceState.Conflicting)]
    [InlineData(EvidenceState.Indeterminate)]
    [InlineData(EvidenceState.UnsupportedForQuery)]
    public void ConstraintPreservesObservationEvidenceBoundary(EvidenceState state)
    {
        var observation = CreateObservation(evidenceState: state);

        var constraint = Assert.IsType<CrossThreadConstraint>(
            CrossThreadConstraintDeriver.DerivePersonEncounterCoLocation(
                observation,
                "person-a"));

        Assert.Equal(state, constraint.EvidenceBoundaryState);
    }

    [Fact]
    public void DeviceOnlyObservationCannotDerivePersonConstraintEvenWhenConfirmed()
    {
        var observation = InteractionObservation.Create(
            "fixture-scenario",
            17,
            "device-only-source",
            referencedInteractionId: null,
            InteractionRelationKind.PersonPersonEncounter,
            [
                Participant("person-a", InteractionEntityKind.Person),
                Participant("device-b", InteractionEntityKind.Device)
            ],
            InteractionTimeWindow.At(20),
            ["region-r3"],
            availableAtTick: 23,
            EvidenceState.Confirmed,
            isPersonEvidence: false,
            isDeviceOnlyEvidence: true);

        Assert.Null(CrossThreadConstraintDeriver.DerivePersonEncounterCoLocation(
            observation,
            "person-a"));
    }

    [Fact]
    public void PublicInteractionContractsDoNotExposeTruthOccurrenceObjects()
    {
        var strategyProperties = typeof(SearchStrategyInput).GetProperties();
        Assert.Equal(8, strategyProperties.Length);
        Assert.Equal(typeof(IReadOnlyList<string>), Assert.Single(strategyProperties,
            property => property.Name == nameof(SearchStrategyInput.PublicSearchDomainRegionIds)).PropertyType);
        Assert.DoesNotContain(strategyProperties, property =>
            property.PropertyType == typeof(InteractionOccurrence) ||
            property.PropertyType == typeof(InteractionObservationSet));
        Assert.Contains(strategyProperties, property =>
            property.PropertyType == typeof(CrossThreadConstraintSet));

        Assert.DoesNotContain(
            typeof(InteractionObservation).GetProperties(),
            property => property.PropertyType == typeof(InteractionOccurrence));
        Assert.DoesNotContain(
            typeof(InteractionObservationSet).GetProperties(),
            property => property.PropertyType == typeof(InteractionOccurrence));
        Assert.DoesNotContain(
            typeof(CrossThreadConstraint).GetProperties(),
            property => property.PropertyType == typeof(InteractionOccurrence));
        AssertNoOccurrenceApi(typeof(InteractionObservation));
        AssertNoOccurrenceApi(typeof(InteractionObservationSet));
        AssertNoOccurrenceApi(typeof(CrossThreadConstraint));
        AssertNoOccurrenceApi(typeof(CrossThreadConstraintSet));
        AssertNoOccurrenceApi(typeof(CrossThreadConstraintDeriver));
        Assert.Equal(8, SearchStrategyRegistry.All.Count);
    }

    [Fact]
    public void ConstraintIdentifierAndSerializationAreStableAcrossInputOrder()
    {
        var firstObservation = CreateObservation(
            [
                Participant("person-b", InteractionEntityKind.Person),
                Participant("person-a", InteractionEntityKind.Person)
            ],
            ["region-z", "region-r3"],
            [
                new InteractionProvenance(
                    InteractionProvenanceKind.ExternalSource,
                    "source-z"),
                new InteractionProvenance(
                    InteractionProvenanceKind.ExternalSource,
                    "source-a")
            ]);
        var secondObservation = CreateObservation(
            [
                Participant("person-a", InteractionEntityKind.Person),
                Participant("person-b", InteractionEntityKind.Person)
            ],
            ["region-r3", "region-z"],
            [
                new InteractionProvenance(
                    InteractionProvenanceKind.ExternalSource,
                    "source-a"),
                new InteractionProvenance(
                    InteractionProvenanceKind.ExternalSource,
                    "source-z")
            ]);
        var first = Assert.IsType<CrossThreadConstraint>(
            CrossThreadConstraintDeriver.DerivePersonEncounterCoLocation(
                firstObservation,
                "person-a"));
        var second = Assert.IsType<CrossThreadConstraint>(
            CrossThreadConstraintDeriver.DerivePersonEncounterCoLocation(
                secondObservation,
                "person-a"));

        Assert.Equal(first.ConstraintId, second.ConstraintId);
        Assert.Equal(JsonDefaults.Serialize(first), JsonDefaults.Serialize(second));
    }

    private static InteractionObservation CreateObservation(
        IEnumerable<InteractionParticipant>? participants = null,
        IEnumerable<string>? regionIds = null,
        IEnumerable<InteractionProvenance>? provenance = null,
        string? referencedInteractionId = null,
        EvidenceState evidenceState = EvidenceState.SupportedButUncertain,
        string sourceEvidenceIdentifier = "encounter-report-001",
        InteractionVisibility visibility = InteractionVisibility.PublicEvidence)
    {
        return InteractionObservation.Create(
            "fixture-scenario",
            17,
            sourceEvidenceIdentifier,
            referencedInteractionId,
            InteractionRelationKind.PersonPersonEncounter,
            participants ??
            [
                Participant("person-a", InteractionEntityKind.Person),
                Participant("person-b", InteractionEntityKind.Person)
            ],
            InteractionTimeWindow.At(20),
            regionIds ?? ["region-r3"],
            availableAtTick: 23,
            evidenceState,
            isPersonEvidence: true,
            isDeviceOnlyEvidence: false,
            visibility: visibility,
            additionalProvenance: provenance);
    }

    private static InteractionParticipant Participant(
        string entityId,
        InteractionEntityKind kind) => new(
            entityId,
            kind,
            InteractionParticipantRole.EncounterParticipant);

    private static void AssertNoOccurrenceApi(Type type)
    {
        var methods = type.GetMethods(
            BindingFlags.Public |
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.DeclaredOnly);
        Assert.All(methods, method =>
        {
            Assert.NotEqual(typeof(InteractionOccurrence), method.ReturnType);
            Assert.DoesNotContain(method.GetParameters(), parameter =>
                parameter.ParameterType == typeof(InteractionOccurrence));
        });
    }

    private static ScenarioDefinition LoadScenario(string directory) => ScenarioLoader.Load(
        Path.Combine(RepositoryRoot, "scenarios", directory, "scenario.json"));

    private static string RepositoryRoot
    {
        get
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null &&
                !File.Exists(Path.Combine(current.FullName, "FootprintSim.sln")))
            {
                current = current.Parent;
            }

            Assert.NotNull(current);
            return current.FullName;
        }
    }
}
