using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Accounting;

public record YearEndCloseResult(int Year, long ClosingEntryId, string EntryNumber, decimal NetIncome);

/// <summary>
/// إقفال سنوي SOCPA: إغلاق الإيرادات والمصروفات في 3104 ثم ترحيل النتيجة إلى 3103،
/// وإغلاق جميع الفترات المالية للسنة.
/// </summary>
public class YearEndCloseService(BrqoshDbContext db)
{
    /// <summary>
    /// ‏وسمُ قيد الإقفال السنويّ — <b>مرجعٌ واحدٌ يكتبه المُقفِلُ ويقرأه الحارس</b>.
    ///
    /// <para>‏وكان نصّاً حرفيّاً في موضعين، ولو بقي لصار ثلاثة: الحارسُ الجديد في
    /// <see cref="JournalPostingService.EnsurePeriodOpenAsync"/> يسأل عن القيد نفسِه.
    /// <b>ونسختان من وسمٍ تفترقان في أوّل تعديل</b>، والافتراقُ هنا لا يُرى: يصمت الحارسُ
    /// ويمرّ الترحيل.</para>
    /// </summary>
    public static string SourceDocumentFor(int year) => $"YearEndClose:{year}";

    public async Task<YearEndCloseResult> CloseYearAsync(
        long companyId, int year, string username, CancellationToken ct = default)
    {
        SecurityContext.EnsureAdministrator();
        username = SecurityContext.ResolveUsername(username);

        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var yearStart = new DateOnly(year, 1, 1);
            var yearEnd = new DateOnly(year, 12, 31);

            // (CEEF-229 · م1-١٥ / B-4) **القفلُ قبل الفحص — وإلا كان الفحصُ حكايةً.**
            //
            // كان الحارسُ الوحيد قراءةً عاديّة، والإدراجُ بعدها بنحو خمسةٍ وتسعين سطراً،
            // والمعاملةُ على `Read Committed`. فيقرأ المُقفلان «غيرُ مقفلة» ويكتبان قيدَين.
            //
            // **وقيس بالرقم:** ستّةُ إقفالاتٍ متزامنة ⟵ **ثلاثةُ قيودِ إقفال**، فتضاعفت
            // الأرباحُ المبقاة ثلاثاً. وأخطرُ ما فيه أنّه لا يُرى: قائمةُ الدخل تستثني قيودَ
            // الإقفال، **فيستقرّ التضاعفُ صامتاً في حقوق الملكية بالميزانية**.
            //
            // ومفتاحٌ مستقلٌّ عن مفتاح ترقيم القيود: إقفالُ سنةٍ لا يجوز أن يُجمّد ترحيلَ
            // كلّ قيدٍ فيها طوالَ مدّته.
            // ‏(م-٢ · ن-٠٢) ‏**والقفلُ أعلاه كان يحرس مُقفلاً من مُقفل، ولا يحرس من مرحّل.**
            //
            // ‏`YEAREND` و`PERIOD{شهر}` مفتاحان مختلفان — **فقفلان لا يتقاطعان أبداً**.
            // ‏فبينما يقرأ هذا التابعُ قائمةَ الدخل (أدناه) ويكتب قيدَه بعد عشراتِ السطور،
            // ‏تُرحّل فاتورةٌ في السنة نفسِها بلا اعتراض: **تدخل الدفترَ ولا تدخل القيد**.
            // ‏وقيس: صافي الدخل المُرحّل **١٣٠٠ بدل ٢٢٠٠**. ثمّ تُقفَل فتراتُ السنة بعدها،
            // ‏**فلا يجرفها إقفالٌ لاحقٌ أبداً** — وقائمةُ الدخل تستثني قيودَ الإقفال فلا
            // ‏يظهر الفرقُ فيها، ويستقرّ صامتاً في حقوق الملكية.
            //
            // ‏والاعتراضُ المكتوبُ أسفلَه — «لا يجوز أن يُجمّد ترحيلَ كلّ قيدٍ فيها» — صحيحٌ
            // ‏وباقٍ: الحاجزُ متبادل، فالمرحّلون يأخذون المشترَكَ ولا ينتظر بعضُهم بعضاً،
            // ‏ويحجبهم هذا الحصريُّ **مدّةَ الإقفال وحدَها**. وهو أوّلُ ما يُؤخذ في المسار.
            await LedgerYearLock.AcquireForClosingAsync(db.Database, companyId, year, token);

            await DocumentSequenceLock.AcquireAsync(db.Database, companyId, "YEAREND", year, token);

            // ‏الوسمُ يُحسب قبل الاستعلام: استدعاءُ تابعٍ داخل شجرة التعبير لا يُترجَم إلى SQL.
            var closeMark = SourceDocumentFor(year);
            var existingClose = await db.JournalEntries.AnyAsync(e =>
                e.CompanyId == companyId
                && e.SourceDocument == closeMark
                && e.Status != JournalEntryStatus.Draft, token);
            if (existingClose)
                throw new AccountingException($"السنة {year} مقفلة مسبقاً.");

            // **والسنةُ الساكنة لا تترك أثراً في الدفتر** — المسارُ القصير يُغلق الفترات
            // ولا يكتب قيداً، فلا يجد الفحصُ أعلاه شيئاً ويُقفَل العامُ مرّةً بعد مرّة.
            // وقيس: سنةٌ ساكنة أُقفلت **ستّ مرّات** بلا اعتراضٍ واحد. فيُسأل عن الفترات نفسِها.
            var hasOpenPeriods = await db.FiscalPeriods.AnyAsync(p =>
                p.CompanyId == companyId
                && p.StartDate.Year == year
                && p.Status == FiscalPeriodStatus.Open, token);
            var hasAnyPeriod = await db.FiscalPeriods.AnyAsync(p =>
                p.CompanyId == companyId && p.StartDate.Year == year, token);

            if (hasAnyPeriod && !hasOpenPeriods)
                throw new AccountingException($"السنة {year} مقفلة مسبقاً — فتراتُها كلُّها مغلقة.");

            // (CEEF-229 · م0-١٤) حُذف حارس «قيود بعد نهاية السنة».
            //
            // كان يرفض الإقفال متى وُجد أيّ قيد مرحّل بعد 31 ديسمبر — وهي الحالة الطبيعية لكل
            // منشأة تُقفل ديسمبر في مارس. فزرّ «إقفال سنوي» **لا ينجح أبداً** بعد أوّل قيد في
            // يناير، و3103/3104 يبقيان صفراً إلى الأبد. ورسالتُه كانت تقترح علاجاً غير موجود:
            // الحارس يفحص **القيود** لا الفترات، ولا يُحذف قيدٌ مرحّل ولا يُنقل تاريخه.
            //
            // وهو بلا مبرّر حسابيّ: قيد الإقفال مؤرَّخ 31 ديسمبر ويجمع صفوف السنة وحدها
            // (IncomeStatementQuery محصورة بـ yearStart..yearEnd)، فحركةُ السنة التالية لا تدخله.
            // الحارس الصحيح باقٍ أدناه: EnsurePeriodOpenAsync يرفض الترحيل في فترة مقفلة،
            // وفحص existingClose يمنع الإقفال المزدوج.

            var pnl = await new IncomeStatementQuery(db).ExecuteAsync(companyId, yearStart, yearEnd);
            var closingDate = yearEnd;

            await new JournalPostingService(db).EnsurePeriodOpenAsync(companyId, closingDate);

            var currentYear = await AccountAsync(companyId, SaudiChartOfAccounts.SystemCodes.CurrentYearProfit, token);
            var retained = await AccountAsync(companyId, SaudiChartOfAccounts.SystemCodes.RetainedEarnings, token);

            var entry = JournalEntry.CreateDraft(
                companyId, closingDate,
                $"إقفال السنة المالية {year}",
                closeMark);

            // (CEEF-231 · O-32) ‏**الإقفالُ على الشخصية الاعتبارية لا على فرع.**
            // ‏وبغير هذا الإعلان يوسمه `SaveChanges` بفرع الجلسة — **فتُسند أرباحُ
            // ‏المنشأة المبقاةُ كلُّها إلى الفرع الذي صادف أن يكون مختاراً حين ضُغط الزرّ**.
            entry.MarkEntityLevel();

            // الحسابات المقابلة (contra) — 4102 مردودات المبيعات و4103 خصم مسموح به مصنّفة Revenue
            // لكن رصيدها الصافي مدين (سالب)، و5103 مردودات المشتريات و5214 خصم مكتسب مصنّفة Expense
            // ورصيدها دائن (سالب). تمرير مبلغ سالب إلى AddLine يرمي استثناءً ويُبطل الإقفال كلياً،
            // فتُوجَّه الأطراف حسب إشارة الرصيد لا حسب تصنيف الحساب.
            foreach (var row in pnl.RevenueRows)
            {
                var account = await AccountAsync(companyId, row.AccountCode, token);
                if (row.Amount > 0)
                {
                    entry.AddClosingLine(account, row.Amount, 0m, $"إقفال إيراد {row.AccountCode}");
                    entry.AddLine(currentYear, 0m, row.Amount, $"تحويل إيراد {row.AccountCode}");
                }
                else
                {
                    var amount = -row.Amount;
                    entry.AddClosingLine(account, 0m, amount, $"إقفال إيراد {row.AccountCode}");
                    entry.AddLine(currentYear, amount, 0m, $"تحويل إيراد {row.AccountCode}");
                }
            }

            foreach (var row in pnl.ExpenseRows)
            {
                var account = await AccountAsync(companyId, row.AccountCode, token);
                if (row.Amount > 0)
                {
                    entry.AddLine(currentYear, row.Amount, 0m, $"تحويل مصروف {row.AccountCode}");
                    entry.AddClosingLine(account, 0m, row.Amount, $"إقفال مصروف {row.AccountCode}");
                }
                else
                {
                    var amount = -row.Amount;
                    entry.AddLine(currentYear, 0m, amount, $"تحويل مصروف {row.AccountCode}");
                    entry.AddClosingLine(account, amount, 0m, $"إقفال مصروف {row.AccountCode}");
                }
            }

            // ترحيل صافي الدخل إلى الأرباح المبقاة
            if (pnl.NetIncome > 0)
            {
                entry.AddLine(currentYear, pnl.NetIncome, 0m, "إقفال أرباح العام");
                entry.AddLine(retained, 0m, pnl.NetIncome, "ترحيل للأرباح المبقاة");
            }
            else if (pnl.NetIncome < 0)
            {
                var loss = Math.Abs(pnl.NetIncome);
                entry.AddLine(retained, loss, 0m, "ترحيل خسائر العام");
                entry.AddLine(currentYear, 0m, loss, "إقفال خسائر العام");
            }

            if (entry.Lines.Count < 2)
            {
                await CloseAllYearPeriodsAsync(companyId, year, username, token);
                await new AuditService(db).LogAsync(companyId, username, "إقفال سنوي",
                    "FiscalYear", year.ToString(), "سنة بلا حركة — إغلاق فترات فقط");
                return new YearEndCloseResult(year, 0, "", 0m);
            }

            if (!entry.IsBalanced)
                throw new AccountingException(
                    $"قيد الإقفال غير متوازن: مدين {entry.TotalDebit:N2} / دائن {entry.TotalCredit:N2}.");

            var entryNumber = await new JournalEntryNumberGenerator(db).NextAsync(companyId, closingDate, token);
            entry.Post(entryNumber);
            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync(token);

            await CloseAllYearPeriodsAsync(companyId, year, username, token);

            await new AuditService(db).LogAsync(companyId, username, "إقفال سنوي",
                "FiscalYear", year.ToString(),
                $"قيد {entryNumber} — صافي الدخل {pnl.NetIncome:N2}");

            return new YearEndCloseResult(year, entry.Id, entryNumber, pnl.NetIncome);
        }, ct);
    }

    /// <summary>
    /// ‏<b>يُقفل اثنَي عشرَ شهراً — وكان يُقفل الموجودَ منها وحدَه.</b>
    ///
    /// <para>‏والفترةُ تُنشأ عند أوّل ترحيلٍ فيها لا عند بدء السنة
    /// (<see cref="JournalPostingService.EnsurePeriodOpenAsync"/>). <b>فشهرٌ بلا حركةٍ لا
    /// صفَّ له</b>، وهذا التابعُ كان يُرشّح <c>Status == Open</c> على الصفوف القائمة —
    /// <b>فيخرج الشهرُ الساكنُ من الإقفال لا لأنّه أُقفل بل لأنّه لم يوجد</b>.</para>
    ///
    /// <para>‏<b>وقيس:</b> إيرادٌ 1000 في 2025-06-15، ثمّ إقفالُ 2025 — فالفتراتُ
    /// <c>2025-06=Closed · 2025-12=Closed</c> ولا غيرُهما. ثمّ قيدٌ بتاريخ 2025-03-10
    /// <b>قُبِل ورُحِّل</b> (<c>JV-2025-000003</c>) وأُنشئ له <c>2025-03</c> مفتوحاً.
    /// فصافي 4101 عن السنة المقفلة <c>-500</c> بدل صفر، و3103 لم يتحرّك — <b>وإعادةُ
    /// الإقفال مرفوضةٌ أبداً</b> لأنّ قيدَ الإقفال قائم. أي أنّ دفترَ سنةٍ اعتمدها المراجعُ
    /// يتغيّر بعد اعتماده، ولا سبيلَ لردّه.</para>
    ///
    /// <para>‏<b>وإنشاءُ الاثنَي عشرَ ليس احتياطاً زائداً</b>: إعادةُ الفتح
    /// (<see cref="FiscalPeriodService.ReopenAsync"/>) تطلب صفّاً بمعرّفه، <b>فشهرٌ بلا
    /// صفٍّ لا يُعاد فتحُه</b> — فالصفُّ المقفَل هو نفسُه طريقُ التصحيح المشروع.</para>
    ///
    /// <para>‏<b>ويبقى أثرٌ في قواعدَ أُقفلت بنسخةٍ سابقة:</b> شهرُها الساكنُ بلا صفّ. وحارسُ
    /// <c>EnsurePeriodOpenAsync</c> يمنع الترحيلَ فيه بوسم قيد الإقفال، <b>لكنّ إعادةَ فتحِه
    /// تحتاج ردماً في مسار الترقية</b> — وهو مُدوَّنٌ بنداً مستقلّاً.</para>
    /// </summary>
    private async Task CloseAllYearPeriodsAsync(long companyId, int year, string username, CancellationToken token)
    {
        var existing = await db.FiscalPeriods
            .Where(p => p.CompanyId == companyId && p.StartDate.Year == year)
            .ToListAsync(token);

        var byMonth = existing.ToDictionary(p => p.StartDate.Month);

        for (var month = 1; month <= 12; month++)
        {
            if (byMonth.TryGetValue(month, out var period))
            {
                // ‏المقفَلُ سلفاً (إقفالٌ شهريّ سابق) يُترك على حاله — و<c>Close()</c> يرمي عليه.
                if (period.Status == FiscalPeriodStatus.Closed) continue;
            }
            else
            {
                period = FiscalPeriod.Create(companyId, year, month);
                db.FiscalPeriods.Add(period);
            }

            period.Close();
            await new AuditService(db).LogAsync(companyId, username, "إغلاق فترة مالية (إقفال سنوي)",
                "FiscalPeriod", period.Code, $"سنة {year}");
        }

        await db.SaveChangesAsync(token);
    }

    /// <summary>
    /// (CEEF-229 · م0-١٣) يتجاوز فلتر الحذف الناعم عمداً.
    ///
    /// الإقفال يشمل كل حساب له رصيدٌ في قائمة الدخل — بما فيه ما أُوقف خلال السنة. وقواعد
    /// العملاء القائمة تحمل حسابات أُوقفت قبل هذا الإصلاح فصارت محذوفةً ناعماً، ولولا التجاوز
    /// لبقي إقفالها مستحيلاً حتى بعد إصلاح <c>Account.Deactivate</c>.
    /// </summary>
    private async Task<Account> AccountAsync(long companyId, string code, CancellationToken ct) =>
        await db.Accounts.IgnoreQueryFilters()
            .SingleAsync(a => a.CompanyId == companyId && a.Code == code, ct);
}
