namespace NOIR.Domain.UnitTests.Entities.Invoicing;

public class InvoicePaymentMethodTests
{
    [Theory]
    [InlineData("BankTransfer", 1)]
    [InlineData("Cash", 2)]
    [InlineData("Cheque", 3)]
    [InlineData("CreditCard", 4)]
    [InlineData("Online", 5)]
    public void InvoiceMethods_ShouldPreserveValuesAndWireNames(string name, int value)
    {
        var method = Enum.Parse<NOIR.Domain.Entities.Invoicing.InvoicePaymentMethod>(name);
        ((int)method).ShouldBe(value);

        var options = new System.Text.Json.JsonSerializerOptions();
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        System.Text.Json.JsonSerializer.Serialize(method, options).ShouldBe($"\"{name}\"");
    }

    [Fact]
    public void PaymentRecord_ShouldUseInvoiceMethodWithoutChangingCheckoutMethods()
    {
        var payment = NOIR.Domain.Entities.Invoicing.PaymentRecord.Create(
            "PAY-001", Guid.NewGuid(), Guid.NewGuid(), 100m,
            NOIR.Domain.Entities.Invoicing.InvoicePaymentMethod.Cash, null, null, "tenant-1");

        payment.Method.ShouldBe(NOIR.Domain.Entities.Invoicing.InvoicePaymentMethod.Cash);
        ((int)PaymentMethod.BankTransfer).ShouldBe(2);
        ((int)PaymentMethod.COD).ShouldBe(6);
    }
}
