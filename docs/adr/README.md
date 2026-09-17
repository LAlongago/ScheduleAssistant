# Architecture decision records

Create one immutable ADR per material architecture or platform decision. Use [`000-template.md`](000-template.md) for new records and never rewrite an accepted ADR; supersede it with a new numbered record.

The specification reserves these topics for later decisions:

- ADR-001: target .NET SDK and dependency versions;
- ADR-002: SQLite data access and migrations;
- ADR-003: Windows notifications and publish model;
- ADR-004: desktop host and fallback;
- ADR-005: single-instance and notification-activation IPC; and
- ADR-006: managed attachments and backup boundary.

DEV-001 established the template. The following proposed records were added by INTEGRATION-001 for the isolated platform experiments; they do not approve production adapters:

- [`003-windows-notifications-and-publish-model.md`](003-windows-notifications-and-publish-model.md) — SPIKE-001 candidate notification/publish direction.
- [`004-desktop-host-and-fallback.md`](004-desktop-host-and-fallback.md) — SPIKE-002 WidgetFallback-first desktop-host direction.

Both records retain their unverified system-behavior conditions and must be superseded or confirmed by the corresponding DEV-081/DEV-084 acceptance work.
