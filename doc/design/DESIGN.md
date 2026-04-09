# GTK => WPF Port: Design

* use .NET 9, as it supports dark/light theme switching automatically
* use plain WPF for default Windows appearance
* ensure everything conforms to dark/light theme switching, even if some features don't work by default: title bars, About dialogs, etc.
* never use `SystemColors` resources (e.g. `SystemColors.ControlBrushKey`, `SystemColors.WindowBrushKey`) — these are legacy Win32 colors that do not follow the Fluent `ThemeMode="System"` dark/light switching. Instead, omit explicit backgrounds to inherit from the Fluent theme, or use theme-aware Fluent resource keys.
* follow Microsoft design guidelines but reuse the GNOME icons from the GTK app

## Icon Conversion: SVG to ICO

The `bin/SvgToIco` utility converts source icons to Windows `.ico` format. It uses the `Svg` NuGet library (SVG.NET) for SVG rendering, which has known limitations:

- **`<mask>` elements** are not rendered correctly — masked regions may appear solid or transparent incorrectly
- **`linearGradient` fills combined with masks** often produce grey/black output instead of the intended colors
- **Transformed groups** (`<g transform="matrix(...)">`) containing paths may not render child elements

Because of these limitations, **prefer PNG input over SVG** when generating `.ico` files. The `SvgToIco` tool accepts both PNG and SVG input. Use the PNG source (e.g. `yellow-floppy5-nomargin.png`) to produce correct icons with gradients, masks, and complex paths intact.
