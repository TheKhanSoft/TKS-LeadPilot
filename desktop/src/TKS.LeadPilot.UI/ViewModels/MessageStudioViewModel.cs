using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.UI.ViewModels;

public partial class MessageStudioViewModel : ViewModelBase
{
    private readonly IOutreachService _outreachService;
    private readonly IBusinessRepository _businessRepository;
    private readonly IAuditRepository _auditRepository;

    // Collections
    [ObservableProperty]
    private ObservableCollection<Business> _businesses = [];

    [ObservableProperty]
    private Business? _selectedBusiness;

    [ObservableProperty]
    private ObservableCollection<BusinessContact> _contacts = [];

    [ObservableProperty]
    private BusinessContact? _selectedContact;

    [ObservableProperty]
    private ObservableCollection<OutreachTemplate> _templates = [];

    [ObservableProperty]
    private OutreachTemplate? _selectedTemplate;

    [ObservableProperty]
    private ObservableCollection<OutreachMessage> _approvalQueue = [];

    [ObservableProperty]
    private OutreachMessage? _selectedQueueMessage;

    // Composer Fields
    [ObservableProperty]
    private OutreachChannel _selectedChannel = OutreachChannel.Email;

    [ObservableProperty]
    private string _subject = string.Empty;

    [ObservableProperty]
    private string _bodyTemplate = string.Empty;

    [ObservableProperty]
    private string _renderedPreview = string.Empty;

    [ObservableProperty]
    private string _recipientAddress = string.Empty;

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private string _statusMessage = "Select a business lead and template to draft customized outreach.";

    public MessageStudioViewModel(
        IOutreachService outreachService,
        IBusinessRepository businessRepository,
        IAuditRepository auditRepository)
    {
        _outreachService = outreachService;
        _businessRepository = businessRepository;
        _auditRepository = auditRepository;

        _ = InitializeAsync();
    }

