using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace GameServer.Infrastructure.Monitoring;

public interface IMetricsService
{
    void IncrementCounter(string name, IDictionary<string, string>? tags = null);
    void RecordHistogram(string name, double value, IDictionary<string, string>? tags = null);
    IDisposable StartTimer(string name);
}

public sealed class OpenTelemetryMetricsService : IMetricsService
{
    private static readonly Meter GameMeter = new("GameServer.MMORPG", "1.0.0");
    private static readonly ActivitySource GameActivity = new("GameServer.MMORPG");

    public void IncrementCounter(string name, IDictionary<string, string>? tags = null)
    {
        var counter = GameMeter.CreateCounter<long>(name);
        if (tags is not null)
        {
            var tagList = new TagList();
            foreach (var tag in tags) tagList.Add(tag.Key, tag.Value);
            counter.Add(1, tagList);
        }
        else
        {
            counter.Add(1);
        }
    }

    public void RecordHistogram(string name, double value, IDictionary<string, string>? tags = null)
    {
        var histogram = GameMeter.CreateHistogram<double>(name);
        if (tags is not null)
        {
            var tagList = new TagList();
            foreach (var tag in tags) tagList.Add(tag.Key, tag.Value);
            histogram.Record(value, tagList);
        }
        else
        {
            histogram.Record(value);
        }
    }

    public IDisposable StartTimer(string name) =>
        GameActivity.StartActivity(name) ?? Activity.Current ?? new Activity(name).Start();
}

public static class DependencyInjection
{
    public static IServiceCollection AddGameMonitoring(this IServiceCollection services)
    {
        services.AddSingleton<IMetricsService, OpenTelemetryMetricsService>();

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("GameServer.MMORPG"))
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation()
                .AddSource("GameServer.MMORPG")
                .AddSource("Microsoft.Orleans"))
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddMeter("GameServer.MMORPG")
                .AddMeter("Microsoft.Orleans"));

        return services;
    }
}
