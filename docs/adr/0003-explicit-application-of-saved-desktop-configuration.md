# Saved desktop configuration is applied explicitly

Saving desktop-mode configuration does not restart Vite or silently replace configuration in an already active canvas session. The desktop manager offers an explicit action to make the running canvas re-read the saved configuration, keeping service lifecycle controls separate from configuration changes and avoiding interruption of in-progress canvas work.
