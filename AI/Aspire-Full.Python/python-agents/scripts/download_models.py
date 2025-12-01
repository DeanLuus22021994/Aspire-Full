"""
Script to download models for local compute.
"""

import logging

from transformers import AutoModel, AutoTokenizer  # type: ignore

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

MODELS_TO_DOWNLOAD = [
    "sentence-transformers/all-MiniLM-L6-v2",
]


def download_models():
    logger.info("Starting model download...")

    for model_name in MODELS_TO_DOWNLOAD:
        logger.info("Downloading %s...", model_name)
        try:
            # Download tokenizer and model
            AutoTokenizer.from_pretrained(model_name)
            AutoModel.from_pretrained(model_name)
            logger.info("Successfully downloaded %s", model_name)
        except Exception as e:  # pylint: disable=broad-exception-caught
            logger.error("Failed to download %s: %s", model_name, e)
            raise

    logger.info("All models downloaded successfully.")


if __name__ == "__main__":
    download_models()
