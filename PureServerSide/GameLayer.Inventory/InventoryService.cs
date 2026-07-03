namespace GameLayer.Inventory;

/// <summary>
/// Server-authoritative inventory management.
/// Mirrors Ark's IInventoryService with slot-based storage.
/// </summary>
public sealed class InventoryManager
{
    private readonly Dictionary<Guid, PlayerInventory> _inventories = [];
    private readonly ItemRegistry _items;

    public InventoryManager(ItemRegistry items)
    {
        _items = items;
    }

    public PlayerInventory GetOrCreate(Guid playerId, int slotCount = 40)
    {
        if (!_inventories.TryGetValue(playerId, out var inv))
        {
            inv = new PlayerInventory(slotCount);
            _inventories[playerId] = inv;
        }
        return inv;
    }

    public bool AddItem(Guid playerId, int itemId, int amount = 1)
    {
        var inv = GetOrCreate(playerId);
        var def = _items.Get(itemId);
        if (def is null) return false;

        // Try to stack into existing slots first
        for (int i = 0; i < inv.Slots.Length; i++)
        {
            ref var slot = ref inv.Slots[i];
            if (slot.ItemId == itemId && slot.Amount < def.Value.StackSize)
            {
                var canAdd = Math.Min(amount, def.Value.StackSize - slot.Amount);
                slot = slot with { Amount = slot.Amount + canAdd };
                amount -= canAdd;
                if (amount <= 0) return true;
            }
        }

        // Find empty slot
        for (int i = 0; i < inv.Slots.Length; i++)
        {
            ref var slot = ref inv.Slots[i];
            if (slot.ItemId == 0)
            {
                var canAdd = Math.Min(amount, def.Value.StackSize);
                slot = new InventorySlot(i, itemId, canAdd, false);
                amount -= canAdd;
                if (amount <= 0) return true;
            }
        }

        return amount <= 0; // false if not all items could be added
    }

    public bool RemoveItem(Guid playerId, int slotIndex, int amount = 1)
    {
        var inv = GetOrCreate(playerId);
        if (slotIndex < 0 || slotIndex >= inv.Slots.Length) return false;

        ref var slot = ref inv.Slots[slotIndex];
        if (slot.ItemId == 0 || slot.Amount < amount) return false;

        slot = slot with { Amount = slot.Amount - amount };
        if (slot.Amount <= 0)
            slot = default;

        return true;
    }

    public int CountItem(Guid playerId, int itemId)
    {
        var inv = GetOrCreate(playerId);
        int total = 0;
        for (int i = 0; i < inv.Slots.Length; i++)
        {
            if (inv.Slots[i].ItemId == itemId)
                total += inv.Slots[i].Amount;
        }

        return total;
    }

    public bool TryConsumeItem(Guid playerId, int itemId, int amount)
    {
        if (amount <= 0)
            return true;

        var inv = GetOrCreate(playerId);
        if (CountItem(playerId, itemId) < amount)
            return false;

        for (int i = 0; i < inv.Slots.Length && amount > 0; i++)
        {
            ref var slot = ref inv.Slots[i];
            if (slot.ItemId != itemId || slot.Amount <= 0)
                continue;

            int consumed = Math.Min(slot.Amount, amount);
            slot = slot with { Amount = slot.Amount - consumed };
            if (slot.Amount <= 0)
                slot = default;
            amount -= consumed;
        }

        return amount <= 0;
    }

    public bool MoveItem(Guid playerId, int fromSlot, int toSlot)
    {
        var inv = GetOrCreate(playerId);
        if (fromSlot < 0 || fromSlot >= inv.Slots.Length) return false;
        if (toSlot < 0 || toSlot >= inv.Slots.Length) return false;

        (inv.Slots[fromSlot], inv.Slots[toSlot]) = (inv.Slots[toSlot], inv.Slots[fromSlot]);

        // Fix slot indices
        inv.Slots[fromSlot] = inv.Slots[fromSlot] with { Index = fromSlot };
        inv.Slots[toSlot] = inv.Slots[toSlot] with { Index = toSlot };

        return true;
    }

    public InventorySlot? GetSlot(Guid playerId, int slotIndex)
    {
        var inv = GetOrCreate(playerId);
        if (slotIndex < 0 || slotIndex >= inv.Slots.Length) return null;
        var slot = inv.Slots[slotIndex];
        return slot.ItemId != 0 ? slot : null;
    }

    public IReadOnlyList<InventorySlot> GetAllSlots(Guid playerId)
    {
        var inv = GetOrCreate(playerId);
        return inv.Slots.Where(s => s.ItemId != 0).ToList().AsReadOnly();
    }
}

public sealed class PlayerInventory
{
    public InventorySlot[] Slots { get; }

    public PlayerInventory(int slotCount)
    {
        Slots = new InventorySlot[slotCount];
        for (int i = 0; i < slotCount; i++)
            Slots[i] = new InventorySlot(i, 0, 0, false);
    }
}

public record struct InventorySlot(int Index, int ItemId, int Amount, bool IsLocked);

public record struct ItemDef(
    int ItemId, string Name, byte Rarity, int StackSize,
    float Weight, string Category, string Description);

/// <summary>
/// Server-side item definition registry.
/// </summary>
public sealed class ItemRegistry
{
    private readonly Dictionary<int, ItemDef> _items = [];

    public void Register(ItemDef item) => _items[item.ItemId] = item;
    public ItemDef? Get(int itemId) => _items.TryGetValue(itemId, out var item) ? item : null;
    public IReadOnlyCollection<ItemDef> GetAll() => _items.Values.ToList().AsReadOnly();

    public void SeedDefaults()
    {
        Register(new ItemDef(1, "Health Kit", 1, 10, 0.5f, "Consumable", "Restores 50 HP"));
        Register(new ItemDef(2, "Ammo Box", 1, 100, 1f, "Ammo", "Standard ammunition"));
        Register(new ItemDef(3, "Iron Ore", 0, 50, 2f, "Material", "Raw iron ore"));
        Register(new ItemDef(4, "Steel Plate", 1, 20, 3f, "Material", "Refined steel plate"));
        Register(new ItemDef(5, "Energy Cell", 2, 20, 0.3f, "Ammo", "Energy weapon ammunition"));
        Register(new ItemDef(6, "Rocket Ammo", 2, 5, 5f, "Ammo", "Rocket launcher ammunition"));
        Register(new ItemDef(7, "Bandage", 0, 20, 0.2f, "Consumable", "Restores 20 HP"));
        Register(new ItemDef(8, "Shield Generator", 3, 1, 5f, "Equipment", "Portable shield device"));
        Register(new ItemDef(9, "Maintenance Kit", 1, 20, 0.8f, "Material", "General service kit for mounted weapons"));
        Register(new ItemDef(10, "Feed Spring", 1, 20, 0.3f, "Material", "Replacement feed assembly spring"));
        Register(new ItemDef(11, "Alignment Shim", 1, 20, 0.2f, "Material", "Fine alignment spacer for turreted weapons"));
    }
}
