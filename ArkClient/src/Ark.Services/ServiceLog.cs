using System;

namespace Ark.Services;

/// <summary>
/// 服务层日志门面 —— 解耦 <c>Ark.Services</c> 与具体日志后端（Godot.GD.Print / ILogger 等）。
/// Bootstrap 在初始化阶段安装 <see cref="InfoSink"/> 与 <see cref="ErrorSink"/>；
/// 服务层代码统一调用 <see cref="Info"/> / <see cref="Error"/>。
/// 满足 ARK004：<c>Ark.Services</c> 不直接引用 <c>Godot.*</c>。
/// </summary>
public static class ServiceLog
{
    /// <summary>信息级日志输出端（默认丢弃）。Bootstrap 注入 Godot.GD.Print / ILogger.LogInformation。</summary>
    public static Action<string>? InfoSink { get; set; }

    /// <summary>错误级日志输出端（默认丢弃）。Bootstrap 注入 Godot.GD.PrintErr / ILogger.LogError。</summary>
    public static Action<string>? ErrorSink { get; set; }

    public static void Info(string message) => InfoSink?.Invoke(message);

    public static void Error(string message) => ErrorSink?.Invoke(message);
}
