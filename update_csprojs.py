import os
import re

# Map of project name to new category folder
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

root_dir = os.getcwd()


def get_new_path(project_name):
    category = project_map.get(project_name)
    if category:
        return os.path.join(root_dir, category, project_name, f"{project_name}.csproj")
    return None


def update_csproj(file_path):
    with open(file_path, "r", encoding="utf-8") as f:
        content = f.read()

    # Update ProjectReference
    def replace_ref(match):
        original_path = match.group(1)
        # Normalize path separators
        original_path = original_path.replace("\\", "/")

        # Extract project name from path
        basename = os.path.basename(original_path)
        proj_name = os.path.splitext(basename)[0]

        target_path = get_new_path(proj_name)
        if target_path:
            # Calculate relative path from current file to target file
            rel_path = os.path.relpath(target_path, os.path.dirname(file_path))
            return f'Include="{rel_path}"'
        return match.group(0)

    content = re.sub(r'Include="([^"]+\.csproj)"', replace_ref, content)

    # Update Content Include for Native DLLs
    # Old pattern: ..\Aspire-Full.Tensor\build\
    # New pattern needs to be calculated relative to current file
    # Target is: root/AI/Aspire-Full.Tensor/build/

    tensor_build_path = os.path.join(root_dir, "AI", "Aspire-Full.Tensor", "build")

    def replace_content(match):
        # match.group(1) is the path after Include="
        path = match.group(1)
        if "Aspire-Full.Tensor" in path and "build" in path:
            # It's a reference to the tensor build
            # We want to replace the path to point to the new location
            # The file name is at the end
            filename = os.path.basename(path)
            # Construct absolute path to the target file (conceptually)
            target_file = os.path.join(tensor_build_path, filename)
            rel_path = os.path.relpath(target_file, os.path.dirname(file_path))
            return f'Include="{rel_path}"'
        return match.group(0)

    # Regex for Content Include that might contain the tensor path
    # We look for Include="...Aspire-Full.Tensor..."
    content = re.sub(
        r'Include="([^"]*Aspire-Full\.Tensor[^"]*)"', replace_content, content
    )

    # Also update the Exists(...) condition
    def replace_exists(match):
        path = match.group(1)
        if "Aspire-Full.Tensor" in path and "build" in path:
            filename = os.path.basename(path)
            target_file = os.path.join(tensor_build_path, filename)
            rel_path = os.path.relpath(target_file, os.path.dirname(file_path))
            return f"Exists('{rel_path}')"
        return match.group(0)

    content = re.sub(r"Exists\('([^']*)'\)", replace_exists, content)

    with open(file_path, "w", encoding="utf-8") as f:
        f.write(content)


# Walk and update
for root, dirs, files in os.walk(root_dir):
    for file in files:
        if file.endswith(".csproj"):
            update_csproj(os.path.join(root, file))

print("CSPROJ updates complete.")
