# Browser configuration migration requires confirmation

When desktop-mode configuration is empty but the existing browser profile contains canvas configuration, the desktop manager offers a one-time import and requires user confirmation before becoming the configuration authority. It must not silently overwrite either side, because existing channels, model selections, and credentials may represent working user configuration that cannot be reconstructed automatically.

The migration prompt is initiated by the desktop-mode Web canvas, which can read its own browser-local state; the WPF manager does not parse Edge storage internals.
