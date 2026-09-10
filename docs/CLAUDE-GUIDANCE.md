# Claude Code use and model verification

The requested name “Claude Opus 5.1” could not be verified in the official model catalogue on 10 September 2026. The official pages distinguish **Claude Opus 5** from **Claude Fable 5.1**. Do not invent `claude-opus-5-1`, assume account access, or claim that a prompt switches the running model.

Open Claude Code in this project and use `/model` to inspect its picker. Select the intended available model. `claude --model opus` is an officially documented startup option; `opus` resolves according to the account/provider and may change. Record the actual model used, rather than hardcoding the alias resolution in project metadata. No fallback to another model is configured by this package. [C1, C2]

The prompt uses a complete product contract, actual source pointers, explicit scope, and a bounded first task. `CLAUDE.md` holds concise persistent constraints; deeper technical documents are loaded when relevant. `planning/PROGRESS.md` preserves state across sessions. These choices follow Claude Code's published guidance. [C3, C4, C5]

Opus 5's official prompting guidance warns about redundant self-check and delegation scaffolding. This package consequently defines real product gates and concrete acceptance cases without asking for repeated independent reviews or blanket “double-check everything” cycles. Passing the relevant gate means proceeding to the next task. Task scope stays the full port; the model may not quietly replace it with a simpler app. [C6]

Use `prompts/IMPLEMENT.md` for the first run and `prompts/CONTINUE.md` for later sessions. These are ordinary project prompt files, not installed Claude skills or a custom system-prompt replacement. No unverified hooks, undocumented model settings or bypass-permissions flags are included. The development operator still controls real permissions and credentials.
