using FluentAssertions;
using NBomber.CSharp;
using NBomber.Http.CSharp;

namespace GameServer.Tests.Load;

public class LoadTests
{
    [Fact(Skip = "Requires running server instance")]
    public void StatusEndpoint_ShouldHandleLoad()
    {
        using var httpClient = new HttpClient();

        var scenario = Scenario.Create("status_check", async context =>
        {
            var request = Http.CreateRequest("GET", "http://localhost:5000/api/status");
            var response = await Http.Send(httpClient, request);
            return response;
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(3))
        .WithLoadSimulations(Simulation.Inject(rate: 50, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(10)));

        var result = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        result.AllOkCount.Should().BeGreaterThan(0);
    }
}
