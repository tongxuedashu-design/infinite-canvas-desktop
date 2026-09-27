# Stabilize the configuration contract before building interfaces

The first implementation slice defines the versioned bridge configuration contract, credential boundaries, migration behavior, and application events before expanding WPF or Web interfaces. WPF must preserve advanced fields it does not edit, so ordinary desktop edits cannot erase Web-only configuration such as model scripts.

