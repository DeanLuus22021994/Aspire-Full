# Plan: Migrate Scripts to C# Pipeline CLI

- [ ] **Phase 1: Foundation & Infrastructure**
    - [ ] Create Command Modules (`InfraModule.cs`, `CiModule.cs`, `DevModule.cs`, `AiModule.cs`, `DocsModule.cs`).
    - [ ] Migrate `init-docker-infra.ps1` and `setup-volumes.ps1` to `pipeline infra init`.
    - [ ] Implement Docker checks (network/volume) in C#.

- [x] **Phase 2: CI/CD Migration**
  - [x] Create `CiModule` in `Modules/Ci/`.
  - [x] Migrate `manage-runner.ps1` to `pipeline ci runner`.
  - [x] Migrate `actions-cache.ps1` to `pipeline ci cache`.
  - [x] Migrate `generate-sbom.ps1` to `pipeline ci sbom`.
  - [x] Migrate `run-actions-locally.ps1` to `pipeline ci run-local`.


- [x] **Phase 3: Development Workflow Migration**
  - [x] Create `DevModule` in `Modules/Dev/`.
  - [x] Migrate `Start-Aspire.ps1` to `pipeline dev start`.
  - [x] Migrate `build.cmd` to `pipeline dev build`.
  - [x] Migrate `run-tests.ps1` to `pipeline dev test`.
  - [x] Migrate `cleanup-branches.ps1` to `pipeline dev cleanup`.


- [ ] **Phase 4: AI & Host Migration**
    - [ ] Migrate `agentic-workflows.ps1` to `pipeline ai workflows`.
    - [ ] Migrate `copilot-helper.ps1` to `pipeline ai copilot`.
    - [ ] Migrate `models-helper.ps1` to `pipeline ai models`.
    - [ ] Enhance `pipeline host` to replace `Start-Aspire.ps1` (add cleanup & GPU checks).

- [ ] **Phase 5: Cleanup**
    - [ ] Verify all commands.
    - [ ] Delete original scripts from `scripts/`.
    - [ ] Update `README.md` with new CLI usage.
