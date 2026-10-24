"""Build the interim report from Markdown and verified capture receipts.

Write outside Unity Assets; use ReportLab, then render and inspect every PDF page.
"""
import argparse
import html
import json
import re
from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.pagesizes import letter
from reportlab.lib.styles import ParagraphStyle
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, PageBreak, Table, TableStyle, Image, KeepTogether


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--evidence', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    repo = Path(__file__).resolve().parents[1]
    shots = args.evidence / 'screenshots'
    receipts = json.loads((shots / 'capture-receipts.json').read_text(encoding='utf-8-sig'))
    assert len(receipts) == 12 and all(r['ExitCode'] == 0 and r['PassGroups'] == 9 for r in receipts)
    # Prefer relocated files; receipts retain original paths.
    shot_map = {r['Variant']: shots / r['Screenshot'].replace('\\', '/').split('/')[-1] for r in receipts}
    assert all(p.is_file() for p in shot_map.values()), 'Capture PNG missing from supplied evidence folder'
    metrics = []
    active = []
    for r in receipts:
        log = shot_map[r['Variant']].with_suffix('.log').read_text(encoding='utf-8-sig')
        match = re.search(r'DTET_PERFORMANCE samples=90 opticsCPU_mean_ms=([\d.]+) opticsCPU_p95_ms=([\d.]+) frame_mean_ms=([\d.]+) frame_p95_ms=([\d.]+)', log)
        motion = re.search(r'DTET_OPTICS_ACTIVE samples=90 CPU_mean_ms=([\d.]+) CPU_p95_ms=([\d.]+)', log)
        assert match and motion, 'Final cache-aware measurements missing'
        metrics.append(tuple(map(float, match.groups())))
        active.append(tuple(map(float, motion.groups())))
    build_path = args.evidence / 'unity-audit-build-verified.log'
    if not build_path.exists():
        build_path = args.evidence / 'evidence' / 'unity-audit-build-verified.log'
    build_log = build_path.read_text(encoding='utf-8-sig')
    assert 'DTET_QA_BUILD_SUCCEEDED' in build_log and 'return code 0' in build_log
    assert not re.search(r'error CS\d|Shader error', build_log)
    sizes = [(folder, sum(p.stat().st_size for p in (repo / 'BinaryBlackHole' / folder).rglob('*') if p.is_file())) for folder in ['Assets', 'Builds', 'Library']]
    verification = '| Check | Observed result | Boundary |\n| --- | --- | --- |\n| Portable regressions | 6 PASS groups | Model/clock/optics/input/choreography fixtures, not scene physics |\n| Unity QA build | Exit 0; 5 shaders validated | Windows development player, not headset build |\n| Player feature captures | 12 variants, 9 PASS groups each | Injected input/command checks, not human evaluation |\n| Screenshot QA | 1600x900 original PNGs | Layout reviewed; other resolutions not certified |'
    size_table = '| Folder | Bytes | Interpretation |\n| --- | --- | --- |\n' + '\n'.join(f'| {folder} | {size:,} ({size / 1048576:.2f} MiB) | {"Source assets" if folder == "Assets" else "Generated output/cache"} |' for folder, size in sizes)
    perf = (f'On the RTX 3060 Ti Windows workstation, each variant sampled 90 stationary frames: optical CPU means ranged {min(x[0] for x in metrics):.3f}-{max(x[0] for x in metrics):.3f} ms; optical p95 {min(x[1] for x in metrics):.3f}-{max(x[1] for x in metrics):.3f} ms. '
            f'The separate moving-lens runs sampled 90 frames each: CPU means {min(x[0] for x in active):.3f}-{max(x[0] for x in active):.3f} ms; p95 {min(x[1] for x in active):.3f}-{max(x[1] for x in active):.3f} ms. '
            'These are short development-player CPU diagnostics, not a GPU profiler, sustained benchmark or VR frame-budget pass. Live binary optics remains a substantial cost for future target-device optimization.')
    text = (repo / 'Docs' / 'PROTOTYPE_AUDIT_REPORT.md').read_text(encoding='utf-8')
    text = text.replace('{{VERIFICATION_TABLE}}', verification).replace('{{SIZE_TABLE}}', size_table).replace('{{PERFORMANCE_SUMMARY}}', perf)
    args.output.mkdir(parents=True, exist_ok=True)
    # Use portable screenshot paths in the delivered copy.
    expanded = re.sub(r'\(audit:([^)]*)\)', lambda m: '(screenshots/' + shot_map[m[1]].name + ')', text)
    (args.output / 'DTET_Prototype_Audit_Report.md').write_text(expanded, encoding='utf-8')
    pdfmetrics.registerFont(TTFont('Audit', 'C:/Windows/Fonts/arial.ttf'))
    pdfmetrics.registerFont(TTFont('AuditBold', 'C:/Windows/Fonts/arialbd.ttf'))
    pdfmetrics.registerFontFamily('Audit', normal='Audit', bold='AuditBold', italic='Audit', boldItalic='AuditBold')
    styles = {
        'body': ParagraphStyle('body', fontName='Audit', fontSize=10.5, leading=14.1, spaceAfter=9, textColor=colors.black),
        'h1': ParagraphStyle('h1', fontName='AuditBold', fontSize=19, leading=23, spaceAfter=15, textColor=colors.black, keepWithNext=True),
        'h2': ParagraphStyle('h2', fontName='AuditBold', fontSize=12.8, leading=16, spaceBefore=7, spaceAfter=8, textColor=colors.black, keepWithNext=True),
        'caption': ParagraphStyle('caption', fontName='Audit', fontSize=9.1, leading=12.2, spaceAfter=12, textColor=colors.HexColor('#334155')),
        'cell': ParagraphStyle('cell', fontName='Audit', fontSize=9.1, leading=12, textColor=colors.black),
        'cellhead': ParagraphStyle('cellhead', fontName='AuditBold', fontSize=9.1, leading=12, textColor=colors.black),
    }
    def rich(value):
        value = html.escape(value)
        return re.sub(r'\[([^]]+)\]\((https?://[^)]+)\)', r'<link href="\2" color="#155E75">\1</link>', value)
    flow = []
    figure_count = 0
    for page_index, page in enumerate(text.split('<!-- PAGE -->')):
        if page_index:
            flow.append(PageBreak())
        lines = page.strip().splitlines()
        i = 0
        while i < len(lines):
            line = lines[i].strip()
            if not line:
                i += 1
                continue
            if line.startswith('|'):
                rows = []
                while i < len(lines) and lines[i].strip().startswith('|'):
                    raw = lines[i].strip().strip('|').split('|')
                    if not all(re.fullmatch(r'\s*:?-+:?\s*', x) for x in raw):
                        rows.append([Paragraph(rich(x.strip()), styles['cellhead' if not rows else 'cell']) for x in raw])
                    i += 1
                table = Table(rows, colWidths=[172,172,184], repeatRows=1, hAlign='LEFT')
                table.setStyle(TableStyle([('VALIGN',(0,0),(-1,-1),'TOP'),('LINEBELOW',(0,0),(-1,0),0.8,colors.HexColor('#64748B')),('LINEBELOW',(0,1),(-1,-1),0.35,colors.HexColor('#CBD5E1')),('LEFTPADDING',(0,0),(-1,-1),5),('RIGHTPADDING',(0,0),(-1,-1),5),('TOPPADDING',(0,0),(-1,-1),6),('BOTTOMPADDING',(0,0),(-1,-1),6)]))
                flow.extend([table, Spacer(1,12)])
                continue
            image_match = re.fullmatch(r'!\[(.*)\]\(audit:([^)]*)\)', line)
            if image_match:
                caption, variant = image_match.groups()
                figure_count += 1
                # Keep captions readable on two-image pages.
                width = 480 if page.count('](audit:') == 2 else 528
                im = Image(str(shot_map[variant]), width=width, height=width*9/16)
                im.hAlign = 'LEFT'
                flow.append(KeepTogether([im, Spacer(1,7), Paragraph(rich(caption),styles['caption'])]))
            elif line.startswith('# '):
                flow.append(Paragraph(rich(line[2:]),styles['h1']))
            elif line.startswith('## '):
                flow.append(Paragraph(rich(line[3:]),styles['h2']))
            else:
                flow.append(Paragraph(rich(line),styles['body']))
            i += 1
    assert figure_count == 12
    def footer(canvas, doc):
        canvas.saveState()
        canvas.setFont('Audit',8)
        canvas.setFillColor(colors.HexColor('#475569'))
        canvas.drawString(42,24,'DT-ET Track A | Interim audit | 5 October 2026')
        canvas.drawRightString(570,24,str(doc.page))
        canvas.restoreState()
    destination = args.output / 'DTET_Prototype_Audit_Report.pdf'
    doc = SimpleDocTemplate(str(destination), pagesize=letter, rightMargin=42,leftMargin=42,topMargin=38,bottomMargin=42,title='DT-ET prototype audit and development report',author='DT-ET student team; Codex-assisted interim audit')
    doc.build(flow,onFirstPage=footer,onLaterPages=footer)
    print(f'Created {destination}; figures={figure_count}')


if __name__ == '__main__':
    main()
