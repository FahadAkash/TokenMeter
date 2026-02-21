using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TokenMeter.Auth;
using TokenMeter.Core.Models;

namespace TokenMeter.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ITokenStore _tokenStore;
    private readonly AuthRegistry _authRegistry;

    [ObservableProperty] private string _claudeCookie = string.Empty;
    [ObservableProperty] private string _chatgptCookie = string.Empty;
    [ObservableProperty] private string _cursorCookie = string.Empty;
    [ObservableProperty] private string _copilotToken = string.Empty;
    [ObservableProperty] private string _authCode = string.Empty;

    [ObservableProperty] private string _statusMessage = "Ready";

    public SettingsViewModel(ITokenStore tokenStore, AuthRegistry authRegistry)
    {
        _tokenStore = tokenStore;
        _authRegistry = authRegistry;

        LoadTokensCommand = new AsyncRelayCommand(LoadTokensAsync);
        SaveTokensCommand = new AsyncRelayCommand(SaveTokensAsync);
        AutoDetectCommand = new AsyncRelayCommand(() => AutoDetectAsync(null));
        LoginCopilotCommand = new AsyncRelayCommand(LoginCopilotAsync);

        // Specific Detect Commands
        AutoDetectClaudeCommand = new AsyncRelayCommand(() => AutoDetectAsync(UsageProvider.Claude));
        AutoDetectChatGPTCommand = new AsyncRelayCommand(() => AutoDetectAsync(UsageProvider.ChatGPT));
        AutoDetectCursorCommand = new AsyncRelayCommand(() => AutoDetectAsync(UsageProvider.Cursor));

        // Browser Open Commands
        OpenClaudeUrlCommand = new RelayCommand(() => OpenUrl("https://claude.ai/login"));
        OpenChatGPTUrlCommand = new RelayCommand(() => OpenUrl("https://chatgpt.com/auth/login"));
        OpenCursorUrlCommand = new RelayCommand(() => OpenUrl("https://cursor.com/login"));
    }

    public IAsyncRelayCommand LoadTokensCommand { get; }
    public IAsyncRelayCommand SaveTokensCommand { get; }
    public IAsyncRelayCommand AutoDetectCommand { get; }
    public IAsyncRelayCommand LoginCopilotCommand { get; }

    public IAsyncRelayCommand AutoDetectClaudeCommand { get; }
    public IAsyncRelayCommand AutoDetectChatGPTCommand { get; }
    public IAsyncRelayCommand AutoDetectCursorCommand { get; }

    public IRelayCommand OpenClaudeUrlCommand { get; }
    public IRelayCommand OpenChatGPTUrlCommand { get; }
    public IRelayCommand OpenCursorUrlCommand { get; }

    private async Task LoadTokensAsync()
    {
        try
        {
            StatusMessage = "Loading credentials...";
            ClaudeCookie = await _tokenStore.GetAsync("claude_cookie") ?? "";
            ChatgptCookie = await _tokenStore.GetAsync("chatgpt_cookie") ?? "";
            CursorCookie = await _tokenStore.GetAsync("cursor_cookie") ?? "";
            CopilotToken = await _tokenStore.GetAsync("copilot_token") ?? "";
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
            await _tokenStore.SetAsync("claude_cookie", ClaudeCookie);
            await _tokenStore.SetAsync("chatgpt_cookie", ChatgptCookie);
            await _tokenStore.SetAsync("cursor_cookie", CursorCookie);
            await _tokenStore.SetAsync("copilot_token", CopilotToken);
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

    private void OpenUrl(string url)
    {
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
                    CopilotToken = await _tokenStore.GetAsync("copilot_token") ?? "";
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
