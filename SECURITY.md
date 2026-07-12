# Security policy

## Reporting a vulnerability

Please open a private GitHub security advisory for vulnerabilities involving privilege elevation, unsafe page-file changes, path handling, or unintended data exposure. Avoid publishing exploit details in a public issue before a fix is available.

## Security boundaries

- RAMFlow does not request administrator rights at startup.
- Write operations are blocked unless the process is elevated.
- Configuration backups are stored only in the current user's local application-data folder.
- No telemetry or network transmission is implemented in the MVP.
