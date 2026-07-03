// Phase 2: 声明 Ark.Services 与 Godot 解耦。Roslyn ARK004 据此报错。
// 服务层日志统一通过 ServiceLog 门面输出；Bootstrap 注入 Godot.GD.Print/PrintErr 作为 sink。
[assembly: Ark.Analyzers.Attributes.ForbidGodotDependency]
