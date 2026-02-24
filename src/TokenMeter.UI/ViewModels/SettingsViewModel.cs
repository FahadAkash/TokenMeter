using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TokenMeter.Auth;
using TokenMeter.Core.Models;
using TokenMeter.Probes;
using TokenMeter.Probes.Settings;

namespace TokenMeter.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ITokenStore _tokenStore;
    private readonly AuthRegistry _authRegistry;

    public ObservableCollection<ProviderSettingsViewModel> ProviderSettings { get; } = new();

    [ObservableProperty] private string _authCode = string.Empty;
    [ObservableProperty] private string _statusMessage = "Ready";

    public SettingsViewModel(ITokenStore tokenStore, AuthRegistry authRegistry)
    {
        _tokenStore = tokenStore;
        _authRegistry = authRegistry;

        LoadTokensCommand = new AsyncRelayCommand(LoadTokensAsync);
        SaveTokensCommand = new AsyncRelayCommand(SaveTokensAsync);
        AutoDetectCommand = new AsyncRelayCommand(() => AutoDetectAsync(null));

        // Define settings metadata per provider
        var defaultSourceOptions = new[] { "Auto", "Web", "Cli", "OAuth", "Api" };
        var settingsRegistry = new Dictionary<UsageProvider, ProviderSettingsDescriptor>
        {
            [UsageProvider.Claude] = new()
            {
                ProviderId = UsageProvider.Claude,
                Settings = new[] {
                    new ProviderSettingDefinition { Key = "claude_source_mode", DisplayName = "Source", Type = ProviderSettingType.Picker, Options = defaultSourceOptions, DefaultValue = "Auto" },
                    new ProviderSettingDefinition { Key = "claude_cookie", DisplayName = "Session Key (sessionKey=...)", Type = ProviderSettingType.SecretField }
                }
            },
            [UsageProvider.ChatGPT] = new()
            {
                ProviderId = UsageProvider.ChatGPT,
                Settings = new[] {
                    new ProviderSettingDefinition { Key = "chatgpt_source_mode", DisplayName = "Source", Type = ProviderSettingType.Picker, Options = defaultSourceOptions, DefaultValue = "Auto" },
                    new ProviderSettingDefinition { Key = "chatgpt_cookie", DisplayName = "Session Token (__Secure...)", Type = ProviderSettingType.SecretField }
                }
            },
            [UsageProvider.Cursor] = new()
            {
                ProviderId = UsageProvider.Cursor,
                Settings = new[] {
                    new ProviderSettingDefinition { Key = "cursor_source_mode", DisplayName = "Source", Type = ProviderSettingType.Picker, Options = defaultSourceOptions, DefaultValue = "Auto" },
                    new ProviderSettingDefinition { Key = "cursor_cookie", DisplayName = "Cursor Token (WorkosCursor...)", Type = ProviderSettingType.SecretField }
                }
            },
            [UsageProvider.Copilot] = new()
            {
                ProviderId = UsageProvider.Copilot,
                Settings = new[] {
                    new ProviderSettingDefinition { Key = "copilot_source_mode", DisplayName = "Source", Type = ProviderSettingType.Picker, Options = defaultSourceOptions, DefaultValue = "Auto" },
                    new ProviderSettingDefinition { Key = "copilot_token", DisplayName = "GitHub API Token (ghp_...)", Type = ProviderSettingType.SecretField }
                }
            }
        };

        // Initialize view models
        foreach (var desc in ProviderDescriptorRegistry.All.Where(d => settingsRegistry.ContainsKey(d.Id)))
        {
            var vm = new ProviderSettingsViewModel { Descriptor = desc };
            vm.Initialize(settingsRegistry[desc.Id], new Dictionary<string, string>());
            ProviderSettings.Add(vm);
        }
    }

    public IAsyncRelayCommand LoadTokensCommand { get; }
    public IAsyncRelayCommand SaveTokensCommand { get; }
    public IAsyncRelayCommand AutoDetectCommand { get; }

    // Provide single auto-detect bound dynamically from view with command parameter
    [RelayCommand]
    private async Task AutoDetectProviderAsync(UsageProvider provider)
    {
        await AutoDetectAsync(provider);
    }

    [RelayCommand]
    private void OpenProviderUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            StatusMessage = "Opening browser for login...";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to open browser: {ex.Message}";
        }
    }

    private async Task LoadTokensAsync()
    {
        try
        {
            StatusMessage = "Loading credentials...";
            foreach (var providerVm in ProviderSettings)
            {
                foreach (var setting in providerVm.Settings)
                {
                    var val = await _tokenStore.GetAsync(setting.Definition.Key);
                    if (val != null)
                    {
                        setting.Value = val;
                        if (setting.IsToggle && bool.TryParse(val, out var b))
                        {
                            setting.ToggleValue = b;
                        }
                    }
                }
            }
            StatusMessage = "Credentials loaded.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Load failed: {ex.Message}";
        }
    }

    private async Task SaveTokensAsync()
    {
        try
        {
            StatusMessage = "Saving...";
            foreach (var providerVm in ProviderSettings)
            {
                foreach (var setting in providerVm.Settings)
                {
                    await _tokenStore.SetAsync(setting.Definition.Key, setting.Value ?? string.Empty);
                }
            }
            StatusMessage = "Saved successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
    }

    private async Task AutoDetectAsync(UsageProvider? specificProvider)
    {
        try
        {
            if (specificProvider == UsageProvider.Copilot)
            {
                await LoginCopilotAsync();
                return;
            }

            StatusMessage = specificProvider.HasValue ? $"Scanning for {specificProvider}..." : "Scanning browsers...";

            var allRunners = _authRegistry.GetAllRunners().Where(r => r.DisplayName.Contains("Extraction")).ToList();
            var targetRunners = specificProvider.HasValue
                ? allRunners.Where(r => r.Provider == specificProvider.Value).ToList()
                : allRunners;

            if (!targetRunners.Any())
            {
                StatusMessage = "No suitable auto-detection runners found.";
                return;
            }

            bool anyFound = false;
            foreach (var runner in targetRunners)
            {
                StatusMessage = $"Checking {runner.Provider}...";
                if (await runner.AuthenticateAsync(msg => StatusMessage = msg))
                {
                    anyFound = true;
                }
            }

            await LoadTokensAsync();

            if (specificProvider.HasValue)
            {
                StatusMessage = anyFound ? $"{specificProvider} session found!" : $"{specificProvider} session not found in browsers.";
            }
            else
            {
                StatusMessage = anyFound ? "Auto-detection complete. Some sessions found!" : "Auto-detection finished. No sessions found.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Detection error: {ex.Message}";
        }
    }

    private async Task LoginCopilotAsync()
    {
        try
        {
            AuthCode = string.Empty;
            StatusMessage = "Starting GitHub login...";
            var runner = _authRegistry.GetRunner(UsageProvider.Copilot);
            if (runner != null)
            {
                // We'll use a specific action to capture the code if it's reported
                var wrappedLogger = new Action<string>(msg =>
                {
                    StatusMessage = msg;
                    // Detect code in message to update AuthCode property
                    if (msg.Contains("CODE: "))
                    {
                        var parts = msg.Split("CODE: ");
                        if (parts.Length > 1) AuthCode = parts[1].Split(' ')[0];
                    }
                });

                if (await runner.AuthenticateAsync(wrappedLogger))
                {
                    await LoadTokensAsync();
                    StatusMessage = "Copilot login successful!";
                    AuthCode = string.Empty;
                }
                else
                {
                    StatusMessage = "Copilot login cancelled or failed.";
                    AuthCode = string.Empty;
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Copilot login failed: {ex.Message}";
        }
    }
}
