namespace Brqosh.Domain.Accounting;

/// <summary>خطأ في قاعدة عمل محاسبية — رسالته عربية وتُعرض للمستخدم مباشرة.</summary>
public class AccountingException : Exception
{
    public AccountingException(string message) : base(message)
    {
    }

    /// <summary>يحفظ السبب التقني الأصلي خلف رسالة عربية مفهومة للمستخدم.</summary>
    public AccountingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
