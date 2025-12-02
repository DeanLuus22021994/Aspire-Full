"""Unified Abstractions Factory and Interface Collections.

Provides a centralized factory pattern for accessing all vendor abstractions,
enabling:
- Unified interface for all vendor modules
- Runtime registration of implementations
- Dependency injection support
- Test mock injection
- Protocol verification

This module serves as the single entry point for accessing all vendor
abstractions in a type-safe and testable manner.
"""

from __future__ import annotations

from abc import ABC, abstractmethod
from dataclasses import dataclass
from enum import Enum, auto
from typing import (
    Any,
    Final,
    Generic,
    TypeVar,
    cast,
)

# ============================================================================
# Type Variables
# ============================================================================

T = TypeVar("T")
T_co = TypeVar("T_co", covariant=True)
P = TypeVar("P")


# ============================================================================
# Vendor Categories
# ============================================================================


class VendorCategory(Enum):
    """Categories of vendor abstractions."""

    # Core ML/AI
    TORCH = auto()
    TRANSFORMERS = auto()
    SAFETENSORS = auto()

    # Agent Frameworks
    AGENTS = auto()
    OPENAI = auto()
    DOCKER_MODEL_RUNNER = auto()

    # Infrastructure
    REDIS = auto()
    THREADING = auto()
    CTYPES = auto()

    # Development
    PROFILER = auto()
    PYTEST = auto()


# ============================================================================
# Protocol Registry
# ============================================================================


@dataclass(slots=True)
class ProtocolRegistration(Generic[P]):
    """Registration entry for a protocol implementation.

    Attributes:
        protocol: The protocol type
        implementation: The actual implementation class/instance
        category: Vendor category
        is_mock: Whether this is a mock implementation
        priority: Priority for resolution (higher = preferred)
    """

    protocol: type[P]
    implementation: type[P] | P
    category: VendorCategory
    is_mock: bool = False
    priority: int = 0


class ProtocolRegistry:
    """Registry for protocol implementations.

    Allows registering multiple implementations of protocols
    and resolving them at runtime.
    """

    __slots__ = ("_registrations", "_singletons")

    def __init__(self) -> None:
        super().__init__()
        self._registrations: dict[type, list[ProtocolRegistration[Any]]] = {}
        self._singletons: dict[type, Any] = {}

    def register(
        self,
        protocol: type[P],
        implementation: type[P] | P,
        category: VendorCategory,
        *,
        is_mock: bool = False,
        priority: int = 0,
        singleton: bool = False,
    ) -> None:
        """Register a protocol implementation.

        Args:
            protocol: Protocol type to register
            implementation: Implementation class or instance
            category: Vendor category
            is_mock: Whether this is a mock
            priority: Resolution priority
            singleton: Whether to use singleton pattern
        """
        registration = ProtocolRegistration(
            protocol=protocol,
            implementation=implementation,
            category=category,
            is_mock=is_mock,
            priority=priority,
        )

        if protocol not in self._registrations:
            self._registrations[protocol] = []

        self._registrations[protocol].append(registration)
        # Sort by priority (descending)
        self._registrations[protocol].sort(key=lambda r: -r.priority)

        if singleton and not isinstance(implementation, type):
            self._singletons[protocol] = implementation

    def unregister(self, protocol: type[P]) -> None:
        """Unregister all implementations of a protocol.

        Args:
            protocol: Protocol to unregister
        """
        self._registrations.pop(protocol, None)
        self._singletons.pop(protocol, None)

    def resolve(self, protocol: type[P]) -> P:
        """Resolve the highest-priority implementation.

        Args:
            protocol: Protocol to resolve

        Returns:
            Implementation instance

        Raises:
            KeyError: If no implementation registered
        """
        if protocol in self._singletons:
            return cast(P, self._singletons[protocol])

        registrations = self._registrations.get(protocol)
        if not registrations:
            raise KeyError(f"No implementation registered for {protocol.__name__}")

        impl = registrations[0].implementation
        if isinstance(impl, type):
            return impl()
        return impl

    def resolve_all(self, protocol: type[P]) -> list[P]:
        """Resolve all implementations of a protocol.

        Args:
            protocol: Protocol to resolve

        Returns:
            List of implementations (highest priority first)
        """
        registrations = self._registrations.get(protocol, [])
        result: list[P] = []
        for reg in registrations:
            impl = reg.implementation
            if isinstance(impl, type):
                result.append(impl())
            else:
                result.append(impl)
        return result

    def resolve_by_category(self, category: VendorCategory) -> dict[type, Any]:
        """Resolve all implementations in a category.

        Args:
            category: Category to resolve

        Returns:
            Dict mapping protocols to implementations
        """
        result: dict[type, Any] = {}
        for protocol, registrations in self._registrations.items():
            for reg in registrations:
                if reg.category == category:
                    result[protocol] = self.resolve(protocol)
                    break
        return result

    def has(self, protocol: type[P]) -> bool:
        """Check if a protocol has any registrations.

        Args:
            protocol: Protocol to check

        Returns:
            True if registered
        """
        return protocol in self._registrations and len(self._registrations[protocol]) > 0

    def get_registration(
        self,
        protocol: type[P],
    ) -> ProtocolRegistration[P] | None:
        """Get the highest-priority registration.

        Args:
            protocol: Protocol to get

        Returns:
            Registration or None
        """
        registrations = self._registrations.get(protocol)
        if registrations:
            return registrations[0]
        return None

    def clear(self) -> None:
        """Clear all registrations."""
        self._registrations.clear()
        self._singletons.clear()

    def __contains__(self, protocol: type) -> bool:
        return self.has(protocol)

    def __len__(self) -> int:
        return len(self._registrations)


