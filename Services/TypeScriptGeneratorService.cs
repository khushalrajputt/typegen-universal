using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using TypeGen.Attributes;
using TypeGen.Configuration;

namespace TypeGen.Services
{
    /// <summary>
    /// Service for generating TypeScript interfaces and enums from C# types
    /// </summary>
    public interface ITypeScriptGeneratorService
    {
        /// <summary>
        /// Generate TypeScript files for the discovered types
        /// </summary>
        Task GenerateTypeScriptFilesAsync(List<Type> types, TypeGenConfig config);
    }

    /// <summary>
    /// Optimized TypeScript generator service with universal inline generation
    /// </summary>
    public class TypeScriptGeneratorService : ITypeScriptGeneratorService
    {
        private readonly ILogger<TypeScriptGeneratorService> _logger;
        private readonly ITypeDiscoveryService _typeDiscovery;
        private Dictionary<string, Type> _availableTypes = new();

        public TypeScriptGeneratorService(ILogger<TypeScriptGeneratorService> logger, ITypeDiscoveryService typeDiscovery)
        {
            _logger = logger;
            _typeDiscovery = typeDiscovery;
        }

        /// <inheritdoc />
        public async Task GenerateTypeScriptFilesAsync(List<Type> types, TypeGenConfig config)
        {
            if (!types.Any())
            {
                _logger.LogInformation("No types to generate TypeScript for");
                return;
            }

            // Load available types for inline generation asynchronously
            _availableTypes = await _typeDiscovery.GetAllAvailableTypesAsync(config.AssembliesToScan);
            _logger.LogInformation("Loaded {Count} types for inline generation", _availableTypes.Count / 2);

            // Separate and process types
            var interfaces = types.Where(t => t.IsClass || t.IsInterface).ToList();
            var enums = types.Where(t => t.IsEnum).ToList();

            _logger.LogInformation("Generating TypeScript for {InterfaceCount} interfaces and {EnumCount} enums", 
                interfaces.Count, enums.Count);

            // Generate files in parallel for optimal performance
            await Task.WhenAll(
                GenerateInterfacesAsync(interfaces, config),
                GenerateEnumsAsync(enums, config)
            );

            // Generate index files if configured
            if (config.GenerateIndexFiles)
            {
                await GenerateIndexFilesAsync(interfaces, enums, config);
            }
        }

