using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TypeGen.Configuration;
using TypeGen.Extensions;
using TypeGen.Services;

namespace TypeGen
{
    /// <summary>
    /// TypeGen Universal Inline Generator - Production Ready Entry Point
    /// </summary>
    class Program
    {
        private const string DefaultConfigFile = "tsgen.config.json";
        private const int ExitSuccess = 0;
        private const int ExitError = 1;

        static async Task<int> Main(string[] args)
        {
            try
            {
                var host = CreateHostBuilder(args).Build();
                
                using (host)
                {
                    await RunApplicationAsync(host, args);
                }

                return ExitSuccess;
            }
            catch (Exception ex)
            {
                // Ensure we always log critical errors to console
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"TypeGen failed: {ex.Message}");
                Console.ResetColor();
                
                // Show inner exceptions for debugging
                var innerEx = ex.InnerException;
                if (innerEx != null)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"   Inner: {innerEx.Message}");
                    Console.ResetColor();
                }

                return ExitError;
            }
        }

        /// <summary>
        /// Create optimized host builder with comprehensive services
        /// </summary>
        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    // Register TypeGen services with optimized configuration
                    services.AddTypeGenServices();
                    
                    // Configure logging for production
                    services.Configure<LoggerFilterOptions>(options =>
                    {
                        // Reduce noise in production while keeping important info
                        options.MinLevel = LogLevel.Information;
                        options.AddFilter("TypeGen", LogLevel.Debug); // Enable debug for TypeGen namespace
                        options.AddFilter("Microsoft", LogLevel.Warning); // Reduce Microsoft framework noise
                        options.AddFilter("System", LogLevel.Warning);
                    });
                })
                .UseConsoleLifetime();

        /// <summary>
        /// Run the application with enhanced error handling and CLI support
        /// </summary>
        static async Task RunApplicationAsync(IHost host, string[] args)
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            
            try
            {
                // Parse command line arguments
                var configFilePath = ParseConfigFilePath(args);
                
                // Load and validate configuration
                var configLoader = host.Services.GetRequiredService<IConfigurationLoader>();
                var config = await configLoader.LoadConfigurationAsync(configFilePath);

                // Show startup banner
                ShowStartupBanner(logger, config);

                // Run the generation process
                var orchestrator = host.Services.GetRequiredService<ITypeGenOrchestrator>();
                await orchestrator.RunCompleteGenerationAsync(config);

                // Success message
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("TypeGen completed successfully!");
                Console.ResetColor();
            }
            catch (FileNotFoundException ex)
            {
                logger.LogError("Configuration file not found: {Message}", ex.Message);
                ShowUsageHelp();
                throw;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Configuration validation"))
            {
                logger.LogError("Configuration validation failed:");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(ex.Message);
                Console.ResetColor();
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "TypeGen execution failed: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Parse command line arguments for configuration file path
        /// </summary>
        static string ParseConfigFilePath(string[] args)
        {
            // Support --config or -c flags
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] is "--config" or "-c")
                {
                    return args[i + 1];
                }
            }

            // Support positional argument
            if (args.Length > 0 && !args[0].StartsWith("-"))
            {
                return args[0];
            }

            return DefaultConfigFile;
        }

        /// <summary>
        /// Show professional startup banner with configuration summary
        /// </summary>
        static void ShowStartupBanner(ILogger<Program> logger, TypeGenConfig config)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                          TypeGen Universal Inline Generator                          ║");
            Console.WriteLine("║                                    v2.0.0                                            ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            
            Console.WriteLine();
            
            logger.LogInformation("*** Configuration Summary: ***");
            logger.LogInformation("   • Assemblies to scan: {Count}", config.AssembliesToScan.Count);
            logger.LogInformation("   • Database enums: {Count}", config.DatabaseEnums.Count);
            logger.LogInformation("   • TypeScript interfaces: {Path}", config.TypeScriptInterfacesOutputPath);
            logger.LogInformation("   • TypeScript enums: {Path}", config.TypeScriptEnumsOutputPath);
            logger.LogInformation("   • Universal inline generation: {Enabled}", config.GenerateNestedInterfaces);
            logger.LogInformation("   • Navigation property filtering: {Enabled}", config.IgnoreNavigationProperties);
            
            Console.WriteLine();
        }

        /// <summary>
        /// Show usage help for command line arguments
        /// </summary>
        static void ShowUsageHelp()
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Usage:");
            Console.ResetColor();
            Console.WriteLine("   typegen                              # Use default tsgen.config.json");
            Console.WriteLine("   typegen custom-config.json          # Use specific config file");
            Console.WriteLine("   typegen --config my-config.json     # Use --config flag");
            Console.WriteLine("   typegen -c my-config.json           # Use -c shorthand");
            Console.WriteLine();
            Console.WriteLine("For detailed documentation, visit:");
            Console.WriteLine("   https://github.com/khushalrajputt/typegen-universal");
            Console.WriteLine();
        }
    }
}