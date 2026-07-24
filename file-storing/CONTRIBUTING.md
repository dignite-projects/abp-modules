# Contributing

## Development

Build the .NET solution from the repository root:

```bash
dotnet build Dignite.Abp.FileStoring.slnx
```

Build the Angular file explorer package from `angular/`:

```bash
npm install --legacy-peer-deps
npm run build:lib
```

## Versioning

The package version is defined in `Directory.Build.props` and should match the Angular package version in `angular/projects/file-explorer/package.json` when publishing coordinated releases.

This repository targets the ABP Framework major version in its own major version number.
