# The spike of a .NET Cloyd (throwaway)

Run on 2026-11-20 on Magnus's go, as `dev_docs/cloyd-as-a-dotnet-application.md` on main proposes under "The spike
proposed"; what it found is recorded there, under "What the spike found". This branch, `spike/dotnet-cloyd`, keeps the
code for reference. It is not to be merged.

## What is here

- **`CloydSpike`:** the window. The sessions of one project (`--project <folder>`) or of every project; the chosen
  session's conversation from the hub, as markdown, with the input box under it; and its TUI beside it, in the candidate
  host chosen. It attaches only to session hosts it started itself, labelled `{"cloydSpike":"throwaway"}`: attaching
  takes a session over from its IDE tab. "Start a throwaway session" starts a session host with pwsh, and
  `claude --name cloyd-spike` in it, on Haiku, in `scratch-session/`. `--show-tui <candidate>` opens the window without
  taking the keyboard and shows the TUI at once.
- **`CloydSpike.Probe`:** on Avalonia's headless platform, with no window on the desktop.
  - `start`, `send <text>`, `end`: the throwaway session.
  - `look [enter]`: its screen, drawn, with Enter pressed first if asked.
  - `tui [<candidate>]`: each candidate that draws headless, attached to the throwaway session's host and driven with
    keys, Escape, the slash-command picker, Shift+Enter, Ctrl+Shift+V and Ctrl+V, resizes and the mouse wheel. At each
    step it saves what the control drew (`probe-output/<candidate>/NN-step.png`) beside what the session's own console
    shows (`NN-step.screen.txt`, from `ClaudeTooling.exe typing screen`). It also logs the bytes the control sent
    (`typed.log`, each line labelled with the step before the action).
  - `conversation [<session id>]`: a conversation from the hub, drawn.
- **`CloydSpike.Specifications`:** five specifications on the headless platform, 0.7 s: the conversation view given the
  hub's own sample stream (`~/.claude/src/ClaudeTooling.Specifications/Hub/LocalApi/Samples/conversation-stream.http`),
  and the input box typed into.

```
dotnet build CloydSpike.slnx
dotnet run --project CloydSpike.Probe --no-build -- start
dotnet run --project CloydSpike.Probe --no-build -- tui
dotnet test CloydSpike.Specifications
CloydSpike\bin\Debug\net11.0-windows10.0.19041.0\win-x64\CloydSpike.exe --project C:\Dev\ClaudeSymbolLinker
```

A new scratch folder may first show Claude Code's "Allow external CLAUDE.md file imports?" dialog, for the
repository's shared rules: `look enter` takes its default, No. The hub lists no running session until Claude Code is
past it.
