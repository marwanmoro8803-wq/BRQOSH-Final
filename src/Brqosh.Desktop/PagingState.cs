namespace Brqosh.Desktop;

/// <summary>حالة ترقيم صفحات للقوائم الكبيرة — يتجنّب Take صامت بدون عدّاد.</summary>
internal sealed class PagingState
{
    public int PageSize { get; }
    public int PageIndex { get; private set; } = 1;
    public int TotalCount { get; private set; }

    public int TotalPages => TotalCount <= 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public int Skip => Math.Max(0, (PageIndex - 1) * PageSize);
    public bool CanPrev => PageIndex > 1;
    public bool CanNext => PageIndex < TotalPages;

    public PagingState(int pageSize = 100) => PageSize = pageSize;

    public void SetTotal(int total)
    {
        TotalCount = Math.Max(0, total);
        if (PageIndex > TotalPages)
            PageIndex = TotalPages;
        if (PageIndex < 1)
            PageIndex = 1;
    }

    public void Next()
    {
        if (CanNext) PageIndex++;
    }

    public void Prev()
    {
        if (CanPrev) PageIndex--;
    }

    public void Reset() => PageIndex = 1;

    public string StatusText(string noun) =>
        $"إجمالي {noun}: {TotalCount:N0} — صفحة {PageIndex} من {TotalPages} (حجم الصفحة {PageSize})";
}
