using Microsoft.Extensions.Logging;
using TypeGen.Configuration;

namespace TypeGen.Services
{
    /// <summary>
    /// Main orchestrator service that coordinates the complete TypeGen workflow
    /// </summary>
    public interface ITypeGenOrchestrator
    {
        /// <summary>
        /// Run the complete generation process: DB ? C# Enums ? TypeScript
        /// </summary>
        Task RunCompleteGenerationAsync();

        /// <summary>
        /// Run the complete generation process with custom configuration
        /// </summary>
        Task RunCompleteGenerationAsync(TypeGenConfig config);
    }

    /// <summary>
    /// Implementation of the TypeGen orchestrator
    /// </summary>
    public class TypeGenOrchestrator : ITypeGenOrchestrator
    {
        private readonly ILogger<TypeGenOrchestrator> _logger;
        private readonly IConfigurationLoader _configurationLoader;
        private readonly IDbEnumGeneratorService _dbEnumGenerator;
        private readonly ITypeDiscoveryService _typeDiscovery;
        private readonly ITypeScriptGeneratorService _typeScriptGenerator;

        public TypeGenOrchestrator(
            ILogger<TypeGenOrchestrator> logger,
            IConfigurationLoader configurationLoader,
            IDbEnumGeneratorService dbEnumGenerator,
            ITypeDiscoveryService typeDiscovery,
            ITypeScriptGeneratorService typeScriptGenerator)
        {
            _logger = logger;
            _configurationLoader = configurationLoader;
            _dbEnumGenerator = dbEnumGenerator;
            _typeDiscovery = typeDiscovery;
            _typeScriptGenerator = typeScriptGenerator;
        }

        /// <inheritdoc />
        public async Task RunCompleteGenerationAsync()
        {
            var config = await _configurationLoader.LoadConfigurationAsync();
            await RunCompleteGenerationAsync(config);
        }

        /// <inheritdoc />
        public async Task RunCompleteGenerationAsync(TypeGenConfig config)
        {
            _logger.LogInformation("?? Starting complete TypeGen workflow...");

            try
            {
                // Step 1: Generate C# enums from database (if configured)
                if (config.DatabaseEnums.Any())
                {
                    _logger.LogInformation("?? Step 1: Generating C# enums from database...");
                    await _dbEnumGenerator.GenerateEnumsFromDatabaseAsync(config);
                    _logger.LogInformation("? Database enums generated successfully");
                }
                else
                {
                    _logger.LogInformation("?? Step 1: Skipped - No database enums configured");
                }

                // Step 2: Discover types marked with [ExportToTs]
                if (config.AssembliesToScan.Any())
                {
                    _logger.LogInformation("?? Step 2: Discovering types marked with [ExportToTs]...");
                    var discoveredTypes = await _typeDiscovery.DiscoverTypesToExportAsync(config.AssembliesToScan);
                    _logger.LogInformation("? Found {TypesCount} types to export", discoveredTypes.Count);

                    // ? Main generation loop
                    if (discoveredTypes.Any())
                    {
                        // Step 3: Generate TypeScript files
                        _logger.LogInformation("?? Step 3: Generating TypeScript interfaces and enums...");
                        
                        await _typeScriptGenerator.GenerateTypeScriptFilesAsync(discoveredTypes, config);
                        _logger.LogInformation("? TypeScript files generated successfully");
                    }
                    else
                    {
                        _logger.LogWarning("?? No types found with [ExportToTs] attribute in the specified assemblies");
                    }
                }
                else
                {
                    _logger.LogInformation("?? Steps 2-3: Skipped - No assemblies configured to scan");
                }

                _logger.LogInformation("?? TypeGen workflow completed successfully!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "? TypeGen workflow failed: {ErrorMessage}", ex.Message);
                throw;
            }
        }
    }
}