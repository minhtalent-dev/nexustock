using Microsoft.AspNetCore.Http;
using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Nexustock.Modules.Observability.Services;

public class TraceContext : ITraceContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TraceContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetCurrentTraceId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            if (httpContext.Request.Headers.TryGetValue("X-Trace-Id", out var traceIdValue) && !string.IsNullOrEmpty(traceIdValue))
            {
                var cleanTraceId = SanitizeTraceId(traceIdValue.ToString());
                if (!string.IsNullOrEmpty(cleanTraceId))
                {
                    return cleanTraceId;
                }
            }

            if (!string.IsNullOrEmpty(httpContext.TraceIdentifier))
            {
                return httpContext.TraceIdentifier;
            }
        }

        var currentActivityId = Activity.Current?.TraceId.ToString();
        if (!string.IsNullOrEmpty(currentActivityId))
        {
            return currentActivityId;
        }

        return Guid.NewGuid().ToString("N");
    }

    private string SanitizeTraceId(string rawTraceId)
    {
        if (string.IsNullOrEmpty(rawTraceId)) return string.Empty;
        var cleaned = Regex.Replace(rawTraceId, @"[^a-zA-Z0-9\-\.\_\:]", "");
        if (cleaned.Length > 80)
        {
            cleaned = cleaned.Substring(0, 80);
        }
        return cleaned;
    }
}
