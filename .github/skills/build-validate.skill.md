---
description: 'Discovers buildable projects in the workspace and runs the appropriate build tool, returning a structured summary of errors and warnings.'
tools: ['execute', 'read', 'search']
---

# Skill: build-validate

Discover the projects in the workspace, run the appropriate build tool, and report results in a structured, actionable format.

## Usage

Call this skill with one of:
- `build-validate all` — build every discovered project (default when no argument given)
- `build-validate <name>` — build only the project whose name contains `<name>` (case-insensitive match against discovered project names)

## Discovery

Before building, resolve the workspace:

1. Search for a solution file (`*.sln`) at the workspace root. If found, enumerate its projects to get the full list of buildable targets and their paths.
2. If no solution file exists, search `src/` recursively for project manifest files (`.csproj`, `package.json`, `Cargo.toml`, etc.) and treat each as a buildable target.
3. Infer the build tool and command flags from the manifest type:

| Manifest | Build command |
|----------|--------------|
| `*.csproj` | `dotnet build <path> /property:GenerateFullPaths=true "/consoleloggerparameters:NoSummary;ForceNoAlign"` |
| `package.json` | `npm run build` (from the manifest's directory) |
| `Cargo.toml` | `cargo build` (from the manifest's directory) |
| other | Use the tool appropriate for the detected stack |

4. Always run builds from the workspace root unless the build tool requires the manifest's directory.

## Output format

After each build, produce a structured report:

```
## Build Result: <ProjectName>

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

If multiple projects are built, append a combined summary line:
`Overall: <ProjectA> ✅ / <ProjectB> ❌ / ...`

## Rules

- Report errors grouped by project, then by file.
- Do **not** attempt fixes — only diagnose. Fixes are the caller's responsibility.