# ============================================================================
# Abstract Factory Base
# ============================================================================


class AbstractFactory(ABC, Generic[T_co]):
    """Abstract base for vendor factories."""

    @abstractmethod
    def create(self) -> T_co:
        """Create an instance of the vendor abstraction.

        Returns:
            New instance
        """
        ...

    @abstractmethod
    def get_protocol(self) -> type:
        """Get the protocol type this factory produces.

        Returns:
            Protocol type
        """
        ...

    @property
    @abstractmethod
    def category(self) -> VendorCategory:
        """Get the vendor category.

        Returns:
            Vendor category
        """
        ...


# ============================================================================
# Vendor Interface Collections
# ============================================================================


@dataclass(frozen=True, slots=True)
class TorchInterfaces:
    """Collection of PyTorch-related protocols.

    Provides access to all torch vendor abstractions.
    """

    from ._torch import (
        Tensor as TensorProtocol,
    )
    from ._torch import (
        TorchDevice,
        TorchDtype,
    )
    from ._torch import (
        TorchModule as ModuleProtocol,
    )
    from ._torch import (
        TorchOptimizer as OptimizerProtocol,
    )

    tensor: type = TensorProtocol
    device: type = TorchDevice
    dtype: Any = TorchDtype  # UnionType, not a class
    module: type = ModuleProtocol
    optimizer: type = OptimizerProtocol


@dataclass(frozen=True, slots=True)
class TransformersInterfaces:
    """Collection of HuggingFace Transformers protocols."""

    from ._transformers import (
        AutoModel as AutoModelProtocol,
    )
    from ._transformers import (
        AutoTokenizer as AutoTokenizerProtocol,
    )
    from ._transformers import (
        PreTrainedModel as ModelProtocol,
    )
    from ._transformers import (
        PreTrainedTokenizer as TokenizerProtocol,
    )

    model: type = ModelProtocol
    tokenizer: type = TokenizerProtocol
    auto_model: type = AutoModelProtocol
    auto_tokenizer: type = AutoTokenizerProtocol


@dataclass(frozen=True, slots=True)
class AgentsInterfaces:
    """Collection of OpenAI Agents SDK protocols."""

    from ._agents import AgentOutputSchema, RunResult
    from ._agents import AgentProtocol as AgentType
    from ._agents import FunctionToolProtocol as ToolType
    from ._agents import RunnerProtocol as RunnerType

    agent: type = AgentType
    runner: type = RunnerType
    tool: type = ToolType
    output_schema: type = AgentOutputSchema
    run_result: type = RunResult


@dataclass(frozen=True, slots=True)
class OpenAIInterfaces:
    """Collection of OpenAI client protocols."""

    from ._openai import ChatCompletion, ChatMessage, EmbeddingResponse
    from ._openai import OpenAIClient as ClientProtocol

    client: type = ClientProtocol
    chat_completion: type = ChatCompletion
    chat_message: type = ChatMessage
    embedding_response: type = EmbeddingResponse


