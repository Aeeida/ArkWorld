using Game.Shared.Core.DTOs;
using GameServer.Application.Core.CQRS;
using GameServer.Application.Core.Behaviors;
using GameServer.Grains.Interfaces;
using Orleans;

namespace GameServer.Application.Features.Inventory;

public sealed record AddItemCommand(Guid PlayerId, string ItemId, int Quantity) : ICommand<bool>;

public sealed record RemoveItemCommand(Guid PlayerId, string ItemId, int Quantity) : ICommand<bool>;

public sealed record GetInventoryQuery(Guid PlayerId) : IQuery<IReadOnlyList<InventoryItemDto>>, ICacheableQuery
{
    public string CacheKey => $"inventory:{PlayerId}";
}

public sealed record GetFullInventoryQuery(Guid PlayerId) : IQuery<InventoryDto>, ICacheableQuery
{
    public string CacheKey => $"inventory:full:{PlayerId}";
}

public sealed record MoveItemCommand(Guid PlayerId, string ItemId, int FromSlot, int ToSlot) : ICommand<ItemMoveResultDto>;

public sealed record EquipItemCommand(Guid PlayerId, string ItemId, string Slot) : ICommand<EquipResultDto>;

public sealed record DropItemCommand(Guid PlayerId, string ItemId, int Quantity) : ICommand<DropItemResultDto>;

public sealed class AddItemHandler : ICommandHandler<AddItemCommand, bool>
{
    public Task<bool> Handle(AddItemCommand request, CancellationToken ct)
    {
        // TODO: Delegate to player inventory grain
        return Task.FromResult(true);
    }
}

public sealed class RemoveItemHandler : ICommandHandler<RemoveItemCommand, bool>
{
    public Task<bool> Handle(RemoveItemCommand request, CancellationToken ct)
    {
        return Task.FromResult(true);
    }
}

public sealed class GetInventoryHandler : IQueryHandler<GetInventoryQuery, IReadOnlyList<InventoryItemDto>>
{
    public Task<IReadOnlyList<InventoryItemDto>> Handle(GetInventoryQuery request, CancellationToken ct)
    {
        // TODO: Fetch from grain state
        return Task.FromResult<IReadOnlyList<InventoryItemDto>>([]);
    }
}

public sealed class GetFullInventoryHandler : IQueryHandler<GetFullInventoryQuery, InventoryDto>
{
    public Task<InventoryDto> Handle(GetFullInventoryQuery request, CancellationToken ct)
    {
        // TODO: Fetch from IItemGrain state
        return Task.FromResult(new InventoryDto(request.PlayerId, [], 50, 0));
    }
}

public sealed class MoveItemHandler : ICommandHandler<MoveItemCommand, ItemMoveResultDto>
{
    public Task<ItemMoveResultDto> Handle(MoveItemCommand request, CancellationToken ct)
    {
        // TODO: Delegate to IItemGrain
        return Task.FromResult(new ItemMoveResultDto(true, null));
    }
}

public sealed class EquipItemHandler : ICommandHandler<EquipItemCommand, EquipResultDto>
{
    public Task<EquipResultDto> Handle(EquipItemCommand request, CancellationToken ct)
    {
        // TODO: Delegate to IItemGrain → equip logic
        return Task.FromResult(new EquipResultDto(true, request.ItemId, request.Slot, null));
    }
}

public sealed class DropItemHandler : ICommandHandler<DropItemCommand, DropItemResultDto>
{
    public Task<DropItemResultDto> Handle(DropItemCommand request, CancellationToken ct)
    {
        // TODO: Delegate to IItemGrain → remove + emit ItemChangedEvent
        return Task.FromResult(new DropItemResultDto(true, request.ItemId, request.Quantity, null));
    }
}
