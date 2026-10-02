namespace MuBredaEditor;

public readonly record struct ClassEvolution(int Stage, string Name, int Code, int Quest)
{
    public string Label => $"{Stage} · {Name} ({(Stage == 1 ? "base" : $"quest {Quest}")})";
}

public static class GameData
{
    /// <summary>DB Class = race * 16 + evolution. Evolution bits are assumed, not verified in game:
    /// base 0, 2nd +1, 3rd +2, 4th +4, 5th +8. Races without a 2nd stage skip that bit.</summary>
    public static readonly string[] Races =
    [
        "Dark Wizard", "Dark Knight", "Elf", "Magic Gladiator", "Dark Lord", "Summoner", "Rage Fighter",
        "Grow Lancer", "Rune Wizard", "Slayer", "Gun Crusher", "Kundun Mephis", "Lemuria",
        "Illusion Knight", "Alchemist", "Crusader",
    ];

    /// <summary>Client names for base / 2nd / 3rd / 4th / 5th. The 2nd entry is unused for MG, DL, RF and GL.</summary>
    private static readonly string[][] EvolutionNames =
    [
        ["Dark Wizard", "Soul Master", "Grand Master", "Soul Wizard", "Darkness Wizard"],
        ["Dark Knight", "Blade Knight", "Blade Master", "Dragon Knight", "Ignition Knight"],
        ["Elf", "Muse Elf", "High Elf", "Noble Elf", "Royal Elf"],
        ["Magic Gladiator", "-", "Dual Master", "Magic Knight", "Duple Knight"],
        ["Dark Lord", "-", "Lord Emperor", "Empire Lord", "Force Empire"],
        ["Summoner", "Bloody Summoner", "Dimension Master", "Dimension Summoner", "Endless Summoner"],
        ["Rage Fighter", "-", "Fist Master", "Fist Blazer", "Bloody Fighter"],
        ["Grow Lancer", "-", "Mirage Lancer", "Shining Lancer", "Arcane Lancer"],
        ["Rune Mage", "Rune Spell Master", "Rune Grand Master", "Majestic Rune Mage", "Infinity Rune Wizard"],
        ["Slayer", "Royal Slayer", "Master Slayer", "Slaughterer", "Rogue Slayer"],
        ["Gun Crusher", "Gun Breaker", "Master Gun Breaker", "Heist Gun Crusher", "Magnus Gun Crusher"],
        ["White Wizard Kundun", "Light Master", "Shine Wizard", "Luminous Wizard", "Glory Wizard"],
        ["Mage Lemuria", "War Mage", "Archmage", "Mystic Mage", "Battle Mage"],
        ["Illusion Knight", "Mirage Knight", "Illusion Master", "Mystic Knight", "Phantom Pain Knight"],
        ["Alchemist", "Alchemist Mage", "Alchemist Master", "Alchemist Force", "Alchemist Creator"],
        ["Crusader", "Impact Crusader", "Master Paladin", "Sacred Paladin", "Templar Commander"],
    ];

    /// <summary>Magic Gladiator, Dark Lord, Rage Fighter and Grow Lancer have no 2nd class.</summary>
    public static bool HasSecondEvolution(int race) => race is not (3 or 4 or 6 or 7);

    public static IReadOnlyList<ClassEvolution> Evolutions(int race)
    {
        if (race < 0 || race >= EvolutionNames.Length) return [];
        var names = EvolutionNames[race];
        var second = HasSecondEvolution(race);
        var list = new List<ClassEvolution>(second ? 5 : 4);
        for (var stage = 1; stage <= 5; stage++)
        {
            if (stage == 2 && !second) continue;
            var code = stage switch
            {
                1 => 0,
                2 => 1,
                3 => second ? 3 : 2,
                4 => second ? 7 : 6,
                _ => second ? 15 : 14,
            };
            var quest = stage switch { 2 => 150, 3 => 400, 4 => 800, 5 => 1200, _ => 0 };
            list.Add(new ClassEvolution(stage, names[stage - 1], code, quest));
        }
        return list;
    }

    public static string ClassName(int dbClass)
    {
        var race = dbClass >> 4;
        var bits = dbClass & 0x0F;
        if ((uint)race < (uint)Races.Length)
            foreach (var e in Evolutions(race))
                if (e.Code == bits) return e.Name;
        return $"código {dbClass} (desconocido)";
    }

    public static readonly Dictionary<int, string> Maps = new()
    {
        [0] = "Lorencia", [1] = "Dungeon", [2] = "Devias", [3] = "Noria", [4] = "Lost Tower",
        [6] = "Arena", [7] = "Atlans", [8] = "Tarkan", [10] = "Icarus", [30] = "Valley of Loren",
        [31] = "Land of Trials", [33] = "Aida", [34] = "Crywolf", [37] = "Kanturu 1", [38] = "Kanturu 2",
        [41] = "Barracks", [42] = "Refuge", [51] = "Elbeland", [56] = "Swamp of Calmness",
        [57] = "Raklion", [62] = "Santa Village", [63] = "Vulcanus", [79] = "Loren Market",
        [80] = "Karutan 1", [81] = "Karutan 2", [91] = "Acheron", [95] = "Debenter",
        [100] = "Urk Mountain", [110] = "Nars", [112] = "Ferea", [113] = "Nixies Lake",
        [121] = "Deep Dungeon", [122] = "Kubera Mine",
    };

    public static string MapName(int map) => Maps.TryGetValue(map, out var n) ? n : $"Mapa {map}";
}
