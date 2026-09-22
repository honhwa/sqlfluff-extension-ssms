# Contributing to SQLFluff for SSMS

Thanks for your interest! Here's how you can help.

## Reporting Bugs

- Use [GitHub Issues](../../issues)
- Describe what you expected vs. what happened
- Include your SSMS version, SQLFluff version, and OS
- Paste error messages from the SQLFluff output pane

## Suggesting Features

Open an issue with:
- Use case: what you're trying to do
- Why it matters
- Any workarounds you've found

## Code Changes

1. Fork and clone the repo
2. Create a feature branch: `git checkout -b feature/my-feature`
3. Make your changes (follow the existing code style)
4. Test locally: build and install the VSIX
5. Commit with a clear message
6. Push and open a pull request

### Build Locally

```bash
cd src\SqlFluff.Ssms
dotnet restore
msbuild SqlFluff.Ssms.csproj /p:Configuration=Release
```

VSIX output: `bin\Release\SqlFluff.Ssms.vsix`

## Code Style

- C# 9+ features OK (.NET 4.8 base class library)
- No external dependencies beyond VS SDK and SQLFluff
- Keep it minimal — one fix per PR when possible
- Comments only for the "why", not the "what"

## Pull Request Process

1. Keep PRs focused (one feature or fix per PR)
2. Update README if user-facing changes
3. Reference any related issues
4. One of the maintainers will review and merge

## Questions?

Open an issue or ask in a pull request. All contributors are welcome.
