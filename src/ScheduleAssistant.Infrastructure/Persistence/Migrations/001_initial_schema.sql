CREATE TABLE IF NOT EXISTS schema_migrations (
    version         INTEGER PRIMARY KEY,
    name            TEXT NOT NULL UNIQUE,
    checksum        TEXT NOT NULL,
    applied_at_utc  TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS categories (
    id             TEXT PRIMARY KEY,
    name           TEXT NOT NULL,
    color_hex      TEXT NOT NULL,
    sort_order     INTEGER NOT NULL,
    is_built_in    INTEGER NOT NULL CHECK (is_built_in IN (0, 1)),
    is_archived    INTEGER NOT NULL CHECK (is_archived IN (0, 1)),
    created_at_utc TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL,
    version        INTEGER NOT NULL CHECK (version >= 1),
    CHECK (length(trim(name)) BETWEEN 1 AND 100)
);

CREATE TABLE IF NOT EXISTS recurrence_series (
    id                TEXT PRIMARY KEY,
    title             TEXT NOT NULL,
    category_id       TEXT NOT NULL,
    priority          INTEGER NOT NULL CHECK (priority IN (0, 1, 2, 3)),
    frequency         INTEGER NOT NULL CHECK (frequency IN (0, 1, 2, 3)),
    effective_date    TEXT NOT NULL,
    end_date          TEXT NULL,
    time_zone_id      TEXT NOT NULL,
    recurrence_interval INTEGER NOT NULL CHECK (recurrence_interval = 1),
    weekdays          INTEGER NOT NULL CHECK (weekdays BETWEEN 0 AND 127),
    month_day         INTEGER NULL CHECK (month_day IS NULL OR month_day BETWEEN 1 AND 31),
    year_month        INTEGER NULL CHECK (year_month IS NULL OR year_month BETWEEN 1 AND 12),
    year_day          INTEGER NULL CHECK (year_day IS NULL OR year_day BETWEEN 1 AND 31),
    planned_start_time TEXT NULL,
    planned_end_time   TEXT NULL,
    location           TEXT NULL,
    description        TEXT NULL,
    materials          TEXT NULL,
    notes              TEXT NULL,
    is_enabled         INTEGER NOT NULL CHECK (is_enabled IN (0, 1)),
    created_at_utc     TEXT NOT NULL,
    updated_at_utc     TEXT NOT NULL,
    version            INTEGER NOT NULL CHECK (version >= 1),
    FOREIGN KEY (category_id) REFERENCES categories(id) ON DELETE RESTRICT,
    CHECK (length(trim(title)) BETWEEN 1 AND 200),
    CHECK (location IS NULL OR length(location) <= 300),
    CHECK (description IS NULL OR length(description) <= 10000),
    CHECK (materials IS NULL OR length(materials) <= 10000),
    CHECK (notes IS NULL OR length(notes) <= 10000),
    CHECK (end_date IS NULL OR end_date >= effective_date),
    CHECK (
        (frequency = 0 AND weekdays = 0 AND month_day IS NULL AND year_month IS NULL AND year_day IS NULL)
        OR (frequency = 1 AND weekdays BETWEEN 1 AND 127 AND month_day IS NULL AND year_month IS NULL AND year_day IS NULL)
        OR (frequency = 2 AND month_day IS NOT NULL AND weekdays = 0 AND year_month IS NULL AND year_day IS NULL)
        OR (frequency = 3 AND year_month IS NOT NULL AND year_day IS NOT NULL AND weekdays = 0 AND month_day IS NULL)
    ),
    CHECK (planned_end_time IS NULL OR planned_start_time IS NULL OR planned_end_time >= planned_start_time)
);

CREATE TABLE IF NOT EXISTS tasks (
    id                       TEXT PRIMARY KEY,
    title                    TEXT NOT NULL,
    category_id              TEXT NOT NULL,
    priority                 INTEGER NOT NULL CHECK (priority IN (0, 1, 2, 3)),
    workflow_status          INTEGER NOT NULL CHECK (workflow_status IN (0, 1, 2)),
    planned_date             TEXT NULL,
    planned_start_time       TEXT NULL,
    planned_end_time         TEXT NULL,
    deadline_local           TEXT NULL,
    deadline_time_zone_id    TEXT NULL,
    deadline_utc             TEXT NULL,
    location                 TEXT NULL,
    description              TEXT NULL,
    materials                TEXT NULL,
    notes                    TEXT NULL,
    series_id                TEXT NULL,
    occurrence_date          TEXT NULL,
    is_occurrence_override   INTEGER NOT NULL DEFAULT 0 CHECK (is_occurrence_override IN (0, 1)),
    created_at_utc           TEXT NOT NULL,
    updated_at_utc           TEXT NOT NULL,
    completed_at_utc         TEXT NULL,
    version                  INTEGER NOT NULL DEFAULT 1 CHECK (version >= 1),
    FOREIGN KEY (category_id) REFERENCES categories(id) ON DELETE RESTRICT,
    FOREIGN KEY (series_id) REFERENCES recurrence_series(id) ON DELETE RESTRICT,
    CHECK (length(trim(title)) BETWEEN 1 AND 200),
    CHECK (location IS NULL OR length(location) <= 300),
    CHECK (description IS NULL OR length(description) <= 10000),
    CHECK (materials IS NULL OR length(materials) <= 10000),
    CHECK (notes IS NULL OR length(notes) <= 10000),
    CHECK (planned_start_time IS NULL OR planned_date IS NOT NULL),
    CHECK (planned_end_time IS NULL OR planned_date IS NOT NULL),
    CHECK (planned_end_time IS NULL OR planned_start_time IS NULL OR planned_end_time >= planned_start_time),
    CHECK (
        (deadline_local IS NULL AND deadline_time_zone_id IS NULL AND deadline_utc IS NULL)
        OR (deadline_local IS NOT NULL AND deadline_time_zone_id IS NOT NULL AND deadline_utc IS NOT NULL)
    ),
    CHECK ((series_id IS NULL AND occurrence_date IS NULL) OR (series_id IS NOT NULL AND occurrence_date IS NOT NULL)),
    CHECK ((is_occurrence_override = 0) OR (series_id IS NOT NULL AND occurrence_date IS NOT NULL)),
    CHECK (
        (workflow_status = 2 AND completed_at_utc IS NOT NULL)
        OR (workflow_status <> 2 AND completed_at_utc IS NULL)
    )
);

CREATE TABLE IF NOT EXISTS reminders (
    id                     TEXT PRIMARY KEY,
    task_id                TEXT NOT NULL,
    relative_offset_minutes INTEGER NOT NULL,
    scheduled_at_utc       TEXT NOT NULL,
    delivered_at_utc       TEXT NULL,
    status                 INTEGER NOT NULL CHECK (status IN (0, 1, 2, 3, 4)),
    deduplication_key      TEXT NOT NULL,
    error_code             TEXT NULL,
    FOREIGN KEY (task_id) REFERENCES tasks(id) ON DELETE CASCADE,
    UNIQUE (deduplication_key),
    CHECK (
        (status IN (0, 2, 3) AND delivered_at_utc IS NULL AND error_code IS NULL)
        OR (status = 1 AND delivered_at_utc IS NOT NULL AND error_code IS NULL)
        OR (status = 4 AND delivered_at_utc IS NULL AND error_code IS NOT NULL)
    )
);

CREATE TABLE IF NOT EXISTS attachments (
    id                    TEXT PRIMARY KEY,
    task_id               TEXT NOT NULL,
    display_name          TEXT NOT NULL,
    managed_relative_path TEXT NOT NULL,
    extension             TEXT NULL,
    mime_type             TEXT NULL,
    size_bytes            INTEGER NOT NULL CHECK (size_bytes >= 0),
    sha256                TEXT NULL,
    imported_at_utc       TEXT NOT NULL,
    FOREIGN KEY (task_id) REFERENCES tasks(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS recurrence_exclusions (
    series_id       TEXT NOT NULL,
    occurrence_date TEXT NOT NULL,
    PRIMARY KEY (series_id, occurrence_date),
    FOREIGN KEY (series_id) REFERENCES recurrence_series(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS settings (
    key            TEXT PRIMARY KEY,
    value          TEXT NULL,
    updated_at_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS cleanup_queue (
    id                TEXT PRIMARY KEY,
    item_type         TEXT NOT NULL,
    item_id           TEXT NOT NULL,
    managed_path      TEXT NOT NULL,
    queued_at_utc     TEXT NOT NULL,
    attempt_count     INTEGER NOT NULL DEFAULT 0 CHECK (attempt_count >= 0),
    last_error        TEXT NULL
);

CREATE INDEX IF NOT EXISTS ix_tasks_planned_date
    ON tasks (planned_date);

CREATE INDEX IF NOT EXISTS ix_tasks_deadline_utc_workflow
    ON tasks (deadline_utc, workflow_status)
    WHERE deadline_utc IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_tasks_deadline_local
    ON tasks (deadline_local)
    WHERE deadline_local IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_tasks_category_id
    ON tasks (category_id);

CREATE UNIQUE INDEX IF NOT EXISTS ux_tasks_series_occurrence
    ON tasks (series_id, occurrence_date)
    WHERE series_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_reminders_status_scheduled
    ON reminders (status, scheduled_at_utc);

CREATE INDEX IF NOT EXISTS ix_reminders_task_id
    ON reminders (task_id);

CREATE INDEX IF NOT EXISTS ix_attachments_task_id
    ON attachments (task_id);

CREATE INDEX IF NOT EXISTS ix_recurrence_exclusions_series_date
    ON recurrence_exclusions (series_id, occurrence_date);
