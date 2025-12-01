import os
import re

root_dir = os.getcwd()

categories = ["Infra", "Core", "AI", "Web", "Tests"]

# Update Scoped SLNX files
for cat in categories:
    slnx_path = os.path.join(root_dir, cat, f"Aspire-Full.{cat}.slnx")
    if os.path.exists(slnx_path):
        with open(slnx_path, "r", encoding="utf-8") as f:
            content = f.read()

        # Update Solution Items to go up one level
        # Use regex to be safe
        content = re.sub(
            r'Path="Directory.Build.props"', 'Path="../Directory.Build.props"', content
        )
        content = re.sub(
            r'Path="Directory.Packages.props"',
            'Path="../Directory.Packages.props"',
            content,
        )
        content = re.sub(r'Path="global.json"', 'Path="../global.json"', content)
        content = re.sub(r'Path="NuGet.config"', 'Path="../NuGet.config"', content)

        with open(slnx_path, "w", encoding="utf-8") as f:
            f.write(content)
        print(f"Updated {slnx_path}")

# Update Master SLNX
master_slnx = os.path.join(root_dir, "Aspire-Full.slnx")
if os.path.exists(master_slnx):
    with open(master_slnx, "r", encoding="utf-8") as f:
        content = f.read()

    project_map = {
        "Aspire-Full": "Infra",
        "Aspire-Full.ServiceDefaults": "Infra",
        "Aspire-Full.DevContainer": "Infra",
        "Aspire-Full.DockerRegistry": "Infra",
        "Aspire-Full.Connectors": "Core",
        "Aspire-Full.Pipeline": "Core",
        "Aspire-Full.Github": "Core",
        "Aspire-Full.Shared": "Core",
        "Aspire-Full.Agents": "AI",
        "Aspire-Full.Embeddings": "AI",
        "Aspire-Full.Tensor": "AI",
        "Aspire-Full.VectorStore": "AI",
        "Aspire-Full.Python": "AI",
        "Aspire-Full.Qdrant": "AI",
        "Aspire-Full.Subagents": "AI",
        "Aspire-Full.Api": "Web",
        "Aspire-Full.Web": "Web",
        "Aspire-Full.WebAssembly": "Web",
        "Aspire-Full.Gateway": "Web",
        "Aspire-Full.Tests.Unit": "Tests",
        "Aspire-Full.Tests.E2E": "Tests",
        "Aspire-Full.Gateway.Tests": "Tests",
    }

    for proj, cat in project_map.items():
        # Regex to match Path="Proj/Proj.csproj"
        # We want to replace it with Path="Cat/Proj/Proj.csproj"
        pattern = f'Path="{re.escape(proj)}/{re.escape(proj)}.csproj"'
        replacement = f'Path="{cat}/{proj}/{proj}.csproj"'
        content = re.sub(pattern, replacement, content)

    with open(master_slnx, "w", encoding="utf-8") as f:
        f.write(content)
    print(f"Updated {master_slnx}")
