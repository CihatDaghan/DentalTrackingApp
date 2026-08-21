namespace Dental.Domain.Enums;

/// <summary>
/// Reçete yaşam döngüsü. SubmittedToUss/Accepted/Rejected H aşaması (USS/Reçetem entegrasyonu)
/// için rezervedir; v1'de yalnız Draft→Printed kullanılır.
/// </summary>
public enum PrescriptionStatus : byte
{
    Draft = 1,
    Printed = 2,
    SubmittedToUss = 3,
    Accepted = 4,
    Rejected = 5,
}
