using System.Text.Json;
using System.Text.Json.Serialization;
using FootprintSim.Evidence;
using FootprintSim.Inference.Derivation;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;
using FootprintSim.Serialization;

namespace FootprintSim.Evaluation;

public sealed record ParticipantAmbiguityPairedExperimentDefinition
{
    public int SchemaVersion { get; init; } = 1;

    public required string Id { get; init; }

    public required string ScenarioId { get; init; }

    public required int RequiredSeedStart { get; init; }

    public required int RequiredSeedEnd { get; init; }

    public required string TargetEntityId { get; init; }

    public required InteractionEntityKind TargetEntityKind { get; init; }

    public required string EncounterSourceEvidenceIdentifier { get; init; }

    public required InteractionRelationKind RelationKind { get; init; }

    public required EvidenceState EncounterEvidenceState { get; init; }

    public IReadOnlyList<string> EncounterSourceAncestryIds { get; init; } = [];

    public required ParticipantAmbiguityWorldGeneratorDefinition Generator { get; init; }

    public IReadOnlyList<ParticipantAmbiguityPairedProfileDefinition> Profiles { get; init; } = [];
}

public sealed record ParticipantAmbiguityWorldGeneratorDefinition
{
    public required string Id { get; init; }

    public required int CandidateCountMinimum { get; init; }

    public required int CandidateCountMaximum { get; init; }

    public required int RegionUniverseSizeMinimum { get; init; }

    public required int RegionUniverseSizeMaximum { get; init; }

    public required int ParticipantPoolSizeMinimum { get; init; }

    public required int ParticipantPoolSizeMaximum { get; init; }

    public required int ParticipantRegionSetSizeMinimum { get; init; }

    public required int ParticipantRegionSetSizeMaximum { get; init; }

    public required int EncounterTickMinimum { get; init; }

    public required int EncounterTickMaximum { get; init; }

    public required int AvailabilityLagTicks { get; init; }

    public required bool CandidateTicksEqualEncounterTick { get; init; }

    public required bool CandidateRegionCollisionsAllowed { get; init; }

    public required bool ParticipantRegionsUniqueWithinParticipant { get; init; }

    public required bool ParticipantRegionSamplingIndependentAcrossParticipants { get; init; }

    public required bool TruthParticipantEvidenceIncludesTruthCandidateRegion { get; init; }

    public required bool MissingEvidenceSecondaryConditionEnabled { get; init; }

    public required string InvalidWorldPolicy { get; init; }
}

public sealed record ParticipantAmbiguityPairedProfileDefinition
{
    public required string Id { get; init; }

    public required int InformationOrder { get; init; }

    public int? ParticipantCount { get; init; }

    public required bool UsesFullParticipantPool { get; init; }

    public required bool IdentityUnknown { get; init; }

    public required string SelectionRule { get; init; }

    public required string Rationale { get; init; }
}

public enum ParticipantEvidenceOverlapKind
{
    Identical,
    CompletelyDistinct,
    PartiallyOverlapping
}

public enum ParticipantPropagationClassification
{
    ParticipantToRegionPlateau,
    RegionToCandidatePlateau,
    CandidateExpansion,
    UnknownBaselineTransition
}

public sealed record GeneratedParticipantAmbiguityCandidate
{
    public required string CandidateKey { get; init; }

    public required string PublicSourceIdentifier { get; init; }

    public required long Tick { get; init; }

    public required string RegionId { get; init; }

    public required bool IsTruthCandidate { get; init; }
}

public sealed record GeneratedParticipantLocationEvidence
{
    public required string ParticipantId { get; init; }

    public required string SourceEvidenceIdentifier { get; init; }

    public IReadOnlyList<string> SourceAncestryIds { get; init; } = [];

    public IReadOnlyList<string> AllowedRegionIds { get; init; } = [];

    public required bool IsTruthParticipant { get; init; }
}

public sealed record GeneratedParticipantAmbiguityWorld
{
    public required string GeneratorId { get; init; }

    public required string PublicWorldId { get; init; }

    public required string TruthWorldId { get; init; }

    public required int Seed { get; init; }

    public required int CandidateCount { get; init; }

    public required int RegionUniverseSize { get; init; }

    public IReadOnlyList<string> RegionUniverse { get; init; } = [];

    public required long EncounterTick { get; init; }

    public required long AvailableAtTick { get; init; }

    public required long EvidenceAvailabilityCutoff { get; init; }

