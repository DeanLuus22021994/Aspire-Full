# Plan: Subnet & Registry Implementation

- [x] **Phase 1: Infrastructure**
    - [x] Create `.specify` structure.
    - [x] Update `Aspire-Full/AppHost.cs` to include `registry` container.
    - [x] Verify `aspire-network` configuration.

- [x] **Phase 2: Agent Containerization**
    - [x] Create `Aspire-Full.Python/python-agents/Dockerfile.agent`.
    - [x] Create `Aspire-Full.Python/python-agents/scripts/download_models.py` (for pre-caching).
    - [x] Create `scripts/provision-agents.ps1` (Build & Push script).

- [ ] **Phase 3: Integration**
    - [ ] Update `Aspire-Full/AppHost.cs` to use the containerized agent.
    - [ ] Verify GPU access inside container.
    - [ ] Verify "Instant Readiness" (measure startup time).
