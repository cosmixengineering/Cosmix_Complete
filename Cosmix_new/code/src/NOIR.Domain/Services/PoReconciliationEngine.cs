namespace NOIR.Domain.Services;

public class PoComparisonResult
{
    public bool IsMatched { get; set; }
    public string MatchType { get; set; } = "Exact Match";
    public List<string> MatchedFields { get; set; } = [];
    public List<string> Discrepancies { get; set; } = [];
    public decimal VarianceAmount { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Reconciles Customer PO against approved BOQ/Quotation (Ported from MatchFlow MatchingService).
/// </summary>
public static class PoReconciliationEngine
{
    public static PoComparisonResult Reconcile(
        decimal poTotal,
        decimal quoteTotal,
        string poCurrency,
        string quoteCurrency,
        IEnumerable<(string Code, decimal Qty, decimal Rate)> poItems,
        IEnumerable<(string Code, decimal Qty, decimal Rate)> quoteItems)
    {
        var result = new PoComparisonResult();

        if (poCurrency.Equals(quoteCurrency, StringComparison.OrdinalIgnoreCase))
        {
            result.MatchedFields.Add("Currency");
        }
        else
        {
            result.Discrepancies.Add($"Currency mismatch: PO={poCurrency}, Quote={quoteCurrency}");
        }

        if (poTotal == quoteTotal)
        {
            result.MatchedFields.Add("TotalAmount");
        }
        else
        {
            result.VarianceAmount = poTotal - quoteTotal;
            result.Discrepancies.Add($"Total amount variance: PO={poTotal}, Quote={quoteTotal} (Diff={result.VarianceAmount})");
        }

        var quoteMap = quoteItems.ToDictionary(x => x.Code, x => x);
        foreach (var poItem in poItems)
        {
            if (quoteMap.TryGetValue(poItem.Code, out var quoteItem))
            {
                if (poItem.Qty != quoteItem.Qty)
                {
                    result.Discrepancies.Add($"Item {poItem.Code}: Quantity mismatch PO={poItem.Qty}, Quote={quoteItem.Qty}");
                }
                else
                {
                    result.MatchedFields.Add($"Item {poItem.Code} Quantity");
                }

                if (poItem.Rate != quoteItem.Rate)
                {
                    result.Discrepancies.Add($"Item {poItem.Code}: Rate mismatch PO={poItem.Rate}, Quote={quoteItem.Rate}");
                }
                else
                {
                    result.MatchedFields.Add($"Item {poItem.Code} Rate");
                }
            }
            else
            {
                result.Discrepancies.Add($"Item {poItem.Code} present in PO but missing from approved Quote");
            }
        }

        result.IsMatched = result.Discrepancies.Count == 0;
        result.MatchType = result.IsMatched ? "Exact Match" : "Discrepancy Detected";
        result.Reason = result.IsMatched ? "All items, quantities, and totals match." : string.Join("; ", result.Discrepancies);

        return result;
    }
}