        /// <summary>
        /// Generate TypeScript interfaces
        /// </summary>
        private async Task GenerateInterfacesAsync(List<Type> types, TypeGenConfig config)
        {
            if (!types.Any()) return;

            _logger.LogInformation("Generating {Count} TypeScript interfaces", types.Count);
            Directory.CreateDirectory(config.TypeScriptInterfacesOutputPath);

            var tasks = types.Select(async type =>
            {
                try
                {
                    var interfaceCode = GenerateInterface(type, config);
                    var fileName = GetTypeScriptFileName(type);
                    var filePath = Path.Combine(config.TypeScriptInterfacesOutputPath, $"{fileName}.ts");

                    await File.WriteAllTextAsync(filePath, interfaceCode);
                    _logger.LogDebug("Generated interface: {TypeName}", type.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to generate interface for type {TypeName}", type.FullName);
                    throw;
                }
            });

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Generate TypeScript enums
        /// </summary>
        private async Task GenerateEnumsAsync(List<Type> types, TypeGenConfig config)
        {
            if (!types.Any()) return;

            _logger.LogInformation("Generating {Count} TypeScript enums", types.Count);
            Directory.CreateDirectory(config.TypeScriptEnumsOutputPath);

            var tasks = types.Select(async type =>
            {
                try
                {
                    var enumCode = GenerateEnum(type, config);
                    var fileName = GetTypeScriptFileName(type);
                    var filePath = Path.Combine(config.TypeScriptEnumsOutputPath, $"{fileName}.ts");

                    await File.WriteAllTextAsync(filePath, enumCode);
                    _logger.LogDebug("Generated enum: {TypeName}", type.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to generate enum for type {TypeName}", type.FullName);
                    throw;
                }
            });

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Generate TypeScript interface with optimized inline interface support
        /// </summary>
        private string GenerateInterface(Type type, TypeGenConfig config)
        {
            var sb = new StringBuilder();
            var fileProcessedTypes = new HashSet<Type>();

            // Add header
            if (config.AddGeneratedHeaders)
            {
                sb.AppendLine("// <auto-generated />");
                //sb.AppendLine($"// Generated on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC by TypeGen");
                sb.AppendLine("// Changes to this file may be overwritten.");
                sb.AppendLine();
            }

            // Collect all needed inline interfaces
            var nestedInterfaces = new HashSet<string>();
            var properties = GetFilteredProperties(type, config);

            foreach (var property in properties)
            {
                CollectNestedInterfaces(property.PropertyType, config, fileProcessedTypes, nestedInterfaces);
            }

            // Output nested interfaces first
            foreach (var nestedInterface in nestedInterfaces.OrderBy(x => x))
            {
                sb.AppendLine(nestedInterface);
                sb.AppendLine();
            }

            // Generate main interface
            var interfaceName = type.GetCustomAttribute<ExportToTsAttribute>()?.CustomName ?? type.Name;
            sb.AppendLine($"export interface {interfaceName} {{");

            foreach (var property in properties)
            {
                var propertyName = config.UseCamelCase ? ToCamelCase(property.Name) : property.Name;
                var propertyType = MapTypeToTypeScript(property.PropertyType, config);
                var isOptional = IsPropertyOptional(property);

                sb.AppendLine($"  {propertyName}{(isOptional ? "?" : "")}: {propertyType};");
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>
        /// Recursively collect all nested interfaces needed
        /// </summary>
        private void CollectNestedInterfaces(Type type, TypeGenConfig config, HashSet<Type> fileProcessedTypes, HashSet<string> nestedInterfaces)
        {
            if (!config.GenerateNestedInterfaces) return;

            // Handle nullable types
            var underlyingType = Nullable.GetUnderlyingType(type);
            if (underlyingType != null)
            {
                CollectNestedInterfaces(underlyingType, config, fileProcessedTypes, nestedInterfaces);
                return;
            }

            // Handle arrays and collections
            if (type.IsArray)
            {
                CollectNestedInterfaces(type.GetElementType()!, config, fileProcessedTypes, nestedInterfaces);
                return;
            }

            if (type.IsGenericType && IsCollectionType(type.GetGenericTypeDefinition()))
            {
                var elementType = type.GetGenericArguments()[0];
                CollectNestedInterfaces(elementType, config, fileProcessedTypes, nestedInterfaces);
                return;
            }

            // Generate inline interface for classes from scanned assemblies
            if (ShouldGenerateInlineInterface(type, fileProcessedTypes))
            {
                fileProcessedTypes.Add(type);
                var interfaceCode = GenerateInlineInterface(type, config, fileProcessedTypes, nestedInterfaces);
                nestedInterfaces.Add(interfaceCode);
            }
        }

        /// <summary>
        /// Generate a single inline interface
        /// </summary>
        private string GenerateInlineInterface(Type type, TypeGenConfig config, HashSet<Type> fileProcessedTypes, HashSet<string> nestedInterfaces)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"interface {type.Name} {{");

            var properties = GetFilteredProperties(type, config);

            // Collect nested interfaces for this type's properties
            foreach (var property in properties)
            {
                CollectNestedInterfaces(property.PropertyType, config, fileProcessedTypes, nestedInterfaces);
            }

            // Generate properties
            foreach (var property in properties)
            {
                var propertyName = config.UseCamelCase ? ToCamelCase(property.Name) : property.Name;
                var propertyType = MapTypeToTypeScript(property.PropertyType, config);
                var isOptional = IsPropertyOptional(property);

                sb.AppendLine($"  {propertyName}{(isOptional ? "?" : "")}: {propertyType};");
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>
        /// Generate TypeScript enum
        /// </summary>
        private string GenerateEnum(Type type, TypeGenConfig config)
        {
            var sb = new StringBuilder();

            if (config.AddGeneratedHeaders)
            {
                sb.AppendLine("// <auto-generated />");
                //sb.AppendLine($"// Generated on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC by TypeGen");
                sb.AppendLine("// Changes to this file may be overwritten.");
                sb.AppendLine();
            }

            var enumName = type.GetCustomAttribute<ExportToTsAttribute>()?.CustomName ?? type.Name;
            sb.AppendLine($"export enum {enumName} {{");

            var enumNames = Enum.GetNames(type);
            var enumValues = Enum.GetValues(type);

            for (int i = 0; i < enumNames.Length; i++)
            {
                var name = enumNames[i];
                var value = Convert.ToInt32(enumValues.GetValue(i));
                var comma = i < enumNames.Length - 1 ? "," : "";
                sb.AppendLine($"  {name} = {value}{comma}");
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>
        /// Map C# type to TypeScript type
        /// </summary>
        private string MapTypeToTypeScript(Type type, TypeGenConfig config)
        {
            // Handle nullable types
            if (Nullable.GetUnderlyingType(type) is Type underlyingType)
            {
                return MapTypeToTypeScript(underlyingType, config);
            }

            // Handle arrays
            if (type.IsArray)
            {
                return $"{MapTypeToTypeScript(type.GetElementType()!, config)}[]";
            }

            // Handle generic collections
            if (type.IsGenericType)
            {
                var genericType = type.GetGenericTypeDefinition();
                
                if (IsCollectionType(genericType))
                {
                    var elementType = type.GetGenericArguments()[0];
                    return $"{MapTypeToTypeScript(elementType, config)}[]";
                }

                if (genericType == typeof(Dictionary<,>) || genericType == typeof(IDictionary<,>))
                {
                    var valueType = type.GetGenericArguments()[1];
                    return type.GetGenericArguments()[0] == typeof(string) 
                        ? $"Record<string, {MapTypeToTypeScript(valueType, config)}>"
                        : "{ [key: string]: any }";
                }
            }

            // Handle enums
            if (type.IsEnum)
            {
                return type.GetCustomAttribute<ExportToTsAttribute>()?.CustomName ?? type.Name;
            }

            // Check custom mappings
            if (config.TypeMappings.TryGetValue(type.FullName ?? type.Name, out var customMapping))
            {
                return customMapping;
            }

            // Classes from scanned assemblies get inline interfaces
            if (type.IsClass && IsFromScannedAssemblies(type) && !IsSystemType(type))
            {
                return type.Name;
            }

            // Default mappings
            return GetDefaultTypeScriptType(type);
        }

        /// <summary>
        /// Get default TypeScript type
        /// </summary>
        private static string GetDefaultTypeScriptType(Type type) => type.Name switch
        {
            nameof(String) => "string",
            nameof(Boolean) => "boolean",
            nameof(Int16) or nameof(Int32) or nameof(Int64) or 
            nameof(UInt16) or nameof(UInt32) or nameof(UInt64) or
            nameof(Byte) or nameof(SByte) or nameof(Single) or 
            nameof(Double) or nameof(Decimal) => "number",
            nameof(DateTime) or nameof(DateTimeOffset) => "Date",
            nameof(Guid) or nameof(TimeSpan) => "string",
            nameof(Object) => "any",
            _ => "any"
        };

        /// <summary>
        /// Check if should generate inline interface
        /// </summary>
        private bool ShouldGenerateInlineInterface(Type type, HashSet<Type> fileProcessedTypes) =>
            type.IsClass &&
            !fileProcessedTypes.Contains(type) &&
            !IsSystemType(type) &&
            IsFromScannedAssemblies(type);

        /// <summary>
        /// Check if type is from scanned assemblies
        /// </summary>
        private bool IsFromScannedAssemblies(Type type) =>
            _availableTypes.ContainsKey(type.FullName ?? string.Empty) || 
            _availableTypes.ContainsKey(type.Name);

        /// <summary>
        /// Check if type is system type
        /// </summary>
        private static bool IsSystemType(Type type) =>
            type.IsPrimitive ||
            type == typeof(string) ||
            type.IsEnum ||
            type.IsAbstract ||
            type.Namespace?.StartsWith("System") == true;

        /// <summary>
        /// Check if generic type is collection
        /// </summary>
        private static bool IsCollectionType(Type genericType) =>
            genericType == typeof(List<>) ||
            genericType == typeof(IList<>) ||
            genericType == typeof(IEnumerable<>) ||
            genericType == typeof(ICollection<>);

        /// <summary>
        /// Get filtered properties
        /// </summary>
        private PropertyInfo[] GetFilteredProperties(Type type, TypeGenConfig config) =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && 
                           p.GetMethod?.IsPublic == true && 
                           ShouldIncludeProperty(p, config))
                .ToArray();

        /// <summary>
        /// Check if property should be included
        /// </summary>
        private bool ShouldIncludeProperty(PropertyInfo property, TypeGenConfig config)
        {
            // Exclude navigation properties
            if (config.IgnoreNavigationProperties && IsNavigationProperty(property))
                return false;

            // Handle JsonIgnore properties
            var hasJsonIgnore = property.GetCustomAttribute<JsonIgnoreAttribute>() != null;
            return !hasJsonIgnore || config.IncludeJsonIgnoreProperties;
        }

        /// <summary>
        /// Check if navigation property
        /// </summary>
        private static bool IsNavigationProperty(PropertyInfo property) =>
            property.GetCustomAttribute<ForeignKeyAttribute>() != null &&
            property.GetMethod?.IsVirtual == true && 
            !property.GetMethod.IsFinal;

        /// <summary>
        /// Check if property is optional
        /// </summary>
        private static bool IsPropertyOptional(PropertyInfo property) =>
            Nullable.GetUnderlyingType(property.PropertyType) != null ||
            !property.PropertyType.IsValueType ||
            property.GetCustomAttribute<JsonIgnoreAttribute>() != null;

        /// <summary>
        /// Generate index files
        /// </summary>
        private async Task GenerateIndexFilesAsync(List<Type> interfaces, List<Type> enums, TypeGenConfig config)
        {
            var tasks = new List<Task>();

            if (interfaces.Any())
            {
                var path = Path.Combine(config.TypeScriptInterfacesOutputPath, "index.ts");
                tasks.Add(File.WriteAllTextAsync(path, GenerateIndexFile(interfaces)));
            }

            if (enums.Any())
            {
                var path = Path.Combine(config.TypeScriptEnumsOutputPath, "index.ts");
                tasks.Add(File.WriteAllTextAsync(path, GenerateIndexFile(enums)));
            }

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Generate index file content
        /// </summary>
        private static string GenerateIndexFile(List<Type> types)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated />");
            //sb.AppendLine($"// Generated on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC by TypeGen");
            sb.AppendLine();

            foreach (var type in types.OrderBy(t => t.Name))
            {
                var typeName = type.GetCustomAttribute<ExportToTsAttribute>()?.CustomName ?? type.Name;
                var fileName = ToCamelCase(typeName);
                sb.AppendLine($"export {{ {typeName} }} from './{fileName}';");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Convert to camelCase
        /// </summary>
        private static string ToCamelCase(string input) =>
            string.IsNullOrEmpty(input) || input.Length < 2
                ? input.ToLowerInvariant()
                : char.ToLowerInvariant(input[0]) + input[1..];

        /// <summary>
        /// Get TypeScript file name
        /// </summary>
        private static string GetTypeScriptFileName(Type type)
        {
            var typeName = type.GetCustomAttribute<ExportToTsAttribute>()?.CustomName ?? type.Name;
            return ToCamelCase(typeName);
        }
    }
}