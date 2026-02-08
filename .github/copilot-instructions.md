# Copilot Instructions for ConcordIO

## Project Overview

ConcordIO is a .NET CLI tool and NuGet-based contract management toolchain. It generates NuGet package scaffolds from OpenAPI, Protocol Buffer, and AsyncAPI specifications — with automatic MSBuild integration for client code generation and breaking-change detection.

## Key Documentation — Keep Updated

When making changes to the codebase, **always update the relevant documentation files** listed below. These files must stay in sync with the code at all times.

### `src/ConcordIO.Tool/README.md`
- **Purpose**: User-facing documentation — installation, CLI commands, options, examples.
- **Update when**: Adding/removing/renaming CLI commands or options, changing default values, changing exit codes, changing supported spec kinds, modifying prerequisites or platform support.

### `src/ConcordIO.Tool/ARCHITECTURE.md`
- **Purpose**: Internal architecture documentation — code structure, component design, data flow, template model, generated package layout.
- **Update when**: Adding/removing/renaming services or interfaces, changing the template rendering pipeline, modifying the template model schema, changing the generated package structure, adding new spec kinds, modifying the oasdiff integration, restructuring folders or namespaces.

### `src/ConcordIO.AsyncApi/README.md`
- **Purpose**: Library overview — key types, dependencies, feature summary.
- **Update when**: Adding/removing/renaming public types or namespaces, changing dependencies, adding new features to the shared library.

### `src/ConcordIO.AsyncApi/ARCHITECTURE.md`
- **Purpose**: Internal architecture — component design, data flow, namespace handling, server/client pipelines.
- **Update when**: Adding/removing/renaming services or classes, changing the schema generation pipeline, modifying the code generation pipeline, changing how namespaces or extensions are handled, restructuring the Server/ or Client/ folders.

### `src/ConcordIO.AsyncApi.Client/README.md`
- **Purpose**: User-facing documentation — installation, MSBuild properties, generated output format, external type detection.
- **Update when**: Adding/removing/renaming MSBuild properties, changing defaults, changing the generated code format, modifying the external type resolution behavior, changing MSBuild target names or ordering.

### `src/ConcordIO.AsyncApi.Client/ARCHITECTURE.md`
- **Purpose**: Internal architecture — MSBuild task design, code generation pipeline, package structure, assembly resolution.
- **Update when**: Changing the GenerateContractsTask, modifying the code generation flow, changing how external types are resolved, modifying the MSBuild targets/props files, changing the NuGet package layout.

### `src/ConcordIO.AsyncApi.Server/README.md`
- **Purpose**: User-facing documentation — installation, type discovery patterns, MSBuild properties, generated AsyncAPI format, NuGet packaging.
- **Update when**: Adding/removing/renaming MSBuild properties, changing defaults, changing the generated AsyncAPI document structure, modifying type discovery patterns, changing MSBuild target names or ordering.

### `src/ConcordIO.AsyncApi.Server/ARCHITECTURE.md`
- **Purpose**: Internal architecture — MSBuild task design, document generation pipeline, type discovery, package structure, consumer targets generation.
- **Update when**: Changing the GenerateAsyncApiTask, modifying type discovery logic, changing the document generation flow, modifying the MSBuild targets/props files, changing the NuGet package layout, changing the auto-generated consumer targets.

### `README.md` (repo root)
- **Purpose**: High-level project overview and vision.
- **Update when**: The project scope, supported spec types, or major capabilities change.

## Project Structure

- `src/ConcordIO.Tool/` — The CLI tool (entry point, commands, services, templates).
- `src/ConcordIO.AsyncApi/` — AsyncAPI document parsing and code generation library.
- `src/ConcordIO.AsyncApi.Client/` — MSBuild task package for AsyncAPI client generation at build time.
- `src/ConcordIO.AsyncApi.Server/` — MSBuild task package for AsyncAPI server-side document generation.
- `src/ConcordIO.AsyncApi.Tests/` — Tests for the AsyncAPI libraries.
- `src/ConcordIO.Tool.Tests/` — Tests for the CLI tool.

## Tech Stack

- **.NET 10** with C# latest
- **DotMake.CommandLine** for CLI parsing (commands are nested classes inside a partial `RootCommand`)
- **Scriban** for template rendering (templates are embedded assembly resources)
- **oasdiff** (bundled native binaries) for OpenAPI breaking-change detection
- **NuGet CLI** (external dependency) for package download in `breaking` / `get-spec` commands
- **NJsonSchema** for JSON Schema generation (server) and C# code generation (client)
- **Neuroglia.AsyncApi** for AsyncAPI 3.x document model and serialization
- **xUnit** + **Verify** for testing

## Conventions

- CLI commands live in `CliCommands/` as nested classes of `RootCommand`.
- Service interfaces live in `Services/` (e.g., `IFileSystem`, `ITemplateRenderer`, `INuGetService`, `IOasDiffRunner`).
- Templates are Scriban files in `Templates/` subdirectories, embedded as assembly resources.
- Template resource names follow the pattern `ConcordIO.Tool.Templates.{Folder}.{FileName}` (dots as separators).
- Spec kinds are string constants: `"openapi"`, `"proto"`, `"asyncapi"`.
- AsyncAPI extension keys: `x-dotnet-namespace`, `x-dotnet-type`.
- MSBuild task packages use `build/` for props/targets, `buildTransitive/` for transitive imports, and `tools/` for task assemblies.

## Testing

- Run all tests: `dotnet test src/ConcordIO.Tool.sln`
- Tool tests are in `src/ConcordIO.Tool.Tests/` (unit, integration, E2E).
- AsyncAPI tests are in `src/ConcordIO.AsyncApi.Tests/`.
- Use `IFileSystem`, `ITemplateRenderer`, and other interfaces for mocking in unit tests.

## Build & Run

- Build: `dotnet build src/ConcordIO.Tool.sln`
- Run the tool locally: `dotnet run --project src/ConcordIO.Tool -- <command> [options]`
- Pack as tool: `dotnet pack src/ConcordIO.Tool/ConcordIO.Tool.csproj`
- Pack AsyncAPI packages: `dotnet pack src/ConcordIO.AsyncApi.Client/ConcordIO.AsyncApi.Client.csproj` and `dotnet pack src/ConcordIO.AsyncApi.Server/ConcordIO.AsyncApi.Server.csproj`
