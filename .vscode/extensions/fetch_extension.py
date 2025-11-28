#!/usr/bin/env python3
"""Download the latest VS Code marketplace extension package into the cache directory."""

from __future__ import annotations

import json
import os
import sys
import urllib.error
import urllib.request

API_URL = "https://marketplace.visualstudio.com/_apis/public/gallery/extensionquery"
API_VERSION = "3.0-preview.1"


def _payload(extension_id: str) -> bytes:
    body = {
        "filters": [
            {
                "criteria": [
                    {
                        "filterType": 7,
                        "value": extension_id,
                    }
                ]
            }
        ],
        "flags": 1030,
    }
    return json.dumps(body).encode("utf-8")


def _latest_vsix_url(payload: bytes) -> str:
    """Return the marketplace URL that hosts the VSIX artifact."""
    request = urllib.request.Request(
        API_URL,
        data=payload,
        headers={
            "Accept": f"application/json;api-version={API_VERSION}",
            "Content-Type": "application/json",
            "User-Agent": "Aspire-Full-extensions-fetcher/1.0",
        },
        method="POST",
    )
    with urllib.request.urlopen(request, timeout=20) as response:  # noqa: S310
        data = json.loads(response.read().decode("utf-8"))

    extensions = data["results"][0]["extensions"]
    if not extensions:
        raise RuntimeError("Extension not found")
    version = extensions[0]["versions"][0]
    for asset in version["files"]:
        if asset["assetType"] == "Microsoft.VisualStudio.Services.VSIXPackage":
            return asset["source"]
    raise RuntimeError("VSIX download URL not found in Marketplace response")


def main() -> None:
    """Download the requested VSIX into the cache directory."""
    extension_id = os.environ.get("EXTENSION_ID")
    destination = os.environ.get("EXTENSION_CACHE")
    if not extension_id or not destination:
        raise RuntimeError("EXTENSION_ID and EXTENSION_CACHE variables are required")

    url = _latest_vsix_url(_payload(extension_id))
    os.makedirs(destination, exist_ok=True)
    filename = f"{extension_id}.vsix"
    target = os.path.join(destination, filename)
    urllib.request.urlretrieve(url, target)  # noqa: S310
    print(f"Downloaded {extension_id} to {target}")


if __name__ == "__main__":
    try:
        main()
    except (RuntimeError, urllib.error.HTTPError, urllib.error.URLError) as exc:
        print(f"Extension download failed: {exc}", file=sys.stderr)
        sys.exit(1)
