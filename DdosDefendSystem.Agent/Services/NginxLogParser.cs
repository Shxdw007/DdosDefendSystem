using System.Text.RegularExpressions;
using DdosDefendSystem.Shared.Models;
using System.Globalization;

namespace DdosDefendSystem.Agent.Services;

public class NginxLogParser
{
    
    private static readonly Regex LogRegex = new Regex(
        @"(?<ip>\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}).*?""(?<method>[A-Z]+)\s+(?<uri>[^\s""]+).*?""\s+(?<status>\d+)\s+(?<time>[\d\.]+)",
        RegexOptions.Compiled);

    public RequestLog? Parse(string logLine)
    {
        if (string.IsNullOrWhiteSpace(logLine))
            return null;

        var match = LogRegex.Match(logLine);

        if (!match.Success)
            return null;

        try
        {
            return new RequestLog
            {
                IpAddress = match.Groups["ip"].Value,
                HttpMethod = match.Groups["method"].Value,
                Uri = match.Groups["uri"].Value,
                StatusCode = int.Parse(match.Groups["status"].Value),
                ResponseTimeSeconds = double.Parse(match.Groups["time"].Value, CultureInfo.InvariantCulture),
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception)
        {
            return null;
        }
    }
}