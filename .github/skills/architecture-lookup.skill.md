---
description: 'Answers "does this approach follow the project architecture?" by consulting ARCHITECTURE.md and AGENTS.md, then gives a clear verdict with references.'
tools: ['read', 'search']
---

# Skill: architecture-lookup

Validate whether a proposed approach is consistent with the project's architecture rules and coding conventions.

## Usage

Call this skill with a description of what you intend to do. Examples:
- `architecture-lookup: I want to add a REST endpoint to return a list of items`
- `architecture-lookup: Should I put the new DTO in the Application project?`
- `architecture-lookup: Is it OK to call Clients.Others.OnItemCreated directly from the handler?`

## Procedure

1. **Read** the following files (read them fresh each invocation — do not rely on cached content):
   - `AGENTS.md` — rules, naming conventions, common mistakes, quick-reference checklist
   - `ARCHITECTURE.md` — ADRs, system design, layer responsibilities, patterns

2. **Identify** which rules, ADRs, or conventions are relevant to the proposed approach.

3. **Produce a verdict** using the template below.

## Output format

```
## Architecture Lookup

### Proposed approach
<Restate the approach in one sentence to confirm understanding.>

### Verdict: ✅ ALIGNED | ⚠️ PARTIALLY ALIGNED | ❌ VIOLATION

### Relevant rules
| Source | Section / ADR | Rule summary |
|--------|--------------|--------------|
| AGENTS.md | Common Mistakes #N | ... |
| ARCHITECTURE.md | ADR-00X | ... |

### Explanation
<2-5 sentences: why the approach is or isn't aligned, with direct quotes from docs where helpful.>

### Recommended approach
<If ❌ or ⚠️: describe the correct alternative.>
<If ✅: "Proceed as planned.">
```

## Key rules to always check

| Topic | Correct pattern | Common mistake |
|-------|----------------|----------------|
| Business logic RPC | SignalR hub method | REST controller |
| Inter-layer DTO location | `AiDemo.Contracts` | Domain or Application |
| CQRS library | `martinothamar/Mediator` | MediatR |
| Client notifications | Outbox → `OutboxProcessorService` | Direct `Clients.Others.*` call |
| Mutating hub methods | Include `Guid? idempotencyKey` | Omit idempotency key |
| Hub auth | `[Authorize]` on hub class | No attribute |
| OIDC config | `Oidc:Authority` + `Oidc:ClientId` keys | Hardcoded URLs |
