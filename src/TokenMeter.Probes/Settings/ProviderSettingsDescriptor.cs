using System.Collections.Generic;
using TokenMeter.Core.Models;

namespace TokenMeter.Probes.Settings;

public enum ProviderSettingType
{
    Toggle,
    Picker,
    TextField,
    SecretField
}

public class ProviderSettingDefinition
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required ProviderSettingType Type { get; init; }
    public string? Description { get; init; }
    public string? DefaultValue { get; init; }
    public IReadOnlyList<string>? Options { get; init; } // Only used if Type is Picker
}

public class ProviderSettingsDescriptor
{
    public required UsageProvider ProviderId { get; init; }
    public IReadOnlyList<ProviderSettingDefinition> Settings { get; init; } = [];
}
