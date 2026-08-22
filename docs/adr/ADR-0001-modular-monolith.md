# ADR-0001 — Start as a Modular Monolith

## Status
Accepted.

## Context
The platform covers HR, attendance, payroll, camp, meals, assets, workflow, reporting and integrations. These domains need clear boundaries, but an early microservice split would add deployment, data consistency and operational complexity before usage patterns are known.

## Decision
Build one deployable backend with explicit module boundaries. Domain/Application/Infrastructure layers remain separated and modules communicate through application contracts/domain events rather than arbitrary table access.

## Consequences
- One transactional database can support early cross-module workflows.
- Deployment and debugging remain simple during MVP.
- Module boundaries must be enforced by code review and architecture tests.
- A module can later be extracted only when scale/team/availability evidence justifies it.
