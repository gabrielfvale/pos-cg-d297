"""
tools/pdf_tool.py
Tool CrewAI para extrair texto completo de PDFs de papers científicos.

Estratégia: full-text extraction sem filtragem.
Gemini 2.5 Flash tem 1M tokens de contexto — papers acadêmicos (~20-50k tokens)
cabem inteiros sem necessidade de chunking ou seleção de seções.

Para papers muito grandes (>200k tokens), aplica map-reduce automático.
"""
from crewai.tools import BaseTool
from pydantic import BaseModel, Field
import fitz  # pymupdf
import os

# ~750k chars ≈ ~187k tokens — limite conservador antes de acionar map-reduce
FULL_CONTEXT_CHAR_LIMIT = 750_000

# Tamanho de cada chunk no map-reduce (com overlap)
CHUNK_SIZE = 60_000
CHUNK_OVERLAP = 3_000


class PDFReaderInput(BaseModel):
    pdf_path: str = Field(description="Caminho para o arquivo PDF do paper")


class PDFReaderTool(BaseTool):
    name: str = "pdf_reader"
    description: str = (
        "Lê um arquivo PDF de paper científico e retorna o texto completo "
        "para análise. Para papers muito grandes, aplica chunking automático "
        "com resumo por segmento."
    )
    args_schema: type[BaseModel] = PDFReaderInput

    def _run(self, pdf_path: str) -> str:
        if not os.path.exists(pdf_path):
            return f"ERRO: Arquivo não encontrado: {pdf_path}"
        try:
            return self._extract_full(pdf_path)
        except Exception as e:
            return f"ERRO ao processar PDF: {str(e)}"

    def _extract_full(self, pdf_path: str) -> str:
        doc = fitz.open(pdf_path)
        pages = [page.get_text() for page in doc]
        doc.close()

        full_text = "\n\n".join(pages)
        total_chars = len(full_text)

        if total_chars <= FULL_CONTEXT_CHAR_LIMIT:
            # Paper cabe inteiro na janela de contexto — retorna tudo
            header = (
                f"[PAPER COMPLETO — {total_chars} caracteres, "
                f"{len(pages)} páginas]\n\n"
            )
            return header + full_text
        else:
            # Paper muito grande — aplica chunking com indicação de segmentos
            return self._chunked_extract(full_text, total_chars, len(pages))

    def _chunked_extract(self, text: str, total_chars: int, total_pages: int) -> str:
        """
        Para papers excepcionalmente grandes.
        Divide em chunks com overlap e marca cada segmento.
        O Summarizer deve processar cada segmento e consolidar.
        """
        chunks = []
        start = 0
        chunk_num = 1

        while start < len(text):
            end = min(start + CHUNK_SIZE, len(text))
            chunk = text[start:end]
            chunks.append(f"[SEGMENTO {chunk_num}]\n{chunk}")
            start = end - CHUNK_OVERLAP
            chunk_num += 1

        header = (
            f"[PAPER EM SEGMENTOS — {total_chars} caracteres, "
            f"{total_pages} páginas, {len(chunks)} segmentos]\n"
            f"INSTRUÇÃO: Processe cada segmento e consolide as informações.\n\n"
        )
        return header + "\n\n---\n\n".join(chunks)