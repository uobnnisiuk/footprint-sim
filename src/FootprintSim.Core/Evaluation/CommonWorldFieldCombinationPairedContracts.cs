using System.Text.Json;
using System.Text.Json.Serialization;
using FootprintSim.Interactions;
using FootprintSim.Serialization;

namespace FootprintSim.Evaluation;

public sealed record CommonWorldFieldCombinationExperimentDefinition
{
    public int SchemaVersion { get; init; } = 1;

    public required string Id { get; init; }

    public required string ScenarioId { get; init; }

    public required int RequiredSeedStart { get; init; }

    public required int RequiredSeedEnd { get; init; }

    public required string TargetEntityId { get; init; }

    public required InteractionEntityKind TargetEntityKind { get; init; }

    public required string TargetStateContextId { get; init; }

    public required string CandidateDomainId { get; init; }

    public required InteractionRelationKind FixedRelationKind { get; init; }

    public required CommonWorldFieldCombinationGeneratorDefinition Generator { get; init; }

    public IReadOnlyList<CommonWorldProfileDefinition> Profiles { get; init; } = [];

    public IReadOnlyList<CommonWorldSetInclusionDefinition> PrimarySetInclusions { get; init; } = [];

    public IReadOnlyList<CommonWorldConditionalComparisonDefinition> ConditionalComparisons { get; init; } = [];

    public IReadOnlyList<string> FailureConditions { get; init; } = [];
}

public sealed record CommonWorldFieldCombinationGeneratorDefinition
{
    public required string Id { get; init; }

    public required int CandidateCountMinimum { get; init; }

    public required int CandidateCountMaximum { get; init; }

    public required int RegionUniverseSizeMinimum { get; init; }

    public required int RegionUniverseSizeMaximum { get; init; }

    public required int CandidateBaseTickMinimum { get; init; }

    public required int CandidateBaseTickMaximum { get; init; }

    public required int CandidateStartOffsetMinimum { get; init; }

    public required int CandidateStartOffsetMaximum { get; init; }

    public required int CandidateIntervalWidthMinimum { get; init; }

    public required int CandidateIntervalWidthMaximum { get; init; }

    public required int LocationObservedRegionCountMinimum { get; init; }

    public required int LocationObservedRegionCountMaximum { get; init; }

    public required int DurationMinimumTickMinimum { get; init; }

    public required int DurationMinimumTickMaximum { get; init; }

    public required int DurationWidthMinimum { get; init; }

    public required int DurationWidthMaximum { get; init; }

    public required int ParticipantPoolSizeMinimum { get; init; }

    public required int ParticipantPoolSizeMaximum { get; init; }

    public required int ParticipantRegionSetSizeMinimum { get; init; }

    public required int ParticipantRegionSetSizeMaximum { get; init; }

    public required int AvailabilityLagTicks { get; init; }

    public required bool CandidateStartRegionPairsSampledWithoutReplacement { get; init; }

    public required bool CandidateTickCollisionsAllowed { get; init; }

    public required bool CandidateRegionCollisionsAllowed { get; init; }

    public required bool UnusedRegionsAllowed { get; init; }

    public required bool TruthSelectedAfterCandidateUniverse { get; init; }

    public required bool TruthCompatibleLocationGenerated { get; init; }

    public required bool TruthCompatibleDurationGenerated { get; init; }

    public required bool TruthParticipantEvidenceIncludesTruthRegion { get; init; }

    public required bool ParticipantRegionSamplingIndependentAcrossParticipants { get; init; }

    public required bool MissingEvidenceEnabled { get; init; }

    public required bool ConflictEnabled { get; init; }

    public required string InvalidWorldPolicy { get; init; }

    public required string SeedStrategy { get; init; }
}

public sealed record CommonWorldSetInclusionDefinition
{
    public required string SubsetProfileId { get; init; }

    public required string SupersetProfileId { get; init; }
}

public sealed record CommonWorldConditionalComparisonDefinition
{
    public required string Id { get; init; }

