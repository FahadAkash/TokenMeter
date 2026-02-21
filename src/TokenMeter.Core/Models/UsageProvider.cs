using System.Text.Json.Serialization;

namespace TokenMeter.Core.Models;

/// <summary>
/// All supported AI/coding-assistant providers.
/// Mirrors the Swift <c>UsageProvider</c> enum from the original CodexBar project.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UsageProvider
{
    Codex,
    Claude,
    Cursor,
    OpenCode,
    Factory,
    Gemini,
    Antigravity,
    Copilot,
    Zai,
    MiniMax,
    Kimi,
    Kiro,
    VertexAI,
    Augment,
    JetBrains,
    KimiK2,
    Amp,
    Ollama,
    Synthetic,
    Warp,
    OpenRouter,
    ChatGPT,
}

/// <summary>
/// Visual style identifier for provider icons in the tray/menu.
/// </summary>
public enum IconStyle
{
    Codex,
    Claude,
    Zai,
    MiniMax,
    Gemini,
    Antigravity,
    Cursor,
    OpenCode,
    Factory,
    Copilot,
    Kimi,
    KimiK2,
    Kiro,
    VertexAI,
    Augment,
    JetBrains,
    Amp,
    Ollama,
    Synthetic,
    Warp,
    OpenRouter,
    ChatGPT,
    Combined,
}
