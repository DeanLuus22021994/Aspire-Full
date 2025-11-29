import logging

from transformers import AutoModel, AutoTokenizer

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

MODELS_TO_DOWNLOAD = [
    "sentence-transformers/all-MiniLM-L6-v2",
]


def download_models():
    logger.info("Starting model download...")

    for model_name in MODELS_TO_DOWNLOAD:
        logger.info(f"Downloading {model_name}...")
        try:
            # Download tokenizer and model
            AutoTokenizer.from_pretrained(model_name)
            AutoModel.from_pretrained(model_name)
            logger.info(f"Successfully downloaded {model_name}")
        except Exception as e:
            logger.error(f"Failed to download {model_name}: {e}")
            raise

    logger.info("All models downloaded successfully.")


if __name__ == "__main__":
    download_models()
