# Cloyd Projects Dashboard

## Projects Dashboard as built

Magnus named the window **Projects Dashboard** on 2026-11-22. The button at the far right of a project window's
tab strip opens the dashboard or brings its existing window forward. A project window can close while the
dashboard stays open. Launching `Cloyd.exe` with no arguments opens or activates the dashboard directly.
`Cloyd.exe --project <folder>` opens the named project window. The dashboard saves its selected
session and virtual desktop in `projects-dashboard.json`.

- **The vertical tab strip** groups sessions under their project and then their worktree. It discovers
  projects from Claude Code's global config, open project windows and session hosts. Recorded projects
  underneath a folder with accepted trust are included. The running session's reported working directory
  takes precedence over the host's original directory. A replacement host resumes a session in its last
  reported directory when that directory still exists. Git's registered worktree list
  supplies every checkout, including empty checkouts anywhere on disk. Previously seen repositories come back
  even when their last session and project window have closed. For a folder outside Git, tabs
  sit directly under the project. Sessions outside a host with a known folder appear there read-only.
  Sessions outside a host whose folder is unknown share a read-only group.
- **The session catalogue** combines interactive, resumable conversations on disk with current agents by
  session ID. History and the dashboard both use it. A host with no known session still has its shell tab.
  Selecting a stopped conversation opens a read-only transcript. **Resume session** starts it explicitly.
  The root, project and worktree clocks open the same history picker, scoped to the clicked heading's full
  membership independently of sidebar filters. Session rows open their conversations directly.
- **Session filters** use a single list at every level. A session appears if any selected switch
  matches its pin, project-tab membership or activity. Open tabs includes the tabs a closed project
  window would restore. The dashboard can read those choices without starting an agent.
  **A** temporarily shows all sessions. Turning it off restores the individual choices. Clicking an
  individual switch releases **A**. An empty selection defaults to **A**.
  At project and worktree headings every switch cycles through On, Off and Unset. Unset follows its
  parent. A ✓ marks an explicit On and × marks an explicit Off. Both stay visible when the strip
  collapses. Hovering or entering by keyboard reveals the full strip. **Reset overrides**
  clears every local choice in one operation. **AG** reveals all group headings. Counts and
  Ctrl+Tab follow the filters. Pins follow the session across host changes.
- **Tree search** opens and takes focus from the magnifier or Ctrl+Shift+F. Words match project, worktree,
  branch and session names within the active filters. Search temporarily expands matching headings.
  An empty box closes on focus loss or a click elsewhere. Escape in the box clears it and restores
  the saved expansion. Ctrl+F remains available in the transcript.
- **Project headings** offer **Open project** to open the project window or bring the existing one forward.
  A Git project's **+** starts a named Claude or Codex session in a new linked worktree.
  Its folder sits under the repository's sibling `.worktrees` folder. Its branch uses dashes for
  spaces in the name. Worktree headings and projects outside Git start a session in their existing folder.
- **Draft preview** uses the transcript's Markdown renderer. Hold Ctrl+M in either agent's input
  box to read the draft in place. Releasing the shortcut or leaving the editor ends the preview.
- **Worktree context menus** have a Git Extensions submenu. Browse opens its revision graph and Commit
  opens the interactive commit dialog in that worktree's directory.
  **Remove worktree…** always opens the review window. Each checkout reports its dirty paths,
  merge status and remote tip verification. The dialog keeps local branches unless explicitly
  selected for deletion. Branch deletion compares the reviewed commit with the current ref.
  **Refresh** re-reads Git and the unfiltered hub inventory, including sessions outside Cloyd.
  Idle-agent and idle-shell actions use reviewed process identities on hubs advertising
  `supportsWorktreeOccupantActions`. Working, waiting, unknown and drafted tabs stay in the review.
  A final removal uses only the stopped-session archive API with an operation ID per checkout.
  The agent-worktree creation path invokes the machine catalog's attach step for consuming
  projects and verifies the catalog registration before starting an agent. The dialog inspects
  both repositories, archives or recoverably deletes their saved conversations, and verifies
  vacancy before removing the shared rules and then the parent. Other nested repositories block it.
  Cloyd records the confirmed disposition under
  `worktree-dispositions` before contacting the hub. If a reply is lost, **Recover previous confirmation**
  replays the same operation ID and identities to recover its receipt.
