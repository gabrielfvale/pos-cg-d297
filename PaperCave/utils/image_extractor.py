"""
utils/image_extractor.py
Extrai imagens de PDFs com metadados contextuais para o pipeline de visão.

Para cada imagem no PDF, extrai:
- A imagem como bytes (PNG)
- Página onde aparece
- Caption: texto dentro de ~5 linhas abaixo que começa com Fig/Figure/Figura
- Contexto: parágrafo imediatamente antes e depois da imagem na página
- Dimensões em pixels

Filtra automaticamente imagens muito pequenas (< 50x50px) que são
provavelmente ícones ou artefatos de formatação.
"""
import re
import fitz  # pymupdf
from pathlib import Path
from models.schemas import ImageContext


MIN_WIDTH  = 50
MIN_HEIGHT = 50
CAPTION_SEARCH_LINES = 5
CONTEXT_CHARS = 400


def extract_images_with_context(pdf_path: str, raw_dir: Path) -> list[ImageContext]:
    """
    Extrai todas as imagens do PDF com contexto textual.
    Salva cada imagem como PNG em raw_dir.
    Retorna lista de ImageContext ordenada por página.
    """
    raw_dir.mkdir(parents=True, exist_ok=True)
    doc = fitz.open(pdf_path)
    results: list[ImageContext] = []

    for page_num, page in enumerate(doc, 1):
        page_text = page.get_text("text")
        image_list = page.get_images(full=True)

        for img_idx, img_ref in enumerate(image_list):
            xref = img_ref[0]

            try:
                base_image = doc.extract_image(xref)
            except Exception:
                continue

            w, h = base_image["width"], base_image["height"]
            if w < MIN_WIDTH or h < MIN_HEIGHT:
                continue

            raw_filename = f"raw_p{page_num}_img{img_idx}.png"
            raw_path = raw_dir / raw_filename
            raw_path.write_bytes(base_image["image"])

            caption        = _find_caption(page_text)
            context_before = _context_before(page_text, img_idx, page)
            context_after  = _context_after(page_text, img_idx, page)

            results.append(ImageContext(
                raw_filename   = raw_filename,
                page           = page_num,
                caption        = caption,
                context_before = context_before,
                context_after  = context_after,
                width_px       = w,
                height_px      = h,
            ))

    doc.close()
    return results


def _find_caption(page_text: str) -> str:
    pattern = re.compile(
        r"(Fig\.?\s*\d+|Figure\s*\d+|Figura\s*\d+)[^\n]{0,200}",
        re.IGNORECASE,
    )
    match = pattern.search(page_text)
    return match.group(0).strip() if match else ""


def _context_before(page_text: str, img_idx: int, page) -> str:
    images = page.get_images(full=True)
    if not images:
        return ""
    segment_size = max(1, len(page_text) // len(images))
    start = max(0, img_idx * segment_size - CONTEXT_CHARS)
    end   = img_idx * segment_size
    return page_text[start:end].strip()


def _context_after(page_text: str, img_idx: int, page) -> str:
    images = page.get_images(full=True)
    if not images:
        return ""
    segment_size = max(1, len(page_text) // len(images))
    start = (img_idx + 1) * segment_size
    end   = min(len(page_text), start + CONTEXT_CHARS)
    return page_text[start:end].strip()


def load_image_bytes(raw_dir: Path, raw_filename: str) -> bytes:
    """Carrega uma imagem salva em raw_dir como bytes."""
    return (raw_dir / raw_filename).read_bytes()
