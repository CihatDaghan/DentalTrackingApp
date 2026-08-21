using Dental.Application.Abstractions;
using Dental.Application.Messaging;
using Dental.Domain.Common;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dental.Infrastructure.Messaging;

/// <summary>
/// Otomasyon iş mantığı — Hangfire job gövdelerinin çağırdığı tenant-içi işleyiciler.
/// Zaman hesapları TrTime ile yerel (TR, UTC+3) gün/saate göre yapılır.
/// </summary>
public sealed class MessageAutomationService(
    AppDbContext db,
    IClock clock,
    IMessageOutboxService outbox,
    ILogger<MessageAutomationService> logger) : IMessageAutomationService
{
    /// <summary>Gecikmiş ödeme hatırlatması aynı taksit için bu aralıkta tekrar gönderilmez.</summary>
    private static readonly TimeSpan OverdueReminderCooldown = TimeSpan.FromDays(7);

    public async Task<int> QueueAppointmentRemindersAsync(CancellationToken ct = default)
    {
        var rule = await ActiveRuleAsync(AutomationRuleType.AppointmentReminder, ct);
        if (rule is null) return 0;

        var now = clock.UtcNow;
        var windowEnd = now.AddHours(rule.OffsetHours);

        // Mükerrer koruması: ReminderState randevu üzerinde tutulur; kuyruğa alınan randevu
        // bir daha bu pencereye giremez (None → Queued tek yönlüdür).
        var appointments = await db.Appointments
            .Where(a => a.PatientId != null
                        && a.ReminderState == ReminderState.None
                        && a.StartUtc > now
                        && a.StartUtc <= windowEnd
                        && a.Status != AppointmentStatus.Cancelled
                        && a.Status != AppointmentStatus.NoShow
                        && a.Type != AppointmentType.EmptySlot
                        && a.Type != AppointmentType.Blocked)
            .OrderBy(a => a.StartUtc)
            .Take(500)
            .ToListAsync(ct);

        if (appointments.Count == 0) return 0;

        var doctorNames = await DoctorNamesAsync(appointments.Select(a => a.DoctorUserId).Distinct(), ct);

        var queued = 0;
        foreach (var appointment in appointments)
        {
            ct.ThrowIfCancellationRequested();
            var local = appointment.StartUtc + TrTime.Offset;
            await outbox.EnqueueAsync(new MessageEnqueueRequest(
                rule.TemplateKey,
                PatientId: appointment.PatientId,
                Channel: ChannelFor(rule.ChannelPolicy),
                Kind: MessageKind.Transactional,
                Params: new Dictionary<string, string>
                {
                    [MessagePlaceholders.AppointmentDate] = local.ToString("dd.MM.yyyy"),
                    [MessagePlaceholders.AppointmentTime] = local.ToString("HH:mm"),
                    [MessagePlaceholders.DoctorName] = doctorNames.GetValueOrDefault(appointment.DoctorUserId, "-"),
                },
                RefType: nameof(Appointment),
                RefId: appointment.Id), ct);

            appointment.ReminderState = ReminderState.Queued;
            queued++;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Randevu hatırlatmaları kuyruğa alındı. Adet={Count}", queued);
        return queued;
    }

    public async Task<int> QueueBirthdayGreetingsAsync(CancellationToken ct = default)
    {
        var rule = await ActiveRuleAsync(AutomationRuleType.Birthday, ct);
        if (rule is null) return 0;

        var today = TrTime.ToLocalDate(clock.UtcNow);
        var patients = await db.Patients.AsNoTracking()
            .Where(p => p.BirthDate != null
                        && p.BirthDate!.Value.Month == today.Month
                        && p.BirthDate!.Value.Day == today.Day)
            .Select(p => p.Id)
            .ToListAsync(ct);

        var queued = 0;
        foreach (var patientId in patients)
        {
            ct.ThrowIfCancellationRequested();
            // Doğum günü mesajı TİCARİDİR: izinsiz hastalar outbox'ta Skipped(NoConsent) olur.
            var dto = await outbox.EnqueueAsync(new MessageEnqueueRequest(
                rule.TemplateKey,
                PatientId: patientId,
                Channel: ChannelFor(rule.ChannelPolicy),
                Kind: MessageKind.Commercial,
                RefType: nameof(Patient),
                RefId: patientId), ct);
            if (dto.State != OutboundMessageState.Skipped) queued++;
        }

        if (patients.Count > 0)
            logger.LogInformation("Doğum günü mesajları işlendi. Hedef={Targeted} Kuyruk={Queued}",
                patients.Count, queued);
        return queued;
    }

    public async Task<int> QueuePaymentOverdueRemindersAsync(CancellationToken ct = default)
    {
        var rule = await ActiveRuleAsync(AutomationRuleType.PaymentOverdue, ct);
        if (rule is null) return 0;

        var today = TrTime.ToLocalDate(clock.UtcNow);
        var cooldownStart = clock.UtcNow - OverdueReminderCooldown;

        var overdue = await (
            from installment in db.PaymentPlanInstallments.AsNoTracking()
            join plan in db.PaymentPlans.AsNoTracking() on installment.PlanId equals plan.Id
            where installment.DueDate < today
                  && (installment.Status == InstallmentStatus.Pending
                      || installment.Status == InstallmentStatus.Partial)
            select new { installment.Id, plan.PatientId, installment.Amount, installment.PaidAmount })
            .Take(500)
            .ToListAsync(ct);

        if (overdue.Count == 0) return 0;

        // Aynı taksit için son 7 gün içinde mesaj varsa tekrar gönderilmez.
        var recent = await db.OutboundMessages.AsNoTracking()
            .Where(m => m.RefType == nameof(PaymentPlanInstallment) && m.CreatedAtUtc >= cooldownStart)
            .Select(m => m.RefId)
            .ToListAsync(ct);
        var recentIds = recent.Where(id => id.HasValue).Select(id => id!.Value).ToHashSet();

        var queued = 0;
        foreach (var row in overdue.Where(r => !recentIds.Contains(r.Id)))
        {
            ct.ThrowIfCancellationRequested();
            await outbox.EnqueueAsync(new MessageEnqueueRequest(
                rule.TemplateKey,
                PatientId: row.PatientId,
                Channel: ChannelFor(rule.ChannelPolicy),
                Kind: MessageKind.Transactional,
                Params: new Dictionary<string, string>
                {
                    [MessagePlaceholders.Balance] = (row.Amount - row.PaidAmount).ToString("N2"),
                },
                RefType: nameof(PaymentPlanInstallment),
                RefId: row.Id), ct);
            queued++;
        }

        if (queued > 0) logger.LogInformation("Gecikmiş ödeme hatırlatmaları kuyruğa alındı. Adet={Count}", queued);
        return queued;
    }

    public async Task<int> QueueRecallRemindersAsync(CancellationToken ct = default)
    {
        var rule = await ActiveRuleAsync(AutomationRuleType.Recall, ct);
        if (rule is null) return 0;

        // Kontrol planları gün bazlıdır: offset saat cinsinden verilir, güne çevrilir.
        var horizon = TrTime.ToLocalDate(clock.UtcNow).AddDays(Math.Max(rule.OffsetHours / 24, 0));
        var plans = await db.RecallPlans
            .Where(r => r.Status == RecallStatus.Planned
                        && r.SuggestedDate <= horizon
                        && r.LastReminderAtUtc == null)
            .OrderBy(r => r.SuggestedDate)
            .Take(500)
            .ToListAsync(ct);

        var queued = 0;
        foreach (var plan in plans)
        {
            ct.ThrowIfCancellationRequested();
            await outbox.EnqueueAsync(new MessageEnqueueRequest(
                rule.TemplateKey,
                PatientId: plan.PatientId,
                Channel: ChannelFor(rule.ChannelPolicy),
                Kind: MessageKind.Transactional,
                Params: new Dictionary<string, string>
                {
                    [MessagePlaceholders.AppointmentDate] = plan.SuggestedDate.ToString("dd.MM.yyyy"),
                },
                RefType: nameof(RecallPlan),
                RefId: plan.Id), ct);

            plan.LastReminderAtUtc = clock.UtcNow;
            queued++;
        }

        if (queued > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Kontrol hatırlatmaları kuyruğa alındı. Adet={Count}", queued);
        }
        return queued;
    }

    // ---- Yardımcılar ----

    private async Task<AutomationRule?> ActiveRuleAsync(AutomationRuleType ruleType, CancellationToken ct) =>
        await db.AutomationRules.AsNoTracking()
            .FirstOrDefaultAsync(r => r.RuleType == ruleType && r.IsEnabled, ct);

    /// <summary>Kanal politikasının ilk denenecek kanalı; fallback dispatcher'da uygulanır.</summary>
    private static MessageChannel ChannelFor(ChannelPolicy policy) => policy switch
    {
        ChannelPolicy.SmsOnly => MessageChannel.Sms,
        _ => MessageChannel.WhatsApp,
    };

    private async Task<Dictionary<long, string>> DoctorNamesAsync(
        IEnumerable<long> userIds, CancellationToken ct)
    {
        var ids = userIds.ToList();
        return await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FirstName + " " + u.LastName, ct);
    }
}
