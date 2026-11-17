using Compze.Must;
using Compze.xUnitBDD;

// ReSharper disable InconsistentNaming

namespace RiderTerminalSymbolLinker.Specifications.Clicking;

public class When_clicking_a_link(TheSandboxWithTheFixtureSolution sandbox)
{
   protected readonly TheSandboxWithTheFixtureSolution Sandbox = sandbox;

   public class to_a_member_of_a_type_whose_name_no_other_type_has(TheSandboxWithTheFixtureSolution sandbox) : When_clicking_a_link(sandbox)
   {
      readonly ClickOutcome _outcome = sandbox.Click("Circle.Area() is wrong", "Circle.Area");

      [XF] public void it_opens_the_types_file() => _outcome.OpenedFile.Must().Be("Geometry/Shapes/Circle.cs");
      [XF] public void the_caret_is_on_the_members_declaration() => _outcome.CaretLine.Must().Be(8);
      [XF] public void the_caret_is_at_the_start_of_the_members_name() => _outcome.CaretColumn.Must().Be(17);
   }

   public class to_a_property_declared_in_a_csharp_14_extension_block(TheSandboxWithTheFixtureSolution sandbox) : When_clicking_a_link(sandbox)
   {
      readonly ClickOutcome _outcome = sandbox.Click("the Diameter is wrong", "Diameter");

      [XF] public void it_opens_the_file_that_declares_the_block() => _outcome.OpenedFile.Must().Be("Geometry/Shapes/CircleExtensions.cs");
      [XF] public void the_caret_is_on_the_propertys_declaration() => _outcome.CaretLine.Must().Be(10);
      [XF] public void the_caret_is_at_the_start_of_the_propertys_name() => _outcome.CaretColumn.Must().Be(19);
   }

   public class to_a_partial_path_with_a_line(TheSandboxWithTheFixtureSolution sandbox) : When_clicking_a_link(sandbox)
   {
      readonly ClickOutcome _outcome = sandbox.Click("see Shapes/Circle.cs:10", "Shapes/Circle.cs:10");

      [XF] public void it_opens_the_file() => _outcome.OpenedFile.Must().Be("Geometry/Shapes/Circle.cs");
      [XF] public void the_caret_is_on_that_line() => _outcome.CaretLine.Must().Be(10);
      [XF] public void nothing_is_selected() => _outcome.SelectedLines.Must().BeNull();
   }

   public class to_a_partial_path_with_a_range_of_lines(TheSandboxWithTheFixtureSolution sandbox) : When_clicking_a_link(sandbox)
   {
      readonly ClickOutcome _outcome = sandbox.Click("see Shapes/Circle.cs:6-8", "Shapes/Circle.cs:6-8");

      [XF] public void it_opens_the_file() => _outcome.OpenedFile.Must().Be("Geometry/Shapes/Circle.cs");
      [XF] public void the_lines_of_the_range_are_selected() => _outcome.SelectedLines.Must().SequenceEqual([6, 8]);
   }

   public class to_a_type_name_that_two_namespaces_share(TheSandboxWithTheFixtureSolution sandbox) : When_clicking_a_link(sandbox), IDisposable
   {
      protected readonly ClickOutcome Outcome = sandbox.Click("Bounds is ambiguous", "Bounds");

      [XF] public void it_shows_a_picker_of_the_symbols() => Outcome.PickerTitle.Must().Be("Symbols matching \"Bounds\"");
      [XF] public void the_picker_has_a_row_for_each_type_by_its_namespace_qualified_name() =>
         Outcome.PickerRows.Must().SequenceEqual(["record Geometry.Layout.Bounds  —  Bounds.cs:4", "record Geometry.Shapes.Bounds  —  Bounds.cs:4"]);

      public virtual void Dispose() => Sandbox.ClosePicker();

      public class and_choosing_the_second_row : to_a_type_name_that_two_namespaces_share
      {
         readonly ClickOutcome _chosen;

         // In the constructor's body, not a field initializer, which would run before the base class's click.
         public and_choosing_the_second_row(TheSandboxWithTheFixtureSolution sandbox) : base(sandbox) => _chosen = sandbox.ChoosePickerRow(1);

         [XF] public void it_opens_the_file_of_the_type_in_the_second_row() => _chosen.OpenedFile.Must().Be("Geometry/Shapes/Bounds.cs");
         [XF] public void the_caret_is_on_its_declaration() => _chosen.CaretLine.Must().Be(4);
         [XF] public void the_caret_is_at_the_start_of_the_types_name() => _chosen.CaretColumn.Must().Be(15);

         public override void Dispose() {} // Choosing a row closed the picker.
      }
   }

   public class to_a_file_name_that_two_files_share(TheSandboxWithTheFixtureSolution sandbox) : When_clicking_a_link(sandbox), IDisposable
   {
      readonly ClickOutcome _outcome = sandbox.Click("see Bounds.cs", "Bounds.cs");

      [XF] public void it_shows_a_picker_of_the_files() => _outcome.PickerTitle.Must().Be("Files named \"Bounds.cs\"");
      [XF] public void the_picker_has_a_row_for_each_file_by_its_path_in_the_solution() =>
         _outcome.PickerRows.Must().SequenceEqual(["Geometry/Layout/Bounds.cs", "Geometry/Shapes/Bounds.cs"]);

      public void Dispose() => Sandbox.ClosePicker();
   }
}
