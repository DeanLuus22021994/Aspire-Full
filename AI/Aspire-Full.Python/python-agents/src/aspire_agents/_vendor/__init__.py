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
- safetensors: Safe tensor serialization
- redis: Redis database client
- ctypes: C FFI interface
- cProfile/pstats: Python profiling
- docker-model-runner: Docker Model Runner for local LLM inference
- pytest: Testing framework abstractions

Unified Factory Access:
    from aspire_agents._vendor import get_vendor_factory

    factory = get_vendor_factory()
    tensor_proto = factory.torch.tensor
    redis_proto = factory.redis.client
"""

from __future__ import annotations

from typing import Final

from ._agents import (
    AgentOutputSchema,
    AgentProtocol,
    FunctionToolProtocol,
    RunnerProtocol,
    RunResult,
)
from ._ctypes import (
    POINTER,
    RTLD_GLOBAL,
    RTLD_LOCAL,
    ArrayProtocol,
    CDataProtocol,
    CDLLProtocol,
    CFuncPtrProtocol,
    FreeLibrary,
    LoadLibrary,
    PointerProtocol,
    SimpleCDataProtocol,
    StructureProtocol,
    UnionProtocol,
    addressof,
    alignment,
    byref,
    cast,
    get_errno,
    get_last_error,
    pointer,
    set_errno,
    set_last_error,
    sizeof,
)
from ._docker_model_runner import (
    # API Constants
    CHAT_COMPLETIONS_ENDPOINT,
    DEFAULT_API_BASE,
    DEFAULT_API_HOST,
    DEFAULT_API_PORT,
    EMBEDDINGS_ENDPOINT,
    MODELS_ENDPOINT,
    # Exceptions
    DockerModelRunnerError,
    # Protocols
    DockerModelRunnerProtocol,
    # Data Classes
    GpuDeviceInfo,
    GpuNotAvailableError,
    InferenceError,
    InferenceProtocol,
    InferenceResult,
    ModelInfo,
    ModelManagerProtocol,
    ModelNotFoundError,
    ModelPullError,
    ModelRunnerConfig,
    RunnerInfo,
    RunnerNotAvailableError,
    StreamChunk,
    is_docker_model_available,
)
from ._docker_model_runner import (
    RunnerProtocol as DockerRunnerProtocol,
)
from ._docker_model_runner import (
    # CLI Functions
    check_gpu_access as docker_check_gpu_access,
)
from ._docker_model_runner import (
    get_logs as docker_get_logs,
)
from ._docker_model_runner import (
    get_runner_status as docker_get_runner_status,
)
from ._docker_model_runner import (
    list_models as docker_list_models,
)
from ._docker_model_runner import (
    pull_model as docker_pull_model,
)
from ._docker_model_runner import (
    reinstall_runner as docker_reinstall_runner,
)
from ._docker_model_runner import (
    run_model as docker_run_model,
)
from ._docker_model_runner import (
    start_runner as docker_start_runner,
)
from ._docker_model_runner import (
    stop_runner as docker_stop_runner,
)

# Centralized Enums (aligned with factory interface names)
from ._enums import (
    # Agents
    AgentState,
    # CTypes
    CTypesCallConv,
    CTypesEndian,
    # Docker Model Runner
    GpuBackendType,
    LockState,
    # OpenAI
    OpenAIFinishReason,
    OpenAIRole,
    # Profiler
    ProfilerSortKey,
    # Pytest
    PytestExitCode,
    PytestOutcome,
    # Redis
    RedisDataType,
    RedisPubSubType,
    # Safetensors
    SafetensorsFramework,
    # Backwards compatibility aliases
    SortKey,
    # Threading
    ThreadState,
    ToolType,
    # Factory
    VendorCategory,
)

# Factory and interface collections
from ._factory import (
    AbstractFactory,
    AgentsInterfaces,
    CTypesInterfaces,
    DockerModelRunnerInterfaces,
    OpenAIInterfaces,
    ProfilerInterfaces,
    ProtocolRegistration,
    ProtocolRegistry,
    PytestInterfaces,
    RedisInterfaces,
    SafetensorsInterfaces,
    ThreadingInterfaces,
    TorchInterfaces,
    TransformersInterfaces,
    VendorFactory,
    get_vendor_factory,
    reset_vendor_factory,
)
from ._openai import (
    ChatCompletion,
    ChatMessage,
    EmbeddingResponse,
    OpenAIClient,
)
from ._profiler import (
    FunctionProfile,
    ProfileProtocol,
    StatsProfile,
    StatsProtocol,
    create_profile,
    create_stats,
)
from ._profiler import (
    run as profile_run,
)
from ._profiler import (
    runctx as profile_runctx,
)

# Pytest abstractions
from ._pytest import (
    # Protocols
    CaptureFixtureProtocol,
    # Exceptions
    CollectionError,
    # Data Classes
    CollectionResult,
    ConfigProtocol,
    # Enums
    ExitCodeEnum,
    Failed,
    FixtureDecoratorProtocol,
    FixtureError,
    FixtureInfo,
    FixtureRequestProtocol,
    ItemProtocol,
    MarkDecoratorProtocol,
    MarkInfo,
    MonkeyPatchProtocol,
    PytestError,
    PytestProtocol,
    RaisesContextProtocol,
    RecorderProtocol,
    SessionProtocol,
    SessionResult,
    Skipped,
    TestItem,
    TestOutcome,
    TestReport,
    TmpPathFactoryProtocol,
    UsageError,
    XFail,
    # Assertion Helpers
    assert_equal,
    assert_false,
    assert_in,
    assert_is,
    assert_is_none,
    assert_is_not,
    assert_is_not_none,
    assert_isinstance,
    assert_not_equal,
    assert_not_in,
    assert_raises,
    assert_true,
)
from ._redis import (
    AuthenticationError,
    AuthenticationWrongNumberOfArgsError,
    BlockingConnectionPoolProtocol,
    BusyLoadingError,
    ChildDeadlockedError,
    ConnectionPoolProtocol,
    ConnectionProtocol,
    CredentialProviderProtocol,
    DataError,
    InvalidResponse,
    PipelineProtocol,
    PubSubError,
    PubSubProtocol,
    ReadOnlyError,
    RedisError,
    RedisProtocol,
    ResponseError,
    SentinelConnectionPoolProtocol,
    SentinelProtocol,
    WatchError,
)
from ._redis import (
    ConnectionError as RedisConnectionError,
)
from ._redis import (
    TimeoutError as RedisTimeoutError,
)
from ._redis import (
    from_url as redis_from_url,
)
from ._safetensors import (
    SafeOpenProtocol,
    SafetensorError,
)
from ._safetensors import (
    deserialize as safetensors_deserialize,
)
from ._safetensors import (
    load_file as safetensors_load_file,
)
from ._safetensors import (
    save_file as safetensors_save_file,
)
from ._safetensors import (
    serialize as safetensors_serialize,
)
from ._safetensors import (
    serialize_file as safetensors_serialize_file,
)
from ._threading import (
    # Constants
    TIMEOUT_MAX,
    BarrierProtocol,
    # Synchronization Protocols
    ConditionProtocol,
    EventProtocol,
    # Data Classes
    ExceptHookArgs,
    FreeThreadingConfig,
    FutureProtocol,
    LockProtocol,
    # Lock Protocols
    LockTypeProtocol,
    RLockProtocol,
    SemaphoreProtocol,
    # Thread Protocols
    ThreadHandleProtocol,
    ThreadLocalProtocol,
    # Executor Protocols
    ThreadPoolExecutorProtocol,
    ThreadProtocol,
    create_barrier,
    create_condition,
    create_event,
    create_lock,
    create_rlock,
    create_semaphore,
    # Factories
    create_thread_pool,
    enumerate_threads,
    # Utilities
    get_active_thread_count,
    get_current_thread,
    get_main_thread,
    get_python_version,
    # GIL Detection
    is_gil_disabled,
    supports_free_threading,
)
from ._torch import (
    Tensor,
    TorchDevice,
    TorchDtype,
    TorchModule,
    TorchOptimizer,
    autocast,
    cuda_device_count,
    cuda_empty_cache,
    cuda_is_available,
    cuda_synchronize,
    no_grad,
)
from ._transformers import (
    AutoModel,
    AutoTokenizer,
    PreTrainedModel,
    PreTrainedTokenizer,
)

__all__: Final[list[str]] = [
    # =========================================================================
    # Centralized Enums (from _enums.py)
    # =========================================================================
    # Factory
    "VendorCategory",
    # Agents
    "AgentState",
    "ToolType",
    # CTypes
    "CTypesCallConv",
    "CTypesEndian",
    # Docker Model Runner
    "GpuBackendType",
    # OpenAI
    "OpenAIRole",
    "OpenAIFinishReason",
    # Profiler
    "ProfilerSortKey",
    "SortKey",  # Backwards compatibility alias
    # Pytest
    "PytestExitCode",
    "PytestOutcome",
    "ExitCodeEnum",  # Backwards compatibility alias
    "TestOutcome",  # Backwards compatibility alias
    # Redis
    "RedisDataType",
    "RedisPubSubType",
    # Safetensors
    "SafetensorsFramework",
    # Threading
    "ThreadState",
    "LockState",
    # =========================================================================
    # Torch abstractions
    # =========================================================================
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
    # =========================================================================
    # Transformers abstractions
    # =========================================================================
    "PreTrainedModel",
    "PreTrainedTokenizer",
    "AutoModel",
    "AutoTokenizer",
    # =========================================================================
    # Agents abstractions
    # =========================================================================
    "AgentProtocol",
    "RunnerProtocol",
    "FunctionToolProtocol",
    "AgentOutputSchema",
    "RunResult",
    # =========================================================================
    # OpenAI abstractions
    # =========================================================================
    "OpenAIClient",
    "ChatCompletion",
    "ChatMessage",
    "EmbeddingResponse",
    # =========================================================================
    # Threading abstractions
    # =========================================================================
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
    # Safetensors - Exceptions
    "SafetensorError",
    # Safetensors - Protocols
    "SafeOpenProtocol",
    # Safetensors - Functions
    "safetensors_serialize",
    "safetensors_deserialize",
    "safetensors_serialize_file",
    "safetensors_load_file",
    "safetensors_save_file",
    # Redis - Exceptions
    "RedisError",
    "RedisConnectionError",
    "RedisTimeoutError",
    "AuthenticationError",
    "AuthenticationWrongNumberOfArgsError",
    "DataError",
    "InvalidResponse",
    "ResponseError",
    "BusyLoadingError",
    "ReadOnlyError",
    "PubSubError",
    "WatchError",
    "ChildDeadlockedError",
    # Redis - Protocols
    "CredentialProviderProtocol",
    "ConnectionProtocol",
    "ConnectionPoolProtocol",
    "BlockingConnectionPoolProtocol",
    "SentinelProtocol",
    "SentinelConnectionPoolProtocol",
    "RedisProtocol",
    "PipelineProtocol",
    "PubSubProtocol",
    # Redis - Factory Functions
    "redis_from_url",
    # Ctypes - Base Protocols
    "CDataProtocol",
    "SimpleCDataProtocol",
    # Ctypes - Structure Protocols
    "StructureProtocol",
    "UnionProtocol",
    # Ctypes - Container Protocols
    "ArrayProtocol",
    "PointerProtocol",
    # Ctypes - Function Protocols
    "CFuncPtrProtocol",
    "CDLLProtocol",
    # Ctypes - Helper Functions
    "addressof",
    "sizeof",
    "alignment",
    "byref",
    "pointer",
    "POINTER",
    "cast",
    "LoadLibrary",
    "FreeLibrary",
    "get_errno",
    "set_errno",
    "get_last_error",
    "set_last_error",
    # Ctypes - Constants
    "RTLD_GLOBAL",
    "RTLD_LOCAL",
    # Profiler - Enums
    "SortKey",
    # Profiler - Data Classes
    "FunctionProfile",
    "StatsProfile",
    # Profiler - Protocols
    "ProfileProtocol",
    "StatsProtocol",
    # Profiler - Convenience Functions
    "profile_run",
    "profile_runctx",
    # Profiler - Factory Functions
    "create_profile",
    "create_stats",
    # Docker Model Runner - Exceptions
    "DockerModelRunnerError",
    "ModelNotFoundError",
    "RunnerNotAvailableError",
    "GpuNotAvailableError",
    "ModelPullError",
    "InferenceError",
    # Docker Model Runner - Data Classes
    "ModelInfo",
    "GpuDeviceInfo",
    "RunnerInfo",
    "InferenceResult",
    "StreamChunk",
    "ModelRunnerConfig",
    # Docker Model Runner - Protocols
    "ModelManagerProtocol",
    "DockerRunnerProtocol",
    "InferenceProtocol",
    "DockerModelRunnerProtocol",
    # Docker Model Runner - CLI Functions
    "is_docker_model_available",
    "docker_get_runner_status",
    "docker_list_models",
    "docker_pull_model",
    "docker_run_model",
    "docker_start_runner",
    "docker_stop_runner",
    "docker_reinstall_runner",
    "docker_get_logs",
    "docker_check_gpu_access",
    # Docker Model Runner - API Constants
    "DEFAULT_API_HOST",
    "DEFAULT_API_PORT",
    "DEFAULT_API_BASE",
    "CHAT_COMPLETIONS_ENDPOINT",
    "MODELS_ENDPOINT",
    "EMBEDDINGS_ENDPOINT",
    # Factory - Categories
    "VendorCategory",
    # Factory - Registry
    "ProtocolRegistration",
    "ProtocolRegistry",
    # Factory - Abstract
    "AbstractFactory",
    # Factory - Interface Collections
    "TorchInterfaces",
    "TransformersInterfaces",
    "AgentsInterfaces",
    "OpenAIInterfaces",
    "DockerModelRunnerInterfaces",
    "RedisInterfaces",
    "ThreadingInterfaces",
    "CTypesInterfaces",
    "ProfilerInterfaces",
    "SafetensorsInterfaces",
    "PytestInterfaces",
    # Factory - Unified Factory
    "VendorFactory",
    "get_vendor_factory",
    "reset_vendor_factory",
    # Pytest - Enums
    "ExitCodeEnum",
    "TestOutcome",
    # Pytest - Exceptions
    "PytestError",
    "FixtureError",
    "UsageError",
    "CollectionError",
    "Failed",
    "Skipped",
    "XFail",
    # Pytest - Data Classes
    "MarkInfo",
    "FixtureInfo",
    "TestItem",
    "TestReport",
    "CollectionResult",
    "SessionResult",
    # Pytest - Protocols
    "FixtureRequestProtocol",
    "ItemProtocol",
    "MarkDecoratorProtocol",
    "ConfigProtocol",
    "SessionProtocol",
    "CaptureFixtureProtocol",
    "MonkeyPatchProtocol",
    "TmpPathFactoryProtocol",
    "RecorderProtocol",
    "RaisesContextProtocol",
    "FixtureDecoratorProtocol",
    "PytestProtocol",
    # Pytest - Assertion Helpers
    "assert_equal",
    "assert_not_equal",
    "assert_true",
    "assert_false",
    "assert_is",
    "assert_is_not",
    "assert_is_none",
    "assert_is_not_none",
    "assert_in",
    "assert_not_in",
    "assert_isinstance",
    "assert_raises",
]
