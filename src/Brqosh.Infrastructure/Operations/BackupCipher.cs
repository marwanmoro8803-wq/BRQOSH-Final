using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Brqosh.Domain.Accounting;

namespace Brqosh.Infrastructure.Operations;

/// <summary>
/// (جاهزيّةُ الإنتاج · H · قرارُ المالك 2026-11-14) <b>تشفيرُ النسخة الاحتياطيّة تشفيراً مغلَّفاً — ومفتاحُ الاستعادة يفتحها.</b>
///
/// <para><b>لماذا لا DPAPI:</b> DPAPI يربط الملفَّ بالجهاز ومستخدم ويندوز — <b>فتموت النسخةُ مع الجهاز الذي وُجدت لتنقذه</b>،
/// وهو سيناريو التعافي نفسُه. فالنسخةُ هنا لا تحتاج للفتح إلّا مفتاحَ استعادة المدير، على أيّ جهاز.</para>
///
/// <para><b>والبنيةُ مغلَّفةٌ على طبقتين لأنّ النسخَ المجدولة لا تملك مفتاحَ الاستعادة</b> (يُخزَّن تجزئةً فقط):</para>
/// <list type="number">
/// <item>لكلّ نسخةٍ <b>مفتاحُ بياناتٍ عشوائيّ 256 بت</b> يشفّر المحتوى بـAES-256-GCM مقطَّعاً (1 ميبي بايت للقطعة)؛ لكلّ قطعةٍ وسمُ
/// مصادقة، ورقمُها وعَلَمُ «الأخيرة» في البيانات المرافقة — فتبديلُ قطعتين أو بترُ الذيل أو تعديلُ بايتٍ يُرفض.</item>
/// <item>مفتاحُ البيانات يُغلَّف بـECDH (P-256) مع <b>المفتاح العامّ الرئيسيّ للنسخ</b> ومفتاحٍ عابرٍ للملفّ — فالنسخُ المجدولة تُشفَّر
/// بلا أيّ سرّ.</item>
/// <item>المفتاحُ الخاصّ الرئيسيّ مغلَّفٌ بمفتاحٍ مشتقٍّ من مفتاح الاستعادة (PBKDF2-HMAC-SHA256، 600,000 دورة، ملحٌ عشوائيّ)
/// <b>ونسختُه في رأس كلّ ملفّ</b> — فالاستعادةُ على جهازٍ جديدٍ بلا قاعدةٍ ولا ملفّاتٍ أخرى تحتاج المفتاحَ وحدَه.</item>
/// </list>
///
/// <para><b>وتدويرُ مفتاح الاستعادة يُعيد تغليفَ المفتاح الخاصّ وحدَه</b>: مقطعُ الاستعادة في الرأس ثابتُ الطول ويُكتب في مكانه
/// (<see cref="RewrapRecovery"/>)، والمحتوى لا يُعاد تشفيرُه. ولهذا يخرج ذلك المقطعُ من البيانات المرافقة للمحتوى.</para>
///
/// <para><b>والنسخُ القديمة غيرُ المشفّرة تبقى تُستعاد</b>: الملفُّ يُعرف مشفّراً من توقيعه السحريّ (<see cref="IsEncrypted"/>)،
/// وما عداه يمرّ بالمسار القديم كما كان.</para>
/// </summary>
public static class BackupCipher
{
    /// <summary>التوقيعُ السحريّ — «BRQENC» ثمّ رقمُ الإصدار.</summary>
    internal static readonly byte[] Magic = "BRQENC01"u8.ToArray();

    internal const int ChunkSize = 1 << 20;
    internal const int TagSize = 16;
    internal const int NonceSize = 12;
    internal const int PointSize = 65;          // ‏نقطةٌ غيرُ مضغوطة على P-256: 0x04 ‖ X ‖ Y
    internal const int KeySize = 32;

    // ‏مواضعُ الرأس — ثابتةٌ كلُّها، فتدويرُ المفتاح كتابةٌ في مكانها.
    internal const int FileIdOffset = 8;
    internal const int MasterPointOffset = FileIdOffset + 16;                 // 24
    internal const int EphemeralPointOffset = MasterPointOffset + PointSize;  // 89
    internal const int WrapNonceOffset = EphemeralPointOffset + PointSize;    // 154
    internal const int WrappedKeyOffset = WrapNonceOffset + NonceSize;        // 166
    internal const int WrapTagOffset = WrappedKeyOffset + KeySize;            // 198
    internal const int RecoveryOffset = WrapTagOffset + TagSize;              // 214
    internal const int RecoveryLength = RecoveryWrap.Length;                  // 80
    internal const int ChunkSizeOffset = RecoveryOffset + RecoveryLength;     // 294
    internal const int DataNonceOffset = ChunkSizeOffset + 4;                 // 298
    internal const int HeaderLength = DataNonceOffset + NonceSize;            // 310

