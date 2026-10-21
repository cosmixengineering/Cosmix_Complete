namespace NOIR.Domain.Entities.Invoicing;

public enum InvoiceStatus
{
    Draft = 1,
    Issued = 2,
    PartiallyPaid = 3,
    Paid = 4,
    Overdue = 5,
    Void = 6
}

public enum InvoicePaymentMethod
{
    BankTransfer = 1,
    Cash = 2,
    Cheque = 3,
    CreditCard = 4,
    Online = 5
}
