# wh7r4bb17.SharedRuntimes

Load centrally installed assemblies and access shared application files from multiple .NET applications without hard-coding installation paths throughout application code.

The library provides runtime-set configuration, compiler-reference resolution through MSBuild, and managed assembly fallback resolution at application startup.

## Requirements

- .NET 10 for applications using the loader.
- Windows for the conventional Common Files installation layout and native DLL loading.
- The configured shared assemblies must be available on the build machine and the target machine, or a build-time directory override must be configured.
- Shared libraries and their dependencies must be compatible with the consuming application and its operating system.

The loader's target framework and a runtime set's framework directory are separate settings. A .NET 10 application can select a set stored under `netstandard2.0`, for example.

## Concepts

A **runtime set** is an independently installed collection of assemblies and related files. Each set has:

| Setting | Purpose |
| --- | --- |
| Set ID | Logical key used to associate compiler references and application code with a set. |
| Company name | Company directory beneath Common Files. |
| Runtime name | Product or component directory name. |
| Runtime version | Installed version of the shared set. |
| Target framework | Exact framework directory containing the selected assets. |

The NuGet package version controls the loader package. `RuntimeVersion` controls the installed shared set. They are independent.

## Installation layout

The conventional directory structure is:

```text
<Common Files>\<CompanyName>\<RuntimeName>\<RuntimeVersion>\<TargetFramework>\
```

Example:

```text
C:\Program Files\Common Files\company\
├── Logging\
│   └── 1.0.8\
│       └── netstandard2.0\
│           ├── wh7r4bb17.Logging.dll
│           ├── Data\
│           │   └── defaults.json
│           └── runtimes\
│               └── win-x64\
│                   └── native\
│                       └── NativeDependency.dll
└── Reporting\
    └── 2.0.0\
        └── net10.0\
            ├── Reporting.Core.dll
            └── Templates\
                └── report.xml
```

The library resolves the Common Files root from the environment rather than assuming a particular drive letter. Install shared files using a separate deployment process or installer. The loader does not install or update runtime sets.

Keep installed sets read-only for ordinary users. Install new set versions alongside existing versions and keep versions required by installed applications available.

## Add the NuGet package

Configure your NuGet source and install the package:

```powershell
dotnet add package wh7r4bb17.SharedRuntimes
```

Alternatively, use **Manage NuGet Packages** in Visual Studio.

The package contains the loader and its MSBuild integration. Configure runtime sets in the executable application project that calls `InitializeFor`.

## Configure one runtime set

```xml
<PropertyGroup>
  <TargetFramework>net10.0-windows</TargetFramework>
  <UseCompanySharedRuntime>true</UseCompanySharedRuntime>
  <CompanySharedCompanyName>company</CompanySharedCompanyName>
</PropertyGroup>

<ItemGroup>
  <CompanySharedRuntimeSet Include="Logging">
    <RuntimeName>Logging</RuntimeName>
    <RuntimeVersion>1.0.8</RuntimeVersion>
    <TargetFramework>netstandard2.0</TargetFramework>
  </CompanySharedRuntimeSet>

  <CompanySharedReference Include="wh7r4bb17.Logging" />
</ItemGroup>
```

The assembly is resolved from:

```text
<Common Files>\company\Logging\1.0.8\netstandard2.0\wh7r4bb17.Logging.dll
```

Specify the assembly name without `.dll`. Each reference name can be declared only once. When exactly one set is configured, references without `Set` metadata use that set.

`CompanySharedCompanyName` supplies the company default. A set can override it using `<CompanyName>AnotherCompany</CompanyName>`. If no company is configured, the default is `company`.

`RuntimeName` defaults to the set ID when omitted. `RuntimeVersion` is required.

Company name, runtime name, runtime version, target framework, and reference names are used as path segments. They must not be empty, contain path separators or other characters that are invalid in Windows file names, end with a period or space, or use a reserved device name such as `CON` or `NUL`.

### Framework selection

Set `<TargetFramework>` to select an exact framework directory:

```xml
<TargetFramework>netstandard2.0</TargetFramework>
```

