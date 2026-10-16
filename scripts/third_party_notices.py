# -*- coding: utf-8 -*-
"""
(جاهزيّةُ الإطلاق · commercial-readiness#12) ‏**إشعاراتُ الطرف الثالث — من الحزم التي تُشحن فعلاً.**

‏يقرأ `Brqosh.Desktop.deps.json` (ما يُحمَّل وقتَ التشغيل، لا ما يُبنى به) ثمّ لكلّ حزمةٍ ملفَّ `nuspec`
‏ونصوصَ الترخيص التي تحملها الحزمةُ نفسُها من ذاكرة NuGet المحلّيّة — **بلا شبكة**. والحزمةُ التي تُعلن
‏معرّفَ SPDX بلا نصٍّ تُحال إلى النصّ القياسيّ في `licenses/`، **منقولاً حرفاً من نسخٍ موزَّعة** (انظر
‏`licenses/README.md`) لا مكتوباً من الذاكرة.

‏والنصُّ المكرَّر يُطبع مرّةً (بصمةُ SHA-256) ويُحال إليه — ستٌّ وثلاثون حزمةً من Microsoft تحمل الملفَّ نفسَه.

‏الاستعمال:

    dotnet build src/Brqosh.Desktop -c Release
    python scripts/third_party_notices.py            # ‏يكتب THIRD-PARTY-NOTICES.txt في الجذر

‏و`TheShippedPackagesAreAllNoticedTests` يُسقط الحزمةَ إن تغيّرت الحزمُ ولم يُعَد التوليد.
"""
import argparse
import glob
import hashlib
import json
import os
import re
import sys

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
DEFAULT_DEPS = os.path.join(REPO, "src", "Brqosh.Desktop", "bin", "Release", "net10.0-windows", "Brqosh.Desktop.deps.json")
SPDX_DIR = os.path.join(REPO, "licenses")
RULE = "=" * 80
THIN = "-" * 80

# ‏ملفّاتُ الترخيص التي تحملها الحزمةُ في جذرها — وأدلّةُ تبعيّاتها الأصليّة (QuestPDF).
LICENSE_FILE = re.compile(r"^(licen[cs]e|notice|third-party-notices|packagelicense)(\.(txt|md))?$", re.I)
LICENSE_DIRS = ("ExternalDependencyLicenses", "LatoFont")


def nuget_root():
    return os.environ.get("NUGET_PACKAGES") or os.path.join(os.path.expanduser("~"), ".nuget", "packages")


def packages(deps_path):
    with open(deps_path, encoding="utf-8-sig") as f:
        deps = json.load(f)
    found = [k.split("/", 1) for k, v in deps["libraries"].items() if v.get("type") == "package"]
    return sorted(found, key=lambda p: (p[0].lower(), p[1]))


def tag(xml, name):
    m = re.search(r"<%s(?:\s[^>]*)?>([^<]*)</%s>" % (name, name), xml)
    return m.group(1).strip() if m else ""


def read_text(path):
    raw = open(path, "rb").read()
    text = raw.decode("utf-8-sig", errors="strict").replace("\r\n", "\n").replace("\r", "\n")
    return text.strip("\n") + "\n"


