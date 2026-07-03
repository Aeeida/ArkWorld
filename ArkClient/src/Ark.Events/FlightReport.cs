namespace Ark.Events;

/// <summary>飞行报告 — 发射结束后的完整总结。</summary>
public sealed class FlightReport
{
    public bool    Success           { get; init; }
    public string  VesselName        { get; init; } = "";
    public float   MaxAltitude       { get; init; }
    public float   MaxSpeed          { get; init; }
    public float   FlightDuration    { get; init; }
    public float   FuelConsumed      { get; init; }
    public float   TotalFuelCapacity { get; init; }
    public float   InitialMass       { get; init; }
    public float   FinalMass         { get; init; }
    public int     StagesUsed        { get; init; }
    public int     TotalStages       { get; init; }
    public float   ImpactSpeed       { get; init; }
    public bool    ParachuteDeployed { get; init; }
    public string  EndReason         { get; init; } = "";

    public float FuelUsedPercent => TotalFuelCapacity > 0 ? (FuelConsumed / TotalFuelCapacity) * 100f : 0;

    public string FormatDuration()
    {
        int min = (int)(FlightDuration / 60f);
        float sec = FlightDuration % 60f;
        return min > 0 ? $"{min}分{sec:F1}秒" : $"{sec:F1}秒";
    }

    public string FormatAltitude()
    {
        return MaxAltitude >= 1000 ? $"{MaxAltitude / 1000f:F1} km" : $"{MaxAltitude:F0} m";
    }
}
