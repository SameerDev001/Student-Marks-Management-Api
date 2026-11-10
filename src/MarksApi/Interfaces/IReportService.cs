using MarksApi.Dtos;

namespace MarksApi.Interfaces;

public interface IReportService
{
    Task<CourseStatsResponse> GetCourseStatsAsync(int courseId, CancellationToken ct = default);
    Task<TranscriptResponse> GetStudentTranscriptAsync(int studentId, int? currentStudentId, string currentRole, CancellationToken ct = default);
}
