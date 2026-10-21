namespace NOIR.Application.Features.Customers.Commands.BulkImportCustomers;

/// <summary>
/// Wolverine handler for bulk importing customers.
/// Validates email uniqueness against existing records and within the batch.
/// </summary>
public class BulkImportCustomersCommandHandler
{
    private readonly IRepository<Domain.Entities.Customer.Customer, Guid> _customerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<BulkImportCustomersCommandHandler> _logger;

    public BulkImportCustomersCommandHandler(
        IRepository<Domain.Entities.Customer.Customer, Guid> customerRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        ILogger<BulkImportCustomersCommandHandler> logger)
    {
        _customerRepository = customerRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<BulkImportCustomersResultDto>> Handle(
        BulkImportCustomersCommand command,
        CancellationToken cancellationToken)
    {
        var successCount = 0;
        var errors = new List<CustomerImportErrorDto>();

        // Pre-load existing customer emails for dedup (single query)
        var emailsToCheck = command.Customers.Select(c => c.Email).ToList();
        var emailCheckSpec = new CustomersEmailCheckSpec(emailsToCheck);
        var existingCustomers = await _customerRepository.ListAsync(emailCheckSpec, cancellationToken);
        var existingEmails = existingCustomers
            .Select(c => c.Email)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Track emails used within this batch to catch duplicates
        var batchEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tenantId = _currentUser.TenantId;

        for (var i = 0; i < command.Customers.Count; i++)
        {
            var dto = command.Customers[i];
            var rowNumber = i + 2; // 1-indexed + header row

            try
            {
                // Validate email
                if (string.IsNullOrWhiteSpace(dto.Email))
                {
                    errors.Add(new CustomerImportErrorDto(rowNumber, "Email is required"));
                    continue;
                }

                // Check against existing customers
                if (existingEmails.Contains(dto.Email))
                {
                    errors.Add(new CustomerImportErrorDto(rowNumber, $"Customer with email '{dto.Email}' already exists"));
                    continue;
                }

                // Check within batch
                if (!batchEmails.Add(dto.Email))
                {
                    errors.Add(new CustomerImportErrorDto(rowNumber, $"Duplicate email '{dto.Email}' in import batch"));
                    continue;
                }

                var customer = Domain.Entities.Customer.Customer.Create(
                    null,
                    dto.Email,
                    dto.FirstName,
                    dto.LastName,
                    dto.Phone,
                    tenantId);

                // Add tags if provided (comma-separated)
                if (!string.IsNullOrEmpty(dto.Tags))
                {
                    foreach (var tag in dto.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        customer.AddTag(tag);
                    }
                }

                await _customerRepository.AddAsync(customer, cancellationToken);
                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error importing customer at row {Row}: {Email}",
                    rowNumber, dto.Email);
                errors.Add(new CustomerImportErrorDto(rowNumber, "Failed to import customer due to unexpected error"));
            }
        }

        // Save all at once
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Completed bulk customer import: {SuccessCount} imported, {ErrorCount} errors",
            successCount, errors.Count);

        return Result.Success(new BulkImportCustomersResultDto(
            successCount,
            errors.Count,
            errors));
    }
}
