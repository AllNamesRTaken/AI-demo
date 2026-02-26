---
description: 'Discovers RPC contract interfaces and their implementations, then verifies every declared method is fully implemented on both server and client sides.'
tools: ['read', 'search']
---

# Skill: verify-contracts

Discover the RPC contract interfaces in the workspace, locate their server and client implementations, and report any gaps or mismatches.

## Usage

- `verify-contracts` — check all discovered interfaces
- `verify-contracts: <InterfaceName>` — check only the named interface and its implementations

## Procedure

### Step 1 — Read project documentation

Check for documentation files in this order and read the first one found:

1. `agent-action-files.md` — generated file tree from the planning agent; most precise
2. `FILES.md` — hand-maintained file index
3. `ARCHITECTURE.md` — architectural documentation; look for a project structure diagram or table listing contract interfaces, hub/service implementations, and client services

Extract from whichever file is found:
- Paths to RPC contract interface files
- Path to the server-side hub / service implementation
- Path to the client-side connection service or stub
- Any notes on patterns used (outbox, idempotency keys, mediator dispatch)

If none of these files exist, or they do not mention contract files, fall through to Step 2 to discover by search.

### Step 2 — Discover contracts (fallback)

Only run this step if Step 1 did not yield contract file paths.

Search `src/` for RPC contract interfaces — files whose names or content suggest a boundary between caller and callee. Common patterns to search for:

- Interfaces in a `Contracts`, `Shared`, or `Abstractions` layer
- Names containing `Hub`, `Service`, `Client`, `Callback`, `Api`, or `IPC`
- Interfaces typed as both a server-side contract (methods the server exposes) and a client-side callback contract (methods the server calls on the client)

Record for each discovered interface:
- `{ContractInterface}` — file path
- `{ContractRole}` — `server-to-client callbacks` | `client-to-server calls` | `unknown`

### Step 3 — Locate implementations

For each `{ContractInterface}`, search `src/` for:

| What | How to find |
|------|------------|
| `{ServerImpl}` | Class that `implements` / `: {ContractInterface}` in the server/host layer |
| `{ClientImpl}` | Class in the client layer that either implements the callback interface or contains invocation wrappers referencing the interface by name |

If a side has no implementation (e.g. a contracts-only repo with no client), record `N/A` and skip that check.

### Step 4 — Read and compare

Read `{ContractInterface}`, `{ServerImpl}`, and `{ClientImpl}` in parallel. For each method declared in the interface, verify the applicable checks from the table below. Apply only the checks relevant to what was found in the implementation — do not invent checks for patterns that don't exist in the codebase.

| Check | Applicable when |
|-------|----------------|
| Method present with matching name | Always |
| Signature matches (parameter types, return type) | Always |
| Mutating methods include an idempotency/correlation key parameter | An idempotency key pattern was observed in `{CommandExample}` or existing methods |
| Server impl dispatches through a mediator / command bus | A mediator pattern was observed in `{ServerImpl}` |
| Server impl uses outbox instead of direct push | An outbox pattern was detected in the project |
| Client impl registers a callback handler (e.g. `On<>`, event subscription) | `{ContractRole}` = `server-to-client callbacks` |
| Client impl has an invocation wrapper method | `{ContractRole}` = `client-to-server calls` |

### Step 5 — Generate report

## Output format

```
## Contract Verification Report

### Resolved targets

| Placeholder | Resolved value |
|-------------|----------------|
| {ContractInterface} | <path> |
| {ServerImpl} | <path or N/A> |
| {ClientImpl} | <path or N/A> |

---

### <InterfaceName> — server implementation

| Method | Present | Signature matches | <any extra checks inferred from codebase> |
|--------|:-------:|:-----------------:|:------------------------------------------:|
| <MethodName> | ✅/❌ | ✅/❌ | ✅/❌ |

Missing implementations: <list or "None">
Violations: <list or "None">

---

### <InterfaceName> — client implementation

| Method | Present | Signature matches | <any extra checks inferred from codebase> |
|--------|:-------:|:-----------------:|:------------------------------------------:|
| <MethodName> | ✅/❌ | ✅/❌ | ✅/❌ |

Missing implementations: <list or "None">
Violations: <list or "None">

---

### Summary

Overall: ✅ FULLY IN SYNC | ❌ GAPS FOUND

<If gaps found, list each as an actionable fix:>
- [ ] <Specific method or registration missing and where to add it>
```

## Rules

- Do **not** attempt fixes — only diagnose. Fixes are the caller's responsibility.
- Only include table columns for checks that are actually applicable to this codebase.
- If an implementation file is `N/A`, omit that half of the report entirely.
