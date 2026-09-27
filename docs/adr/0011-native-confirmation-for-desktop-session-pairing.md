# Require native confirmation for desktop session pairing

Desktop-mode canvas sessions establish bridge authorization through pairing explicitly approved in the native desktop manager, so neither persistent nor one-time authorization credentials need to travel in URLs or browser persistence. First opening, opening a new session, and refreshing the canvas require pairing again; activating the same live canvas session does not. This deliberately accepts an extra native confirmation instead of retaining URL/sessionStorage tokens or assuming that loopback reachability and an allowed Origin authenticate a page.

## Consequences

The canvas and manager show a matching non-authorizing pairing identifier; approval is an internal native operation bound to that pending request, never an unauthenticated browser-callable approval endpoint. The approved session credential is delivered only to the corresponding in-memory client challenge, claimed once, and held only in memory; manager exit invalidates sessions, and failed authentication never falls back to browser-local configuration authority.
