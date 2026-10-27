# Approvals endpoints

Five configuration/delegation endpoints under `/api/v1/approvals` require current web MFA, named permissions and audited transaction/retry evidence. Publication requires explicit All-scope route-manage and selected stage-review grants, including for Admin or HR actors. They configure shared claim/benefit authority; the existing fixed workflows retain their own controllers. See `docs/Approval-Routing.md` for the later consumer contract and acceptance limits.
