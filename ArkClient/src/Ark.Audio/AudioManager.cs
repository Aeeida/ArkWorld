namespace Ark.Audio;

/// <summary>
/// 音频管理器 — 管理空间音效、BGM、枪声/爆炸/引擎声。
/// TODO: 实现音频池、3D 空间衰减、音量分组。
/// </summary>
public static class AudioManager
{
    /// <summary>播放一次性音效。</summary>
    public static void PlayOneShot(string audioPath, float volume = 1f) { }

    /// <summary>播放 3D 空间音效。</summary>
    public static void PlaySpatial(string audioPath, System.Numerics.Vector3 position, float volume = 1f) { }

    /// <summary>设置背景音乐。</summary>
    public static void SetBGM(string audioPath, float fadeTime = 1f) { }
}
