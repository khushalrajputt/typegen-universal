namespace TypeGen.Configuration
{
    /// <summary>
    /// Main configuration for TypeGen code generation
    /// </summary>
    public class TypeGenConfig
    {
        /// <summary>
        /// PostgreSQL connection string for database enum generation
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Paths to assemblies to scan for [ExportToTs] attributed types
        /// </summary>
        public List<string> AssembliesToScan { get; set; } = new();

        /// <summary>
        /// Output path for generated TypeScript interfaces
        /// </summary>
        public string TypeScriptInterfacesOutputPath { get; set; } = "./src/app/core/models/generated/";

        /// <summary>
        /// Output path for generated TypeScript enums
        /// </summary>
        public string TypeScriptEnumsOutputPath { get; set; } = "./src/app/core/enums/generated/";

        /// <summary>
        /// Output path for generated C# enums from database
        /// </summary>
        public string CSharpEnumsOutputPath { get; set; } = "./Generated/Enums/";

        /// <summary>
        /// Database enum configurations
        /// </summary>
        public List<DbEnumConfig> DatabaseEnums { get; set; } = new();

        /// <summary>
        /// Whether to generate index.ts files for easy imports
        /// </summary>
        public bool GenerateIndexFiles { get; set; } = false;

        /// <summary>
        /// Whether to use camelCase for TypeScript property names
        /// </summary>
        public bool UseCamelCase { get; set; } = true;

        /// <summary>
        /// Whether to add generated file headers
        /// </summary>
        public bool AddGeneratedHeaders { get; set; } = true;

        /// <summary>
        /// Whether to automatically detect and ignore navigation properties (virtual properties with ForeignKey attribute)
        /// </summary>
        public bool IgnoreNavigationProperties { get; set; } = true;

        /// <summary>
        /// Whether to generate nested interfaces inline for any class found in scanned assemblies
        /// </summary>
        public bool GenerateNestedInterfaces { get; set; } = true;

        /// <summary>
        /// Whether to include properties marked with [JsonIgnore] (they will be made optional)
        /// </summary>
        public bool IncludeJsonIgnoreProperties { get; set; } = true;

        /// <summary>
        /// Custom type mappings from C# to TypeScript with comprehensive defaults
        /// </summary>
        public Dictionary<string, string> TypeMappings { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            // Temporal types
            { "System.DateTime", "Date" },
            { "System.DateTimeOffset", "Date" },
            { "System.DateOnly", "string" }, // .NET 6+
            { "System.TimeOnly", "string" }, // .NET 6+
            { "System.TimeSpan", "string" },
            
            // Numeric types
            { "System.Decimal", "number" },
            { "System.Double", "number" },
            { "System.Single", "number" },
            { "System.Int16", "number" },
            { "System.Int32", "number" },
            { "System.Int64", "number" },
            { "System.UInt16", "number" },
            { "System.UInt32", "number" },
            { "System.UInt64", "number" },
            { "System.Byte", "number" },
            { "System.SByte", "number" },
            
            // Other common types
            { "System.Boolean", "boolean" },
            { "System.String", "string" },
            { "System.Char", "string" },
            { "System.Guid", "string" },
            { "System.Object", "any" },
            { "System.Byte[]", "string" }, // Base64 encoded
            
            // .NET 8 specific types
            { "System.Half", "number" },
            { "System.Int128", "string" }, // Too large for JS number
            { "System.UInt128", "string" }
        };

        /// <summary>
        /// Enhanced validation with detailed error reporting
        /// </summary>
        public ValidationResult Validate()
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            // Validate database configuration
            ValidateDatabaseConfiguration(errors, warnings);
            
            // Validate TypeScript generation configuration
            ValidateTypeScriptConfiguration(errors, warnings);
            
            // Validate paths
            ValidatePaths(errors, warnings);

            return new ValidationResult(errors, warnings);
        }

        private void ValidateDatabaseConfiguration(List<string> errors, List<string> warnings)
        {
            if (!DatabaseEnums.Any()) return;

            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                errors.Add("ConnectionString is required when DatabaseEnums are configured.");
            }

            foreach (var dbEnum in DatabaseEnums)
            {
                ValidateDatabaseEnum(dbEnum, errors);
            }

            if (string.IsNullOrWhiteSpace(CSharpEnumsOutputPath))
            {
                warnings.Add("CSharpEnumsOutputPath not specified - database enums will not be generated.");
            }
        }

        private void ValidateDatabaseEnum(DbEnumConfig dbEnum, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(dbEnum.TableName))
                errors.Add("TableName is required for all database enum configurations.");
            
            if (string.IsNullOrWhiteSpace(dbEnum.KeyColumn))
                errors.Add($"KeyColumn is required for database enum '{dbEnum.EnumName}'.");
            
            if (string.IsNullOrWhiteSpace(dbEnum.ValueColumn))
                errors.Add($"ValueColumn is required for database enum '{dbEnum.EnumName}'.");
            
            if (string.IsNullOrWhiteSpace(dbEnum.EnumName))
                errors.Add($"EnumName is required for database enum with table '{dbEnum.TableName}'.");
            
            if (string.IsNullOrWhiteSpace(dbEnum.Namespace))
                errors.Add($"Namespace is required for database enum '{dbEnum.EnumName}'.");
        }

        private void ValidateTypeScriptConfiguration(List<string> errors, List<string> warnings)
        {
            if (!AssembliesToScan.Any()) return;

            if (string.IsNullOrWhiteSpace(TypeScriptInterfacesOutputPath))
            {
                errors.Add("TypeScriptInterfacesOutputPath is required when AssembliesToScan are configured.");
            }

            if (string.IsNullOrWhiteSpace(TypeScriptEnumsOutputPath))
            {
                errors.Add("TypeScriptEnumsOutputPath is required when AssembliesToScan are configured.");
            }

            // Validate assembly paths
            foreach (var assemblyPath in AssembliesToScan)
            {
                if (string.IsNullOrWhiteSpace(assemblyPath))
                {
                    warnings.Add("Empty assembly path found in AssembliesToScan.");
                }
            }
        }

        private void ValidatePaths(List<string> errors, List<string> warnings)
        {
            // Only validate if we have something to generate
            if (DatabaseEnums.Any() && !string.IsNullOrWhiteSpace(CSharpEnumsOutputPath))
            {
                ValidateOutputPath(CSharpEnumsOutputPath, "CSharpEnumsOutputPath", warnings);
            }

            if (AssembliesToScan.Any())
            {
                if (!string.IsNullOrWhiteSpace(TypeScriptInterfacesOutputPath))
                    ValidateOutputPath(TypeScriptInterfacesOutputPath, "TypeScriptInterfacesOutputPath", warnings);
                
                if (!string.IsNullOrWhiteSpace(TypeScriptEnumsOutputPath))
                    ValidateOutputPath(TypeScriptEnumsOutputPath, "TypeScriptEnumsOutputPath", warnings);
            }
        }

        private static void ValidateOutputPath(string path, string configName, List<string> warnings)
        {
            try
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    warnings.Add($"{configName} directory does not exist: {directory}. It will be created during generation.");
                }
            }
            catch (Exception)
            {
                warnings.Add($"{configName} contains invalid path: {path}");
            }
        }
    }

    /// <summary>
    /// Result of configuration validation
    /// </summary>
    public class ValidationResult
    {
        public List<string> Errors { get; }
        public List<string> Warnings { get; }
        public bool IsValid => !Errors.Any();

        public ValidationResult(List<string> errors, List<string> warnings)
        {
            Errors = errors ?? new List<string>();
            Warnings = warnings ?? new List<string>();
        }

        public void ThrowIfInvalid()
        {
            if (!IsValid)
            {
                throw new InvalidOperationException($"Configuration validation failed:\n{string.Join("\n", Errors)}");
            }
        }
    }
}