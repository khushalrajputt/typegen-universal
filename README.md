# 🚀 TypeGen Universal Inline Generator

## Production-Ready TypeScript Interface Generator for .NET 8+

[![.NET 8](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![TypeScript](https://img.shields.io/badge/TypeScript-Universal%20Generation-blue)](https://www.typescriptlang.org/)

### ✨ **Key Features**

- 🎯 **Universal Inline Generation** - Generate inline interfaces for ANY class from your assemblies
- 🤖 **Smart Navigation Detection** - Automatically detect and exclude Entity Framework navigation properties
- 📋 **Audit Field Support** - Include JsonIgnore audit fields as optional TypeScript properties
- 🔄 **Database Enum Generation** - Generate TypeScript enums from database tables
- ⚡ **High Performance** - Parallel processing with async/await throughout
- 🛡️ **Production Ready** - Comprehensive error handling and validation

---

## 📦 **Installation**

### Option 1: Global Tool (Recommended)

```bash
dotnet tool install -g TypeGen.UniversalInlineGenerator
```

### Option 2: Local Tool

```bash
dotnet new tool-manifest
dotnet tool install TypeGen.UniversalInlineGenerator
```

### Option 3: Clone and Build

```bash
git clone https://github.com/khushalrajputt/typegen-universal.git
cd typegen-universal/TypeGen
dotnet build -c Release
```

---

## 🎯 **Quick Start**

### 1. Create Configuration

Create `tsgen.config.json` in your project root:

```json
{
  "assembliesToScan": [
    "../YourProject.Service/bin/Debug/net8.0/YourProject.Service.dll",
    "../YourProject.Repository/bin/Debug/net8.0/YourProject.Repository.dll"
  ],
  "typeScriptInterfacesOutputPath": "./src/app/models/",
  "typeScriptEnumsOutputPath": "./src/app/enums/",
  "generateNestedInterfaces": true,
  "ignoreNavigationProperties": true,
  "useCamelCase": true
}
```

### 2. Mark Your C# Classes

```csharp
using TypeGen.Attributes;

[ExportToTs]
public class UserDto
{
    public string Name { get; set; }
    public List<Role> Roles { get; set; }  // ✅ Will generate inline Role interface
}

public class Role  // No [ExportToTs] needed!
{
    public int RoleId { get; set; }
    public string RoleName { get; set; }
}
```

### 3. Generate TypeScript

```bash
# Build your project first
dotnet build

# Generate TypeScript interfaces
typegen

# Or with custom config
typegen --config my-config.json
```

### 4. Generated Output

```typescript
// user.ts
interface Role {
  roleId?: number;
  roleName?: string;
}

export interface UserDto {
  name?: string;
  roles?: Role[];
}
```

---

## 🛠️ **Advanced Features**

### Universal Inline Generation

Works with **ANY class** from your scanned assemblies - provides complete type safety without manual configuration.

### Smart Navigation Detection

Automatically excludes Entity Framework navigation properties using pattern detection:

```csharp
// ❌ Excluded (navigation property)
[ForeignKey(nameof(CreatedBy))]
public virtual User CreatedByUser { get; set; }

// ✅ Included (data composition)
public Role UserRole { get; set; }
```

### Database-Driven Enums

Generate TypeScript enums directly from your database tables:

```json
{
  "databaseEnums": [
    {
      "tableName": "auth.roles",
      "keyColumn": "role_id",
      "valueColumn": "role_name", 
      "enumName": "Roles",
      "namespace": "MyProject.Service.Enums"
    }
  ]
}
```

---

## 🔧 **CLI Options**

```bash
typegen                              # Use default tsgen.config.json
typegen custom-config.json          # Use specific config file
typegen --config my-config.json     # Use --config flag
typegen -c my-config.json           # Use -c shorthand
typegen --help                      # Show help
```

---

## 🏗️ **Project Integration**

### npm Scripts Integration

```json
{
  "scripts": {
    "generate-models": "typegen",
    "prebuild": "npm run generate-models",
    "build": "ng build"
  }
}
```

### MSBuild Integration

```xml
<Target Name="GenerateTypeScript" AfterTargets="Build">
  <Exec Command="typegen" ContinueOnError="false" />
</Target>
```

---

## 📖 **Documentation**

- 🛠️ [Configuration Guide](./CONFIGURATION-GUIDE.md)  
- 🔧 [Congiguration template](./tsgen.config.template.json)

---

## 🎉 **Ready for Production**

TypeGen Universal Inline Generator is now optimized and production-ready! Features include:

- ✅ **Async/await throughout** for optimal performance
- ✅ **Assembly caching** and error resilience  
- ✅ **Parallel file generation** for speed
- ✅ **Comprehensive validation** with helpful error messages
- ✅ **Easy portability** to any .NET project
- ✅ **Zero configuration** for standard scenarios

**Start generating complete, type-safe TypeScript interfaces today!** 🚀