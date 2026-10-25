"""يولّد assets/flags/{cc}.png (علمُ كلّ دولةٍ لها عملةٌ في الكتالوج) وملفَّ التعيين Domain/Companies/CurrencyCountries.g.cs.

    python tools/brand/currency_flags.py

الأعلامُ من flagcdn.com بعرض ٨٠ بكسل (الأعلامُ تصاميمُ عامّة؛ الخدمة مجّانيّة للاستعمال). التعيينُ عملةٌ ⟵ دولةٌ **ممثِّلةٌ**: ما تتشاركه دولٌ
(اليورو ⟵ الاتحاد الأوروبيّ، فرنكا غرب/وسط إفريقيا وشرق الكاريبي) يُمثَّل بعلم الجهة المُصدِرة أو مقرّ مصرفها المركزيّ.
"""
import os
import sys
import time
import urllib.request

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
FLAGS = os.path.join(ROOT, "assets", "flags")
OUT = os.path.join(ROOT, "src", "Brqosh.Domain", "Companies", "CurrencyCountries.g.cs")

MAP = {
    "AED": "ae", "AFN": "af", "ALL": "al", "AMD": "am", "ANG": "cw", "AOA": "ao", "ARS": "ar", "AUD": "au", "AWG": "aw", "AZN": "az",
    "BAM": "ba", "BBD": "bb", "BDT": "bd", "BGN": "bg", "BHD": "bh", "BIF": "bi", "BMD": "bm", "BND": "bn", "BOB": "bo", "BRL": "br",
    "BSD": "bs", "BTN": "bt", "BWP": "bw", "BYN": "by", "BZD": "bz", "CAD": "ca", "CDF": "cd", "CHF": "ch", "CLP": "cl", "CNY": "cn",
    "COP": "co", "CRC": "cr", "CUP": "cu", "CVE": "cv", "CZK": "cz", "DJF": "dj", "DKK": "dk", "DOP": "do", "DZD": "dz", "EGP": "eg",
    "ERN": "er", "ETB": "et", "EUR": "eu", "FJD": "fj", "FKP": "fk", "GBP": "gb", "GEL": "ge", "GHS": "gh", "GIP": "gi", "GMD": "gm",
    "GNF": "gn", "GTQ": "gt", "GYD": "gy", "HKD": "hk", "HNL": "hn", "HTG": "ht", "HUF": "hu", "IDR": "id", "ILS": "il", "INR": "in",
    "IQD": "iq", "IRR": "ir", "ISK": "is", "JMD": "jm", "JOD": "jo", "JPY": "jp", "KES": "ke", "KGS": "kg", "KHR": "kh", "KMF": "km",
    "KPW": "kp", "KRW": "kr", "KWD": "kw", "KYD": "ky", "KZT": "kz", "LAK": "la", "LBP": "lb", "LKR": "lk", "LRD": "lr", "LSL": "ls",
    "LYD": "ly", "MAD": "ma", "MDL": "md", "MGA": "mg", "MKD": "mk", "MMK": "mm", "MNT": "mn", "MOP": "mo", "MRU": "mr", "MUR": "mu",
    "MVR": "mv", "MWK": "mw", "MXN": "mx", "MYR": "my", "MZN": "mz", "NAD": "na", "NGN": "ng", "NIO": "ni", "NOK": "no", "NPR": "np",
    "NZD": "nz", "OMR": "om", "PAB": "pa", "PEN": "pe", "PGK": "pg", "PHP": "ph", "PKR": "pk", "PLN": "pl", "PYG": "py", "QAR": "qa",
    "RON": "ro", "RSD": "rs", "RUB": "ru", "RWF": "rw", "SAR": "sa", "SBD": "sb", "SCR": "sc", "SDG": "sd", "SEK": "se", "SGD": "sg",
    "SHP": "sh", "SLE": "sl", "SOS": "so", "SRD": "sr", "SSP": "ss", "STN": "st", "SVC": "sv", "SYP": "sy", "SZL": "sz", "THB": "th",
    "TJS": "tj", "TMT": "tm", "TND": "tn", "TOP": "to", "TRY": "tr", "TTD": "tt", "TWD": "tw", "TZS": "tz", "UAH": "ua", "UGX": "ug",
    "USD": "us", "UYU": "uy", "UZS": "uz", "VES": "ve", "VND": "vn", "VUV": "vu", "WST": "ws", "XAF": "cm", "XCD": "kn", "XOF": "sn",
    "XPF": "pf", "YER": "ye", "ZAR": "za", "ZMW": "zm", "ZWG": "zw",
}


def main():
    os.makedirs(FLAGS, exist_ok=True)
    wanted = sorted(set(MAP.values()))
    missing = []
    for cc in wanted:
        path = os.path.join(FLAGS, cc + ".png")
        if os.path.exists(path) and os.path.getsize(path) > 100:
            continue
        for attempt in range(3):
            try:
                data = urllib.request.urlopen(f"https://flagcdn.com/w80/{cc}.png", timeout=30).read()
                if data[:4] == b"\x89PNG":
                    open(path, "wb").write(data)
                    break
            except Exception as ex:  # noqa: BLE001
                err = ex
            time.sleep(1)
        else:
            missing.append(cc)
    if missing:
        print("لم تُنزَّل:", missing)
        return 1

    lines = ",\n".join(f'        ["{k}"] = "{v.upper()}"' for k, v in sorted(MAP.items()))
    text = f'''namespace Brqosh.Domain.Companies;

// ‏مولَّدٌ بـ tools/brand/currency_flags.py — لا يُعدَّل يدويّاً.
public static partial class CurrencyCatalog
{{
    /// <summary>رمزُ الدولة (ISO 3166) الممثِّلة لكلّ عملة — يُعرض مع علمها في قوائم اختيار العملة. اليورو «EU»، وما تتشاركه دولٌ يُمثَّل بمقرّ مصرفها المركزيّ.</summary>
    private static readonly Dictionary<string, string> Countries = new(StringComparer.Ordinal)
    {{
{lines},
    }};
}}
'''
    open(OUT, "w", encoding="utf-8", newline="\n").write(text)
    print("ok:", len(wanted), "علماً،", len(MAP), "عملة")
    return 0


if __name__ == "__main__":
    sys.exit(main())
