# Subagents Library & Agent

## Overview

`Aspire-Full.Subagents` centralizes the contracts for the ArcFace sandbox subagents (Embedding Service, Vector Store, Users Kernel). It ships:

- Canonical metadata pulled from `sandboxes/arcface-upsert/AGENTS.md`
- `SubagentSelfReviewService` for generating self-retrospectives
- Delegation helpers that classify follow-up work by priority

`Aspire-Full.Agents` is a lightweight console companion that ingests a JSON update, produces a retrospective, and emits a delegation plan for hand-off to other automation.

## JSON Workflow

1. Create an input file (default `subagents.update.json`):

```json
{
  "role": "EmbeddingService",
  "completed": ["Validated arcface_r100_v1 hashes"],
  "risks": ["Need newer CUDA driver"],
  "next": ["Wire batching harness"],
  "delegations": ["GPU driver upgrade"]
}
```

2. Run the agent:

```bash
dotnet run --project Aspire-Full.Agents -- --input subagents.update.json
```

3. Review the generated retrospective + delegation at `subagents.update.output.json` (or supply `--output`).

## Data Contracts

- `SubagentRole`: EmbeddingService, VectorStore, UsersKernel
- `SubagentDefinition`: mission/inputs/outputs/constraints/interfaces per role
- `SubagentRetrospective`: highlights, risks, next steps, timestamp
- `SubagentDelegationPlan`: prioritized delegation items derived from the update

Use `SubagentSelfReviewService` to hydrate definitions, retrospectives, and delegation plans inside other automation or dashboards.
