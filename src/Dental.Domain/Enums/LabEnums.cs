namespace Dental.Domain.Enums;

/// <summary>
/// Laboratuvar vakası durum makinesi. Gecikmiş = DueDate &lt; bugün &amp;&amp; Status &lt; Received
/// (sorgu bazlı bayrak, kalıcı yazılmaz). Her geçiş LabCaseStatusHistory'ye kaydedilir.
/// </summary>
public enum LabCaseStatus : byte
{
    Draft = 1,
    Sent = 2,
    InLab = 3,
    /// <summary>Prova.</summary>
    TryIn = 4,
    Received = 5,
    Delivered = 6,
    /// <summary>Yeniden yapım.</summary>
    Redo = 7,
}
