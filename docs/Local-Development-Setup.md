# Local Development Setup

## NuGet Configuration for Local Development

Due to compliance requirements, the repository does not include a `nuget.config` file. For local development, you need to configure NuGet package sources.

Copy the template file:

```powershell
copy nuget.config.template nuget.config
```

### Verifying Configuration

After setup, verify your NuGet sources:

```bash
dotnet nuget list source
```

You should see both `nuget.org` and `ORT` sources listed.
