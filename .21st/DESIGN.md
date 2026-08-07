<!-- Maintained after 21st design-context initialization. -->
# Project Design Context

## Project

- Name: CAD Work Assistant
- Product type: Professional desktop CAD automation tool
- Stack: WPF, .NET, AutoCAD automation
- Density: Dense
- Color mode: Light, token-ready for future dark mode

## Sources

- Tokens: `src/CADWorkAssistant.Desktop/Themes/DesignTokens.xaml`
- Primary UI: `src/CADWorkAssistant.Desktop/MainWindow.xaml`
- Design system: `design-system/MASTER.md`

## Preferred Patterns

- Sidebar navigation grouped by project, CAD, quantity, output, and settings.
- Workspace-first layout with a persistent status bar.
- DataGrid for dense quantity records.
- Property inspector for selection and calculation settings.
- Command palette for keyboard-first repeated work.
- Activity log for loading, warning, success, and error feedback.

## Constraints

### Must

- Use native WPF controls and MVVM-style bindings.
- Preserve keyboard access, visible focus states, and automation names.
- Keep table rows compact and numeric values aligned with a monospace font.
- Use native motion only when it clarifies state changes and respect reduced motion.

### Avoid

- Marketing hero sections.
- Purple AI gradients, neon glow, decorative glass, and oversized cards.
- React-only packages for the desktop app.
- Color-only status communication.
