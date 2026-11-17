using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.UI.ViewModels;

public partial class PipelineViewModel : ViewModelBase
{
    private readonly ICrmService _crmService;
    private readonly ILeadScoringEngine _scoringEngine;
    private readonly ITksServiceMatcher _serviceMatcher;
    private readonly IAuditRepository _auditRepository;

    // Collections
    [ObservableProperty]
    private ObservableCollection<PipelineStage> _stages = [];

    [ObservableProperty]
    private PipelineStage? _selectedStage;

    [ObservableProperty]
    private ObservableCollection<Business> _stageBusinesses = [];

    [ObservableProperty]
    private Business? _selectedBusiness;

    // Explainable Scoring State (Stage 9)
    [ObservableProperty]
    private LeadScore? _currentScore;

    [ObservableProperty]
    private ObservableCollection<LeadScoreComponent> _scoreComponents = [];

    // Service Matching State (Stage 9)
    [ObservableProperty]
    private ObservableCollection<TksServiceMatch> _matchedServices = [];

    // CRM Tasks & Notes (Stage 9)
    [ObservableProperty]
    private ObservableCollection<SalesTask> _tasks = [];

    [ObservableProperty]
    private ObservableCollection<SalesNote> _notes = [];

    [ObservableProperty]
    private string _newTaskTitle = string.Empty;

    [ObservableProperty]
    private string _newNoteContent = string.Empty;

    // Manual Score Override Modal
    [ObservableProperty]
    private bool _isOverrideModalOpen = false;

    [ObservableProperty]
    private int _overrideScore = 80;

    [ObservableProperty]
    private string _overrideReason = string.Empty;

    // UI Status
    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private string _statusMessage = "Select a business lead to inspect pipeline status and score components.";

    public PipelineViewModel(
        ICrmService crmService,
        ILeadScoringEngine scoringEngine,
        ITksServiceMatcher serviceMatcher,
        IAuditRepository auditRepository)
    {
        _crmService = crmService;
        _scoringEngine = scoringEngine;
        _serviceMatcher = serviceMatcher;
        _auditRepository = auditRepository;

        _ = InitializeAsync();
    }

