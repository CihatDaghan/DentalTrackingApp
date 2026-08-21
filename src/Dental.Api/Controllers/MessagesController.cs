using Dental.Api.Auth;
using Dental.Application.Common;
using Dental.Application.Messaging;
using Dental.Domain.Enums;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

/// <summary>
/// Giden mesaj uçları. Gönderim doğrudan sürücüye değil OUTBOX'a yazar; kayıt Pending
/// oluşur ve MessageDispatchJob (*/1 dk) gönderir. İzinsiz ticari mesajlar Skipped(NoConsent)
/// kaydı olarak görünür — "neden gitmedi" sorusu ekrandan yanıtlanabilsin diye.
/// </summary>
[ApiController]
[Route("api/v1/messages")]
public sealed class MessagesController(
    IMessageOutboxService outbox,
    IMessageDispatcher dispatcher,
    IValidator<MessageSendRequest> sendValidator,
    IValidator<BulkMessageRequest> bulkValidator) : ControllerBase
{
    [HttpGet]
    [HasPermission("messaging.read")]
    public async Task<ActionResult<PagedResult<OutboundMessageDto>>> List(
        [FromQuery] MessageChannel? channel,
        [FromQuery] OutboundMessageState? state,
        [FromQuery] long? patientId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
        => Ok(await outbox.ListAsync(new MessageListQuery(channel, state, patientId, from, to, page, pageSize), ct));

    [HttpGet("{id:long}")]
    [HasPermission("messaging.read")]
    public async Task<ActionResult<OutboundMessageDto>> Get(long id, CancellationToken ct)
        => Ok(await outbox.GetAsync(id, ct));

    [HttpPost]
    [HasPermission("messaging.send")]
    public async Task<ActionResult<OutboundMessageDto>> Send(MessageSendRequest request, CancellationToken ct)
    {
        await sendValidator.ValidateAndThrowAsync(request, ct);
        var dto = await outbox.EnqueueAsync(new MessageEnqueueRequest(
            request.TemplateKey,
            PatientId: request.PatientId,
            Channel: request.Channel,
            Kind: request.Kind,
            Params: request.Params,
            ScheduledAtUtc: request.ScheduledAtUtc,
            RefType: "Manual",
            BodyOverride: request.BodyOverride), ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    /// <summary>
    /// Toplu gönderim: filtreye uyan hastalara kuyruğa alır. Yanıtta hedeflenen ve
    /// izin/numara nedeniyle atlanan sayılar döner (İYS/KVKK raporlaması).
    /// </summary>
    [HttpPost("bulk")]
    [HasPermission("messaging.bulk")]
    public async Task<ActionResult<BulkMessageResult>> Bulk(BulkMessageRequest request, CancellationToken ct)
    {
        await bulkValidator.ValidateAndThrowAsync(request, ct);
        return Ok(await outbox.EnqueueBulkAsync(request, ct));
    }

    /// <summary>
    /// Kuyruğu elle tetikler (job'ı beklemeden). Operasyon/teşhis ucudur;
    /// işlenen mesaj sayısını döner.
    /// </summary>
    [HttpPost("dispatch")]
    [HasPermission("messaging.send")]
    public async Task<ActionResult<int>> Dispatch([FromQuery] int batchSize = 100, CancellationToken ct = default)
        => Ok(await dispatcher.DispatchPendingAsync(Math.Clamp(batchSize, 1, 500), ct));
}
