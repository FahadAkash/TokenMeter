using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TokenMeter.Core.Models;
using TokenMeter.Probes;
using TokenMeter.Probes.Settings;

namespace TokenMeter.UI.ViewModels;

public partial class ProviderSettingItemViewModel : ObservableObject
{
    public required ProviderSettingDefinition Definition { get; init; }

    [ObservableProperty]
    private string _value = string.Empty;

    public bool IsToggle => Definition.Type == ProviderSettingType.Toggle;
    public bool IsPicker => Definition.Type == ProviderSettingType.Picker;
    public bool IsTextField => Definition.Type == ProviderSettingType.TextField;
    public bool IsSecretField => Definition.Type == ProviderSettingType.SecretField;

    [ObservableProperty]
    private bool _toggleValue;

    partial void OnValueChanged(string value)
    {
        if (IsToggle && bool.TryParse(value, out var b))
        {
            ToggleValue = b;
        }
    }

    partial void OnToggleValueChanged(bool value)
    {
        if (IsToggle)
        {
            Value = value.ToString();
        }
    }
}

public partial class ProviderSettingsViewModel : ObservableObject
{
    public required ProviderDescriptor Descriptor { get; init; }

    public UsageProvider ProviderId => Descriptor.Id;
    public string DisplayName => Descriptor.Metadata.DisplayName;
    public string IconKind => Descriptor.Branding.Icon.ToString();
    public string ColorHex => Descriptor.Branding.ColorHex;

    public ObservableCollection<ProviderSettingItemViewModel> Settings { get; } = new();

    public void Initialize(ProviderSettingsDescriptor settingsDescriptor, IReadOnlyDictionary<string, string> storedValues)
    {
        Settings.Clear();
        foreach (var def in settingsDescriptor.Settings)
        {
            string val = storedValues.GetValueOrDefault(def.Key) ?? def.DefaultValue ?? string.Empty;

            var item = new ProviderSettingItemViewModel
            {
                Definition = def,
                Value = val
            };

            if (item.IsToggle && bool.TryParse(val, out var b))
            {
                item.ToggleValue = b;
            }

            Settings.Add(item);
        }
    }
}
