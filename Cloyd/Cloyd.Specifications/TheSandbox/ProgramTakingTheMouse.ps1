# A program in a session's terminal that takes the mouse as Codex does on Windows: by the console's mode, which has
# conhost ask the terminal for SGR mouse reports (ESC [ ? 1003 ; 1006 h). It then turns urxvt's mouse encoding off, as
# crossterm does outside Windows (ESC [ ? 1015 l), which conhost passes on to the terminal, as it doesn't know the mode.
# It says what came to it until q is typed, and gives the mouse back.
Add-Type -Namespace Specifications -Name ConsoleInput -MemberDefinition @'
[DllImport("kernel32.dll")] static extern IntPtr GetStdHandle(int which);
[DllImport("kernel32.dll")] static extern bool GetConsoleMode(IntPtr handle, out uint mode);
[DllImport("kernel32.dll")] static extern bool SetConsoleMode(IntPtr handle, uint mode);
[DllImport("kernel32.dll", EntryPoint = "ReadConsoleInputW")] static extern bool ReadConsoleInput(IntPtr handle, [Out] Record[] records, uint length, out uint read);

[StructLayout(LayoutKind.Explicit, Size = 20)]
public struct Record
{
   [FieldOffset(0)] public ushort Kind;
   [FieldOffset(4)] public int KeyIsDown;
   [FieldOffset(14)] public char Character;
}

const ushort OfAKey = 1;
const ushort OfTheMouse = 2;

// The mouse's input, the window's, and the flags that turn quick edit off, as crossterm sets them.
const uint WithTheMouse = 0x0010 | 0x0008 | 0x0080;

static IntPtr Input { get { return GetStdHandle(-10); } }

public static uint TakeTheMouse()
{
   uint before;
   GetConsoleMode(Input, out before);
   SetConsoleMode(Input, WithTheMouse);
   return before;
}

public static void GiveBackTheMouse(uint before) { SetConsoleMode(Input, before); }

public static string WhatComesUntilQ()
{
   var keys = new System.Text.StringBuilder();
   var ofTheMouse = 0;
   var records = new Record[1];
   uint read;
   while(ReadConsoleInput(Input, records, 1, out read))
   {
      if(records[0].Kind == OfTheMouse) ofTheMouse++;
      else if(records[0].Kind == OfAKey && records[0].KeyIsDown != 0 && records[0].Character != '\0')
      {
         if(records[0].Character == 'q') break;
         keys.Append(records[0].Character);
      }
   }
   return "keys [" + keys + "], mouse events " + ofTheMouse;
}
'@

$before = [Specifications.ConsoleInput]::TakeTheMouse()
[Console]::Out.Write("$([char]27)[?1015l")
'It has the mouse.'
$whatCame = [Specifications.ConsoleInput]::WhatComesUntilQ()
[Specifications.ConsoleInput]::GiveBackTheMouse($before)
"What came: $whatCame"
