"""
crew_agents/image_selector.py
Agente Image Selector: recebe metadados de todas as imagens extraídas do PDF
e seleciona até 3 mais relevantes para a contribuição central.
Prompts carregados de prompts/agents.yaml.
"""
import yaml
from pathlib import Path
from crewai import Agent


def _load_agent_prompts() -> dict:
    path = Path(__file__).parent.parent / "prompts" / "agents.yaml"
    return yaml.safe_load(path.read_text(encoding="utf-8"))


def make_image_selector_agent(llm) -> Agent:
    prompts = _load_agent_prompts()["image_selector"]
    return Agent(
        role=prompts["role"],
        goal=prompts["goal"],
        backstory=prompts["backstory"],
        llm=llm,
        verbose=True,
        allow_delegation=False,
    )
