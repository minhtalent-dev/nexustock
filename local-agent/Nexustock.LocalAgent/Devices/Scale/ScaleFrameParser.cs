using System.Globalization;
using System.Text.RegularExpressions;

namespace Nexustock.LocalAgent.Devices.Scale;

public sealed class ScaleFrameParser
{
    private static readonly Regex NumberPattern = new(@"[-+]?\d+(?:[\.,]\d+)?", RegexOptions.Compiled);

    public bool TryParse(string rawFrame, string profile, out decimal weightKg, out string? errorCode)
    {
        weightKg = 0m;
        errorCode = null;

        if (string.IsNullOrWhiteSpace(rawFrame))
        {
            errorCode = "scale.frame_empty";
            return false;
        }

        var matches = NumberPattern.Matches(rawFrame);
        if (matches.Count == 0)
        {
            errorCode = "scale.frame_no_weight";
            return false;
        }

        if (matches.Count > 1 && !string.Equals(profile, "generic-rs232", StringComparison.OrdinalIgnoreCase))
        {
            errorCode = "scale.frame_ambiguous";
            return false;
        }

        var valueText = matches[^1].Value.Replace(',', '.');
        if (!decimal.TryParse(valueText, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out weightKg))
        {
            errorCode = "scale.frame_invalid_weight";
            return false;
        }

        return true;
    }
}
