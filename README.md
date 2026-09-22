# SQLFluff for SSMS

Integrates SQLFluff (the SQL linter and formatter) into SQL Server Management Studio 22.

## Features

- **Lint on save/open**: Diagnostics in Error List with squiggles in the editor
- **Auto-fix**: Format or fix violations with one keystroke  
- **Configurable**: Dialect, rules, exclusions, and triggers
- **T-SQL ready**: Ships with `tsql` as the default dialect

## Installation

### Prerequisites

- SQL Server Management Studio 22.x
- Python 3.7+ with SQLFluff:
  ```bash
  pip install sqlfluff
  ```

### Install

1. Download `SqlFluff.Ssms.vsix` from Releases
2. Double-click the file or run:
   ```bash
   VSIXInstaller.exe SqlFluff.Ssms.vsix
   ```
3. Restart SSMS

## Usage

**Tools > SQLFluff**:
- **Lint Document** — Check and show issues
- **Fix / Format** — Apply fixes to selection or document
- **Clear Diagnostics** — Remove diagnostics
- **Options** — Configure the extension

**Keyboard**:
- `Ctrl+K, Ctrl+Shift+L` — Lint
- `Ctrl+K, Ctrl+Shift+F` — Fix

**Right-click editor** for quick Lint/Fix.

## Configuration

**Tools > Options > SQLFluff**:

| Setting | Default |
|---------|---------|
| Executable | `sqlfluff` (auto-detect) |
| Dialect | `tsql` |
| Lint on open | ✓ |
| Lint on save | ✓ |
| Lint while typing | ✗ |

## Building

```bash
cd src\SqlFluff.Ssms
dotnet restore
msbuild SqlFluff.Ssms.csproj /p:Configuration=Release
```

Output: `bin\Release\SqlFluff.Ssms.vsix`

## License

MIT
