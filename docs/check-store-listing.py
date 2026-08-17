"""스토어 문안 글자 수 검사.

플레이 콘솔은 앱 이름 30자, 짧은 설명 80자, 자세한 설명 4000자를 넘기면
저장 자체를 거부한다. 언어마다 길이가 크게 달라서(독일어·러시아어가 길다)
눈으로는 놓치기 쉬우므로 스크립트로 센다.

    python docs/check-store-listing.py
"""
import io
import re
import sys
import pathlib

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

LIMITS = {"앱 이름": 30, "짧은 설명": 80, "자세한 설명": 4000}
DOC = pathlib.Path(__file__).with_name("store-listing-i18n.md")

text = DOC.read_text(encoding="utf-8")
sections = re.split(r"^## ", text, flags=re.M)[1:]

failures = 0
for section in sections:
    lang = section.split("\n", 1)[0].strip()
    for field, limit in LIMITS.items():
        m = re.search(r"\*\*" + field + r"\*\*\s*\n```\n(.*?)\n```", section, re.S)
        if not m:
            continue
        value = m.group(1)
        n = len(value)
        mark = "OK " if n <= limit else "초과"
        if n > limit:
            failures += 1
        print(f"{mark} {lang:<24} {field:<10} {n:>4} / {limit}")

print()
if failures:
    print(f"!! {failures}건이 제한을 넘었다. 콘솔에서 저장이 거부된다.")
    sys.exit(1)
print("모두 제한 안에 들어간다.")
