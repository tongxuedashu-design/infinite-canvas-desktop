# Reject stale desktop configuration writes

Desktop configuration reads and writes carry a revision number. A save based on an older revision is rejected with a reload-and-review message rather than allowing the WPF manager or Web canvas to silently overwrite a concurrent change.
