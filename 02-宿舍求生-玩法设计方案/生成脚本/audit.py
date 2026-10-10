# -*- coding: utf-8 -*-
"""排版审计：docx 表格宽度 + 7 册工作簿的行高与超长单元格"""
import os, sys, glob
sys.stdout.reconfigure(encoding="utf-8")
from docx import Document
from openpyxl import load_workbook

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TOPBAR = "D:\\HOME\\Game development\\game-prctice\\My project\\面试材料\\宿舍求生"
doc = Document(os.path.join(TOPBAR, "《宿舍求生》玩法设计方案.docx"))

print("=" * 74); print("docx 排版审计（可用正文宽度 17.8 cm）"); print("=" * 74)
for fn in ["《宿舍求生》玩法设计方案.docx", "《宿舍求生》可行性与面试问答.docx", "面试自我介绍与速查卡.docx"]:
    d0 = Document(os.path.join(TOPBAR, fn))
    over = 0
    for t in d0.tables:
        tot = sum(c.width.cm for c in t.rows[0].cells if c.width)
        if tot > 17.85:
            over += 1
            print(f"  {fn} 有超宽表：{len(t.columns)} 列 合计 {tot:.2f} cm")
    print(f"  {fn:<34} 表格 {len(d0.tables):>2} 张，超宽 {over} 张")

print(); print("=" * 74); print("xlsx 排版审计（7 册）"); print("=" * 74)
books = sorted(glob.glob(os.path.join(TOPBAR, "数值表", "*.xlsx")))
mx, tall, longc, tot_sheets = 0, 0, 0, 0
for p in books:
    wb = load_workbook(p)
    n = len(wb.worksheets)
    tot_sheets += n
    bh = 0
    for ws in wb.worksheets:
        for _, dim in ws.row_dimensions.items():
            if dim.height:
                bh = max(bh, dim.height)
                if dim.height >= 300:
                    tall += 1
        for row in ws.iter_rows():
            for c in row:
                if c.value and len(str(c.value)) > 260:
                    longc += 1
    mx = max(mx, bh)
    print(f"  {os.path.basename(p):<34} {n:>2} 页  最高行 {bh:.0f} pt")
print(f"\n  合计 {len(books)} 册 / {tot_sheets} 页（含每册目录页）")
print(f"  最大行高 {mx:.0f} pt（Excel 上限 409）｜触及 300 pt 的单元格 {tall} 个｜超长单元格(>260 字) {longc} 个")
