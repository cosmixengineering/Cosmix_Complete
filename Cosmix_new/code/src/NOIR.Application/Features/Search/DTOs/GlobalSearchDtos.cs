namespace NOIR.Application.Features.Search.DTOs;

public sealed record GlobalSearchResultDto(
    string Type,
    string Id,
    string Title,
    string? Subtitle,
    string Url,
    string? ImageUrl);

public sealed record GlobalSearchResponseDto(
    List<GlobalSearchResultDto> Products,
    List<GlobalSearchResultDto> Orders,
    List<GlobalSearchResultDto> Customers,
    List<GlobalSearchResultDto> BlogPosts,
    List<GlobalSearchResultDto> Users,
    int TotalCount);
