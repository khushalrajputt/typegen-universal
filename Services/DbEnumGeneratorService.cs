using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data;
using System.Text;
using TypeGen.Attributes;
using TypeGen.Configuration;

namespace TypeGen.Services
{
    /// <summary>
    /// Service for generating C# enums from database tables
    /// </summary>
    public interface IDbEnumGeneratorService
    {
        /// <summary>
        /// Generate C# enums from database tables based on configuration
        /// </summary>
        Task GenerateEnumsFromDatabaseAsync(TypeGenConfig config);
    }

    /// <summary>
    /// Implementation of database enum generator service
    /// </summary>
    public class DbEnumGeneratorService : IDbEnumGeneratorService
    {
        private readonly ILogger<DbEnumGeneratorService> _logger;

        // PostgreSQL reserved words that need to be escaped
        private static readonly HashSet<string> PostgreSqlReservedWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "order", "group", "user", "role", "type", "class", "namespace", "public", "private", 
            "protected", "internal", "static", "readonly", "const", "new", "override", "virtual",
            "abstract", "sealed", "partial", "async", "await", "using", "var", "dynamic",
            "true", "false", "null", "this", "base", "return", "if", "else", "switch", "case",
            "default", "for", "foreach", "while", "do", "break", "continue", "try", "catch",
            "finally", "throw", "lock", "checked", "unchecked", "unsafe", "fixed", "sizeof",
            "typeof", "nameof", "is", "as", "in", "out", "ref", "params", "delegate", "event",
            "operator", "implicit", "explicit", "interface", "struct", "enum", "union", "select"
        };

