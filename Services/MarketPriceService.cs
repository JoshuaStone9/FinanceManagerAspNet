using System.Globalization;
using System.Text.Json;
using FinanceManagerAspNet.Models;

namespace FinanceManagerAspNet.Services;

public sealed class MarketPriceService(HttpClient http)
{
    public async Task<decimal?> GetLivePriceGbpAsync(AssetHolding asset, CancellationToken cancellationToken = default)
    {
        var type = NormalizeType(asset.AssetType);
        var source = string.IsNullOrWhiteSpace(asset.PriceSource) ? "Auto" : asset.PriceSource.Trim();
        var sourceKey = source.ToLowerInvariant();

        if (sourceKey == "manual" || type is "cash" or "manual") return null;

        try
        {
            return type switch
            {
                "gold" => await GetBullionPriceGbpPerOzAsync("gold", cancellationToken),
                "silver" => await GetBullionPriceGbpPerOzAsync("silver", cancellationToken),
                "crypto" => await GetCryptoPriceGbpAsync(asset.Symbol, cancellationToken),
                "stock" => await GetEquityOrEtfPriceGbpAsync(asset.Symbol, false, sourceKey, cancellationToken),
                "etf" or "fund" => await GetEquityOrEtfPriceGbpAsync(asset.Symbol, true, sourceKey, cancellationToken),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private async Task<decimal?> GetCryptoPriceGbpAsync(string? input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var id = CryptoId(input);
        var coingeckoUrl = $"https://api.coingecko.com/api/v3/simple/price?ids={Uri.EscapeDataString(id)}&vs_currencies=gbp";
        using (var request = JsonRequest(coingeckoUrl))
        using (var response = await http.SendAsync(request, cancellationToken))
        {
            if (response.IsSuccessStatusCode)
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                if (doc.RootElement.TryGetProperty(id, out var coin) && coin.TryGetProperty("gbp", out var price))
                    return Math.Round(price.GetDecimal(), 8);
            }
        }

        var coinbaseSymbol = CryptoSymbol(input);
        var coinbaseUrl = $"https://api.coinbase.com/v2/prices/{Uri.EscapeDataString(coinbaseSymbol)}-GBP/spot";
        using (var request = JsonRequest(coinbaseUrl))
        using (var response = await http.SendAsync(request, cancellationToken))
        {
            if (!response.IsSuccessStatusCode) return null;
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var amount = doc.RootElement.GetProperty("data").GetProperty("amount").GetString();
            return decimal.TryParse(amount, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? Math.Round(value, 8) : null;
        }
    }

    private async Task<decimal?> GetEquityOrEtfPriceGbpAsync(string? input, bool isEtf, string sourceKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var candidates = BuildMarketSymbolCandidates(input, isEtf).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        foreach (var symbol in candidates)
        {
            decimal? price = null;

            if (sourceKey == "stooq")
            {
                price = await GetStooqPriceGbpAsync(symbol, cancellationToken);
            }
            else
            {
                price = await GetYahooPriceGbpAsync(symbol, cancellationToken)
                    ?? await GetStooqPriceGbpAsync(symbol, cancellationToken);
            }

            if (price.HasValue && price.Value > 0) return price;
        }

        return null;
    }

    private async Task<decimal?> GetBullionPriceGbpPerOzAsync(string metal, CancellationToken cancellationToken)
    {
        // Use market symbols first because they are more consistently available than free spot APIs.
        // Gold: XAUUSD=X spot, fallback GC=F futures. Silver: XAGUSD=X, fallback SI=F.
        var yahooSymbols = metal == "gold"
            ? new[] { "XAUUSD=X", "GC=F" }
            : new[] { "XAGUSD=X", "SI=F" };

        foreach (var symbol in yahooSymbols)
        {
            var price = await GetYahooRawPriceAsync(symbol, cancellationToken);
            if (price.Price.HasValue && price.Price.Value > 0)
                return await ConvertPriceToGbpAsync(price.Price.Value, price.Currency ?? "USD", cancellationToken);
        }

        // Stooq fallback. These may be delayed or unavailable depending on the install/network.
        var stooqSymbols = metal == "gold"
            ? new[] { "xauusd", "xaugbp" }
            : new[] { "xagusd", "xaggbp" };

        foreach (var symbol in stooqSymbols)
        {
            var raw = await GetStooqRawPriceAsync(symbol, cancellationToken);
            if (!raw.HasValue || raw.Value <= 0) continue;
            var currency = symbol.EndsWith("gbp", StringComparison.OrdinalIgnoreCase) ? "GBP" : "USD";
            return await ConvertPriceToGbpAsync(raw.Value, currency, cancellationToken);
        }

        return null;
    }

    private async Task<decimal?> GetYahooPriceGbpAsync(string symbol, CancellationToken cancellationToken)
    {
        var result = await GetYahooRawPriceAsync(symbol, cancellationToken);
        return result.Price.HasValue
            ? await ConvertPriceToGbpAsync(result.Price.Value, result.Currency ?? "GBP", cancellationToken)
            : null;
    }

    private async Task<(decimal? Price, string? Currency)> GetYahooRawPriceAsync(string symbol, CancellationToken cancellationToken)
    {
        var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol)}?interval=1d&range=1d";
        using var request = JsonRequest(url);
        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return (null, null);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var chart = doc.RootElement.GetProperty("chart");
        if (chart.GetProperty("result").ValueKind == JsonValueKind.Null) return (null, null);

        var meta = chart.GetProperty("result")[0].GetProperty("meta");
        var currency = meta.TryGetProperty("currency", out var c) ? c.GetString() : null;

        if (meta.TryGetProperty("regularMarketPrice", out var regular) && TryGetDecimal(regular, out var regularValue))
            return (regularValue, currency);

        if (meta.TryGetProperty("previousClose", out var previous) && TryGetDecimal(previous, out var previousValue))
            return (previousValue, currency);

        return (null, currency);
    }

    private async Task<decimal?> GetStooqPriceGbpAsync(string symbol, CancellationToken cancellationToken)
    {
        foreach (var s in BuildStooqCandidates(symbol))
        {
            var raw = await GetStooqRawPriceAsync(s, cancellationToken);
            if (!raw.HasValue || raw.Value <= 0) continue;

            // Stooq uses .uk for London tickers and usually returns pence. Convert GBp to GBP.
            if (s.EndsWith(".uk", StringComparison.OrdinalIgnoreCase))
                return Math.Round(raw.Value / 100m, 4);

            return await ConvertPriceToGbpAsync(raw.Value, "USD", cancellationToken);
        }

        return null;
    }

    private async Task<decimal?> GetStooqRawPriceAsync(string symbol, CancellationToken cancellationToken)
    {
        var url = $"https://stooq.com/q/l/?s={Uri.EscapeDataString(symbol.ToLowerInvariant())}&f=sd2t2ohlcv&h&e=csv";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("FinanceManagerAspNet/1.0");
        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        var csv = await response.Content.ReadAsStringAsync(cancellationToken);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length < 2) return null;
        var columns = lines[1].Split(',');
        if (columns.Length >= 7 && decimal.TryParse(columns[6], NumberStyles.Any, CultureInfo.InvariantCulture, out var close))
            return close;
        return null;
    }

