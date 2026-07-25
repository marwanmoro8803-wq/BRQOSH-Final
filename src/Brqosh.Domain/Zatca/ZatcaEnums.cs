namespace Brqosh.Domain.Zatca;

public enum ZatcaEnvironment
{
    /// <summary>بيئة المطور / المحاكاة — للاختبار دون إرسال حقيقي.</summary>
    Simulation = 0,

    /// <summary>الإنتاج — إرسال فعلي لهيئة الزكاة.</summary>
    Production = 1,
}

public enum ZatcaTransmissionMode
{
    /// <summary>فاتورة مبسطة (B2C) — إبلاغ.</summary>
    Reporting = 0,

    /// <summary>فاتورة ضريبية (B2B) — اعتماد فوري.</summary>
    Clearance = 1,
}

public enum ZatcaDocumentType
{
    SalesInvoice = 1,
    CreditNote = 2,
    ProgressBilling = 3,
    /// <summary>إشعار مدين — لإلغاء إشعار دائن (UBL 383).</summary>
    DebitNote = 4,
}

/// <summary>حالة مستند الفاتورة الإلكترونية في دورة ZATCA.</summary>
public enum ZatcaComplianceStatus
{
    Generated = 0,
    ReadyToReport = 1,
    ReadyToClear = 2,
    Reported = 3,
    Cleared = 4,
    Accepted = 5,
    Rejected = 6,
}
