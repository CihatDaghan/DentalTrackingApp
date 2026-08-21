using System.Xml.Linq;

namespace Dental.EDocument.Ubl.Builders;

public static class UblNamespaces
{
    public static readonly XNamespace Invoice = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    public static readonly XNamespace CreditNote = "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2";
    public static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    public static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    public static readonly XNamespace Ext = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";
}