@dataclass(frozen=True, slots=True)
class DockerModelRunnerInterfaces:
    """Collection of Docker Model Runner protocols."""

    from ._docker_model_runner import DockerModelRunnerProtocol as ClientProtocol
    from ._docker_model_runner import (
        GpuDeviceInfo,
        InferenceProtocol,
        InferenceResult,
        ModelInfo,
        ModelManagerProtocol,
        ModelRunnerConfig,
        RunnerInfo,
        RunnerProtocol,
        StreamChunk,
    )

    client: type = ClientProtocol
    model_manager: type = ModelManagerProtocol
    runner: type = RunnerProtocol
    inference: type = InferenceProtocol
    model_info: type = ModelInfo
    gpu_device_info: type = GpuDeviceInfo
    runner_info: type = RunnerInfo
    inference_result: type = InferenceResult
    stream_chunk: type = StreamChunk
    config: type = ModelRunnerConfig


@dataclass(frozen=True, slots=True)
class RedisInterfaces:
    """Collection of Redis client protocols."""

    from ._redis import (
        BlockingConnectionPoolProtocol,
        ConnectionPoolProtocol,
        ConnectionProtocol,
        CredentialProviderProtocol,
        PipelineProtocol,
        PubSubProtocol,
        SentinelConnectionPoolProtocol,
        SentinelProtocol,
    )
    from ._redis import RedisProtocol as ClientProtocol

    client: type = ClientProtocol
    connection: type = ConnectionProtocol
    connection_pool: type = ConnectionPoolProtocol
    blocking_pool: type = BlockingConnectionPoolProtocol
    sentinel: type = SentinelProtocol
    sentinel_pool: type = SentinelConnectionPoolProtocol
    pipeline: type = PipelineProtocol
    pubsub: type = PubSubProtocol
    credential_provider: type = CredentialProviderProtocol


@dataclass(frozen=True, slots=True)
class ThreadingInterfaces:
    """Collection of Python 3.15 threading protocols."""

    from ._threading import (
        BarrierProtocol,
        ConditionProtocol,
        EventProtocol,
        ExceptHookArgs,
        FreeThreadingConfig,
        FutureProtocol,
        LockProtocol,
        LockTypeProtocol,
        RLockProtocol,
        SemaphoreProtocol,
        ThreadHandleProtocol,
        ThreadLocalProtocol,
        ThreadPoolExecutorProtocol,
        ThreadProtocol,
    )

    lock: type = LockProtocol
    lock_type: type = LockTypeProtocol
    rlock: type = RLockProtocol
    condition: type = ConditionProtocol
    event: type = EventProtocol
    semaphore: type = SemaphoreProtocol
    barrier: type = BarrierProtocol
    thread: type = ThreadProtocol
    thread_handle: type = ThreadHandleProtocol
    thread_local: type = ThreadLocalProtocol
    thread_pool: type = ThreadPoolExecutorProtocol
    future: type = FutureProtocol
    except_hook_args: type = ExceptHookArgs
    free_threading_config: type = FreeThreadingConfig


@dataclass(frozen=True, slots=True)
class CTypesInterfaces:
    """Collection of ctypes FFI protocols."""

    from ._ctypes import (
        ArrayProtocol,
        CDataProtocol,
        CDLLProtocol,
        CFuncPtrProtocol,
        PointerProtocol,
        SimpleCDataProtocol,
        StructureProtocol,
        UnionProtocol,
    )

    cdata: type = CDataProtocol
    simple_cdata: type = SimpleCDataProtocol
    structure: type = StructureProtocol
    union: type = UnionProtocol
    array: type = ArrayProtocol
    pointer: type = PointerProtocol
    cfunc_ptr: type = CFuncPtrProtocol
    cdll: type = CDLLProtocol


@dataclass(frozen=True, slots=True)
class ProfilerInterfaces:
    """Collection of profiler protocols."""

    from ._profiler import (
        FunctionProfile,
        ProfileProtocol,
        SortKey,
        StatsProfile,
        StatsProtocol,
    )

    profile: type = ProfileProtocol
    stats: type = StatsProtocol
    function_profile: type = FunctionProfile
    stats_profile: type = StatsProfile
    sort_key: type = SortKey


@dataclass(frozen=True, slots=True)
class SafetensorsInterfaces:
    """Collection of safetensors protocols."""

    from ._safetensors import SafeOpenProtocol, SafetensorError

    safe_open: type = SafeOpenProtocol
    error: type = SafetensorError


