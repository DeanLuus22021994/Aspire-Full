"""
This module demonstrates how to use an output type that is not in strict mode.
"""

import asyncio
import json
from dataclasses import dataclass
from typing import TYPE_CHECKING, Any, override

from agents import (
    Agent,
    AgentOutputSchema,
    AgentOutputSchemaBase,
    Runner,
)

if TYPE_CHECKING:
    from aspire_agents.gpu import TensorCoreInfo


def _noop_ensure_tensor_core_gpu() -> "TensorCoreInfo | None":
    """Fallback when aspire_agents.gpu is unavailable."""
    return None


try:
    from aspire_agents.gpu import ensure_tensor_core_gpu
except ImportError:
    ensure_tensor_core_gpu = _noop_ensure_tensor_core_gpu  # type: ignore[assignment]


# This example demonstrates how to use an output type that is not in strict mode. Strict mode
# allows us to guarantee valid JSON output, but some schemas are not strict-compatible.
#
# In this example, we define an output type that is not strict-compatible, and then we run the
# agent with strict_json_schema=False.
#
# We also demonstrate a custom output type.
#
# To understand which schemas are strict-compatible, see:
# https://platform.openai.com/docs/guides/structured-outputs?api-mode=responses#supported-schemas


@dataclass
class OutputType:
    """
    Output type for the agent.
    """

    jokes: dict[int, str]
    """A list of jokes, indexed by joke number."""


class CustomOutputSchema(AgentOutputSchemaBase):
    """A demonstration of a custom output schema."""

    @override
    def is_plain_text(self) -> bool:
        """
        Check if the output is plain text.
        """
        return False

    @override
    def name(self) -> str:
        """
        Get the name of the schema.
        """
        return "CustomOutputSchema"

    @override
    def json_schema(self) -> dict[str, Any]:
        """
        Get the JSON schema.
        """
        return {
            "type": "object",
            "properties": {"jokes": {"type": "object", "properties": {"joke": {"type": "string"}}}},
        }

    @override
    def is_strict_json_schema(self) -> bool:
        """
        Check if the schema is strict JSON.
        """
        return False

    @override
    def validate_json(self, json_str: str) -> Any:
        """
        Validate the JSON string.
        """
        json_obj = json.loads(json_str)
        # Just for demonstration, we'll return a list.
        return list(json_obj["jokes"].values())


async def main() -> None:
    """
    Main entry point for the non-strict output type example.
    """
    ensure_tensor_core_gpu()
    agent = Agent(
        name="Assistant",
        instructions="You are a helpful assistant.",
        output_type=OutputType,
    )

    user_input = "Tell me 3 short jokes."

    # First, let's try with a strict output type. This should raise an exception.
    try:
        result = await Runner.run(agent, user_input)
        raise AssertionError("Should have raised an exception")
    except Exception as e:  # pylint: disable=broad-exception-caught
        print(f"Error (expected): {e}")

    # Now let's try again with a non-strict output type. This should work.
    # In some cases, it will raise an error - the schema isn't strict, so the model may
    # produce an invalid JSON object.
    agent.output_type = AgentOutputSchema(OutputType, strict_json_schema=False)
    result = await Runner.run(agent, user_input)
    print(result.final_output)

    # Finally, let's try a custom output type.
    agent.output_type = CustomOutputSchema()
    result = await Runner.run(agent, user_input)
    print(result.final_output)


if __name__ == "__main__":
    asyncio.run(main())
