Analyze the best strategy in performance cost of adding 'its_bouncing' field:

Analyze the prox and cons of adding a boolean field that indicates if the stock is bouncing from Calculate20Ema (public async Task<SymbolHistoryResult> GetSymbolHistory(string symbol, GetSymbolHistoryParameters parameters, CancellationToken cancellationToken = default))

Affects service: 
Services/StatisticsService.cs

Method:

public async Task<List<SymbolResult>> GetTopGrowth(StartStatisticJobParameters startJobParameters, StatisticJobInfo jobInfo, CancellationToken cancellationToken = default).

Idea:
'its_bouncing' = true if:
- in the past it has a close price higher than today
- Also, after that higher value it had a close_price that was lower from that higher priece, but also lower from 20EMA, 
- now its going up from that lower value;
- Target price is lower that current price