    public required int ParticipantPoolSize { get; init; }

    public IReadOnlyList<GeneratedParticipantLocationEvidence> Participants { get; init; } = [];

    public required ParticipantEvidenceOverlapKind EvidenceOverlapKind { get; init; }

    public IReadOnlyList<string> ExactParticipantIds { get; init; } = [];

    public IReadOnlyList<string> NarrowParticipantIds { get; init; } = [];

    public IReadOnlyList<string> BroadParticipantIds { get; init; } = [];

    public required string TruthParticipantId { get; init; }

    public required string TruthCandidateKey { get; init; }

    public required string TruthRegionId { get; init; }

    public IReadOnlyList<GeneratedParticipantAmbiguityCandidate> CandidateStates { get; init; } = [];
}

public sealed record ParticipantAmbiguityPairedEvaluationRequest
{
    public required ParticipantAmbiguityPairedExperimentDefinition Experiment { get; init; }

    public IReadOnlyList<int> Seeds { get; init; } = [];

    public required string ExperimentContentSha256 { get; init; }

    public required string SourceCommitSha { get; init; }

    public required bool DirtyWorkingTree { get; init; }
}

public sealed record ParticipantAmbiguityPairedCandidateRun
{
    public required string CandidateKey { get; init; }

    public required string CandidateId { get; init; }

    public required string StateId { get; init; }

    public required long Tick { get; init; }

    public required string RegionId { get; init; }

    public required string PublicSourceIdentifier { get; init; }

    public required bool IsTruthCandidate { get; init; }
}

public sealed record ParticipantAmbiguityPairedEvidenceRun
{
    public required string ParticipantId { get; init; }

    public required string PublicInputId { get; init; }

    public required string SourceEvidenceIdentifier { get; init; }

    public required string SourceGroupId { get; init; }

    public IReadOnlyList<string> SourceAncestryIds { get; init; } = [];

    public IReadOnlyList<string> AllowedRegionIds { get; init; } = [];

    public required bool IsTruthParticipant { get; init; }
}

public sealed record ParticipantAmbiguityPairedProfileRun
{
    public required string ProfileId { get; init; }

    public required int InformationOrder { get; init; }

    public required bool EncounterObservationPresent { get; init; }

    public required string SourceObservationId { get; init; }

    public required string EncounterSourceGroupId { get; init; }

    public required ParticipantIdentityKnowledge IdentityKnowledge { get; init; }

    public IReadOnlyList<string> PossibleParticipantIds { get; init; } = [];

    public required int ParticipantAlternativeCount { get; init; }

    public required bool DerivationSuccess { get; init; }

    public required ParticipantConstraintDerivationFailureReason FailureReason { get; init; }

    public required string RuleId { get; init; }

    public required PastStateEvidenceUsageRole UsageRole { get; init; }

    public required bool ConstraintGenerated { get; init; }

    public string? ConstraintId { get; init; }

    public IReadOnlyList<string> DerivedAllowedRegionIds { get; init; } = [];

    public required int DerivedRegionCount { get; init; }

    public IReadOnlyList<string> PublicInputIdsUsed { get; init; } = [];

    public IReadOnlyList<string> DerivedConstraintAncestryIds { get; init; } = [];

    public IReadOnlyList<string> ParticipantEvidenceSourceGroupIds { get; init; } = [];

    public required int EncounterSourceGroupCount { get; init; }

    public required int ParticipantEvidenceSourceGroupCount { get; init; }

    public required int GeneratedConstraintSourceGroupCount { get; init; }

    public required int ParticipantAlternativesEvaluated { get; init; }

    public required int ParticipantLocationEvidenceRecordsInspected { get; init; }

    public IReadOnlyList<string> CandidateIds { get; init; } = [];

    public IReadOnlyList<string> CandidateRegionIds { get; init; } = [];

    public required int CandidateCount { get; init; }

    public required bool ContainsTruth { get; init; }

    public required bool FalseExclusion { get; init; }

    public required int CandidateReductionCountVsUnknown { get; init; }

    public required decimal CandidateReductionFractionVsUnknown { get; init; }

    public required int CandidateGenerationWork { get; init; }

    public required int ReconstructionEvidenceRecordsInspected { get; init; }

    public required int ReconstructionSourceGroupsInspected { get; init; }
}

public sealed record ParticipantAmbiguityPairedMonotonicityAudit
{
    public required int Seed { get; init; }

