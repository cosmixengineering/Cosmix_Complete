namespace NOIR.Application.Features.Customers.Commands.BulkActivateCustomers;

/// <summary>
/// Wolverine handler for bulk activating customers.
/// </summary>
public class BulkActivateCustomersCommandHandler
{
    private readonly IRepository<Domain.Entities.Customer.Customer, Guid> _customerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public BulkActivateCustomersCommandHandler(
        IRepository<Domain.Entities.Customer.Customer, Guid> customerRepository,
        IUnitOfWork unitOfWork)
    {
        _customerRepository = customerRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<BulkOperationResultDto>> Handle(
        BulkActivateCustomersCommand command,
        CancellationToken cancellationToken)
    {
        var successCount = 0;
        var errors = new List<BulkOperationErrorDto>();

        var spec = new CustomersByIdsForUpdateSpec(command.CustomerIds);
        var customers = await _customerRepository.ListAsync(spec, cancellationToken);

        foreach (var customerId in command.CustomerIds)
        {
            var customer = customers.FirstOrDefault(c => c.Id == customerId);

            if (customer is null)
            {
                errors.Add(new BulkOperationErrorDto(customerId, null, "Customer not found"));
                continue;
            }

            if (customer.IsActive)
            {
                errors.Add(new BulkOperationErrorDto(customerId, customer.Email, "Customer is already active"));
                continue;
            }

            try
            {
                customer.Activate();
                successCount++;
            }
            catch (Exception ex)
            {
                errors.Add(new BulkOperationErrorDto(customerId, customer.Email, ex.Message));
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new BulkOperationResultDto(successCount, errors.Count, errors));
    }
}
