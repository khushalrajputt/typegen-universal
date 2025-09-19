using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TypeGen.Services;

namespace TypeGen.Extensions
{
    /// <summary>
    /// Service collection extensions for TypeGen dependency injection
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Add TypeGen services with optimized configuration for production use
        /// </summary>
        public static IServiceCollection AddTypeGenServices(this IServiceCollection services)
        {
            // Core services with scoped lifetimes for optimal memory usage
            services.AddScoped<IConfigurationLoader, ConfigurationLoader>();
            services.AddScoped<ITypeDiscoveryService, TypeDiscoveryService>();
            services.AddScoped<ITypeScriptGeneratorService, TypeScriptGeneratorService>();
            services.AddScoped<IDbEnumGeneratorService, DbEnumGeneratorService>();
            services.AddScoped<ITypeGenOrchestrator, TypeGenOrchestrator>();

            // Configure logging for production readiness
            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information);
                builder.AddFilter("Microsoft.Extensions", LogLevel.Warning);
                builder.AddFilter("System", LogLevel.Warning);

                // Enable debug logging only for TypeGen namespace
                builder.AddFilter("TypeGen", LogLevel.Debug);
            });

            return services;
        }

        /// <summary>
        /// Add TypeGen services with custom configuration
        /// </summary>
        public static IServiceCollection AddTypeGenServices(this IServiceCollection services,
            Action<TypeGenServiceOptions> configure)
        {
            var options = new TypeGenServiceOptions();
            configure(options);

            // Apply custom configuration
            services.Configure<LoggerFilterOptions>(loggerOptions =>
            {
                loggerOptions.MinLevel = options.MinLogLevel;

                foreach (var filter in options.LogFilters)
                {
                    loggerOptions.AddFilter(filter.Category, filter.Level);
                }
            });

            return services.AddTypeGenServices();
        }
    }

    /// <summary>
    /// Options for configuring TypeGen services
    /// </summary>
    public class TypeGenServiceOptions
    {
        public LogLevel MinLogLevel { get; set; } = LogLevel.Information;
        public List<LogFilter> LogFilters { get; set; } = new();

        public void AddLogFilter(string category, LogLevel level)
        {
            LogFilters.Add(new LogFilter(category, level));
        }
    }

    /// <summary>
    /// Log filter configuration
    /// </summary>
    public record LogFilter(string Category, LogLevel Level);
}