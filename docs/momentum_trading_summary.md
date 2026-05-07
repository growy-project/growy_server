# Momentum Trading — Research Summary

A consolidated synthesis of the empirical and academic literature on momentum investing. This document distills only the findings from reliable sources; personal observations and trading experience live separately in `docs/momentum_personal_notes.md`.

**Primary sources:**
- Asness, C., Frazzini, A., Israel, R., Moskowitz, T. (2014). *Fact, Fiction, and Momentum Investing.* The Journal of Portfolio Management, 40th Anniversary Special Issue. (Local: `docs/JPM Fact Fiction and Momentum Investing.md`.)
- Carhart, M. (1997). *On Persistence in Mutual Fund Performance.* Journal of Finance. (Via Trustnet summary.)
- Gray, W., Vogel, J. *Quantitative Momentum.* (Via NovelInvestor notes.)
- Institutional practitioner frameworks (AQR, BlackRock/iShares).

---

## 1. Definition: the 12-1 momentum window

The standard practitioner definition of momentum is the **past 12-month return, excluding the most recent month** (the "12-1" window). The most recent month is dropped to avoid short-term reversal effects driven by microstructure and liquidity. Source: Asness (1994), built on Jegadeesh & Titman (1993). JPM paper, footnote 6.

Alternative measures exist (3–12 month windows, "consistency" of past returns, fundamental momentum from earnings revisions), but Chan, Jegadeesh & Lakonishok (1996) find similar long-term performance across reasonable measures. **Occam's razor applies**: the simplest definition is hard to beat.

Momentum is **relative**, not absolute: it ranks securities against their peers, regardless of overall market direction. This distinguishes it from trend-following, which scales exposure with absolute price changes (JPM, footnote 1).

---

## 2. Robustness

The momentum premium is one of the most empirically robust effects in finance:

- **212 years** of US equity data, 1801–2012 (Geczy & Samonov 2013, "the world's longest backtest").
- Present in UK equities back to the Victorian era (Chabot et al. 2009).
- More than 20 years of out-of-sample evidence after the original 1993 discovery.
- Present in **40 different countries** and in more than a dozen other asset classes (bonds, currencies, commodities).

Headline numbers from Kenneth French's data (1927–2013), reported in JPM Exhibit 1:

| Factor                | Annual return | Sharpe ratio |
|-----------------------|---------------|--------------|
| SMB (size)            | 2.9%          | 0.26         |
| HML (value)           | 4.7%          | 0.39         |
| **UMD (momentum)**    | **8.3%**      | **0.50**     |

UMD (winners minus losers) is the largest of the three premia in raw return *and* the largest in Sharpe ratio. The ranking is consistent over the full 87-year sample, the 1963-onward Fama-French sample, and the 1991–2013 out-of-sample period.

---

## 3. The Ten Myths (refuted by the JPM paper)

The bulk of the JPM paper is a point-by-point rebuttal of recurring criticisms of momentum. Each row below is a one-line refutation; the JPM file contains the supporting data.

| #  | Myth | Reality |
|----|------|---------|
| 1  | Momentum returns are too small and sporadic | UMD has the largest premium and Sharpe of the major factors over 87+ years |
| 2  | Momentum only works on the short side | Long side ≈5.5% / short side ≈5.1% of UMD's 10.6% — split roughly evenly |
| 3  | Momentum only works in small caps | Large-cap UMD ≈6.8%/yr, small-cap ≈9.8%/yr — both significant. *Value* is the one that's weak in large caps |
| 4  | Trading costs kill momentum | AQR live-trade data ($1T+) shows per-dollar costs are low; momentum survives easily |
| 5  | Momentum is tax-inefficient | Turnover is biased toward holding winners (LT gains) and selling losers (ST losses) — roughly equal tax burden to value |
| 6  | Use momentum as a screen, not a factor | Factor approach beats screen; the screen story implicitly assumes myths 2–4 |
| 7  | Momentum's returns will disappear | No evidence of degradation since the original 1993 discovery; even at zero expected return, the −0.4 correlation with value still warrants a positive weight |
| 8  | Momentum is too volatile (the 2009 crash) | Crashes follow bear-market-then-sharp-rally setups; combining with value cuts max drawdown from −77% to −30% |
| 9  | Different measures give different results | True but trivial — same is true of value. Standard 12-1 definition is robust |
| 10 | There is no theory behind momentum | Both behavioural (underreaction, disposition effect, delayed overreaction) and risk-based (cash-flow risk, discount-rate risk) models exist |

