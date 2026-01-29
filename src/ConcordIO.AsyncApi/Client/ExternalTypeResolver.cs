using System.Reflection;

namespace ConcordIO.AsyncApi.Client;

/// <summary>
/// Resolves types from external assemblies to determine if they should be generated
/// or referenced from existing assemblies.
/// </summary>
public class ExternalTypeResolver
{
    private readonly Dictionary<string, Type> _typeCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Assembly> _loadedAssemblies = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a new instance of the external type resolver.
    /// </summary>
    public ExternalTypeResolver()
    {
    }

    /// <summary>
    /// Creates a new instance of the external type resolver with pre-loaded assemblies.
    /// </summary>
    /// <param name="assemblies">Assemblies to scan for existing types.</param>
    public ExternalTypeResolver(IEnumerable<Assembly> assemblies)
    {
        foreach (var assembly in assemblies)
        {
            LoadAssembly(assembly);
        }
    }

    /// <summary>
    /// Loads assemblies from file paths.
    /// </summary>
    /// <param name="assemblyPaths">Paths to assembly files.</param>
    public void LoadAssemblies(IEnumerable<string> assemblyPaths)
    {
        foreach (var path in assemblyPaths)
        {
            try
            {
                if (File.Exists(path) && !_loadedAssemblies.ContainsKey(path))
                {
                    var assembly = Assembly.LoadFrom(path);
                    LoadAssembly(assembly);
                    _loadedAssemblies[path] = assembly;
                }
            }
            catch (Exception)
            {
                // Ignore assemblies that can't be loaded (native, etc.)
            }
        }
    }

    private void LoadAssembly(Assembly assembly)
    {
        try
        {
            foreach (var type in assembly.GetExportedTypes())
            {
                if (type.FullName is not null && !_typeCache.ContainsKey(type.FullName))
                {
                    _typeCache[type.FullName] = type;
                }
            }
        }
        catch (Exception)
        {
            // Ignore errors from reflection (missing dependencies, etc.)
        }
    }

    /// <summary>
    /// Checks if a type with the given full name exists in any loaded assembly.
    /// </summary>
    /// <param name="fullTypeName">The fully qualified type name (e.g., "MyService.Contracts.Customer").</param>
    /// <returns>True if the type exists, false otherwise.</returns>
    public bool TypeExists(string fullTypeName)
    {
        return _typeCache.ContainsKey(fullTypeName);
    }

    /// <summary>
    /// Gets the type with the given full name if it exists.
    /// </summary>
    /// <param name="fullTypeName">The fully qualified type name.</param>
    /// <returns>The type if found, null otherwise.</returns>
    public Type? GetType(string fullTypeName)
    {
        return _typeCache.TryGetValue(fullTypeName, out var type) ? type : null;
    }

    /// <summary>
    /// Gets information about an external type if it exists.
    /// </summary>
    /// <param name="fullTypeName">The fully qualified type name.</param>
    /// <returns>TypeInfo if external, null if not found.</returns>
    public TypeInfo? GetExternalTypeInfo(string fullTypeName)
    {
        if (_typeCache.TryGetValue(fullTypeName, out var type))
        {
            var typeName = type.Name;
            var ns = type.Namespace ?? string.Empty;
            var assemblyName = type.Assembly.GetName().Name;
            
            return new TypeInfo(typeName, ns, IsExternal: true, ExternalAssembly: assemblyName);
        }
        
        return null;
    }

    /// <summary>
    /// Gets all loaded type full names.
    /// </summary>
    public IEnumerable<string> GetLoadedTypeNames() => _typeCache.Keys;
}
