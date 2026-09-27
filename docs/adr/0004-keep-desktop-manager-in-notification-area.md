# Keep the desktop manager in the notification area

The desktop entry starts the local Vite service and opens the Edge canvas by default. Closing the management window hides it to the Windows notification area instead of stopping the service; an explicit Exit action stops only host-owned processes. A second launch restores the existing manager, so closing a control surface does not unexpectedly interrupt active canvas work.

When the user asks to open the canvas while an existing desktop canvas is active, the manager should activate or reuse that window where the platform allows it instead of creating duplicate canvas sessions.