The single thread tying these myths together: critics point at one bad period (e.g. 2009) or one weak slice (e.g. small caps in a particular sample) and generalise. The longer the sample and the broader the geography, the more clearly momentum holds up.

---

## 4. Why Combine Value and Momentum

This is the single most actionable takeaway from the JPM paper. Value (HML) and momentum (UMD) are negatively correlated (≈ −0.4 in Kenneth French's data; ≈ −0.7 using Asness & Frazzini's "pure value" definition). Their combination produces a portfolio that is more than the sum of its parts.

A 60/40 HML/UMD blend over 1927–2013:

| Metric                      | HML alone | UMD alone | 60/40 blend |
|-----------------------------|-----------|-----------|-------------|
| Worst drawdown              | −43%      | −77%      | **−30%**    |

The blend fixes the two factors' worst weaknesses:
- 1999–2000 (the dot-com era) — value was hammered; momentum carried.
- Spring 2009 — momentum crashed (long low-beta, short high-beta into a sharp rally); value carried.

Even if you assumed UMD's expected return were *zero* going forward, the diversification benefit alone justifies a meaningful weight on momentum (JPM, Exhibit 6). Asness (2011) shows the same holds true even in Japan, where the historical momentum premium has been roughly zero — the diversification value remains.

---

## 5. Crash Mechanics

Daniel & Moskowitz (2013), referenced in JPM Myth #8, identify the recipe for momentum crashes:

1. A long bear market (say, the prior two years).
2. An abrupt market upswing.

In that setup, the momentum portfolio is mechanically long low-beta stocks and short high-beta stocks (because betas re-rank during the bear). When the market snaps back, high-beta stocks rally hardest — and the short side detonates. Spring 2009 and late summer 1932 are the textbook cases.

Daniel & Moskowitz find that **all** historical momentum crashes are driven by the short side; the long (winners) side fares well even during these episodes.

Two practical defences:
- A conditional market-beta hedge on the short side.
- The value-momentum combination from §4, which empirically eliminates most of the worst episodes.

---

## 6. Momentum Quality — Smooth vs. Choppy

Wesley Gray and Jack Vogel (*Quantitative Momentum*, summarised on novelinvestor.com) introduce a quality dimension on top of raw momentum:

- **Smooth momentum** — characterised by a high percentage of positive return days — has historically outperformed **choppy momentum** (volatile, gap-driven moves of similar total magnitude).
- **"Boring"** low-volatility momentum tends to outperform **"lottery"** high-beta momentum. Low beta is a usable proxy for "boring".

The principle is that a steady climb reflects sustained buying pressure and slow information diffusion, while gap-up patterns are more often noise or one-off reactions that mean-revert.

---

## 7. Risk-Adjusted Scaling (Institutional Practice)

Large-scale managers (BlackRock/iShares, AQR) implement momentum with risk-adjusted frameworks rather than raw past-return rankings:

