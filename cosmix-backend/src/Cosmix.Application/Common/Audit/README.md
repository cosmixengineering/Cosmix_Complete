# Audit

Owner: Developer 1.
Source: AUTH-008; SRS 12.2.

Immutable sensitive-read/write and decision history. Keep security audit separate from diagnostic logs; redact CNIC, salary, bank and medical data.

Planned shared capability, not implemented. Agree the interface before both developers depend on it; implement adapters under Infrastructure and test real failure/retry paths.
