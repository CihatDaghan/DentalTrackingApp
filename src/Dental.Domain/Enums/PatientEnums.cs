namespace Dental.Domain.Enums;

public enum IdentityType : byte
{
    Tckn = 1,
    Passport = 2,
}

public enum Gender : byte
{
    Unknown = 0,
    Male = 1,
    Female = 2,
}

public enum ConsentType : byte
{
    KvkkProcessing = 1,
    SmsInfo = 2,
    SmsMarketing = 3,
    WhatsApp = 4,
    Email = 5,
}

public enum ConsentSource : byte
{
    Form = 1,
    Verbal = 2,
    SmsLink = 3,
}
