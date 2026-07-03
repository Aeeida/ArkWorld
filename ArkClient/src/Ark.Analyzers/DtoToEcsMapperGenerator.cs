using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ark.Analyzers;

/// <summary>
/// 增量 Source Generator：扫描标记了 <c>[MapToEcsComponent(typeof(T))]</c> 的 DTO 类型，
/// 自动生成 DTO → ECS Component 的映射扩展方法。
/// 
/// <para>支持两种模式：</para>
/// <list type="bullet">
///   <item>源码内声明的 DTO（通过 <c>ForAttributeWithMetadataName</c> 增量扫描）</item>
///   <item>元数据引用的 DTO（通过 <c>CompilationProvider</c> 扫描引用程序集）</item>
/// </list>
/// 
/// <para>生成内容：</para>
/// <list type="bullet">
///   <item><c>static void ApplyToEcs(this DtoType dto, ref ComponentType component)</c></item>
///   <item><c>static ComponentType ToComponentName(this DtoType dto)</c></item>
/// </list>
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class DtoToEcsMapperGenerator : IIncrementalGenerator
{
    private const string MapToEcsComponentAttributeName = "Ark.Analyzers.Attributes.MapToEcsComponentAttribute";
    private const string EcsFieldMapAttributeName = "Ark.Analyzers.Attributes.EcsFieldMapAttribute";
    private const string EcsIgnoreAttributeName = "Ark.Analyzers.Attributes.EcsIgnoreAttribute";
    private const string ExternalDtoMappingAttributeName = "Ark.Analyzers.Attributes.ExternalDtoMappingAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // ── Path 1: Source-declared DTOs (incremental, fast) ──────────
        var sourceDeclarations = context.SyntaxProvider.ForAttributeWithMetadataName(
            MapToEcsComponentAttributeName,
            predicate: static (node, _) => node is TypeDeclarationSyntax,
            transform: static (ctx, ct) => ExtractMappingInfoFromSymbol(
                ctx.TargetSymbol as INamedTypeSymbol, ctx.SemanticModel.Compilation, ct))
            .Where(static info => info is not null);

        // ── Path 2: Metadata-referenced DTOs (scans referenced assemblies) ──
        var metadataDeclarations = context.CompilationProvider.Select(static (compilation, ct) =>
        {
            var attrSymbol = compilation.GetTypeByMetadataName(MapToEcsComponentAttributeName);
            if (attrSymbol is null)
                return ImmutableArray<DtoMappingInfo>.Empty;

            var results = ImmutableArray.CreateBuilder<DtoMappingInfo>();

            foreach (var reference in compilation.References)
            {
                ct.ThrowIfCancellationRequested();
                if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol asm)
                    continue;

                ScanNamespace(asm.GlobalNamespace, attrSymbol, compilation, results, ct);
            }

            return results.ToImmutable();
        });

        // ── Path 3: Assembly-level [ExternalDtoMapping] ─────────────────
        var assemblyDeclarations = context.CompilationProvider.Select(static (compilation, ct) =>
        {
            var results = ImmutableArray.CreateBuilder<DtoMappingInfo>();
            ExtractExternalMappings(compilation.Assembly, results, ct);
            return results.ToImmutable();
        });

        // ── Merge all paths and generate ──────────────────────────────
        var combined = sourceDeclarations.Collect()
            .Combine(metadataDeclarations)
            .Combine(assemblyDeclarations);

        context.RegisterSourceOutput(combined, static (spc, triple) =>
        {
            var ((sourceMappings, metadataMappings), assemblyMappings) = triple;

            var all = new List<DtoMappingInfo>();
            if (!sourceMappings.IsDefaultOrEmpty)
            {
                foreach (var m in sourceMappings)
                {
                    if (m is not null) all.Add(m);
                }
            }
            if (!metadataMappings.IsDefaultOrEmpty)
            {
                // Deduplicate by DTO full name (source wins over metadata)
                var sourceNames = new HashSet<string>(all.Select(m => m.DtoFullName));
                foreach (var m in metadataMappings)
                {
                    if (!sourceNames.Contains(m.DtoFullName))
                        all.Add(m);
                }
            }
            if (!assemblyMappings.IsDefaultOrEmpty)
            {
                // Merge external (assembly) mappings — same DTO can appear in both:
                // append additional component targets if the DTO already exists.
                var byName = new Dictionary<string, DtoMappingInfo>(StringComparer.Ordinal);
                foreach (var m in all)
                    byName[m.DtoFullName] = m;

                foreach (var external in assemblyMappings)
                {
                    if (byName.TryGetValue(external.DtoFullName, out var existing))
                    {
                        var existingTargets = new HashSet<string>(existing.ComponentMappings.Select(c => c.ComponentFullName));
                        foreach (var comp in external.ComponentMappings)
                        {
                            if (existingTargets.Add(comp.ComponentFullName))
                                existing.ComponentMappings.Add(comp);
                        }
                    }
                    else
                    {
                        byName[external.DtoFullName] = external;
                        all.Add(external);
                    }
                }
            }

            if (all.Count == 0)
                return;

            var source = GenerateMapperSource(all);
            spc.AddSource("DtoToEcsMappers.g.cs", source);
        });
    }

    /// <summary>
    /// 递归扫描命名空间，查找带有 [MapToEcsComponent] 的类型。
    /// </summary>
    private static void ScanNamespace(
        INamespaceSymbol ns,
        INamedTypeSymbol attrSymbol,
        Compilation compilation,
        ImmutableArray<DtoMappingInfo>.Builder results,
        CancellationToken ct)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            ct.ThrowIfCancellationRequested();
            var info = ExtractMappingInfoFromSymbol(type, compilation, ct);
            if (info is not null)
                results.Add(info);
        }

        foreach (var childNs in ns.GetNamespaceMembers())
        {
            ScanNamespace(childNs, attrSymbol, compilation, results, ct);
        }
    }

    /// <summary>
    /// 扫描程序集级 [ExternalDtoMapping] 特性，为外部 sealed DTO 构建映射。
    /// </summary>
    private static void ExtractExternalMappings(
        IAssemblySymbol assembly,
        ImmutableArray<DtoMappingInfo>.Builder results,
        CancellationToken ct)
    {
        // Group by DTO so multiple [ExternalDtoMapping] on the same DTO collapse into one info.
        var byDto = new Dictionary<string, DtoMappingInfo>(StringComparer.Ordinal);

        foreach (var attrData in assembly.GetAttributes())
        {
            ct.ThrowIfCancellationRequested();

            var attrClass = attrData.AttributeClass;
            if (attrClass is null)
                continue;
            if (attrClass.ToDisplayString() != ExternalDtoMappingAttributeName)
                continue;

            if (attrData.ConstructorArguments.Length < 2)
                continue;

            if (attrData.ConstructorArguments[0].Value is not INamedTypeSymbol dtoType)
                continue;
            if (attrData.ConstructorArguments[1].Value is not INamedTypeSymbol componentType)
                continue;

            var fieldOverrides = new List<string>();
            if (attrData.ConstructorArguments.Length >= 3 &&
                attrData.ConstructorArguments[2].Kind == TypedConstantKind.Array)
            {
                foreach (var v in attrData.ConstructorArguments[2].Values)
                {
                    if (v.Value is string s) fieldOverrides.Add(s);
                }
            }

            var dtoMembers = GetDtoMembers(dtoType);
            var componentFields = GetComponentFields(componentType);
            var fieldMappings = BuildExternalFieldMappings(dtoMembers, componentFields, fieldOverrides);

            var dtoFullName = dtoType.ToDisplayString();
            if (!byDto.TryGetValue(dtoFullName, out var info))
            {
                info = new DtoMappingInfo(
                    dtoFullName,
                    dtoType.Name,
                    dtoType.ContainingNamespace?.ToDisplayString() ?? "",
                    dtoType.IsRecord,
                    new List<ComponentMapping>());
                byDto[dtoFullName] = info;
            }

            info.ComponentMappings.Add(new ComponentMapping(
                componentType.ToDisplayString(),
                componentType.Name,
                fieldMappings));
        }

        foreach (var info in byDto.Values)
            results.Add(info);
    }

    /// <summary>
    /// 解析外部映射的字段重命名表："DtoField=ComponentField" 或 "DtoField=" (忽略)。
    /// 未列出的 DTO 字段按大小写不敏感同名匹配。
    /// </summary>
    private static List<FieldMapping> BuildExternalFieldMappings(
        List<DtoMember> dtoMembers,
        List<ComponentField> componentFields,
        List<string> fieldOverrides)
    {
        var renames = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var entry in fieldOverrides)
        {
            if (string.IsNullOrEmpty(entry))
                continue;
            var eqIdx = entry.IndexOf('=');
            if (eqIdx < 0)
                continue;
            var lhs = entry.Substring(0, eqIdx).Trim();
            var rhs = entry.Substring(eqIdx + 1).Trim();
            if (lhs.Length == 0)
                continue;
            renames[lhs] = rhs.Length == 0 ? null : rhs;
        }

        var componentFieldMap = new Dictionary<string, ComponentField>(StringComparer.OrdinalIgnoreCase);
        foreach (var cf in componentFields)
            componentFieldMap[cf.Name] = cf;

        var mappings = new List<FieldMapping>();
        foreach (var dm in dtoMembers)
        {
            string? targetName;
            if (renames.TryGetValue(dm.Name, out var override_))
            {
                if (override_ is null) continue; // explicit ignore
                targetName = override_;
            }
            else
            {
                targetName = dm.Name;
            }

            if (componentFieldMap.TryGetValue(targetName, out var matched))
            {
                mappings.Add(new FieldMapping(dm.Name, matched.Name, dm.TypeName, matched.TypeName, dm.IsProperty));
            }
        }

        return mappings;
    }

    /// <summary>
    /// 从带有 [MapToEcsComponent] 特性的类型符号中提取映射信息。
    /// 同时适用于源码和元数据引用的类型。
    /// 支持 typeof() 和字符串两种构造函数。
    /// </summary>
    private static DtoMappingInfo? ExtractMappingInfoFromSymbol(
        INamedTypeSymbol? typeSymbol,
        Compilation compilation,
        CancellationToken ct)
    {
        if (typeSymbol is null)
            return null;

        var mappings = new List<ComponentMapping>();

        foreach (var attrData in typeSymbol.GetAttributes())
        {
            ct.ThrowIfCancellationRequested();

            if (attrData.AttributeClass?.ToDisplayString() != MapToEcsComponentAttributeName)
                continue;

            if (attrData.ConstructorArguments.Length < 1)
                continue;

            INamedTypeSymbol? componentType = null;
            var arg = attrData.ConstructorArguments[0];

            if (arg.Value is INamedTypeSymbol directType)
            {
                // typeof(ComponentType) constructor
                componentType = directType;
            }
            else if (arg.Value is string typeName)
            {
                // string constructor — resolve via compilation
                componentType = compilation.GetTypeByMetadataName(typeName);
            }

            if (componentType is null)
                continue;

            // Collect DTO members (properties and fields)
            var dtoMembers = GetDtoMembers(typeSymbol);

            // Collect component fields
            var componentFields = GetComponentFields(componentType);

            // Build field mappings
            var fieldMappings = BuildFieldMappings(dtoMembers, componentFields, componentType);

            mappings.Add(new ComponentMapping(
                componentType.ToDisplayString(),
                componentType.Name,
                fieldMappings));
        }

        if (mappings.Count == 0)
            return null;

        return new DtoMappingInfo(
            typeSymbol.ToDisplayString(),
            typeSymbol.Name,
            typeSymbol.ContainingNamespace?.ToDisplayString() ?? "",
            typeSymbol.IsRecord,
            mappings);
    }

    private static List<DtoMember> GetDtoMembers(INamedTypeSymbol typeSymbol)
    {
        var members = new List<DtoMember>();

        foreach (var member in typeSymbol.GetMembers())
        {
            // Skip members with [EcsIgnore]
            if (member.GetAttributes().Any(a =>
                a.AttributeClass?.ToDisplayString() == EcsIgnoreAttributeName))
                continue;

            string? globalMapping = null;
            var perComponentOverrides = new List<ExplicitFieldOverride>();
            foreach (var attr in member.GetAttributes())
            {
                if (attr.AttributeClass?.ToDisplayString() != EcsFieldMapAttributeName)
                    continue;
                if (attr.ConstructorArguments.Length < 1 ||
                    attr.ConstructorArguments[0].Value is not string mapped)
                    continue;

                if (attr.ConstructorArguments.Length >= 2 &&
                    attr.ConstructorArguments[1].Value is INamedTypeSymbol target)
                {
                    perComponentOverrides.Add(new ExplicitFieldOverride(target.ToDisplayString(), mapped));
                }
                else if (globalMapping is null)
                {
                    globalMapping = mapped;
                }
            }

            switch (member)
            {
                case IPropertySymbol prop when !prop.IsStatic && prop.DeclaredAccessibility == Accessibility.Public:
                    members.Add(new DtoMember(prop.Name, prop.Type.ToDisplayString(), globalMapping, perComponentOverrides, IsProperty: true));
                    break;
                case IFieldSymbol field when !field.IsStatic && field.DeclaredAccessibility == Accessibility.Public:
                    members.Add(new DtoMember(field.Name, field.Type.ToDisplayString(), globalMapping, perComponentOverrides, IsProperty: false));
                    break;
            }
        }

        return members;
    }

    private static List<ComponentField> GetComponentFields(INamedTypeSymbol componentType)
    {
        var fields = new List<ComponentField>();

        foreach (var member in componentType.GetMembers())
        {
            if (member is IFieldSymbol field && !field.IsStatic && field.DeclaredAccessibility == Accessibility.Public)
            {
                fields.Add(new ComponentField(field.Name, field.Type.ToDisplayString()));
            }
        }

        return fields;
    }

    private static List<FieldMapping> BuildFieldMappings(
        List<DtoMember> dtoMembers,
        List<ComponentField> componentFields,
        INamedTypeSymbol componentType)
    {
        var mappings = new List<FieldMapping>();
        var componentFieldMap = new Dictionary<string, ComponentField>(StringComparer.OrdinalIgnoreCase);
        foreach (var cf in componentFields)
            componentFieldMap[cf.Name] = cf;

        var componentTypeName = componentType.ToDisplayString();

        foreach (var dm in dtoMembers)
        {
            // 1. Per-component override has highest priority.
            string? explicitName = null;
            foreach (var ov in dm.PerComponentOverrides)
            {
                if (string.Equals(ov.ComponentTypeName, componentTypeName, StringComparison.Ordinal))
                {
                    explicitName = ov.ComponentFieldName;
                    break;
                }
            }

            // 2. Fall back to the global [EcsFieldMap("name")] when no per-component override matched.
            explicitName ??= dm.GlobalExplicitField;

            if (explicitName is not null)
            {
                if (componentFieldMap.TryGetValue(explicitName, out var target))
                {
                    mappings.Add(new FieldMapping(dm.Name, target.Name, dm.TypeName, target.TypeName, dm.IsProperty));
                }
                continue;
            }

            // 3. Name-based matching (case-insensitive)
            if (componentFieldMap.TryGetValue(dm.Name, out var matched))
            {
                mappings.Add(new FieldMapping(dm.Name, matched.Name, dm.TypeName, matched.TypeName, dm.IsProperty));
            }
        }

        return mappings;
    }

    /// <summary>
    /// 生成所有映射器的源代码。
    /// </summary>
    private static string GenerateMapperSource(List<DtoMappingInfo> mappings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Generated by Ark.Analyzers.DtoToEcsMapperGenerator");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        // Group by DTO namespace to create organized extension classes
        var byNamespace = mappings.GroupBy(m => m.DtoNamespace);

        foreach (var nsGroup in byNamespace)
        {
            var mapperNamespace = string.IsNullOrEmpty(nsGroup.Key)
                ? "Ark.Generated.EcsMappers"
                : nsGroup.Key + ".EcsMappers";

            sb.AppendLine($"namespace {mapperNamespace}");
            sb.AppendLine("{");

            foreach (var dto in nsGroup)
            {
                sb.AppendLine($"    /// <summary>");
                sb.AppendLine($"    /// Auto-generated ECS mapping extensions for <see cref=\"{dto.DtoFullName}\"/>.");
                sb.AppendLine($"    /// </summary>");
                sb.AppendLine($"    public static class {dto.DtoName}EcsMapper");
                sb.AppendLine("    {");

                foreach (var comp in dto.ComponentMappings)
                {
                    // ApplyToEcs method
                    sb.AppendLine($"        /// <summary>Apply DTO values to an existing ECS component (by ref).</summary>");
                    sb.AppendLine($"        public static void ApplyToEcs(this {dto.DtoFullName} dto, ref {comp.ComponentFullName} component)");
                    sb.AppendLine("        {");

                    foreach (var field in comp.FieldMappings)
                    {
                        var conversion = GetConversionExpression($"dto.{field.DtoFieldName}", field.DtoTypeName, field.ComponentTypeName);
                        sb.AppendLine($"            component.{field.ComponentFieldName} = {conversion};");
                    }

                    sb.AppendLine("        }");
                    sb.AppendLine();

                    // ToEcsComponent factory method
                    sb.AppendLine($"        /// <summary>Create a new ECS component from this DTO.</summary>");
                    sb.AppendLine($"        public static {comp.ComponentFullName} To{comp.ComponentName}(this {dto.DtoFullName} dto)");
                    sb.AppendLine("        {");
                    sb.AppendLine($"            var component = new {comp.ComponentFullName}();");
                    sb.AppendLine($"            {dto.DtoName}EcsMapper.ApplyToEcs(dto, ref component);");
                    sb.AppendLine("            return component;");
                    sb.AppendLine("        }");
                }

                sb.AppendLine("    }");
                sb.AppendLine();
            }

            sb.AppendLine("}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// 生成简单的类型转换表达式。支持常见数字类型、Guid、string 等。
    /// </summary>
    private static string GetConversionExpression(string sourceExpr, string sourceType, string targetType)
    {
        if (sourceType == targetType)
            return sourceExpr;

        // Common numeric conversions
        if (IsNumericType(sourceType) && IsNumericType(targetType))
            return $"({targetType}){sourceExpr}";

        // Guid -> System.Guid / vice-versa
        if ((sourceType == "System.Guid" && targetType == "System.Guid") ||
            (sourceType == "Guid" && targetType == "System.Guid"))
            return sourceExpr;

        // bool -> byte
        if (sourceType == "bool" && targetType == "byte")
            return $"(byte)({sourceExpr} ? 1 : 0)";

        // byte -> bool
        if (sourceType == "byte" && targetType == "bool")
            return $"({sourceExpr} != 0)";

        // Default: explicit cast
        return $"({targetType}){sourceExpr}";
    }

    private static bool IsNumericType(string typeName) => typeName switch
    {
        "byte" or "sbyte" or "short" or "ushort" or "int" or "uint" or
        "long" or "ulong" or "float" or "double" or "decimal" => true,
        _ => false
    };

    // ── Data models ──────────────────────────────────────────────────

    private sealed record DtoMappingInfo(
        string DtoFullName,
        string DtoName,
        string DtoNamespace,
        bool IsRecord,
        List<ComponentMapping> ComponentMappings);

    private sealed record ComponentMapping(
        string ComponentFullName,
        string ComponentName,
        List<FieldMapping> FieldMappings);

    private sealed record FieldMapping(
        string DtoFieldName,
        string ComponentFieldName,
        string DtoTypeName,
        string ComponentTypeName,
        bool IsProperty);

    private sealed record DtoMember(
        string Name,
        string TypeName,
        string? GlobalExplicitField,
        List<ExplicitFieldOverride> PerComponentOverrides,
        bool IsProperty);

    private sealed record ExplicitFieldOverride(
        string ComponentTypeName,
        string ComponentFieldName);

    private sealed record ComponentField(
        string Name,
        string TypeName);
}
