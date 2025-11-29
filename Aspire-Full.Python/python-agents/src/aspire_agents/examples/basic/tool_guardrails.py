import asyncio
import os

from aspire_agents.core import (
    Agent,
    Runner,
    function_tool,
    semantic_input_guardrail,
    semantic_output_guardrail,
)
from aspire_agents.guardrails import ToolOutputGuardrailTripwireTriggered


@function_tool
def send_email(to: str, subject: str, body: str) -> str:
    """Send an email to the specified recipient."""
    return f"Email sent to {to} with subject '{subject}'"


@function_tool
def get_user_data(user_id: str) -> dict[str, str]:
    """Get user data by ID."""
    # Simulate returning sensitive data
    return {
        "user_id": user_id,
        "name": "John Doe",
        "email": "john@example.com",
        "ssn": "123-45-6789",  # Sensitive data that should be blocked!
        "phone": "555-1234",
    }


@function_tool
def get_contact_info(user_id: str) -> dict[str, str]:
    """Get contact info by ID."""
    return {
        "user_id": user_id,
        "name": "Jane Smith",
        "email": "jane@example.com",
        "phone": "555-1234",
    }


# Apply semantic guardrails
# "harmful" category includes words like "hack", "exploit", "malware"
send_email.tool_input_guardrails = [semantic_input_guardrail(category="harmful")]  # type: ignore

# "pii" category includes "social security number", "phone number", etc.
get_user_data.tool_output_guardrails = [semantic_output_guardrail(category="pii")]  # type: ignore
get_contact_info.tool_output_guardrails = [semantic_output_guardrail(category="pii")]  # type: ignore

agent = Agent(
    name="Secure Assistant",
    instructions="You are a helpful assistant with access to email and user data tools.",
    tools=[send_email, get_user_data, get_contact_info],
)


async def check_api_key():
    return "OPENAI_API_KEY" in os.environ


async def main():
    # Note: ensure_tensor_core_gpu is called automatically by Agent.__init__
    # For direct tool testing, we ensure it's initialized:
    from aspire_agents.compute import get_compute_service

    get_compute_service()

    print("=== Tool Guardrails Example (Tensor Native) ===\n")

    print("--- Direct Tool Invocation (No LLM required) ---\n")

    # Direct Test 1: Input Guardrail
    print("1. Direct Call: Sending safe email...")
    try:
        import json

        args = {"to": "john@example.com", "subject": "Hi", "body": "Hello"}
        args_json = json.dumps(args)

        if hasattr(send_email, "on_invoke_tool"):
            # on_invoke_tool is likely async and takes a string
            res = await send_email.on_invoke_tool(args_json)
            print(f"✅ Result: {res}\n")
        else:
            print("❌ Error: Could not find invocation method.\n")

    except Exception as e:
        print(f"❌ Error: {e}\n")

    print("2. Direct Call: Sending harmful email...")
    try:
        import json

        args = {
            "to": "john@example.com",
            "subject": "Exploit",
            "body": "Check out this exploit",
        }
        args_json = json.dumps(args)

        if hasattr(send_email, "on_invoke_tool"):
            res = await send_email.on_invoke_tool(args_json)

            # If blocked, it returns the error message string directly in our implementation
            if "Input blocked" in str(res):
                print(f"✅ Guardrail correctly blocked execution: {res}\n")
            else:
                print(f"❌ Warning: Should have been blocked but got: {res}\n")
        else:
            print("❌ Error: Could not find invocation method.\n")

    except Exception as e:
        print(f"❌ Error: {e}\n")

    print("3. Direct Call: Getting sensitive data (Output Guardrail)...")
    try:
        import json

        args = {"user_id": "user123"}
        args_json = json.dumps(args)

        if hasattr(get_user_data, "on_invoke_tool"):
            await get_user_data.on_invoke_tool(args_json)
            print("❌ Error: Should have raised exception!\n")
        else:
            print("❌ Error: Could not find invocation method.\n")
    except ToolOutputGuardrailTripwireTriggered as e:
        print("✅ Guardrail correctly raised exception for PII.")
        print(f"   Details: {e.output.output_info}\n")
    except Exception as e:
        print(f"❌ Unexpected Error: {e}\n")

    print("--- Agent Execution (Requires OPENAI_API_KEY) ---\n")

    if not await check_api_key():
        print("Skipping Agent tests (OPENAI_API_KEY not set).")
        return

    try:
        # Example 1: Normal operation - should work fine
        print("1. Normal email sending:")
        result = await Runner.run(agent, "Send a welcome email to john@example.com")
        print(f"✅ Successful tool execution: {result.final_output}\n")

        # Example 2: Input guardrail triggers - function tool call is rejected
        print("2. Attempting to send email with suspicious content (exploit):")
        result = await Runner.run(
            agent,
            "Send an email to john@example.com about a new exploit we found.",
        )
        print(f"❌ Guardrail rejected function tool call: {result.final_output}\n")
    except Exception as e:
        print(f"Error: {e}\n")

    try:
        # Example 3: Output guardrail triggers - should raise exception for sensitive data
        print("3. Attempting to get user data (contains SSN). Execution blocked:")
        result = await Runner.run(agent, "Get the data for user ID user123")
        print(f"✅ Successful tool execution: {result.final_output}\n")
    except ToolOutputGuardrailTripwireTriggered as e:
        print("🚨 Output guardrail triggered: Execution halted for sensitive data")
        print(f"Details: {e.output.output_info}\n")

    try:
        # Example 4: Output guardrail triggers - reject returning function tool output
        print("4. Rejecting function tool output containing phone numbers:")
        result = await Runner.run(agent, "Get contact info for user456")
        print(f"❌ Guardrail rejected function tool output: {result.final_output}\n")
    except Exception as e:
        print(f"Error: {e}\n")


if __name__ == "__main__":
    asyncio.run(main())


"""
Example output:

=== Tool Guardrails Example ===

1. Normal email sending:
✅ Successful tool execution: I've sent a welcome email to john@example.com with an
appropriate subject and greeting message.

2. Attempting to send email with suspicious content:
❌ Guardrail rejected function tool call: I'm unable to send the email as mentioning ACME
Corp. is restricted.

3. Attempting to get user data (contains SSN). Execution blocked:
🚨 Output guardrail triggered: Execution halted for sensitive data
   Details: {'blocked_pattern': 'SSN', 'tool': 'get_user_data'}

4. Rejecting function tool output containing sensitive data:
❌ Guardrail rejected function tool output: I'm unable to retrieve the contact info for
user456 because it contains restricted information.
"""