- **Branch icons** compare checkout commits against local main (otherwise the available integration branch).
  Green means integrated; purple means commits are still outside it. On the integration branch, green and
  purple compare against the last locally known remote upstream, and ↑/↓ mark outgoing/incoming commits.
  An amber dot independently marks staged, unstaged or non-ignored untracked files. Gray means the comparison
  is unavailable. Tooltips name the target and counts. The dashboard performs local Git reads without fetching.
- Project tabs and dashboard tabs share their heading, including the idle agent mark, busy animation,
  title and close button. A group heading toggles expansion independently of the active session. A
  collapsed group shows its active tab and counts of sessions, including those busy or waiting. Reordering
  siblings moves entire groups and preserves the selected chat or terminal. Order and expansion are
  saved with the dashboard. Only the presentation changes when Magnus rearranges a group.
- **The selected session** uses the same `SessionView` as the project tab. Each view has its own editor,
  selection, search and scroll position. `HostedSession` owns the shared `SessionChatState`. Claude Code's
  draft, ordered sends and delivery notices are shared, as are Codex drafts and steer receipts. Both editors of
  a draft use the same AvaloniaEdit document and pasted segments. Closing the project window leaves the
  dashboard's draft available. Cloyd reads older drafts from saved project tabs and keeps per-host snapshots
  under `hosted-sessions`.
- **The terminal** has one attach client, emulator, screen and scrollback per host. Merely listing a host
  opens no connection. The first layout of its terminal starts the reader. Asking for Terminal in the other
  window reparents the existing control there. The previous view shows Chat, or a button to bring Terminal
  back when the hub offers no chat. An explicit terminal request can take over an external attach client.
- **Session actions** include starting a Claude Code or Codex session from a worktree's **+** (or a non-Git project's **+**), ending a session
  with **×**, switching Chat/Terminal with Ctrl+H, and navigating sessions with Ctrl+Tab or Ctrl+Shift+Tab.
  Ctrl+Alt+Shift+Y opens the history picker with **All projects** selected. A notification still prefers an open
  project tab and otherwise selects the hosted session in Projects Dashboard.
- **Hub subscriptions** belong to `SessionsOfCloyd`. One running-session stream serves all windows. Host
  discovery also checks the inventory because a shell can start or exit without changing the running agents.
  Claude Code's visible chats share an ordered conversation stream for delivery tracking.
- **View models** hold the dashboard rows, Claude Code turn status, history picker scope and filter, and the
  shared chat input. The shared catalog now gives its view-model guidance to code-built views too.

## Original request and design discussion

The following records the November 21–22 discussion in new-view. Its estimates, proposed names, open questions
and descriptions of the code are from before implementation.

## What Magnus asked for

Magnus, 2026-11-21: "I want a view where I see all sessions in all projects as a kind of hierarchical tree on the left.
So I should be able to see all sessions currently active all at once at a glance. This would be a separate window from
the current cloyd windows that are one per project. If not hard, I'd like to be able to interact with the sessions in
this view the same way that I can in the current windows. This probably means that things I write and paste into the
input boxes need to sync between the windows. If that is hard, just viewing and not sending can be step one."

## What Magnus decided

- **It stays beside the project windows, with all they can do:** "It won't replace it, but I will want to be able to do
  everything I can in the per-project window in this view too." So starting a session, the session history picker,
  ending a session and the keys (Ctrl+H, Ctrl+Tab, Ctrl+Alt+Shift+Y) all come to the new window.
