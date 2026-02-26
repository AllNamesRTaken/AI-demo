---
description: 'Canonical tracking file lifecycle used by all agents: initialization, manage_todo_list synchronization, per-task update loop, and prefix support.'
tools: ['edit', 'todo']
---

# Skill: tracking-sync

Defines the standard tracking file pattern shared by all agents in this workspace. Every agent that uses tracking files **must** follow this skill exactly.

## File Naming

Each agent owns a **namespace** (e.g. `planning`, `action`, `agent`). The three files are:

```
{prefix}-agent-{namespace}-internal.md   ← context, decisions, working memory
{prefix}-agent-{namespace}-todo.md       ← authoritative task list (checkbox format)
{prefix}-agent-{namespace}-done.md       ← completed tasks (moved one at a time)
```

When no prefix is given, omit the `{prefix}-` segment entirely:

```
agent-{namespace}-internal.md
agent-{namespace}-todo.md
agent-{namespace}-done.md
```

## Initialization (run once at startup)

1. **Scan** for the three tracking files.
2. **Branch**:
   - Todo file exists and all tasks complete → clear `todo` and `internal` to prevent context bleed, then start fresh.
   - Todo file exists with incomplete tasks → load plan from `todo.md` and context from `internal.md`, resume.
   - Files missing → create them (content depends on agent role — see each agent's instructions).
3. **Invoke `manage_todo_list`** with ALL tasks (statuses as loaded or newly planned). This step is **mandatory** before any work begins — it makes the TODO UI visible to the user.

## Per-Task Update Loop

After **every** completed task, in this exact order:

1. Mark the checkbox `[x]` in `agent-{namespace}-todo.md`.
2. Invoke `manage_todo_list` with updated statuses.
3. Move the completed task entry to `agent-{namespace}-done.md` (with timestamp).
4. Remove it from `agent-{namespace}-todo.md`.
5. If execution revealed new subtasks, append them to `todo.md` and invoke `manage_todo_list` again before continuing.

## Prefix Support

When the user specifies a prefix (e.g. `"myproject"`):
- Apply it consistently to **all three** tracking files for the agent's namespace.
- Default (no prefix stated) → use bare `agent-{namespace}-*.md` names.

## Rules

- **Never** begin work before step 3 of Initialization (tool invoked, UI visible).
- **Never** batch completions — move tasks to done.md **one at a time**, immediately after finishing each.
- **Always** expand the todo list dynamically when new work is discovered; invoke `manage_todo_list` after each expansion.
- Keep `done.md` as an append-only log; never delete entries from it.
