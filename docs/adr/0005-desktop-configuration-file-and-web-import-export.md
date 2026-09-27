# Store non-secret desktop configuration separately from Web import and export

Desktop mode stores a versioned non-secret configuration document under the desktop data directory, while secrets remain in Windows Credential Manager. The existing Web configuration page remains the import/export surface, with desktop-mode export omitting all secrets; WPF provides a direct route to that surface instead of duplicating file format and validation logic.