    public required string BaselineProfileId { get; init; }

    public required string CombinedProfileId { get; init; }

    public required CommonWorldObservationAxis AddedAxis { get; init; }
}

public sealed record CommonWorldFieldCombinationEvaluationRequest
{
    public required CommonWorldFieldCombinationExperimentDefinition Experiment { get; init; }

    public IReadOnlyList<int> Seeds { get; init; } = [];

    public required string ExperimentContentSha256 { get; init; }

    public required string SourceCommitSha { get; init; }

    public required bool DirtyWorkingTree { get; init; }
}

public sealed record GeneratedCommonWorldCandidate
{
    public required string CandidateKey { get; init; }

    public required string PublicSourceIdentifier { get; init; }

    public required long StartTick { get; init; }

    public required long EndTick { get; init; }

    public required string RegionId { get; init; }

    public required bool IsTruthCandidate { get; init; }
}

public sealed record GeneratedCommonWorldParticipantEvidence
{
    public required string ParticipantId { get; init; }

    public required string SourceEvidenceIdentifier { get; init; }

    public required string SourceAncestryId { get; init; }

    public IReadOnlyList<string> AllowedRegionIds { get; init; } = [];

    public required bool IsTruthParticipant { get; init; }
}

public sealed record GeneratedCommonFieldCombinationWorld
{
    public required string GeneratorId { get; init; }

    public required string PublicWorldId { get; init; }

    public required string TruthWorldId { get; init; }

    public required int Seed { get; init; }

    public IReadOnlyList<string> RegionUniverse { get; init; } = [];

    public required int UnusedRegionCount { get; init; }

    public required int CandidateCount { get; init; }

    public required int CandidateBaseTick { get; init; }

    public IReadOnlyList<GeneratedCommonWorldCandidate> CandidateStates { get; init; } = [];

    public required string TruthCandidateKey { get; init; }

    public required long TruthCandidateStartTick { get; init; }

    public required long TruthCandidateEndTick { get; init; }

    public required string TruthRegionId { get; init; }

    public IReadOnlyList<string> LocationObservedRegionIds { get; init; } = [];

    public required string LocationObservationSourceId { get; init; }

    public required string LocationSourceAncestryId { get; init; }

    public required long EncounterTick { get; init; }

    public required string EncounterObservationSourceId { get; init; }

    public required string EncounterSourceAncestryId { get; init; }

    public required long LaterEventTick { get; init; }

    public required string LaterEventObservationSourceId { get; init; }

    public required string LaterEventSourceAncestryId { get; init; }

    public required long DurationMinimumTicks { get; init; }

    public required long DurationMaximumTicks { get; init; }

    public required long ActualTruthDurationTicks { get; init; }

    public required string DependencyId { get; init; }

    public required string DurationSourceId { get; init; }

    public required string DurationSourceGroupId { get; init; }

    public IReadOnlyList<GeneratedCommonWorldParticipantEvidence> Participants { get; init; } = [];

    public required ParticipantEvidenceOverlapKind ParticipantEvidenceOverlapKind { get; init; }

    public IReadOnlyList<string> ObservedPossibleParticipantIds { get; init; } = [];

    public required string TruthParticipantId { get; init; }

    public required long EvidenceAvailabilityCutoff { get; init; }
}

public enum CommonWorldConditionalOutcomeClassification
{
    AdditionalCandidateReduction,
    ObservationToConstraintPlateau,
    ConstraintOverlapPlateau,
    ConstraintToCandidatePlateau,
    Saturation
}

public sealed record CommonWorldSetInclusionAudit
{
    public required int Seed { get; init; }

    public required string SubsetProfileId { get; init; }

    public required string SupersetProfileId { get; init; }

    public IReadOnlyList<string> SubsetCandidateIds { get; init; } = [];

    public IReadOnlyList<string> SupersetCandidateIds { get; init; } = [];

    public required bool CandidateSetIsSubset { get; init; }
}

