using System;
using System.Collections.Generic;
using System.Text;

namespace DdosDefendSystem.Shared.Models
{
    public class RequestLog
    {
        public string IpAddress { get; set; } = string.Empty;
        
        public string Uri { get; set; } = string.Empty;

        public string HttpMethod { get; set; } = string.Empty;

        public int StatusCode { get; set; }

        public double ResponseTime { get; set; }

        public DateTime Timestamp { get; set; }



    }
}
