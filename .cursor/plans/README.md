# Cursor plans (SpecialPG)

## Active work

Keep **at most one** plan in this folder (`plans/`) marked as the current milestone. The plan should include:

- Goal and scope (what is out of scope)
- **Done when** checklist (tests + manual playtest steps)
- Implementation order

When the milestone ships, **archive** the plan — do not leave `status: pending` todos in place.

## Archive

Completed plans live in [`archive/`](archive/). They are historical reference only; agents should not treat them as active tasks.

Example: [archive/phase_4_transitions.plan.md](archive/phase_4_transitions.plan.md) (COMPLETE — REV 56).

## Agent memory elsewhere

- Regressions and verify steps: [docs/agent-pitfalls.md](../../docs/agent-pitfalls.md)
- Contracts: [docs/architecture.md](../../docs/architecture.md)
- Per-session prompt template: [docs/agent-session-handoff.md](../../docs/agent-session-handoff.md)
- Cursor rules: [`.cursor/rules/`](../rules/)
