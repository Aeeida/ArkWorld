using System.Collections.Immutable;

namespace Ark.Analyzers;

/// <summary>
/// 所有 Ark Analyzer 诊断描述符的集中定义。
/// </summary>
internal static class DiagnosticDescriptors
{
    private const string Category = "Ark.Architecture";
    private const string EcsFirstCategory = "ECSFIRST";

    /// <summary>
    /// ARK001: 禁止在 Godot Node 派生类中直接使用网络 DTO 类型。
    /// DTO 必须先映射到 ECS 组件，再由 ECS 系统同步到 Godot。
    /// </summary>
    public static readonly Microsoft.CodeAnalysis.DiagnosticDescriptor DtoInGodotNode = new(
        id: "ARK001",
        title: "DTO type used directly in Godot Node",
        messageFormat: "Type '{0}' is a network DTO and must not be used directly in Godot Node class '{1}'. Map it to an ECS component first.",
        category: Category,
        defaultSeverity: Microsoft.CodeAnalysis.DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Network DTO types must flow through the ECS layer before reaching Godot presentation. "
                   + "Use [MapToEcsComponent] on the DTO and consume the generated ECS component instead.");

    /// <summary>
    /// ARK002: 禁止在 Godot Node 派生类的方法中直接引用 DTO 类型（参数/局部变量/返回值）。
    /// </summary>
    public static readonly Microsoft.CodeAnalysis.DiagnosticDescriptor DtoInGodotMethod = new(
        id: "ARK002",
        title: "DTO type referenced in Godot Node method",
        messageFormat: "Method '{0}' in Godot Node class '{1}' references DTO type '{2}'. Route data through ECS components instead.",
        category: Category,
        defaultSeverity: Microsoft.CodeAnalysis.DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Methods in Godot Node-derived classes must not accept, return, or use network DTO types. "
                   + "Consume ECS components instead.");

