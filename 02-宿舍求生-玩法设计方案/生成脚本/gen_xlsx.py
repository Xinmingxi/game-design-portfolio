# -*- coding: utf-8 -*-
"""生成《宿舍求生》v3.0 数值表（按主题拆分为 7 个工作簿 + 各册目录页）"""
import math, os, sys, re
sys.stdout.reconfigure(encoding="utf-8")
from openpyxl import Workbook
from openpyxl.styles import Font, PatternFill, Alignment, Border, Side
from openpyxl.utils import get_column_letter

import common
import data_a, data_b, data_c, data_d   # noqa: F401

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUTDIR = os.path.join(ROOT, "数值表")
os.makedirs(OUTDIR, exist_ok=True)

# ---------------------------------------------------------------
# 分册定义：每册一个主题色，第一张自动生成「表目录」
# ---------------------------------------------------------------
BOOKS = [
    dict(file="《宿舍求生》01 总览与节奏",  accent="1F3A5F", sub="世界观 · 昼夜节奏 · 天气 · 胜负结算",
         keys=["00", "01", "02", "03", "04", "05", "06", "07"]),
    dict(file="《宿舍求生》02 地图与怪谈",  accent="2E5F4F", sub="楼层 · 房型 · 地标 · 怪谈单位与刷新规则",
         keys=["08", "09", "10", "42", "43"]),
    dict(file="《宿舍求生》03 生存与行动",  accent="6B5A1F", sub="生存五轴 · 消耗恢复 · 状态 · 行动与视角",
         keys=["11", "12", "13", "14"]),
    dict(file="《宿舍求生》04 采集建造与陷阱", accent="6B4A2F", sub="材料 · 家具破坏 · 宿舍升级 · 工事 · 陷阱 · 图纸 · 电力 · 搬运",
         keys=["15", "16", "17", "18", "19", "20", "21", "22", "23"]),
    dict(file="《宿舍求生》05 武器与战斗",  accent="6B2F3A", sub="68 件武器 · 代价矩阵 · 工具 · 配方 · 防具 · TTK 与平衡",
         keys=["24", "武器获取来源表", "武器耐久与修复表", "武器磨损与修复规则表", "25", "26", "27", "28", "29", "30", "31", "32"]),
    dict(file="《宿舍求生》06 角色与社交",  accent="4A2F6B", sub="专业与天赋 · 通信信道 · 交易 · 悬赏 · 留言 · 室友名录",
         keys=["33", "34", "35", "36", "37", "38", "51"]),
    dict(file="《宿舍求生》07 主线与通关",  accent="2F5F6B", sub="解谜主线 · BOSS · 双通关判定 · 经济 · 双端差异 · 调参开关",
         keys=["39", "40", "41", "44", "45", "46", "47", "48", "49", "50"]),
]

S = {s["name"]: s for s in common.SHEETS}
def pick(key):
    if key in S:
        return S[key]
    for n, s in S.items():
        if n.startswith(key + "-"):
            return s
    raise KeyError(key)
for b in BOOKS:
    b["sheets"] = [pick(k) for k in b["keys"]]

# ---------------------------------------------------------------
# 校验：52 张表必须被完整分配
# ---------------------------------------------------------------
alloc = [s["name"] for b in BOOKS for s in b["sheets"]]
assert len(alloc) == len(set(alloc)), "有表被重复分配"
missing = set(S) - set(alloc)
assert not missing, f"未被分配的表：{missing}"

# ---------------------------------------------------------------
# 样式
# ---------------------------------------------------------------
FONT = "微软雅黑"
def H(hexc, bold=False, size=10, color=None):
    return Font(name=FONT, size=size, bold=bold, color=color or "FF1F2937")
AL_L = Alignment(horizontal="left", vertical="top", wrap_text=True)
AL_C = Alignment(horizontal="center", vertical="center", wrap_text=True)
AL_R = Alignment(horizontal="right", vertical="center", wrap_text=True)
THIN = Side(style="thin", color="FFD7DEE8")
BORDER = Border(left=THIN, right=THIN, top=THIN, bottom=THIN)

def cjk(s):
    return sum(2 if ord(ch) > 0x2E80 else 1 for ch in str(s))

def est_lines(text, width):
    if text is None or str(text) == "":
        return 1
    per = max(6.0, width * 1.05)
    return sum(max(1, math.ceil(cjk(seg) / per)) for seg in str(text).split("\n"))

def is_num(v):
    return isinstance(v, (int, float)) and not isinstance(v, bool)

