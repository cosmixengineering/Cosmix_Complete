namespace NOIR.Application.Features.Search.Queries;

/// <summary>
/// Wolverine handler for global search across multiple entity types.
/// Queries are run sequentially because DbContext is not thread-safe.
/// </summary>
public class GlobalSearchQueryHandler
{
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IRepository<Order, Guid> _orderRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<Post, Guid> _postRepository;
    private readonly IUserIdentityService _userIdentityService;
    private readonly IFeatureChecker _featureChecker;

    public GlobalSearchQueryHandler(
        IRepository<Product, Guid> productRepository,
        IRepository<Order, Guid> orderRepository,
        IRepository<Customer, Guid> customerRepository,
        IRepository<Post, Guid> postRepository,
        IUserIdentityService userIdentityService,
        IFeatureChecker featureChecker)
    {
        _productRepository = productRepository;
        _orderRepository = orderRepository;
        _customerRepository = customerRepository;
        _postRepository = postRepository;
        _userIdentityService = userIdentityService;
        _featureChecker = featureChecker;
    }

    public async Task<Result<GlobalSearchResponseDto>> Handle(
        GlobalSearchQuery query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.Search) || query.Search.Length < 2)
        {
            return Result.Success(new GlobalSearchResponseDto([], [], [], [], [], 0));
        }

        var search = query.Search.Trim();
        var max = query.MaxPerCategory;

        // Products (sequential - DbContext is NOT thread-safe)
        var productResults = new List<GlobalSearchResultDto>();
        if (await _featureChecker.IsEnabledAsync(ModuleNames.Ecommerce.Products, cancellationToken))
        {
            var products = await _productRepository.ListAsync(
                new ProductsSpec(search: search, take: max), cancellationToken);
            productResults = products.Select(p => new GlobalSearchResultDto(
                "product",
                p.Id.ToString(),
                p.Name,
                p.Sku,
                $"/portal/ecommerce/products/{p.Id}",
                p.PrimaryImage?.Url)).ToList();
        }

        // Orders
        var orderResults = new List<GlobalSearchResultDto>();
        if (await _featureChecker.IsEnabledAsync(ModuleNames.Ecommerce.Orders, cancellationToken))
        {
            var orders = await _orderRepository.ListAsync(
                new OrderSearchSpec(search, max), cancellationToken);
            orderResults = orders.Select(o => new GlobalSearchResultDto(
                "order",
                o.Id.ToString(),
                o.OrderNumber,
                o.CustomerEmail,
                $"/portal/ecommerce/orders/{o.Id}",
                null)).ToList();
        }

        // Customers
        var customerResults = new List<GlobalSearchResultDto>();
        if (await _featureChecker.IsEnabledAsync(ModuleNames.Ecommerce.Customers, cancellationToken))
        {
            var customers = await _customerRepository.ListAsync(
                new CustomersFilterSpec(take: max, search: search), cancellationToken);
            customerResults = customers.Select(c => new GlobalSearchResultDto(
                "customer",
                c.Id.ToString(),
                $"{c.FirstName} {c.LastName}",
                c.Email,
                $"/portal/ecommerce/customers/{c.Id}",
                null)).ToList();
        }

        // Blog Posts
        var blogResults = new List<GlobalSearchResultDto>();
        if (await _featureChecker.IsEnabledAsync(ModuleNames.Content.Blog, cancellationToken))
        {
            var posts = await _postRepository.ListAsync(
                new PostsSpec(search: search, take: max), cancellationToken);
            blogResults = posts.Select(p => new GlobalSearchResultDto(
                "blogPost",
                p.Id.ToString(),
                p.Title,
                p.Category?.Name,
                $"/portal/blog/posts/{p.Id}",
                p.FeaturedImage?.DefaultUrl)).ToList();
        }

        // Users (always search - Core)
        var (users, _) = await _userIdentityService.GetUsersPaginatedAsync(
            null, search, 1, max, ct: cancellationToken);
        var userResults = users.Select(u => new GlobalSearchResultDto(
            "user",
            u.Id,
            u.FullName,
            u.Email,
            "/portal/admin/users",
            u.AvatarUrl)).ToList();

        var totalCount = productResults.Count + orderResults.Count +
                         customerResults.Count + blogResults.Count + userResults.Count;

        return Result.Success(new GlobalSearchResponseDto(
            productResults,
            orderResults,
            customerResults,
            blogResults,
            userResults,
            totalCount));
    }
}