If omitted, the consuming project's evaluated `$(TargetFramework)` is used, including platform suffixes such as `net10.0-windows`.

The following framework directories are supported:

| Family | Accepted values |
| --- | --- |
| .NET Standard | `netstandard2.0`, `netstandard2.1` |
| .NET | `net8.0`, `net9.0`, `net10.0`, `net11.0` |
| .NET with platform | Any supported .NET value followed by a platform suffix, for example `net10.0-windows` or `net10.0-windows10.0.19041.0`. |

Any other value is rejected during the build, for example `netstandard`, `net7.0`, or `net10.0-`.

There is no automatic search for a nearest compatible directory. Specify a complete framework name such as `netstandard2.0`, not `netstandard`. The directory name selects installed assets; it does not change the framework compatibility of their DLLs.

The build writes the selected configuration into application assembly metadata. `InitializeFor` reads that configuration at startup.

## Configure multiple sets

```xml
<PropertyGroup>
  <UseCompanySharedRuntime>true</UseCompanySharedRuntime>
  <CompanySharedCompanyName>company</CompanySharedCompanyName>
</PropertyGroup>

<ItemGroup>
  <CompanySharedRuntimeSet Include="Logging">
    <RuntimeName>Logging</RuntimeName>
    <RuntimeVersion>1.0.8</RuntimeVersion>
    <TargetFramework>netstandard2.0</TargetFramework>
  </CompanySharedRuntimeSet>

  <CompanySharedRuntimeSet Include="Reporting">
    <RuntimeName>Reporting</RuntimeName>
    <RuntimeVersion>2.0.0</RuntimeVersion>
    <TargetFramework>net10.0</TargetFramework>
  </CompanySharedRuntimeSet>

  <CompanySharedReference Include="wh7r4bb17.Logging">
    <Set>Logging</Set>
  </CompanySharedReference>

  <CompanySharedReference Include="Reporting.Core">
    <Set>Reporting</Set>
  </CompanySharedReference>
</ItemGroup>
```

When multiple sets are configured, each shared reference must specify its `Set`.

Set IDs are case-insensitive and must be unique. Use letters, digits, underscores, periods, or hyphens.

## Initialize the application

Register the sets before executing code that uses shared types. Keep bootstrap code separate so the runtime does not need to resolve shared types before the handlers have been registered.

### Console application

```csharp
using System.Reflection;
using System.Runtime.CompilerServices;
using Runtime = wh7r4bb17.SharedRuntimes.SharedRuntime;

internal static class Program
{
    private static void Main()
    {
        Runtime.InitializeFor(
            Assembly.GetExecutingAssembly(),
            message => Console.Error.WriteLine(message));

        RunApplication();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunApplication()
    {
        // Use types from the configured shared assemblies here.
    }
}
```

Pass the assembly containing the MSBuild-generated set metadata. In a normal executable this is the application assembly; in a hosted application it may be the add-in assembly.

`SharedRuntime` owns an application-wide registration. Initialize it once and keep the registration active for as long as shared code can execute. Registering a resolver does not eagerly load every DLL in its directory.

### WPF application

Use a custom entry point to register the sets before constructing `App` or initializing XAML.

```csharp
using System.Reflection;
using System.Runtime.CompilerServices;
using Runtime = wh7r4bb17.SharedRuntimes.SharedRuntime;

namespace MyApplication;

internal static class Bootstrap
{
    [STAThread]
    private static void Main()
    {
        Runtime.InitializeFor(Assembly.GetExecutingAssembly());
        RunApplication();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunApplication()
    {
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
```

Select this entry point in the project:

```xml
<PropertyGroup>
  <StartupObject>MyApplication.Bootstrap</StartupObject>
</PropertyGroup>
```

Keep shared type usage out of the bootstrap class's fields, signatures, and initialization code before registration.

## Access shared files

Use the set ID to resolve files beneath a runtime directory:

```csharp
using Runtime = wh7r4bb17.SharedRuntimes.SharedRuntime;

string configuration = Runtime.GetPathForSet(
    "Logging", "Data", "defaults.json");

string template = Runtime.GetRuntime("Reporting")
    .GetPath("Templates", "report.xml");
```

