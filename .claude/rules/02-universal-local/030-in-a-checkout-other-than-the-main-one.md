# In a checkout other than the main one: deploy nothing, and leave desktop Rider's tools alone

The main checkout is `C:\Dev\ClaudeSymbolLinker`. Others are git worktrees of it, such as the Codex helpers' under
`C:\Dev\ClaudeSymbolLinker.worktrees\` (`dev_docs/development-environment.md`, "Codex helpers, in checkouts of their
own"). In one of those:

- **Deploy and reload nothing.** `everyday deploy` puts the build of the checkout it runs in into Magnus's IDEs, so a
  deploy from a worktree would put work that nobody has reviewed there. Here, a change is done when it is
  committed on the checkout's branch, with its specifications passing. The session that handed you the work merges it
  into `main` and deploys it from the main checkout.
- **Desktop Rider's tools see the main checkout, not yours.** Magnus's Rider has the main checkout's solution open, so
  `jetbrains-rider` reads the main checkout's files, and its edits, such as `rename_refactoring`, land there, in another
  session's work: use none of its editing tools. Royder (`dotnet-analysis-and-refactoring`) opens your checkout's
  solution when given your checkout's absolute paths, so its tools, renames and inspections included, work on your
  branch.
