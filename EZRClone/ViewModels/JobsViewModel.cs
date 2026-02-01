using System.Collections.ObjectModel;
using System.IO;
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

    public JobsViewModel(
        IJobStorageService jobStorageService,
        IRCloneConfigService configService,
        IRCloneProcessService processService,
        IAppSettingsService settingsService)
    {
        _jobStorageService = jobStorageService;
        _configService = configService;
        _processService = processService;
        _settingsService = settingsService;
        
        _ = LoadJobsAsync();
        _ = LoadRemotesAsync();
    }

    private async Task LoadJobsAsync()
    {
        var jobs = await _jobStorageService.LoadJobsAsync();
        Jobs = new ObservableCollection<RCloneJob>(jobs);
        OnPropertyChanged(nameof(HasJobs));
    }

    [RelayCommand]
    private async Task LoadJobsCommandAsync()
    {
        await LoadJobsAsync();
    }

    private async Task LoadRemotesAsync()
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
    }

    [RelayCommand]
    private void AddJob()
    {
        var jobNumber = Jobs.Count + 1;
        var jobName = $"Job {jobNumber}";

        // Ensure unique name
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
            LogFilePath = Path.Combine(logDirectory, $"{jobName}.log")
        };
        IsEditing = true;
    }

    [RelayCommand]
    private void EditJob()
    {
        if (SelectedJob == null) return;
        
        // Create a copy to edit
        EditingJob = new RCloneJob
        {
            Id = SelectedJob.Id,
            Name = SelectedJob.Name,
            Operation = SelectedJob.Operation,
            SourcePath = SelectedJob.SourcePath,
            SourceIsRemote = SelectedJob.SourceIsRemote,
            SourceRemoteName = SelectedJob.SourceRemoteName,
            DestinationPath = SelectedJob.DestinationPath,
            DestinationIsRemote = SelectedJob.DestinationIsRemote,
            DestinationRemoteName = SelectedJob.DestinationRemoteName,
            Transfers = SelectedJob.Transfers,
            CreateLogFile = SelectedJob.CreateLogFile,
            LogFilePath = SelectedJob.LogFilePath,
            Verbosity = SelectedJob.Verbosity,
            IncludePatterns = new List<string>(SelectedJob.IncludePatterns),
            ExcludePatterns = new List<string>(SelectedJob.ExcludePatterns),
            LastRun = SelectedJob.LastRun,
            LastStatus = SelectedJob.LastStatus,
            LastError = SelectedJob.LastError
        };
        IsEditing = true;
    }

    [RelayCommand]
    private async Task SaveJobAsync()
    {
        if (EditingJob == null) return;

        var existingJob = Jobs.FirstOrDefault(j => j.Id == EditingJob.Id);
        if (existingJob != null)
        {
            Jobs.Remove(existingJob);
        }
        
        Jobs.Add(EditingJob);
        await _jobStorageService.SaveJobsAsync(Jobs.ToList());
        
        SelectedJob = EditingJob;
        IsEditing = false;
        OnPropertyChanged(nameof(HasJobs));
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
        
        Jobs.Remove(SelectedJob);
        OnPropertyChanged(nameof(HasJobs));
        await _jobStorageService.SaveJobsAsync(Jobs.ToList());
        SelectedJob = null;
    }

    [RelayCommand]
    private async Task RunJobAsync()
    {
        if (SelectedJob == null || IsRunning) return;

        IsRunning = true;
        SelectedJob.LastStatus = RCloneJobStatus.Running;

        try
        {
            var args = BuildRCloneArgs(SelectedJob);
            var (exitCode, output, error) = await _processService.ExecuteAsync(args);

            if (exitCode == 0)
            {
                SelectedJob.LastStatus = RCloneJobStatus.Success;
                SelectedJob.LastError = null;
            }
            else
            {
                SelectedJob.LastStatus = RCloneJobStatus.Failed;
                SelectedJob.LastError = error;
            }

            SelectedJob.LastRun = DateTime.Now;
            await _jobStorageService.SaveJobsAsync(Jobs.ToList());
        }
        catch (Exception ex)
        {
            SelectedJob.LastStatus = RCloneJobStatus.Failed;
            SelectedJob.LastError = ex.Message;
        }
        finally
        {
            IsRunning = false;
        }
    }

    private List<string> BuildRCloneArgs(RCloneJob job)
    {
        var args = new List<string>();

        // Operation
        args.Add(job.Operation.ToString().ToLower());

        // Source
        var source = job.SourceIsRemote && !string.IsNullOrEmpty(job.SourceRemoteName)
            ? $"{job.SourceRemoteName}:{job.SourcePath}"
            : job.SourcePath;
        args.Add(source);

        // Destination
        var dest = job.DestinationIsRemote && !string.IsNullOrEmpty(job.DestinationRemoteName)
            ? $"{job.DestinationRemoteName}:{job.DestinationPath}"
            : job.DestinationPath;
        args.Add(dest);

        // Options
        args.Add("--transfers");
        args.Add(job.Transfers.ToString());

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

        return args;
    }
}
