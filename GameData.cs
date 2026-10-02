namespace MuBredaEditor;

public static class GameData
{
    /// <summary>DB Class = race * 16 + evolution.</summary>
    public static readonly string[] Races =
    [
        "Dark Wizard", "Dark Knight", "Elf", "Magic Gladiator", "Dark Lord", "Summoner", "Rage Fighter",
        "Grow Lancer", "Rune Wizard", "Slayer", "Gun Crusher", "Kundun Mephis", "Lemuria",
        "Illusion Knight", "Alchemist", "Crusader",
    ];

    public static string ClassName(int dbClass)
    {
        var race = dbClass >> 4;
        var evo = dbClass & 0x0F;
        var name = race < Races.Length ? Races[race] : $"Raza {race}";
        return evo == 0 ? name : $"{name} (evo {evo})";
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