- **The terminal shows in one window at a time, where it was last asked for** (Ctrl+H, the Terminal switch, "Show the
  terminal"): "Sounds good. I only need to look at it in one window at a time. The other can just show whatever." The
  other window shows the chat.
- **What the tree lists** ("Agreed with your ideas"): every session host of every project, grouped by project, whether or
  not a project window or an IDE has the project open, including hosts whose Claude Code exited to the shell. The
  sessions that run in no session host, such as in a terminal of their own, show greyed in a group of their own,
  read-only: Cloyd can't show their terminal.
- **How it opens:** "For now, A button at the far right of the tab list in a per-project window should do the trick?"
- **MVVM where it earns its place, not across Cloyd** ("OK"). Cloyd builds its views in C# and keeps the state in the
  controls. The shared rule "Avalonia AXAML View Guidelines" says when a view gets a view model, but loads only for
  `.axaml` files, so it never reached the sessions that built Cloyd (an incident was logged, 2026-11-21).
  - **A view model where views show state that changes, or state the view edits:** the session's own state (below), the
    status rows (`Cloyd.Chat.Status`), the new window's tree, the session history picker. Bindings are the standard way
    for several views to hear of changes to one state.
  - **Code, as now:** the terminal control, which holds the emulator's screen and scrollback itself; the chat's drawing
    (`DrawnEndOfTheConversation`, scroll anchoring, `FollowingTheEnd`, the markdown); AvaloniaEdit's editor; the windows'
    virtual desktop and saving mechanics.
  - **AXAML for new views** where a declarative layout reads better, such as the tree's rows; no converting the existing
    code-built views for style alone. CommunityToolkit.Mvvm and compiled bindings, as JAStudio and Deskmancer use.

## The design

### One session's state, shared by every window that shows it

All windows run in the one Cloyd application process, so nothing syncs between processes: the windows share objects.

- **A view model per session host, owned by the application, not by a window.** It holds:
  - the `AttachmentToASessionHost` and the `TerminalOfASessionHost`: a session host has one attach client at a time,
    the newest taking the session over, and its console has one size;
  - the `AgentCli` that runs in the host, which the terminal is given (since 42ac8f6 its constructor takes
    `Func<AgentCli> agentCliInTheHost`, for `IgnoresTheShiftOnEnter`);
  - the `RunningSession`, from one follow of the hub's running sessions for the whole application. Today each
    `ProjectWindow` follows them on its own (`FollowTheRunningSessions`);
  - the `LatestSession` and the title;
  - **only for a Claude Code session,** as an optional part, the chat's state: the `DraftOfAMessage` and the
    `SendingFromTheChat` with its `MessageOnItsWay`s. By Magnus's word the chat and its classes stay each agent CLI's own
    (cloyd-gpt): a Codex session has a host, an attachment and terminal, a running session and a title, and no chat yet
    (`AgentCli.HasAChat`).
- **Today all of it is inside the views:** `SessionTab` owns the attachment, the terminal and the `ChatOfASession`;
  `InputBoxOfTheChat` owns the `DraftOfAMessage` and the `SendingFromTheChat`, and sends through the hub, which is a
  control doing domain work. The owning control pushes state into its children (`ChatOfASession.ClaudeCode`'s setter
  calls `StatusLine.Show`, `WorkingLine.Show` and the like), which works for one view and not for two.
- **Per view, not per session:** the chat's search (`FoundInTheChat`), what the view follows (`WhatTheViewFollows`),
  `DrawnEndOfTheConversation`, `OpenedInTheChat`, the scroll position.
- **The draft syncs by sharing its document.** `DraftOfAMessage` is AvaloniaEdit's `TextDocument`, with the pasted
  stretches in a `TextSegmentCollection` on it. Several editors can edit one document: AvaloniaEdit 12.0.0's
  `TextArea.OnDocumentChanged` subscribes through weak events, and each editor keeps its own caret and selection
  (checked in the decompiled source, 2026-11-21). Two input boxes on one draft show the same text as Magnus types.
- **What changes with it:**
  - `ProjectWindow.BringBackTheTabs` shows the hosts that no attach client shows (`!host.Attached`); with the
    application's own attachment serving every window, it asks the application instead.
  - The drafts are saved per session by the application, not with the project window's tabs (`SavedSessionTab.Draft`),
    so that a session shown only in the new window keeps its draft too.
  - The chat follows a conversation only while shown, so the new window adds one hub stream, for its selected session.

### The new window

- **The tree on the left, project then sessions.** Each session's row: Claude Code's spinner while busy, as the tab
  headers have it (`SpinnerOfClaudeCode`); a prominent orange mark while it waits for Magnus in the terminal (status
  `waiting`); its name; greyed under it, what the turn does, from `TurnUnderWay` ("Running tools… 1m 12s"). A project's
  row turns orange when any of its sessions waits for Magnus, so that a collapsed project still tells. A Codex session's
  row has its status (busy, waiting, idle, error) and no turn: the hub has none for Codex.
- **On the right, the selected session:** its `ChatOfASession` with the Chat/Terminal switch, or a Codex session's
  terminal.
- **The data is all the hub's already:** `GET /session-hosts`, each labelled with its project (`SessionHostLabel`), and
  the running sessions' stream. No change to the hub.
- **The terminal moves** to the window that last asked for it; the other window's tab says it shows in the other window,
  with a button that brings it back. The console resizes, and Claude Code redraws, as on an attach. An Avalonia control
  has one parent at a time.

### Calls made in new-view, for Magnus to know of

- The button opens the new window, or brings it forward when it's open. It's an ordinary window that goes back to the
  virtual desktop it was on, as the project windows do: no pinning to every desktop, which needs Windows' undocumented
  virtual desktop API.
- A notification's click (`POST /sessions/show`) still selects the project window's tab, as now; it goes to the new
  window only when no project window shows the session.

## The work, in steps

An estimate, not a measurement, of agent time; the yardstick is that agents wrote Cloyd's 7,300 lines and its 4,400 lines
of specifications in about two days. About two days in all.

1. **The refactoring, with no change Magnus could see: half a day to a day.** The session's view model, the views bound
   to it, one follow of the running sessions, saving the drafts per session. It goes through `SessionTab`,
   `ChatOfASession`, `InputBoxOfTheChat`, `Cloyd.Chat.Status`, `ProjectWindow` and `Cloyd.Saving`; 29 of the
   specification files reach into those controls and must keep passing. In commits of its own.
2. **The new window: a few hours.** The tree with the statuses, the chat on the right with sending, the terminal moving,
   the button at the far right of the tab strip, with specifications. Magnus can try it from here.
3. **Everything the project window does: a few hours.** + on a project's row, × to end a session, the session history
   picker (laid over a `ProjectWindow` and acting on it today: it needs generalizing), the keys (Ctrl+Tab moving through
   the tree), saving the new window's selection and its desktop.

**Not known yet:**

- **How Iciclecreek's terminal control takes moving from one window to another.** It connects once its template is
  applied, and `TerminalOfASessionHost` allows that once. Read d4b93ce ("Keep Cloyd terminals attached when their tabs
  move", with the tabs' reordering by drag, 740c48a) first: it may have settled part of this. The fallback: a new
  terminal and attachment in the window that asks, letting go of the old one; the session host redraws its whole screen
  for each new attach client. Its cost: the scrollback, the 5,000 lines the old control kept, doesn't come along.
- **Whether the hub can send to a Claude Code that runs in no session host.** Until known, those sessions are read-only.

## Questions resolved on November 22

Magnus delegated items 6 to 9 and named the window Projects Dashboard. The original alternatives follow.

- **6. The views rule:** move the shared catalog's section "When a view gets a ViewModel" (in
  `.claude-shared/rules/path-scoped/avalonia-axaml-views.md`) into a universal code standard for UI code, keeping the
  AXAML specifics path-scoped, and push the catalog. Recommended, so that the next Avalonia project built in code gets it.
- **7. The go-ahead, and the order with .NET-cloyd's chat features.** .NET-cloyd's open items for Magnus are all chat
  features in the same files: queued messages over the input box, a marker in the tab header while Claude waits in the
  terminal, links in the chat, the background work's look, the line after each turn. .NET-cloyd and new-view proposed
  that the refactoring goes first and the features follow on top of it; whichever session gets Magnus's go first tells
  the other.
- **8. The window's name.** Recommended: the all-projects window, `AllProjectsWindow`, titled "Cloyd: all projects". It
  pairs with `ProjectWindow`, and reuses the history picker's "All projects" scope (`ClaudeSessionHistoryScope.AllProjects`).
  Others: the all-sessions window (but the project window shows sessions too, and "all sessions" reads as the whole
  history); the overview window (suggests looking, not acting); the switchboard (a metaphor); the dashboard (suggests
  metrics). Not "hub": that's `TheHub`.
- **9. Codex sessions in the tree.** Recommended, by cloyd-gpt and new-view: yes, as above. Magnus said of listing them
  "It is just different types of sessions", and the + and the history picker show both kinds already.

## Working with the others

- **.NET-cloyd** works on Cloyd's chat. Its helper's Shift+Enter fix for Codex tabs is merged (42ac8f6, f9cd574). Tell it
  before starting the refactoring.
- **cloyd-gpt** built the Codex tabs (`AgentCli`, `RunningSession` with `RunningClaudeCode` and `RunningCodex`,
  `Cloyd.Projects.ResumableSessions`, `Cloyd.Hub.RunningSessions`, `Cloyd.Hub.Commands`); nothing of its was in flight
  on 2026-11-22.
- **Codex helpers:** .NET-cloyd keeps codex-helper-1 (`cloyd-codex-1`) and gave codex-helper-2 (`cloyd-codex-2`, in
  `C:\Dev\ClaudeSymbolLinker.worktrees\codex-helper-2`) to this work. How to brief and review them:
  `development-environment.md`, "Codex helpers, in checkouts of their own", and
  `~/.claude/dev_docs/working-with-codex-helpers.md`. The plan was to do the refactoring in the coordinating session,
  since it decides the design, and hand a helper a self-contained part after it, such as the tree with its statuses,
  as a first trial of a helper on UI work.
