# ConcordIO.AsyncApi.Server — Architecture

This document explains the internal design of the Server MSBuild task package. For usage instructions, see [README.md](README.md).

## High-Level Overview

ConcordIO.AsyncApi.Server is a NuGet tool package that runs after build in producer projects. It uses reflection to discover message types from the compiled assembly, generates an AsyncAPI 3.x document, and optionally packages it into the NuGet output.

```
┌──────────────────────────────────────────────────────────────┐
│  Producer Project Build                                      │
│                                                              │
│  Build (compile the assembly)                                │
│     ▼                                                        │
│  ConcordIOGenerateAsyncApi (AfterTargets=Build)              │
│     │                                                        │
│     ├─ Parse ConcordIOEventTypes / ConcordIOCommandTypes     │
│     │  into MessageTypePattern[] with Kind metadata          │
│     │                                                        │
│     ├─ GenerateAsyncApiTask.Execute()                        │
│     │  ├─ Assembly.LoadFrom(TargetPath)                      │
│     │  ├─ TypeDiscoveryService.DiscoverTypes()               │
│     │  ├─ AsyncApiDocumentGenerator.Generate()               │
│     │  └─ AsyncApiDocumentWriter.WriteYaml/JsonAsync()       │
│     │                                                        │
│     └─ Output: _ConcordIOGeneratedFile                       │
│     ▼                                                        │
│  ConcordIOGenerateContractTargets                            │
│     │  Generates a .targets file for consumers               │
│     ▼                                                        │
│  ConcordIOIncludeAsyncApiInPackage (BeforeTargets=GenerateNuspec) │
│     │  Adds spec + targets to NuGet package                  │
└──────────────────────────────────────────────────────────────┘
```

## Package Structure

```
ConcordIO.AsyncApi.Server.nupkg
├── build/
│   ├── ConcordIO.AsyncApi.Server.props    # Default MSBuild properties
│   └── ConcordIO.AsyncApi.Server.targets  # Task registration and build targets
├── buildTransitive/
│   └── ConcordIO.AsyncApi.Server.props    # Imports build/props for transitive consumers
└── tools/
    └── net10.0/
        ├── ConcordIO.AsyncApi.Server.dll  # MSBuild task assembly
        ├── ConcordIO.AsyncApi.dll         # Core library (PrivateAssets=all)
        └── (NJsonSchema, Neuroglia, etc.) # Dependencies bundled as tools
```

## Key Components

### GenerateAsyncApiTask (MSBuild Task)

Entry point invoked by MSBuild. Located in `Tasks/GenerateAsyncApiTask.cs`.

**Input properties:**

| Property | Required | Description |
|----------|----------|-------------|
| `AssemblyPath` | Yes | Path to the compiled assembly (`$(TargetPath)`) |
| `MessageTypePatterns` | No | `ITaskItem[]` with `Kind` metadata (Event/Command) |
| `DocumentTitle` | No | AsyncAPI document title (defaults to assembly name) |
| `DocumentVersion` | No | AsyncAPI document version (defaults to `1.0.0`) |
| `OutputPath` | No | Output file path |
| `OutputFormat` | No | `"yaml"` or `"json"` |

**Output properties:**

| Property | Description |
|----------|-------------|
| `GeneratedFile` | Path to the generated AsyncAPI spec file |

### Document Generation Pipeline

