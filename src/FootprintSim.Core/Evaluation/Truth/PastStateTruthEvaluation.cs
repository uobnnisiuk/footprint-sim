namespace FootprintSim.Evaluation.Truth;

public sealed record PastStateTruthEvaluation
{
    public string? PersonRegionId { get; init; }

    public string? DeviceRegionId { get; init; }

    public required string OutcomeIdentifier { get; init; }
}
