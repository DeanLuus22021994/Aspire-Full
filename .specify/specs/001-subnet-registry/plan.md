# Plan: Subnet & Registry Implementation

- [ ] **Phase 1: Infrastructure**
    - [x] Create `.specify` structure.
    - [ ] Update `Aspire-Full/AppHost.cs` to include `registry` container.
    - [ ] Verify `aspire-network` configuration.

- [ ] **Phase 2: Agent Containerization**
    - [ ] Create `Aspire-Full.Python/python-agents/Dockerfile.agent`.
    - [ ] Create `Aspire-Full.Python/python-agents/scripts/download_models.py` (for pre-caching).
    - [ ] Create `scripts/provision-agents.ps1` (Build & Push script).

- [ ] **Phase 3: Integration**
    - [ ] Update `Aspire-Full/AppHost.cs` to use the containerized agent.
    - [ ] Verify GPU access inside container.
    - [ ] Verify "Instant Readiness" (measure startup time).
