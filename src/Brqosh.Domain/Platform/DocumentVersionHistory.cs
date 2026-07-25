namespace Brqosh.Domain.Platform;

/// <summary>سجل إصدارات المستند — v1 عند الترحيل (LIFECYCLE-21–30).</summary>
public class DocumentVersionHistory
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public string DocumentType { get; private set; }
    public string DocumentKey { get; private set; }
    public int Version { get; private set; }
    public string Action { get; private set; }
    public string Username { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private DocumentVersionHistory()
    {
        DocumentType = null!;
        DocumentKey = null!;
        Action = null!;
        Username = null!;
    }

    public static DocumentVersionHistory RecordPost(
        long companyId, string documentType, string documentKey, int version, string username) =>
        new()
        {
            CompanyId = companyId,
            DocumentType = documentType,
            DocumentKey = documentKey,
            Version = version,
            Action = "Posted",
            Username = username,
            CreatedAtUtc = DateTime.UtcNow,
        };
}
