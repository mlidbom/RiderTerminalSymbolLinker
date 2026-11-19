using Compze.Must;
using Compze.xUnitBDD;
using Xunit;

// ReSharper disable InconsistentNaming

namespace RiderTerminalSymbolLinker.Specifications.ClaudeSessions;

// The New Claude Session popup: the + in the tool window's title bar opens it, for the name of the session to start.
// Enter starts the session. Each story runs once, in its class fixture.

public class When_a_session_is_started_from_the_new_session_popup_with_a_name(When_a_session_is_started_from_the_new_session_popup_with_a_name.Story story)
   : IClassFixture<When_a_session_is_started_from_the_new_session_popup_with_a_name.Story>
{
   public sealed class Story
   {
      public const string Name = "Fixing the login bug";
      public readonly SessionTab Tab;
      public readonly string TabName;
      public readonly NewSessionPopup PopupAfter;
      public readonly TerminalSize? SizeTheClaudeCodeDrawsAt;
      public readonly TerminalSize? SizeOfTheTab;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         view.OpenTheNewSessionPopup();
         view.NameInTheNewSessionPopup(Name);
         Tab = view.PressEnterInTheNewSessionPopup();
         TabName = view.TabNames.Single();
         PopupAfter = view.NewSessionPopup;
         SizeTheClaudeCodeDrawsAt = view.SizeTheClaudeCodeDrawsAtIn(0);
         SizeOfTheTab = view.SizeOf(0);
      }
   }

   [XF] public void the_session_has_the_name() => story.Tab.SessionName.Must().Be(Story.Name);
   [XF] public void the_tab_is_named_after_it() => story.TabName.Must().Be(Story.Name);
   [XF] public void the_popup_closes() => story.PopupAfter.Shown.Must().BeFalse();

   // Until 2026-11-19, often at 80 columns by 20 rows, the terminal's guess, until the tab was resized.
   [XF] public void its_claude_code_draws_itself_at_the_size_of_the_tab() => story.SizeTheClaudeCodeDrawsAt.Must().Be(story.SizeOfTheTab);
}

public class When_a_session_is_started_from_the_new_session_popup_with_its_field_left_empty(When_a_session_is_started_from_the_new_session_popup_with_its_field_left_empty.Story story)
   : IClassFixture<When_a_session_is_started_from_the_new_session_popup_with_its_field_left_empty.Story>
{
   public sealed class Story
   {
      public readonly SessionTab Tab;
      public readonly string TabName;
      public readonly NewSessionPopup PopupAfter;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         view.OpenTheNewSessionPopup();
         Tab = view.PressEnterInTheNewSessionPopup();
         TabName = view.TabNames.Single();
         PopupAfter = view.NewSessionPopup;
      }
   }

   // The name the stand-in for Claude Code gives a session that wasn't given one, as Claude Code gives it one of its own.
   [XF] public void the_session_is_given_no_name() => story.Tab.SessionName.Must().Be($"fake-{story.Tab.SessionId![..8]}");
   [XF] public void the_tab_is_named_claude() => story.TabName.Must().Be("Claude");
   [XF] public void the_popup_closes() => story.PopupAfter.Shown.Must().BeFalse();
}

public class When_the_new_session_popup_is_closed_with_escape(When_the_new_session_popup_is_closed_with_escape.Story story)
   : IClassFixture<When_the_new_session_popup_is_closed_with_escape.Story>
{
   public sealed class Story
   {
      public readonly NewSessionPopup AsItOpens;
      public readonly NewSessionPopup AfterEscape;
      public readonly int TabsAfter;

      public Story()
      {
         var view = new TheClaudeSessionsView();
         AsItOpens = view.OpenTheNewSessionPopup();
         view.NameInTheNewSessionPopup("Never started");
         AfterEscape = view.CloseTheNewSessionPopup();
         Thread.Sleep(TimeSpan.FromSeconds(2)); // Time for a tab to show, were one opened.
         TabsAfter = view.Tabs.Count;
      }
   }

   [XF] public void it_opens_with_its_field_empty() => story.AsItOpens.Must().Be(new NewSessionPopup(true, ""));
   [XF] public void it_closes() => story.AfterEscape.Shown.Must().BeFalse();
   [XF] public void no_session_starts() => story.TabsAfter.Must().Be(0);
}

