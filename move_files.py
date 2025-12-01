import os
import shutil

structure = {
    "Infra": [
        "Aspire-Full",
        "Aspire-Full.ServiceDefaults",
        "Aspire-Full.DevContainer",
        "Aspire-Full.DockerRegistry",
    ],
    "Core": [
        "Aspire-Full.Connectors",
        "Aspire-Full.Pipeline",
        "Aspire-Full.Github",
        "Aspire-Full.Shared",
    ],
    "AI": [
        "Aspire-Full.Agents",
        "Aspire-Full.Embeddings",
        "Aspire-Full.Tensor",
        "Aspire-Full.VectorStore",
        "Aspire-Full.Python",
        "Aspire-Full.Qdrant",
        "Aspire-Full.Subagents",
    ],
    "Web": [
        "Aspire-Full.Api",
        "Aspire-Full.Web",
        "Aspire-Full.WebAssembly",
        "Aspire-Full.Gateway",
    ],
    "Tests": [
        "Aspire-Full.Tests.Unit",
        "Aspire-Full.Tests.E2E",
        "Aspire-Full.Gateway.Tests",
    ],
}

root_dir = os.getcwd()

for category, projects in structure.items():
    cat_dir = os.path.join(root_dir, category)
    if not os.path.exists(cat_dir):
        os.makedirs(cat_dir)
        print(f"Created {category}")

    for proj in projects:
        src = os.path.join(root_dir, proj)
        dst = os.path.join(cat_dir, proj)
        if os.path.exists(src):
            try:
                shutil.move(src, dst)
                print(f"Moved {proj} to {category}")
            except Exception as e:
                print(f"Error moving {proj}: {e}")
        elif os.path.exists(dst):
            print(f"{proj} already in {category}")
        else:
            print(f"Warning: {proj} not found in root or {category}")

    # Move slnx
    slnx_name = f"Aspire-Full.{category}.slnx"
    src_slnx = os.path.join(root_dir, slnx_name)
    dst_slnx = os.path.join(cat_dir, slnx_name)
    if os.path.exists(src_slnx):
        try:
            shutil.move(src_slnx, dst_slnx)
            print(f"Moved {slnx_name} to {category}")
        except Exception as e:
            print(f"Error moving {slnx_name}: {e}")
