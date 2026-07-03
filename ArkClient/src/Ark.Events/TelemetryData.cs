namespace Ark.Events;

/// <summary>遥测数据结构。</summary>
public struct TelemetryData
{
    public float Altitude;
    public float Velocity;          // 垂直速度 m/s
    public float Speed3D;           // 3D 合速度 m/s
    public float HorizontalSpeed;   // 水平速度 m/s
    public float Acceleration;
    public float TWR;
    public float FuelPercent;
    public float FuelBurnRate;
    public float Heading;           // 偏航 °
    public float Pitch;             // 俯仰 °
    public float Roll;              // 滚转 °
    public float DragForce;
    public string PhaseName;
    public float Mass;
    public float Throttle;
    public int Stage;
    public bool HoverMode;
    public bool EngineCutoff;
}
