# Idempotency

Owner: Shared; Developer 2 coordinates punches.
Source: API-003; ATT-003.

Persist request identity, request hash and outcome atomically. Scope keys to actor and operation. Approval/punch replays must not duplicate effects.

Planned shared capability, not implemented. Agree the interface before both developers depend on it; implement adapters under Infrastructure and test real failure/retry paths.
