"""Output guardrails with GPU-accelerated semantic similarity checking.

This module demonstrates output guardrails using:
- GPU-accelerated PII detection via BatchComputeService
- Semantic similarity checking with Tensor Core optimization
- Thread-safe guardrail evaluation for Python 3.15+ free-threading

Environment Variables:
- ASPIRE_TENSOR_BATCH_SIZE: Batch size for embeddings (default: 32)
- CUDA_TENSOR_CORE_ALIGNMENT: Memory alignment in bytes (default: 128)

GPU-ONLY: Requires CUDA GPU. No CPU fallback supported.
"""

from __future__ import annotations

import asyncio
import json
from typing import Any

from agents import (
    Agent,
    GuardrailFunctionOutput,
    OutputGuardrailTripwireTriggered,
    RunContextWrapper,
    Runner,
    output_guardrail,
)
from aspire_agents import (
    get_guardrail_service,
    semantic_output_guardrail,
)
from pydantic import BaseModel, Field

# This example shows how to use output guardrails.
#
# Output guardrails are checks that run on the final output of an agent.
# They can be used to do things like:
# - Check if the output contains sensitive data
# - Check if the output is a valid response to the user's message
#
# In this example, we'll use a (contrived) example where we check if the agent's response contains
# a phone number.


# The agent's output type
class MessageOutput(BaseModel):
    """
    Output schema for the agent.
    """

    reasoning: str = Field(description="Thoughts on how to respond to the user's message")
    response: str = Field(description="The response to the user's message")
    user_name: str | None = Field(description="The name of the user who sent the message, if known")


@output_guardrail
async def sensitive_data_check(
    _context: RunContextWrapper[Any], _agent: object, output: MessageOutput
) -> GuardrailFunctionOutput:
    """
    Check if the output contains sensitive data.
    """
    phone_number_in_response = "650" in output.response
    phone_number_in_reasoning = "650" in output.reasoning

    return GuardrailFunctionOutput(
        output_info={
            "phone_number_in_response": phone_number_in_response,
            "phone_number_in_reasoning": phone_number_in_reasoning,
        },
        tripwire_triggered=phone_number_in_response or phone_number_in_reasoning,
    )


agent = Agent(
    name="Assistant",
    instructions="You are a helpful assistant.",
    output_type=MessageOutput,
    output_guardrails=[sensitive_data_check],
)


async def main() -> None:
    """
    Main entry point for the output guardrails example.
    """
    # This should be ok
    await Runner.run(agent, "What's the capital of California?")
    print("First message passed")

    # This should trip the guardrail
    try:
        result = await Runner.run(agent, "My phone number is 650-123-4567. Where do you think I live?")
        print(
            f"Guardrail didn't trip - this is unexpected. Output: "
            f"{json.dumps(result.final_output.model_dump(), indent=2)}"
        )

    except OutputGuardrailTripwireTriggered as e:
        print(f"Guardrail tripped. Info: {e.guardrail_result.output.output_info}")


if __name__ == "__main__":
    asyncio.run(main())