    public required string MorePreciseProfileId { get; init; }

    public required string CoarserProfileId { get; init; }

    public IReadOnlyList<string> MorePreciseParticipantIds { get; init; } = [];

    public IReadOnlyList<string> CoarserParticipantIds { get; init; } = [];

    public required bool ParticipantSetIsSubset { get; init; }

    public required bool ParticipantSetStrictlyWidened { get; init; }

    public IReadOnlyList<string> MorePreciseRegionIds { get; init; } = [];

    public IReadOnlyList<string> CoarserRegionIds { get; init; } = [];

    public required bool DerivedRegionSetIsSubset { get; init; }

    public required bool DerivedRegionSetsEqual { get; init; }

    public required bool DerivedRegionSetStrictlyWidened { get; init; }

    public IReadOnlyList<string> MorePreciseCandidateIds { get; init; } = [];

    public IReadOnlyList<string> CoarserCandidateIds { get; init; } = [];

    public required bool CandidateSetIsSubset { get; init; }

    public required bool CandidateSetsEqual { get; init; }

    public required ParticipantPropagationClassification Classification { get; init; }
}

public sealed record ParticipantAmbiguityPairedWorldRun
{
    public required int Seed { get; init; }

    public required string GeneratorId { get; init; }

    public required string PublicWorldId { get; init; }

    public required string TruthWorldId { get; init; }

    public required int CandidateCount { get; init; }

    public required int RegionUniverseSize { get; init; }

    public IReadOnlyList<string> RegionUniverse { get; init; } = [];

    public required long EncounterTick { get; init; }

    public required long AvailableAtTick { get; init; }

    public required int ParticipantPoolSize { get; init; }

    public required ParticipantEvidenceOverlapKind EvidenceOverlapKind { get; init; }

    public required string TruthParticipantId { get; init; }

    public required string TruthCandidateKey { get; init; }

    public required string TruthCandidateId { get; init; }

    public required string TruthRegionId { get; init; }

    public IReadOnlyList<ParticipantAmbiguityPairedCandidateRun> CandidateStates { get; init; } = [];

    public IReadOnlyList<ParticipantAmbiguityPairedEvidenceRun> ParticipantEvidence { get; init; } = [];

    public IReadOnlyList<ParticipantAmbiguityPairedProfileRun> Profiles { get; init; } = [];

    public IReadOnlyList<ParticipantAmbiguityPairedMonotonicityAudit> MonotonicityAudits { get; init; } = [];

    public required bool TruthEvaluatedAfterReconstruction { get; init; }

    public required bool WorldValid { get; init; }
}

public sealed record ParticipantAmbiguityPairedProfileAggregate
{
    public required string ProfileId { get; init; }

    public required int InformationOrder { get; init; }

    public required int SeedCount { get; init; }

    public required decimal MeanCandidateCount { get; init; }

    public required decimal MedianCandidateCount { get; init; }

    public required int MinimumCandidateCount { get; init; }

    public required int MaximumCandidateCount { get; init; }

    public required decimal MeanDerivedRegionCount { get; init; }

    public required decimal MedianDerivedRegionCount { get; init; }

    public required decimal ContainmentRate { get; init; }

    public required int FalseExclusionCount { get; init; }

    public required decimal MeanCandidateReductionCountVsUnknown { get; init; }

    public required decimal MeanCandidateReductionFractionVsUnknown { get; init; }

    public required decimal MeanParticipantAlternativeCount { get; init; }

    public required decimal MeanParticipantAlternativesEvaluated { get; init; }

    public required int DerivationSuccessCount { get; init; }

    public required int DerivationFailureCount { get; init; }

    public required decimal MeanCandidateGenerationWork { get; init; }

    public required decimal MeanParticipantLocationEvidenceRecordsInspected { get; init; }

    public required decimal MeanEncounterSourceGroupCount { get; init; }

    public required decimal MeanParticipantEvidenceSourceGroupCount { get; init; }

    public required decimal MeanGeneratedConstraintSourceGroupCount { get; init; }
}

public sealed record ParticipantOverlapFrequency
{
    public required ParticipantEvidenceOverlapKind Kind { get; init; }

    public required int WorldCount { get; init; }
}

public sealed record ParticipantPropagationFrequency
{
    public required ParticipantPropagationClassification Classification { get; init; }

    public required int TransitionCount { get; init; }
}

public sealed record ParticipantAmbiguityPairedSummary
{
    public required int WorldCount { get; init; }

