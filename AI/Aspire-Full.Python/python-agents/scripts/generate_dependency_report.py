import json
import subprocess
import sys
from datetime import datetime

import yaml


def get_installed_packages():
    try:
        result = subprocess.run(["uv", "pip", "list", "--format=json"], capture_output=True, text=True, check=True)
        return json.loads(result.stdout)
    except subprocess.CalledProcessError as e:
        print(f"Error getting installed packages: {e}")
        sys.exit(1)


def get_outdated_packages():
    try:
        # uv pip list --outdated returns a table, not JSON currently in all versions,
        # but let's try to parse it or use a different approach.
        # Actually, uv pip list --outdated --format=json is supported in newer versions.
        result = subprocess.run(
            ["uv", "pip", "list", "--outdated", "--format=json"], capture_output=True, text=True, check=True
        )
        return json.loads(result.stdout)
    except subprocess.CalledProcessError:
        # Fallback if json format not supported or other error
        return []


def generate_report():
    installed = get_installed_packages()
    outdated = get_outdated_packages()

    outdated_map = {pkg["name"]: pkg for pkg in outdated}

    report_data = {
        "generated_at": datetime.now().isoformat(),
        "summary": {"total_packages": len(installed), "outdated_packages": len(outdated)},
        "packages": [],
    }

    for pkg in installed:
        name = pkg["name"]
        version = pkg["version"]

        pkg_info = {"name": name, "current_version": version, "status": "current"}

        if name in outdated_map:
            latest = outdated_map[name]["latest_version"]
            pkg_info["status"] = "outdated"
            pkg_info["latest_version"] = latest
            pkg_info["update_type"] = outdated_map[name].get("type", "unknown")

        report_data["packages"].append(pkg_info)

    with open("python-deps.config.yaml", "w") as f:
        yaml.dump(report_data, f, sort_keys=False)

    print("Report generated: python-deps.config.yaml")


if __name__ == "__main__":
    generate_report()
