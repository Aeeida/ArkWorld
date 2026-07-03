# Ark Public Source Showcase

本目录是从原始 ArkMc 工程中整理出的公开展示版源码包，目标是用于作品集项目展示、技术栈说明和代码结构浏览。

> 注意：本目录不是完整可编译工程。为了公开发布安全和减少噪音，已主动移除二进制产物、缓存、重复共享代码、IDE 文件、发布包和部分非必要配置。

## 工程展示文档

- [工程技术栈与数据流展示](docs/ENGINEERING_SHOWCASE.md)：包含总体架构图、客户端图形/GPU 调用链、服务端权威 Tick、SignalR/TCP 实时同步、共享协议层、持久化/缓存/消息/监控链路，以及源码路径到技术栈的对应关系。

## 项目定位

Ark 是一个使用 C#/.NET 与 Godot 构建的 3D 开放世界 / 元宇宙 / MMORPG 技术原型。项目同时覆盖客户端渲染与交互、多人在线同步、服务器权威游戏循环、分布式 Actor 后端、实时通信、程序化世界、GPU Compute、ECS 架构和模块化游戏业务域。

## 顶层结构

```text
ArkClient/          Godot 4.6 + C# 客户端源码
Middle/             客户端与服务端共享的协议、DTO、事件、核心模型
PureServerSide/     .NET 10 游戏服务端、模块化业务域和基础设施
JointDev/           联合开发解决方案入口，仅保留工程关联文件
```

## 客户端 ArkClient

客户端基于 Godot.NET.Sdk 4.6 与 C#/.NET 10，主要展示以下能力：

- `src/Bootstrap`：Godot Autoload 入口，作为 Composition Root 统一初始化 ECS、网络、GPU、UI、世界、玩法模块。
- `src/Ark.Ecs`：ECS 组件与标签，承载角色、战斗、网络同步、本地预测等数据结构。
- `src/Ark.Services`：客户端服务门面、远程世界、服务端权威桥接、快照应用、远端实体缓存。
- `src/Ark.Networking`：SignalR 与 TCP 客户端通信链路，配合共享协议实现多人同步。
- `src/Ark.World.*`：程序化世界、地形 Chunk、环境切换、地形修改和远端地形回放。
- `src/Ark.Gpu`：GPU Compute 管线、StorageBuffer、Compute Shader 管理。
- `src/Ark.Gameplay.*`：战斗、建造、太空飞行、载具、小队、生活玩法等客户端侧系统。
- `src/Ark.UI`：战斗 HUD、太空 HUD、建造面板、火箭装配、网络信息面板、选择面板等 UI。
- `src/Ark.Analyzers`：自定义 Roslyn Analyzer，用于约束 ECS、DTO 映射、网络层依赖和 Godot 节点边界。

客户端重点技术亮点：

- Godot 4.6 Forward Plus、D3D12、Jolt Physics、多线程物理。
- Friflo ECS 数据驱动架构。
- GPU Compute 加速高度图生成、视锥剔除、群体移动和粒子计算。
- 程序化地形、Chunk 流式加载、LOD、远端地形修改回放。
- 本地预测、远端快照应用、服务端权威动作桥接。

## 共享层 Middle

`Middle` 是公开包中唯一保留的共享代码来源，用于减少重复。原工程中客户端和服务端各自存在部分 `Game.Shared.*` 副本，本公开包已跳过这些重复副本。

主要内容：

- `Game.Shared.Core`：DTO、枚举、通用结果类型、工具类、宇宙/空间模型。
- `Game.Shared.Events`：玩家、战斗、经济、脚本、功能事件。
- `Game.Shared.Models`：共享模型。
- `Game.Shared.Protocols`：网络消息类型、MessagePack 序列化、自定义二进制协议解析。

## 服务端 PureServerSide

服务端基于 .NET 10 / ASP.NET Core，采用模块化游戏后端架构：

- `GameServer.Host`：服务端启动入口，注册 Orleans Silo、业务模块、基础设施、REST API、SignalR Hub、TCP 高频通道和世界 Tick。
- `GameServer.Api.Core`：统一游戏服务 API 抽象。
- `GameServer.Application.Core`：应用层基础能力和 Mediator 注册。
- `GameServer.Application.Features.*`：登录、角色、世界、战斗、背包、经济、舰队、公会、副本、技能、任务、制造、探索、主权、成就、脚本剧情等用例。
- `GameServer.Domain.*`：领域实体、领域事件、基础 Entity/ValueObject/Repository/Specification 抽象。
- `GameServer.Grains.Interfaces` 与 `GameServer.Grains.Implementations`：基于 Microsoft Orleans 的虚拟 Actor，承载玩家、飞船、星系、市场、公会、副本、技能、任务、脚本等状态。
- `GameServer.Infrastructure.*`：EF Core/PostgreSQL 持久化、Redis 缓存、MassTransit/RabbitMQ 消息、OpenTelemetry 监控。
- `GameServer.Networking.*`：SignalR 实时 Hub、自定义 TCP Transport、AOI 分区广播、高频快照通道。
- `GameLayer.*`：服务器权威游戏逻辑，包括战斗、背包、载具、建筑、任务、世界 Tick。
- `GameServer.Tests.*`：xUnit、FluentAssertions、Orleans TestingHost 相关测试源码。

服务端重点技术亮点：

- Microsoft Orleans 虚拟 Actor 分布式状态模型。
- ASP.NET Core Minimal API + SignalR + 自定义 TCP 高频同步通道。
- 20Hz Server-authoritative World Tick，按 AOI/Zone 广播实体快照。
- EF Core + PostgreSQL 持久化、Redis 缓存、RabbitMQ 事件消息。
- OpenTelemetry 可观测性。
- DDD/CQRS 风格的领域层、应用层、模块层拆分。

![rocket](images/rocket.png)

![base](images/base.png)

![space](images/space.png)

![login](images/login.png)