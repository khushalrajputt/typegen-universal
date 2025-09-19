using Microsoft.Extensions.Logging;
using System.Text.Json;
using TypeGen.Configuration;

namespace TypeGen.Services
{
    /// <summary>
    /// Service for loading and validating TypeGen configuration
    /// </summary>
    public interface IConfigurationLoader
    {
        /// <summary>
        /// Load configuration from the default config file (tsgen.config.json)
        /// </summary>
        Task<TypeGenConfig> LoadConfigurationAsync();

        /// <summary>
        /// Load configuration from a specific file path
        /// </summary>
        Task<TypeGenConfig> LoadConfigurationAsync(string configFilePath);
    }

    /// <summary>
    /// Implementation of configuration loader with JSON support
    /// </summary>
    public class ConfigurationLoader : IConfigurationLoader
    {
        private readonly ILogger<ConfigurationLoader> _logger;
        private const string DEFAULT_CONFIG_FILE = "tsgen.config.json";

        public ConfigurationLoader(ILogger<ConfigurationLoader> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<TypeGenConfig> LoadConfigurationAsync()
        {
            return await LoadConfigurationAsync(DEFAULT_CONFIG_FILE);
        }

        /// <inheritdoc />
        public async Task<TypeGenConfig> LoadConfigurationAsync(string configFilePath)
        {
            // Resolve the full path for the config file
            var resolvedPath = ResolveConfigFilePath(configFilePath);
            
            _logger.LogInformation("Loading configuration from: {ConfigPath}", resolvedPath);

            if (!File.Exists(resolvedPath))
            {
                throw new FileNotFoundException($"Configuration file not found: {resolvedPath}");
            }

            try
            {
                var jsonContent = await File.ReadAllTextAsync(resolvedPath);

                // Remove JSON comments (simple implementation)
                jsonContent = RemoveJsonComments(jsonContent);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };

                var config = JsonSerializer.Deserialize<TypeGenConfig>(jsonContent, options);

                if (config == null)
                {
                    throw new InvalidOperationException("Failed to deserialize configuration file.");
                }

                // Validate the configuration
                // Enhanced validation with detailed feedback
                var validationResult = config.Validate();
                
                if (validationResult.Warnings.Any())
                {
                    foreach (var warning in validationResult.Warnings)
                    {
                        _logger.LogWarning("Configuration warning: {Warning}", warning);
                    }
                }

                validationResult.ThrowIfInvalid();

                _logger.LogInformation("Configuration loaded and validated successfully");
                _logger.LogInformation("- Database enums to generate: {DatabaseEnumsCount}", config.DatabaseEnums.Count);
                _logger.LogInformation("- Assemblies to scan: {AssembliesCount}", config.AssembliesToScan.Count);
                _logger.LogInformation("- TypeScript interfaces output: {InterfacesPath}", config.TypeScriptInterfacesOutputPath);
                _logger.LogInformation("- TypeScript enums output: {EnumsPath}", config.TypeScriptEnumsOutputPath);

                return config;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Invalid JSON in configuration file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Resolve config file path to handle both relative and absolute paths
        /// </summary>
        private string ResolveConfigFilePath(string configFilePath)
        {
            // If it's already an absolute path, return as-is
            if (Path.IsPathRooted(configFilePath))
            {
                return configFilePath;
            }

            // Check if the file exists relative to current directory first
            var currentDirPath = Path.Combine(Directory.GetCurrentDirectory(), configFilePath);
            if (File.Exists(currentDirPath))
            {
                return currentDirPath;
            }

            // If not found, check relative to the TypeGen project directory
            // This helps when running from different directories
            var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
            
            if (!string.IsNullOrEmpty(assemblyDirectory))
            {
                var assemblyRelativePath = Path.Combine(assemblyDirectory, configFilePath);
                if (File.Exists(assemblyRelativePath))
                {
                    return assemblyRelativePath;
                }

                // Also check the project root (go up from bin/Debug/net8.0/)
                var projectRoot = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(assemblyDirectory)));
                if (!string.IsNullOrEmpty(projectRoot))
                {
                    var projectRootPath = Path.Combine(projectRoot, configFilePath);
                    if (File.Exists(projectRootPath))
                    {
                        return projectRootPath;
                    }
                }
            }

            // If still not found, return the original path for error reporting
            return configFilePath;
        }

        /// <summary>
        /// Simple JSON comment removal (handles // comments)
        /// </summary>
        private static string RemoveJsonComments(string json)
        {
            var lines = json.Split('\n');
            var cleanedLines = new List<string>();

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Skip lines that start with //
                if (trimmedLine.StartsWith("//"))
                    continue;

                // Remove inline comments (simple approach - doesn't handle strings containing //)
                var commentIndex = line.IndexOf("//", StringComparison.Ordinal);
                if (commentIndex >= 0)
                {
                    // Check if // is inside quotes (simple check)
                    var beforeComment = line[..commentIndex];
                    var quoteCount = beforeComment.Count(c => c == '"');
                    
                    // If even number of quotes, the // is not inside a string
                    if (quoteCount % 2 == 0)
                    {
                        cleanedLines.Add(beforeComment.TrimEnd());
                        continue;
                    }
                }

                cleanedLines.Add(line);
            }

            return string.Join('\n', cleanedLines);
        }
    }
}