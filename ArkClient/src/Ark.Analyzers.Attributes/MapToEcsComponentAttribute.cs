using System;

namespace Ark.Analyzers.Attributes;

/// <summary>
/// 标记一个 DTO 类型，指定其应映射到的 ECS 组件类型。
/// Source Generator 将自动生成 DTO → ECS Component 的映射扩展方法。
/// <para>
/// 用法: <c>[MapToEcsComponent(typeof(MyComponent))]</c> 或
///       <c>[MapToEcsComponent("Ark.Ecs.Components.MyComponent")]</c>
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class MapToEcsComponentAttribute : Attribute
{
    /// <summary>目标 ECS 组件类型（当使用 typeof() 构造时可用）。</summary>
    public Type? ComponentType { get; }

    /// <summary>目标 ECS 组件的完全限定类型名（字符串形式，跨项目引用时使用）。</summary>
    public string ComponentTypeName { get; }

    /// <summary>
    /// 创建 DTO → ECS 组件映射声明（类型安全版本）。
    /// </summary>
    /// <param name="componentType">目标 ECS 组件类型。</param>
    public MapToEcsComponentAttribute(Type componentType)
    {
        ComponentType = componentType;
        ComponentTypeName = componentType.FullName ?? componentType.Name;
    }

    /// <summary>
    /// 创建 DTO → ECS 组件映射声明（字符串版本，用于跨 SDK 项目引用）。
    /// </summary>
    /// <param name="componentTypeName">目标 ECS 组件的完全限定类型名。</param>
    public MapToEcsComponentAttribute(string componentTypeName)
    {
        ComponentTypeName = componentTypeName;
    }
}

/// <summary>
/// 在 DTO 属性/字段上指定与 ECS 组件字段的显式映射关系。
/// 当 DTO 字段名称与组件字段名称不匹配时使用。
/// <para>
/// 用法: <c>[EcsFieldMap("TargetFieldName")]</c> — 应用到所有目标组件。
/// </para>
/// <para>
/// 多组件场景: <c>[EcsFieldMap("TargetFieldName", typeof(SpecificComponent))]</c>
/// — 只对指定的目标组件生效，可在同一字段上叠加多个特性。
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = true, Inherited = false)]
public sealed class EcsFieldMapAttribute : Attribute
{
    /// <summary>ECS 组件中对应的字段名。</summary>
    public string ComponentFieldName { get; }

    /// <summary>
    /// 仅当目标组件为该类型时生效；为 <c>null</c> 时对所有目标组件生效。
    /// </summary>
    public Type? TargetComponentType { get; }

    /// <summary>
    /// 显式映射到组件字段（对所有目标组件生效）。
    /// </summary>
    /// <param name="componentFieldName">目标 ECS 组件字段名。</param>
    public EcsFieldMapAttribute(string componentFieldName)
    {
        ComponentFieldName = componentFieldName;
        TargetComponentType = null;
    }

    /// <summary>
    /// 显式映射到指定组件的字段（用于一个 DTO 同时映射到多个组件且字段名不同的场景）。
    /// </summary>
    /// <param name="componentFieldName">目标 ECS 组件字段名。</param>
    /// <param name="targetComponentType">仅对该组件生效。</param>
    public EcsFieldMapAttribute(string componentFieldName, Type targetComponentType)
    {
        ComponentFieldName = componentFieldName;
        TargetComponentType = targetComponentType;
    }
}

/// <summary>
/// 标记 DTO 属性/字段在 Source Generator 映射中被忽略。
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class EcsIgnoreAttribute : Attribute
{
}

/// <summary>
/// 标记一个命名空间或程序集中的 DTO 类型禁止在 Godot Node 类中直接使用。
/// Roslyn Analyzer 将对违规使用报告诊断错误。
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
public sealed class ForbidDtoInGodotAttribute : Attribute
{
    /// <summary>禁止直接在 Godot 层使用的 DTO 命名空间前缀。</summary>
    public string DtoNamespacePrefix { get; }

    public ForbidDtoInGodotAttribute(string dtoNamespacePrefix)
    {
        DtoNamespacePrefix = dtoNamespacePrefix;
    }
}

/// <summary>
/// 在程序集层面声明外部 DTO（无法直接加 <see cref="MapToEcsComponentAttribute"/>，
/// 例如来自共享协议库的 sealed record）到 ECS 组件的映射。
/// <para>
/// 字段重命名通过 <paramref name="fieldOverrides"/> 表达，每项格式：
/// <c>"DtoField=ComponentField"</c>；右侧留空（<c>"DtoField="</c>）等价于忽略该 DTO 字段。
/// 未列出的字段按大小写不敏感的同名匹配。
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
public sealed class ExternalDtoMappingAttribute : Attribute
{
    public Type DtoType { get; }
    public Type ComponentType { get; }
    public string[] FieldOverrides { get; }

