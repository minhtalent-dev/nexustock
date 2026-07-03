using System;
using System.IO;

namespace Nexustock.Api.Infrastructure;

public static class EnvLoader
{
    public static void LoadDotEnvFromNearestParent()
    {
        try
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

            while (directory != null)
            {
                var envPath = Path.Combine(directory.FullName, ".env");
                if (File.Exists(envPath))
                {
                    foreach (var line in File.ReadAllLines(envPath))
                    {
                        var trimmed = line.Trim();
                        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                        {
                            continue;
                        }

                        var separatorIndex = trimmed.IndexOf('=');
                        if (separatorIndex <= 0)
                        {
                            continue;
                        }

                        var key = trimmed[..separatorIndex].Trim();
                        var value = trimmed[(separatorIndex + 1)..].Trim();
                        Environment.SetEnvironmentVariable(key, value);
                    }

                    return;
                }

                directory = directory.Parent;
            }
        }
        catch
        {
            // Bỏ qua lỗi load .env khi test
        }
    }
}
