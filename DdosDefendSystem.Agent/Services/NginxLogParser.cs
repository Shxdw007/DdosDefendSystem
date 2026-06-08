using System.Globalization;
using System.Text.RegularExpressions;
using DdosDefendSystem.Shared.Models;

namespace DdosDefendSystem.Agent.Services;

public class NginxLogParser
{
  // Стандартный nginx combined + кастомный формат с $request_time в конце
  private static readonly Regex LogRegex = new(
      @"^(?<ip>\S+)\s+-\s+\S+\s+\[[^\]]+\]\s+""(?<method>[A-Z]+)\s+(?<uri>\S+)(?:\s+[^""]*)?""\s+(?<status>\d+)",
      RegexOptions.Compiled);

  public RequestLog? Parse(string logLine)
  {
    if (string.IsNullOrWhiteSpace(logLine))
      return null;

    var match = LogRegex.Match(logLine.Trim());
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
        ResponseTime = TryParseResponseTime(logLine),
        Timestamp = DateTime.UtcNow
      };
    }
    catch (Exception)
    {
      return null;
    }
  }

  private static double TryParseResponseTime(string logLine)
  {
    var trimmed = logLine.TrimEnd();
    var lastSpace = trimmed.LastIndexOf(' ');
    if (lastSpace < 0)
      return 0.0;

    var lastToken = trimmed[(lastSpace + 1)..].Trim('"');
    if (double.TryParse(lastToken, NumberStyles.Float, CultureInfo.InvariantCulture, out var responseTime))
      return responseTime;

    return 0.0;
  }
}
