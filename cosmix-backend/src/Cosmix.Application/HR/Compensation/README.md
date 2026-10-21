# Compensation

Owner: Developer 1.

Source: SRS 5, ROLE-004, HR-004.

Restricted effective-dated salary/wage and bank information, separate from general employee DTOs. Sensitive changes need approval and audit. Do not expose these fields by default.

EmployeeCompensationService implements the HR salary-change request and fixed-Admin decision flow,
including effective/as-of reads and immutable history. See docs/Employee-Compensation.md for contracts,
permissions, effective-date behavior and exclusions. Bank changes and payroll calculations remain separate.
