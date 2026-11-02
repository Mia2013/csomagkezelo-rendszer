namespace Csomagkuldo.Helpers;

public static class PackageSizeHelper
{
    public static int GetSpace(string size) => size switch
    {
        "S" => 1,
        "M" => 2,
        "L" => 3,
        _ => 0
    };

    public static int? GetTotalSpace(IEnumerable<string> sizes)
    {
        var spaces = sizes.Select(GetSpace).ToList();
        return spaces.Contains(0) ? null : spaces.Sum();
    }
}
