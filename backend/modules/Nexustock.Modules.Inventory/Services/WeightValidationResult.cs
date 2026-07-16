using System;

namespace Nexustock.Modules.Inventory.Services;

public sealed record WeightValidationResult(
    bool Success,
    decimal Weight,
    string WeightSource,
    bool ScaleStable,
    Guid? ManualOverrideId,
    string? ErrorCode,
    string? Message);
