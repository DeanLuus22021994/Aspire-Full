# Constitution

1. **Specification-First**: All major features must have a specification in `specs/` before implementation code is written.
2. **Library-First**: Core logic should be implemented as reusable libraries/modules, not embedded directly in application code.
3. **Test-First**: Tests (or at least test plans) should be defined alongside specifications.
4. **Infrastructure-as-Code**: All infrastructure (Docker, Networks) must be defined in code/config, not manual steps.
5. **High-Performance**: Default to high-performance configurations (e.g., Tensor Cores, Free-Threading) where applicable.
