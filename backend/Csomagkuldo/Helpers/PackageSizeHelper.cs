namespace Csomagkuldo.Helpers;

public static class PackageSizeHelper
{
    // Régi megoldás:
    // public static int GetSpace(string size)
    // {
    //     switch (size)
    //     {
    //         case "S": return 1;
    //         case "M": return 2;
    //         case "L": return 3;
    //         default: return 0;
    //     }
    // }
    // Új megoldás:
    public static int GetSpace(string size) => size switch
    {
        "S" => 1,
        "M" => 2,
        "L" => 3,
        _ => 0
    };

    // Null jelzi, ha legalább egy méret ismeretlen.
    public static int? GetTotalSpace(IEnumerable<string> sizes)
    {
        var spaces = sizes.Select(GetSpace).ToList();
        return spaces.Contains(0) ? null : spaces.Sum();
    }
}
