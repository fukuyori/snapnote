# Version Update Checklist

Update these files whenever the application version changes:

- `SnapNoteStudio.csproj`: update the `<Version>` property.
- `installer.iss`: update the `MyAppVersion` definition used by the installer metadata and filename.
- `README.md`: update the installer output filename in the build example.
- `README.ja.md`: update the installer output filename in the build example.
- `CHANGELOG.md`: add a dated section describing the release changes.

For version 4.1.1, all files above were updated.

## Verification

- Search tracked source and documentation for the previous version.
- Build the project without creating a release package.
- Create an installer, commit, tag, or push only when explicitly requested.
