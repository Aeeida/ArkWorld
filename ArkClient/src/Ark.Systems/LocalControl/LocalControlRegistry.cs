namespace Ark.Systems.LocalControl;

/// <summary>
/// Phase 4 本地控制 System 的全局访问点。由 Bootstrap 在初始化阶段填充；
/// 玩家/相机/HUD 节点通过此入口注册自身实体以接收 ECS 推动的预测/相机状态。
/// </summary>
public static class LocalControlRegistry
{
    public static InputIntentCollectSystem? InputIntent { get; private set; }
    public static LocalMovementPredictionSystem? MovementPrediction { get; private set; }
    public static CameraOrbitSystem? CameraOrbit { get; private set; }

    public static void Set(
        InputIntentCollectSystem? input,
        LocalMovementPredictionSystem? movement,
        CameraOrbitSystem? camera)
    {
        InputIntent = input;
        MovementPrediction = movement;
        CameraOrbit = camera;
    }

    public static void Clear()
    {
        InputIntent = null;
        MovementPrediction = null;
        CameraOrbit = null;
    }
}
