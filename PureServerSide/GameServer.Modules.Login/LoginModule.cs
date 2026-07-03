using GameServer.Application.Core;
using GameServer.Application.Features.Login;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Modules.Login;

public sealed class LoginModule : IGameModule
{
    public string Name => "Login";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddFeatureHandlers(typeof(LoginCommand));
    }
}
