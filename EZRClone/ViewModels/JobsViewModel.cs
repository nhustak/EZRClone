using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EZRClone.Models;
using EZRClone.Services;

namespace EZRClone.ViewModels;

public partial class JobsViewModel : ObservableObject
{
    private readonly IJobStorageService _jobStorageService;
    private readonly IRCloneConfigService _configService;
    private readonly IRCloneProcessService _processService;
    private readonly IAppSettingsService _settingsService;
    private readonly IBatchImportService _batchImportService;
    private readonly IAppLogService _appLogService;

    [ObservableProperty]
    private ObservableCollection<RCloneJob> _jobs = new();

    [ObservableProperty]
    private RCloneJob? _selectedJob;

    public bool HasJobs => Jobs.Count > 0;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private RCloneJob? _editingJob;

    [ObservableProperty]
    private ObservableCollection<string> _availableRemotes = new();

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string? _statusMessage;

    public Action<string?>? OpenJobHistoryRequested { get; set; }

    public JobsViewModel(
        IJobStorageService jobStorageService,
        IRCloneConfigService configService,
        IRCloneProcessService processService,
        IAppSettingsService settingsService,
        IBatchImportService batchImportService,
        IAppLogService appLogService)
    {
        _jobStorageService = jobStorageService;
        _configService = configService;
        _processService = processService;
        _settingsService = settingsService;
        _batchImportService = batchImportService;
        _appLogService = appLogService;

        _ = LoadJobsAsync();
        _ = LoadRemotesAsync();
    }

    private async Task LoadJobsAsync()
    {
        var jobs = await _jobStorageService.LoadJobsAsync();
        Jobs = new ObservableCollection<RCloneJob>(jobs.OrderBy(j => j.Name, StringComparer.OrdinalIgnoreCase));
        OnPropertyChanged(nameof(HasJobs));
    }

    [RelayCommand]
    private async Task LoadJobsCommandAsync()
    {
        await LoadJobsAsync();
    }

