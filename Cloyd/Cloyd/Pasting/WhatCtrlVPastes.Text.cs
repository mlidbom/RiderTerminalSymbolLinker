namespace Cloyd.Pasting;

partial record WhatCtrlVPastes
{
   /// <summary>The clipboard's text, <paramref name="Value"/>.</summary>
   public sealed record Text(string Value) : WhatCtrlVPastes;
}
