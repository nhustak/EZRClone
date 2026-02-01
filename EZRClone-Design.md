# EZRClone — Design Document

## 1. Project Overview

**EZRClone** is a Windows desktop application that provides a graphical user interface for [RClone](https://rclone.org/), the open-source command-line tool for managing files across 70+ cloud storage providers.

### Goals

- Simplify RClone usage by replacing CLI commands with an intuitive WPF GUI
- Provide visual management of RClone remotes (cloud storage connections)
- Enable creation and execution of sync, copy, and move jobs
- Lower the barrier to entry for users unfamiliar with command-line tools

### Target Users

- Windows users who want cloud storage management without memorizing CLI syntax
- IT administrators who need a quick way to configure and manage RClone remotes
- Users automating file transfers across multiple cloud services

### Scope

| Module | Description | Status |
|--------|-------------|--------|
| App Settings | Configure the path to `rclone.exe` | ✅ Implemented |
| Config Editor | Read, create, edit, and delete remotes in `rclone.conf` | ✅ Implemented |
| Jobs | Create, manage, and execute sync/copy/move operations | ✅ Implemented |
| Log Viewer | Display rclone command output and application logs | Placeholder |

---

## 2. Technology Stack

| Component | Technology |
|-----------|------------|
| Runtime | .NET 9.0 |
| Language | C# 13 |
| UI Framework | WPF (Windows Presentation Foundation) |
| MVVM Toolkit | CommunityToolkit.Mvvm 8.4.0 (source-generated) |
| UI Components | WPF-UI 4.2.0 (modern controls) |
| Serialization | System.Text.Json (app settings, jobs) |
| INI Parsing | Custom implementation |
| DI Container | Microsoft.Extensions.DependencyInjection 10.0.2 |

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
| 📁 | Config | ConfigView | ✅ Implemented |
| ⚙️ | Settings | SettingsView | ✅ Implemented |
| 🔄 | Jobs | JobsView | ✅ Implemented |
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

#### Window: Jobs View

The Jobs view provides full management of RClone sync/copy/move operations.

```
┌──────────┬─────────────────────────────────────────────────────────┐
│          │  Jobs                                    [+ Add Job]    │
│   NAV    │                                                         │
│          │  ┌──────────────────────────────────────────────────┐   │
│          │  │ Name          │ Operation │ Last Run   │ Status  │   │
│          │  │───────────────│───────────│────────────│─────────│   │
│          │  │ ● Daily Sync  │ Sync      │ 2025-01-30 │ ✓       │   │
│          │  │   S3 Backup   │ Copy      │ 2025-01-29 │ ✗       │   │
│          │  │   Archive     │ Move      │ (never)    │ —       │   │
│          │  └──────────────────────────────────────────────────┘   │
│          │                                                         │
│          │  [Edit]  [Delete]  [▶ Run]                              │
│          │                                                         │
│          │  ── Edit Job ──────────────────────────────────────     │
│          │  Name:       [Daily Sync             ]                  │
│          │  Operation:  [Sync              ▼    ]                  │
│          │                                                         │
│          │  Source:                                                 │
│          │  ☐ Remote     Remote: [myS3     ▼]                      │
│          │  Path:        [/backups                ]                 │
│          │                                                         │
│          │  Destination:                                            │
│          │  ☑ Remote     Remote: [myGDrive ▼]                      │
│          │  Path:        [/archive                ]                 │
│          │                                                         │
│          │  Options:                                                │
│          │  Transfers:   [4    ]                                    │
│          │  Verbosity:   [Normal           ▼]                      │
│          │  ☑ Create Log File                                      │
│          │  Log Path:    [C:\logs\sync.log       ]                 │
│          │                                                         │
│          │  Include Patterns:  *.doc, *.pdf                        │
│          │  Exclude Patterns:  *.tmp, thumbs.db                    │
│          │                                                         │
│          │  [Save]  [Cancel]                                       │
└──────────┴─────────────────────────────────────────────────────────┘
```

**Job Operations:**

| Operation | RClone Command | Behavior |
|-----------|---------------|----------|
| Copy | `rclone copy` | Copy files from source to dest, skipping identical files |
| Sync | `rclone sync` | Make destination identical to source (one-way) |
| Move | `rclone move` | Move files from source to dest |

**Job Options:**

| Option | Description | Default |
|--------|-------------|---------|
| Transfers | Number of parallel file transfers | 4 |
| Verbosity | Quiet, Normal, Verbose, VeryVerbose | Normal |
| Create Log File | Write rclone output to a log file | true |
| Include Patterns | Only transfer files matching these patterns | (none) |
| Exclude Patterns | Skip files matching these patterns | (none) |

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
EZRClone.slnx
└── EZRClone/
    ├── App.xaml                        # Application entry, DI container setup
    ├── App.xaml.cs
    ├── MainWindow.xaml                 # App shell: sidebar + content area
    ├── MainWindow.xaml.cs
    ├── AssemblyInfo.cs
    │
    ├── Models/
    │   ├── AppSettings.cs              # App-level settings (rclone.exe path, preferences)
    │   ├── RCloneRemote.cs             # One configured remote from rclone.conf
    │   ├── RCloneBackendType.cs        # Metadata for known backend types
    │   └── RCloneJob.cs                # Job configuration, enums (Operation, Status, Verbosity)
    │
    ├── ViewModels/
    │   ├── MainWindowViewModel.cs      # Shell/navigation state
    │   ├── ConfigViewModel.cs          # Remote list + detail + inline edit
    │   ├── SettingsViewModel.cs        # rclone.exe path configuration
    │   ├── JobsViewModel.cs            # Job management and execution
    │   └── LogViewModel.cs             # Placeholder
    │
    ├── Views/
    │   ├── ConfigView.xaml             # Master-detail remote management
    │   ├── SettingsView.xaml           # App settings page
    │   ├── JobsView.xaml               # Job creation, editing, and execution
    │   └── LogView.xaml                # Placeholder
    │
    ├── Services/
    │   ├── IAppSettingsService.cs      # Interface: load/save app settings
    │   ├── AppSettingsService.cs       # Implementation: JSON file in %APPDATA%
    │   ├── IRCloneConfigService.cs     # Interface: parse/write rclone.conf
    │   ├── RCloneConfigService.cs      # Implementation: INI read/write
    │   ├── IRCloneProcessService.cs    # Interface: execute rclone.exe commands
    │   ├── RCloneProcessService.cs     # Implementation: Process.Start wrapper
    │   ├── IJobStorageService.cs       # Interface: load/save jobs
    │   └── JobStorageService.cs        # Implementation: JSON file in %APPDATA%
    │
    ├── Converters/
    │   └── InverseBoolToVisConverter.cs # Bool ↔ Visibility converter
    │
    └── Resources/
        └── DarkTheme.xaml              # Dark theme color definitions and styles
```

### Dependency Injection Setup

```csharp
// App.xaml.cs
// Services
services.AddSingleton<IAppSettingsService, AppSettingsService>();
services.AddSingleton<IRCloneConfigService, RCloneConfigService>();
services.AddSingleton<IRCloneProcessService, RCloneProcessService>();
services.AddSingleton<IJobStorageService, JobStorageService>();

// ViewModels (all singletons for navigation state preservation)
services.AddSingleton<MainWindowViewModel>();
services.AddSingleton<ConfigViewModel>();
services.AddSingleton<SettingsViewModel>();
services.AddSingleton<JobsViewModel>();
services.AddSingleton<LogViewModel>();

// Window
services.AddSingleton<MainWindow>();
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
| `RCloneJob` | `Id`, `Name`, `Operation`, Source/Dest paths, options, status | Job configuration and execution state |

### RCloneJob Model

```csharp
public class RCloneJob
{
    public string Id { get; set; }                      // GUID
    public string Name { get; set; }
    public RCloneOperation Operation { get; set; }      // Copy, Sync, Move
    public string SourcePath { get; set; }
    public bool SourceIsRemote { get; set; }
    public string? SourceRemoteName { get; set; }
    public string DestinationPath { get; set; }
    public bool DestinationIsRemote { get; set; }
    public string? DestinationRemoteName { get; set; }

    // Common options
    public int Transfers { get; set; } = 4;
    public bool CreateLogFile { get; set; } = true;
    public string? LogFilePath { get; set; }
    public RCloneVerbosity Verbosity { get; set; }      // Quiet, Normal, Verbose, VeryVerbose

    // Filtering
    public List<string> IncludePatterns { get; set; }
    public List<string> ExcludePatterns { get; set; }

    // Status
    public DateTime? LastRun { get; set; }
    public RCloneJobStatus LastStatus { get; set; }     // NotRun, Running, Success, Failed, Cancelled
    public string? LastError { get; set; }
}
```

**Storage:** JSON file at `%APPDATA%\EZRClone\jobs.json`

### IJobStorageService Interface

```csharp
public interface IJobStorageService
{
    Task<List<RCloneJob>> LoadJobsAsync();
    Task SaveJobsAsync(List<RCloneJob> jobs);
}
```

---

## 7. IRCloneProcessService

Wraps execution of `rclone.exe` for operations that require the CLI:

```csharp
public interface IRCloneProcessService
{
    /// <summary>Run rclone with arguments and return stdout.</summary>
    Task<string> RunAsync(string arguments);

    /// <summary>Execute rclone with args list, return exit code + stdout/stderr.</summary>
    Task<(int ExitCode, string Output, string Error)> ExecuteAsync(List<string> args);

    /// <summary>Get rclone version string for validation.</summary>
    Task<string> GetVersionAsync();

    /// <summary>Get the config file path from rclone.</summary>
    Task<string> GetConfigFilePathAsync();
}
```

---

## 8. File I/O Locations

| Purpose | Location | Format |
|---------|----------|--------|
| App Settings | `%APPDATA%\EZRClone\appsettings.json` | JSON |
| RClone Config | Auto-detected or user-specified | INI |
| Jobs | `%APPDATA%\EZRClone\jobs.json` | JSON |

---

## 9. Future Considerations

These are **out of scope** for the current release but inform architectural decisions:

- **Job Monitoring** — Real-time transfer progress with `--progress` output parsing
- **Job Scheduling** — Recurring sync jobs via Windows Task Scheduler integration
- **Mount Manager** — Mount remotes as drive letters via `rclone mount`
- **Log Viewer** — Display and filter rclone log output
- **Encryption Setup** — GUI wizard for `crypt` remote configuration
