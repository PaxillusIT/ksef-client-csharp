
using System.ComponentModel.DataAnnotations;
using KSeF.Client.Core.Models.Invoices.Common;

namespace KSeF.Client.Core.Models.Invoices;

public class InvoiceQueryFilters
{
    [Required]
    public SubjectType SubjectType { get; set; }
    [Required]
    public DateRange DateRange { get; set; }
    public string KsefNumber { get; set; }
    public string InvoiceNumber { get; set; }
    public AmountFilter Amount { get; set; }
    public string SellerNip { get; set; }
    public ContextIdentifier BuyerIdentifier { get; set; }
    public List<CurrencyCode> CurrencyCodes { get; set; }
    public InvoicingMode? InvoicingMode { get; set; }
    public bool IsSelfInvoicing { get; set; }
    public FormType FormType { get; set; }
    public ICollection<InvoiceType> InvoiceTypes { get; set; }
    public bool HasAttachment { get; set; }
}

public partial class ContextIdentifier
{
    public ContextIdentifierType Type { get; set; }
    public string Value { get; set; }
}

public enum ContextIdentifierType
{
    None,
    Other,
    Nip,
    VatUe,
}
