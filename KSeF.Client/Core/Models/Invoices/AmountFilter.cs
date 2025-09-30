
using System.ComponentModel.DataAnnotations;

namespace KSeF.Client.Core.Models.Invoices;

public class AmountFilter
{
    [Required]
    public AmountType Type { get; set; }
    public decimal From { get; set; }
    public decimal To { get; set; }
}
