# Contributing

## Development workflow

1. Create a focused branch from `main`.
2. Keep Windows-specific operations inside `Nexora.RAMFlow.App`.
3. Keep recommendation rules testable inside `Nexora.RAMFlow.Core`.
4. Add or update tests for recommendation changes.
5. Run `dotnet build Nexora.RAMFlow.sln -c Release` and `dotnet test Nexora.RAMFlow.sln -c Release`.
6. Open a pull request describing the user impact and safety implications.

## Safety-sensitive changes

Changes to `PageFileManager` require particular care. Pull requests must explain:

- which Windows setting is changed;
- whether administrator privileges are required;
- how the previous configuration is backed up;
- how disk-space limits are enforced;
- whether a restart is required.

Do not add misleading claims such as “download more RAM” or “turn storage into physical RAM.”