- **Volatility scaling.** Momentum scores are normalised by the underlying asset's volatility, so that "high momentum" is not just a reflection of higher overall risk. This addresses the lottery-stock concern from §6.
- **12-1 windows.** As in §1, with the most recent month dropped.
- **Long-only tilts.** Academic models use long-short decile portfolios; institutional long-only products instead overweight high-momentum names (typically large caps) while managing transaction costs and turnover. The factor-based approach is preferred to a screen-based approach (JPM Myth #6).

---

## 8. Theoretical Drivers

The JPM paper closes (Myth #10) by noting that there is no academic consensus on the *cause* of the momentum premium, but there are two well-developed families of explanations.

**Behavioural:**
- **Underreaction.** Information diffuses slowly into prices because investors are conservative, inattentive, face liquidity constraints, or exhibit the **disposition effect** — selling winners too quickly and holding losers too long (Shefrin & Statman 1985; Frazzini 2006).
- **Delayed overreaction.** Investors chase returns, creating a feedback loop that pushes prices beyond fundamental value before eventual reversal (DeLong et al. 1990; Daniel, Hirshleifer & Subrahmanyam 1998).

**Risk-based:**
- **Cash-flow / discount-rate risk.** High-momentum stocks face greater cash-flow risk because of their growth prospects, or greater discount-rate risk because of their investment opportunities — generating a higher cost of capital (Berk, Green & Naik 1999; Johnson 2002; Sagi & Seasholes 2007).
- **Shared economic risk across markets.** The correlation of momentum strategies across asset classes suggests a common economic risk factor (Asness, Moskowitz & Pedersen 2013).

A complementary practitioner explanation, often cited alongside these:
- **Institutional fund flows.** Persistent outperformance attracts steady capital inflows into winning funds, which in turn create continued buying pressure on their holdings — a self-reinforcing dynamic.

For practical purposes, the distinction matters less than is often claimed: under either family of explanations, the premium is expected to persist as long as the underlying risks (or behavioural biases and limits to arbitrage) remain stable.

---

## 9. Lessons from Carhart (1997)

Mark Carhart's *On Persistence in Mutual Fund Performance* is the paper that formally established momentum as a distinct fourth factor alongside Fama-French's market, size, and value. Four practical lessons (via Trustnet's summary):

1. **Past short-term performance is not a reliable indicator of future success.** Selecting funds (or stocks) on recent returns alone is dangerous.
2. **Fees compound.** High expense ratios materially reduce the chance of beating the market over long horizons.
3. **Survivorship bias inflates apparent persistence.** Failed funds disappear from the data; what's left looks more skilled than it really is.
4. **Diversify across factors and strategies.** No single factor — including momentum — is reliable enough to bet everything on.

---

## 10. Caveats

- **The recent decade.** The JPM paper (2014) notes that the ten years prior to publication had been below average for momentum (UMD's 10-year return was in the 7th percentile of rolling 10-year returns going back to 1927). HML was similarly weak (5th percentile). The 60/40 blend's 10-year return was in the 2nd percentile — but still positive. Anyone relying on the historical averages should re-check them against current data.
- **All numbers above are gross of transaction costs and taxes** unless explicitly noted (myths #4 and #5 address those).
- **Long-short vs. long-only.** Most of the cited returns are from long-short factor portfolios. Long-only implementations capture less of the premium but also avoid the short-side crash risk discussed in §5.

---

## 11. Sources

**Local files:**
- `docs/JPM Fact Fiction and Momentum Investing.md`
- `docs/JPM Fact Fiction and Momentum Investing.pdf` (original)
- `docs/momentum_trading.md` (institutional summary by section)
- `docs/momentum_personal_notes.md` (separate — not a source for this summary)

**External URLs** — reachability noted:
- ✅ `trustnet.com/.../the-lessons-of-mark-carharts-on-persistence-in-mutual-fund-performance` — Carhart 1997 summary
- ✅ `novelinvestor.com/notes/quantitative-momentum-by-wesley-gray-jack-vogel/` — momentum-quality (smooth vs. choppy)
- ⚠️ `www-2.rotman.utoronto.ca/.../Jegadeesh_Titman_JF_1993.pdf` — fetched but only as raw PDF binary; locally extractable if needed
- ❌ `researchgate.net/publication/23573737_An_Institutional_Theory_of_Momentum_and_Reversal` — HTTP 403 (login required)

**Primary academic references** (all cited in the JPM paper unless noted):
- Jegadeesh, N. & Titman, S. (1993). *Returns to Buying Winners and Selling Losers.* Journal of Finance.
- Asness, C. (1994). *Variables That Explain Stock Returns.* PhD dissertation, Univ. of Chicago.
- Chan, L., Jegadeesh, N. & Lakonishok, J. (1996). *Momentum Strategies.* Journal of Finance.
- Carhart, M. (1997). *On Persistence in Mutual Fund Performance.* Journal of Finance.
- Asness, C., Moskowitz, T. & Pedersen, L. (2013). *Value and Momentum Everywhere.* Journal of Finance.
- Geczy, C. & Samonov, M. (2013). *212 Years of Price Momentum.* Wharton working paper.
- Daniel, K. & Moskowitz, T. (2013). *Momentum Crashes.* Working paper, Univ. of Chicago.
- Israel, R. & Moskowitz, T. (2013). *The Role of Shorting, Firm Size, and Time on Market Anomalies.* JFE.
- Frazzini, A., Israel, R. & Moskowitz, T. (2013). *Trading Costs of Asset Pricing Anomalies.* Working paper.
- Gray, W. & Vogel, J. *Quantitative Momentum.* (Practitioner book.)
