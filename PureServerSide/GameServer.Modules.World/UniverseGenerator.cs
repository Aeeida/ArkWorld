using Game.Shared.Core.Universe;
using GameServer.Domain.Entities;
using GameServer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameServer.Modules.World;

/// <summary>
/// 程序化宇宙生成器 — 基于种子确定性地生成完整的宇宙层级树。
///
/// 生成层级：Universe → Galaxy → Region → Constellation → SolarSystem
///           → Star + Planets (+ Moons) + AsteroidBelt + Stations + JumpPoints
///
/// 每个节点的生成参数（名称、位置、类型）均由种子派生，保证确定性。
/// 生成结果持久化到 PostgreSQL LocationNode 表，只在首次启动时生成。
/// </summary>
public sealed class UniverseGenerator
{
    private readonly IServiceProvider _services;
    private readonly ILogger<UniverseGenerator> _logger;
    private readonly long _masterSeed;

    // ── 生成规模参数 ──
    private const int RegionsPerGalaxy = 4;
    private const int ConstellationsPerRegion = 3;
    private const int SystemsPerConstellation = 4;
    private const int MaxPlanetsPerSystem = 6;
    private const int MaxMoonsPerPlanet = 3;
    private const int MaxStationsPerSystem = 2;

    // ── 出生点配置 ──
    private static readonly string[] Factions = ["Federation", "Empire", "Republic", "FreeStates"];

    public UniverseGenerator(IServiceProvider services, ILogger<UniverseGenerator> logger, long masterSeed = 42)
    {
        _services = services;
        _logger = logger;
        _masterSeed = masterSeed;
    }

    /// <summary>
    /// 生成完整宇宙（如果尚未生成）。
    /// </summary>
    public async Task EnsureUniverseGeneratedAsync(CancellationToken ct = default)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        if (await db.Locations.AnyAsync(ct))
        {
            _logger.LogInformation("Universe already generated, checking surface world content");
            await EnsureSurfaceWorldContentAsync(db, ct);
            return;
        }

        _logger.LogInformation("Generating universe with seed {Seed}...", _masterSeed);

        var rng = new SeededRng(_masterSeed);
        var idGen = new IdCounter();

        // ── Universe ──
        var universe = MakeLocation(idGen, null, LocationType.Universe, "Arkiverse", "arkiverse",
            0, 0, 0, 0, 1e15, 1.0f, _masterSeed);
        db.Locations.Add(universe);

        // ── Galaxy ──
        var galaxy = MakeLocation(idGen, universe.Id, LocationType.Galaxy, "银河系", "milky_way",
            0, 0, 0, 1, 1e12, 1.0f, rng.Next());
        galaxy.HierarchyPath = $"{universe.Id}";
        db.Locations.Add(galaxy);

        // ── Regions ──
        for (int r = 0; r < RegionsPerGalaxy; r++)
        {
            var regionSeed = rng.Next();
            var regionRng = new SeededRng(regionSeed);
            var regionName = GenerateRegionName(r, regionRng);

            var region = MakeLocation(idGen, galaxy.Id, LocationType.Region, regionName, $"region_{r}",
                regionRng.NextDouble(-5000, 5000), 0, regionRng.NextDouble(-5000, 5000),
                2, 2000, RegionSecurityLevel(r), regionSeed);
            region.HierarchyPath = $"{universe.Id}/{galaxy.Id}";
            db.Locations.Add(region);

            // ── Constellations ──
            for (int c = 0; c < ConstellationsPerRegion; c++)
            {
                var constSeed = regionRng.Next();
                var constRng = new SeededRng(constSeed);
                var constName = $"{regionName}-{(char)('A' + c)}座";

                var constellation = MakeLocation(idGen, region.Id, LocationType.Constellation, constName, $"const_{r}_{c}",
                    constRng.NextDouble(-500, 500), 0, constRng.NextDouble(-500, 500),
                    3, 500, region.SecurityLevel, constSeed);
                constellation.HierarchyPath = $"{universe.Id}/{galaxy.Id}/{region.Id}";
                db.Locations.Add(constellation);

                // ── Solar Systems ──
                for (int s = 0; s < SystemsPerConstellation; s++)
                {
                    var sysSeed = constRng.Next();
                    GenerateSolarSystem(db, idGen, constellation, s, sysSeed);
                }
            }
        }

