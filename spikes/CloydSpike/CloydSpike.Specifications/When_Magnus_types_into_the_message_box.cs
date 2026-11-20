using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Compze.Must;
using Compze.xUnitBDD;
using static CloydSpike.Specifications.HeadlessCloydApp;
using MessageBox = CloydSpike.Chat.MessageBox;

namespace CloydSpike.Specifications;

/// <summary>The chat's input box, Avalonia's editor, typed into with the keyboard on the headless platform.</summary>
public abstract class When_Magnus_types_into_the_message_box
{
   readonly List<string> _sent = [];
   string _boxHolds = "";

   void TypeAndPress(params object[] textAndKeys) => _boxHolds = OnTheUiThread(() =>
   {
      var box = new MessageBox();
      box.Sending += _sent.Add;
      var window = new Window { Width = 600, Height = 200, Content = box };
      window.Show();
      box.FocusTheBox();
      foreach(var textOrKey in textAndKeys)
      {
         if(textOrKey is string text) window.KeyTextInput(text);
         else if(textOrKey is (PhysicalKey key, RawInputModifiers modifiers))
         {
            window.KeyPressQwerty(key, modifiers);
            window.KeyReleaseQwerty(key, modifiers);
         }
         Dispatcher.UIThread.RunJobs();
      }
      window.Close();
      return box.Text;
   });

   public class a_line_then_Enter : When_Magnus_types_into_the_message_box
   {
      public a_line_then_Enter() => TypeAndPress("Fix the merge, please.", (PhysicalKey.Enter, RawInputModifiers.None));

      [XF] public void it_is_sent() => _sent.AsEnumerable().Must().SequenceEqual(["Fix the merge, please."]);
   }

   public class two_lines_with_Shift_Enter_between : When_Magnus_types_into_the_message_box
   {
      public two_lines_with_Shift_Enter_between() => TypeAndPress("first", (PhysicalKey.Enter, RawInputModifiers.Shift), "second");

      [XF] public void nothing_is_sent() => _sent.AsEnumerable().Must().BeEmpty();
      [XF] public void the_box_holds_both_lines() => _boxHolds.ReplaceLineEndings("\n").Must().Be("first\nsecond");
   }
}
