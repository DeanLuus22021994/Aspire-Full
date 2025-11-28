"""Typer entry point for running Aspire Semantic Kernel agents."""

from __future__ import annotations

import asyncio
from pathlib import Path

import typer
from rich.console import Console

from .config import AgentConfig
from .runner import AgentRunner

app = typer.Typer(help="Typer entry point for running Aspire Semantic Kernel agents")
console = Console()


@app.command()
def run(
    config: Path = typer.Option(
        ..., exists=True, help="Path to the agent YAML configuration"
    ),
    input_text: str | None = typer.Option(
        None,
        "--input",
        help="Inline user input",
    ),
    input_file: Path | None = typer.Option(
        None,
        "--input-file",
        exists=True,
        help="File that contains the user request",
    ),
) -> None:
    """Execute an agent manifest using the configured provider."""

    if not input_text and not input_file:
        raise typer.BadParameter("Provide --input or --input-file")

    if input_text is not None:
        payload = input_text
    else:
        assert input_file is not None
        payload = input_file.read_text(encoding="utf-8")
    agent_cfg = AgentConfig.from_file(config)
    runner = AgentRunner(agent_cfg)
    console.log(f"Running agent '{agent_cfg.name}' with model '{agent_cfg.model.name}'")
    console.log(
        f"Tensor GPU: {runner.tensor_info.name} "
        f"(cc {runner.tensor_info.compute_capability}, "
        f"{runner.tensor_info.total_memory_gb:.2f} GiB)"
    )

    result = asyncio.run(runner.arun(payload))
    runner.pretty_print(result)


if __name__ == "__main__":
    app()
