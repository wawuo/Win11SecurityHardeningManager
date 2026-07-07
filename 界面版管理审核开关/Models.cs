namespace Win11SecurityHardeningManager;

public sealed class HardeningStep
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string ScriptFolder { get; init; }
    public bool HighRisk { get; init; }
}
