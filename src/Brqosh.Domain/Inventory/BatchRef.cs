namespace Brqosh.Domain.Inventory;

/// <summary>
/// (CEEF-230 · ط1-٣) <b>هويّةُ دفعةٍ مصاحبةٌ لحركةٍ واحدة.</b>
///
/// <para>ولماذا نوعٌ قائمٌ بذاته لا وسيطان: لأنّه يُمرَّر <b>مُلغىً كوحدة</b>
/// (<c>BatchRef?</c>) — و«لا هويّةَ لهذا السطر» حالةٌ واحدةٌ لا حالتان. ووسيطان
/// منفصلان يسمحان بالمُحال: رقمٌ بلا صلاحيةٍ مقبول، <b>وصلاحيةٌ بلا رقمٍ لا معنى لها</b>
/// ولا شيء يمنعها.</para>
///
/// <para>و<see cref="Expiry"/> اختياريّ لأنّ الدفعةَ ليست بالضرورة ذاتَ صلاحية: لوطُ
/// مسامير له رقمٌ ولا ينتهي.</para>
/// </summary>
public readonly record struct BatchRef(string Number, DateOnly? Expiry = null)
{
    /// <summary>يُنشئ هويّةً من نصٍّ قد يكون فارغاً — والفارغُ «لا هويّة» لا رقمٌ فارغ.</summary>
    public static BatchRef? From(string? number, DateOnly? expiry = null) =>
        string.IsNullOrWhiteSpace(number) ? null : new BatchRef(number.Trim(), expiry);
}