```
MSBuild properties
    │
    ▼
ConcordIOGenerateAsyncApi target
    │  Parse semicolon-separated ConcordIOEventTypes/ConcordIOCommandTypes
    │  into <_ConcordIOAllMessageTypes> items with Kind metadata
    │
    ▼
GenerateAsyncApiTask.Execute()
    │
    ├─ Set up AssemblyResolve handler for dependency resolution
    │
    ├─ Assembly.LoadFrom(AssemblyPath)
    │
    ├─ ParsePatterns()
    │  Convert ITaskItem[] → List<MessageTypePattern>
    │  Each item: pattern string + Kind (Event/Command)
    │
    ├─ TypeDiscoveryService.DiscoverTypes(assembly, patterns)
    │  │  Per pattern:
    │  │  ├─ *.** → recursive namespace wildcard
    │  │  ├─ .*  → exact namespace wildcard
    │  │  ├─ IsInterface → find all implementations
    │  │  ├─ IsAbstract/HasSubclasses → find all subclasses
    │  │  └─ Concrete → just that type
    │  └─ Returns: DiscoveredType[] (Type + MessageKind)
    │
    ├─ AsyncApiDocumentGenerator.Generate(title, version, types)
    │  │  1. CollectTypeAndDependencies() — walk properties recursively
    │  │  2. GenerateSchema() per type — NJsonSchema + x-dotnet-* extensions
    │  │  3. Build channels (MassTransit URN address)
    │  │  4. Build messages ($ref to schemas)
    │  │  5. Build operations (receive for events, send for commands)
    │  └─ Returns: V3AsyncApiDocument
    │
    └─ AsyncApiDocumentWriter.WriteYaml/JsonAsync(document, outputPath)
```

### MSBuild Integration

**Props** (`build/ConcordIO.AsyncApi.Server.props`):
- Sets defaults for `ConcordIOAsyncApiDocumentVersion` (falls back to `$(Version)` then `1.0.0`)
- Sets defaults for `ConcordIOAsyncApiOutputFormat` (`yaml`) and `ConcordIOIncludeAsyncApiInPackage` (`true`)
- Does NOT set `OutputPath` or `DocumentTitle` — these depend on `IntermediateOutputPath` and `AssemblyName` which aren't available at props evaluation time

**Targets** (`build/ConcordIO.AsyncApi.Server.targets`):

Three targets form the pipeline:

1. **`ConcordIOGenerateAsyncApi`** (`AfterTargets="Build"`)
   - Converts `ConcordIOEventTypes`/`ConcordIOCommandTypes` semicolon-separated properties into `_ConcordIOAllMessageTypes` items with `Kind` metadata
   - Computes output path at target time
   - Runs `GenerateAsyncApiTask`
   - Tracks generated file in `FileWrites` and `ConcordIOGeneratedAsyncApi` items

2. **`ConcordIOGenerateContractTargets`** (`AfterTargets="ConcordIOGenerateAsyncApi"`)
   - Auto-generates a consumer `.targets` file that exposes `ConcordIOAsyncApiContract` items
   - The `.targets` content uses `$(MSBuildThisFileDirectory)` for path resolution at consumer evaluation time
   - Other properties (`DocumentTitle`, `OutputExtension`, `Version`) are baked in at generation time

3. **`ConcordIOIncludeAsyncApiInPackage`** (`BeforeTargets="GenerateNuspec"`)
   - Includes the generated spec in the NuGet package under `asyncapi/`
   - Includes the auto-generated consumer `.targets` file under `build/{PackageId}.targets`

**Transitive** (`buildTransitive/ConcordIO.AsyncApi.Server.props`):
- Imports the main props file for projects that transitively reference this package

### Assembly Resolution

The task sets up an `AppDomain.CurrentDomain.AssemblyResolve` handler to resolve type dependencies from the assembly's output directory. This is necessary because the consumer's contract types may reference types from other assemblies (e.g., shared DTOs), and the task assembly runs from the NuGet package's `tools/` folder.

### Generated Consumer Targets

The auto-generated `.targets` file for consumers:

```xml
<Project>
  <ItemGroup>
    <ConcordIOAsyncApiContract Include="$(MSBuildThisFileDirectory)..\asyncapi\{Title}{Extension}">
      <PackageId>{PackageId}</PackageId>
      <Version>{Version}</Version>
    </ConcordIOAsyncApiContract>
  </ItemGroup>
</Project>
```

- `$(MSBuildThisFileDirectory)` resolves at consumer evaluation time (relative to the installed package)
- `{Title}`, `{Extension}`, `{PackageId}`, `{Version}` are baked in at generation time from the producer's build context