    private async Task<decimal?> ConvertPriceToGbpAsync(decimal price, string currency, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currency)) return Math.Round(price, 4);
        if (currency.Equals("GBP", StringComparison.OrdinalIgnoreCase)) return Math.Round(price, 4);
        if (currency.Equals("GBp", StringComparison.OrdinalIgnoreCase) || currency.Equals("GBX", StringComparison.OrdinalIgnoreCase)) return Math.Round(price / 100m, 4);

        var fx = await GetFxToGbpAsync(currency, cancellationToken);
        return fx.HasValue ? Math.Round(price * fx.Value, 4) : Math.Round(price, 4);
    }

    private async Task<decimal?> GetFxToGbpAsync(string fromCurrency, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fromCurrency) || fromCurrency.Equals("GBP", StringComparison.OrdinalIgnoreCase)) return 1m;
        var pair = $"{fromCurrency.ToUpperInvariant()}GBP=X";
        var result = await GetYahooRawPriceAsync(pair, cancellationToken);
        return result.Price;
    }

    private static HttpRequestMessage JsonRequest(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("FinanceManagerAspNet/1.0");
        request.Headers.Accept.ParseAdd("application/json");
        return request;
    }

    private static bool TryGetDecimal(JsonElement element, out decimal value)
    {
        value = 0;
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetDecimal(out value),
            JsonValueKind.String => decimal.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value),
            _ => false
        };
    }

    private static string NormalizeType(string? assetType) => (assetType ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "stocks" => "stock",
        "cryptocurrency" => "crypto",
        "fund" => "etf",
        var x => x
    };

    private static IEnumerable<string> BuildMarketSymbolCandidates(string input, bool isEtf)
    {
        var s = input.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(s)) yield break;

        // Explicit known Trading 212 / LSE ETF mappings. Yahoo normally uses .L.
        var mapped = s switch
        {
            "VWRP" => "VWRP.L",
            "VWRL" => "VWRL.L",
            "VUAG" => "VUAG.L",
            "VUSA" => "VUSA.L",
            "VHVG" => "VHVG.L",
            "VFEG" => "VFEG.L",
            "EQQB" => "EQQB.L",
            "MCTS" => "MCTS.L",
            "EQQQ" => "EQQQ.L",
            "VUKG" => "VUKG.L",
            _ => s
        };

        yield return mapped;
        yield return s;

        if (isEtf && !s.Contains('.') && !s.Contains(':'))
            yield return s + ".L";
    }

    private static IEnumerable<string> BuildStooqCandidates(string yahooOrRawSymbol)
    {
        var s = yahooOrRawSymbol.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(s)) yield break;

        if (s.EndsWith(".l"))
        {
            yield return s[..^2] + ".uk";
            yield break;
        }

        yield return s;
        if (!s.Contains('.'))
        {
            yield return s + ".uk";
            yield return s + ".us";
        }
    }

    private static string CryptoId(string symbol) => symbol.Trim().ToLowerInvariant() switch
    {
        "btc" or "bitcoin" => "bitcoin",
        "eth" or "ethereum" => "ethereum",
        "xrp" or "ripple" => "ripple",
        "sol" or "solana" => "solana",
        "ada" or "cardano" => "cardano",
        "doge" or "dogecoin" => "dogecoin",
        "dot" or "polkadot" => "polkadot",
        "link" or "chainlink" => "chainlink",
        "ltc" or "litecoin" => "litecoin",
        "bnb" or "binancecoin" => "binancecoin",
        "matic" or "polygon" => "polygon",
        "avax" or "avalanche" => "avalanche-2",
        var x => x
    };

    private static string CryptoSymbol(string symbol) => symbol.Trim().ToLowerInvariant() switch
    {
        "bitcoin" => "BTC",
        "ethereum" => "ETH",
        "ripple" => "XRP",
        "solana" => "SOL",
        "cardano" => "ADA",
        "dogecoin" => "DOGE",
        "polkadot" => "DOT",
        "chainlink" => "LINK",
        "litecoin" => "LTC",
        "binancecoin" => "BNB",
        "polygon" => "MATIC",
        "avalanche-2" => "AVAX",
        var x => x.ToUpperInvariant()
    };
}
