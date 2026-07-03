using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Ark.Ecs.Components;

namespace Ark.Bridge.Player;

public partial class RemotePlayerBridge
{
    private sealed class CachedAnimationResource
    {
        public Resource Resource = null!;
        public double LastTouchedAt;
        public string StreamPolicy = string.Empty;
        public int PreloadPriority;
    }

    private readonly Dictionary<string, CachedAnimationResource> _animationResourceCache = new();
    private readonly Dictionary<int, RemoteAnimationState> _pendingAnimationStateWrites = new();
    private readonly Dictionary<int, RemotePresentationFeedbackState> _pendingFeedbackStateWrites = new();
    private readonly Queue<AnimationFragmentBinding> _pendingAnimationPrefetch = new();
    private readonly HashSet<string> _queuedAnimationPaths = new();
    private double _animationStreamingClock;

    private void PumpAnimationStreaming(double delta)
    {
        _animationStreamingClock += delta;
        int budget = 2;
        while (budget-- > 0 && _pendingAnimationPrefetch.Count > 0)
        {
            var binding = _pendingAnimationPrefetch.Dequeue();
            _queuedAnimationPaths.Remove(binding.ResourcePath);
            if (_animationResourceCache.ContainsKey(binding.ResourcePath))
                continue;

            var resource = LoadOrGenerateAnimationResource(binding);
            if (resource is null)
                continue;

            _animationResourceCache[binding.ResourcePath] = new CachedAnimationResource
            {
                Resource = resource,
                LastTouchedAt = _animationStreamingClock,
                StreamPolicy = binding.StreamPolicy,
                PreloadPriority = binding.PreloadPriority,
            };
        }

        if (_animationStreamingClock < 0.5)
            return;

        var stalePaths = new List<string>();
        foreach (var (path, cached) in _animationResourceCache)
        {
            double ttl = cached.StreamPolicy switch
            {
                "Resident" => double.PositiveInfinity,
                "StreamLoop" => 4.5,
                "StreamWarm" => 2.5,
                _ => 1.2,
            };
            if (_animationStreamingClock - cached.LastTouchedAt > ttl)
                stalePaths.Add(path);
        }

        foreach (var path in stalePaths)
            _animationResourceCache.Remove(path);

        _animationStreamingClock = 0.0;
    }

    private static Resource? LoadOrGenerateAnimationResource(in AnimationFragmentBinding binding)
    {
        var loaded = ResourceLoader.Load(binding.ResourcePath);
        if (loaded is not null)
            return loaded;

        if (!binding.ResourcePath.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
            return null;

        return CreateGeneratedPlaceholderAnimation(binding);
    }

    private static Animation CreateGeneratedPlaceholderAnimation(in AnimationFragmentBinding binding)
    {
        var animation = new Animation
        {
            Length = ResolveGeneratedAnimationLength(binding),
            LoopMode = binding.StreamPolicy == "StreamLoop"
                ? Animation.LoopModeEnum.Linear
                : Animation.LoopModeEnum.None,
            ResourceName = $"Generated_{binding.Name}",
        };

        return animation;
    }

    private static float ResolveGeneratedAnimationLength(in AnimationFragmentBinding binding)
    {
        return binding.StreamPolicy switch
        {
            "Resident" => 1.6f,
            "StreamLoop" => 1.2f,
            "StreamWarm" => 0.9f,
            _ => 0.7f,
        };
    }

    private void TrackAnimationStreaming(int ecsEntityId, Node3D visual, AnimationFragmentBinding binding, ref RemoteAnimationState animation)
    {
        if (_animationResourceCache.TryGetValue(binding.ResourcePath, out var cached))
        {
            cached.LastTouchedAt = _animationStreamingClock;
            animation.CacheHits++;
            animation.StreamingState = binding.StreamPolicy == "Resident" ? (byte)3 : (byte)2;
        }
        else
        {
            animation.CacheMisses++;
            animation.StreamingState = 1;
            if (_queuedAnimationPaths.Add(binding.ResourcePath))
                _pendingAnimationPrefetch.Enqueue(binding);
        }

        visual.SetMeta("anim_cache_hits", animation.CacheHits);
        visual.SetMeta("anim_cache_misses", animation.CacheMisses);
        visual.SetMeta("anim_streaming_state", animation.StreamingState);
        _pendingAnimationStateWrites[ecsEntityId] = animation;
    }

    private void QueuePresentationFeedbackWrite(int ecsEntityId, in RemotePresentationFeedbackState feedback)
    {
        _pendingFeedbackStateWrites[ecsEntityId] = feedback;
    }

    private void FlushDeferredEcsStateWrites()
    {
        if (_store is null || _animationEcsFlush is null)
            return;

        _animationEcsFlush.Flush(_pendingAnimationStateWrites, _pendingFeedbackStateWrites);
    }
}
