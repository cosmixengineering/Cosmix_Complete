# Rosters

Owner: Developer 2.

Source: SRS 6.2; SHIFT-001 and SHIFT-002.

Effective employee/team/site shift assignments and approved temporary changes with evidence. Coordinate employee assignment IDs with Developer 1.

IndividualRosterService now implements future-dated individual assignment, replacement/ending, history, scoped effective lookup and My Shift. IIndividualRosterResolver returns exact assignment and immutable schedule identities independently of policy calculation. See docs/Shifts-Rosters.md. Group precedence, approved temporary changes and holiday applicability remain pending and must not be inferred from existing models. Keep EF Core and HTTP types out of this project.