    public async Task InitializeAsync()
    {
        try
        {
            IsLoading = true;
            var stagesList = await _crmService.GetPipelineStagesAsync();
            Stages = new ObservableCollection<PipelineStage>(stagesList);

            SelectedStage = Stages.FirstOrDefault(s => s.Code == "QUALIFIED") ?? Stages.FirstOrDefault();
            await LoadBusinessesForStage();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Pipeline Init Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedStageChanged(PipelineStage? value)
    {
        if (value != null)
        {
            _ = LoadBusinessesForStage();
        }
    }

    partial void OnSelectedBusinessChanged(Business? value)
    {
        if (value != null)
        {
            _ = LoadBusinessDetails(value);
        }
    }

    private async Task LoadBusinessesForStage()
    {
        if (SelectedStage == null) return;

        try
        {
            var board = await _crmService.GetPipelineBoardAsync();
            if (board.TryGetValue(SelectedStage.Id, out var list))
            {
                StageBusinesses = new ObservableCollection<Business>(list);
            }
            else
            {
                StageBusinesses.Clear();
            }

            SelectedBusiness = StageBusinesses.FirstOrDefault();
            StatusMessage = $"Loaded {StageBusinesses.Count} leads in stage '{SelectedStage.Name}'.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading stage leads: {ex.Message}";
        }
    }

    public async Task LoadBusinessDetails(Business business)
    {
        try
        {
            var audit = await _auditRepository.GetLatestAuditForBusinessAsync(business.Id);

            // 1. Explainable Lead Score
            var score = await _scoringEngine.GetLatestScoreForBusinessAsync(business.Id);
            if (score == null)
            {
                score = await _scoringEngine.CalculateScoreAsync(business, audit);
            }
            CurrentScore = score;
            ScoreComponents = new ObservableCollection<LeadScoreComponent>(score.Components);

            // 2. Matched TKS Services
            var matches = await _serviceMatcher.MatchServicesAsync(business, audit, score);
            MatchedServices = new ObservableCollection<TksServiceMatch>(matches);

            // 3. Tasks
            var tasksList = await _crmService.GetTasksForBusinessAsync(business.Id);
            Tasks = new ObservableCollection<SalesTask>(tasksList);

            // 4. Notes
            var notesList = await _crmService.GetNotesForBusinessAsync(business.Id);
            Notes = new ObservableCollection<SalesNote>(notesList);

            StatusMessage = $"Inspecting '{business.Name}'. Opportunity Score: {business.LeadScore}/100.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading details for {business.Name}: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task RecalculateScore()
    {
        if (SelectedBusiness == null) return;

        try
        {
            var audit = await _auditRepository.GetLatestAuditForBusinessAsync(SelectedBusiness.Id);
            var score = await _scoringEngine.CalculateScoreAsync(SelectedBusiness, audit);
            CurrentScore = score;
            ScoreComponents = new ObservableCollection<LeadScoreComponent>(score.Components);
            StatusMessage = $"Recalculated opportunity score: {score.TotalScore}/100.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Recalculate error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task MoveToStage(string targetStageId)
    {
        if (SelectedBusiness == null) return;

        try
        {
            var ok = await _crmService.MoveLeadStageAsync(SelectedBusiness.Id, targetStageId);
            if (ok)
            {
                var targetStage = Stages.FirstOrDefault(s => s.Id == targetStageId);
                StatusMessage = $"Moved '{SelectedBusiness.Name}' to '{targetStage?.Name ?? targetStageId}'.";
                await LoadBusinessesForStage();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Move stage error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task AddTask()
    {
        if (SelectedBusiness == null || string.IsNullOrWhiteSpace(NewTaskTitle)) return;

        try
        {
            var task = new SalesTask
            {
                BusinessId = SelectedBusiness.Id,
                Title = NewTaskTitle.Trim(),
                DueDateUtc = DateTime.UtcNow.AddDays(2),
                Priority = Core.Enums.TaskPriority.Medium
            };

            await _crmService.CreateTaskAsync(task);
            Tasks.Insert(0, task);
            NewTaskTitle = string.Empty;
            StatusMessage = "Task added successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Add task error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task ToggleTaskComplete(SalesTask task)
    {
        if (task == null) return;

        try
        {
            await _crmService.CompleteTaskAsync(task.Id);
            task.IsCompleted = true;
            task.CompletedAtUtc = DateTime.UtcNow;
            StatusMessage = $"Task '{task.Title}' marked as complete.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Complete task error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task DeleteTask(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId)) return;

        try
        {
            await _crmService.DeleteTaskAsync(taskId);
            var item = Tasks.FirstOrDefault(t => t.Id == taskId);
            if (item != null) Tasks.Remove(item);
            StatusMessage = "Task deleted.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Delete task error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task AddNote()
    {
        if (SelectedBusiness == null || string.IsNullOrWhiteSpace(NewNoteContent)) return;

        try
        {
            var note = new SalesNote
            {
                BusinessId = SelectedBusiness.Id,
                Content = NewNoteContent.Trim(),
                AuthorName = "Operator",
                CreatedAtUtc = DateTime.UtcNow
            };

            await _crmService.AddNoteAsync(note);
            Notes.Insert(0, note);
            NewNoteContent = string.Empty;
            StatusMessage = "Sales activity note logged.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Add note error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task DeleteNote(string noteId)
    {
        if (string.IsNullOrWhiteSpace(noteId)) return;

        try
        {
            await _crmService.DeleteNoteAsync(noteId);
            var item = Notes.FirstOrDefault(n => n.Id == noteId);
            if (item != null) Notes.Remove(item);
            StatusMessage = "Note deleted.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Delete note error: {ex.Message}";
        }
    }

    // --- Score Manual Override ---
    [RelayCommand]
    public void OpenOverrideModal()
    {
        if (SelectedBusiness == null) return;
        OverrideScore = SelectedBusiness.LeadScore;
        OverrideReason = string.Empty;
        IsOverrideModalOpen = true;
    }

    [RelayCommand]
    public void CloseOverrideModal()
    {
        IsOverrideModalOpen = false;
    }

    [RelayCommand]
    public async Task ApplyManualOverride()
    {
        if (SelectedBusiness == null || string.IsNullOrWhiteSpace(OverrideReason))
        {
            StatusMessage = "Please specify a mandatory audit reason for the score override.";
            return;
        }

        try
        {
            var updatedScore = await _scoringEngine.ApplyManualOverrideAsync(SelectedBusiness.Id, OverrideScore, OverrideReason);
            CurrentScore = updatedScore;
            ScoreComponents = new ObservableCollection<LeadScoreComponent>(updatedScore.Components);
            SelectedBusiness.LeadScore = updatedScore.TotalScore;

            CloseOverrideModal();
            StatusMessage = $"Manual score override applied: {updatedScore.TotalScore}/100.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Override error: {ex.Message}";
        }
    }
}
