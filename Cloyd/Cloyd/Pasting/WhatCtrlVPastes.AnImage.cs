using Avalonia.Media.Imaging;

namespace Cloyd.Pasting;

partial record WhatCtrlVPastes
{
   /// <summary>The clipboard's image, <paramref name="Image"/>, which it holds with no text.</summary>
   public sealed record AnImage(Bitmap Image) : WhatCtrlVPastes;
}
