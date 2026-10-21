import xml.etree.ElementTree as ET

def build_connected_svg():
    width = 1650
    height = 1140

    lines = []
    lines.append(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} {height}" width="{width}" height="{height}" style="--bg:#1F1F1F;--fg:#CCCCCC;--line:#CCCCCC;--accent:#0078D4;--muted:#CCCCCCCC;--surface:#181818;--border:#CCCCCC;background:var(--bg)">')
    lines.append('<style>')
    lines.append("  @import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&amp;display=swap');")
    lines.append("  text { font-family: 'Inter', system-ui, -apple-system, sans-serif; }")
    lines.append('  svg {')
    lines.append('    --_text:          var(--fg);')
    lines.append('    --_text-sec:      var(--muted, color-mix(in srgb, var(--fg) 60%, var(--bg)));')
    lines.append('    --_text-muted:    var(--muted, color-mix(in srgb, var(--fg) 40%, var(--bg)));')
    lines.append('    --_line:          var(--line, color-mix(in srgb, var(--fg) 50%, var(--bg)));')
    lines.append('    --_arrow:         var(--accent, color-mix(in srgb, var(--fg) 85%, var(--bg)));')
    lines.append('    --_node-fill:     var(--surface, color-mix(in srgb, var(--fg) 3%, var(--bg)));')
    lines.append('    --_node-stroke:   var(--border, color-mix(in srgb, var(--fg) 20%, var(--bg)));')
    lines.append('    --_group-fill:    var(--bg);')
    lines.append('    --_group-hdr:     color-mix(in srgb, var(--fg) 5%, var(--bg));')
    lines.append('    --_inner-stroke:  color-mix(in srgb, var(--fg) 12%, var(--bg));')
    lines.append('  }')
    lines.append('</style>')
    lines.append('<defs>')
    lines.append('  <marker id="arrowhead" markerWidth="8" markerHeight="5" refX="7" refY="2.5" orient="auto">')
    lines.append('    <polygon points="0 0, 8 2.5, 0 5" fill="var(--_arrow)" stroke="var(--_arrow)" stroke-width="0.75" stroke-linejoin="round" />')
    lines.append('  </marker>')
    lines.append('  <marker id="arrowhead-start" markerWidth="8" markerHeight="5" refX="1" refY="2.5" orient="auto-start-reverse">')
    lines.append('    <polygon points="8 0, 0 2.5, 8 5" fill="var(--_arrow)" stroke="var(--_arrow)" stroke-width="0.75" stroke-linejoin="round" />')
    lines.append('  </marker>')
    lines.append('</defs>')

    def add_subgraph(sid, label, x, y, w, h):
        lines.append(f'<g class="subgraph" data-id="{sid}" data-label="{label}">')
        lines.append(f'  <rect x="{x}" y="{y}" width="{w}" height="{h}" rx="0" ry="0" fill="var(--_group-fill)" stroke="var(--_node-stroke)" stroke-width="1" />')
        lines.append(f'  <rect x="{x}" y="{y}" width="{w}" height="28" rx="0" ry="0" fill="var(--_group-hdr)" stroke="var(--_node-stroke)" stroke-width="1" />')
        lines.append(f'  <text x="{x + 14}" y="{y + 14}" font-size="12" font-weight="600" fill="var(--_text-sec)" dy="4.2">{label}</text>')
        lines.append('</g>')

    def add_node(nid, label_lines, x, y, w, h):
        lines.append(f'<g class="node" data-id="{nid}">')
        lines.append(f'  <rect x="{x}" y="{y}" width="{w}" height="{h}" rx="0" ry="0" fill="var(--_node-fill)" stroke="var(--_node-stroke)" stroke-width="0.75" />')
        lines.append(f'  <text x="{x + w/2}" y="{y + h/2}" text-anchor="middle" font-size="12.5" font-weight="500" fill="var(--_text)">')
        
        n = len(label_lines)
        start_dy = -((n - 1) * 8.5)
        for i, l in enumerate(label_lines):
            dy_val = start_dy if i == 0 else 17
            lines.append(f'    <tspan x="{x + w/2}" dy="{dy_val}">{l}</tspan>')
        lines.append('  </text>')
        lines.append('</g>')

    def add_edge(points, label=None, label_x=None, label_y=None, label_w=None, bi=False):
        pts_str = ' '.join(f'{px},{py}' for px, py in points)
        marker_start = ' marker-start="url(#arrowhead-start)"' if bi else ''
        lines.append(f'<polyline class="edge" points="{pts_str}" fill="none" stroke="var(--_line)" stroke-width="1" marker-end="url(#arrowhead)"{marker_start} />')
        if label and label_x and label_y:
            lw = label_w or (len(label) * 6.8 + 20)
            lh = 26
            lines.append(f'<g class="edge-label">')
            lines.append(f'  <rect x="{label_x - lw/2}" y="{label_y - lh/2}" width="{lw}" height="{lh}" rx="2" ry="2" fill="var(--bg)" stroke="var(--_inner-stroke)" stroke-width="1" />')
            lines.append(f'  <text x="{label_x}" y="{label_y}" text-anchor="middle" font-size="10.5" font-weight="400" fill="var(--_text-sec)" dy="3.8">{label}</text>')
            lines.append('</g>')

    # 1. Top Subgraph: Operational Feeder Modules
    add_subgraph("Operations", "Operational Feeder Modules (Enquiry to Handover Lifecycle)", 40, 30, 1570, 120)
    add_node("Sales", ["Sales, BOQ &amp; Projects", "(Enquiry, Pricing &amp; M1 Client Advance)"], 55, 72, 285, 54)
    add_node("SCM", ["Procurement &amp; SCM", "(PO, Stock Reservation &amp; Shortages)"], 365, 72, 285, 54)
    add_node("Stores", ["Inventory &amp; Stores", "(GRN, Stock Valuation &amp; M2 Trigger)"], 675, 72, 285, 54)
    add_node("Mfg", ["Manufacturing &amp; QC", "(Production WIP, Factory QC &amp; M3)"], 985, 72, 285, 54)
    add_node("Sites", ["Fulfillment &amp; Project Sites", "(Dispatch Challan, Install &amp; M4 Cert)"], 1295, 72, 300, 54)

    # 2. Middle Subgraph: Finance & Accounting Core
    add_subgraph("FinanceCore", "Finance &amp; Accounting Core (Milestone Gates, Tax, Dual Ledgers &amp; Controls)", 40, 270, 1570, 480)
    
    # Core Tier 1 (Gates, CEO Approval, Tax Engine)
    add_node("ApprovalGate", ["Finance Milestone &amp; Budget Gates", "(M1 Advance, M2 Material, M3 Dispatch, M4 Close)"], 65, 318, 455, 62)
    add_node("CEO", ["CEO &amp; Executive Approval Gateway", "(Payment Schedule, Supplier POs &amp; Scope Changes)"], 550, 318, 480, 62)
    add_node("TaxEngine", ["FBR &amp; Customs Duty Engine", "(HS Code Tariffs, WHT, Sales Tax &amp; Invoicing)"], 1055, 318, 540, 62)

    # Core Tier 2 (GL Engine & Project Cost Engine)
    add_node("GL", ["General Ledger (GL) &amp; Double-Entry Engine", "(IAS 2 Inventory WIP &amp; IFRS 15 Revenue Recognition)"], 65, 460, 725, 64)
    add_node("CostEngine", ["Project Cost Accounting Engine", "(Direct Materials, Direct Labor, Transport &amp; Site Costs)"], 825, 460, 770, 64)

    # Core Tier 3 (Dual Ledgers: AP & AR)
    add_node("AP", ["Accounts Payable (AP) &amp; Supplier Ledger", "(3-Way Match: Supplier PO + GRN + Vendor Invoice)"], 65, 600, 725, 64)
    add_node("AR", ["Accounts Receivable (AR) &amp; Customer Ledger", "(Milestone Billing, Advance Allocation &amp; Aging)"], 825, 600, 770, 64)

    # 3. Bottom Subgraph Left: Statutory & Banking Ecosystem
    add_subgraph("External", "Statutory &amp; Banking Ecosystem", 40, 830, 765, 275)
    add_node("FBR", ["FBR Iris &amp; Digital Invoicing API"], 65, 875, 340, 48)
    add_node("PSW", ["Pakistan Single Window (WeBOC)"], 435, 875, 345, 48)
    add_node("Banks", ["Commercial Banks Network", "(Corporate Salary, Vendor ACH &amp; Client RTGS)"], 65, 965, 715, 56)

    # 4. Bottom Subgraph Right: Profitability & Reporting Engine
    add_subgraph("ProfitCore", "Profit Reporting &amp; Executive Intelligence (CEO View)", 840, 830, 770, 275)
    add_node("ProjectPL", ["Project P&amp;L (Direct-Cost View)", "Project Margin = Revenue - Matched Direct Costs", "(Excludes General Office Rent &amp; Admin Salaries)"], 865, 875, 350, 68)
    add_node("CompanyPL", ["Company P&amp;L (Statutory Report)", "Net Profit = Total Revenue - All Costs - Overheads", "(Includes Rent, General Admin Salaries &amp; Tax)"], 1245, 875, 345, 68)
    add_node("Dashboard", ["CEO Real-Time Executive Dashboard", "(Project Margins, Budget vs Actual, Cash Flows, Stock/WIP &amp; QC Holds)"], 865, 975, 725, 56)

    # ================= CONNECTING EDGES & LABELS =================
    
    # Operational to Finance Gates
    # Sales -> ApprovalGate (M1)
    add_edge([(197, 126), (197, 210), (220, 210), (220, 318)], 
             "Client Advance Clearance (M1) &amp; Contract Lock", 220, 210, 300)
    
    # SCM -> CEO / ApprovalGate
    add_edge([(507, 126), (507, 240), (410, 240), (410, 318)],
             "PO Budget Verification &amp; Vendor Bills", 430, 240, 260)
    
    # Stores -> ApprovalGate (M2) & GL
    add_edge([(817, 126), (817, 185), (500, 185), (500, 318)],
             "100% Material Arrival Trigger (M2)", 660, 185, 230)
    
    add_edge([(817, 126), (817, 230), (740, 230), (740, 460)],
             "Landed Cost Capitalization &amp; Stock Receipts", 770, 230, 280)

    # Mfg -> ApprovalGate (M3) & CostEngine
    add_edge([(1127, 126), (1127, 205), (1030, 205), (1030, 460)],
             "QC Pass Clearance (M3) &amp; Factory WIP Log", 1080, 205, 280)

    # Sites -> CostEngine & ApprovalGate (M4)
    add_edge([(1442, 126), (1442, 245), (1350, 245), (1350, 460)],
             "Site Acceptance Certificate (M4) &amp; Site Costs", 1390, 245, 290)

    # Inside Core Connections
    # ApprovalGate -> CEO
    add_edge([(520, 349), (550, 349)])
    
    # CEO -> TaxEngine
    add_edge([(1030, 349), (1055, 349)])

    # ApprovalGate -> GL
    add_edge([(292, 380), (292, 460)], "Milestone Baseline Release", 292, 420, 180)

    # CEO -> AP
    add_edge([(790, 380), (790, 420), (760, 420), (760, 600)], "Approved Supplier POs", 775, 420, 160)

    # TaxEngine -> GL
    add_edge([(1325, 380), (1325, 430), (750, 430), (750, 460)], "Duties &amp; Sales Tax Ledger Postings", 1040, 430, 240)

    # GL -> AP
    add_edge([(292, 524), (292, 600)], "Accruals &amp; Vendor Balance", 292, 562, 180)

    # GL -> CostEngine
    add_edge([(790, 492), (825, 492)], "WIP Direct Cost Transfer", 807, 492, 170)

    # CostEngine -> AR
    add_edge([(1210, 524), (1210, 600)], "Earned Milestone Billing Check", 1210, 562, 210)

    # External & Statutory Connections
    # TaxEngine -> FBR
    add_edge([(1140, 380), (1140, 800), (235, 800), (235, 875)],
             "Real-time e-Invoicing &amp; ATL Check", 685, 800, 230, bi=True)

    # TaxEngine -> PSW
    add_edge([(1460, 380), (1460, 815), (607, 815), (607, 875)],
             "Import Customs Duty &amp; HS Tariff", 1030, 815, 230, bi=True)

    # AP & AR -> Banks
    add_edge([(427, 664), (427, 965)], "Vendor Disbursements &amp; Payroll ACH", 427, 790, 240)
    add_edge([(1050, 664), (1050, 785), (550, 785), (550, 965)], "Client RTGS &amp; Milestone Receipts", 800, 785, 220)

    # Profit Reporting Connections
    # CostEngine -> ProjectPL
    add_edge([(1040, 524), (1040, 875)], "Direct Cost Match (IAS 2)", 1040, 720, 180)

    # GL -> CompanyPL
    add_edge([(600, 524), (600, 765), (1417, 765), (1417, 875)], "Entity-wide Overheads &amp; GL Balance", 1010, 765, 240)

    # ProjectPL & CompanyPL -> Dashboard
    add_edge([(1040, 943), (1040, 975)])
    add_edge([(1417, 943), (1417, 960), (1350, 960), (1350, 975)])

    lines.append('</svg>')

    full_svg = '\n'.join(lines)
    ET.fromstring(full_svg)
    return full_svg

if __name__ == '__main__':
    svg_data = build_connected_svg()
    
    paths = [
        r"C:\Users\Huma.Khan\Desktop\cosmix-finance-diagram.svg",
        r"C:\Users\Huma.Khan\Desktop\mermaid-diagram.svg",
        r"C:\Users\Huma.Khan\Downloads\cosmix-finance-diagram.svg",
        r"C:\Users\Huma.Khan\Downloads\mermaid-diagram.svg",
    ]
    
    for p in paths:
        with open(p, "w", encoding="utf-8") as f:
            f.write(svg_data)
        print(f"Generated & verified: {p}")