        public DbEnumGeneratorService(ILogger<DbEnumGeneratorService> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task GenerateEnumsFromDatabaseAsync(TypeGenConfig config)
        {
            if (!config.DatabaseEnums.Any())
            {
                _logger.LogInformation("No database enums configured to generate");
                return;
            }

            _logger.LogInformation("Connecting to database: {ConnectionString}", 
                MaskConnectionString(config.ConnectionString));

            using var connection = new NpgsqlConnection(config.ConnectionString);
            await connection.OpenAsync();

            foreach (var enumConfig in config.DatabaseEnums)
            {
                await GenerateEnumFromTableAsync(connection, enumConfig, config.CSharpEnumsOutputPath);
            }
        }

        /// <summary>
        /// Generate a single enum from a database table
        /// </summary>
        private async Task GenerateEnumFromTableAsync(NpgsqlConnection connection, DbEnumConfig enumConfig, string defaultOutputPath)
        {
            _logger.LogInformation("Generating enum {EnumName} from table {TableName}", 
                enumConfig.EnumName, enumConfig.TableName);

            try
            {
                // Fetch enum data from database
                var enumData = await FetchEnumDataAsync(connection, enumConfig);

                if (!enumData.Any())
                {
                    _logger.LogWarning("No data found in table {TableName} for enum {EnumName}", 
                        enumConfig.TableName, enumConfig.EnumName);
                    return;
                }

                // Generate C# enum code
                var enumCode = GenerateEnumCode(enumConfig, enumData);

                // Determine output path
                var outputPath = enumConfig.CustomOutputPath ?? defaultOutputPath;
                
                // Ensure directory exists
                Directory.CreateDirectory(outputPath);

                // Write enum file
                var filePath = Path.Combine(outputPath, $"{enumConfig.EnumName}.cs");
                await File.WriteAllTextAsync(filePath, enumCode);

                _logger.LogInformation("? Generated enum {EnumName} -> {FilePath}", enumConfig.EnumName, filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate enum {EnumName} from table {TableName}: {Error}", 
                    enumConfig.EnumName, enumConfig.TableName, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Fetch enum data from database table
        /// </summary>
        private async Task<List<(object Key, string Value)>> FetchEnumDataAsync(NpgsqlConnection connection, DbEnumConfig config)
        {
            var sql = $"SELECT {NormalizePostgreSqlIdentifier(config.KeyColumn)}, {NormalizePostgreSqlIdentifier(config.ValueColumn)} " +
                     $"FROM {NormalizePostgreSqlIdentifier(config.TableName)} " +
                     $"ORDER BY {NormalizePostgreSqlIdentifier(config.KeyColumn)}";

            _logger.LogDebug("Executing SQL: {Sql}", sql);

            using var command = new NpgsqlCommand(sql, connection);
            using var reader = await command.ExecuteReaderAsync();

            var enumData = new List<(object Key, string Value)>();

            while (await reader.ReadAsync())
            {
                var key = reader.GetValue(0);
                var value = reader.GetString(1);
                enumData.Add((key, value));
            }

            return enumData;
        }

        /// <summary>
        /// Generate C# enum source code
        /// </summary>
        private string GenerateEnumCode(DbEnumConfig config, List<(object Key, string Value)> enumData)
        {
            var sb = new StringBuilder();

            // File header
            sb.AppendLine("// <auto-generated />");
            //sb.AppendLine($"// Generated on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC by TypeGen");
            sb.AppendLine();

            // Using statements
            sb.AppendLine("using TypeGen.Attributes;");
            sb.AppendLine();

            // Namespace
            sb.AppendLine($"namespace {config.Namespace}");
            sb.AppendLine("{");

            // Enum declaration with ExportToTs attribute
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// Auto-generated enum from database table: {config.TableName}");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    [ExportToTs]");
            sb.AppendLine($"    public enum {config.EnumName}");
            sb.AppendLine("    {");

            // Enum values
            for (int i = 0; i < enumData.Count; i++)
            {
                var (key, value) = enumData[i];
                var enumMemberName = SanitizeEnumMemberName(value);
                var isLast = i == enumData.Count - 1;

                sb.AppendLine($"        {enumMemberName} = {key}{(isLast ? string.Empty : ",")}");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        /// <summary>
        /// Sanitize enum member name to be valid C# identifier
        /// </summary>
        private static string SanitizeEnumMemberName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Unknown";

            var sb = new StringBuilder();
            bool capitalizeNext = true;

            foreach (char c in value)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(capitalizeNext ? char.ToUpper(c) : c);
                    capitalizeNext = false;
                }
                else if (char.IsWhiteSpace(c) || c == '-' || c == '_')
                {
                    capitalizeNext = true;
                }
            }

            var result = sb.ToString();

            // Ensure it starts with a letter
            if (result.Length > 0 && char.IsDigit(result[0]))
            {
                result = "Item" + result;
            }

            return string.IsNullOrEmpty(result) ? "Unknown" : result;
        }

        /// <summary>
        /// Normalize PostgreSQL identifiers (handle case sensitivity)
        /// </summary>
        private static string NormalizePostgreSqlIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return identifier;

            // If identifier contains dots (schema.table format), handle each part
            if (identifier.Contains('.'))
            {
                var parts = identifier.Split('.');
                return string.Join('.', parts.Select(part => NormalizeSingleIdentifier(part)));
            }

            return NormalizeSingleIdentifier(identifier);
        }

        /// <summary>
        /// Normalize a single PostgreSQL identifier
        /// </summary>
        private static string NormalizeSingleIdentifier(string identifier)
        {
            // If it's already quoted, return as is
            if (identifier.StartsWith('"') && identifier.EndsWith('"'))
                return identifier;

            // Check if it's a reserved word or contains special characters
            var needsQuoting = PostgreSqlReservedWords.Contains(identifier) ||
                              !char.IsLetter(identifier[0]) ||
                              identifier.Any(c => !char.IsLetterOrDigit(c) && c != '_');

            return needsQuoting ? $"\"{identifier}\"" : identifier;
        }

        /// <summary>
        /// Mask sensitive information in connection string for logging
        /// </summary>
        private static string MaskConnectionString(string connectionString)
        {
            try
            {
                var builder = new NpgsqlConnectionStringBuilder(connectionString);
                if (!string.IsNullOrEmpty(builder.Password))
                {
                    builder.Password = "***";
                }
                return builder.ToString();
            }
            catch
            {
                // If parsing fails, just show a generic message
                return "[ConnectionString]";
            }
        }
    }
}