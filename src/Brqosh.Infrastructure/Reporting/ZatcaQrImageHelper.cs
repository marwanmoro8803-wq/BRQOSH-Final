using QRCoder;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>Render ZATCA TLV (Base64 payload) as PNG bytes for QuestPDF embedding.</summary>
public static class ZatcaQrImageHelper
{
    public static byte[]? TryCreatePng(string? tlvBase64, int pixelsPerModule = 4)
    {
        if (string.IsNullOrWhiteSpace(tlvBase64))
            return null;

        try
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(tlvBase64, QRCodeGenerator.ECCLevel.M);
            return new PngByteQRCode(data).GetGraphic(pixelsPerModule);
        }
        catch
        {
            return null;
        }
    }
}