public sealed record CommonWorldConditionalReductionAudit
{
    public required int Seed { get; init; }

    public required string ComparisonId { get; init; }

    public required string BaselineProfileId { get; init; }

    public required string CombinedProfileId { get; init; }

    public required CommonWorldObservationAxis AddedAxis { get; init; }

    public required int BaselineCandidateCount { get; init; }

    public required int CombinedCandidateCount { get; init; }

    public required int AdditionalCandidateReduction { get; init; }

    public required bool CandidateSetsEqual { get; init; }

    public required bool AddedPredicateIsTopForCandidateDomain { get; init; }

    public required bool AddedPredicateImpliedByBaselinePredicates { get; init; }

    public required CommonWorldConditionalOutcomeClassification Classification { get; init; }
}

public sealed record CommonWorldProfileEquivalenceAudit
{
    public required int Seed { get; init; }

    public required string LeftProfileId { get; init; }

    public required string RightProfileId { get; init; }

    public required bool CandidateSetsEqual { get; init; }
}

public sealed record CommonWorldMinimumCandidateSetGroup
{
    public required string CandidateSetId { get; init; }

    public IReadOnlyList<string> CandidateIds { get; init; } = [];

    public IReadOnlyList<string> ProfileIds { get; init; } = [];
}

public sealed record CommonWorldPerWorldMinimumAudit
{
    public required int Seed { get; init; }

    public required int MinimumCandidateCount { get; init; }

    public IReadOnlyList<string> ProfilesAtMinimumCount { get; init; } = [];

    public IReadOnlyList<string> InclusionMinimalProfileIds { get; init; } = [];

    public IReadOnlyList<CommonWorldMinimumCandidateSetGroup> CandidateSetGroups { get; init; } = [];

    public required bool AllMinimumProfilesReturnSameCandidateSet { get; init; }
}

public sealed record CommonWorldOutcomeFlags
{
    public required bool AllThreeSingleAxisCandidateSetsDiffer { get; init; }

    public required bool AFieldSingleAxisSetIsSubsetOfBothOthers { get; init; }

    public required bool AnyTwoAxisPlateau { get; init; }

    public required bool AnyThirdAxisAdditionalReduction { get; init; }

    public required bool FullProfileSaturatedAtOneCandidate { get; init; }
}

public sealed record CommonWorldFieldCombinationWorldRun
{
    public required int Seed { get; init; }

    public required GeneratedCommonFieldCombinationWorld World { get; init; }

    public required string CandidateUniverseId { get; init; }

    public required string TruthCandidateId { get; init; }

    public IReadOnlyList<CommonWorldSourceGraphNode> SourceGraph { get; init; } = [];

    public required CommonWorldDerivationAudit DerivationAudit { get; init; }

    public IReadOnlyList<string> DerivedLocationAllowedRegionIds { get; init; } = [];

    public required InteractionTimeWindow DerivedHistoricalTimeWindow { get; init; }

    public IReadOnlyList<string> DerivedParticipantAllowedRegionIds { get; init; } = [];

    public IReadOnlyList<CommonWorldProfileRun> Profiles { get; init; } = [];

    public IReadOnlyList<CommonWorldSetInclusionAudit> SetInclusionAudits { get; init; } = [];

    public IReadOnlyList<CommonWorldConditionalReductionAudit> ConditionalReductions { get; init; } = [];

    public IReadOnlyList<CommonWorldProfileEquivalenceAudit> ProfileEquivalences { get; init; } = [];

    public required CommonWorldPerWorldMinimumAudit MinimumAudit { get; init; }

    public required CommonWorldOutcomeFlags OutcomeFlags { get; init; }

    public required bool AxisOrthogonalityPreserved { get; init; }

    public required bool CandidateUniverseInvariant { get; init; }

    public required bool RelationInvariant { get; init; }

    public required bool WorldValid { get; init; }
}

public sealed record CommonWorldFieldCombinationProfileAggregate
{
    public required string ProfileId { get; init; }

