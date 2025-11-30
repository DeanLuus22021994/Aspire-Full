"""
This module demonstrates the usage of the ComputerTool with a local Playwright browser.
"""

import asyncio
import base64
from typing import TYPE_CHECKING, Any, Literal

# pylint: disable=import-error
from agents import (  # type: ignore
    Agent,
    AsyncComputer,
    Button,
    ComputerTool,
    Environment,
    ModelSettings,
    Runner,
    trace,
)

from aspire_agents.gpu import ensure_tensor_core_gpu  # type: ignore # pylint: disable=import-error

if TYPE_CHECKING:
    from playwright.async_api import (  # type: ignore
        Browser,
        Page,
        Playwright,
        async_playwright,
    )
else:
    try:
        from playwright.async_api import Browser, Page, Playwright, async_playwright
    except ImportError:
        # For type checking or if playwright is not installed
        Browser = Any
        Page = Any
        Playwright = Any
        async_playwright = None

# Uncomment to see very verbose logs
# import logging
# logging.getLogger("openai.agents").setLevel(logging.DEBUG)
# logging.getLogger("openai.agents").addHandler(logging.StreamHandler())


async def main() -> None:
    """
    Main entry point for the computer use example.
    """
    ensure_tensor_core_gpu()
    async with LocalPlaywrightComputer() as computer:
        with trace("Computer use example"):
            agent = Agent(
                name="Browser user",
                instructions="You are a helpful agent.",
                tools=[ComputerTool(computer)],
                # Use the computer using model, and set truncation to auto because its required
                model="computer-use-preview",
                model_settings=ModelSettings(truncation="auto"),
            )
            result = await Runner.run(agent, "Search for SF sports news and summarize.")
            print(result.final_output)


CUA_KEY_TO_PLAYWRIGHT_KEY = {
    "/": "Divide",
    "\\": "Backslash",
    "alt": "Alt",
    "arrowdown": "ArrowDown",
    "arrowleft": "ArrowLeft",
    "arrowright": "ArrowRight",
    "arrowup": "ArrowUp",
    "backspace": "Backspace",
    "capslock": "CapsLock",
    "cmd": "Meta",
    "ctrl": "Control",
    "delete": "Delete",
    "end": "End",
    "enter": "Enter",
    "esc": "Escape",
    "home": "Home",
    "insert": "Insert",
    "option": "Alt",
    "pagedown": "PageDown",
    "pageup": "PageUp",
    "shift": "Shift",
    "space": " ",
    "super": "Meta",
    "tab": "Tab",
    "win": "Meta",
}


class LocalPlaywrightComputer(AsyncComputer):
    """A computer, implemented using a local Playwright browser."""

    def __init__(self):
        self._playwright: Playwright | None = None
        self._browser: Browser | None = None
        self._page: Page | None = None

    async def _get_browser_and_page(self) -> tuple[Browser, Page]:
        """
        Launch the browser and create a new page.
        """
        width, height = self.dimensions
        launch_args = [f"--window-size={width},{height}"]
        browser = await self.playwright.chromium.launch(headless=False, args=launch_args)
        page = await browser.new_page()
        await page.set_viewport_size({"width": width, "height": height})
        await page.goto("https://www.bing.com")
        return browser, page

    async def __aenter__(self) -> "LocalPlaywrightComputer":
        """
        Start the computer session.
        """
        # Start Playwright and call the subclass hook for getting browser/page
        if async_playwright:
            self._playwright = await async_playwright().start()
            self._browser, self._page = await self._get_browser_and_page()
        return self

    async def __aexit__(self, _exc_type: Any, _exc_val: Any, _exc_tb: Any) -> None:
        """
        End the computer session.
        """
        if self._browser:
            await self._browser.close()
        if self._playwright:
            await self._playwright.stop()

    @property
    def playwright(self) -> Playwright:
        assert self._playwright is not None
        return self._playwright

    @property
    def browser(self) -> Browser:
        assert self._browser is not None
        return self._browser

    @property
    def page(self) -> Page:
        assert self._page is not None
        return self._page

    @property
    def environment(self) -> Environment:
        return "browser"

    @property
    def dimensions(self) -> tuple[int, int]:
        return (1024, 768)

    async def screenshot(self) -> str:
        """Capture only the viewport (not full_page)."""
        png_bytes = await self.page.screenshot(full_page=False)
        return base64.b64encode(png_bytes).decode("utf-8")

    async def click(self, x: int, y: int, button: Button = "left") -> None:
        """
        Click at the specified coordinates.
        """
        playwright_button: Literal["left", "middle", "right"] = "left"

        # Playwright only supports left, middle, right buttons
        if button in ("left", "right", "middle"):
            playwright_button = button  # type: ignore

        await self.page.mouse.click(x, y, button=playwright_button)

    async def double_click(self, x: int, y: int) -> None:
        """
        Double click at the specified coordinates.
        """
        await self.page.mouse.dblclick(x, y)

    async def scroll(self, x: int, y: int, scroll_x: int, scroll_y: int) -> None:
        """
        Scroll the page.
        """
        await self.page.mouse.move(x, y)
        await self.page.evaluate(f"window.scrollBy({scroll_x}, {scroll_y})")

    async def type(self, text: str) -> None:
        """
        Type text.
        """
        await self.page.keyboard.type(text)

    async def wait(self) -> None:
        """
        Wait for a short duration.
        """
        await asyncio.sleep(1)

    async def move(self, x: int, y: int) -> None:
        """
        Move the mouse to the specified coordinates.
        """
        await self.page.mouse.move(x, y)

    async def keypress(self, keys: list[str]) -> None:
        """
        Press a combination of keys.
        """
        mapped_keys = [CUA_KEY_TO_PLAYWRIGHT_KEY.get(key.lower(), key) for key in keys]
        for key in mapped_keys:
            await self.page.keyboard.down(key)
        for key in reversed(mapped_keys):
            await self.page.keyboard.up(key)

    async def drag(self, path: list[tuple[int, int]]) -> None:
        """
        Drag the mouse along the specified path.
        """
        if not path:
            return
        await self.page.mouse.move(path[0][0], path[0][1])
        await self.page.mouse.down()
        for px, py in path[1:]:
            await self.page.mouse.move(px, py)
        await self.page.mouse.up()


if __name__ == "__main__":
    asyncio.run(main())
