using System.Text;
using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Operations;

namespace Brqosh.Application.Tests.Operations;

/// <summary>(جاهزيّةُ الإنتاج · C) ‏ملفُّ الاستيراد يُقاس قبل أن يُقرأ — والرفضُ يُقال بالعربيّة باسم الملفّ.</summary>
public sealed class ImportFileGuardTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "brqosh-import-" + Guid.NewGuid().ToString("N")[..8]);

    public ImportFileGuardTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); }
        catch (IOException ex) { Console.WriteLine($"تنظيف: {ex.Message}"); }
    }

    [Fact]
    public async Task AFileWithinTheLimit_IsReadWithItsArabicText()
    {
        var path = Path.Combine(_folder, "customers.csv");
        await File.WriteAllTextAsync(path, "الاسم,الرصيد\nشركة النخيل,1250.50\n", new UTF8Encoding(true));

        var text = await ImportFileGuard.ReadCsvAsync(path, Encoding.UTF8);

        Assert.Contains("شركة النخيل,1250.50", text, StringComparison.Ordinal);
    }

    // ‏ملفٌّ أكبر من السقف بقليل — يُرفض قبل أن يُقرأ منه بايت (‏SetLength لا يكتب المحتوى فالاختبار سريع).
    [Fact]
    public async Task AFileOverTheCsvLimit_IsRefusedBeforeReading()
    {
        var path = Path.Combine(_folder, "huge.csv");
        await using (var stream = File.Create(path))
            stream.SetLength(ImportFileGuard.MaxCsvBytes + 1);

        var ex = await Assert.ThrowsAsync<AccountingException>(() => ImportFileGuard.ReadCsvAsync(path));
        Assert.Contains("«huge.csv» أكبر من المسموح", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExactlyTheLimit_IsAccepted_AndOneByteMore_IsNot()
    {
        var path = Path.Combine(_folder, "logo.png");
        File.WriteAllBytes(path, new byte[1024]);

        ImportFileGuard.EnsureWithinLimit(path, 1024);
        Assert.Throws<AccountingException>(() => ImportFileGuard.EnsureWithinLimit(path, 1023));
    }

    [Fact]
    public void AnEmptyOrMissingFile_IsRefusedInWords()
    {
        var empty = Path.Combine(_folder, "empty.csv");
        File.WriteAllBytes(empty, []);

        Assert.Contains("فارغ", Assert.Throws<AccountingException>(
            () => ImportFileGuard.EnsureWithinLimit(empty, ImportFileGuard.MaxCsvBytes)).Message, StringComparison.Ordinal);
        Assert.Contains("غير موجود", Assert.Throws<AccountingException>(
            () => ImportFileGuard.EnsureWithinLimit(Path.Combine(_folder, "nope.csv"), ImportFileGuard.MaxCsvBytes)).Message,
            StringComparison.Ordinal);
    }
}
