# EZRClone — Design Document

## 1. Project Overview

**EZRClone** is a Windows desktop application that provides a graphical user interface for [RClone](https://rclone.org/), the open-source command-line tool for managing files across 70+ cloud storage providers.

### Goals

- Simplify RClone usage by replacing CLI commands with an intuitive WPF GUI
- Provide visual management of RClone remotes (cloud storage connections)
- Lower the barrier to entry for users unfamiliar with command-line tools

### Target Users

- Windows users who want cloud storage management without memorizing CLI syntax
- IT administrators who need a quick way to configure and manage RClone remotes

### Initial Scope

| Module | Description |
|--------|-------------|
| App Settings | Configure the path to `rclone.exe` |
| Config Editor | Read, create, edit, and delete remotes in `rclone.conf` |

---

## 2. Technology Stack

| Component | Technology |
|-----------|------------|
| Runtime | .NET 9 |
| Language | C# 13 |
| UI Framework | WPF (Windows Presentation Foundation) |
| MVVM Toolkit | CommunityToolkit.Mvvm (source-generated) |
| Serialization | System.Text.Json (app settings) |
| INI Parsing | Custom parser or `ini-parser-netstandard` NuGet |
| DI Container | Microsoft.Extensions.DependencyInjection |

---

## 3. Architecture

EZRClone follows the **Model-View-ViewModel (MVVM)** pattern with dependency injection.

### Layer Responsibilities

| Layer | Responsibility |
|-------|---------------|
| **Views** | XAML UI — binds to ViewModels, no business logic |
| **ViewModels** | Presentation logic, commands, observable properties |
| **Models** | Data structures (POCOs) |
| **Services** | Business logic — file I/O, process execution, config parsing |

### Application Windows

The application uses a **left navigation sidebar + content area** shell. Each nav item switches the content area to a different view.

#### Navigation Sidebar

| Icon | Label | View | Status |
|------|-------|------|--------|
| 📁 | Config | ConfigView | Initial scope |
| ⚙️ | Settings | SettingsView | Initial scope |
| 🔄 | Jobs | JobsView | Placeholder (future) |
| 📋 | Log | LogView | Placeholder (future) |

#### Main Shell Layout

```
┌──────────┬─────────────────────────────────────────────────────┐
│          │                                                     │
│   NAV    │              CONTENT AREA                           │
│  SIDEBAR │                                                     │
│          │   (switches based on selected nav item)             │
│  ┌────┐  │                                                     │
│  │ 📁 │  │                                                     │
│  │Conf│  │                                                     │
│  ├────┤  │                                                     │
│  │ ⚙️ │  │                                                     │
│  │Sett│  │                                                     │
│  ├────┤  │                                                     │
│  │ 🔄 │  │                                                     │
│  │Jobs│  │                                                     │
│  ├────┤  │                                                     │
│  │ 📋 │  │                                                     │
│  │Log │  │                                                     │
│  └────┘  │                                                     │
│          │                                                     │
└──────────┴─────────────────────────────────────────────────────┘
```

#### Window: Config View (Master-Detail)

The primary window for managing RClone remotes. Uses a **master-detail** layout.

```
┌──────────┬─────────────────────────────────────────────────────────┐
│          │ Config: C:\Users\user\.config\rclone\rclone.conf       │
│   NAV    │ ─────────────────────────────────────────────────────── │
│          │                                                         │
│          │ Remotes                                [+ New] [Refresh]│
│          │ ┌──────────────────────┐ ┌────────────────────────────┐ │
│          │ │ Name      │ Type    │ │  myS3                      │ │
│          │ │───────────│─────────│ │                            │ │
│          │ │ ● myS3    │ s3      │ │  Type:     s3              │ │
│          │ │   myGDrive│ drive   │ │  Provider: AWS             │ │
│          │ │   mySFTP  │ sftp    │ │  Region:   us-west-2      │ │
│          │ │   myAzure │ azblob  │ │  Access Key: AKIA...       │ │
│          │ │           │         │ │  Secret Key: ••••••••  👁  │ │
│          │ │           │         │ │                            │ │
│          │ │           │         │ │  [Edit]  [Delete]  [Test]  │ │
│          │ └──────────────────────┘ └────────────────────────────┘ │
└──────────┴─────────────────────────────────────────────────────────┘
```

**Components:**

| Area | Description |
|------|-------------|
| **Config path banner** | Full path to `rclone.conf` displayed at top, always visible |
| **Remote list** (left) | Two-column list: Name + Type. Selected row highlighted. Sorted alphabetically |
| **Detail panel** (right) | Read-only view of selected remote's properties |
| **Action buttons** | **Edit** — switches detail panel to inline edit mode. **Delete** — with confirmation dialog. **Test** — runs `rclone lsd remote:` to verify connectivity |
| **Sensitive values** | Masked by default (••••). Eye icon toggles reveal |
| **+ New button** | Switches detail panel to blank inline edit mode for a new remote |
| **Refresh button** | Re-reads `rclone.conf` from disk |

#### Config View: Inline Edit Mode

When the user clicks **Edit** or **+ New**, the detail panel switches in-place to editable fields. No modal dialog.

```
┌────────────────────────────────┐
│  ✏️ Editing: myS3              │
│                                │
│  Name:  [myS3              ]   │
│  Type:  [s3            ▼   ]   │
│                                │
│  Properties:                   │
│  provider          [AWS     ]  │
│  access_key_id     [AKIA... ]  │
│  secret_access_key [wJal... ]  │
│  region            [us-west-2] │
│                                │
│  [+ Add Property]              │
│                                │
│  [Save]  [Cancel]              │
└────────────────────────────────┘
```

**Edit mode behavior:**
- **Type dropdown** — lists known backend types (s3, drive, sftp, azblob, etc.)
- **Properties** — rendered as label + text field pairs from the Dictionary
- **+ Add Property** — appends a new blank key/value row
- **Save** — validates, writes to `rclone.conf`, returns to read-only mode
- **Cancel** — discards changes, returns to read-only mode
- **Remote list** remains visible but selection is locked during edit

#### Window: Settings View

```
┌──────────┬─────────────────────────────────────────────────────┐
│          │  Settings                                           │
│   NAV    │                                                     │
│          │  RClone Executable Path:                            │
│          │  ┌────────────────────────────────┐ ┌────────────┐  │
│          │  │ C:\tools\rclone\rclone.exe     │ │ Browse...  │  │
│          │  └────────────────────────────────┘ └────────────┘  │
│          │  ✅ Valid — rclone v1.68.2                          │
│          │                                                     │
│          │  Config File Location:                              │
│          │  ┌────────────────────────────────┐ ┌────────────┐  │
│          │  │ (auto-detected from rclone)    │ │ Override.. │  │
│          │  └────────────────────────────────┘ └────────────┘  │
│          │                                                     │
│          │               ┌────────┐                            │
│          │               │  Save  │                            │
│          │               └────────┘                            │
└──────────┴─────────────────────────────────────────────────────┘
```

#### Window: Jobs View (Future Placeholder)

```
┌──────────┬─────────────────────────────────────────────────────┐
│          │  Jobs                                               │
│   NAV    │                                                     │
│          │  🔄 No jobs configured yet.                         │
│          │                                                     │
│          │  This view will support creating and monitoring     │
│          │  sync/copy/move operations in a future release.     │
│          │                                                     │
└──────────┴─────────────────────────────────────────────────────┘
```

#### Window: Log View (Future Placeholder)

```
┌──────────┬─────────────────────────────────────────────────────┐
│          │  Log                                                │
│   NAV    │                                                     │
│          │  📋 No log entries yet.                              │
│          │                                                     │
│          │  This view will display rclone command output       │
│          │  and application logs in a future release.          │
│          │                                                     │
└──────────┴─────────────────────────────────────────────────────┘
```

### Project Structure

```
EZRClone.sln
└── EZRClone/
    ├── App.xaml                        # Application entry, DI container setup
    ├── App.xaml.cs
    │
    ├── Models/
    │   ├── AppSettings.cs              # App-level settings (rclone.exe path, preferences)
    │   ├── RCloneRemote.cs             # One configured remote from rclone.conf
    │   └── RCloneBackendType.cs        # Enum/metadata for backend types
    │
    ├── ViewModels/
    │   ├── MainWindowViewModel.cs      # Shell/navigation state
    │   ├── ConfigViewModel.cs          # Remote list + detail + inline edit
    │   ├── SettingsViewModel.cs        # rclone.exe path configuration
    │   ├── JobsViewModel.cs            # Placeholder
    │   └── LogViewModel.cs             # Placeholder
    │
    ├── Views/
    │   ├── MainWindow.xaml             # App shell: sidebar + content area
    │   ├── ConfigView.xaml             # Master-detail remote management
    │   ├── SettingsView.xaml           # App settings page
    │   ├── JobsView.xaml               # Placeholder
    │   └── LogView.xaml                # Placeholder
    │
    └── Services/
        ├── IAppSettingsService.cs      # Interface: load/save app settings
        ├── AppSettingsService.cs       # Implementation: JSON file in %APPDATA%
        ├── IRCloneConfigService.cs     # Interface: parse/write rclone.conf
        ├── RCloneConfigService.cs      # Implementation: INI read/write + CRUD
        ├── IRCloneProcessService.cs    # Interface: execute rclone.exe commands
        └── RCloneProcessService.cs     # Implementation: Process.Start wrapper
```

### Dependency Injection Setup

```csharp
// App.xaml.cs
services.AddSingleton<IAppSettingsService, AppSettingsService>();
services.AddSingleton<IRCloneConfigService, RCloneConfigService>();
services.AddSingleton<IRCloneProcessService, RCloneProcessService>();
services.AddSingleton<MainWindowViewModel>();
services.AddTransient<SettingsViewModel>();
services.AddTransient<RemotesViewModel>();
```

---

## 4. Module: App Settings (rclone.exe Path Configuration)

### Purpose

Allow the user to specify where `rclone.exe` is installed on their system. This is required before any RClone operations can be performed.

### Behavior

1. **First Run** — If no settings file exists, prompt the user to browse for `rclone.exe` using `OpenFileDialog`
2. **Persist** — Save the path to a JSON file at `%APPDATA%\EZRClone\appsettings.json`
3. **Validate** — Confirm the file exists and is functional by running `rclone version`
4. **Settings UI** — Always accessible to change the path later

### Settings File Format

```json
{
  "RCloneExePath": "C:\\tools\\rclone\\rclone.exe",
  "RCloneConfigPath": ""
}
```

When `RCloneConfigPath` is empty, the application discovers it by running `rclone config file`.

### AppSettings Model

```csharp
public class AppSettings
{
    public string RCloneExePath { get; set; } = string.Empty;
    public string RCloneConfigPath { get; set; } = string.Empty;
}
```

### IAppSettingsService Interface

```csharp
public interface IAppSettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
    bool Validate(AppSettings settings);  // checks exe exists + runs rclone version
}
```

### Settings UI Wireframe

```
┌─────────────────────────────────────────────────────┐
│  Settings                                           │
│                                                     │
│  RClone Executable Path:                            │
│  ┌─────────────────────────────────┐ ┌──────────┐  │
│  │ C:\tools\rclone\rclone.exe      │ │ Browse...│  │
│  └─────────────────────────────────┘ └──────────┘  │
│  ✅ Valid — rclone v1.68.2                          │
│                                                     │
│  Config File Location:                              │
│  ┌─────────────────────────────────┐                │
│  │ C:\Users\user\.config\rclone\.. │ (auto-detect)  │
│  └─────────────────────────────────┘                │
│                                                     │
│              ┌────────┐                             │
│              │  Save  │                             │
│              └────────┘                             │
└─────────────────────────────────────────────────────┘
```

---

## 5. Module: RClone Config Reader/Writer

### Purpose

Parse the RClone configuration file (`rclone.conf`) to enable GUI-based management of remotes (cloud storage connections).

### RClone Config File Format

RClone uses a standard **INI file format**. Each section represents one remote:

```ini
[myS3]
type = s3
provider = AWS
access_key_id = AKIAIOSFODNN7EXAMPLE
secret_access_key = wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY
region = us-west-2

[myGDrive]
type = drive
client_id = 123456.apps.googleusercontent.com
client_secret = GOCSPX-xxxxx
scope = drive
root_folder_id =

[mySFTP]
type = sftp
host = server.example.com
user = admin
port = 22
key_file = C:\Users\user\.ssh\id_rsa
```

### Config File Location

Discovered in priority order:
1. User-specified path in app settings (`RCloneConfigPath`)
2. Output of `rclone config file` command
3. Default: `%APPDATA%\rclone\rclone.conf`

### RCloneRemote Model

```csharp
public class RCloneRemote
{
    public string Name { get; set; } = string.Empty;        // Section name, e.g. "myS3"
    public string Type { get; set; } = string.Empty;        // Backend type, e.g. "s3"
    public Dictionary<string, string> Properties { get; set; } = new();  // All key-value pairs
}
```

Using a `Dictionary<string, string>` for properties provides flexibility across all 70+ backend types without needing type-specific models.

### IRCloneConfigService Interface

```csharp
public interface IRCloneConfigService
{
    /// <summary>Load and parse all remotes from rclone.conf.</summary>
    List<RCloneRemote> ReadConfig(string configPath);

    /// <summary>Write the full list of remotes back to rclone.conf.</summary>
    void WriteConfig(string configPath, List<RCloneRemote> remotes);

    /// <summary>Add a new remote to the config file.</summary>
    void AddRemote(RCloneRemote remote);

    /// <summary>Update an existing remote by name.</summary>
    void UpdateRemote(string originalName, RCloneRemote remote);

    /// <summary>Delete a remote by name.</summary>
    void DeleteRemote(string name);
}
```

### INI Parsing Strategy

**Read:**
1. Read all lines from the config file
2. Identify section headers: lines matching `[name]`
3. For each section, collect `key = value` pairs until the next section or EOF
4. Map to `RCloneRemote` objects; extract `type` into the dedicated property

**Write:**
1. Serialize each `RCloneRemote` as `[Name]` followed by `key = value` lines
2. Always write `type` as the first property
3. Separate sections with a blank line
4. Write atomically (write to temp file, then replace)

### Remotes UI Wireframe

```
┌──────────────────────────────────────────────────────────┐
│  Remotes                                    ┌──────────┐ │
│                                             │ + New    │ │
│  ┌────────────────────────────────────────┐ └──────────┘ │
│  │ Name       │ Type    │ Actions         │              │
│  │────────────│─────────│─────────────────│              │
│  │ myS3       │ s3      │ [Edit] [Delete] │              │
│  │ myGDrive   │ drive   │ [Edit] [Delete] │              │
│  │ mySFTP     │ sftp    │ [Edit] [Delete] │              │
│  └────────────────────────────────────────┘              │
│                                                          │
│  ── Edit Remote: myS3 ──────────────────────────────     │
│  Name:    [myS3          ]                               │
│  Type:    [s3        ▼   ]                               │
│                                                          │
│  Properties:                                             │
│  provider           = [AWS              ]                │
│  access_key_id      = [AKIA...          ]                │
│  secret_access_key  = [wJal...          ]                │
│  region             = [us-west-2        ]                │
│  + Add Property                                          │
│                                                          │
│           ┌────────┐  ┌──────────┐                       │
│           │  Save  │  │  Cancel  │                       │
│           └────────┘  └──────────┘                       │
└──────────────────────────────────────────────────────────┘
```

---

## 6. Data Models Summary

| Model | Properties | Purpose |
|-------|-----------|---------|
| `AppSettings` | `RCloneExePath`, `RCloneConfigPath` | Application configuration |
| `RCloneRemote` | `Name`, `Type`, `Properties` (Dictionary) | One remote from rclone.conf |
| `RCloneBackendType` | `TypeName`, `DisplayName`, `Description` | UI metadata for known backend types |

---

## 7. IRCloneProcessService

Wraps execution of `rclone.exe` for operations that require the CLI:

```csharp
public interface IRCloneProcessService
{
    /// <summary>Run rclone with arguments and return stdout.</summary>
    Task<string> RunAsync(string arguments);

    /// <summary>Get rclone version string for validation.</summary>
    Task<string> GetVersionAsync();

    /// <summary>Get the config file path from rclone.</summary>
    Task<string> GetConfigFilePathAsync();
}
```

---

## 8. Future Considerations

These are **out of scope** for the initial release but inform architectural decisions:

- **Sync/Copy Job Builder** — GUI to construct `rclone sync`/`copy` commands with source, destination, and flags
- **Job Monitoring** — Real-time transfer progress with `--progress` output parsing
- **Job Scheduling** — Recurring sync jobs via Windows Task Scheduler integration
- **Mount Manager** — Mount remotes as drive letters via `rclone mount`
- **Log Viewer** — Display and filter rclone log output
- **Encryption Setup** — GUI wizard for `crypt` remote configuration
