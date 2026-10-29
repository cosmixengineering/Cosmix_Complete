namespace Cosmix.Application.HR.PayrollAccounting;

/// <summary>Structure extracted from owner-supplied HBL portal sheets. These are unapproved definitions, never live profiles.</summary>
public static class PayrollBankTemplates
{
    public const string HblIbftSha256 = "56c2380c543b8933ca3b9d8b7d06d8ba7b6c3ba21e59fb9092cb609956d92c04";
    public const string HblInternalSha256 = "a810b52e3a4776d4f0a973e01138f3be31fe736c611a4334c4f29cef5f67978b";
    public static IReadOnlyList<PayrollBankTemplateDefinition> Definitions() =>
    [
        new("HBL_IBFT", "HBL IBFT supplied template", HblIbftSha256,
            new(",", true, false, "yyyy-MM-dd",
            [new("Beneficiary  Name", "BeneficiaryName"), new("Beneficiary Account Number", "Iban"), new("Transaction Amount", "Amount"),
             new("Customer Reference Number", "Reference"), new("Bank Code", "BankCode"), new("Beneficary Contact Number", "ContactNumber", Required: false),
             new("Beneficiary Email Address", "Email", Required: false), new("Purpose of Payment ", "Literal", "021"), new("Reference # 3", "Blank"),
             new("Invoice Number ", "Blank"), new("Account Title ", "AccountTitle")], "Xlsx", "Sheet1", HblIbftSha256, [])),
        new("HBL_INTERNAL", "HBL to HBL supplied template", HblInternalSha256,
            new(",", true, false, "yyyy-MM-dd",
            [new("Beneficiary  Name", "BeneficiaryName"), new("Beneficiary Account Number", "AccountNumber"), new("Transaction Amount", "Amount"),
             new("Customer Reference Number", "Reference"), new("Contact Number", "ContactNumber", Required: false), new("Beneficiary Email Address", "Email", Required: false),
             new("Purpose of Payment ", "Literal", "021"), new("Invoice Number ", "Blank"), new("Account Title ", "AccountTitle")],
             "Xlsx", "Sheet1", HblInternalSha256, BeneficiaryBankNames: []))
    ];
}