@dataclass(frozen=True, slots=True)
class PytestInterfaces:
    """Collection of pytest testing protocols."""

    from ._pytest import (
        CaptureFixtureProtocol,
        CollectionResult,
        ConfigProtocol,
        FixtureDecoratorProtocol,
        FixtureInfo,
        FixtureRequestProtocol,
        ItemProtocol,
        MarkDecoratorProtocol,
        MarkInfo,
        MonkeyPatchProtocol,
        PytestProtocol,
        RaisesContextProtocol,
        RecorderProtocol,
        SessionProtocol,
        SessionResult,
        TestItem,
        TestOutcome,
        TestReport,
        TmpPathFactoryProtocol,
    )

    pytest: type = PytestProtocol
    config: type = ConfigProtocol
    session: type = SessionProtocol
    item: type = ItemProtocol
    fixture_request: type = FixtureRequestProtocol
    fixture_decorator: type = FixtureDecoratorProtocol
    mark_decorator: type = MarkDecoratorProtocol
    capture: type = CaptureFixtureProtocol
    monkeypatch: type = MonkeyPatchProtocol
    tmp_path_factory: type = TmpPathFactoryProtocol
    recorder: type = RecorderProtocol
    raises_context: type = RaisesContextProtocol
    mark_info: type = MarkInfo
    fixture_info: type = FixtureInfo
    test_item: type = TestItem
    test_report: type = TestReport
    test_outcome: type = TestOutcome
    collection_result: type = CollectionResult
    session_result: type = SessionResult


# ============================================================================
# Unified Factory
# ============================================================================