    public async Task InitializeAsync()
    {
        try
        {
            IsLoading = true;
            var bizList = await _businessRepository.GetAllAsync();
            Businesses = new ObservableCollection<Business>(bizList.Where(b => b.LeadStatus != LeadStatus.DoNotContact));

            var tplList = await _outreachService.GetTemplatesAsync();
            Templates = new ObservableCollection<OutreachTemplate>(tplList);

            await RefreshQueueAsync();

            SelectedBusiness = Businesses.FirstOrDefault();
            SelectedTemplate = Templates.FirstOrDefault(t => t.Channel == SelectedChannel) ?? Templates.FirstOrDefault();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Init Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedChannelChanged(OutreachChannel value)
    {
        var match = Templates.FirstOrDefault(t => t.Channel == value);
        if (match != null)
        {
            SelectedTemplate = match;
        }
        UpdateRecipient();
        _ = UpdatePreviewAsync();
    }

    partial void OnSelectedBusinessChanged(Business? value)
    {
        if (value != null)
        {
            Contacts = new ObservableCollection<BusinessContact>(value.Contacts);
            SelectedContact = Contacts.FirstOrDefault();
            UpdateRecipient();
            _ = UpdatePreviewAsync();
        }
    }

    partial void OnSelectedContactChanged(BusinessContact? value)
    {
        UpdateRecipient();
        _ = UpdatePreviewAsync();
    }

    partial void OnSelectedTemplateChanged(OutreachTemplate? value)
    {
        if (value != null)
        {
            SelectedChannel = value.Channel;
            Subject = value.SubjectTemplate ?? string.Empty;
            BodyTemplate = value.BodyTemplate;
            _ = UpdatePreviewAsync();
        }
    }

    partial void OnBodyTemplateChanged(string value)
    {
        _ = UpdatePreviewAsync();
    }

    partial void OnSubjectChanged(string value)
    {
        _ = UpdatePreviewAsync();
    }

    private void UpdateRecipient()
    {
        if (SelectedBusiness == null)
        {
            RecipientAddress = string.Empty;
            return;
        }

        RecipientAddress = SelectedChannel == OutreachChannel.Email
            ? SelectedContact?.Email ?? SelectedBusiness.EmailPrimary ?? string.Empty
            : SelectedContact?.Phone ?? SelectedBusiness.PhonePrimary ?? string.Empty;
    }

    private async Task UpdatePreviewAsync()
    {
        if (SelectedBusiness == null || string.IsNullOrWhiteSpace(BodyTemplate))
        {
            RenderedPreview = "Select a business lead and template to render preview.";
            return;
        }

        var audit = await _auditRepository.GetLatestAuditForBusinessAsync(SelectedBusiness.Id);
        RenderedPreview = _outreachService.RenderTemplate(
            BodyTemplate,
            SelectedBusiness,
            SelectedContact,
            audit,
            "Modern High-Speed Web Solutions");
    }

    [RelayCommand]
    public async Task RefreshQueueAsync()
    {
        try
        {
            var queue = await _outreachService.GetApprovalQueueAsync();
            ApprovalQueue = new ObservableCollection<OutreachMessage>(queue);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Queue error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task CreateDraft()
    {
        if (SelectedBusiness == null)
        {
            StatusMessage = "Please select a target business lead.";
            return;
        }

        try
        {
            var msg = new OutreachMessage
            {
                BusinessId = SelectedBusiness.Id,
                ContactId = SelectedContact?.Id,
                Channel = SelectedChannel,
                Subject = Subject,
                Body = BodyTemplate,
                RenderedBody = RenderedPreview,
                RecipientAddress = RecipientAddress,
                ApprovalStatus = OutreachApprovalStatus.PendingApproval
            };

            await _outreachService.CreateCustomDraftAsync(msg);
            await RefreshQueueAsync();
            StatusMessage = $"✅ Draft created for '{SelectedBusiness.Name}' ({SelectedChannel}). Added to Approval Queue.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Draft Error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task ApproveMessage(OutreachMessage message)
    {
        if (message == null) return;
        try
        {
            await _outreachService.ApproveMessageAsync(message.Id);
            message.ApprovalStatus = OutreachApprovalStatus.Approved;
            StatusMessage = "Message approved for transmission.";
            await RefreshQueueAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Approve error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task RejectMessage(OutreachMessage message)
    {
        if (message == null) return;
        try
        {
            await _outreachService.RejectMessageAsync(message.Id);
            message.ApprovalStatus = OutreachApprovalStatus.Rejected;
            StatusMessage = "Message rejected.";
            await RefreshQueueAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Reject error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task SendViaEmail(OutreachMessage message)
    {
        if (message == null) return;

        try
        {
            var uri = _outreachService.BuildMailtoUri(message);
            Process.Start(new ProcessStartInfo
            {
                FileName = uri,
                UseShellExecute = true
            });

            await _outreachService.MarkAsSentAsync(message.Id);
            StatusMessage = $"Launched email client for '{message.RecipientAddress}'. Marked as Sent!";
            await RefreshQueueAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Email launch error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task SendViaWhatsApp(OutreachMessage message)
    {
        if (message == null) return;

        try
        {
            var uri = _outreachService.BuildWhatsAppUri(message);
            Process.Start(new ProcessStartInfo
            {
                FileName = uri,
                UseShellExecute = true
            });

            await _outreachService.MarkAsSentAsync(message.Id);
            StatusMessage = $"Opened WhatsApp for '{message.RecipientAddress}'. Marked as Sent!";
            await RefreshQueueAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"WhatsApp launch error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task MarkDoNotContact()
    {
        if (SelectedBusiness == null) return;

        try
        {
            SelectedBusiness.LeadStatus = LeadStatus.DoNotContact;
            SelectedBusiness.PipelineStageId = "stage-do-not-contact";
            SelectedBusiness.AddTag("DoNotContact");
            SelectedBusiness.AddTag("Suppressed");
            await _businessRepository.UpdateAsync(SelectedBusiness);

            Businesses.Remove(SelectedBusiness);
            SelectedBusiness = Businesses.FirstOrDefault();
            StatusMessage = "Lead marked as Do-Not-Contact and added to suppression list.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Suppression error: {ex.Message}";
        }
    }
}
