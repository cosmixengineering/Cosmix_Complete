namespace NOIR.Domain.Entities.Procurement;

/// <summary>
/// Supplier / Vendor Master entity.
/// Manages registered procurement suppliers, payment terms, and contact details.
/// </summary>
public class Supplier : TenantAggregateRoot<Guid>
{
    private Supplier() : base() { }

    private Supplier(Guid id, string? tenantId) : base(id, tenantId) { }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? ContactPerson { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? TaxId { get; private set; }
    public string? Address { get; private set; }
    public string? PaymentTerms { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string? Notes { get; private set; }

    public static Supplier Create(
        string code,
        string name,
        string? contactPerson,
        string? email,
        string? phone,
        string? taxId,
        string? address,
        string? paymentTerms,
        string? notes,
        string? tenantId = null)
    {
        return new Supplier(Guid.NewGuid(), tenantId)
        {
            Code = code,
            Name = name,
            ContactPerson = contactPerson,
            Email = email,
            Phone = phone,
            TaxId = taxId,
            Address = address,
            PaymentTerms = paymentTerms,
            Notes = notes,
            IsActive = true
        };
    }

    public void Update(
        string name,
        string? contactPerson,
        string? email,
        string? phone,
        string? taxId,
        string? address,
        string? paymentTerms,
        string? notes)
    {
        Name = name;
        ContactPerson = contactPerson;
        Email = email;
        Phone = phone;
        TaxId = taxId;
        Address = address;
        PaymentTerms = paymentTerms;
        Notes = notes;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }
}
