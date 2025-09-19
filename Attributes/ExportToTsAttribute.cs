namespace TypeGen.Attributes
{
    /// <summary>
    /// Marks C# classes and enums for TypeScript export
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum | AttributeTargets.Interface)]
    public class ExportToTsAttribute : Attribute
    {
        /// <summary>
        /// Optional custom name for the TypeScript type
        /// </summary>
        public string? CustomName { get; }

        /// <summary>
        /// Creates an export attribute with default naming
        /// </summary>
        public ExportToTsAttribute()
        {
        }

        /// <summary>
        /// Creates an export attribute with custom naming
        /// </summary>
        /// <param name="customName">Custom name for the TypeScript type</param>
        public ExportToTsAttribute(string customName)
        {
            CustomName = customName;
        }
    }
}