class VendorFactory:
    """Unified factory for all vendor abstractions.

    Provides centralized access to all vendor interface collections
    and manages the protocol registry.

    Example:
        factory = VendorFactory()

        # Access interface collections
        tensor_protocol = factory.torch.tensor
        redis_client = factory.redis.client

        # Register custom implementations
        factory.registry.register(
            RedisProtocol,
            MyRedisImpl,
            VendorCategory.REDIS,
        )

        # Resolve implementations
        client = factory.registry.resolve(RedisProtocol)
    """

    __slots__ = (
        "_registry",
        "_torch",
        "_transformers",
        "_agents",
        "_openai",
        "_docker_model_runner",
        "_redis",
        "_threading",
        "_ctypes",
        "_profiler",
        "_safetensors",
        "_pytest",
    )

    def __init__(self, registry: ProtocolRegistry | None = None) -> None:
        """Initialize the vendor factory.

        Args:
            registry: Optional custom registry (creates new if None)
        """
        super().__init__()
        self._registry = registry or ProtocolRegistry()

        # Lazy-initialized interface collections
        self._torch: TorchInterfaces | None = None
        self._transformers: TransformersInterfaces | None = None
        self._agents: AgentsInterfaces | None = None
        self._openai: OpenAIInterfaces | None = None
        self._docker_model_runner: DockerModelRunnerInterfaces | None = None
        self._redis: RedisInterfaces | None = None
        self._threading: ThreadingInterfaces | None = None
        self._ctypes: CTypesInterfaces | None = None
        self._profiler: ProfilerInterfaces | None = None
        self._safetensors: SafetensorsInterfaces | None = None
        self._pytest: PytestInterfaces | None = None

    @property
    def registry(self) -> ProtocolRegistry:
        """Access the protocol registry."""
        return self._registry

    @property
    def torch(self) -> TorchInterfaces:
        """PyTorch interface collection."""
        if self._torch is None:
            self._torch = TorchInterfaces()
        return self._torch

    @property
    def transformers(self) -> TransformersInterfaces:
        """Transformers interface collection."""
        if self._transformers is None:
            self._transformers = TransformersInterfaces()
        return self._transformers

    @property
    def agents(self) -> AgentsInterfaces:
        """OpenAI Agents interface collection."""
        if self._agents is None:
            self._agents = AgentsInterfaces()
        return self._agents

    @property
    def openai(self) -> OpenAIInterfaces:
        """OpenAI client interface collection."""
        if self._openai is None:
            self._openai = OpenAIInterfaces()
        return self._openai

    @property
    def docker_model_runner(self) -> DockerModelRunnerInterfaces:
        """Docker Model Runner interface collection."""
        if self._docker_model_runner is None:
            self._docker_model_runner = DockerModelRunnerInterfaces()
        return self._docker_model_runner

    @property
    def redis(self) -> RedisInterfaces:
        """Redis interface collection."""
        if self._redis is None:
            self._redis = RedisInterfaces()
        return self._redis

    @property
    def threading(self) -> ThreadingInterfaces:
        """Threading interface collection."""
        if self._threading is None:
            self._threading = ThreadingInterfaces()
        return self._threading

    @property
    def ctypes(self) -> CTypesInterfaces:
        """CTypes interface collection."""
        if self._ctypes is None:
            self._ctypes = CTypesInterfaces()
        return self._ctypes

    @property
    def profiler(self) -> ProfilerInterfaces:
        """Profiler interface collection."""
        if self._profiler is None:
            self._profiler = ProfilerInterfaces()
        return self._profiler

    @property
    def safetensors(self) -> SafetensorsInterfaces:
        """Safetensors interface collection."""
        if self._safetensors is None:
            self._safetensors = SafetensorsInterfaces()
        return self._safetensors

    @property
    def pytest(self) -> PytestInterfaces:
        """Pytest interface collection."""
        if self._pytest is None:
            self._pytest = PytestInterfaces()
        return self._pytest

    def get_interfaces_by_category(
        self,
        category: VendorCategory,
    ) -> (
        TorchInterfaces
        | TransformersInterfaces
        | AgentsInterfaces
        | OpenAIInterfaces
        | DockerModelRunnerInterfaces
        | RedisInterfaces
        | ThreadingInterfaces
        | CTypesInterfaces
        | ProfilerInterfaces
        | SafetensorsInterfaces
        | PytestInterfaces
    ):
        """Get interface collection by category.

        Args:
            category: Vendor category

        Returns:
            Corresponding interface collection
        """
        mapping = {
            VendorCategory.TORCH: self.torch,
            VendorCategory.TRANSFORMERS: self.transformers,
            VendorCategory.AGENTS: self.agents,
            VendorCategory.OPENAI: self.openai,
            VendorCategory.DOCKER_MODEL_RUNNER: self.docker_model_runner,
            VendorCategory.REDIS: self.redis,
            VendorCategory.THREADING: self.threading,
            VendorCategory.CTYPES: self.ctypes,
            VendorCategory.PROFILER: self.profiler,
            VendorCategory.SAFETENSORS: self.safetensors,
            VendorCategory.PYTEST: self.pytest,
        }
        return mapping[category]

    def list_all_protocols(self) -> dict[VendorCategory, list[type]]:
        """List all available protocol types by category.

        Returns:
            Dict mapping categories to lists of protocol types
        """
        return {
            VendorCategory.TORCH: [
                self.torch.tensor,
                self.torch.device,
                self.torch.module,
                self.torch.optimizer,
            ],
            VendorCategory.TRANSFORMERS: [
                self.transformers.model,
                self.transformers.tokenizer,
                self.transformers.auto_model,
                self.transformers.auto_tokenizer,
            ],
            VendorCategory.AGENTS: [
                self.agents.agent,
                self.agents.runner,
                self.agents.tool,
            ],
            VendorCategory.OPENAI: [
                self.openai.client,
                self.openai.chat_completion,
            ],
            VendorCategory.DOCKER_MODEL_RUNNER: [
                self.docker_model_runner.client,
                self.docker_model_runner.model_manager,
                self.docker_model_runner.runner,
                self.docker_model_runner.inference,
            ],
            VendorCategory.REDIS: [
                self.redis.client,
                self.redis.connection,
                self.redis.pipeline,
                self.redis.pubsub,
            ],
            VendorCategory.THREADING: [
                self.threading.lock,
                self.threading.thread,
                self.threading.thread_pool,
            ],
            VendorCategory.CTYPES: [
                self.ctypes.cdata,
                self.ctypes.structure,
                self.ctypes.cdll,
            ],
            VendorCategory.PROFILER: [
                self.profiler.profile,
                self.profiler.stats,
            ],
            VendorCategory.SAFETENSORS: [
                self.safetensors.safe_open,
            ],
            VendorCategory.PYTEST: [
                self.pytest.pytest,
                self.pytest.config,
                self.pytest.session,
                self.pytest.fixture_request,
            ],
        }


# ============================================================================
# Global Factory Instance
# ============================================================================

# Singleton factory instance for convenient access
_global_factory: VendorFactory | None = None


def get_vendor_factory() -> VendorFactory:
    """Get the global vendor factory instance.

    Returns:
        Global VendorFactory singleton
    """
    global _global_factory
    if _global_factory is None:
        _global_factory = VendorFactory()
    return _global_factory


def reset_vendor_factory() -> None:
    """Reset the global vendor factory (for testing)."""
    global _global_factory
    if _global_factory is not None:
        _global_factory.registry.clear()
    _global_factory = None


# ============================================================================
# Module Exports
# ============================================================================

__all__: Final[list[str]] = [
    # Enums
    "VendorCategory",
    # Registry
    "ProtocolRegistration",
    "ProtocolRegistry",
    # Abstract Factory
    "AbstractFactory",
    # Interface Collections
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
    # Unified Factory
    "VendorFactory",
    # Global Access
    "get_vendor_factory",
    "reset_vendor_factory",
]