With exactly one active set, the shorter form is available:

```csharp
string configuration = Runtime.GetPath("Data", "defaults.json");
```

`Current` and the static `GetPath` require exactly one set. With multiple sets, select the set explicitly.

Path methods construct and validate paths; they do not create files or guarantee that a file exists. Rooted paths and paths escaping the set directory are rejected. Keep writable application data outside the shared installation directory.

## Additional search directories

Managed assemblies are searched in the set root by default. Add relative directories if needed:

```xml
<CompanySharedRuntimeSet Include="Logging">
  <RuntimeName>Logging</RuntimeName>
  <RuntimeVersion>1.0.8</RuntimeVersion>
  <TargetFramework>netstandard2.0</TargetFramework>
  <ManagedSubdirectories>.;lib</ManagedSubdirectories>
</CompanySharedRuntimeSet>
```

The list is semicolon-separated and ordered. Searching is not recursive.

Directories must be relative to the set root and must stay inside it. Backslashes and forward slashes are equivalent, trailing separators are ignored, and `lib`, `lib/`, and `./lib` refer to the same directory. Duplicate entries are removed while the first occurrence keeps its position. `.` refers to the set root itself.

For an explicitly referenced assembly in a subdirectory:

```xml
<CompanySharedReference Include="Example.Extension">
  <Set>Logging</Set>
  <Subdirectory>lib</Subdirectory>
</CompanySharedReference>
```

The reference subdirectory is also added to the set's managed search directories. Install transitive managed dependencies in configured search directories. Culture-specific resource assemblies are resolved beneath their culture directories.

## Native Windows dependencies

By default, native libraries are searched under:

```text
<RuntimeDirectory>\runtimes\win-<process architecture>\native\
```

For example, an x64 process uses `runtimes\win-x64\native`. This default directory depends on the process architecture and is not checked during the build.

Override the directories when necessary:

```xml
<NativeSubdirectories>native;runtimes/win-x64/native</NativeSubdirectories>
```

Native directories follow the same rules as managed search directories: they must be relative, stay inside the set root, and are normalized and de-duplicated. A list that contains no directory, such as `;`, is rejected.

Explicitly configured native directories must exist during regular builds. IDE design-time builds tolerate missing installed files.

By default, native fallback applies only to requesting assemblies located inside the set. The following option broadens the scope and should be used carefully in shared hosts:

```xml
<ResolveNativeForAnyAssembly>true</ResolveNativeForAnyAssembly>
```

Native libraries must match the process architecture and have loadable dependencies. Native handles are cached for the process lifetime; disposing a resolver does not unload those cached libraries.

## Configure sets directly in code

Use explicit options when metadata-based configuration is not appropriate:

```csharp
using wh7r4bb17.SharedRuntimes;
using Runtime = wh7r4bb17.SharedRuntimes.SharedRuntime;

Runtime.Initialize(new SharedRuntimeOptions
{
    CompanyName = "company",
    RuntimeName = "Logging",
    RuntimeVersion = "1.0.8",
    TargetFramework = "netstandard2.0"
});
```

For a custom absolute deployment location, set `RuntimeDirectory` instead of relying on the conventional layout.

Manual options default to `net10.0`; they do not infer the consuming project's framework. Set `TargetFramework` explicitly when using another framework directory.

For scoped registration or a custom load context, use `SharedRuntimeRegistration.Attach` and keep the returned registration alive while shared code is in use:

```csharp
using wh7r4bb17.SharedRuntimes;

using var registration = SharedRuntimeRegistration.Attach(
[
    new SharedRuntimeSet("Logging", new SharedRuntimeOptions
    {
        RuntimeVersion = "1.0.8",
        RuntimeName = "Logging",
        TargetFramework = "netstandard2.0"
    })
]);

// Run the complete application or scoped operation here.
// registration.GetRuntime("Logging") provides access to the resolver.
```

Scoped registrations do not initialize the static `SharedRuntime` facade.

## Build-time path overrides

