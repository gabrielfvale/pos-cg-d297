"""
utils/pdf_selector.py
Paper folder selector for the terminal interface.

v4: papers are organized in subfolders — papers/{paper_name}/paper.pdf
    The selector lists folders (not individual PDFs) and returns
    (pdf_path, paper_folder_path).

Backward compat: if papers/ contains PDFs directly at root level (v2/v3),
they are listed under a "(root)" group with a migration hint.
"""
from pathlib import Path


PAPERS_DIR = Path(__file__).parent.parent / "papers"


def select_paper_folder() -> tuple[str, Path]:
    """
    Lists paper subfolders in papers/ and returns (pdf_path, paper_folder).

    Each folder must contain at least one .pdf file.
    Returns the first PDF found inside the chosen folder.
    """
    if not PAPERS_DIR.exists():
        PAPERS_DIR.mkdir(parents=True)

    # Subfolders containing at least one PDF
    folders = [
        f for f in sorted(PAPERS_DIR.iterdir())
        if f.is_dir() and list(f.glob("*.pdf"))
    ]

    # PDFs directly in papers/ root (v2/v3 layout)
    root_pdfs = sorted(PAPERS_DIR.glob("*.pdf"))

    if not folders and not root_pdfs:
        print(f"\n  No papers found.")
        print(f"  Create a folder inside papers/ and place your PDF and FIG*.png files there.")
        print(f"  Example: papers/my_paper/paper.pdf")
        raise SystemExit(1)

    print(f"\n  Available papers:\n")
    options: list[tuple[Path, Path]] = []   # (pdf_path, folder_path)

    # Subfolders (preferred v4 structure)
    for folder in folders:
        pdfs  = sorted(folder.glob("*.pdf"))
        figs  = (list(folder.glob("FIG*.png")) + list(folder.glob("FIG_*.png"))
                 + list(folder.glob("FIG*.jpg")) + list(folder.glob("FIG_*.jpg")))
        pdf   = pdfs[0]
        label = (
            f"  [{len(options)+1}] {folder.name}"
            + (f"  ({len(figs)} figure(s))" if figs else "")
            + f"  —  {pdf.name}"
        )
        print(label)
        options.append((pdf, folder))

    # Root-level PDFs (legacy, with migration hint)
    if root_pdfs:
        print(f"\n  [Legacy — PDFs directly in papers/]")
        print(f"  (Consider moving each PDF into its own subfolder: papers/paper_name/paper.pdf)\n")
        for pdf in root_pdfs:
            size_kb = pdf.stat().st_size // 1024
            label   = f"  [{len(options)+1}] {pdf.stem}  ({size_kb} KB)  [root — no figures]"
            print(label)
            options.append((pdf, PAPERS_DIR))

    print()
    while True:
        raw = input("  Select paper number [1]: ").strip()
        if raw == "":
            raw = "1"
        try:
            idx = int(raw)
            if 1 <= idx <= len(options):
                pdf_path, folder = options[idx - 1]
                print(f"\n  Selected: {folder.name} / {pdf_path.name}")
                return str(pdf_path), folder
            else:
                print(f"  Invalid number. Enter a value between 1 and {len(options)}.")
        except ValueError:
            print("  Enter only the number.")


# Backward-compat alias used by older parts of the codebase
def select_pdf() -> str:
    """Legacy alias — returns only the pdf_path string."""
    pdf_path, _ = select_paper_folder()
    return pdf_path
