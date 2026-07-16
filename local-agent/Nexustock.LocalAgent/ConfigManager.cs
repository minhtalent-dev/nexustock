using System;
using System.IO;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;

namespace Nexustock.LocalAgent;

public static class ConfigManager
{
    private const string ConfigDirectoryOverrideVariable = "NEXUSTOCK_AGENT_CONFIG_DIR";

    private static readonly string ConfigDirectory = GetConfigDirectory();

    private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, "agent.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static Action<Exception>? LoadFailed { get; set; }

    public static AgentConfig Load()
    {
        try
        {
            EnsureConfigDirectory();
            if (!File.Exists(ConfigFilePath))
            {
                return new AgentConfig();
            }

            var json = File.ReadAllText(ConfigFilePath);
            var config = JsonSerializer.Deserialize<AgentConfig>(json, JsonOptions);
            return config ?? new AgentConfig();
        }
        catch (Exception ex)
        {
            LoadFailed?.Invoke(ex);
            return new AgentConfig();
        }
    }

    public static void Save(AgentConfig config)
    {
        EnsureConfigDirectory();
        var json = JsonSerializer.Serialize(config, JsonOptions);
        var tempPath = Path.Combine(ConfigDirectory, $"agent.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(tempPath, json);
            HardenFileAcl(tempPath);

            if (File.Exists(ConfigFilePath))
            {
                ReplaceExistingFile(tempPath);
            }
            else
            {
                File.Move(tempPath, ConfigFilePath);
            }

            HardenFileAcl(ConfigFilePath);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static void ReplaceExistingFile(string tempPath)
    {
        try
        {
            File.Replace(tempPath, ConfigFilePath, null);
        }
        catch (Exception) when (!IsUnauthorizedAccess(tempPath))
        {
            File.Copy(tempPath, ConfigFilePath, true);
            File.Delete(tempPath);
        }
    }

    private static bool IsUnauthorizedAccess(string tempPath)
    {
        if (!File.Exists(tempPath))
        {
            return true;
        }

        try
        {
            using var stream = File.Open(ConfigFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static void EnsureConfigDirectory()
    {
        if (!Directory.Exists(ConfigDirectory))
        {
            Directory.CreateDirectory(ConfigDirectory);
        }

        HardenDirectoryAcl(ConfigDirectory);
    }

    private static string GetConfigDirectory()
    {
        var overridePath = Environment.GetEnvironmentVariable(ConfigDirectoryOverrideVariable);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return overridePath;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Nexustock"
        );
    }

    [SupportedOSPlatform("windows")]
    private static void HardenDirectoryAcl(string directoryPath)
    {
        if (!OperatingSystem.IsWindows()) return;

        var directoryInfo = new DirectoryInfo(directoryPath);
        var security = directoryInfo.GetAccessControl();
        security.SetAccessRuleProtection(true, false);
        AddRequiredDirectoryRules(security);
        directoryInfo.SetAccessControl(security);
    }

    [SupportedOSPlatform("windows")]
    private static void HardenFileAcl(string filePath)
    {
        if (!OperatingSystem.IsWindows()) return;

        var fileInfo = new FileInfo(filePath);
        var security = fileInfo.GetAccessControl();
        security.SetAccessRuleProtection(true, false);
        AddRequiredFileRules(security);
        fileInfo.SetAccessControl(security);
    }

    [SupportedOSPlatform("windows")]
    private static void AddRequiredDirectoryRules(DirectorySecurity security)
    {
        AddDirectoryRule(security, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null));
        AddDirectoryRule(security, new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null));
        AddDirectoryRule(security, WindowsIdentity.GetCurrent().User!);
    }

    private static void AddDirectoryRule(DirectorySecurity security, IdentityReference identity)
    {
        security.AddAccessRule(new FileSystemAccessRule(
            identity,
            FileSystemRights.FullControl,
            InheritanceFlags.None,
            PropagationFlags.None,
            AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            identity,
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.InheritOnly,
            AccessControlType.Allow));
    }

    private static void AddRequiredFileRules(FileSecurity security)
    {
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            FileSystemRights.FullControl,
            AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            FileSystemRights.FullControl,
            AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            WindowsIdentity.GetCurrent().User!,
            FileSystemRights.FullControl,
            AccessControlType.Allow));
    }
}
