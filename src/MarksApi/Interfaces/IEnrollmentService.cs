using MarksApi.Dtos;

namespace MarksApi.Interfaces;

public interface IEnrollmentService
{
    Task<PagedResult<EnrollmentResponse>> GetByCourseIdAsync(int courseId, PaginationParams pagination, CancellationToken ct = default);
    Task<EnrollmentResponse> EnrollStudentAsync(int courseId, CreateEnrollmentRequest request, CancellationToken ct = default);
    Task<EnrollmentResponse> UpdateMarkAsync(int id, UpdateMarkRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