    public required int PairedProfileRunCount { get; init; }

    public required int FiniteTransitionCount { get; init; }

    public required int DerivedRegionMonotonicityViolationCount { get; init; }

    public required int CandidateSetMonotonicityViolationCount { get; init; }

    public required int FiniteDerivationFailureCount { get; init; }

    public required int FalseExclusionCount { get; init; }

    public required int SourceIndependenceViolationCount { get; init; }

    public required int UnknownEncounterAbsenceViolationCount { get; init; }

    public required int ExactLessThanNarrowSeedCount { get; init; }

    public required int NarrowLessThanBroadSeedCount { get; init; }

    public required int BroadLessThanUnknownSeedCount { get; init; }

    public required int FiniteCandidateSetsAllEqualSeedCount { get; init; }

    public required int ParticipantWidenedRegionUnchangedSeedCount { get; init; }

    public required int ParticipantWidenedRegionUnchangedTransitionCount { get; init; }

    public required int RegionWidenedCandidateUnchangedSeedCount { get; init; }

    public required int RegionWidenedCandidateUnchangedTransitionCount { get; init; }

    public required int CandidateExpansionTransitionCount { get; init; }

    public required int BroadUnknownSaturationSeedCount { get; init; }

    public required int SkippedSeedCount { get; init; }

    public required int RegeneratedSeedCount { get; init; }

    public IReadOnlyList<int> FiniteCandidateSetsAllEqualSeeds { get; init; } = [];

    public IReadOnlyList<int> ParticipantWidenedRegionUnchangedSeeds { get; init; } = [];

    public IReadOnlyList<int> RegionWidenedCandidateUnchangedSeeds { get; init; } = [];

    public IReadOnlyList<int> BroadUnknownSaturationSeeds { get; init; } = [];

    public IReadOnlyList<ParticipantOverlapFrequency> OverlapFrequencies { get; init; } = [];

    public IReadOnlyList<ParticipantPropagationFrequency> PropagationFrequencies { get; init; } = [];
}

public sealed record ParticipantAmbiguityPairedManifest
{
    public int OutputSchemaVersion { get; init; } = 1;

    public required string ExperimentId { get; init; }

    public required string ScenarioId { get; init; }

    public required string GeneratorId { get; init; }

    public required string RuleId { get; init; }

    public required string ExperimentContentSha256 { get; init; }

    public required string SourceCommitSha { get; init; }

    public required bool DirtyWorkingTree { get; init; }

    public IReadOnlyList<int> Seeds { get; init; } = [];

    public IReadOnlyList<string> ProfileIds { get; init; } = [];

    public required int WorldCount { get; init; }

    public required int PairedProfileRunCount { get; init; }

    public required bool TruthUsedForDerivation { get; init; }

    public IReadOnlyList<string> OutputFiles { get; init; } = [];
}

public sealed record ParticipantAmbiguityPairedEvaluationResult
{
    public required ParticipantAmbiguityPairedManifest Manifest { get; init; }

    public required string ConservativeUnionFormula { get; init; }

    public required ParticipantAmbiguityWorldGeneratorDefinition Generator { get; init; }

    public IReadOnlyList<ParticipantAmbiguityPairedProfileDefinition> Profiles { get; init; } = [];

    public IReadOnlyList<ParticipantAmbiguityPairedWorldRun> Runs { get; init; } = [];

    public IReadOnlyList<ParticipantAmbiguityPairedProfileAggregate> Aggregates { get; init; } = [];

    public required ParticipantAmbiguityPairedSummary Summary { get; init; }

    public required bool OriginalF10FixtureRetained { get; init; }

    public required bool F8F9RegressionsRetained { get; init; }

    public required bool ExistingS5Changed { get; init; }

    public required string InterpretationBoundary { get; init; }
}

public static class ParticipantAmbiguityPairedExperimentLoader
{
    public static ParticipantAmbiguityPairedExperimentDefinition Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var options = new JsonSerializerOptions(JsonDefaults.Options)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        var definition = JsonSerializer.Deserialize<ParticipantAmbiguityPairedExperimentDefinition>(
            File.ReadAllText(path),
            options) ?? throw new InvalidDataException($"Experiment '{path}' was empty.");
        if (definition.SchemaVersion != 1)
        {
            throw new InvalidDataException(
                $"Experiment '{definition.Id}' has unsupported schema version {definition.SchemaVersion}.");
        }

        return definition;
    }
}
