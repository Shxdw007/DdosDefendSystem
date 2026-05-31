using System.Globalization;
using System.Text.RegularExpressions;
using DdosDefendSystem.Shared.Models;

namespace DdosDefendSystem.Agent.Services;

public class NginxLogParser
{
    private static readonly Regex LogRegex = new Regex(
        @"(?<ip>\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}).*?""(?<method>[A-Z]+)\s+(?<uri>[^\s""]+).*?""\s+(?<status>\d+)",
        RegexOptions.Compiled);

    public RequestLog? Parse(string logLine)
    {
        if (string.IsNullOrWhiteSpace(logLine))
            return null;

        var match = LogRegex.Match(logLine);

        if (!match.Success)
            return null;

        var trimmed = logLine.TrimEnd();
        var lastSpace = trimmed.LastIndexOf(' ');
        if (lastSpace < 0)
            return null;

        var timeStr = trimmed[(lastSpace + 1)..];
        if (!double.TryParse(timeStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var responseTime))
            return null;

        try
        {
            return new RequestLog
            {
                IpAddress = match.Groups["ip"].Value,
                HttpMethod = match.Groups["method"].Value,
                Uri = match.Groups["uri"].Value,
                StatusCode = int.Parse(match.Groups["status"].Value),
                ResponseTime = responseTime,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception)
        {
            return null;
        }
    }
}
