using System.Text.RegularExpressions;

namespace Indamin.Performance.Services;

public static class AutoCodeGenerator
{
    public static string Next(IEnumerable<string> existingCodes, string prefix)
    {
        var max = 0;
        var pattern = new Regex($"^{Regex.Escape(prefix)}-(\\d+)$", RegexOptions.IgnoreCase);
        foreach (var code in existingCodes)
        {
            var match = pattern.Match(code?.Trim() ?? "");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var n))
                max = Math.Max(max, n);
        }

        return $"{prefix}-{(max + 1):000}";
    }
}