# -*- coding: utf-8 -*-
"""
(جاهزيّةُ الإطلاق · ح-٣٨) ‏**مُشغِّلُ البرهان الأحمر — وثلاثةُ شواهدَ لا واحد.**

‏البرهانُ الأحمر أن يُنقل التغييرُ كاملاً إلى شجرةٍ معزولة، **ثمّ يُنزع السطرُ السببيُّ
وحدَه**، فيكون السقوطُ منسوباً إلى قرارٍ واحدٍ لا إلى الحزمة.

‏**ويسقط البرهانُ نفسُه بطريقتين متعاكستين، ووقعتا معاً في ح-٣٧:**

* **أحمرُ كاذب** — النزعُ ترك كوداً لا يُترجَم، **فسقط البناءُ لا الاختبار**
  (`CS1525`). و`EXIT=1` **لا يُفرّق بينهما**.
* **أخضرُ كاذبٌ أسوأ** — النزعُ سقط على `assert` **ومضى السكربتُ**، فشُغِّلت
  الاختباراتُ على كودٍ لم يُمَسّ فخرجت خضراء. **وتُقرأ «الإصلاحُ لا يُميّزه قياس».**

‏**فلا يُقبل برهانٌ إلّا بثلاثة:**

1. ‏**أنّ النزعَ وقع** — الملفُّ يختلف عن الأصل فعلاً (لا «نجح السكربت»).
2. ‏**أنّ البناءَ أنتج حصيلة** — سطرُ `Failed: N, Passed: M` موجود.
3. ‏**أنّ الساقطَ هو المنسوبُ إليه** — بأسماء الاختبارات لا بعددها، **وكلُّ ساقطٍ منسوب**
   (ح-٥٦): صفُّ `[Theory]` يُطبع باسمه ومعاملاته، وكان الشاهدُ لا يراه فيقبل قائمةً فارغة.
   ‏`python scripts/red_proof.py --self-test` يُسقطه إن عمي ثانيةً.

‏الاستعمال:

    python scripts/red_proof.py --unfix <سكربت> --mode <وسم> \
        --files <ملفّات التغيير...> --touched <الملفّ المنزوع> \
        --filter "FullyQualifiedName~..." [--project tests/...csproj]

‏ويخرج بـ`0` **حين يكون البرهانُ صحيحاً وأحمر**، وبغيره حين يبطل.
"""
import argparse
import os
import shutil
import subprocess
import sys
import tempfile
import re

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
TALLY = re.compile(r"Failed:\s*(\d+),\s*Passed:\s*(\d+)")

# ‏(ح-٥٦) **صفُّ النظريّة يُنسَب كالحقيقة.** xUnit يطبع صفَّ `[Theory]` باسمه ومعاملاته:
# ‏`Method(mode: null, …) [FAIL]` — **وكان النمطُ `([A-Za-z0-9_.]+) \[FAIL\]` لا يراه**، فخرج
# ‏برهانُ `precedence` «صحيحاً» بقائمة نسبةٍ فارغة: الشاهدُ الثالثُ أعمى ويقول نعم.
FAIL_ROW = re.compile(r"([A-Za-z0-9_.]+(?:\([^\r\n]*\))?) \[FAIL\]")


def attribute(out, failed):
    """‏الساقطون بأسمائهم — **وكلُّ ساقطٍ منسوب**، وإلّا فسببُ البطلان."""
    rows = [r.split("(")[0].split(".")[-1] + ("(" + r.split("(", 1)[1] if "(" in r else "")
            for r in FAIL_ROW.findall(out)]
    if len(rows) != failed:
        return rows, ("%d test(s) failed but %d [FAIL] row(s) were attributed by name — the "
                      "third witness is blind to the rest" % (failed, len(rows)))
    return rows, None


def self_test():
    """‏(ح-٥٦) **شاهدُ ضبطٍ للشاهد الثالث** — يُسقطه إن عمي عن صفّ نظريّة أو رضي بقائمةٍ فارغة."""
    fact = "[xUnit.net 00:00:01.00]     A.B.C.Fact_Name [FAIL]\n"
    theory = ("[xUnit.net 00:00:01.00]     A.B.C.Rule(mode: null, flag: False) [FAIL]\n"
              "[xUnit.net 00:00:01.10]     A.B.C.Rule(mode: Clearance, flag: (x)) [FAIL]\n")
    checks = [
        ("a fact is attributed", attribute(fact, 1), (["Fact_Name"], None)),
        ("theory rows are attributed", attribute(theory, 2)[0],
         ["Rule(mode: null, flag: False)", "Rule(mode: Clearance, flag: (x))"]),
        ("an unattributed failure voids the proof", attribute(fact, 2)[1] is not None, True),
        ("no rows at all voids the proof", attribute("", 1)[1] is not None, True),
        # ‏والشاهدُ القديمُ كان أعمى هنا — فالضبطُ يقيس الفرقَ لا الشكل.
        ("the old pattern was blind to theory rows",
         re.findall(r"([A-Za-z0-9_.]+) \[FAIL\]", theory), []),
    ]
    bad = [name for name, got, want in checks if got != want]
    for name, got, want in checks:
        print("%s %s" % ("ok  " if got == want else "FAIL", name))
    return 1 if bad else 0


