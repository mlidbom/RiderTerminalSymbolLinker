# Rules folder

Project rules, read by both humans and the agent.

- `01-universal-shared` — always-on shared standards (a directory symlink into the `.claude-shared/` catalog)
- `02-universal-local/` — always-on rules of this plugin: who owns what, when a change is done, the changelog
- `path-scoped/` — rules that load only when touching matching files; `01-shared/` holds links into the catalog

**Start here:** [`path-scoped/01-shared/rules-folder.md`](path-scoped/01-shared/rules-folder.md) — how the
folder is loaded, the include order, and the `name.rationale.txt` rationale-sidecar convention.

**Shared vs local is visible in the names:** everything pulled from the `.claude-shared/` catalog (a git
submodule; see `.claude-shared/README.md`) sits behind a `…-shared` entry — `01-universal-shared` is a single
directory symlink to the catalog's universal rules; `path-scoped/01-shared/` holds per-file symlinks to the
adopted path-scoped rules. Project-specific rules live alongside as regular files and folders. (Skills link
flat in `.claude/skills/` with a `shared-` prefix on the link name — grouping folders break skill discovery,
and the link folder's name becomes the skill's name; see the catalog README.) If the links look wrong, run
`.claude-shared/git-scripts/verify-claude-symlinks.ps1`. If `.claude-shared/` is empty, the submodule isn't
initialized (a clone without `--recurse-submodules`, or a new worktree): run `git submodule update --init`. To
sync with upstream, run `.claude-shared/git-scripts/pull.ps1` or `push.ps1`.

(This README is `.txt` on purpose — a `README.md` with no frontmatter would inject into every session. Like
every `.txt` doc here, it's still written in Markdown.)
