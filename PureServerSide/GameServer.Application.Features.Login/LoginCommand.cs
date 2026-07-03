using Game.Shared.Core.DTOs;
using GameServer.Application.Core.CQRS;

namespace GameServer.Application.Features.Login;

public sealed record LoginCommand(string AccountId, string PasswordHash) : ICommand<LoginResultDto>;

public sealed record LogoutCommand(Guid PlayerId) : ICommand<LogoutResultDto>;

public sealed record JoinWorldCommand(Guid PlayerId, string WorldId) : ICommand<JoinWorldResultDto>;
