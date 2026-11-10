using MarksApi.Dtos;

namespace MarksApi.Interfaces;

public interface IStudentService
{
    Task<PagedResult<StudentResponse>> GetAllAsync(PaginationParams pagination, CancellationToken ct = default);
    Task<StudentResponse> GetByIdAsync(int id, int? currentStudentId, string currentRole, CancellationToken ct = default);
    Task<StudentResponse> CreateAsync(CreateStudentRequest request, CancellationToken ct = default);
    Task<StudentResponse> UpdateAsync(int id, UpdateStudentRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
