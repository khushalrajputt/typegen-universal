namespace TypeGen.Configuration
{
    /// <summary>
    /// Configuration for database enum generation
    /// </summary>
    public class DbEnumConfig
    {
        /// <summary>
        /// Database table name (including schema if needed, e.g., "auth.roles")
        /// </summary>
        public string TableName { get; set; } = string.Empty;

        /// <summary>
        /// Column name containing the enum key/id
        /// </summary>
        public string KeyColumn { get; set; } = string.Empty;

        /// <summary>
        /// Column name containing the enum value/name
        /// </summary>
        public string ValueColumn { get; set; } = string.Empty;

        /// <summary>
        /// Name of the C# enum to generate
        /// </summary>
        public string EnumName { get; set; } = string.Empty;

        /// <summary>
        /// C# namespace for the generated enum
        /// </summary>
        public string Namespace { get; set; } = string.Empty;

        /// <summary>
        /// Optional: Custom output path for this specific enum (overrides global setting)
        /// </summary>
        public string? CustomOutputPath { get; set; }
    }
}