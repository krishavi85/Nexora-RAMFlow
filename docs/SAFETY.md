# Nexora RAMFlow safety design

RAMFlow modifies a protected Windows setting. The application therefore follows a conservative safety model.

## What RAMFlow changes

RAMFlow can enable Windows automatic page-file management or set an initial and maximum size for the page file on the Windows system drive.

It does not create physical RAM, modify BIOS settings, overclock hardware, add GPU VRAM, or promise RAM-level SSD performance.

## Guardrails

1. The application starts with normal user privileges.
2. Reading diagnostics does not require elevation.
3. Configuration changes require an elevated process.
4. Every change requires a confirmation dialog.
5. The current page-file configuration is written to a timestamped JSON backup first.
6. Custom maximum sizes are capped at 64 GB in the MVP.
7. A requested profile is rejected if it would leave less than 10 GB free on the Windows drive.
8. The application does not terminate processes or disable Windows services.
9. The interface clearly states when a restart is required.

## Backup location

Backups are stored under:

```text
%LOCALAPPDATA%\Nexora\RAMFlow\Backups
```

The MVP creates backups for diagnosis and manual recovery. A guided restore workflow is planned for a future release.

## Operational advice

- Keep Windows system-managed paging unless you have a clear reason to use a custom profile.
- Do not disable the page file merely because physical RAM appears sufficient.
- Keep adequate free disk space.
- Save work before applying changes and restarting Windows.
- Treat repeated extreme paging as a signal that more physical RAM or a lighter workload may be necessary.

## Development rule

Any future code that automatically closes programs, disables services, deletes backups, or changes page-file settings without explicit confirmation should be rejected during review.