# ---------------------------------------------------------------
# 目录页
# ---------------------------------------------------------------
def build_index(wb, book, other_rows):
    ws = wb.create_sheet(title="表目录")
    ws.sheet_properties.tabColor = "C9A227"
    ws.column_dimensions["A"].width = 5
    ws.column_dimensions["B"].width = 26
    ws.column_dimensions["C"].width = 96
    ws.column_dimensions["D"].width = 14
    ws.merge_cells("A1:D1")
    c = ws.cell(row=1, column=1, value=f"《宿舍求生》v3.0 数值表 · {book['file'].split('》')[1].strip()}")
    c.font = Font(name=FONT, size=14, bold=True, color="FF" + book["accent"])
    c.alignment = Alignment(horizontal="left", vertical="center")
    ws.row_dimensions[1].height = 30
    ws.merge_cells("A2:D2")
    c = ws.cell(row=2, column=2 - 1)
    c = ws.cell(row=2, column=1, value=book["sub"])
    c.font = Font(name=FONT, size=10, color="FF475569")
    ws.row_dimensions[2].height = 20

    r = 4
    hdr = ["序号", "表名", "内容说明", "数据行"]
    for j, h in enumerate(hdr, 1):
        cc = ws.cell(row=r, column=j, value=h)
        cc.font = Font(name=FONT, size=10, bold=True, color="FFFFFFFF")
        cc.fill = PatternFill("solid", fgColor="FF" + book["accent"])
        cc.alignment = AL_C
        cc.border = BORDER
    ws.row_dimensions[r].height = 22
    r += 1
    for i, s in enumerate(book["sheets"], 1):
        desc = re.split(r"[。；]", s["desc"])[0][:78]
        vals = [i, s["name"], desc, len(s["rows"])]
        for j, v in enumerate(vals, 1):
            cc = ws.cell(row=r, column=j, value=v)
            cc.font = H(book["accent"], bold=(j == 2), size=10)
            cc.alignment = AL_C if j in (1, 4) else AL_L
            cc.border = BORDER
            if r % 2 == 0:
                cc.fill = PatternFill("solid", fgColor="FFF7FAFC")
        ws.row_dimensions[r].height = max(18, est_lines(desc, 96) * 14 + 6)
        r += 1

    r += 1
    ws.merge_cells(start_row=r, start_column=1, end_row=r, end_column=4)
    cc = ws.cell(row=r, column=1, value="本套数值表共 7 册，全部 52 张表；以下为各分册导航。")
    cc.font = Font(name=FONT, size=10, bold=True, color="FF1F2937")
    r += 1
    for j, h in enumerate(["册号", "工作簿", "包含内容", "表数"], 1):
        cc = ws.cell(row=r, column=j, value=h)
        cc.font = Font(name=FONT, size=10, bold=True, color="FFFFFFFF")
        cc.fill = PatternFill("solid", fgColor="FF475569")
        cc.alignment = AL_C
        cc.border = BORDER
    r += 1
    for no, name, sub, cnt in other_rows:
        for j, v in enumerate([no, name, sub, cnt], 1):
            cc = ws.cell(row=r, column=j, value=v)
            cc.font = H(None, size=10, bold=(j == 2))
            cc.alignment = AL_C if j in (1, 4) else AL_L
            cc.border = BORDER
        ws.row_dimensions[r].height = max(18, est_lines(sub, 96) * 14 + 6)
        r += 1
    ws.freeze_panes = "A5"
    return ws

# ---------------------------------------------------------------
# 主生成
# ---------------------------------------------------------------
def build_sheet(wb, sh, accent):
    ws = wb.create_sheet(title=sh["name"][:31])
    headers, rows = sh["headers"], sh["rows"]
    ncol = len(headers)
    widths = list(sh["widths"] or [])
    if len(widths) < ncol:
        widths += [18] * (ncol - len(widths))
    for i, w in enumerate(widths[:ncol], 1):
        ws.column_dimensions[get_column_letter(i)].width = w

    ws.merge_cells(start_row=1, start_column=1, end_row=1, end_column=ncol)
    c = ws.cell(row=1, column=1, value=sh["desc"])
    c.font = Font(name=FONT, size=10, color="FF334155")
    c.fill = PatternFill("solid", fgColor="FFF1F5F9")
    c.alignment = Alignment(horizontal="left", vertical="center", wrap_text=True)
    ws.row_dimensions[1].height = max(20, est_lines(sh["desc"], sum(widths[:ncol])) * 15 + 6)

    for j, h in enumerate(headers, 1):
        cell = ws.cell(row=2, column=j, value=h)
        cell.font = Font(name=FONT, size=10, bold=True, color="FFFFFFFF")
        cell.fill = PatternFill("solid", fgColor="FF" + accent)
        cell.alignment = AL_C
        cell.border = BORDER
    ws.row_dimensions[2].height = max(24, max(est_lines(h, widths[j - 1]) for j, h in enumerate(headers, 1)) * 14 + 8)

    for r, row in enumerate(rows, start=3):
        mx = 1
        for j in range(ncol):
            v = row[j] if j < len(row) else None
            cell = ws.cell(row=r, column=j + 1, value=v)
            cell.font = H(None)
            cell.border = BORDER
            if j == 0:
                cell.alignment = AL_C
            elif is_num(v):
                cell.alignment = AL_R
            else:
                cell.alignment = AL_L
            if r % 2 == 0:
                cell.fill = PatternFill("solid", fgColor="FFF7FAFC")
            mx = max(mx, est_lines(v, widths[j]))
        ws.row_dimensions[r].height = min(300, max(18, mx * 14 + 6))

    ws.freeze_panes = sh.get("freeze") or "A3"
    ws.auto_filter.ref = f"A2:{get_column_letter(ncol)}{len(rows) + 2}"
    return ws

TAB = ["1F3A5F", "2E5F4F", "6B5A1F", "6B4A2F", "6B2F3A", "4A2F6B", "2F5F6B", "8B5A2B", "5A5A5A"]
other_rows = [(f"{i+1:02d}", b["file"], b["sub"], len(b["sheets"])) for i, b in enumerate(BOOKS)]

total = 0
for i, b in enumerate(BOOKS):
    wb = Workbook(); wb.remove(wb.active)
    build_index(wb, b, other_rows)
    for k, sh in enumerate(b["sheets"]):
        ws = build_sheet(wb, sh, b["accent"])
        ws.sheet_properties.tabColor = TAB[k % len(TAB)]
        ws.sheet_view.zoomScale = 90
    path = os.path.join(OUTDIR, b["file"] + ".xlsx")
    wb.save(path)
    total += len(b["sheets"])
    print(f"  {b['file']}.xlsx  ->  {len(b['sheets'])} 张表 + 目录页")

print(f"\nOK  共 {len(BOOKS)} 册，{total} 张数据表 -> {OUTDIR}")
assert total == 55, total
