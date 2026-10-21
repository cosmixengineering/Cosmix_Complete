import xml.etree.ElementTree as ET

def build_svg():
    width = 1600
    height = 1940

    lines = []
    lines.append(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} {height}" width="{width}" height="{height}" style="--bg:#1F1F1F;--fg:#CCCCCC;--line:#AAAAAA;--accent:#FFFFFF;--muted:#888888;--surface:#181818;--border:#444444;--highlight:#E5E5E5;background:var(--bg)">')
    lines.append('<style>')
    lines.append("  @import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&amp;display=swap');")
    lines.append("  text { font-family: 'Inter', system-ui, -apple-system, sans-serif; }")
    lines.append('  .title { font-size: 20px; font-weight: 800; fill: #FFFFFF; letter-spacing: 0.5px; }')
    lines.append('  .subtitle { font-size: 12px; font-weight: 500; fill: #999999; }')
    lines.append('  .section-hdr { font-size: 11.5px; font-weight: 700; fill: #CCCCCC; letter-spacing: 0.8px; text-transform: uppercase; }')
    lines.append('  .card-tag { font-size: 10px; font-weight: 800; fill: #888888; letter-spacing: 0.5px; text-transform: uppercase; }')
    lines.append('  .card-tag-gate { font-size: 10px; font-weight: 800; fill: #F59E0B; letter-spacing: 0.5px; text-transform: uppercase; }')
    lines.append('  .card-tag-approval { font-size: 10px; font-weight: 800; fill: #A78BFA; letter-spacing: 0.5px; text-transform: uppercase; }')
    lines.append('  .card-title { font-size: 13.5px; font-weight: 700; fill: #FFFFFF; }')
    lines.append('  .card-desc { font-size: 11px; font-weight: 400; fill: #AAAAAA; line-height: 1.45; }')
    lines.append('  .card-bullet { font-size: 11px; font-weight: 400; fill: #BBBBBB; }')
    lines.append('  .label-text { font-size: 10.5px; font-weight: 600; fill: #CCCCCC; }')
    lines.append('</style>')
    
    # Defs
    lines.append('<defs>')
    lines.append('  <marker id="arrow" markerWidth="8" markerHeight="6" refX="7" refY="3" orient="auto">')
    lines.append('    <polygon points="0 0, 8 3, 0 6" fill="#CCCCCC" />')
    lines.append('  </marker>')
    lines.append('  <marker id="arrow-gold" markerWidth="8" markerHeight="6" refX="7" refY="3" orient="auto">')
    lines.append('    <polygon points="0 0, 8 3, 0 6" fill="#F59E0B" />')
    lines.append('  </marker>')
    lines.append('</defs>')

    # 1. Main Header Box
    lines.append('<!-- Main Header -->')
    lines.append('<g class="main-header">')
    lines.append(f'  <rect x="40" y="30" width="1520" height="75" rx="0" ry="0" fill="#181818" stroke="#333333" stroke-width="1" />')
    lines.append('  <text x="65" y="60" class="title">COSMIX CONNECTED ERP &#8212; FROM ENQUIRY TO PROJECT PROFIT</text>')
    lines.append('  <text x="65" y="84" class="subtitle">12-Step Architecture, 4 Milestone Cash Gates, Finance-CEO Approvals &amp; Dual-Ledger Governance &#8226; CEO Review Draft</text>')
    lines.append('  <rect x="1380" y="48" width="150" height="30" rx="0" ry="0" fill="#242424" stroke="#444444" stroke-width="1" />')
    lines.append('  <text x="1455" y="67" text-anchor="middle" font-size="11" font-weight="700" fill="#E5E5E5">CEO BASELINE v1.0</text>')
    lines.append('</g>')

    # Helper function for Subgraph
    def add_subgraph(gx, gy, gw, gh, label):
        lines.append(f'<g class="subgraph">')
        lines.append(f'  <rect x="{gx}" y="{gy}" width="{gw}" height="{gh}" rx="0" ry="0" fill="#181818" stroke="#383838" stroke-width="1" />')
        lines.append(f'  <rect x="{gx}" y="{gy}" width="{gw}" height="30" rx="0" ry="0" fill="#242424" stroke="#383838" stroke-width="1" />')
        lines.append(f'  <text x="{gx + 18}" y="{gy + 19}" class="section-hdr">{label}</text>')
        lines.append('</g>')

    # Helper function for Node Cards
    def add_card(cx, cy, cw, ch, tag, tag_type, title, bullets, is_gate=False):
        stroke_color = "#F59E0B" if is_gate else "#444444"
        bg_color = "#1D1A14" if is_gate else "#1F1F1F"
        tag_class = "card-tag-gate" if is_gate else ("card-tag-approval" if "APPROVAL" in tag or "CEO" in tag else "card-tag")

        lines.append(f'<g class="node-card">')
        lines.append(f'  <rect x="{cx}" y="{cy}" width="{cw}" height="{ch}" rx="0" ry="0" fill="{bg_color}" stroke="{stroke_color}" stroke-width="{1.5 if is_gate else 1}" />')
        lines.append(f'  <text x="{cx + 16}" y="{cy + 24}" class="{tag_class}">{tag}</text>')
        lines.append(f'  <text x="{cx + 16}" y="{cy + 45}" class="card-title">{title}</text>')
        
        cur_y = cy + 70
        for b in bullets:
            clean_b = b.replace('&bull;', '&#8226;').replace('&mdash;', '&#8212;').replace('&minus;', '-').replace('&plusmn;', '&#177;')
            lines.append(f'  <text x="{cx + 16}" y="{cur_y}" class="card-bullet">{clean_b}</text>')
            cur_y += 18
        lines.append('</g>')

    # Helper for horizontal connector arrow
    def add_h_arrow(x1, y1, x2, y2, label=None, label_y=None, is_gate=False):
        marker = "url(#arrow-gold)" if is_gate else "url(#arrow)"
        stroke = "#F59E0B" if is_gate else "#777777"
        lines.append(f'<line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke="{stroke}" stroke-width="1.2" marker-end="{marker}" />')
        if label:
            ly = label_y if label_y else y1 - 8
            mid_x = (x1 + x2) / 2
            lbl_w = len(label) * 6.5 + 16
            lines.append(f'<rect x="{mid_x - lbl_w/2}" y="{ly - 11}" width="{lbl_w}" height="18" fill="#1F1F1F" stroke="#333333" stroke-width="0.75" />')
            lines.append(f'<text x="{mid_x}" y="{ly + 2}" text-anchor="middle" class="label-text">{label}</text>')

    # -------------------------------------------------------------
    # ROW 1: PHASE 01 - AGREE & ACTIVATE
    # -------------------------------------------------------------
    add_subgraph(40, 125, 1520, 240, "PHASE 01: AGREE &amp; ACTIVATE &#8212; CLIENT ENGAGEMENT, ESTIMATION &amp; CONTRACT LOCK")
    add_card(65, 170, 335, 175, "01 CRM / INTAKE / ENGINEERING", "normal", "Enquiry, BOQ &amp; Cost Estimate", [
        "&#8226; Capture project scope, drawings &amp; quantities",
        "&#8226; Technical estimation &amp; direct cost pricing",
        "&#8226; Select delivery route: Supply-Only vs Install",
        "&#8226; Baseline commercial assumptions"
    ])
    add_card(445, 170, 335, 175, "02 COMMERCIAL / FINANCE / CEO", "approval", "Quote &amp; Payment Schedule", [
        "&#8226; Review HS code tariffs, WHT &amp; sales taxes",
        "&#8226; Define milestone triggers (M1, M2, M3, M4)",
        "&#8226; Approval sequence: Finance &#8594; CEO final",
        "&#8226; Customer formal contract sign-off"
    ])
    add_card(825, 170, 335, 175, "03 CUSTOMER PAYMENT M1", "gate", "Advance Payment + Clearance", [
        "&#8226; Adjustable advance % per approved contract",
        "&#8226; Bank receipt &amp; payment allocation",
        "&#8226; Finance Clearance Gate Check",
        "&#8226; Mandatory unlock before project activation"
    ], is_gate=True)
    add_card(1205, 170, 335, 175, "04 PROJECT MANAGEMENT", "normal", "Activate the Project Order", [
        "&#8226; Single dedicated PM oversees full lifecycle",
        "&#8226; Sets direct-cost budget &amp; milestone schedule",
        "&#8226; Freezes project material list &amp; BOM",
        "&#8226; Initiates procurement handoff (Continue at A)"
    ])

    # Connectors Row 1
    add_h_arrow(400, 257, 445, 257)
    add_h_arrow(780, 257, 825, 257, "Schedule Approved")
    add_h_arrow(1160, 257, 1205, 257, "Advance Cleared", is_gate=True)

    # -------------------------------------------------------------
    # ROW 2: PHASE 02 - SOURCE & BUILD
    # -------------------------------------------------------------
    add_subgraph(40, 395, 1520, 240, "PHASE 02: SOURCE &amp; BUILD &#8212; WAREHOUSE RECEIVING, MATERIAL CASH GATE &amp; FACTORY QC")
    add_card(65, 440, 335, 175, "05 STORES / PROCUREMENT", "normal", "Plan Materials &amp; Buy Shortages", [
        "&#8226; Check warehouse inventory &amp; reserve items",
        "&#8226; Purchase requisitions for shortages",
        "&#8226; PO Approval Chain: Finance &#8594; CEO final",
        "&#8226; Issue PO to verified supplier"
    ])
    add_card(445, 440, 335, 175, "06 STORES / RECEIVING", "normal", "All Project Materials Arrive", [
        "&#8226; Goods Receipt Note (GRN) &amp; quality check",
        "&#8226; Partial deliveries wait in warehouse stock",
        "&#8226; 100% project material arrival validated",
        "&#8226; System triggers Milestone M2 invoice"
    ])
    add_card(825, 440, 335, 175, "07 CUSTOMER PAYMENT M2", "gate", "Material Payment + Clearance", [
        "&#8226; Due after all project materials at Cosmix",
        "&#8226; Customer pays warehouse material invoice",
        "&#8226; Finance verifies bank receipt allocation",
        "&#8226; Clears factory production release gate"
    ], is_gate=True)
    add_card(1205, 440, 335, 175, "08 MANUFACTURING / QUALITY", "normal", "Complete Production &amp; Pass QC", [
        "&#8226; Material consumption, direct labor &amp; WIP log",
        "&#8226; Factory QA &amp; structural inspection",
        "&#8226; Failed QC: returns to rework &amp; re-inspection",
        "&#8226; Passed QC triggers M3 (Continue at B)"
    ])

    # Vertical Link A from Step 4 to Step 5
    lines.append('<!-- Link A -->')
    lines.append('<path d="M 1372 345 L 1372 380 L 232 380 L 232 440" fill="none" stroke="#777777" stroke-width="1.2" marker-end="url(#arrow)" />')
    lines.append('<rect x="730" y="370" width="140" height="18" fill="#1F1F1F" stroke="#333333" stroke-width="0.75" />')
    lines.append('<text x="800" y="383" text-anchor="middle" class="label-text">Connection A: PM to Stores</text>')

    # Connectors Row 2
    add_h_arrow(400, 527, 445, 527, "PO Issued")
    add_h_arrow(780, 527, 825, 527, "100% GRN Complete")
    add_h_arrow(1160, 527, 1205, 527, "M2 Cleared", is_gate=True)

    # -------------------------------------------------------------
    # ROW 3: PHASE 03 - DELIVER & CLOSE
    # -------------------------------------------------------------
    add_subgraph(40, 665, 1520, 255, "PHASE 03: DELIVER &amp; CLOSE &#8212; DISPATCH CLEARANCE, SITE ACCEPTANCE &amp; FINANCIAL HANDOVER")
    add_card(65, 710, 335, 190, "09 CUSTOMER PAYMENT M3", "gate", "Production Payment + Clearance", [
        "&#8226; Due after production &amp; Factory QC pass",
        "&#8226; Customer pays production installment",
        "&#8226; Finance validates payment clearance",
        "&#8226; Mandatory release gate for dispatch"
    ], is_gate=True)
    add_card(445, 710, 335, 190, "10 FULFILLMENT", "normal", "Dispatch &amp; Confirm Delivery", [
        "&#8226; Delivery order / official delivery challan",
        "&#8226; Secure packaging, logistics &amp; shipment",
        "&#8226; Proof of delivery (POD) recorded",
        "&#8226; Customer digital OTP / delivery confirmation"
    ])
    add_card(825, 710, 335, 190, "11 DELIVERY OR INSTALLATION", "normal", "Route Acceptance &amp; M4 Due", [
        "&#8226; Route 11A (Supply Only): M4 due after delivery",
        "  confirmation &amp; site OTP verification",
        "&#8226; Route 11B (Installation): Site installation",
        "  + testing + customer signed completion cert",
        "&#8226; Final installment never blocks work"
    ])
    add_card(1205, 710, 335, 190, "12 FINANCE / PROJECT MANAGER", "normal", "Collect Final Payment &amp; Close", [
        "&#8226; Collect final balance (Milestone M4)",
        "&#8226; Reconcile direct costs, bills &amp; customer GL",
        "&#8226; PM completes formal handover to client",
        "&#8226; Warranty &amp; complaints stay permanently linked",
        "&#8226; PROJECT CLOSED &amp; PROFIT LOCKED"
    ])

    # Vertical Link B from Step 8 to Step 9
    lines.append('<!-- Link B -->')
    lines.append('<path d="M 1372 615 L 1372 650 L 232 650 L 232 710" fill="none" stroke="#777777" stroke-width="1.2" marker-end="url(#arrow)" />')
    lines.append('<rect x="730" y="640" width="140" height="18" fill="#1F1F1F" stroke="#333333" stroke-width="0.75" />')
    lines.append('<text x="800" y="653" text-anchor="middle" class="label-text">Connection B: QC to Dispatch</text>')

    # Connectors Row 3
    add_h_arrow(400, 805, 445, 805, "M3 Cleared", is_gate=True)
    add_h_arrow(780, 805, 825, 805, "Shipped to Site")
    add_h_arrow(1160, 805, 1205, 805, "Acceptance Signed")

    # -------------------------------------------------------------
    # ROW 4: FINANCE GATE PROTOCOL (PAGE 2)
    # -------------------------------------------------------------
    add_subgraph(40, 950, 1520, 275, "FINANCE GATE PROTOCOL &#8212; WHAT HAPPENS AT EVERY MILESTONE GATE (M1, M2, M3, M4)")
    
    # Box 1: Trigger
    add_card(65, 995, 290, 205, "GATE TRIGGER", "normal", "Milestone Becomes Due", [
        "&#8226; Contract signed (M1)",
        "&#8226; All materials arrived (M2)",
        "&#8226; Production &amp; QC passed (M3)",
        "&#8226; Delivery / Installation accepted (M4)",
        "&#8226; System generates milestone billing"
    ])

    # Box 2: Finance Check
    add_card(405, 995, 330, 205, "FINANCE AUDIT", "approval", "Are Amounts Due Cleared?", [
        "&#8226; Verify bank credit advice &amp; cash receipt",
        "&#8226; Reconcile customer ledger &amp; allocations",
        "&#8226; Check advance balance &amp; outstanding bills",
        "&#8226; Review tax / WHT deduction challans",
        "&#8226; Decision: Pass, Hold, or Exception"
    ])

    # Box 3: Release (YES)
    add_card(785, 995, 345, 95, "OUTCOME: YES", "gate", "Release Specified Stage", [
        "&#8226; Stage unlocked (Project, Production, Dispatch)",
        "&#8226; Future unbilled installments do NOT hold gate"
    ])

    # Box 4: Hold (NO)
    add_card(785, 1105, 345, 95, "OUTCOME: NO / OVERDUE", "normal", "Hold Next Stage &#9940;", [
        "&#8226; Operational work paused at current gate",
        "&#8226; Hold remains until payment or formal exception"
    ])

    # Box 5: Exception Route
    add_card(1180, 995, 360, 205, "EXECUTIVE EXCEPTION", "approval", "Finance Exception Route", [
        "&#8226; Finance approves operational continuation",
        "&#8226; Must preserve audit trail in ERP:",
        "  - Exact amount due &amp; overdue aging",
        "  - Documented business reason &amp; evidence",
        "  - Specific Finance approver identity",
        "&#8226; Customer balance remains strictly due"
    ])

    # Finance Gate Connectors
    add_h_arrow(355, 1097, 405, 1097, "Trigger Fired")
    lines.append('<path d="M 735 1042 L 785 1042" fill="none" stroke="#10B981" stroke-width="1.2" marker-end="url(#arrow)" />')
    lines.append('<text x="760" y="1035" text-anchor="middle" font-size="10" font-weight="700" fill="#10B981">YES</text>')
    
    lines.append('<path d="M 735 1152 L 785 1152" fill="none" stroke="#EF4444" stroke-width="1.2" marker-end="url(#arrow)" />')
    lines.append('<text x="760" y="1145" text-anchor="middle" font-size="10" font-weight="700" fill="#EF4444">NO</text>')

    lines.append('<path d="M 1130 1152 L 1155 1152 L 1155 1097 L 1180 1097" fill="none" stroke="#A78BFA" stroke-width="1.2" marker-end="url(#arrow)" />')
    lines.append('<text x="1155" y="1085" text-anchor="middle" font-size="9.5" font-weight="700" fill="#A78BFA">Override</text>')

    # -------------------------------------------------------------
    # ROW 5: DUAL LEDGERS & INVENTORY FLOW (PAGE 3)
    # -------------------------------------------------------------
    add_subgraph(40, 1255, 1520, 310, "CONNECTED DUAL LEDGERS &amp; INVENTORY COST FLOW (IFRS 15 &amp; IAS 2 COMPLIANT)")

    # Sub-column 1: Supplier Side
    add_card(65, 1300, 470, 245, "SUPPLIER SIDE (PAYABLES LEDGER)", "normal", "Materials &amp; Supplier Payables", [
        "1. Approve Supplier PO (Procurement &#8594; Finance &#8594; CEO)",
        "2. Goods Receipt Note (GRN) + Quantity &amp; Spec Inspection",
        "3. Match Supplier Bill (3-Way Match: PO + GRN + Vendor Bill)",
        "4. Approve Payable (Resolve price/tax/quantity variances)",
        "5. Pay Supplier (Bank advice reduces payable &amp; cash balance)",
        "&#8226; Every document links project code, item code &amp; PO reference"
    ])

    # Sub-column 2: Inventory Value Flow
    add_card(565, 1300, 470, 245, "INVENTORY VALUE &amp; COSTING ENGINE", "normal", "WIP &amp; Valuation Accumulation", [
        "1. Raw Materials: Accepted stock + eligible landed costs (duty/freight)",
        "2. Work in Progress (WIP): Consumed stock + assigned direct labour",
        "3. Finished Goods: Completed assembly; valuation transferred once",
        "4. Recognized Project Cost: Matched strictly to earned project revenue",
        "&#8226; IAS 2 compliant inventory cost &amp; expense timing standards",
        "&#8226; Purchasing and consumption are never double-counted"
    ])

    # Sub-column 3: Customer Side
    add_card(1065, 1300, 470, 245, "CUSTOMER SIDE (RECEIVABLES LEDGER)", "normal", "Billing, Collections &amp; Revenue", [
        "1. Customer Milestone: Fired from approved contract schedule",
        "2. Billing Document: Generated under contract terms &amp; tax rules",
        "3. Receivable / Advance: Maintained in distinct ledger categories",
        "4. Receive &amp; Allocate: Bank proof linked to specific invoice slots",
        "5. Reconcile Balance: Due vs Paid vs Outstanding balance",
        "&#8226; IFRS 15 revenue recognition separated from cash collection timing"
    ])

    # -------------------------------------------------------------
    # ROW 6: PROFIT SEPARATION & GOVERNANCE (PAGE 4 & 5)
    # -------------------------------------------------------------
    add_subgraph(40, 1595, 1520, 285, "PROFIT ARCHITECTURE &amp; EXECUTIVE GOVERNANCE (PROJECT MARGIN VS COMPANY PROFIT)")

    add_card(65, 1640, 720, 220, "PROJECT P&amp;L (DIRECT-COST VIEW)", "normal", "Project Margin Formula &amp; Direct Cost Boundary", [
        "FORMULA: Recognized Project Revenue - Matched Direct Project Costs = Project Margin",
        "",
        "&#8226; OWN COSTS INCLUDED (Captured strictly against project ID):",
        "  - Consumed materials (net of warehouse returns and scrap)",
        "  - Directly assigned project labour &amp; subcontracting invoices",
        "  - Direct transport, logistics, lifting &amp; site equipment fuel",
        "  - Site installation labour, travel expenses &amp; evidenced rework costs",
        "",
        "&#8226; OVERHEAD EXCLUSION: Office rent, corporate utilities and general admin salaries",
        "  are strictly excluded from project margin (they reside in company P&amp;L)."
    ])

    add_card(815, 1640, 720, 220, "COMPANY P&amp;L &amp; EXECUTIVE APPROVAL MATRIX", "approval", "Full Company Reporting &amp; CEO Approval Hierarchy", [
        "FORMULA: Company Revenue - All Project Costs - Company Overheads &#177; Other Income = Net Profit",
        "",
        "&#8226; MANDATORY 3-TIER APPROVAL SEQUENCES (CONFIRMED):",
        "  1. Customer Payment Schedule: Commercial prepares &#8594; Finance approves &#8594; CEO final approval",
        "  2. Supplier Purchases: Requisition / PO &#8594; Finance approves budget &#8594; CEO final approval",
        "  3. Scope / Price Changes: Customer accepts revised price &#8594; Finance approves &#8594; CEO final",
        "  4. Payment Hold Exception: Finance approves continuation with logged audit evidence",
        "",
        "&#8226; CEO DASHBOARD: Project margin, budget vs actual, cash &amp; overdue balances, stock &amp; WIP value."
    ])

    # Footer
    lines.append(f'<text x="{width/2}" y="1915" text-anchor="middle" font-size="11" font-weight="500" fill="#666666">COSMIX CONNECTED ERP &#8226; BUSINESS PROCESS ARCHITECTURE &#8226; DERIVED DIRECTLY FROM FINANCE ABC.PDF</text>')
    lines.append('</svg>')

    full_svg = '\n'.join(lines)
    # Strictly validate XML
    ET.fromstring(full_svg)
    return full_svg

if __name__ == '__main__':
    svg_data = build_svg()
    
    paths = [
        r"C:\Users\Huma.Khan\Desktop\cosmix-finance-diagram.svg",
        r"C:\Users\Huma.Khan\Desktop\mermaid-diagram.svg",
        r"C:\Users\Huma.Khan\Downloads\cosmix-finance-diagram.svg",
        r"C:\Users\Huma.Khan\Downloads\mermaid-diagram.svg",
    ]
    
    for p in paths:
        with open(p, "w", encoding="utf-8") as f:
            f.write(svg_data)
        print(f"Valid XML verified and saved: {p}")
