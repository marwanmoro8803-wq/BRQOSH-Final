using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;

namespace Brqosh.Application.Tests.Security;

/// <summary>
/// (CEEF-237 · ت-٧) <b>رمزُ واتساب يُحفَظ محميّاً كسائر الأسرار.</b>
///
/// <para><b>ما كان:</b> <c>whatsapp.json</c> يُكتب صريحاً في <c>%APPDATA%\BRQOSH</c> بينما
/// أسرارُ ZATCA وقاعدةِ البيانات محميّةٌ بـDPAPI في المجلّد نفسِه. <b>ورمزُ Meta يُرسل
/// رسائلَ باسم المنشأة ويصلح لانتحالها</b> — فليس أدنى منها خطراً.</para>
///
/// <para><b>وملفٌّ قديمٌ صريحٌ لا يتوقّف عن العمل:</b> <c>Unprotect</c> يمرّ ما لا يحمل
/// البادئة كما هو، فيُقرأ ثمّ يُعاد حفظُه محميّاً عند أوّل كتابة.</para>
/// </summary>
public class WhatsAppTokenProtectionTests : IDisposable
{
    private readonly string _root;

    public WhatsAppTokenProtectionTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "brqosh-wa-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        Environment.SetEnvironmentVariable(BrqoshDataRoot.OverrideEnvironmentVariable, _root);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(BrqoshDataRoot.OverrideEnvironmentVariable, null);
        try { Directory.Delete(_root, recursive: true); } catch { }
        GC.SuppressFinalize(this);
    }

    private const string Token = "EAAG-meta-access-token-0123456789";

    [Fact]
    public void TheSavedFile_DoesNotContainTheTokenInClearText()
    {
        WhatsAppChannelOptions.SaveToConfigFile(new WhatsAppChannelOptions
        {
            AccessToken = Token, PhoneNumberId = "123", TemplateName = "brqosh_otp",
        });

        var onDisk = File.ReadAllText(WhatsAppChannelOptions.ConfigFilePath);

        Assert.DoesNotContain(Token, onDisk);
        Assert.Contains("BRQOSH1:", onDisk);   // ‏بادئةُ الحماية المحلّيّة
    }

    [Fact]
    public void WhatWasSaved_IsReadBackIdentically()
    {
        WhatsAppChannelOptions.SaveToConfigFile(new WhatsAppChannelOptions
        {
            AccessToken = Token, PhoneNumberId = "123", TemplateName = "brqosh_otp",
        });

        Assert.Equal(Token, WhatsAppChannelOptions.TryLoad()?.AccessToken);
    }

    [Fact]
    public void AnOlderPlainTextFile_KeepsWorking()
    {
        // ‏ملفٌّ كتبته نسخةٌ سابقة: الترقيةُ لا تُعطّل قناةَ عميلٍ تعمل.
        Directory.CreateDirectory(Path.GetDirectoryName(WhatsAppChannelOptions.ConfigFilePath)!);
        File.WriteAllText(WhatsAppChannelOptions.ConfigFilePath,
            $"{{\"AccessToken\":\"{Token}\",\"PhoneNumberId\":\"123\",\"TemplateName\":\"brqosh_otp\"}}");

        Assert.Equal(Token, WhatsAppChannelOptions.TryLoad()?.AccessToken);
    }
}
