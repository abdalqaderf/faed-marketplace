# 23 — GitHub Repository Policy

## Principle

GitHub should contain everything required to:

- understand the project;
- build it from source;
- reproduce the architecture;
- give Claude Code, Codex, Copilot, and human contributors the same instructions;
- review the project history.

Do not use GitHub as storage for machine-specific build output, credentials, temporary agent state, or packaging archives.

---

# Commit to GitHub

## Application source

Commit:

```text
Faed.sln / Faed.slnx
src/
tests/
```

Including:

- `.csproj` files;
- C# source;
- controllers;
- Razor views;
- static application assets;
- EF Core migrations;
- non-secret configuration;
- test source.

## Authoritative project documentation

Commit:

```text
AGENTS.md
CLAUDE.md
README.md
START_PROMPT.md
PROJECT_STATUS.md
docs/
tasks/
.editorconfig
Directory.Build.props
```

These files are part of the project, not disposable notes.

## Agent skills and project instructions

**Commit these to GitHub:**

```text
.claude/skills/
.github/copilot-instructions.md
AGENTS.md
CLAUDE.md
```

The project-specific skills are version-controlled implementation guidance.

They should be available to:
- the developer;
- Claude Code;
- Codex;
- future machines;
- future contributors.

Do **not** ignore `.claude/skills/`.

---

# Do not commit

## Build and IDE-local output

Do not commit:

```text
.vs/
bin/
obj/
artifacts/
*.user
*.suo
```

## Secrets/local settings

Do not commit:

```text
.env
.env.*
secrets.json
appsettings.Local.json
.claude/settings.local.json
```

User Secrets stored by .NET outside the repository are also not committed.

Never commit:
- DB passwords;
- API keys;
- SMTP credentials;
- cloud storage secrets;
- access tokens.

## Local runtime/test artifacts

Do not commit:

```text
logs/
TestResults/
coverage/
uploads/
private-storage/
*.db
*.sqlite
*.sqlite3
```

## Packaging/reference artifacts

The final repository does not need local packaging/merge artifacts such as:

```text
reference/
MANIFEST.json
MERGE_NOTES.md
QUALITY-CHECK.md
*.zip
```

They may remain locally for archive/history, but they are not part of the authoritative implementation repository.

---

# Why skills should be committed

Files under:

```text
.claude/skills/
```

are analogous to project tooling/configuration.

They encode Faed-specific rules such as:
- visual direction;
- commerce UX;
- marketplace page behavior;
- dashboard UX;
- responsive/accessibility expectations;
- UI quality gates.

If they exist only on one machine:
- another agent run may behave differently;
- future clones lose the project guidance;
- design consistency becomes dependent on local setup.

Therefore they belong in Git.

---

# Recommended first commits

## Commit 1 — Project specification

Before creating the MVC project:

```text
docs: initialize Faed project specification
```

This commit should include the authoritative docs/tasks/skills.

## Commit 2 — Visual Studio baseline

After Visual Studio creates the MVC + Identity project and it successfully builds/runs:

```text
chore: create MVC Identity baseline
```

## Commit 3 — TASK-001

After the agent completes TASK-001 and review passes:

```text
feat: complete solution foundation
```

Then continue one task/branch at a time.
