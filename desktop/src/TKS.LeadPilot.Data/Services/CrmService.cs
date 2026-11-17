using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.Data.Services;

public class CrmService : ICrmService
{
    private readonly AppDbContext _context;

    public CrmService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<PipelineStage>> GetPipelineStagesAsync(CancellationToken ct = default)
    {
        return await _context.PipelineStages
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync(ct);
    }

    public async Task<Dictionary<string, List<Business>>> GetPipelineBoardAsync(CancellationToken ct = default)
    {
        var stages = await GetPipelineStagesAsync(ct);
        var businesses = await _context.Businesses
            .Include(b => b.PipelineStage)
            .Include(b => b.Contacts)
            .Include(b => b.Tasks)
            .Where(b => !b.IsDeleted)
            .OrderByDescending(b => b.LeadScore)
            .ToListAsync(ct);

        var board = new Dictionary<string, List<Business>>();
        foreach (var stage in stages)
        {
            board[stage.Id] = businesses.Where(b => b.PipelineStageId == stage.Id).ToList();
        }

        return board;
    }

    public async Task<bool> MoveLeadStageAsync(string businessId, string targetStageId, CancellationToken ct = default)
    {
        var business = await _context.Businesses.FirstOrDefaultAsync(b => b.Id == businessId, ct);
        if (business == null) return false;

        var targetStage = await _context.PipelineStages.FirstOrDefaultAsync(s => s.Id == targetStageId, ct);
        if (targetStage == null) return false;

        business.PipelineStageId = targetStage.Id;
        business.UpdatedAtUtc = DateTime.UtcNow;
        business.Version++;

        // Sync lead status
        business.LeadStatus = targetStage.Code switch
        {
            "DISCOVERED" => LeadStatus.Discovered,
            "TO_REVIEW" => LeadStatus.ToReview,
            "QUALIFIED" => LeadStatus.Qualified,
            "RESEARCHING" => LeadStatus.Researching,
            "CONTACT_DRAFTED" => LeadStatus.ContactDrafted,
            "AWAITING_APPROVAL" => LeadStatus.AwaitingApproval,
            "CONTACTED" => LeadStatus.Contacted,
            "RESPONDED" => LeadStatus.Responded,
            "MEETING_SCHEDULED" => LeadStatus.MeetingScheduled,
            "PROPOSAL_SENT" => LeadStatus.ProposalSent,
            "NEGOTIATION" => LeadStatus.Negotiation,
            "WON" => LeadStatus.Won,
            "LOST" => LeadStatus.Lost,
            "DO_NOT_CONTACT" => LeadStatus.DoNotContact,
            _ => business.LeadStatus
        };

        // Add auto-generated sales note tracking the transition
        var note = new SalesNote
        {
            BusinessId = business.Id,
            Content = $"Moved stage to '{targetStage.Name}'",
            AuthorName = "System",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.SalesNotes.Add(note);

        await _context.SaveChangesAsync(ct);
        return true;
    }

    // --- Task Management ---

    public async Task<List<SalesTask>> GetTasksForBusinessAsync(string businessId, CancellationToken ct = default)
    {
        return await _context.SalesTasks
            .Where(t => t.BusinessId == businessId)
            .OrderBy(t => t.IsCompleted)
            .ThenBy(t => t.DueDateUtc)
            .ToListAsync(ct);
    }

    public async Task<List<SalesTask>> GetPendingTasksAsync(CancellationToken ct = default)
    {
        return await _context.SalesTasks
            .Include(t => t.Business)
            .Where(t => !t.IsCompleted)
            .OrderBy(t => t.DueDateUtc)
            .ToListAsync(ct);
    }

    public async Task<SalesTask> CreateTaskAsync(SalesTask task, CancellationToken ct = default)
    {
        task.CreatedAtUtc = DateTime.UtcNow;
        _context.SalesTasks.Add(task);
        await _context.SaveChangesAsync(ct);
        return task;
    }

    public async Task<bool> CompleteTaskAsync(string taskId, CancellationToken ct = default)
    {
        var task = await _context.SalesTasks.FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (task == null) return false;

        task.IsCompleted = true;
        task.CompletedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteTaskAsync(string taskId, CancellationToken ct = default)
    {
        var task = await _context.SalesTasks.FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (task == null) return false;

        _context.SalesTasks.Remove(task);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    // --- Notes Management ---

    public async Task<List<SalesNote>> GetNotesForBusinessAsync(string businessId, CancellationToken ct = default)
    {
        return await _context.SalesNotes
            .Where(n => n.BusinessId == businessId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<SalesNote> AddNoteAsync(SalesNote note, CancellationToken ct = default)
    {
        note.CreatedAtUtc = DateTime.UtcNow;
        note.UpdatedAtUtc = DateTime.UtcNow;
        _context.SalesNotes.Add(note);
        await _context.SaveChangesAsync(ct);
        return note;
    }

    public async Task<bool> DeleteNoteAsync(string noteId, CancellationToken ct = default)
    {
        var note = await _context.SalesNotes.FirstOrDefaultAsync(n => n.Id == noteId, ct);
        if (note == null) return false;

        _context.SalesNotes.Remove(note);
        await _context.SaveChangesAsync(ct);
        return true;
    }
}
