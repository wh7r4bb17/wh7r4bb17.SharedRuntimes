# wh7r4bb17.SharedRuntimes

`wh7r4bb17.SharedRuntimes` loads centrally installed .NET assemblies and related files at runtime. Shared assemblies are not copied into the application output; they are loaded from a shared installation directory.

## Requirements

- .NET 10 or later
- Windows when using the default location under *Program Files\Common Files*
- The shared assemblies must be available on both the build machine and the target machine

## Install the package

```powershell
dotnet add package wh7r4bb17.SharedRuntimes
```

## Configure a runtime set

Add the following to the `.csproj` file of the **executable application**:

```xml
<PropertyGroup>
  <UseCompanySharedRuntime>true</UseCompanySharedRuntime>
  <CompanySharedCompanyName>company</CompanySharedCompanyName>
</PropertyGroup>

<ItemGroup>
  <CompanySharedRuntimeSet Include="MySet">
    <RuntimeName>MyRuntime</RuntimeName>
    <RuntimeVersion>1.0.0</RuntimeVersion>
    <TargetFramework>net10.0</TargetFramework>
  </CompanySharedRuntimeSet>

  <CompanySharedReference Include="MyCompany.MyLibrary" />
</ItemGroup>
```

The build resolves the reference from this path:

```text
<Common Files>\company\MyRuntime\1.0.0\net10.0\MyCompany.MyLibrary.dll
```

The DLL is not copied to the application output folder. It must therefore be installed on the target machine.

## Initialize the application

Register the runtime before using any type from the shared assembly:

```csharp
using System.Reflection;
using wh7r4bb17.SharedRuntimes;

SharedRuntime.InitializeFor(Assembly.GetExecutingAssembly());
```

To enable diagnostic output:

```csharp
SharedRuntime.InitializeFor(
    Assembly.GetExecutingAssembly(),
    message => Console.Error.WriteLine(message));
```

With multiple runtime sets:

```csharp
var runtime = SharedRuntime.GetRuntime("MySet");
string path = runtime.GetPath("Data", "config.json");
```

With exactly one runtime set:

```csharp
string path = SharedRuntime.GetPath("Data", "config.json");
```

## Multiple runtime sets

When more than one set is configured, every shared reference must specify its set:

```xml
<CompanySharedReference Include="MyCompany.MyLibrary">
  <Set>MySet</Set>
</CompanySharedReference>
```

## Troubleshooting

| Problem | Solution |
|---|---|
| `Runtime metadata is missing` | Verify that `UseCompanySharedRuntime` is enabled, a `CompanySharedRuntimeSet` is configured, and `InitializeFor` is called. |
| `Runtime set ... is missing` | Compare the company name, runtime name, version, and target framework with the installed directory layout. |
| Assembly cannot be found | Check the assembly name without `.dll`, the configured set, and the installation path. |
| Wrong framework | Set `<TargetFramework>` explicitly in the runtime set, for example `net10.0` or `netstandard2.0`. |
| Multiple sets without a mapping | Add `<Set>...</Set>` to every `CompanySharedReference`. |
| `Current` reports multiple sets | Use `GetRuntime("SetId")` or `GetPathForSet("SetId", ...)`. |
| Native DLL cannot be loaded | Check the process architecture, `runtimes\win-<arch>\native`, and the DLL dependencies. |
| Assembly conflict | Use compatible versions. Different DLLs with the same assembly name must not be placed in the same set. |

## Important limitations

- The NuGet package does not install runtime files. Deploy them using a separate installer or deployment process.
- The runtime version configured in a set is independent of the NuGet package version.
- Trimming, NativeAOT, and ReadyToRun are not supported.
- Disposing the registration does not unload assemblies that have already been loaded.