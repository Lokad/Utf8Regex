namespace Lokad.Utf8Regex.Internal.Planning;

internal readonly struct Utf8ExecutablePipelinePlan
{
    public Utf8ExecutablePipelinePlan(
        Utf8SearchMetaStrategyPlan strategy,
        Utf8ConfirmationPlan confirmation = default,
        Utf8ProjectionPlan projection = default)
    {
        Strategy = strategy;
        Confirmation = confirmation;
        Projection = projection;
    }

    public Utf8SearchMetaStrategyPlan Strategy { get; }

    public Utf8ConfirmationPlan Confirmation { get; }

    public Utf8ProjectionPlan Projection { get; }

    public bool HasValue => Strategy.HasValue || Confirmation.HasValue || Projection.HasValue;
}