def package_texts(root):
    files = [f for f in sorted(os.listdir(root)) if os.path.isfile(os.path.join(root, f)) and LICENSE_FILE.match(f)]
    paths = [os.path.join(root, f) for f in files]
    for d in LICENSE_DIRS:
        sub = os.path.join(root, d)
        if os.path.isdir(sub):
            paths += sorted(p for p in glob.glob(os.path.join(sub, "*"))
                            if os.path.isfile(p) and p.lower().endswith((".txt", ".md")))
    return paths


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--deps", default=DEFAULT_DEPS)
    ap.add_argument("--out", default=os.path.join(REPO, "THIRD-PARTY-NOTICES.txt"))
    args = ap.parse_args()

    if not os.path.isfile(args.deps):
        sys.exit("no deps.json at %s — build src/Brqosh.Desktop in Release first" % args.deps)

    cache = nuget_root()
    texts = {}     # sha256 -> (label, text)
    order = []     # label order of first appearance
    entries = []
    spdx_used = set()

    def cite(label_hint, text):
        digest = hashlib.sha256(text.encode("utf-8")).hexdigest()
        if digest not in texts:
            label = "T%d" % (len(texts) + 1)
            texts[digest] = (label, label_hint, text)
            order.append(digest)
        return texts[digest][0]

    for pid, ver in packages(args.deps):
        root = os.path.join(cache, pid.lower(), ver)
        nuspecs = glob.glob(os.path.join(root, "*.nuspec"))
        if not nuspecs:
            sys.exit("%s %s is not in the local NuGet cache (%s) — restore first" % (pid, ver, root))
        xml = open(nuspecs[0], encoding="utf-8-sig").read()

        expr = re.search(r'<license type="expression">([^<]+)</license>', xml)
        lic_file = re.search(r'<license type="file">([^<]+)</license>', xml)
        refs = []
        for p in package_texts(root):
            rel = os.path.relpath(p, root).replace("\\", "/")
            refs.append("%s [%s]" % (rel, cite("%s %s — %s" % (pid, ver, rel), read_text(p))))

        if expr:
            spdx = expr.group(1).strip()
            ids = re.findall(r"[A-Za-z0-9.\-]+", spdx)
            for i in ids:
                if i.upper() in ("AND", "OR", "WITH"):
                    continue
                if not os.path.isfile(os.path.join(SPDX_DIR, i + ".txt")):
                    sys.exit("%s %s declares %s but licenses/%s.txt is missing" % (pid, ver, i, i))
                spdx_used.add(i)
            license_line = "%s (SPDX) — standard text under «%s» below" % (spdx, spdx)
        elif lic_file:
            license_line = "the package's own licence file %s" % lic_file.group(1).strip()
        elif tag(xml, "licenseUrl"):
            license_line = "as published at %s" % tag(xml, "licenseUrl")
        else:
            sys.exit("%s %s declares no licence at all" % (pid, ver))

        entries.append((pid, ver, license_line, tag(xml, "copyright") or "(not declared in the package metadata)",
                        tag(xml, "projectUrl") or "(none declared)", refs))

    out = []
    out.append("BRQOSH — THIRD-PARTY NOTICES")
    out.append("")
    out.append("This product includes the third-party components listed below. Each is distributed")
    out.append("under its own licence; the licence and copyright notices required for redistribution")
    out.append("follow. Generated by scripts/third_party_notices.py from Brqosh.Desktop.deps.json and")
    out.append("the packages themselves — do not edit by hand.")
    out.append("")
    out.append("Components: %d" % len(entries))
    out.append("")
    for pid, ver, lic, copy, proj, refs in entries:
        out.append(RULE)
        out.append("Package: %s %s" % (pid, ver))
        out.append("License: %s" % lic)
        out.append("Copyright: %s" % copy)
        out.append("Project: %s" % proj)
        if refs:
            out.append("Texts shipped with the package: " + " · ".join(refs))
    out.append(RULE)
    out.append("")
    out.append("STANDARD LICENCE TEXTS")
    out.append("")
    out.append("The standard texts of the SPDX identifiers declared above. Where a text names a")
    out.append("copyright holder, each component's own copyright line (in its entry) stands in its place.")
    out.append("")
    for spdx in sorted(spdx_used):
        out.append(RULE)
        out.append("«%s»" % spdx)
        out.append(THIN)
        out.append(read_text(os.path.join(SPDX_DIR, spdx + ".txt")).rstrip("\n"))
    out.append(RULE)
    out.append("")
    out.append("TEXTS SHIPPED WITH THE PACKAGES")
    out.append("")
    for digest in order:
        label, hint, text = texts[digest]
        out.append(RULE)
        out.append("[%s] first shipped with %s" % (label, hint))
        out.append(THIN)
        out.append(text.rstrip("\n"))
    out.append(RULE)

    body = "\n".join(out) + "\n"
    with open(args.out, "wb") as f:
        f.write(body.encode("utf-8"))
    print("wrote %s — %d packages, %d shipped texts, SPDX: %s"
          % (args.out, len(entries), len(order), ", ".join(sorted(spdx_used))))


if __name__ == "__main__":
    main()
