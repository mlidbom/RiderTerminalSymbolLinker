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

   public class to_a_member_qualified_by_its_namespace(TheSandboxWithTheFixtureSolution sandbox) : When_clicking_a_link(sandbox)
   {
      readonly ClickOutcome _outcome = sandbox.Click("Geometry.Shapes.Circle.Area() is wrong", "Geometry.Shapes.Circle.Area");

      [XF] public void it_opens_the_types_file() => _outcome.OpenedFile.Must().Be("Geometry/Shapes/Circle.cs");
      [XF] public void the_caret_is_on_the_members_declaration() => _outcome.CaretLine.Must().Be(8);
      [XF] public void the_caret_is_at_the_start_of_the_members_name() => _outcome.CaretColumn.Must().Be(17);
   }

   public class to_a_type_name_that_two_namespaces_share_qualified_by_one_of_them(TheSandboxWithTheFixtureSolution sandbox) : When_clicking_a_link(sandbox)
   {
      readonly ClickOutcome _outcome = sandbox.Click("Layout.Bounds is wrong", "Layout.Bounds");

      [XF] public void it_opens_the_file_of_the_type_in_that_namespace() => _outcome.OpenedFile.Must().Be("Geometry/Layout/Bounds.cs");
      [XF] public void the_caret_is_on_the_types_declaration() => _outcome.CaretLine.Must().Be(4);
      [XF] public void the_caret_is_at_the_start_of_the_types_name() => _outcome.CaretColumn.Must().Be(15);
   }

   public class to_a_property_declared_in_a_csharp_14_extension_block(TheSandboxWithTheFixtureSolution sandbox) : When_clicking_a_link(sandbox)
   {
      readonly ClickOutcome _outcome = sandbox.Click("the Diameter is wrong", "Diameter");

      [XF] public void it_opens_the_file_that_declares_the_block() => _outcome.OpenedFile.Must().Be("Geometry/Shapes/CircleExtensions.cs");
      [XF] public void the_caret_is_on_the_propertys_declaration() => _outcome.CaretLine.Must().Be(10);
      [XF] public void the_caret_is_at_the_start_of_the_propertys_name() => _outcome.CaretColumn.Must().Be(19);
   }

   public class to_a_property_declared_in_a_csharp_14_extension_block_after_the_name_of_the_class_that_declares_the_block(TheSandboxWithTheFixtureSolution sandbox)
      : When_clicking_a_link(sandbox)
   {
      readonly ClickOutcome _outcome = sandbox.Click("CircleExtensions.Diameter is wrong", "CircleExtensions.Diameter");

      [XF] public void it_opens_the_file_that_declares_the_block() => _outcome.OpenedFile.Must().Be("Geometry/Shapes/CircleExtensions.cs");
      [XF] public void the_caret_is_on_the_propertys_declaration() => _outcome.CaretLine.Must().Be(10);
   }

   public class to_a_property_of_a_positional_record(TheSandboxWithTheFixtureSolution sandbox) : When_clicking_a_link(sandbox)
   {
      readonly ClickOutcome _outcome = sandbox.Click("Bounds.Width is negative", "Bounds.Width");

      [XF] public void it_opens_the_records_file() => _outcome.OpenedFile.Must().Be("Geometry/Shapes/Bounds.cs");
      [XF] public void the_caret_is_on_the_records_header() => _outcome.CaretLine.Must().Be(4);
      [XF] public void the_caret_is_at_the_start_of_the_parameter_that_declares_the_property() => _outcome.CaretColumn.Must().Be(29);
   }

   /// <summary>
   ///   ReSharper lists the parts of a partial type with the one edited last first, and the MCP's <c>go_to_definition</c>
   ///   gave that part until it gave the one in the file named after the type, its main declaration.
   /// </summary>
   public class to_a_type_declared_in_parts_whose_other_part_was_edited_last : When_clicking_a_link
   {
      const string TheOtherPartWithAMemberAdded = """
         namespace Geometry.Shapes;

         public sealed partial class Palette
         {
           public string Mix(string first, string second) => $"{first}+{second}";

           public string Blend(string first, string second) => Mix(first, second);
         }

         """;

      readonly ClickOutcome _outcome;

      public to_a_type_declared_in_parts_whose_other_part_was_edited_last(TheSandboxWithTheFixtureSolution sandbox) : base(sandbox)
      {
         sandbox.Edit("Geometry/Shapes/Palette.Mixing.cs", TheOtherPartWithAMemberAdded, "Geometry.Shapes.Palette.Blend");
         _outcome = sandbox.Click("the Palette is wrong", "Palette");
      }

      [XF] public void it_opens_the_part_in_the_file_named_after_the_type() => _outcome.OpenedFile.Must().Be("Geometry/Shapes/Palette.cs");
      [XF] public void the_caret_is_on_that_parts_declaration() => _outcome.CaretLine.Must().Be(4);
      [XF] public void the_caret_is_at_the_start_of_the_types_name() => _outcome.CaretColumn.Must().Be(29);
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

      /// <summary>Centred in the window, it showed far from the link, as Cloyd's picker did (Magnus, 2026-11-26).</summary>
      [XF] public void the_picker_opens_where_the_link_was_clicked() => Outcome.PickerLocation.Must().Be(Outcome.ClickedAt);

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

   /// <summary>
   ///   The terminal's text doesn't say which overload it means, so a click lists them all, in the order they're declared.
   ///   Magnus, 2026-11-24: "If a symbol is ambiguous, showing a list is IMO the correct thing to do."
   /// </summary>
   public class to_a_method_with_overloads(TheSandboxWithTheFixtureSolution sandbox) : When_clicking_a_link(sandbox), IDisposable
   {
      readonly ClickOutcome _outcome = sandbox.Click("Circle.ScaledBy() is wrong", "Circle.ScaledBy");

      [XF] public void it_shows_a_picker_of_the_overloads() => _outcome.PickerTitle.Must().Be("Symbols matching \"Circle.ScaledBy\"");
      [XF] public void the_picker_has_a_row_for_each_overload_in_the_order_they_are_declared() =>
         _outcome.PickerRows.Must().SequenceEqual(["method Geometry.Shapes.Circle.ScaledBy  —  Circle.cs:10", "method Geometry.Shapes.Circle.ScaledBy  —  Circle.cs:13"]);

      public void Dispose() => Sandbox.ClosePicker();
   }

   /// <summary>
   ///   A click on a namespace always shows a picker, of one type too, so that Magnus sees he clicked a namespace, and says
   ///   so above its rows (2026-11-26).
   /// </summary>
   public class to_a_namespace(TheSandboxWithTheFixtureSolution sandbox) : When_clicking_a_link(sandbox), IDisposable
   {
      protected readonly ClickOutcome Outcome = sandbox.Click("Geometry.Shapes is where it is", "Geometry.Shapes");

      [XF] public void it_shows_a_picker_titled_with_the_namespace() => Outcome.PickerTitle.Must().Be("Namespace \"Geometry.Shapes\"");

      [XF] public void the_picker_says_above_its_rows_that_a_namespace_was_clicked() =>
         Outcome.PickerNote.Must().Be("You clicked a namespace. These are the types in it and in the namespaces under it: choose one to open it.");

      [XF] public void the_picker_has_a_row_for_each_type_in_it_nested_types_included_then_for_those_in_the_namespaces_under_it() =>
         Outcome.PickerRows.Must().SequenceEqual([
            "record Geometry.Shapes.Bounds  —  Bounds.cs:4",
            "class Geometry.Shapes.Circle  —  Circle.cs:4",
            "class Geometry.Shapes.CircleExtensions  —  CircleExtensions.cs:6",
            "class Geometry.Shapes.Drawing  —  Drawing.cs:4",
            "record Geometry.Shapes.Drawing.Stroke  —  Drawing.cs:6",
            "class Geometry.Shapes.Palette  —  Palette.cs:4",
            "class Geometry.Shapes.Swatch  —  Swatch.cs:7",
            "class Geometry.Shapes.Rendering.Brush  —  Brush.cs:7"
         ]);

      [XF] public void the_picker_opens_where_the_link_was_clicked() => Outcome.PickerLocation.Must().Be(Outcome.ClickedAt);

      public virtual void Dispose() => Sandbox.ClosePicker();

      public class and_choosing_the_row_of_a_type_in_a_namespace_under_it : to_a_namespace
      {
         readonly ClickOutcome _chosen;

         public and_choosing_the_row_of_a_type_in_a_namespace_under_it(TheSandboxWithTheFixtureSolution sandbox) : base(sandbox) => _chosen = sandbox.ChoosePickerRow(7);

         [XF] public void it_opens_the_file_of_that_type() => _chosen.OpenedFile.Must().Be("Geometry/Shapes/Rendering/Brush.cs");
         [XF] public void the_caret_is_on_the_types_declaration() => _chosen.CaretLine.Must().Be(7);
         [XF] public void the_caret_is_at_the_start_of_the_types_name() => _chosen.CaretColumn.Must().Be(14);

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