    private static readonly byte[] WrapLabel = "BRQ-BACKUP-WRAP-v1"u8.ToArray();

    /// <summary>هل الملفُّ نسخةٌ مشفّرةٌ بهذه الصيغة؟ يقرأ التوقيعَ وحدَه.</summary>
    public static bool IsEncrypted(string path)
    {
        if (!File.Exists(path)) return false;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length < HeaderLength) return false;
        Span<byte> head = stackalloc byte[8];
        stream.ReadExactly(head);
        return head.SequenceEqual(Magic);
    }

    /// <summary>يشفّر <paramref name="plainPath"/> إلى <paramref name="encryptedPath"/> للمفتاح العامّ الرئيسيّ.</summary>
    public static void Encrypt(string plainPath, string encryptedPath, byte[] masterPoint, RecoveryWrap recovery)
    {
        if (masterPoint.Length != PointSize) throw new ArgumentException("نقطةُ المفتاح العامّ ليست P-256 غيرَ مضغوطة.", nameof(masterPoint));

        var header = new byte[HeaderLength];
        Magic.CopyTo(header, 0);
        RandomNumberGenerator.Fill(header.AsSpan(FileIdOffset, 16));
        masterPoint.CopyTo(header, MasterPointOffset);

        using var ephemeral = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        WritePoint(ephemeral.ExportParameters(false).Q, header.AsSpan(EphemeralPointOffset, PointSize));

        var dataKey = RandomNumberGenerator.GetBytes(KeySize);
        try
        {
            var wrapKey = DeriveWrapKey(ephemeral, masterPoint, header.AsSpan(FileIdOffset, 16));
            try
            {
                RandomNumberGenerator.Fill(header.AsSpan(WrapNonceOffset, NonceSize));
                using var wrap = new AesGcm(wrapKey, TagSize);
                wrap.Encrypt(header.AsSpan(WrapNonceOffset, NonceSize), dataKey,
                    header.AsSpan(WrappedKeyOffset, KeySize), header.AsSpan(WrapTagOffset, TagSize),
                    header.AsSpan(0, WrapNonceOffset));
            }
            finally { CryptographicOperations.ZeroMemory(wrapKey); }

            recovery.WriteTo(header.AsSpan(RecoveryOffset, RecoveryLength));
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(ChunkSizeOffset, 4), ChunkSize);
            RandomNumberGenerator.Fill(header.AsSpan(DataNonceOffset, NonceSize));

            using var input = new FileStream(plainPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var output = new FileStream(encryptedPath, FileMode.Create, FileAccess.Write, FileShare.None);
            output.Write(header);

            using var aes = new AesGcm(dataKey, TagSize);
            var current = new byte[ChunkSize];
            var next = new byte[ChunkSize];
            var cipher = new byte[ChunkSize];
            var tag = new byte[TagSize];
            var currentLength = ReadFull(input, current);
            long index = 0;
            while (true)
            {
                var nextLength = currentLength == ChunkSize ? ReadFull(input, next) : 0;
                var final = nextLength == 0;
                aes.Encrypt(ChunkNonce(header, index), current.AsSpan(0, currentLength),
                    cipher.AsSpan(0, currentLength), tag, ChunkAad(header, index, final));
                output.Write(cipher, 0, currentLength);
                output.Write(tag);
                if (final) break;
                (current, next) = (next, current);
                currentLength = nextLength;
                index++;
            }

            output.Flush(flushToDisk: true);
            CryptographicOperations.ZeroMemory(current);
            CryptographicOperations.ZeroMemory(next);
        }
        finally { CryptographicOperations.ZeroMemory(dataKey); }
    }

    /// <summary>
    /// يفكّ النسخةَ إلى <paramref name="plainPath"/> بالمفتاح الخاصّ الرئيسيّ. <b>أيُّ وسمٍ لا يطابق يرمي</b> ويُحذف الناتجُ الجزئيّ.
    /// </summary>
    public static void Decrypt(string encryptedPath, string plainPath, ECDiffieHellman masterPrivate)
    {
        try
        {
            using var input = new FileStream(encryptedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var header = ReadHeader(input, encryptedPath);
            var masterPoint = header.AsSpan(MasterPointOffset, PointSize).ToArray();
            if (!PointOf(masterPrivate).AsSpan().SequenceEqual(masterPoint))
                throw new AccountingException(
                    "مفتاحُ النسخ في هذا الجهاز ليس المفتاحَ الذي شُفّرت به هذه النسخة — افتحها بمفتاح الاستعادة الذي كان قائماً عند أخذها.");

            var dataKey = UnwrapDataKey(header, masterPrivate);
            try
            {
                var chunkSize = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(ChunkSizeOffset, 4));
                if (chunkSize is <= 0 or > 64 << 20)
                    throw Tampered(encryptedPath);

                using var output = new FileStream(plainPath, FileMode.Create, FileAccess.Write, FileShare.None);
                using var aes = new AesGcm(dataKey, TagSize);
                var cipher = new byte[chunkSize + TagSize];
                var plain = new byte[chunkSize];
                var remaining = input.Length - HeaderLength;
                long index = 0;
                if (remaining < TagSize) throw Tampered(encryptedPath);
                while (remaining > 0)
                {
                    if (remaining < TagSize) throw Tampered(encryptedPath);
                    var take = (int)Math.Min(chunkSize + TagSize, remaining);
                    var final = take == remaining;
                    input.ReadExactly(cipher, 0, take);
                    var length = take - TagSize;
                    try
                    {
                        aes.Decrypt(ChunkNonce(header, index), cipher.AsSpan(0, length), cipher.AsSpan(length, TagSize),
                            plain.AsSpan(0, length), ChunkAad(header, index, final));
                    }
                    catch (AuthenticationTagMismatchException)
                    {
                        throw Tampered(encryptedPath);
                    }

                    output.Write(plain, 0, length);
                    remaining -= take;
                    index++;
                }

                output.Flush(flushToDisk: true);
                CryptographicOperations.ZeroMemory(plain);
            }
            finally { CryptographicOperations.ZeroMemory(dataKey); }
        }
        catch
        {
            // ‏هنا لا في مرشّح: المرشّحُ يجري قبل إغلاق الملفّ فيفشل الحذف. والاستثناءُ الأصليُّ يصعد كما هو.
            DiscardPartial(plainPath);
            throw;
        }
    }

    // ‏نصٌّ صريحٌ مفكوكٌ جزئيّاً لا يبقى على القرص بعد فشل.
    private static void DiscardPartial(string plainPath)
    {
        try
        {
            if (File.Exists(plainPath)) File.Delete(plainPath);
        }
        catch (IOException ex)
        {
            CrashLog.Write(ex, "backup-decrypt-cleanup");
        }
    }

    /// <summary>
    /// يفتح المفتاحَ الخاصّ الرئيسيّ من رأس النسخة بمفتاح الاستعادة — <b>بلا أيّ ملفٍّ آخر</b>، فيصلح على جهازٍ جديد.
    /// </summary>
    public static ECDiffieHellman OpenWithRecoveryKey(string encryptedPath, string recoveryKey)
    {
        using var input = new FileStream(encryptedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var header = ReadHeader(input, encryptedPath);
        var recovery = RecoveryWrap.ReadFrom(header.AsSpan(RecoveryOffset, RecoveryLength));
        return recovery.Open(recoveryKey, header.AsSpan(MasterPointOffset, PointSize).ToArray());
    }

    /// <summary>نقطةُ المفتاح العامّ الرئيسيّ التي شُفّرت لها النسخة.</summary>
    public static byte[] MasterPointOf(string encryptedPath)
    {
        using var input = new FileStream(encryptedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return ReadHeader(input, encryptedPath).AsSpan(MasterPointOffset, PointSize).ToArray();
    }

    /// <summary>
    /// يكتب مقطعَ استعادةٍ جديداً في مكانه — <b>والمحتوى لا يُمسّ</b>. يرفض إن لم تكن النسخةُ لهذا المفتاح العامّ.
    /// </summary>
    public static bool RewrapRecovery(string encryptedPath, byte[] masterPoint, RecoveryWrap recovery)
    {
        using var stream = new FileStream(encryptedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var header = ReadHeader(stream, encryptedPath);
        if (!header.AsSpan(MasterPointOffset, PointSize).SequenceEqual(masterPoint))
            return false;

        var section = new byte[RecoveryLength];
        recovery.WriteTo(section);
        stream.Position = RecoveryOffset;
        stream.Write(section);
        stream.Flush(flushToDisk: true);
        return true;
    }

    internal static byte[] PointOf(ECDiffieHellman key)
    {
        var point = new byte[PointSize];
        WritePoint(key.ExportParameters(false).Q, point);
        return point;
    }

    internal static ECDiffieHellman ImportPrivate(byte[] d, byte[] masterPoint) =>
        ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = d,
            Q = new ECPoint { X = masterPoint[1..33], Y = masterPoint[33..65] },
        });

    private static byte[] UnwrapDataKey(byte[] header, ECDiffieHellman masterPrivate)
    {
        var ephemeralPoint = header.AsSpan(EphemeralPointOffset, PointSize).ToArray();
        var wrapKey = DeriveWrapKey(masterPrivate, ephemeralPoint, header.AsSpan(FileIdOffset, 16));
        try
        {
            var dataKey = new byte[KeySize];
            using var wrap = new AesGcm(wrapKey, TagSize);
            try
            {
                wrap.Decrypt(header.AsSpan(WrapNonceOffset, NonceSize), header.AsSpan(WrappedKeyOffset, KeySize),
                    header.AsSpan(WrapTagOffset, TagSize), dataKey, header.AsSpan(0, WrapNonceOffset));
            }
            catch (AuthenticationTagMismatchException)
            {
                throw new AccountingException("رأسُ النسخة الاحتياطيّة معدَّلٌ أو تالف — مفتاحُ البيانات لا يُفكّ.");
            }
            return dataKey;
        }
        finally { CryptographicOperations.ZeroMemory(wrapKey); }
    }

    private static byte[] DeriveWrapKey(ECDiffieHellman own, byte[] otherPoint, ReadOnlySpan<byte> fileId)
    {
        using var other = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = otherPoint[1..33], Y = otherPoint[33..65] },
        });
        return own.DeriveKeyFromHash(other.PublicKey, HashAlgorithmName.SHA256, WrapLabel, fileId.ToArray());
    }

    private static byte[] ReadHeader(FileStream stream, string path)
    {
        if (stream.Length < HeaderLength) throw Tampered(path);
        var header = new byte[HeaderLength];
        stream.Position = 0;
        stream.ReadExactly(header);
        if (!header.AsSpan(0, 8).SequenceEqual(Magic))
            throw new AccountingException($"الملفُّ ليس نسخةً مشفّرةً بصيغة BRQOSH: {path}");
        return header;
    }

    private static byte[] ChunkNonce(byte[] header, long index)
    {
        var nonce = header.AsSpan(DataNonceOffset, NonceSize).ToArray();
        var counter = BinaryPrimitives.ReadUInt64BigEndian(nonce.AsSpan(4));
        BinaryPrimitives.WriteUInt64BigEndian(nonce.AsSpan(4), counter ^ (ulong)index);
        return nonce;
    }

    // ‏البياناتُ المرافقة للقطعة: الرأسُ كلُّه عدا مقطعِ الاستعادة القابلِ للتدوير، ثمّ رقمُ القطعة وعَلَمُ الأخيرة.
    private static byte[] ChunkAad(byte[] header, long index, bool final)
    {
        var aad = new byte[RecoveryOffset + (HeaderLength - ChunkSizeOffset) + 9];
        header.AsSpan(0, RecoveryOffset).CopyTo(aad);
        header.AsSpan(ChunkSizeOffset).CopyTo(aad.AsSpan(RecoveryOffset));
        BinaryPrimitives.WriteInt64BigEndian(aad.AsSpan(aad.Length - 9, 8), index);
        aad[^1] = final ? (byte)1 : (byte)0;
        return aad;
    }

    private static void WritePoint(ECPoint q, Span<byte> target)
    {
        target[0] = 0x04;
        q.X!.CopyTo(target[1..33]);
        q.Y!.CopyTo(target[33..65]);
    }

    private static int ReadFull(Stream stream, byte[] buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer, total, buffer.Length - total);
            if (read == 0) break;
            total += read;
        }
        return total;
    }

    private static AccountingException Tampered(string path) =>
        new($"النسخةُ الاحتياطيّة المشفّرة معدَّلةٌ أو مبتورةٌ أو تالفة — وسمُ المصادقة لا يطابق: {path}");
}