    private Task LoadRemotesAsync()
    {
        try
        {
            var settings = _settingsService.Load();
            var remotes = _configService.ReadConfig(settings.RCloneConfigPath);
            AvailableRemotes = new ObservableCollection<string>(remotes.Select(r => r.Name));
        }
        catch
        {
            AvailableRemotes = new ObservableCollection<string>();
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private void AddJob()
    {
        var jobNumber = Jobs.Count + 1;
        var jobName = $"Job {jobNumber}";

        while (Jobs.Any(j => j.Name == jobName))
        {
            jobNumber++;
            jobName = $"Job {jobNumber}";
        }

        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EZRClone", "Logs");

        EditingJob = new RCloneJob
        {
            Name = jobName,
            Operation = RCloneOperation.Copy,
            Transfers = 4,
            Verbosity = RCloneVerbosity.Normal,
            CreateLogFile = true,
            LogFilePath = Path.Combine(logDirectory, $"{jobName}.log"),
            DryRun = false
        };
        IsEditing = true;
    }

    [RelayCommand]
    private void DuplicateJob()
    {
        if (SelectedJob == null) return;

        var clone = CloneJob(SelectedJob);
        clone.Id = Guid.NewGuid().ToString();
        clone.Name = BuildUniqueJobName($"{SelectedJob.Name} Copy");
        clone.LastRun = null;
        clone.LastStatus = RCloneJobStatus.NotRun;
        clone.LastError = null;
        EditingJob = clone;
        IsEditing = true;
    }

    [RelayCommand]
    private void EditJob()
    {
        if (SelectedJob == null) return;

        EditingJob = CloneJob(SelectedJob);
        IsEditing = true;
    }

    [RelayCommand]
    private async Task SaveJobAsync()
    {
        if (EditingJob == null) return;

        var validationError = ValidateJob(EditingJob, isRunValidation: false);
        if (validationError is not null)
        {
            StatusMessage = validationError;
            return;
        }

        var existingJob = Jobs.FirstOrDefault(j => j.Id == EditingJob.Id);
        if (existingJob != null)
            Jobs.Remove(existingJob);

        Jobs.Add(EditingJob);
        SortJobs();
        await _jobStorageService.SaveJobsAsync(Jobs.ToList());

        SelectedJob = Jobs.FirstOrDefault(j => j.Id == EditingJob.Id);
        IsEditing = false;
        OnPropertyChanged(nameof(HasJobs));
        StatusMessage = $"Saved job '{EditingJob.Name}'.";
        EditingJob = null;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        EditingJob = null;
    }

    [RelayCommand]
    private async Task DeleteJobAsync()
    {
        if (SelectedJob == null) return;

        var result = MessageBox.Show(
            $"Delete job '{SelectedJob.Name}'?",
            "Confirm Job Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        Jobs.Remove(SelectedJob);
        OnPropertyChanged(nameof(HasJobs));
        await _jobStorageService.SaveJobsAsync(Jobs.ToList());
        StatusMessage = "Job deleted.";
        SelectedJob = null;
    }

    [RelayCommand]
    private async Task RunJobAsync()
    {
        if (SelectedJob == null || IsRunning) return;

        var validationError = ValidateJob(SelectedJob, isRunValidation: true);
        if (validationError is not null)
        {
            StatusMessage = validationError;
            return;
        }

        var effectiveDryRun = SelectedJob.DryRun;
        if (SelectedJob.Operation == RCloneOperation.Delete && SelectedJob.LastRun is null && !SelectedJob.DryRun)
        {
            effectiveDryRun = _settingsService.Load().DefaultDeleteDryRun;
        }

        if (SelectedJob.Operation == RCloneOperation.Delete)
        {
            var label = effectiveDryRun ? "Run delete job in dry-run mode?" : "Run delete job?";
            var result = MessageBox.Show(
                $"{label}\n\nJob: {SelectedJob.Name}",
                "Confirm Delete Job",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;
        }

        IsRunning = true;
        SelectedJob.LastStatus = RCloneJobStatus.Running;
        StatusMessage = $"Running '{SelectedJob.Name}'...";

        try
        {
            var args = BuildRCloneArgs(SelectedJob, effectiveDryRun);
            var request = new RCloneCommandRequest
            {
                Arguments = args,
                Operation = SelectedJob.Operation.ToString(),
                Category = "Job",
                JobId = SelectedJob.Id,
                JobName = SelectedJob.Name,
                OnOutput = output =>
                {
                    _appLogService.AddSessionEntry(new AppLogEntry
                    {
                        Timestamp = output.Timestamp,
                        Severity = output.IsError ? AppLogSeverity.Warning : AppLogSeverity.Info,
                        Category = "Job Output",
                        Operation = SelectedJob.Operation.ToString(),
                        JobId = SelectedJob.Id,
                        JobName = SelectedJob.Name,
                        Message = output.Text
                    });
                }
            };

            _appLogService.AddSessionEntry(new AppLogEntry
            {
                Category = "Job",
                Operation = SelectedJob.Operation.ToString(),
                JobId = SelectedJob.Id,
                JobName = SelectedJob.Name,
                Message = $"Started job '{SelectedJob.Name}'.",
                CommandText = string.Join(" ", args)
            });

            var result = await _processService.ExecuteDetailedAsync(request);

            SelectedJob.LastStatus = result.IsSuccess ? RCloneJobStatus.Success : RCloneJobStatus.Failed;
            SelectedJob.LastError = result.IsSuccess
                ? null
                : FirstNonEmpty(result.Error, result.Output, "rclone returned a non-zero exit code.");
            SelectedJob.LastRun = DateTime.Now;

            await _jobStorageService.SaveJobsAsync(Jobs.ToList());

            var historyEntry = new AppLogEntry
            {
                Timestamp = result.FinishedAt,
                Severity = result.IsSuccess ? AppLogSeverity.Info : AppLogSeverity.Error,
                Category = "Job Run",
                Operation = SelectedJob.Operation.ToString(),
                Message = result.IsSuccess
                    ? $"Job '{SelectedJob.Name}' completed successfully."
                    : $"Job '{SelectedJob.Name}' failed.",
                CommandText = result.CommandText,
                OutputSnippet = TakeSnippet(result.Output),
                ErrorSnippet = TakeSnippet(result.Error),
                ExitCode = result.ExitCode,
                JobId = SelectedJob.Id,
                JobName = SelectedJob.Name
            };

            await _appLogService.AddRunHistoryEntryAsync(historyEntry);
            _appLogService.AddSessionEntry(new AppLogEntry
            {
                Timestamp = historyEntry.Timestamp,
                Severity = historyEntry.Severity,
                Category = historyEntry.Category,
                Operation = historyEntry.Operation,
                Message = historyEntry.Message,
                CommandText = historyEntry.CommandText,
                OutputSnippet = historyEntry.OutputSnippet,
                ErrorSnippet = historyEntry.ErrorSnippet,
                ExitCode = historyEntry.ExitCode,
                JobId = historyEntry.JobId,
                JobName = historyEntry.JobName
            });

            StatusMessage = result.IsSuccess
                ? $"Job '{SelectedJob.Name}' completed."
                : $"Job '{SelectedJob.Name}' failed.";
        }
        catch (Exception ex)
        {
            SelectedJob.LastStatus = RCloneJobStatus.Failed;
            SelectedJob.LastError = ex.Message;
            SelectedJob.LastRun = DateTime.Now;
            await _jobStorageService.SaveJobsAsync(Jobs.ToList());

            var failureEntry = new AppLogEntry
            {
                Severity = AppLogSeverity.Error,
                Category = "Job",
                Operation = SelectedJob.Operation.ToString(),
                JobId = SelectedJob.Id,
                JobName = SelectedJob.Name,
                Message = $"Job '{SelectedJob.Name}' failed before completion.",
                ErrorSnippet = ex.Message
            };
            _appLogService.AddSessionEntry(failureEntry);
            await _appLogService.AddRunHistoryEntryAsync(failureEntry);
            StatusMessage = $"Job '{SelectedJob.Name}' failed: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }

    [RelayCommand]
    private void OpenHistory()
    {
        OpenJobHistoryRequested?.Invoke(SelectedJob?.Id);
    }

    [RelayCommand]
    private async Task ImportFromBatchFileAsync(string filePath)
    {
        var result = _batchImportService.ImportFromFile(filePath);

        foreach (var job in result.Jobs)
        {
            if (Jobs.Any(existing => string.Equals(existing.Name, job.Name, StringComparison.OrdinalIgnoreCase)))
                job.Name = BuildUniqueJobName(job.Name);

            Jobs.Add(job);
        }

        if (result.Jobs.Count > 0)
        {
            SortJobs();
            await _jobStorageService.SaveJobsAsync(Jobs.ToList());
            OnPropertyChanged(nameof(HasJobs));
            SelectedJob = result.Jobs[0];
        }

        var parts = new List<string>();
        if (result.Jobs.Count > 0)
            parts.Add($"Imported {result.Jobs.Count} job{(result.Jobs.Count > 1 ? "s" : "")}");
        if (result.SkippedLines.Count > 0)
            parts.Add($"skipped {result.SkippedLines.Count} unsupported line{(result.SkippedLines.Count > 1 ? "s" : "")}");

        StatusMessage = parts.Count > 0 ? string.Join(", ", parts) : "No rclone commands found in file";
        _appLogService.AddSessionEntry(new AppLogEntry
        {
            Category = "Import",
            Operation = "Batch Import",
            Message = StatusMessage ?? "Batch import completed."
        });
    }

    private string? ValidateJob(RCloneJob job, bool isRunValidation)
    {
        if (string.IsNullOrWhiteSpace(job.Name))
            return "Job name is required.";

        if (Jobs.Any(existing =>
            existing.Id != job.Id &&
            string.Equals(existing.Name, job.Name, StringComparison.OrdinalIgnoreCase)))
            return $"A job named '{job.Name}' already exists.";

        if (string.IsNullOrWhiteSpace(job.SourcePath))
            return "A source path is required.";

        if (job.SourceIsRemote && string.IsNullOrWhiteSpace(job.SourceRemoteName))
            return "A source remote name is required when the source is remote.";

        if (job.Operation != RCloneOperation.Delete)
        {
            if (string.IsNullOrWhiteSpace(job.DestinationPath))
                return "A destination path is required.";
            if (job.DestinationIsRemote && string.IsNullOrWhiteSpace(job.DestinationRemoteName))
                return "A destination remote name is required when the destination is remote.";
        }

        if (job.Transfers <= 0)
            return "Transfers must be greater than zero.";

        if (job.CreateLogFile && string.IsNullOrWhiteSpace(job.LogFilePath))
            return "A log file path is required when log file creation is enabled.";

        if (isRunValidation && string.IsNullOrWhiteSpace(_settingsService.Load().RCloneExePath))
            return "Configure rclone in Settings before running jobs.";

        return null;
    }

    internal static List<string> BuildRCloneArgs(RCloneJob job, bool effectiveDryRun)
    {
        var args = new List<string> { job.Operation.ToString().ToLowerInvariant() };

        var source = ComposePath(job.SourceIsRemote, job.SourceRemoteName, job.SourcePath);
        args.Add(source);

        if (job.Operation != RCloneOperation.Delete)
            args.Add(ComposePath(job.DestinationIsRemote, job.DestinationRemoteName, job.DestinationPath));

        if (job.Operation != RCloneOperation.Delete)
        {
            args.Add("--transfers");
            args.Add(job.Transfers.ToString());
        }

        if (effectiveDryRun)
            args.Add("--dry-run");

        if (!string.IsNullOrEmpty(job.MinAge))
        {
            args.Add("--min-age");
            args.Add(job.MinAge);
        }

        if (job.CreateLogFile && !string.IsNullOrEmpty(job.LogFilePath))
        {
            args.Add("--log-file");
            args.Add(job.LogFilePath);
        }

        switch (job.Verbosity)
        {
            case RCloneVerbosity.Quiet:
                args.Add("-q");
                break;
            case RCloneVerbosity.Verbose:
                args.Add("-v");
                break;
            case RCloneVerbosity.VeryVerbose:
                args.Add("-vv");
                break;
        }

        foreach (var pattern in job.IncludePatterns)
        {
            args.Add("--include");
            args.Add(pattern);
        }

        foreach (var pattern in job.ExcludePatterns)
        {
            args.Add("--exclude");
            args.Add(pattern);
        }

        args.AddRange(job.ExtraFlags);
        return args;
    }

    private static string ComposePath(bool isRemote, string? remoteName, string path)
    {
        return isRemote && !string.IsNullOrWhiteSpace(remoteName)
            ? $"{remoteName}:{path}"
            : path;
    }

    private RCloneJob CloneJob(RCloneJob job)
    {
        return new RCloneJob
        {
            Id = job.Id,
            Name = job.Name,
            Operation = job.Operation,
            SourcePath = job.SourcePath,
            SourceIsRemote = job.SourceIsRemote,
            SourceRemoteName = job.SourceRemoteName,
            DestinationPath = job.DestinationPath,
            DestinationIsRemote = job.DestinationIsRemote,
            DestinationRemoteName = job.DestinationRemoteName,
            Transfers = job.Transfers,
            CreateLogFile = job.CreateLogFile,
            LogFilePath = job.LogFilePath,
            Verbosity = job.Verbosity,
            DryRun = job.DryRun,
            IncludePatterns = new List<string>(job.IncludePatterns),
            ExcludePatterns = new List<string>(job.ExcludePatterns),
            MinAge = job.MinAge,
            ExtraFlags = new List<string>(job.ExtraFlags),
            LastRun = job.LastRun,
            LastStatus = job.LastStatus,
            LastError = job.LastError
        };
    }

    private string BuildUniqueJobName(string seed)
    {
        var name = seed;
        var counter = 2;
        while (Jobs.Any(job => string.Equals(job.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            name = $"{seed} {counter}";
            counter++;
        }

        return name;
    }

    private void SortJobs()
    {
        Jobs = new ObservableCollection<RCloneJob>(Jobs.OrderBy(j => j.Name, StringComparer.OrdinalIgnoreCase));
    }

    private static string TakeSnippet(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        return text.Length <= 500 ? text : $"{text[..500]}...";
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }
}
