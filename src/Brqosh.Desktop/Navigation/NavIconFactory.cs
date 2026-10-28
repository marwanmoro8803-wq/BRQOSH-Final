using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Navigation;

/// <summary>أحرف اختصار احترافية لعناصر التنقل — بديل عن الرموز التعبيرية (UI UX Pro Max).</summary>
public static class NavIconGlyphs
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dashboard"] = "LC",
        ["chart_of_accounts"] = "DH",
        ["journal_entries"] = "QY",
        ["cost_centers"] = "CT",
        ["fiscal_periods"] = "FP",
        ["projects"] = "PR",
        ["contracts"] = "CN",
        ["boq"] = "BQ",
        ["progress_billings"] = "MK",
        ["subcontractors"] = "SB",
        ["retentions"] = "RT",
        ["project_profitability"] = "RB",
        ["bom"] = "BM",
        ["work_orders"] = "WO",
        ["wip"] = "WIP",
        ["production_cost_sheet"] = "PCS",
        ["pos_session"] = "POS",
        ["pos_shifts"] = "SH",
        ["pos_reports"] = "RP",
        ["customers"] = "CL",
        ["customer_statement"] = "KS",
        ["sales_quotations"] = "QT",
        ["sales_invoices"] = "FV",
        ["sales_returns"] = "MR",
        ["suppliers"] = "SP",
        ["supplier_statement"] = "KM",
        ["purchase_orders"] = "PO",
        ["goods_receipts"] = "GR",
        ["purchase_invoices"] = "PI",
        ["debit_notes"] = "DN",
        ["products"] = "IT",
        ["product_categories"] = "CT",
        ["posting_accounts"] = "PR",
        ["warehouses"] = "WH",
        ["stock_movements"] = "MV",
        ["stock_count"] = "JD",
        ["landed_cost"] = "LND",
        ["receipt_vouchers"] = "SQ",
        ["payment_vouchers"] = "SX",
        ["bank_accounts"] = "BN",
        ["bank_transfers"] = "BT",
        ["exchange_rates"] = "FX",
        ["cheques"] = "CH",
        ["bank_reconciliation"] = "TS",
        ["zatca_invoices"] = "ZE",
        ["zatca_settings"] = "ZS",
        ["trial_balance"] = "TB",
        ["income_statement"] = "PL",
        ["balance_sheet"] = "BS",
        ["equity_statement"] = "SCE",
        ["consolidated_trial_balance"] = "CTB",
        ["consolidated_income_statement"] = "CPL",
        ["consolidated_balance_sheet"] = "CBS",
        ["vat_return"] = "VAT",
        ["general_ledger_report"] = "GL",
        ["fixed_assets"] = "FA",
        ["payroll"] = "PY",
        ["company_settings"] = "CO",
        ["backup"] = "BK",
        ["company_groups"] = "GRP",
        ["branches"] = "BR",
        ["users"] = "US",
        ["audit_log"] = "AU",
    };

    public static string Get(string pageKey)
    {
        if (Map.TryGetValue(pageKey, out var glyph))
            return glyph;

        var parts = pageKey.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";

        return pageKey.Length >= 2
            ? pageKey[..2].ToUpperInvariant()
            : pageKey.ToUpperInvariant();
    }
}

public static class NavIconFactory
{
    public static UIElement BuildContent(FrameworkElement host, string pageKey, string titleAr)
    {
        // ‏(«مِسطرة») ‏القائمةُ نصٌّ عربيٌّ خالص: لا شاراتُ حروفٍ لاتينيّةٍ (SI · JE · CTB) داخل قائمةٍ عربيّة — فهرسٌ لا لوحةُ أيقونات.
        // ‏والمعاملُ `pageKey` باقٍ في التوقيع ليبقى النداءُ كما هو.
        _ = pageKey;

        var title = new TextBlock
        {
            Text = titleAr,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        return title;
    }
}
