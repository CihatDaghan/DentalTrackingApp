using Dental.Application.Common;

namespace Dental.Application.Notifications;

/// <summary>Bildirim olay anahtarları — ön yüz ikon/renk eşlemesini bu koddan yapar.</summary>
public static class NotificationEvents
{
    public const string AppointmentCreated = "appointment_created";
    public const string AppointmentCancelled = "appointment_cancelled";
    public const string PaymentReceived = "payment_received";
    public const string EInvoiceError = "einvoice_error";
    public const string EnabizRejected = "enabiz_rejected";
    public const string StockLow = "stock_low";
}

public sealed record NotificationDto(
    long Id,
    string EventType,
    string Title,
    string? Body,
    string? LinkPath,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc);

/// <param name="UserId">NULL = kiracıdaki herkese görünür (yayın bildirimi).</param>
/// <param name="TenantId">NULL ise bağlamdaki kiracı; job/callback bağlamında açıkça verilir.</param>
public sealed record NotificationCreateRequest(
    string EventType,
    string Title,
    string? Body = null,
    string? LinkPath = null,
    long? UserId = null,
    long? TenantId = null);

public sealed record NotificationListDto(PagedResult<NotificationDto> Page, int UnreadCount);

/// <summary>
/// Uygulama içi bildirim (topbar zili). <see cref="PublishAsync"/> ÇAĞIRANI ASLA PATLATMAZ —
/// bildirim yan etkidir; hata yalnız loglanır (iş akışı bildirim yüzünden başarısız olmamalı).
/// </summary>
public interface INotificationService
{
    Task PublishAsync(NotificationCreateRequest request, CancellationToken ct = default);

    Task<NotificationListDto> ListAsync(bool unreadOnly, int page, int pageSize, CancellationToken ct = default);

    Task<int> MarkReadAsync(long id, CancellationToken ct = default);

    Task<int> MarkAllReadAsync(CancellationToken ct = default);
}
