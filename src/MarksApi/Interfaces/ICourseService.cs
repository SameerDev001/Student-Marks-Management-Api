using MarksApi.Dtos;

namespace MarksApi.Interfaces;

public interface ICourseService
{
    Task<PagedResult<CourseResponse>> GetAllAsync(PaginationParams pagination, CancellationToken ct = default);
    Task<CourseResponse> GetByIdAsync(int id, CancellationToken ct = default);
    Task<CourseResponse> CreateAsync(CreateCourseRequest request, CancellationToken ct = default);
    Task<CourseResponse> UpdateAsync(int id, UpdateCourseRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
