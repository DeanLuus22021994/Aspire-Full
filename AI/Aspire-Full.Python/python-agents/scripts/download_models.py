"""
Script to download models for local compute.

Uses the vendor abstraction layer for transformers to ensure type safety.
"""

from __future__ import annotations

import logging
import sys
from pathlib import Path
from typing import TYPE_CHECKING, Final

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

MODELS_TO_DOWNLOAD: Final[list[str]] = [
    "sentence-transformers/all-MiniLM-L6-v2",
]

if TYPE_CHECKING:
    from aspire_agents._vendor._transformers import AutoModel, AutoTokenizer


def _get_vendor_classes() -> (
    tuple[type["AutoTokenizer"], type["AutoModel"]]
):
    """Get vendor abstraction classes with path setup."""
    # Add parent directory to path to access vendor modules
    script_dir = Path(__file__).parent
    project_root = script_dir.parent
    src_dir = project_root / "src"
    if str(src_dir) not in sys.path:
        sys.path.insert(0, str(src_dir))

    from aspire_agents._vendor._transformers import (
        AutoModel as _AutoModel,
    )
    from aspire_agents._vendor._transformers import (
        AutoTokenizer as _AutoTokenizer,
    )

    return _AutoTokenizer, _AutoModel


def download_models() -> None:
    """Download all required models for local compute."""
    logger.info("Starting model download...")

    tokenizer_cls, model_cls = _get_vendor_classes()

    for model_name in MODELS_TO_DOWNLOAD:
        logger.info("Downloading %s...", model_name)
        try:
            # Download tokenizer and model using vendor abstractions
            tokenizer_cls.from_pretrained(model_name)
            model_cls.from_pretrained(model_name)
            logger.info("Successfully downloaded %s", model_name)
        except RuntimeError:
            logger.exception("Failed to download %s", model_name)
            raise

    logger.info("All models downloaded successfully.")


if __name__ == "__main__":
    download_models()
