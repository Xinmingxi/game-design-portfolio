# -*- coding: utf-8 -*-
"""批量渲染：把 _src 下的 markdown 渲染成对应的 docx + md"""
import os, sys
sys.stdout.reconfigure(encoding="utf-8")
HERE = os.path.dirname(os.path.abspath(__file__))
base = open(os.path.join(HERE, "gen_doc.py"), encoding="utf-8").read()

JOBS = [
    ("doc.md",            "《宿舍求生》玩法设计方案"),
    ("doc_feasibility.md","《宿舍求生》可行性与面试问答"),
    ("doc_intro.md",      "面试自我介绍与速查卡"),
]
for srcname, outname in JOBS:
    code = base
    code = code.replace('SRC = os.path.join(HERE, "doc.md")',
                        'SRC = os.path.join(HERE, r"%s")' % srcname)
    code = code.replace('OUT_DOCX = os.path.join(ROOT, "《宿舍求生》玩法设计方案.docx")',
                        'OUT_DOCX = os.path.join(ROOT, r"%s.docx")' % outname)
    code = code.replace('OUT_MD = os.path.join(ROOT, "《宿舍求生》玩法设计方案.md")',
                        'OUT_MD = os.path.join(ROOT, r"%s.md")' % outname)
    g = {"__file__": os.path.join(HERE, "gen_doc.py")}
    exec(compile(code, "gen_doc.py", "exec"), g)
