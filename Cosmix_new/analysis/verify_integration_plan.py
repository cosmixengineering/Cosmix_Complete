from pathlib import Path
import json
import re
from zipfile import ZipFile
import xml.etree.ElementTree as ET

import pypdfium2 as pdfium
from pypdf import PdfReader
from PIL import Image, ImageDraw, ImageFont


ROOT = Path(r"C:\Users\Huma.Khan\Downloads\Cosmix_Erp_software")
BASE = ROOT / "Cosmix_new/analysis"
QA = BASE / "integration-plan-qa"
DOCX = BASE / "Cosmix_Module_Integration_and_Delivery_Plan_2026_10_12.docx"
PDF = QA / "integration-plan.pdf"
pdf = pdfium.PdfDocument(str(PDF))
reader = PdfReader(str(PDF))
page_data = []
images = []
for index, page in enumerate(pdf):
    bitmap = page.render(scale=1.8)
    output = QA / f"page-{index + 1:03}.png"
    bitmap.to_pil().save(output)
    images.append(output)
    text = reader.pages[index].extract_text()
    textpage = page.get_textpage()
    outside = []
    for char_index in range(textpage.count_chars()):
        char = textpage.get_text_range(char_index, 1)
        if char.isspace():
            continue
        left, bottom, right, top = textpage.get_charbox(char_index)
        if left < 0 or bottom < 0 or right > 612.5 or top > 792.5:
            outside.append(char_index)
    page_data.append({"page": index + 1, "words": len(text.split()), "out_of_page_chars": outside})
    textpage.close()
    page.close()

font = ImageFont.truetype(r"C:\Windows\Fonts\arial.ttf", 18)
for start in range(0, len(images), 6):
    sheet = Image.new("RGB", (920, 1885), "#e7e7e7")
    draw = ImageDraw.Draw(sheet)
    for offset, path in enumerate(images[start:start + 6]):
        original = Image.open(path).convert("RGB")
        original.thumbnail((430, 585))
        x = 20 + (offset % 2) * 455
        y = 34 + (offset // 2) * 622
        draw.text((x, y - 27), f"Page {start + offset + 1}", fill="black", font=font)
        sheet.paste(original, (x, y))
    sheet.save(QA / f"contact-{start // 6 + 1:02}.png")

ns = {"w": "http://schemas.openxmlformats.org/wordprocessingml/2006/main"}
with ZipFile(DOCX) as archive:
    body = ET.fromstring(archive.read("word/document.xml"))
    styles = ET.fromstring(archive.read("word/styles.xml"))
    body_text = "\n".join(node.text or "" for node in body.findall(".//w:t", ns))
    title_border = False
    for style in styles.findall("w:style", ns):
        if style.get("{" + ns["w"] + "}styleId") == "Title":
            title_border = style.find(".//w:pBdr", ns) is not None
    assert not title_border
    for name in ("Sales and commercial", "Estimation", "Engineering", "Operations and field service",
                 "Inventory and stores", "Procurement", "Manufacturing", "Quality assurance", "Human resources",
                 "Office employee mobile panel", "Field and site employee panel", "Customer application", "Accounts",
                 "Finance", "Administration", "Superadmin and platform administration", "Shared functions and management visibility"):
        assert name in body_text, name
    assert "70adaf7eeae4857b739c4b80cae6d8833ad163d2" in body_text
    assert "existing Cosmix login/session" in body_text
    assert "D07" in body_text

assert not any(page["out_of_page_chars"] for page in page_data), page_data
assert all(page["words"] > 25 for page in page_data), page_data
report = {"pages": len(images), "tables": len(body.findall(".//w:tbl", ns)), "title_border": title_border,
          "module_groups": 17, "page_checks": page_data}
(QA / "verification.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report, indent=2))
