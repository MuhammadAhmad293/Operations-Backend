# Task Planning & Workflow (Mandatory)

## Core Rule
NO WORK WITHOUT A TASK FILE.

---

## Folder Structure

```
.tasks/
├── planned/
├── active/
├── completed/
└── _template.md
```

---

## Task File Naming
`NNN-kebab-task-name.md`
NNN is sequential across all folders.

---

## Task Lifecycle

| Stage    | Action                      |
|----------|-----------------------------|
| Plan     | Create in `.tasks/planned/` |
| Start    | Move to `.tasks/active/`    |
| Work     | One sub-task at a time      |
| Complete | Move to `.tasks/completed/` |

---

## Sub-Task Rules (Human-in-the-Loop)

For EACH sub-task:

1. Announce
2. Execute
3. Report
4. Update task file
5. STOP
6. Await approval

❌ Never batch sub-tasks
✅ One approval = one sub-task

---

## Session Start Protocol
1. Read all `.tasks/planned/`
2. Read all `.tasks/active/`
3. Identify pending sub-tasks
4. Resume from first incomplete sub-task

---

## Discovery Rule
If new sub-tasks are discovered:
- Add them to the task file
- Announce before working
- Await approval
