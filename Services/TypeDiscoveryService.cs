using Microsoft.Extensions.Logging;
using System.Reflection;
using TypeGen.Attributes;

namespace TypeGen.Services
{
    /// <summary>
    /// Service for discovering types marked with [ExportToTs] attribute and collecting all available types for inline generation
    /// </summary>
    public interface ITypeDiscoveryService
    {
        /// <summary>
        /// Discover all types marked with [ExportToTs] in the specified assemblies
        /// </summary>
        Task<List<Type>> DiscoverTypesToExportAsync(List<string> assemblyPaths);

        /// <summary>
        /// Get all types from the specified assemblies (for inline generation support)
        /// </summary>
        Task<Dictionary<string, Type>> GetAllAvailableTypesAsync(List<string> assemblyPaths);
    }

    /// <summary>
    /// Optimized implementation of type discovery service with caching and error resilience
    /// </summary>
    public class TypeDiscoveryService : ITypeDiscoveryService
    {
        private readonly ILogger<TypeDiscoveryService> _logger;
        private readonly Dictionary<string, Assembly> _assemblyCache = new();
        private readonly Dictionary<string, Type[]> _typesCache = new();

        public TypeDiscoveryService(ILogger<TypeDiscoveryService> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<List<Type>> DiscoverTypesToExportAsync(List<string> assemblyPaths)
        {
            var discoveredTypes = new List<Type>();

            var assemblies = await LoadAssembliesAsync(assemblyPaths);
            
            foreach (var (assemblyPath, assembly) in assemblies)
            {
                try
                {
                    var types = GetTypesFromAssembly(assembly, assemblyPath);
                    var exportedTypes = types.Where(HasExportToTsAttribute).ToList();
                    
                    discoveredTypes.AddRange(exportedTypes);
                    
                    _logger.LogInformation("Found {TypesCount} types with [ExportToTs] in {AssemblyName}", 
                        exportedTypes.Count, Path.GetFileNameWithoutExtension(assemblyPath));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to scan assembly {AssemblyPath}: {Error}", assemblyPath, ex.Message);
                    throw new InvalidOperationException($"Type discovery failed for assembly: {assemblyPath}", ex);
                }
            }

            _logger.LogInformation("Total discovered types: {Count} (Classes: {Classes}, Enums: {Enums})", 
                discoveredTypes.Count,
                discoveredTypes.Count(t => t.IsClass),
                discoveredTypes.Count(t => t.IsEnum));

            return discoveredTypes;
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, Type>> GetAllAvailableTypesAsync(List<string> assemblyPaths)
        {
            var allTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

            var assemblies = await LoadAssembliesAsync(assemblyPaths);

            foreach (var (assemblyPath, assembly) in assemblies)
            {
                try
                {
                    var types = GetTypesFromAssembly(assembly, assemblyPath);
                    var validTypes = types.Where(IsValidForInlineGeneration).ToList();

                    foreach (var type in validTypes)
                    {
                        // Store by both full name and simple name with case-insensitive lookup
                        if (!string.IsNullOrEmpty(type.FullName))
                        {
                            allTypes[type.FullName] = type;
                        }
                        allTypes[type.Name] = type;
                    }

                    _logger.LogDebug("Collected {ValidCount} valid types from {AssemblyName}", 
                        validTypes.Count, Path.GetFileNameWithoutExtension(assemblyPath));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to collect types from assembly {AssemblyPath}", assemblyPath);
                    throw;
                }
            }

            _logger.LogInformation("Collected {Count} types total for inline generation", allTypes.Count / 2); // Divided by 2 because we store each type twice
            return allTypes;
        }

        /// <summary>
        /// Load assemblies with caching and parallel processing
        /// </summary>
        private async Task<List<(string Path, Assembly Assembly)>> LoadAssembliesAsync(List<string> assemblyPaths)
        {
            var loadTasks = assemblyPaths.Select(async path =>
            {
                var resolvedPath = ResolveAssemblyPath(path);
                
                if (_assemblyCache.TryGetValue(resolvedPath, out var cachedAssembly))
                {
                    return (resolvedPath, cachedAssembly);
                }

                var assembly = await Task.Run(() => LoadAssemblyFromPath(resolvedPath));
                _assemblyCache[resolvedPath] = assembly;
                
                return (resolvedPath, assembly);
            });

            return (await Task.WhenAll(loadTasks)).ToList();
        }

        /// <summary>
        /// Get types from assembly with caching and error handling
        /// </summary>
        private Type[] GetTypesFromAssembly(Assembly assembly, string assemblyPath)
        {
            if (_typesCache.TryGetValue(assemblyPath, out var cachedTypes))
            {
                return cachedTypes;
            }

            try
            {
                var types = assembly.GetTypes();
                _typesCache[assemblyPath] = types;
                return types;
            }
            catch (ReflectionTypeLoadException ex)
            {
                _logger.LogWarning("Partial type loading from {AssemblyName}: {LoaderExceptions}",
                    assembly.GetName().Name,
                    string.Join(", ", ex.LoaderExceptions?.Select(e => e?.GetType().Name) ?? Array.Empty<string>()));

                var loadedTypes = ex.Types.Where(t => t != null).Cast<Type>().ToArray();
                _typesCache[assemblyPath] = loadedTypes;
                return loadedTypes;
            }
        }

        /// <summary>
        /// Resolve assembly path with multiple fallback strategies
        /// </summary>
        private string ResolveAssemblyPath(string assemblyPath)
        {
            // Return absolute paths as-is
            if (Path.IsPathRooted(assemblyPath) && File.Exists(assemblyPath))
                return assemblyPath;

            // Try relative to current directory
            var currentDirPath = Path.GetFullPath(assemblyPath);
            if (File.Exists(currentDirPath))
                return currentDirPath;

            // Try relative to TypeGen directory
            var typeGenDir = AppDomain.CurrentDomain.BaseDirectory;
            var typeGenRelativePath = Path.Combine(typeGenDir, assemblyPath);
            if (File.Exists(typeGenRelativePath))
                return typeGenRelativePath;

            // Try going up to solution directory and resolving from there
            var solutionDir = FindSolutionDirectory();
            if (!string.IsNullOrEmpty(solutionDir))
            {
                var solutionRelativePath = Path.Combine(solutionDir, assemblyPath);
                if (File.Exists(solutionRelativePath))
                    return solutionRelativePath;
            }

            throw new FileNotFoundException($"Assembly not found: {assemblyPath}. Searched in current directory, TypeGen directory, and solution directory.");
        }

        /// <summary>
        /// Find solution directory by looking for .sln files
        /// </summary>
        private string? FindSolutionDirectory()
        {
            var currentDir = Directory.GetCurrentDirectory();
            var parentDir = currentDir;

            // Look up the directory tree for a .sln file
            for (int i = 0; i < 10 && parentDir != null; i++)
            {
                if (Directory.GetFiles(parentDir, "*.sln").Any())
                {
                    return parentDir;
                }
                parentDir = Directory.GetParent(parentDir)?.FullName;
            }

            return null;
        }

        /// <summary>
        /// Load assembly with multiple strategies and proper error handling
        /// </summary>
        private Assembly LoadAssemblyFromPath(string assemblyPath)
        {
            try
            {
                return Assembly.LoadFrom(assemblyPath);
            }
            catch (BadImageFormatException)
            {
                // Try loading by name as fallback
                var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
                return Assembly.Load(assemblyName);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load assembly from {assemblyPath}", ex);
            }
        }

        /// <summary>
        /// Check if type is valid for inline generation (optimized)
        /// </summary>
        private static bool IsValidForInlineGeneration(Type type) =>
            type.IsPublic &&
            (type.IsClass || type.IsEnum) &&
            !type.IsAbstract &&
            !type.IsGenericTypeDefinition &&
            !string.IsNullOrEmpty(type.FullName);

        /// <summary>
        /// Check if type has ExportToTs attribute (cached)
        /// </summary>
        private static bool HasExportToTsAttribute(Type type) =>
            type.GetCustomAttribute<ExportToTsAttribute>() != null;
    }
}