    public required int Order { get; init; }

    public required int SeedCount { get; init; }

    public required decimal MeanCandidateCount { get; init; }

    public required decimal MedianCandidateCount { get; init; }

    public required int MinimumCandidateCount { get; init; }

    public required int MaximumCandidateCount { get; init; }

    public required decimal ContainmentRate { get; init; }

    public required int FalseExclusionCount { get; init; }

    public required decimal MeanReductionCountVsNone { get; init; }

    public required decimal MeanReductionFractionVsNone { get; init; }

    public required decimal MeanCandidateGenerationWork { get; init; }

    public required decimal MeanComponentsReceived { get; init; }

    public required decimal MeanPredicatesApplied { get; init; }

    public required decimal MeanCandidatePredicateChecks { get; init; }

    public required decimal MeanSupportingSourceGroupCount { get; init; }

    public required decimal MeanSelectedFieldCount { get; init; }

    public required decimal MeanParticipantAlternativesEvaluatedWhenSelected { get; init; }

    public required int DuplicateConstraintsSuppressed { get; init; }

    public required int AlternativeBranchesEvaluated { get; init; }
}

public sealed record CommonWorldConditionalReductionAggregate
{
    public required string ComparisonId { get; init; }

    public required string BaselineProfileId { get; init; }

    public required string CombinedProfileId { get; init; }

    public required CommonWorldObservationAxis AddedAxis { get; init; }

    public required decimal MeanAdditionalCandidateReduction { get; init; }

    public required decimal MedianAdditionalCandidateReduction { get; init; }

    public required int MinimumAdditionalCandidateReduction { get; init; }

    public required int MaximumAdditionalCandidateReduction { get; init; }

    public required int PositiveAdditionalEffectWorldCount { get; init; }

    public required int ZeroAdditionalEffectWorldCount { get; init; }

    public IReadOnlyList<CommonWorldConditionalClassificationFrequency> ClassificationFrequencies { get; init; } = [];
}

public sealed record CommonWorldConditionalClassificationFrequency
{
    public required CommonWorldConditionalOutcomeClassification Classification { get; init; }

    public required int WorldCount { get; init; }
}

public sealed record CommonWorldProfileEquivalenceAggregate
{
    public required string LeftProfileId { get; init; }

    public required string RightProfileId { get; init; }

    public required int EqualCandidateSetWorldCount { get; init; }
}

public sealed record CommonWorldMinimumProfileFrequency
{
    public required string ProfileId { get; init; }

    public required int MinimumCountWorldCount { get; init; }

    public required int InclusionMinimalWorldCount { get; init; }
}

public sealed record CommonWorldFieldCombinationRepresentationSummary
{
    public required int RealizedCandidateCountMinimum { get; init; }

    public required int RealizedCandidateCountMaximum { get; init; }

    public required int RealizedRegionUniverseMinimum { get; init; }

    public required int RealizedRegionUniverseMaximum { get; init; }

    public required int WorldsWithUnusedRegions { get; init; }

    public required int WorldsWithSameRegionCandidates { get; init; }

    public required int WorldsWithSameStartTickCandidates { get; init; }

    public required int WorldsWithStateIntervals { get; init; }

    public required int WorldsWithIdenticalParticipantEvidence { get; init; }

    public required int WorldsWithPartialParticipantEvidenceOverlap { get; init; }

    public required int WorldsWithDisjointParticipantEvidence { get; init; }
}

public sealed record CommonWorldFieldCombinationSummary
{
    public required int WorldCount { get; init; }

    public required int ProfileEvaluationCount { get; init; }

    public required int SetInclusionAuditCount { get; init; }

    public required int JointSetInclusionViolationCount { get; init; }

    public required int FalseExclusionCount { get; init; }

    public required int AxisOrthogonalityViolationCount { get; init; }

    public required int CandidateUniverseInvarianceViolationCount { get; init; }

    public required int RelationInvarianceViolationCount { get; init; }