If the build machine uses a different asset location, override a set's build directory:

```xml
<CompanySharedRuntimeSet Include="Logging">
  <RuntimeName>Logging</RuntimeName>
  <RuntimeVersion>1.0.8</RuntimeVersion>
  <TargetFramework>netstandard2.0</TargetFramework>
  <RuntimeDirectory>D:\BuildAssets\Logging\1.0.8\netstandard2.0</RuntimeDirectory>
</CompanySharedRuntimeSet>
```

The directory must be an absolute path. `CompanySharedCommonFilesDirectory` overrides the build-time Common Files root for conventional path construction. A global `CompanySharedRuntimeDirectory` override is available for a single set; with multiple sets, use per-set overrides.

These are build/staging overrides. Absolute developer paths are not embedded in the application's set metadata. `InitializeFor` uses the conventional deployment layout. If the deployed application uses a custom absolute root too, initialize with explicit runtime options matching that deployment.

## Assembly identity and conflicts

The resolver validates assembly name, culture, requested public-key token, and assembly version. Exact assembly versions are required by default for fallback candidates:

```xml
<RequireExactAssemblyVersion>true</RequireExactAssemblyVersion>
```

Set this to `false` to allow candidates with an equal or higher assembly version. This is an identity policy, not a guarantee of API compatibility. Both this option and `ResolveNativeForAnyAssembly` accept `true` or `false`; other values fail the build.

Registration checks configured managed directories for conflicting assembly identities. Byte-identical duplicates are permitted; differing DLLs with the same simple name and culture are rejected.

The resolver is a fallback, not a replacement for normal .NET resolution. It does not replace assemblies already loaded by the host. Different runtime directories do not provide assembly-version isolation in the same load context. Shared dependencies across sets must remain compatible.

## Build and deployment behavior

The MSBuild integration:

- Creates compiler references to explicitly declared shared assemblies.
- Marks shared references as non-private and filters configured set files from local-copy and publish output.
- Embeds set configuration for `InitializeFor`.
- Validates installed search directories, explicitly configured native directories, referenced assemblies, and managed conflicts during regular builds.
- Tolerates missing installed files during IDE design-time builds, so projects still load before the shared sets are installed.

Because set files are filtered from the application output, native libraries inside a set directory are not copied locally either. Install them together with the set.

Inspect the final application output as part of deployment validation, especially if additional packages or custom copy targets also provide the same dependencies.

The loader NuGet package does not contain your product DLLs. Deploy the required runtime sets separately, and install the appropriate .NET runtime for the application if using a framework-dependent deployment.

Trimming, NativeAOT, and ReadyToRun publishing are rejected by the integration. Disposing a registration detaches handlers but does not unload assemblies already loaded into the default context.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Runtime metadata is missing | Enable `UseCompanySharedRuntime`, configure a set, keep assembly-info generation enabled, and pass the correct assembly to `InitializeFor`. |
| A runtime directory is missing | Compare company, runtime name, set version, and selected framework with the installed directory layout. |
| Unsupported target framework | Use one of the supported framework values listed under framework selection, and specify the complete version. |
| A shared reference requires a set | Add `<Set>...</Set>` when multiple sets are configured. |
| A referenced assembly is missing during build | Install the build assets or configure a build-time directory override. |
| A search folder is missing | Create the directory in the installed set or correct `ManagedSubdirectories` and the reference `Subdirectory`. |
| A native search folder is missing | Create the directory or correct `NativeSubdirectories`. Remove the setting to use the default architecture-specific directory. |
| `NativeSubdirectories` contains no folder | Provide at least one relative directory, or remove the setting. |
| Managed assembly conflict | Align dependencies across sets; separate directories alone do not isolate incompatible assembly versions. |
| A native DLL cannot load | Check process architecture, native search directories, and the DLL's own dependencies. |
| `Current` or static `GetPath` reports multiple sets | Use `GetRuntime(setId)` or `GetPathForSet(setId, ...)`. |

Use the optional logging callback passed to `InitializeFor` or `SharedRuntimeOptions.Log` for runtime resolution diagnostics.
