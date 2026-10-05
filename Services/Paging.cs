namespace Indamin.Performance.Services;

public static class Paging
{
    public static int NormalizePageSize(int pageSize) =>
        pageSize is 25 or 50 or 75 or 100 ? pageSize : 25;

    public static int TotalPages(int totalCount, int pageSize) =>
        Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));

    public static int NormalizePage(int page, int totalPages) =>
        Math.Clamp(page, 1, Math.Max(1, totalPages));
}