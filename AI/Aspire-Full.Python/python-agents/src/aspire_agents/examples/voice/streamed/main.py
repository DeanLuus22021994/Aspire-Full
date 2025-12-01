"""
This module implements a streamed voice agent example using Textual.
"""

from __future__ import annotations

import asyncio
from typing import TYPE_CHECKING, Any, cast

import numpy as np

if TYPE_CHECKING:
    from typing import Generic, Iterator, TypeVar

    T = TypeVar("T")

    class sd:
        """SoundDevice stub."""

        class OutputStream:
            """OutputStream stub."""

            def __init__(self, samplerate: int, channels: int, dtype: Any) -> None:
                _ = (samplerate, channels, dtype)

            def start(self) -> None:
                pass

            def write(self, data: Any) -> None:
                _ = data

            def close(self) -> None:
                pass

        class InputStream:
            """InputStream stub."""

            def __init__(self, channels: int, samplerate: int, dtype: str) -> None:
                _ = (channels, samplerate, dtype)

            def start(self) -> None:
                pass

            def stop(self) -> None:
                pass

            def close(self) -> None:
                pass

            read_available: int

            def read(self, size: int) -> tuple[Any, Any]:
                _ = size
                return (None, None)

        @staticmethod
        def query_devices() -> Any:
            return None

    class StreamedAudioInput:
        """StreamedAudioInput stub."""

        async def add_audio(self, audio: Any) -> None:
            _ = audio

    class VoicePipeline:
        """VoicePipeline stub."""

        def __init__(self, workflow: Any) -> None:
            _ = workflow

        async def run(self, input_data: Any) -> Any:
            _ = input_data
            return None

    class events:
        """Events stub."""

        class Key:
            """Key stub."""

            key: str

    class App(Generic[T]):
        """App stub."""

        def run(self) -> None:
            pass

        def exit(self, result: Any = None) -> None:
            _ = result

        def run_worker(self, worker: Any) -> None:
            _ = worker

        def query_one(self, selector: Any, type_hint: Any = None) -> Any:
            _ = (selector, type_hint)
            return cast(Any, None)

    ComposeResult = Iterator[Any]

    class Container:
        """Container stub."""

        def __enter__(self) -> None:
            pass

        def __exit__(self, exc_type: Any, exc_value: Any, traceback: Any) -> None:
            _ = (exc_type, exc_value, traceback)

    def reactive(default: Any) -> Any:
        return default

    class Button:
        """Button stub."""

        def press(self) -> None:
            pass

    class Static:
        """Static stub."""

        id: str | None

        def __init__(self, widget_id: str | None = None, **kwargs: Any) -> None:
            _ = (widget_id, kwargs)

    class RichLog(Static):
        """RichLog stub."""

        def __init__(
            self,
            widget_id: str | None = None,
            wrap: bool = False,
            highlight: bool = False,
            markup: bool = False,
            **kwargs: Any,
        ) -> None:
            super().__init__(widget_id=widget_id, **kwargs)
            _ = (wrap, highlight, markup)

        def write(self, content: Any) -> None:
            _ = content
else:
    try:
        import sounddevice as sd
    except ImportError:
        sd = None

    try:
        from agents.voice import StreamedAudioInput, VoicePipeline
    except ImportError:
        StreamedAudioInput = None
        VoicePipeline = None

    try:
        from textual import events
        from textual.app import App, ComposeResult
        from textual.containers import Container
        from textual.reactive import reactive
        from textual.widgets import Button, RichLog, Static
    except ImportError:
        events = None
        App = Any
        ComposeResult = Any
        Container = Any
        reactive = Any
        Button = Any
        RichLog = Any
        Static = Any

try:
    from aspire_agents.gpu import ensure_tensor_core_gpu  # type: ignore
except ImportError:

    def ensure_tensor_core_gpu() -> Any:  # type: ignore
        """Ensure that the tensor core GPU is available."""


# Import MyWorkflow class - handle both module and package use cases
if TYPE_CHECKING:
    # For type checking, use the relative import
    from .my_workflow import MyWorkflow
else:
    # At runtime, try both import styles
    try:
        # Try relative import first (when used as a package)
        from .my_workflow import MyWorkflow
    except ImportError:
        # Fall back to direct import (when run as a script)
        from my_workflow import MyWorkflow

CHUNK_LENGTH_S = 0.05  # 100ms
SAMPLE_RATE = 24000
FORMAT = np.int16
CHANNELS = 1


class Header(Static):
    """A header widget."""

    session_id = reactive("")

    def render(self) -> str:
        return "Speak to the agent. When you stop speaking, it will respond."


class AudioStatusIndicator(Static):
    """A widget that shows the current audio recording status."""

    is_recording = reactive(False)

    def render(self) -> str:
        status = (
            "🔴 Recording... (Press K to stop)"
            if self.is_recording
            else "⚪ Press K to start recording (Q to quit)"
        )
        return status