    public ExternalDtoMappingAttribute(Type dtoType, Type componentType, params string[] fieldOverrides)
    {
        DtoType = dtoType;
        ComponentType = componentType;
        FieldOverrides = fieldOverrides ?? Array.Empty<string>();
    }
}

/// <summary>
/// 标记 ECS 组件中代表"控制权威"的字段。
/// 投影/遥测路径下分析器会拒绝写入此类字段（ECS001），
/// 仅允许由座位/认证流程更新。
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class ControlAuthorityFieldAttribute : Attribute
{
    /// <summary>授权写入此字段的源队列名称（用于豁免，例如 "_vehicleSeatUpdates"）。</summary>
    public string AuthoritySourceQueue { get; }

    public ControlAuthorityFieldAttribute(string authoritySourceQueue = "")
    {
        AuthoritySourceQueue = authoritySourceQueue ?? string.Empty;
    }
}

/// <summary>
/// 标记 ECS 组件中"派生/计算"字段，允许在投影路径中手写赋值（不会触发 ECS004）。
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class EcsComputedFieldAttribute : Attribute { }

/// <summary>
/// 标记一个 ECS 组件为"控制权威源"。
/// 当 <c>[ControlAuthorityField]</c> 字段在某个写入位置的 RHS 中读取了被本属性标记的组件成员时，
/// 该写入被视为来自合法权威源，ECS001 豁免。
/// </summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ControlAuthoritySourceAttribute : Attribute { }

/// <summary>
/// 标记一个客户端服务/缓存类，禁止 Godot Node 派生类直接 <c>new</c>。
/// 必须通过 DI / 工厂获取实例（参考 <c>GameServices</c>）。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class ServiceClassAttribute : Attribute { }

/// <summary>
/// 标记一个方法为"控制权威解析器"。
/// ECS003 分析器对此类方法启用遥测组件读取检查（禁止把 telemetry 组件当 control authority）。
/// 取代旧版按方法名硬编码白名单的方式。
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ControlAuthorityResolverAttribute : Attribute { }

/// <summary>
/// 在程序集层面声明：本程序集禁止依赖 <c>Godot.*</c> 命名空间下的任何类型。
/// ARK004 分析器据此对 <c>Godot.</c> 类型引用报告诊断错误。
/// 用于网络层 / ECS 数据层 / 共享协议层等"必须与 Godot 解耦"的程序集。
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
public sealed class ForbidGodotDependencyAttribute : Attribute { }

/// <summary>
/// 标记一个 Godot Node 派生类为"ECS 权威系统宿主"，豁免 ARK005
/// （允许在该 Node 内进行 <c>Entity.AddComponent / AddTag / DeleteEntity</c> 等结构变更）。
/// 仅用于 Bootstrap / 系统驱动 Node。常规表现层 Node 不应使用此属性。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class EcsAuthoritySystemNodeAttribute : Attribute { }

/// <summary>
/// 标记一个 ECS 组件为"Bridge 表现层可写"。
/// ECS005 分析器允许 <c>Ark.Bridge.*</c> 程序集对该组件执行
/// <c>AddComponent / RemoveComponent / GetComponent() = ...</c> 等写入；
/// 未标注此特性的业务组件不允许在 Bridge 程序集中被写入
/// （应通过 ECS 系统 / 服务器授权回投 / 写入意图组件）。
/// 典型用途：表现反馈类组件（命中闪光、UI 反馈状态等）。
/// </summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class BridgeWritableComponentAttribute : Attribute { }

/// <summary>
/// 标记一个 Tag 类型为"Bridge 表现层可写"。语义同 <see cref="BridgeWritableComponentAttribute"/>。
/// </summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class BridgeWritableTagAttribute : Attribute { }

/// <summary>
/// 标记一个 <c>Ark.Bridge.*</c> 程序集中的非 Node 类为"Bridge 授权写入器"。
/// ECS005 对该类内的 ECS 结构变更豁免（语义类似 Godot Node 上的
/// <see cref="EcsAuthoritySystemNodeAttribute"/>）。
/// 用于：客户端本地权威管理（如本地小队 / 本地建造放置 / 本地反馈聚合）。
/// 一旦相应业务被迁到服务端授权回投，应移除此属性以让分析器重新生效。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class EcsAuthorityBridgeAttribute : Attribute { }

