# Apply configuration through a local bridge event

Applying saved desktop configuration is a distinct action from saving and from restarting the Vite service. The authenticated local bridge signals the active Web canvas to re-read the saved configuration in memory, so applying changes does not require a page reload or interrupt the current canvas view.

The event is delivered over an authenticated loopback SSE stream. A request already in progress keeps the configuration snapshot captured at its start; the newly applied configuration affects subsequent requests only.
