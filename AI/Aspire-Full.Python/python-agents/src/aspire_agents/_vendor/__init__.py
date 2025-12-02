"""Vendor abstractions for external dependencies.

This module provides protocol definitions and type stubs for external packages,
enabling static type checking without requiring the packages to be installed.

All external dependencies are abstracted through protocols, allowing:
- Type checking without venv/package installation
- Clear interface contracts for each dependency
- Easy mocking in tests
- Gradual migration to alternative implementations

Packages abstracted:
- torch: GPU tensor operations
- transformers: HuggingFace model loading
- agents (openai-agents): Agent runtime
- openai: OpenAI API client
- threading: Python 3.15 free-threading primitives
"""

from __future__ import annotations

from typing import Final

from ._torch import (
    Tensor,
    TorchDevice,
    TorchDtype,
    TorchModule,
    TorchOptimizer,
    cuda_is_available,
    cuda_device_count,
    cuda_synchronize,
    cuda_empty_cache,
    no_grad,
    autocast,
)
from ._transformers import (
    PreTrainedModel,
    PreTrainedTokenizer,
    AutoModel,
    AutoTokenizer,
)
from ._agents import (
    AgentProtocol,
    RunnerProtocol,
    FunctionToolProtocol,
    AgentOutputSchema,
    RunResult,
)
from ._openai import (
    OpenAIClient,
    ChatCompletion,
    ChatMessage,
    EmbeddingResponse,
)
from ._threading import (
    # GIL Detection
    is_gil_disabled,
    get_python_version,
    supports_free_threading,
    # Lock Protocols
    LockTypeProtocol,
    LockProtocol,
    RLockProtocol,
    # Synchronization Protocols
    ConditionProtocol,
    EventProtocol,
    SemaphoreProtocol,
    BarrierProtocol,
    # Thread Protocols
    ThreadHandleProtocol,
    ThreadProtocol,
    ThreadLocalProtocol,
    # Executor Protocols
    ThreadPoolExecutorProtocol,
    FutureProtocol,
    # Data Classes
    ExceptHookArgs,
    FreeThreadingConfig,
    # Factories
    create_thread_pool,
    create_lock,
    create_rlock,
    create_event,
    create_semaphore,
    create_condition,
    create_barrier,
    # Utilities
    get_active_thread_count,
    get_current_thread,
    get_main_thread,
    enumerate_threads,
    # Constants
    TIMEOUT_MAX,
)

__all__: Final[list[str]] = [
    # Torch abstractions
    "Tensor",
    "TorchDevice",
    "TorchDtype",
    "TorchModule",
    "TorchOptimizer",
    "cuda_is_available",
    "cuda_device_count",
    "cuda_synchronize",
    "cuda_empty_cache",
    "no_grad",
    "autocast",
    # Transformers abstractions
    "PreTrainedModel",
    "PreTrainedTokenizer",
    "AutoModel",
    "AutoTokenizer",
    # Agents abstractions
    "AgentProtocol",
    "RunnerProtocol",
    "FunctionToolProtocol",
    "AgentOutputSchema",
    "RunResult",
    # OpenAI abstractions
    "OpenAIClient",
    "ChatCompletion",
    "ChatMessage",
    "EmbeddingResponse",
    # Threading - GIL Detection
    "is_gil_disabled",
    "get_python_version",
    "supports_free_threading",
    # Threading - Lock Protocols
    "LockTypeProtocol",
    "LockProtocol",
    "RLockProtocol",
    # Threading - Synchronization Protocols
    "ConditionProtocol",
    "EventProtocol",
    "SemaphoreProtocol",
    "BarrierProtocol",
    # Threading - Thread Protocols
    "ThreadHandleProtocol",
    "ThreadProtocol",
    "ThreadLocalProtocol",
    # Threading - Executor Protocols
    "ThreadPoolExecutorProtocol",
    "FutureProtocol",
    # Threading - Data Classes
    "ExceptHookArgs",
    "FreeThreadingConfig",
    # Threading - Factories
    "create_thread_pool",
    "create_lock",
    "create_rlock",
    "create_event",
    "create_semaphore",
    "create_condition",
    "create_barrier",
    # Threading - Utilities
    "get_active_thread_count",
    "get_current_thread",
    "get_main_thread",
    "enumerate_threads",
    # Threading - Constants
    "TIMEOUT_MAX",
]
