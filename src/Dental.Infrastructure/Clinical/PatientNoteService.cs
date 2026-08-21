using Dental.Application.Abstractions;
using Dental.Application.Clinical;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Clinical;

/// <summary>Hasta notları. Düzenleme/silme yalnız yazara veya Owner/Manager'a açıktır; IsPinned üstte listelenir.</summary>
public sealed class PatientNoteService(
    AppDbContext db,
    ITenantContext tenant,
    IValidator<PatientNoteUpsertRequest> validator) : IPatientNoteService
{
    public async Task<IReadOnlyList<PatientNoteDto>> ListAsync(long patientId, CancellationToken ct = default)
    {
        if (!await db.Patients.AnyAsync(p => p.Id == patientId, ct))
            throw new KeyNotFoundException("Hasta bulunamadı.");
        return await Project(db.PatientNotes.AsNoTracking()
                .Where(n => n.PatientId == patientId)
                .OrderByDescending(n => n.IsPinned)
                .ThenByDescending(n => n.CreatedAtUtc)
                .ThenByDescending(n => n.Id))
            .ToListAsync(ct);
    }

    public async Task<PatientNoteDto> CreateAsync(
        long patientId, PatientNoteUpsertRequest request, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        if (!await db.Patients.AnyAsync(p => p.Id == patientId, ct))
            throw new KeyNotFoundException("Hasta bulunamadı.");

        var note = new PatientNote
        {
            PatientId = patientId,
            AuthorUserId = tenant.UserId
                ?? throw new InvalidOperationException("Not eklemek için oturum gereklidir."),
            NoteText = request.NoteText.Trim(),
            IsPinned = request.IsPinned,
            ColorHex = request.ColorHex,
        };
        db.PatientNotes.Add(note);
        await db.SaveChangesAsync(ct);
        return await GetDtoAsync(note.Id, ct);
    }

    public async Task<PatientNoteDto> UpdateAsync(
        long patientId, long noteId, PatientNoteUpsertRequest request, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var note = await FindOwnedAsync(patientId, noteId, ct);
        await EnsureCanModifyAsync(note, ct);

        note.NoteText = request.NoteText.Trim();
        note.IsPinned = request.IsPinned;
        note.ColorHex = request.ColorHex;
        await db.SaveChangesAsync(ct);
        return await GetDtoAsync(noteId, ct);
    }

    public async Task DeleteAsync(long patientId, long noteId, CancellationToken ct = default)
    {
        var note = await FindOwnedAsync(patientId, noteId, ct);
        await EnsureCanModifyAsync(note, ct);
        db.PatientNotes.Remove(note); // interceptor soft delete'e çevirir
        await db.SaveChangesAsync(ct);
    }

    // ---- Yardımcılar ----

    private async Task<PatientNote> FindOwnedAsync(long patientId, long noteId, CancellationToken ct) =>
        await db.PatientNotes.FirstOrDefaultAsync(n => n.Id == noteId && n.PatientId == patientId, ct)
            ?? throw new KeyNotFoundException("Not bulunamadı.");

    /// <summary>Yalnız yazar veya Owner/Manager düzenler-siler.</summary>
    private async Task EnsureCanModifyAsync(PatientNote note, CancellationToken ct)
    {
        var userId = tenant.UserId;
        if (userId == note.AuthorUserId) return;
        var userType = await db.Users.Where(u => u.Id == userId).Select(u => (UserType?)u.UserType)
            .FirstOrDefaultAsync(ct);
        if (userType is UserType.Owner or UserType.Manager) return;
        throw new UnauthorizedAccessException("Notu yalnız yazarı veya Yönetici düzenleyebilir/silebilir.");
    }

    private IQueryable<PatientNoteDto> Project(IQueryable<PatientNote> source) =>
        from n in source
        join u in db.Users on n.AuthorUserId equals u.Id into uj
        from u in uj.DefaultIfEmpty()
        select new PatientNoteDto(
            n.Id, n.PatientId, n.AuthorUserId,
            u != null ? u.FirstName + " " + u.LastName : "-",
            n.NoteText, n.IsPinned, n.ColorHex, n.CreatedAtUtc, n.UpdatedAtUtc);

    private async Task<PatientNoteDto> GetDtoAsync(long id, CancellationToken ct) =>
        await Project(db.PatientNotes.AsNoTracking().Where(n => n.Id == id)).FirstAsync(ct);
}