class RealtimeApp(App[None]):
    """
    A Textual app for the Realtime Agent.
    """

    CSS = """
        Screen {
            background: #1a1b26;  /* Dark blue-grey background */
        }

        Container {
            border: double rgb(91, 164, 91);
        }

        Horizontal {
            width: 100%;
        }

        #input-container {
            height: 5;  /* Explicit height for input container */
            margin: 1 1;
            padding: 1 2;
        }

        Input {
            width: 80%;
            height: 3;  /* Explicit height for input */
        }

        Button {
            width: 20%;
            height: 3;  /* Explicit height for button */
        }

        #bottom-pane {
            width: 100%;
            height: 82%;  /* Reduced to make room for session display */
            border: round rgb(205, 133, 63);
            content-align: center middle;
        }

        #status-indicator {
            height: 3;
            content-align: center middle;
            background: #2a2b36;
            border: solid rgb(91, 164, 91);
            margin: 1 1;
        }

        #session-display {
            height: 3;
            content-align: center middle;
            background: #2a2b36;
            border: solid rgb(91, 164, 91);
            margin: 1 1;
        }

        Static {
            color: white;
        }
    """

    should_send_audio: asyncio.Event
    audio_player: sd.OutputStream | None
    last_audio_item_id: str | None
    connected: asyncio.Event
    result: Any

    def __init__(self) -> None:
        super().__init__()
        ensure_tensor_core_gpu()
        self.last_audio_item_id = None
        self.should_send_audio = asyncio.Event()
        self.connected = asyncio.Event()
        self.result = None
        self.pipeline = VoicePipeline(
            workflow=MyWorkflow(secret_word="dog", on_start=self._on_transcription)
        )
        self._audio_input = StreamedAudioInput()
        if cast(Any, sd) is not None:
            self.audio_player = sd.OutputStream(
                samplerate=SAMPLE_RATE,
                channels=CHANNELS,
                dtype=FORMAT,
            )
        else:
            self.audio_player = None

    def _on_transcription(self, transcription: str) -> None:
        """Callback for when transcription is received."""
        try:
            log_widget = self.query_one("#bottom-pane", RichLog)
            if TYPE_CHECKING:
                assert isinstance(log_widget, RichLog)
            log_widget.write(f"Transcription: {transcription}")
        except Exception:  # pylint: disable=broad-exception-caught
            pass

    def compose(self) -> ComposeResult:
        """Create child widgets for the app."""
        with Container():
            yield Header(id="session-display")
            yield AudioStatusIndicator(id="status-indicator")
            yield RichLog(id="bottom-pane", wrap=True, highlight=True, markup=True)

    async def on_mount(self) -> None:
        """Handle app mount event."""
        self.run_worker(self.start_voice_pipeline())
        self.run_worker(self.send_mic_audio())

    async def start_voice_pipeline(self) -> None:
        """Start the voice pipeline."""
        try:
            if self.audio_player:
                self.audio_player.start()
            self.result = await self.pipeline.run(self._audio_input)

            async for event in self.result.stream():
                bottom_pane = self.query_one("#bottom-pane", RichLog)
                if TYPE_CHECKING:
                    assert isinstance(bottom_pane, RichLog)
                if event.type == "voice_stream_event_audio":
                    if self.audio_player:
                        self.audio_player.write(event.data)
                    data_len = len(event.data) if event.data is not None else 0
                    msg = f"Received audio: {data_len} bytes"
                    bottom_pane.write(msg)
                elif event.type == "voice_stream_event_lifecycle":
                    bottom_pane.write(f"Lifecycle event: {event.event}")
        except Exception as e:  # pylint: disable=broad-exception-caught
            bottom_pane = self.query_one("#bottom-pane", RichLog)
            if TYPE_CHECKING:
                assert isinstance(bottom_pane, RichLog)
            bottom_pane.write(f"Error: {e}")
        finally:
            if self.audio_player:
                self.audio_player.close()

    async def send_mic_audio(self) -> None:
        """Send microphone audio to the pipeline."""
        if cast(Any, sd) is None:
            return

        device_info = sd.query_devices()
        print(device_info)

        read_size = int(SAMPLE_RATE * 0.02)

        stream = sd.InputStream(
            channels=CHANNELS,
            samplerate=SAMPLE_RATE,
            dtype="int16",
        )
        stream.start()

        status_indicator = self.query_one(AudioStatusIndicator)
        if TYPE_CHECKING:
            assert isinstance(status_indicator, AudioStatusIndicator)

        try:
            while True:
                if stream.read_available < read_size:
                    await asyncio.sleep(0)
                    continue

                await self.should_send_audio.wait()
                status_indicator.is_recording = True

                data, _ = stream.read(read_size)

                # Cast to Any to avoid mypy error about float64 vs int16
                # sounddevice returns numpy array, but type inference is tricky
                if self._audio_input:
                    await self._audio_input.add_audio(data.astype(np.int16))
                await asyncio.sleep(0)
        except KeyboardInterrupt:
            pass
        finally:
            stream.stop()
            stream.close()

    async def on_key(self, event: events.Key) -> None:
        """Handle key press events."""
        if event.key == "enter":
            self.query_one(Button).press()
            return

        if event.key == "q":
            self.exit()
            return

        if event.key == "k":
            status_indicator = self.query_one(AudioStatusIndicator)
            if status_indicator.is_recording:
                self.should_send_audio.clear()
                status_indicator.is_recording = False
            else:
                self.should_send_audio.set()
                status_indicator.is_recording = True


if __name__ == "__main__":
    app = RealtimeApp()
    app.run()