    /// <summary>
    /// ECS001: 禁止在遥测投影路径中写入控制归属字段（单一真相源防护）。
    /// </summary>
    public static readonly Microsoft.CodeAnalysis.DiagnosticDescriptor EcsSingleSourceViolation = new(
        id: "ECS001",
        title: "Single source-of-truth violation for control ownership",
        messageFormat: "Method '{0}' writes control-ownership field '{1}' while processing telemetry source '{2}'. Route ownership updates through seat/authority source only.",
        category: EcsFirstCategory,
        defaultSeverity: Microsoft.CodeAnalysis.DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Control ownership fields must have a single authoritative writer and must not be overwritten from telemetry projection flows.");

    /// <summary>
    /// ECS002: 禁止在 ECS query chunk 循环内进行结构变更。
    /// </summary>
    public static readonly Microsoft.CodeAnalysis.DiagnosticDescriptor EcsStructuralChangeInsideQueryLoop = new(
        id: "ECS002",
        title: "Structural change inside ECS query loop",
        messageFormat: "Invocation '{0}' performs ECS structural change inside query chunk loop. Use ECS-first closure pass/deferred writes.",
        category: EcsFirstCategory,
        defaultSeverity: Microsoft.CodeAnalysis.DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "ECS structural changes must be deferred and applied after query iteration to preserve deterministic ECS-first behavior.");

    /// <summary>
    /// ECS003: 禁止在控制解析路径中优先读取遥测组件而非 ownership 组件。
    /// </summary>
    public static readonly Microsoft.CodeAnalysis.DiagnosticDescriptor EcsTelemetryUsedAsControlAuthority = new(
        id: "ECS003",
        title: "Telemetry component used as control authority",
        messageFormat: "Method '{0}' uses telemetry component '{1}' as control authority source. Read ownership/control component instead.",
        category: EcsFirstCategory,
        defaultSeverity: Microsoft.CodeAnalysis.DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Control identity must come from ownership/control components, not telemetry/runtime projection components.");

    /// <summary>
    /// ECS900: 禁止通过 pragma/suppress 方式绕过 ECSFIRST 规则。
    /// </summary>
    public static readonly Microsoft.CodeAnalysis.DiagnosticDescriptor EcsAnalyzerBypass = new(
        id: "ECS900",
        title: "ECSFIRST analyzer bypass is not allowed",
        messageFormat: "Do not suppress ECSFIRST diagnostic '{0}' via pragma or SuppressMessage.",
        category: EcsFirstCategory,
        defaultSeverity: Microsoft.CodeAnalysis.DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "ECSFIRST diagnostics are hard architecture gates and cannot be bypassed locally.");

    /// <summary>
    /// ECS004: 投影路径中手写 DTO→Component 字段拷贝（应使用生成的 ApplyToEcs 扩展）。
    /// </summary>
    public static readonly Microsoft.CodeAnalysis.DiagnosticDescriptor EcsManualDtoMapping = new(
        id: "ECS004",
        title: "Manual DTO-to-component field assignment in projection path",
        messageFormat: "Field '{0}' is assigned manually from DTO type '{1}' which has [MapToEcsComponent]/[ExternalDtoMapping]. Use the generated ApplyToEcs extension instead, or mark the field with [EcsComputedField] if it is derived.",
        category: EcsFirstCategory,
        defaultSeverity: Microsoft.CodeAnalysis.DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "DTO→Component assignments in Ark.Services.Remote.* must go through the source-generated mapper to keep DTO/Component coupling discoverable.");

    /// <summary>
    /// ARK003: Godot Node 派生类不得直接实例化标记了 [ServiceClass] 的服务/缓存类型。
    /// </summary>
    public static readonly Microsoft.CodeAnalysis.DiagnosticDescriptor ServiceNewedFromGodotNode = new(
        id: "ARK003",
        title: "Service class instantiated from Godot Node",
        messageFormat: "Godot Node class '{0}' directly instantiates service type '{1}'. Inject via GameServices/DI instead.",
        category: Category,
        defaultSeverity: Microsoft.CodeAnalysis.DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Service/cache classes marked with [ServiceClass] must be created by the bootstrap layer; Godot presentation nodes should resolve them via DI.");

    /// <summary>
    /// ARK004: 程序集声明 [ForbidGodotDependency] 后，禁止引用任何 Godot.* 命名空间下的类型。
    /// </summary>
    public static readonly Microsoft.CodeAnalysis.DiagnosticDescriptor NetworkLayerGodotDependency = new(
        id: "ARK004",
        title: "Godot dependency in decoupled assembly",
        messageFormat: "Type '{0}' is from Godot and cannot be referenced in assembly '{1}' which declares [ForbidGodotDependency]. Inject an abstraction (e.g. INetLogger) instead.",
        category: Category,
        defaultSeverity: Microsoft.CodeAnalysis.DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Network/data/protocol assemblies must stay free of Godot types so they remain testable and portable. Use abstractions and inject Godot-backed implementations from Bootstrap.");

    /// <summary>
    /// ARK005: 禁止 Godot Node 派生类对 ECS Entity/EntityStore/CommandBuffer 进行结构变更。
    /// 结构变更必须发生在 ECS 系统类内（或显式标 [EcsAuthoritySystemNode] 的 Bootstrap 节点）。
    /// </summary>
    public static readonly Microsoft.CodeAnalysis.DiagnosticDescriptor EcsStructuralChangeFromGodotNode = new(
        id: "ARK005",
        title: "ECS structural change performed from Godot Node",
        messageFormat: "Godot Node class '{0}' invokes ECS structural method '{1}'. Move this write into an ECS system or write an intent component instead.",
        category: Category,
        defaultSeverity: Microsoft.CodeAnalysis.DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Presentation Nodes must not own ECS authority. Push intent components from the Node and let an ECS system perform AddComponent/AddTag/DeleteEntity. Bootstrap nodes can opt out via [EcsAuthoritySystemNode].");

    /// <summary>
    /// ECS005: 禁止 Ark.Bridge.* 程序集对未标 [BridgeWritableComponent] 的业务组件进行写入。
    /// Bridge 表现层应只读 ECS 组件并执行 Godot 表现，不得拥有业务组件的写权威。
    /// 业务组件写入路径：ECS System / 服务器授权回投 / 客户端发送意图后由服务端写回。
    /// </summary>
    public static readonly Microsoft.CodeAnalysis.DiagnosticDescriptor BridgeWritesBusinessComponent = new(
        id: "ECS005",
        title: "Bridge layer writes a business ECS component",
        messageFormat: "Bridge type '{0}' writes business component '{1}' via '{2}'. Mark the component with [BridgeWritableComponent] (presentation feedback) or move the write into an ECS system / server-authority projection.",
        category: EcsFirstCategory,
        defaultSeverity: Microsoft.CodeAnalysis.DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Ark.Bridge.* assemblies present ECS state to Godot; they must not author business component writes. "
                   + "Allow-listed presentation feedback components must opt in via [BridgeWritableComponent] / [BridgeWritableTag].");

    /// <summary>
    /// ECS007: Bridge 层方法内的 ECS 组件拉取调用过多，建议改为 Sync System 推送范式。
    /// 阈值：单方法内 <c>entity.TryGetComponent&lt;T&gt;</c> / <c>entity.GetComponent&lt;T&gt;</c>
    /// 调用 ≥ <c>PullThreshold</c>（默认 5）即触发。
    /// 豁免：标 [EcsAuthorityBridge]/[EcsAuthoritySystemNode]、Sync System Receiver
    /// （在 Ark.Systems.Sync 命名空间）以及容器类型自身在 Ark.Ecs.Bridge.* 命名空间。
    /// </summary>
    public static readonly Microsoft.CodeAnalysis.DiagnosticDescriptor BridgeExcessivePullPattern = new(
        id: "ECS007",
        title: "Bridge method pulls ECS components too often",
        messageFormat: "Bridge method '{0}' performs {1} ECS component pulls (TryGetComponent/GetComponent). Consider implementing a Sync System receiver and consuming a pushed frame instead.",
        category: EcsFirstCategory,
        defaultSeverity: Microsoft.CodeAnalysis.DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Excessive per-frame ECS pulls in Ark.Bridge.* indicates the presentation logic should be migrated "
                   + "to a Sync System push (see PlayerHudSyncSystem / VehicleHudSyncSystem).");
}