/// <summary>
/// <b>المفتاحُ الخاصّ الرئيسيّ مغلَّفاً بمفتاح الاستعادة</b> — 80 بايتاً ثابتة: الدوراتُ ‖ الملحُ ‖ الرقمُ الفريد ‖ المغلَّفُ ‖ الوسم.
/// </summary>
public sealed class RecoveryWrap
{
    public const int Length = 4 + 16 + 12 + 32 + 16;
    private static readonly byte[] Label = "BRQ-BACKUP-MASTER-v1"u8.ToArray();

    public int Iterations { get; }
    public byte[] Salt { get; }
    public byte[] Nonce { get; }
    public byte[] Ciphertext { get; }
    public byte[] Tag { get; }

    private RecoveryWrap(int iterations, byte[] salt, byte[] nonce, byte[] ciphertext, byte[] tag)
    {
        Iterations = iterations;
        Salt = salt;
        Nonce = nonce;
        Ciphertext = ciphertext;
        Tag = tag;
    }

    /// <summary>يغلّف المفتاحَ الخاصّ <paramref name="d"/> بمفتاحٍ مشتقٍّ من مفتاح الاستعادة بملحٍ جديد.</summary>
    public static RecoveryWrap Create(byte[] d, string recoveryKey, byte[] masterPoint, int iterations)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var nonce = RandomNumberGenerator.GetBytes(BackupCipher.NonceSize);
        var ciphertext = new byte[BackupCipher.KeySize];
        var tag = new byte[BackupCipher.TagSize];
        var kek = Derive(recoveryKey, salt, iterations);
        try
        {
            using var aes = new AesGcm(kek, BackupCipher.TagSize);
            aes.Encrypt(nonce, d, ciphertext, tag, Aad(masterPoint));
        }
        finally { CryptographicOperations.ZeroMemory(kek); }
        return new RecoveryWrap(iterations, salt, nonce, ciphertext, tag);
    }

    /// <summary>يفتح المفتاحَ الخاصّ — <b>ومفتاحُ استعادةٍ خاطئ يرمي برسالةٍ تقول ذلك</b>.</summary>
    public ECDiffieHellman Open(string recoveryKey, byte[] masterPoint)
    {
        if (Iterations is < 100_000 or > 10_000_000)
            throw new AccountingException("رأسُ النسخة الاحتياطيّة معدَّل: عددُ دورات الاشتقاق خارج الحدّ.");

        var kek = Derive(recoveryKey, Salt, Iterations);
        var d = new byte[BackupCipher.KeySize];
        try
        {
            using var aes = new AesGcm(kek, BackupCipher.TagSize);
            aes.Decrypt(Nonce, Ciphertext, Tag, d, Aad(masterPoint));
            return BackupCipher.ImportPrivate(d, masterPoint);
        }
        catch (AuthenticationTagMismatchException)
        {
            throw new AccountingException(
                "مفتاحُ الاستعادة غير صحيح لهذه النسخة (أو رأسُها معدَّل). استعمل المفتاحَ الذي كان قائماً عند أخذها أو أحدثَ منه.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
            CryptographicOperations.ZeroMemory(d);
        }
    }

    public void WriteTo(Span<byte> target)
    {
        BinaryPrimitives.WriteInt32LittleEndian(target[..4], Iterations);
        Salt.CopyTo(target[4..20]);
        Nonce.CopyTo(target[20..32]);
        Ciphertext.CopyTo(target[32..64]);
        Tag.CopyTo(target[64..80]);
    }

    public static RecoveryWrap ReadFrom(ReadOnlySpan<byte> source) => new(
        BinaryPrimitives.ReadInt32LittleEndian(source[..4]),
        source[4..20].ToArray(), source[20..32].ToArray(), source[32..64].ToArray(), source[64..80].ToArray());

    public byte[] ToBytes()
    {
        var bytes = new byte[Length];
        WriteTo(bytes);
        return bytes;
    }

    private static byte[] Derive(string recoveryKey, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(Security.AdminRecoveryKey.Normalize(recoveryKey)),
            salt, iterations, HashAlgorithmName.SHA256, BackupCipher.KeySize);

    private static byte[] Aad(byte[] masterPoint) => [.. Label, .. masterPoint];
}