        // ── 生成出生点 ──
        await GenerateSpawnPointsAsync(db, ct);

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Universe generated: {Count} locations", idGen.Current - 1);
    }

    private void GenerateSolarSystem(GameDbContext db, IdCounter idGen,
        LocationNode constellation, int index, long seed)
    {
        var rng = new SeededRng(seed);
        var spectral = (SpectralClass)(rng.NextInt(0, 7));
        var sysName = GenerateSystemName(index, rng);

        var system = MakeLocation(idGen, constellation.Id, LocationType.SolarSystem, sysName, $"sys_{constellation.Code}_{index}",
            rng.NextDouble(-100, 100), 0, rng.NextDouble(-100, 100),
            4, 50, constellation.SecurityLevel * (0.8f + rng.NextFloat() * 0.4f), seed);
        system.HierarchyPath = $"{constellation.HierarchyPath}/{constellation.Id}";
        system.SpectralClass = spectral;
        db.Locations.Add(system);

        // ── Star ──
        var star = MakeLocation(idGen, system.Id, LocationType.CelestialBody, $"{sysName}恒星", $"star_{system.Code}",
            0, 0, 0, 5, StarRadius(spectral), system.SecurityLevel, rng.Next());
        star.BodyType = CelestialBodyType.Star;
        star.SpectralClass = spectral;
        star.BaseTemperature = StarTemperature(spectral);
        star.AtmosphereDensity = 0;
        star.GravityMultiplier = StarGravity(spectral);
        star.HierarchyPath = $"{system.HierarchyPath}/{system.Id}";
        db.Locations.Add(star);

        // ── Planets ──
        int planetCount = rng.NextInt(2, MaxPlanetsPerSystem + 1);
        for (int p = 0; p < planetCount; p++)
        {
            var planetSeed = rng.Next();
            GeneratePlanet(db, idGen, system, p, planetSeed, spectral);
        }

        // ── Stations ──
        int stationCount = rng.NextInt(0, MaxStationsPerSystem + 1);
        for (int st = 0; st < stationCount; st++)
        {
            var stSeed = rng.Next();
            var stRng = new SeededRng(stSeed);
            var stName = $"{sysName}空间站-{st + 1}号";

            var station = MakeLocation(idGen, system.Id, LocationType.Station, stName, $"station_{system.Code}_{st}",
                stRng.NextDouble(-20, 20), stRng.NextDouble(-5, 5), stRng.NextDouble(-20, 20),
                5, 0.5, system.SecurityLevel, stSeed);
            station.HierarchyPath = $"{system.HierarchyPath}/{system.Id}";
            station.AtmosphereDensity = 1.0f;
            station.GravityMultiplier = 1.0f;
            station.BaseTemperature = 293f;
            db.Locations.Add(station);
        }

        // ── World Environment State ──
        var envState = WorldEnvironmentState.Create(idGen.NextId());
        envState.LocationId = system.Id;
        envState.WeatherId = 0;
        envState.WeatherIntensity = 0;
        envState.Temperature = 2.7f; // cosmic background
        envState.TimeOfDay = 12f;
        envState.TimeScale = 1f;
        db.WorldEnvironmentStates.Add(envState);
    }

    private void GeneratePlanet(GameDbContext db, IdCounter idGen,
        LocationNode system, int index, long seed, SpectralClass starType)
    {
        var rng = new SeededRng(seed);
        var orbitRadius = 2.0 + index * 3.0 + rng.NextDouble(-0.5, 0.5);
        var angle = rng.NextDouble(0, Math.PI * 2);

        var bodyType = index < 3 ? CelestialBodyType.RockyPlanet :
                       index < 5 ? CelestialBodyType.GasGiant : CelestialBodyType.IcePlanet;

        var planetName = $"{system.Name} {RomanNumeral(index + 1)}";
        var biome = bodyType switch
        {
            CelestialBodyType.RockyPlanet => SelectBiome(rng),
            CelestialBodyType.IcePlanet => "tundra",
            _ => null
        };

        var planet = MakeLocation(idGen, system.Id, LocationType.CelestialBody, planetName, $"planet_{system.Code}_{index}",
            orbitRadius * Math.Cos(angle), 0, orbitRadius * Math.Sin(angle),
            5, PlanetRadius(bodyType), system.SecurityLevel, seed);
        planet.BodyType = bodyType;
        planet.BiomeId = biome;
        planet.AtmosphereDensity = bodyType == CelestialBodyType.RockyPlanet ? rng.NextFloat(0.3f, 1.2f) : 0;
        planet.GravityMultiplier = bodyType == CelestialBodyType.RockyPlanet ? rng.NextFloat(0.5f, 1.5f) : bodyType == CelestialBodyType.GasGiant ? 2.5f : 0.3f;
        planet.BaseTemperature = PlanetTemperature(index, starType);
        planet.TerrainSeed = rng.Next();
        planet.HierarchyPath = $"{system.HierarchyPath}/{system.Id}";
        db.Locations.Add(planet);

        // ── Planet Surface Zone (for landable rocky planets) ──
        if (bodyType == CelestialBodyType.RockyPlanet)
        {
            var surface = MakeLocation(idGen, planet.Id, LocationType.PlanetSurface,
                $"{planetName} 地表", $"surface_{planet.Code}",
                0, 0, 0, 6, PlanetRadius(bodyType), system.SecurityLevel, rng.Next());
            surface.BiomeId = biome;
            surface.TerrainSeed = planet.TerrainSeed;
            surface.AtmosphereDensity = planet.AtmosphereDensity;
            surface.GravityMultiplier = planet.GravityMultiplier;
            surface.BaseTemperature = planet.BaseTemperature;
            surface.HierarchyPath = $"{planet.HierarchyPath}/{planet.Id}";
            db.Locations.Add(surface);

            // ── Environment state for surface ──
            var surfEnv = WorldEnvironmentState.Create(idGen.NextId());
            surfEnv.LocationId = surface.Id;
            surfEnv.WeatherId = (byte)rng.NextInt(0, 5);
            surfEnv.WeatherIntensity = rng.NextFloat(0, 0.5f);
            surfEnv.Temperature = planet.BaseTemperature;
            surfEnv.FogDensity = rng.NextFloat(0, 0.3f);
            surfEnv.TimeOfDay = 9.5f;
            surfEnv.TimeScale = 1f;
            db.WorldEnvironmentStates.Add(surfEnv);

            // ── Surface spawns (NPC/monsters/villages/etc.) ──
            GenerateSurfaceSpawns(db, idGen, surface, rng);
        }

        // ── Moons ──
        int moonCount = bodyType == CelestialBodyType.GasGiant ? rng.NextInt(1, MaxMoonsPerPlanet + 1) : rng.NextInt(0, 2);
        for (int m = 0; m < moonCount; m++)
        {
            var moonSeed = rng.Next();
            var moonRng = new SeededRng(moonSeed);
            var moonOrbit = 0.5 + m * 0.3;
            var moonAngle = moonRng.NextDouble(0, Math.PI * 2);

            var moon = MakeLocation(idGen, planet.Id, LocationType.CelestialBody,
                $"{planetName} 卫星-{(char)('a' + m)}", $"moon_{planet.Code}_{m}",
                moonOrbit * Math.Cos(moonAngle), 0, moonOrbit * Math.Sin(moonAngle),
                6, 0.3, system.SecurityLevel, moonSeed);
            moon.BodyType = CelestialBodyType.Moon;
            moon.GravityMultiplier = moonRng.NextFloat(0.1f, 0.4f);
            moon.HierarchyPath = $"{planet.HierarchyPath}/{planet.Id}";
            db.Locations.Add(moon);
        }
    }

    private void GenerateSurfaceSpawns(GameDbContext db, IdCounter idGen, LocationNode surface, SeededRng rng)
    {
        // ── 主城/新手村 ──
        var townRng = new SeededRng(rng.Next());
        var town = WorldSpawnEntry.Create(idGen.NextId());
        town.LocationId = surface.Id;
        town.SpawnType = "town";
        town.TemplateId = "town_starter";
        town.DisplayName = $"{surface.Name}新手村";
        town.LocalX = townRng.NextDouble(-200, 200);
        town.LocalY = 0;
        town.LocalZ = townRng.NextDouble(-200, 200);
        town.Level = 1;
        town.RespawnSeconds = 0;
        town.MetadataJson = System.Text.Json.JsonSerializer.Serialize(new { Category = "starter_town", Radius = 120.0, AttachToTerrain = true });
        db.WorldSpawns.Add(town);

        // ── 村庄 / 哨站 ──
        int villageCount = rng.NextInt(2, 5);
        var settlementCenters = new List<(double X, double Z)> { (town.LocalX, town.LocalZ) };
        for (int v = 0; v < villageCount; v++)
        {
            var village = WorldSpawnEntry.Create(idGen.NextId());
            village.LocationId = surface.Id;
            village.SpawnType = "village";
            village.TemplateId = v % 2 == 0 ? "village_farmstead" : "outpost_watch";
            village.DisplayName = $"{surface.Name}聚落-{v + 1}";
            village.LocalX = rng.NextDouble(-1400, 1400);
            village.LocalY = 0;
            village.LocalZ = rng.NextDouble(-1400, 1400);
            village.Level = rng.NextInt(1, 4);
            village.RespawnSeconds = 0;
            village.MetadataJson = System.Text.Json.JsonSerializer.Serialize(new { Category = "settlement", Radius = 80.0, AttachToTerrain = true });
            db.WorldSpawns.Add(village);
            settlementCenters.Add((village.LocalX, village.LocalZ));
        }

        // ── 地标 / 废墟 / 篝火 ──
        int landmarkCount = rng.NextInt(6, 14);
        for (int i = 0; i < landmarkCount; i++)
        {
            var landmark = WorldSpawnEntry.Create(idGen.NextId());
            landmark.LocationId = surface.Id;
            landmark.SpawnType = "landmark";
            landmark.TemplateId = LandmarkTemplates[rng.NextInt(0, LandmarkTemplates.Length)];
            landmark.DisplayName = $"{surface.Name}地标-{i + 1}";
            landmark.LocalX = rng.NextDouble(-1800, 1800);
            landmark.LocalY = 0;
            landmark.LocalZ = rng.NextDouble(-1800, 1800);
            landmark.Level = 1;
            landmark.RespawnSeconds = 0;
            db.WorldSpawns.Add(landmark);
        }

        // ── NPC ──
        int npcCount = rng.NextInt(16, 32);
        for (int n = 0; n < npcCount; n++)
        {
            var center = settlementCenters[rng.NextInt(0, settlementCenters.Count)];
            var npc = WorldSpawnEntry.Create(idGen.NextId());
            npc.LocationId = surface.Id;
            npc.SpawnType = "npc";
            npc.TemplateId = NpcTemplates[rng.NextInt(0, NpcTemplates.Length)];
            npc.DisplayName = GenerateNpcName(rng);
            npc.LocalX = center.X + rng.NextDouble(-90, 90);
            npc.LocalY = 0;
            npc.LocalZ = center.Z + rng.NextDouble(-90, 90);
            npc.Level = rng.NextInt(1, 10);
            npc.RespawnSeconds = 0;
            npc.MetadataJson = BuildMovementMetadata(rng, rng.NextDouble(18, 60), rng.NextDouble(0.6, 1.8), "wander");
            db.WorldSpawns.Add(npc);
        }

        // ── 野生动物 / 中立生物 ──
        int wildlifeCount = rng.NextInt(12, 28);
        for (int w = 0; w < wildlifeCount; w++)
        {
            var wildlife = WorldSpawnEntry.Create(idGen.NextId());
            wildlife.LocationId = surface.Id;
            wildlife.SpawnType = "wildlife";
            wildlife.TemplateId = WildlifeTemplates[rng.NextInt(0, WildlifeTemplates.Length)];
            wildlife.DisplayName = $"生态生物-{w + 1}";
            wildlife.LocalX = rng.NextDouble(-2200, 2200);
            wildlife.LocalY = 0;
            wildlife.LocalZ = rng.NextDouble(-2200, 2200);
            wildlife.Level = rng.NextInt(1, 8);
            wildlife.RespawnSeconds = 45;
            wildlife.MetadataJson = BuildMovementMetadata(rng, rng.NextDouble(35, 120), rng.NextDouble(0.8, 2.2), "graze");
            db.WorldSpawns.Add(wildlife);
        }

        // ── 怪物 ──
        int monsterCount = rng.NextInt(20, 42);
        for (int m = 0; m < monsterCount; m++)
        {
            var monster = WorldSpawnEntry.Create(idGen.NextId());
            monster.LocationId = surface.Id;
            monster.SpawnType = "monster";
            monster.TemplateId = MonsterTemplates[rng.NextInt(0, MonsterTemplates.Length)];
            monster.DisplayName = $"{surface.BiomeId ?? "荒野"}威胁-{m + 1}";
            monster.LocalX = rng.NextDouble(-2600, 2600);
            monster.LocalY = 0;
            monster.LocalZ = rng.NextDouble(-2600, 2600);
            monster.Level = rng.NextInt(1, 20);
            monster.RespawnSeconds = rng.NextInt(30, 300);
            monster.Rotation = rng.NextFloat(0, MathF.PI * 2);
            monster.MetadataJson = BuildMovementMetadata(rng, rng.NextDouble(45, 180), rng.NextDouble(1.0, 3.2), "patrol");
            db.WorldSpawns.Add(monster);
        }

        // ── 资源点 ──
        int resourceCount = rng.NextInt(10, 22);
        for (int r = 0; r < resourceCount; r++)
        {
            var res = WorldSpawnEntry.Create(idGen.NextId());
            res.LocationId = surface.Id;
            res.SpawnType = "resource_node";
            res.TemplateId = ResourceTemplates[rng.NextInt(0, ResourceTemplates.Length)];
            res.LocalX = rng.NextDouble(-600, 600);
            res.LocalY = 0;
            res.LocalZ = rng.NextDouble(-600, 600);
            res.Level = 1;
            res.RespawnSeconds = rng.NextInt(60, 600);
            db.WorldSpawns.Add(res);
        }

        // ── 任务点 ──
        int questCount = rng.NextInt(2, 6);
        for (int q = 0; q < questCount; q++)
        {
            var quest = WorldSpawnEntry.Create(idGen.NextId());
            quest.LocationId = surface.Id;
            quest.SpawnType = "quest_giver";
            quest.TemplateId = "quest_board";
            quest.DisplayName = $"告示板-{q + 1}";
            quest.LocalX = rng.NextDouble(-300, 300);
            quest.LocalY = 0;
            quest.LocalZ = rng.NextDouble(-300, 300);
            quest.Level = 1;
            quest.RespawnSeconds = 0;
            db.WorldSpawns.Add(quest);
        }
    }

    private async Task GenerateSpawnPointsAsync(GameDbContext db, CancellationToken ct)
    {
        // 在已生成的行星表面中找出安全度最高的若干位置作为各阵营出生点
        var surfaces = db.Locations.Local
            .Where(l => l.LocationType == LocationType.PlanetSurface && l.SecurityLevel >= 0.7f)
            .OrderByDescending(l => l.SecurityLevel)
            .Take(Factions.Length * 2)
            .ToList();

        for (int i = 0; i < Math.Min(Factions.Length, surfaces.Count); i++)
        {
            var surface = surfaces[i];
            // 在该地表的城镇附近设定出生点
            var town = db.WorldSpawns.Local
                .FirstOrDefault(s => s.LocationId == surface.Id && s.SpawnType == "town");

            var spawnX = town?.LocalX ?? 0;
            var spawnZ = town?.LocalZ ?? 0;

            // 标记到 MetadataJson
            surface.MetadataJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                SpawnPoint = true,
                Faction = Factions[i],
                SpawnX = spawnX,
                SpawnZ = spawnZ,
                ScatterRadius = 50.0
            });
        }

        await Task.CompletedTask;
    }

    private async Task EnsureSurfaceWorldContentAsync(GameDbContext db, CancellationToken ct)
    {
        var surfaces = await db.Locations
            .Where(l => l.LocationType == LocationType.PlanetSurface)
            .ToListAsync(ct);
        if (surfaces.Count == 0)
            return;

        var nextSpawnId = (await db.WorldSpawns.MaxAsync(s => (long?)s.Id, ct) ?? 0) + 1;
        var idGen = new IdCounter(nextSpawnId);
        var enrichedSurfaces = 0;

        foreach (var surface in surfaces)
        {
            var existing = await db.WorldSpawns
                .Where(s => s.LocationId == surface.Id)
                .ToListAsync(ct);

            if (!NeedsSurfaceEnrichment(existing))
                continue;

            var rng = new SeededRng(surface.TerrainSeed ?? surface.Seed);
            BackfillSurfaceSpawns(db, idGen, surface, rng, existing);
            enrichedSurfaces++;
        }

        if (enrichedSurfaces > 0)
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Surface world content backfilled for {Count} surfaces", enrichedSurfaces);
        }
    }

    private static bool NeedsSurfaceEnrichment(IReadOnlyCollection<WorldSpawnEntry> existing)
    {
        var counts = existing.GroupBy(s => s.SpawnType)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        return GetCount(counts, "town") < 1
            || GetCount(counts, "village") < 2
            || GetCount(counts, "npc") < 16
            || GetCount(counts, "wildlife") < 12
            || GetCount(counts, "monster") < 20
            || GetCount(counts, "landmark") < 6
            || GetCount(counts, "resource_node") < 10
            || GetCount(counts, "quest_giver") < 2;
    }

    private static void BackfillSurfaceSpawns(GameDbContext db, IdCounter idGen, LocationNode surface, SeededRng rng, List<WorldSpawnEntry> existing)
    {
        var settlementCenters = existing
            .Where(s => s.SpawnType is "town" or "village")
            .Select(s => (s.LocalX, s.LocalZ))
            .ToList();

        EnsureSpawnCount(db, idGen, existing, settlementCenters, surface, rng, "town", 1, () =>
        {
            var townRng = new SeededRng(rng.Next());
            return CreateSpawn(idGen, surface.Id, "town", "town_starter", $"{surface.Name}新手村",
                townRng.NextDouble(-200, 200), 0, townRng.NextDouble(-200, 200), 1, 0,
                System.Text.Json.JsonSerializer.Serialize(new { Category = "starter_town", Radius = 120.0 }));
        }, addSettlementCenter: true);

        EnsureSpawnCount(db, idGen, existing, settlementCenters, surface, rng, "village", 2, () =>
            CreateSpawn(idGen, surface.Id, "village",
                rng.NextInt(0, 2) == 0 ? "village_farmstead" : "outpost_watch",
                $"{surface.Name}聚落-{GetCount(existing, "village") + 1}",
                rng.NextDouble(-1400, 1400), 0, rng.NextDouble(-1400, 1400), rng.NextInt(1, 4), 0,
                System.Text.Json.JsonSerializer.Serialize(new { Category = "settlement", Radius = 80.0 })),
            addSettlementCenter: true);

        EnsureSpawnCount(db, idGen, existing, settlementCenters, surface, rng, "landmark", 6, () =>
            CreateSpawn(idGen, surface.Id, "landmark",
                LandmarkTemplates[rng.NextInt(0, LandmarkTemplates.Length)],
                $"{surface.Name}地标-{GetCount(existing, "landmark") + 1}",
                rng.NextDouble(-1800, 1800), 0, rng.NextDouble(-1800, 1800), 1, 0));

        EnsureSpawnCount(db, idGen, existing, settlementCenters, surface, rng, "npc", 16, () =>
        {
            var center = settlementCenters.Count > 0 ? settlementCenters[rng.NextInt(0, settlementCenters.Count)] : (0d, 0d);
            return CreateSpawn(idGen, surface.Id, "npc",
                NpcTemplates[rng.NextInt(0, NpcTemplates.Length)],
                GenerateNpcName(rng),
                center.Item1 + rng.NextDouble(-90, 90), 0, center.Item2 + rng.NextDouble(-90, 90),
                rng.NextInt(1, 10), 0,
                BuildMovementMetadata(rng, rng.NextDouble(18, 60), rng.NextDouble(0.6, 1.8), "wander"));
        });

        EnsureSpawnCount(db, idGen, existing, settlementCenters, surface, rng, "wildlife", 12, () =>
            CreateSpawn(idGen, surface.Id, "wildlife",
                WildlifeTemplates[rng.NextInt(0, WildlifeTemplates.Length)],
                $"生态生物-{GetCount(existing, "wildlife") + 1}",
                rng.NextDouble(-2200, 2200), 0, rng.NextDouble(-2200, 2200),
                rng.NextInt(1, 8), 45,
                BuildMovementMetadata(rng, rng.NextDouble(35, 120), rng.NextDouble(0.8, 2.2), "graze")));

        EnsureSpawnCount(db, idGen, existing, settlementCenters, surface, rng, "monster", 20, () =>
            CreateSpawn(idGen, surface.Id, "monster",
                MonsterTemplates[rng.NextInt(0, MonsterTemplates.Length)],
                $"{surface.BiomeId ?? "荒野"}威胁-{GetCount(existing, "monster") + 1}",
                rng.NextDouble(-2600, 2600), 0, rng.NextDouble(-2600, 2600),
                rng.NextInt(1, 20), rng.NextInt(30, 300),
                BuildMovementMetadata(rng, rng.NextDouble(45, 180), rng.NextDouble(1.0, 3.2), "patrol"),
                rotation: rng.NextFloat(0, MathF.PI * 2)));

        EnsureSpawnCount(db, idGen, existing, settlementCenters, surface, rng, "resource_node", 10, () =>
            CreateSpawn(idGen, surface.Id, "resource_node",
                ResourceTemplates[rng.NextInt(0, ResourceTemplates.Length)],
                null,
                rng.NextDouble(-600, 600), 0, rng.NextDouble(-600, 600), 1, rng.NextInt(60, 600)));

        EnsureSpawnCount(db, idGen, existing, settlementCenters, surface, rng, "quest_giver", 2, () =>
            CreateSpawn(idGen, surface.Id, "quest_giver", "quest_board", $"告示板-{GetCount(existing, "quest_giver") + 1}",
                rng.NextDouble(-300, 300), 0, rng.NextDouble(-300, 300), 1, 0));
    }

    private static void EnsureSpawnCount(
        GameDbContext db,
        IdCounter idGen,
        List<WorldSpawnEntry> existing,
        List<(double LocalX, double LocalZ)> settlementCenters,
        LocationNode surface,
        SeededRng rng,
        string spawnType,
        int minimum,
        Func<WorldSpawnEntry> factory,
        bool addSettlementCenter = false)
    {
        while (GetCount(existing, spawnType) < minimum)
        {
            var spawn = factory();
            db.WorldSpawns.Add(spawn);
            existing.Add(spawn);
            if (addSettlementCenter)
                settlementCenters.Add((spawn.LocalX, spawn.LocalZ));
        }
    }

    private static WorldSpawnEntry CreateSpawn(
        IdCounter idGen,
        long locationId,
        string spawnType,
        string templateId,
        string? displayName,
        double x,
        double y,
        double z,
        int level,
        int respawnSeconds,
        string? metadataJson = null,
        float rotation = 0f)
    {
        var spawn = WorldSpawnEntry.Create(idGen.NextId());
        spawn.LocationId = locationId;
        spawn.SpawnType = spawnType;
        spawn.TemplateId = templateId;
        spawn.DisplayName = displayName;
        spawn.LocalX = x;
        spawn.LocalY = y;
        spawn.LocalZ = z;
        spawn.Level = level;
        spawn.RespawnSeconds = respawnSeconds;
        spawn.MetadataJson = metadataJson;
        spawn.Rotation = rotation;
        return spawn;
    }

    private static int GetCount(IReadOnlyDictionary<string, int> counts, string key) =>
        counts.TryGetValue(key, out var count) ? count : 0;

    private static int GetCount(IEnumerable<WorldSpawnEntry> entries, string spawnType) =>
        entries.Count(s => string.Equals(s.SpawnType, spawnType, StringComparison.OrdinalIgnoreCase));

    // ══════════════════════════════════════════════════════════════════
    // 辅助方法
    // ══════════════════════════════════════════════════════════════════

    private static LocationNode MakeLocation(IdCounter idGen, long? parentId, LocationType type,
        string name, string code, double x, double y, double z, int depth, double boundsRadius,
        float security, long seed)
    {
        var node = LocationNode.Create(idGen.NextId());
        node.ParentLocationId = parentId;
        node.LocationType = type;
        node.Name = name;
        node.Code = code;
        node.LocalX = x;
        node.LocalY = y;
        node.LocalZ = z;
        node.Depth = depth;
        node.BoundsRadius = boundsRadius;
        node.SecurityLevel = Math.Clamp(security, 0f, 1f);
        node.Seed = seed;
        node.IsGenerated = true;
        node.Scale = type switch
        {
            LocationType.Universe => 1e12,
            LocationType.Galaxy => 1e9,
            LocationType.Region => 1e6,
            LocationType.Constellation => 1e4,
            LocationType.SolarSystem => 1e3,
            LocationType.CelestialBody => 1.0,
            LocationType.PlanetSurface => 1.0,
            LocationType.Station => 1.0,
            _ => 1.0
        };
        return node;
    }

    private static float RegionSecurityLevel(int index) => index switch
    {
        0 => 0.9f,
        1 => 0.7f,
        2 => 0.4f,
        _ => 0.1f
    };

    private static string GenerateRegionName(int index, SeededRng rng)
    {
        string[] prefixes = ["星云", "深渊", "光辉", "幽暗"];
        string[] suffixes = ["星域", "走廊", "前线", "核心区"];
        return $"{prefixes[index % prefixes.Length]}{suffixes[rng.NextInt(0, suffixes.Length)]}";
    }

    private static string GenerateSystemName(int index, SeededRng rng)
    {
        string[] prefixes = ["阿尔法", "贝塔", "伽马", "德尔塔", "泽塔", "希格玛", "奥米伽", "塞坦"];
        string[] suffixes = ["星系", "系统", "区"];
        return $"{prefixes[rng.NextInt(0, prefixes.Length)]}-{index + 1}{suffixes[rng.NextInt(0, suffixes.Length)]}";
    }

    private static string GenerateNpcName(SeededRng rng)
    {
        string[] first = ["张", "李", "王", "赵", "艾", "诺", "雷", "萨"];
        string[] last = ["铁匠", "药师", "猎人", "商人", "学者", "守卫", "船长", "矿工"];
        return $"{first[rng.NextInt(0, first.Length)]}{last[rng.NextInt(0, last.Length)]}";
    }

    private static string RomanNumeral(int n) => n switch
    {
        1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI",
        7 => "VII", 8 => "VIII", 9 => "IX", 10 => "X", _ => n.ToString()
    };

    private static string SelectBiome(SeededRng rng)
    {
        string[] biomes = ["temperate", "desert", "tundra", "tropical", "volcanic", "oceanic", "forest", "plains"];
        return biomes[rng.NextInt(0, biomes.Length)];
    }

    private static string BuildMovementMetadata(SeededRng rng, double patrolRadius, double moveSpeed, string mode) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            AttachToTerrain = true,
            Mobile = true,
            Mode = mode,
            PatrolRadius = patrolRadius,
            MoveSpeed = moveSpeed,
            PauseSecondsMin = rng.NextDouble(0.5, 2.0),
            PauseSecondsMax = rng.NextDouble(2.0, 6.0)
        });

    private static double StarRadius(SpectralClass sc) => sc switch
    {
        SpectralClass.O => 10.0, SpectralClass.B => 5.0, SpectralClass.A => 2.0,
        SpectralClass.F => 1.5, SpectralClass.G => 1.0, SpectralClass.K => 0.8,
        SpectralClass.M => 0.5, _ => 1.0
    };

    private static float StarTemperature(SpectralClass sc) => sc switch
    {
        SpectralClass.O => 40000, SpectralClass.B => 20000, SpectralClass.A => 10000,
        SpectralClass.F => 7000, SpectralClass.G => 5800, SpectralClass.K => 4500,
        SpectralClass.M => 3200, _ => 5800
    };

    private static float StarGravity(SpectralClass sc) => sc switch
    {
        SpectralClass.O => 50f, SpectralClass.B => 20f, SpectralClass.A => 5f,
        SpectralClass.G => 1f, SpectralClass.M => 0.3f, _ => 1f
    };

    private static double PlanetRadius(CelestialBodyType bt) => bt switch
    {
        CelestialBodyType.GasGiant => 5.0, CelestialBodyType.RockyPlanet => 1.0,
        CelestialBodyType.IcePlanet => 0.8, _ => 0.5
    };

    private static float PlanetTemperature(int orbitIndex, SpectralClass star)
    {
        float baseStar = StarTemperature(star);
        float distance = 0.5f + orbitIndex * 0.8f;
        return baseStar / (distance * distance * 100f);
    }

    private static readonly string[] NpcTemplates =
        ["npc_blacksmith", "npc_merchant", "npc_healer", "npc_guard", "npc_miner", "npc_pilot", "npc_scholar"];
    private static readonly string[] WildlifeTemplates =
        ["wildlife_grazer", "wildlife_flockbird", "wildlife_packbeast", "wildlife_lizard"];
    private static readonly string[] MonsterTemplates =
        ["monster_wolf", "monster_spider", "monster_drone", "monster_golem", "monster_dragon", "monster_bandit", "monster_pirate"];
    private static readonly string[] LandmarkTemplates =
        ["landmark_ruins", "landmark_campfire", "landmark_watchtower", "landmark_obelisk", "landmark_shrine"];
    private static readonly string[] ResourceTemplates =
        ["resource_iron", "resource_crystal", "resource_fuel", "resource_wood", "resource_herb", "resource_titanium"];
}

/// <summary>
/// ID 计数器 — 替代 ref long，解决 async 方法不能使用 ref 参数的限制。
/// </summary>
public sealed class IdCounter
{
    private long _current;
    public IdCounter(long start = 1) => _current = start;
    public long Current => _current;
    public long NextId() => _current++;
}

/// <summary>
/// 确定性随机数生成器 — 基于种子的伪随机序列。
/// </summary>
public sealed class SeededRng
{
    private long _state;

    public SeededRng(long seed) => _state = seed == 0 ? 1 : seed;

    public long Next()
    {
        _state ^= _state << 13;
        _state ^= _state >> 7;
        _state ^= _state << 17;
        return _state;
    }

    public int NextInt(int min, int max)
    {
        if (min >= max) return min;
        var n = Next();
        return min + (int)(((n & 0x7FFFFFFF) % (long)(max - min)));
    }

    public float NextFloat() => (float)((Next() & 0x7FFFFFFF) / (double)0x7FFFFFFF);
    public float NextFloat(float min, float max) => min + NextFloat() * (max - min);
    public double NextDouble(double min, double max) => min + NextFloat() * (max - min);
}
