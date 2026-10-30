namespace growy_server.Strategies
{
    // A screener over the price tables. Each strategy has its own request/result shape
    // (Momentum: date range + growth; Bouncy: as-of snapshot), so the typed interface is generic;
    // the non-generic base carries the key used to pick a strategy (e.g. ?strategy=bouncy later).
    public interface IScreeningStrategy
    {
        string Key { get; }
    }

    public interface IScreeningStrategy<in TRequest, TResult> : IScreeningStrategy
    {
        Task<List<TResult>> ScreenAsync(TRequest request, IProgress<string>? progress = null, CancellationToken cancellationToken = default);
    }
}
