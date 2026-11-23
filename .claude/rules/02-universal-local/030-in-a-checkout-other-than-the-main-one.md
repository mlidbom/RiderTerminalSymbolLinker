# In a checkout other than the main one: deploy nothing, and edit with no IDE's tools

The main checkout is `C:\Dev\ClaudeSymbolLinker`. Others are git worktrees of it, such as the Codex helpers' under
`C:\Dev\ClaudeSymbolLinker.worktrees\` (`dev_docs/development-environment.md`, "Codex helpers, in checkouts of their
own"). In one of those:

- **Deploy and reload nothing.** `everyday deploy` puts the build of the checkout it runs in into Magnus's IDEs, so a
  deploy from a worktree would put work that nobody has reviewed there. Here, a change is done when it is
  committed on the checkout's branch, with its specifications passing. The session that handed you the work merges it
  into `main` and deploys it from the main checkout.
- **Rider's and ReSharper's tools see the main checkout, not yours.** Rider has the main checkout's solution open, so
  `dotnet-analysis-and-refactoring` and `jetbrains-rider` read the main checkout's files, and their edits land there,
  in another session's work. Use none of their editing tools: no `rename_symbol`, `move_type_to_namespace`,
  `fix_usings`, `format_file`, `apply_quick_fix`, `apply_suggestions`, `generate_members` or `rename_refactoring`.
  What they read is `main`'s code, not your branch's. Check your code with `dotnet build` and the specifications; for the inspections, use
  `jb inspectcode` on your checkout (the `jetbrains-inspect` skill).
