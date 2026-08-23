# Legacy Data Inventory Register

This register is the MIG-001 control document. Replace `TBD` entries with facts from each legacy source before any production migration is approved.

## Source inventory fields

For every source object/file/table, record:

- **Source system** — product/database/workbook/archive name.
- **Source object** — table, sheet, API resource, directory or file set.
- **Business owner** — person/team that can validate meaning and completeness.
- **Extract owner** — person/team responsible for producing the final extract.
- **Format** — SQL/CSV/XLSX/JSON/files/API.
- **Source key** — stable primary/natural key used for lineage and idempotence.
- **Estimated rows/files** — expected migration volume.
- **Date range** — oldest/newest relevant business date.
- **Sensitivity** — PUBLIC / INTERNAL / PERSONAL / SENSITIVE-HR / FINANCIAL.
- **Decision** — MIGRATE / REBUILD / ARCHIVE / IGNORE.
- **Target domain/entity** — destination in Personnel Platform.
- **Reconciliation** — count, sum, balance, sample or business-specific proof.
- **Retention note** — source/archive retention after cutover.

## Initial domain inventory

| Domain | Expected legacy content | Target entities | Default decision | Minimum reconciliation | Source facts |
| --- | --- | --- | --- | --- | --- |
| Organization | Companies, branches, departments, positions, projects, cost centers | `Company`, `Branch`, `Department`, `Position`, `Project`, `CostCenter` | MIGRATE | active/inactive counts by company; hierarchy completeness | TBD |
| Personnel | Employee master, employee type, status, hire/termination dates, personnel numbers | `EmployeeType`, `Employee` | MIGRATE | employee counts by company/status/type; duplicate personnel-number check | TBD |
| Sensitive personnel | National identity/passport/tax identifiers, bank/IBAN and other protected HR fields | `EmployeeSensitiveProfile` | MIGRATE | non-null coverage; sample decrypt/read through authorized API | TBD |
| Employee assignments | Project/organization assignments and effective dates | `EmployeeProjectAssignment` plus employee organization fields | MIGRATE | active assignment counts and effective-date coverage | TBD |
| Documents | Personnel documents, document metadata, expiry dates and binaries | `StoredFile`, `DocumentType`, `EmployeeDocument`, `EmployeeDocumentHistory` | MIGRATE or ARCHIVE by type | document count by employee/type; file-size/checksum sampling | TBD |
| Leave | Leave types, entitlements, balances, requests, approvals, attachments | `LeaveType`, `LeaveEntitlement`, `LeaveBalance`, `LeaveRequest`, `LeaveApproval`, `LeaveApprovalHistory`, `LeaveAttachment` | MIGRATE | opening balance by employee/type; request counts/status totals | TBD |
| Work calendars/shifts | Calendars, calendar days, shift definitions and employee assignments | `WorkCalendar`, `WorkCalendarDay`, `ShiftDefinition`, `EmployeeShiftAssignment` | MIGRATE/REBUILD | assigned employee coverage; calendar day count | TBD |
| Attendance | Raw terminal/Pdks events and derived daily attendance | `RawAttendanceEvent`, `DailyAttendance` | MIGRATE history window TBD | event counts by day/device; worked-minute totals; exception counts | TBD |
| Overtime | Overtime requests/approvals/results | `OvertimeRequest` | MIGRATE | approved hours/amounts by period and employee | TBD |
| Camp | Camps, rooms, beds, rates and employee stays | `CampSite`, `CampRoom`, `CampBed`, `AccommodationRate`, `AccommodationStay` | MIGRATE | occupied-day totals; active stay reconciliation | TBD |
| Meals | Meal types, rates and employee consumption | `MealType`, `MealRate`, `MealConsumption` | MIGRATE | quantity/cost totals by day/employee/type | TBD |
| Compensation | Salary/compensation effective-dated master | `EmployeeCompensation`, `CompensationSalarySecret` | MIGRATE | employee coverage and protected salary sample validation | TBD |
| Payroll | Payroll periods and per-employee results | `PayrollPeriod`, `PayrollEmployeeResult` | MIGRATE selected history or ARCHIVE | gross/net/deduction/employer-cost totals by period; employee sample | TBD |
| Stock/assets | Stock locations/items/movements, asset master and assignments | `StockLocation`, `StockItem`, `StockMovement`, `AssetItem`, `AssetAssignment` | MIGRATE | on-hand balances; active assignment counts | TBD |
| Vehicles | Vehicle master, assignments, odometer, maintenance and fuel | `Vehicle`, `VehicleAssignment`, `VehicleOdometerEvent`, `VehicleMaintenanceRecord`, `VehicleFuelRecord` | MIGRATE | active vehicles/assignments; odometer continuity; fuel totals | TBD |
| Administrative affairs | Tasks, task completions, contracts and reminder state | `AdministrativeTask`, `AdministrativeTaskCompletion`, `AdministrativeContract`, `AdministrativeReminderEvent` | MIGRATE active/open records; history TBD | open-task/contract counts and expiry samples | TBD |
| Workflow | Open requests/approval state and required history | `WorkflowRequestType`, `WorkflowApprovalStepDefinition`, `WorkflowRequest`, `WorkflowRequestApproval`, `WorkflowRequestHistory`, `WorkflowSlaEvent` | REBUILD config; MIGRATE open business requests only if required | open request count/status/owner | TBD |
| Notifications | Templates/rules/current notifications | `NotificationTemplate`, `NotificationRule`, `UserNotification`, `NotificationHistory` | REBUILD rules; history generally ARCHIVE | active rule/template count | TBD |
| Finance | Cost entries and payroll cost allocation overrides | `CostEntry`, `PayrollCostAllocationOverride` | MIGRATE where needed for reporting/ERP continuity | amount totals by period/category/project/cost center | TBD |
| Reporting exports | Historical generated exports | `ReportExportJob` plus files | ARCHIVE unless business requirement says otherwise | archive count/checksum | TBD |
| Integration config | External systems, devices and entity mappings | `IntegrationSystem`, `IntegrationDevice`, `ExternalEntityMapping` | REBUILD credentials; MIGRATE mappings when verified | mapping counts and sample resolutions | TBD |
| Integration queue | Unprocessed/failed staging records | `IntegrationStagingRecord`, `IntegrationStagingHistory` | normally DO NOT MIGRATE; drain/replay explicitly | zero or approved residual backlog at freeze | TBD |
| ERP mappings/history | Account mappings, export batches/lines/reconciliation | `ErpAccountMapping`, `ErpExportBatch`, `ErpExportLine`, `ErpReconciliationEvent` | MIGRATE config; history ARCHIVE/MIGRATE by reporting need | account mapping coverage; batch totals | TBD |
| Identity/access | Legacy users, roles and permissions | `User`, `Role`, `Permission`, `UserRole`, `RolePermission`, `UserScope`, `EmployeeUserLink` | REBUILD/PROVISION | named-user access review and scope sign-off | TBD |
| Authentication sessions/MFA | Password hashes, refresh tokens, MFA secrets/challenges | `RefreshToken`, `UserMfaCredential`, `MfaChallenge` | IGNORE | all users authenticate through target enrollment path | TBD |
| Audit | Legacy audit/access/change logs | `AuditLog` only if a specific structured requirement exists | ARCHIVE by default | archive availability/readability and retention sign-off | TBD |

## Required source questions before MIG-001 can close

1. What are the legacy applications, databases and Excel workbooks that contain authoritative data?
2. Which source is authoritative when the same employee or transaction exists in more than one system?
3. Which historical date range is legally/business-required for each domain?
4. Are employee/personnel numbers globally unique or only company-unique?
5. Which codes are stable natural keys for organization, project, camp, meal, document and cost-center masters?
6. Which source values are encrypted/masked and which require privileged extraction?
7. Where are document binaries stored and can stable file checksums be produced?
8. Are there deleted/inactive records that must still be retained for history?
9. Which payroll periods must remain queryable in the new platform versus read-only archive?
10. What is the source-system freeze mechanism for final cutover?

## MIG-001 exit criteria

MIG-001 is complete only when every in-scope source object has a named owner, extraction method, source key, approximate volume/date range, sensitivity classification, migration decision, target entity/domain, reconciliation method and retention decision. Unknowns must be recorded as blockers rather than silently defaulted.