    public required int SkippedSeedCount { get; init; }

    public required int RegeneratedSeedCount { get; init; }

    public required int AllThreeSinglesDifferWorldCount { get; init; }

    public required int SingleAxisDominanceWorldCount { get; init; }

    public required int AnyTwoAxisPlateauWorldCount { get; init; }

    public required int AnyThirdAxisAdditionalReductionWorldCount { get; init; }

    public required int FullSaturationWorldCount { get; init; }

    public required decimal MeanPerWorldMinimumCandidateCount { get; init; }

    public required int AllMinimumProfilesSameCandidateSetWorldCount { get; init; }
}

public sealed record CommonWorldFieldCombinationManifest
{
    public int OutputSchemaVersion { get; init; } = 1;

    public required string ExperimentId { get; init; }

    public required string ScenarioId { get; init; }

    public required string GeneratorId { get; init; }

    public required string ExperimentContentSha256 { get; init; }

    public required string SourceCommitSha { get; init; }

    public required bool DirtyWorkingTree { get; init; }

    public IReadOnlyList<int> Seeds { get; init; } = [];

    public IReadOnlyList<string> ProfileIds { get; init; } = [];

    public required int WorldCount { get; init; }

    public required int ProfileEvaluationCount { get; init; }

    public required string LocationRuleId { get; init; }

    public required string TimeRuleId { get; init; }

    public required string ParticipantRuleId { get; init; }

    public required InteractionRelationKind FixedRelationKind { get; init; }

    public required bool TruthUsedForDerivationOrComposition { get; init; }

    public IReadOnlyList<string> OutputFiles { get; init; } = [];
}

public sealed record CommonWorldFieldCombinationEvaluationResult
{
    public required CommonWorldFieldCombinationManifest Manifest { get; init; }

    public required CommonWorldFieldCombinationGeneratorDefinition Generator { get; init; }

    public IReadOnlyList<CommonWorldProfileDefinition> Profiles { get; init; } = [];

    public IReadOnlyList<CommonWorldSetInclusionDefinition> PrimarySetInclusions { get; init; } = [];

    public IReadOnlyList<CommonWorldConditionalComparisonDefinition> ConditionalComparisons { get; init; } = [];

    public IReadOnlyList<string> FailureConditions { get; init; } = [];

    public IReadOnlyList<CommonWorldFieldCombinationWorldRun> Runs { get; init; } = [];

    public IReadOnlyList<CommonWorldFieldCombinationProfileAggregate> ProfileAggregates { get; init; } = [];

    public IReadOnlyList<CommonWorldConditionalReductionAggregate> ConditionalReductionAggregates { get; init; } = [];

    public IReadOnlyList<CommonWorldProfileEquivalenceAggregate> ProfileEquivalenceAggregates { get; init; } = [];

    public IReadOnlyList<CommonWorldMinimumProfileFrequency> MinimumProfileFrequencies { get; init; } = [];

    public required CommonWorldFieldCombinationRepresentationSummary RepresentationSummary { get; init; }

    public required CommonWorldFieldCombinationSummary Summary { get; init; }

    public required bool OriginalF15FixtureRetained { get; init; }

    public required bool F8ThroughF14RegressionsRetained { get; init; }

    public required bool ExistingS5Changed { get; init; }

    public required string InterpretationBoundary { get; init; }
}

public static class CommonWorldFieldCombinationExperimentLoader
{
    public static CommonWorldFieldCombinationExperimentDefinition Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var options = new JsonSerializerOptions(JsonDefaults.Options)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        var definition = JsonSerializer.Deserialize<CommonWorldFieldCombinationExperimentDefinition>(
            File.ReadAllText(path),
            options) ?? throw new InvalidDataException($"Experiment '{path}' was empty.");
        if (definition.SchemaVersion != 1)
        {
            throw new InvalidDataException(
                $"Experiment '{definition.Id}' has unsupported schema version " +
                $"{definition.SchemaVersion}.");
        }

        return definition;
    }
}