def run(cmd, cwd=None):
    p = subprocess.run(cmd, cwd=cwd, shell=isinstance(cmd, str),
                       capture_output=True)
    return p.returncode, p.stdout.decode("utf-8", "replace") + p.stderr.decode("utf-8", "replace")


def die(msg, code=2):
    sys.stderr.write("BROKEN PROOF: %s\n" % msg)
    sys.exit(code)


def main():
    if sys.argv[1:] == ["--self-test"]:
        return self_test()

    ap = argparse.ArgumentParser()
    ap.add_argument("--unfix", required=True)
    ap.add_argument("--mode", required=True)
    ap.add_argument("--files", nargs="+", required=True,
                    help="‏كلُّ ملفّات التغيير — تُنقل إلى الشجرة المعزولة")
    ap.add_argument("--touched", nargs="+", required=True,
                    help="‏الملفّات التي يجب أن يُغيّرها النزع")
    ap.add_argument("--filter", required=True)
    ap.add_argument("--project", default="tests/Brqosh.Application.Tests/Brqosh.Application.Tests.csproj")
    ap.add_argument("--base", default="HEAD")
    args = ap.parse_args()

    wt = os.path.join(tempfile.gettempdir(), "brqosh-redproof-%s" % args.mode)
    shutil.rmtree(wt, ignore_errors=True)
    run(["git", "worktree", "remove", "--force", wt], cwd=REPO)

    rc, out = run(["git", "worktree", "add", "--detach", wt, args.base], cwd=REPO)
    if rc != 0:
        die("worktree add failed:\n" + out)

    try:
        for rel in args.files:
            src = os.path.join(REPO, rel)
            dst = os.path.join(wt, rel)
            os.makedirs(os.path.dirname(dst), exist_ok=True)
            shutil.copy2(src, dst)

        # ── (١) النزعُ شرطٌ لا محاولة ───────────────────────────────────
        rc, out = run([sys.executable, args.unfix, args.mode, wt])
        sys.stdout.write(out)
        if rc != 0:
            die("the unfix script failed — nothing was reverted, so any test "
                "result below would measure UNMODIFIED code.")

        # ── (٢) وبصمةٌ تُثبت أنّ الملفَّ تغيّر ──────────────────────────
        unchanged = [rel for rel in args.touched
                     if _same(os.path.join(REPO, rel), os.path.join(wt, rel))]
        if unchanged:
            die("the unfix reported success but these files are IDENTICAL to "
                "the original: %s" % ", ".join(unchanged))
        print("[1/3] unfix applied — %s differ(s) from the original" % ", ".join(args.touched))

        # ── (٣) والحصيلةُ تُثبت أنّ الاختباراتِ جرت ────────────────────
        rc, out = run(["dotnet", "test", args.project, "-c", "Release",
                       "--filter", args.filter], cwd=wt)
        tally = TALLY.search(out)
        if not tally:
            # ‏(ح-٥٦) **والمخرَجُ كاملاً إلى ملفّ** — وكان يُختصر في «First errors: unknown»
            # ‏ثمّ تُزال الشجرةُ، فلا يبقى ما يُقرأ منه السببُ.
            log = wt + ".log"
            with open(log, "w", encoding="utf-8") as f:
                f.write(out)
            errs = sorted(set(re.findall(r"error [A-Z]+\d+", out)))
            die("no test tally in the output — the build failed, this is NOT a "
                "red proof. First errors: %s. Full output: %s"
                % (", ".join(errs[:5]) or "unknown", log))

        failed, passed = int(tally.group(1)), int(tally.group(2))
        print("[2/3] build produced a tally — Failed: %d, Passed: %d" % (failed, passed))

        if failed == 0:
            die("the revert changed the code and the suite is STILL GREEN — "
                "nothing distinguishes this change. Either the test does not "
                "reach it, or the change is not load-bearing.")

        names, blind = attribute(out, failed)
        if blind:
            die(blind)

        print("[3/3] attributed failures:")
        for n in names:
            print("       - %s" % n)
        return 0
    finally:
        run(["git", "worktree", "remove", "--force", wt], cwd=REPO)


def _same(a, b):
    try:
        with open(a, "rb") as fa, open(b, "rb") as fb:
            return fa.read() == fb.read()
    except OSError:
        return False


if __name__ == "__main__":
    sys.exit(main())
