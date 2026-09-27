# Scope secret credential hydration to the active desktop session

The local bridge returns only the credential values needed by an authenticated desktop-mode Web canvas, and only for in-memory use. API keys, WebDAV passwords, Agent tokens, and bridge tokens must never be written to browser persistence, URLs, exports, logs, or ordinary configuration documents; WebDAV and Agent UI migration can follow after this security boundary is in place.
