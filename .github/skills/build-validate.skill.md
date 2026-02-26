---
description: 'Runs dotnet build for the server and/or client projects and returns a structured summary of errors and warnings.'
tools: ['execute', 'read']
---

# Skill: build-validate

Run `dotnet build` for one or both projects and report results in a structured, actionable format.

## Usage

Call this skill with one of:
- `build-validate server` — build AiDemo.Server only
- `build-validate client` — build AvaloniaApp only
- `build-validate all` — build both (default when no argument given)

## Execution

### Server build

```bash
dotnet build src/AiDemo.Server/AiDemo.Server.csproj /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary;ForceNoAlign
```

### Client build

```bash
dotnet build src/AvaloniaApp/AvaloniaApp.csproj /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary;ForceNoAlign
```

## Output format

After each build, produce a structured report:

```
## Build Result: <Server|Client>

Status: ✅ SUCCESS | ❌ FAILED
Errors: <count>
Warnings: <count>

### Errors
- <file>(<line>,<col>): error <code>: <message>

### Warnings
- <file>(<line>,<col>): warning <code>: <message>

### Action Required
<If errors exist, summarise the root cause and the files that need to be fixed.>
<If clean, write "None — build is clean.">
```

## Rules

- Always run builds from the workspace root (where `AI-demo.sln` lives).
- Report errors grouped by project, then by file.
- If both projects are built, print a combined summary line at the end:
  `Overall: Server ✅ / Client ✅` (or ❌ as appropriate).
- Do **not** attempt fixes — only diagnose. Fixes are the caller's responsibility.
