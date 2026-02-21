# TokenMeter Architecture Overview

TokenMeter is a WPF/.NET 8 application designed to provide premium visualization and tracking for AI coding assistant token usage and costs. It is inspired by and aligned with the **CodexBar** parent project.

## Core Principles

1.  **Registry Pattern**: Following the architecture of `CodexBarCore`, TokenMeter uses a registry system (`AuthRegistry`, `ProviderRegistry`) to manage multiple providers and their associated metadata, branding, and authentication strategies.
2.  **Unified Authentication (`IAuthRunner`)**: Authentication is decoupled from the UI and core logic. Whether it's **OAuth (Device Flow)** for GitHub Copilot or **Browser Cookie Extraction** for Claude/OpenAI, all strategies implement the `IAuthRunner` interface.
3.  **Secure Persistence**: Credentials are never stored in plain text. On Windows, they are protected using **DPAPI** via the `WindowsCredentialStore`, mirroring the secure `Keychain` usage on macOS.
4.  **Local-First Persistence**: Historical usage data is stored in a local **SQLite** database, enabling offline analysis and long-term cost tracking without relying on volatile browser sessions.

## Project Structure

-   `TokenMeter.Core`: Shared models, constants (e.g., `UsageProvider`), and data access (`AppDbContext`).
-   `TokenMeter.Auth`: The authentication subsystem.
    -   `OAuth`: Specific OAuth 2.0 and Device Flow implementations (e.g., `GitHubDeviceFlow`).
    -   `Runners`: Strategy implementations for `IAuthRunner`.
    -   `Browser`: Low-level cookie extraction logic.
-   `TokenMeter.Probes`: Implementation of API probes that fetch real-time usage data from various dashboards and APIs.
-   `TokenMeter.UI`: The WPF application, using the **Catppuccin** design language for a premium, unified aesthetic.

## Authentication System (OAuth & Beyond)

The OAuth system implemented for GitHub Copilot uses the **Device Authorization Flow**, allowing users to authenticate securely via their browser without providing their GitHub password directly to the application. 

The application generates a unique user code, opens the GitHub verification URL, and polls for the short-lived access token, which is then securely encrypted and stored.

---

*This project is built to be a high-performance Windows companion to the CodexBar ecosystem.*