/// <summary>
///   A session is named in the popup with <see cref="Name"/>, a name with each character some shell acts on, while the
///   view's terminals run the shell a story names: each shell has its own way of quoting it.
/// </summary>
public abstract class A_session_named_with_every_character_a_shell_acts_on_story
{
   /// <summary>
   ///   Quotes of every kind, PowerShell's typographic single quotes among them, a variable, the shells' escapes and
   ///   operators, whitespace, and a backslash at its end, which would escape a closing <c>"</c>.
   /// </summary>
   public static readonly string Name = $"""it's "quoted" $HOME `tick` & a|b <c> (d) 50% ^caret {(char)0x2018}curly{(char)0x2019} back\slash\""";

   public readonly SessionTab Tab;

   protected A_session_named_with_every_character_a_shell_acts_on_story(string shell)
   {
      var view = new TheClaudeSessionsView();
      view.RunTheShell(shell);
      view.OpenTheNewSessionPopup();
      view.NameInTheNewSessionPopup(Name);
      Tab = view.PressEnterInTheNewSessionPopup();
   }
}

public class When_a_session_is_named_with_every_character_a_shell_acts_on_in_powershell_7(When_a_session_is_named_with_every_character_a_shell_acts_on_in_powershell_7.Story story)
   : IClassFixture<When_a_session_is_named_with_every_character_a_shell_acts_on_in_powershell_7.Story>
{
   public sealed class Story() : A_session_named_with_every_character_a_shell_acts_on_story("pwsh.exe");

   [XF] public void the_session_gets_the_name_as_it_was_typed() => story.Tab.SessionName.Must().Be(A_session_named_with_every_character_a_shell_acts_on_story.Name);
}

public class When_a_session_is_named_with_every_character_a_shell_acts_on_in_windows_powershell(When_a_session_is_named_with_every_character_a_shell_acts_on_in_windows_powershell.Story story)
   : IClassFixture<When_a_session_is_named_with_every_character_a_shell_acts_on_in_windows_powershell.Story>
{
   public sealed class Story() : A_session_named_with_every_character_a_shell_acts_on_story("powershell.exe");

   [XF] public void the_session_gets_the_name_as_it_was_typed() => story.Tab.SessionName.Must().Be(A_session_named_with_every_character_a_shell_acts_on_story.Name);
}

public class When_a_session_is_named_with_every_character_a_shell_acts_on_in_cmd(When_a_session_is_named_with_every_character_a_shell_acts_on_in_cmd.Story story)
   : IClassFixture<When_a_session_is_named_with_every_character_a_shell_acts_on_in_cmd.Story>
{
   public sealed class Story() : A_session_named_with_every_character_a_shell_acts_on_story("cmd.exe");

   [XF] public void the_session_gets_the_name_as_it_was_typed() => story.Tab.SessionName.Must().Be(A_session_named_with_every_character_a_shell_acts_on_story.Name);
}

public class When_a_session_is_named_with_every_character_a_shell_acts_on_in_git_bash(When_a_session_is_named_with_every_character_a_shell_acts_on_in_git_bash.Story story)
   : IClassFixture<When_a_session_is_named_with_every_character_a_shell_acts_on_in_git_bash.Story>
{
   public sealed class Story() : A_session_named_with_every_character_a_shell_acts_on_story(@"C:\Program Files\Git\bin\bash.exe");

   // Git for Windows' bin\bash.exe is a launcher, which runs usr\bin\bash.exe as its child: the hub types into the inner one.
   [XF] public void the_session_gets_the_name_as_it_was_typed() => story.Tab.SessionName.Must().Be(A_session_named_with_every_character_a_shell_acts_on_story.Name);
}
