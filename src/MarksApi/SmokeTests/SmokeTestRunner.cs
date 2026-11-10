using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MarksApi.Dtos;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace MarksApi.SmokeTests;

public class SmokeTestRunner
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmokeTestRunner> _logger;

    public SmokeTestRunner(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<SmokeTestRunner> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<int> RunAsync(string baseAddress)
    {
        Console.WriteLine();
        Console.WriteLine("================================================================================");
        Console.WriteLine("                  STARTING PRODUCTION STARTUP SMOKE TESTS                       ");
        Console.WriteLine($" Target API Base Address: {baseAddress}");
        Console.WriteLine("================================================================================");

        using var client = new HttpClient { BaseAddress = new Uri(baseAddress) };

        var passedCount = 0;
        var failedCount = 0;

        void ReportResult(bool passed, string method, string path, HttpStatusCode expected, HttpStatusCode actual, string detail = "")
        {
            var statusStr = passed ? "PASS" : "FAIL";
            var color = passed ? ConsoleColor.Green : ConsoleColor.Red;
            var prevColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write($"[{statusStr}] ");
            Console.ForegroundColor = prevColor;
            Console.WriteLine($"{method,-6} {path,-40} | Expected: {(int)expected} {expected,-12} | Actual: {(int)actual} {actual,-12} {detail}");

            if (passed) passedCount++;
            else failedCount++;
        }

        // Configuration credentials
        var teacherUser = _configuration["SmokeTest:TeacherUsername"] ?? "teacher1";
        var teacherPass = _configuration["SmokeTest:TeacherPassword"] ?? "TeacherPassword123!";
        var studentUser = _configuration["SmokeTest:StudentUsername"] ?? "student1";
        var studentPass = _configuration["SmokeTest:StudentPassword"] ?? "StudentPassword123!";

        string teacherToken = "";
        string studentToken = "";

        // Track created test IDs for safe cleanup
        var createdStudentIds = new List<int>();
        var createdCourseIds = new List<int>();
        var createdEnrollmentIds = new List<int>();

        try
        {
            // -------------------------------------------------------------------------
            // 18. HEALTH CHECK
            // -------------------------------------------------------------------------
            {
                var response = await client.GetAsync("/health");
                ReportResult(response.StatusCode == HttpStatusCode.OK, "GET", "/health", HttpStatusCode.OK, response.StatusCode);
            }

            // -------------------------------------------------------------------------
            // 1. AUTHENTICATION & LOGIN
            // -------------------------------------------------------------------------
            // 1.1 Positive Teacher Login
            {
                var body = new { username = teacherUser, password = teacherPass };
                var response = await client.PostAsync("/api/auth/login", JsonContent(body));
                var ok = response.StatusCode == HttpStatusCode.OK;
                ReportResult(ok, "POST", "/api/auth/login (Teacher)", HttpStatusCode.OK, response.StatusCode);
                if (ok)
                {
                    var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                    teacherToken = doc.RootElement.GetProperty("token").GetString() ?? "";
                }
            }

            // 1.2 Positive Student Login
            {
                var body = new { username = studentUser, password = studentPass };
                var response = await client.PostAsync("/api/auth/login", JsonContent(body));
                var ok = response.StatusCode == HttpStatusCode.OK;
                ReportResult(ok, "POST", "/api/auth/login (Student)", HttpStatusCode.OK, response.StatusCode);
                if (ok)
                {
                    var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                    studentToken = doc.RootElement.GetProperty("token").GetString() ?? "";
                }
            }

            // 1.3 Negative Login - Invalid credentials (401)
            {
                var body = new { username = "wrong_user", password = "wrong_password" };
                var response = await client.PostAsync("/api/auth/login", JsonContent(body));
                ReportResult(response.StatusCode == HttpStatusCode.Unauthorized, "POST", "/api/auth/login (Invalid Creds)", HttpStatusCode.Unauthorized, response.StatusCode);
            }

            // 1.4 Negative Login - Missing fields (400)
            {
                var body = new { username = "", password = "" };
                var response = await client.PostAsync("/api/auth/login", JsonContent(body));
                ReportResult(response.StatusCode == HttpStatusCode.BadRequest, "POST", "/api/auth/login (Empty Fields)", HttpStatusCode.BadRequest, response.StatusCode);
            }

            // -------------------------------------------------------------------------
            // 2. STUDENTS ENDPOINTS
            // -------------------------------------------------------------------------
            // 2.1 GET /api/students without token (401)
            {
                var response = await client.GetAsync("/api/students");
                ReportResult(response.StatusCode == HttpStatusCode.Unauthorized, "GET", "/api/students (No Auth)", HttpStatusCode.Unauthorized, response.StatusCode);
            }

            // 2.2 GET /api/students as Student role (403 Forbidden)
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, "/api/students");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
                var response = await client.SendAsync(req);
                ReportResult(response.StatusCode == HttpStatusCode.Forbidden, "GET", "/api/students (Student Role)", HttpStatusCode.Forbidden, response.StatusCode);
            }

            // 2.3 GET /api/students as Teacher (200 OK)
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, "/api/students?page=1&pageSize=10");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
                var response = await client.SendAsync(req);
                ReportResult(response.StatusCode == HttpStatusCode.OK, "GET", "/api/students (Teacher Role)", HttpStatusCode.OK, response.StatusCode);
            }

            // 2.4 POST /api/students - Validation error: empty fields (400)
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, "/api/students")
                {
                    Content = JsonContent(new { registrationNo = "", fullName = "", email = "invalid-email" })
                };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
                var response = await client.SendAsync(req);
                ReportResult(response.StatusCode == HttpStatusCode.BadRequest, "POST", "/api/students (Validation Error)", HttpStatusCode.BadRequest, response.StatusCode);
            }

            // 2.5 POST /api/students - Valid student creation (201 Created + Location header)
            int smokeStudentId = 0;
            {
                var uniqueSuffix = Guid.NewGuid().ToString("N")[..4];
                var body = new
                {
                    registrationNo = $"SMOKE-{uniqueSuffix}",
                    fullName = "SMOKE Test Student",
                    email = $"smoke_{uniqueSuffix}@example.com"
                };
                using var req = new HttpRequestMessage(HttpMethod.Post, "/api/students") { Content = JsonContent(body) };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
                var response = await client.SendAsync(req);
                var is201 = response.StatusCode == HttpStatusCode.Created;
                var hasLocation = response.Headers.Location != null;
                ReportResult(is201 && hasLocation, "POST", "/api/students (Valid Create)", HttpStatusCode.Created, response.StatusCode, hasLocation ? $"Location: {response.Headers.Location}" : "No Location header");
                if (is201)
                {
                    var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                    smokeStudentId = doc.RootElement.GetProperty("id").GetInt32();
                    createdStudentIds.Add(smokeStudentId);
                }
            }

            // 2.6 POST /api/students - Duplicate registration number (409 Conflict)
            if (smokeStudentId > 0)
            {
                var body = new
                {
                    registrationNo = createdStudentIds.Count > 0 ? $"SMOKE-{createdStudentIds[0]}" : "CS-2022-001",
                    fullName = "Duplicate Student",
                    email = $"other_{Guid.NewGuid():N}@example.com"
                };
                // use seeded student reg no to test 409
                using var req = new HttpRequestMessage(HttpMethod.Post, "/api/students")
                {
                    Content = JsonContent(new { registrationNo = "CS-2022-001", fullName = "Duplicate Reg", email = "new_unique@example.com" })
                };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
                var response = await client.SendAsync(req);
                ReportResult(response.StatusCode == HttpStatusCode.Conflict, "POST", "/api/students (Duplicate RegNo)", HttpStatusCode.Conflict, response.StatusCode);
            }

            // 2.7 GET /api/students/{id} - Teacher reading student (200 OK)
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/students/{smokeStudentId}");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
                var response = await client.SendAsync(req);
                ReportResult(response.StatusCode == HttpStatusCode.OK, "GET", $"/api/students/{smokeStudentId} (Teacher)", HttpStatusCode.OK, response.StatusCode);
            }

            // 2.8 GET /api/students/{id} - Student reading own record (200 OK)
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, "/api/students/1");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
                var response = await client.SendAsync(req);
                ReportResult(response.StatusCode == HttpStatusCode.OK, "GET", "/api/students/1 (Student Own)", HttpStatusCode.OK, response.StatusCode);
            }

            // 2.9 GET /api/students/{id} - Student reading another student's record (403 Forbidden)
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, "/api/students/2");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
                var response = await client.SendAsync(req);
                ReportResult(response.StatusCode == HttpStatusCode.Forbidden, "GET", "/api/students/2 (Student Forbidden)", HttpStatusCode.Forbidden, response.StatusCode);
            }

            // 2.10 GET /api/students/{id} - Unknown ID (404 Not Found)
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, "/api/students/999999");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
                var response = await client.SendAsync(req);
                ReportResult(response.StatusCode == HttpStatusCode.NotFound, "GET", "/api/students/999999 (Unknown ID)", HttpStatusCode.NotFound, response.StatusCode);
            }

            // 2.11 PUT /api/students/{id} - Update student (200 OK)
            {
                var body = new
                {
                    registrationNo = $"SMOKE-U{smokeStudentId}",
                    fullName = "SMOKE Updated Student",
                    email = $"smoke_updated_{smokeStudentId}@example.com"
                };
                using var req = new HttpRequestMessage(HttpMethod.Put, $"/api/students/{smokeStudentId}") { Content = JsonContent(body) };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
                var response = await client.SendAsync(req);
                ReportResult(response.StatusCode == HttpStatusCode.OK, "PUT", $"/api/students/{smokeStudentId} (Valid)", HttpStatusCode.OK, response.StatusCode);
            }

            // -------------------------------------------------------------------------
            // 3. COURSES ENDPOINTS
            // -------------------------------------------------------------------------
            // 3.1 GET /api/courses as Student (200 OK)
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, "/api/courses");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
                var response = await client.SendAsync(req);
                ReportResult(response.StatusCode == HttpStatusCode.OK, "GET", "/api/courses (Student Role)", HttpStatusCode.OK, response.StatusCode);
            }

            // 3.2 POST /api/courses - Invalid credit hours (400 Bad Request)
            {
                var body = new { code = "SMK01", title = "Invalid Credits Course", creditHours = 5 };
                using var req = new HttpRequestMessage(HttpMethod.Post, "/api/courses") { Content = JsonContent(body) };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
                var response = await client.SendAsync(req);
                ReportResult(response.StatusCode == HttpStatusCode.BadRequest, "POST", "/api/courses (Invalid Credits 5)", HttpStatusCode.BadRequest, response.StatusCode);
            }

            // 3.3 POST /api/courses - Valid course creation (201 Created + Location header)
            int smokeCourseId = 0;
            {
                var uniqueSuffix = Guid.NewGuid().ToString("N")[..3].ToUpperInvariant();
                var body = new
                {
                    code = $"SMK{uniqueSuffix}",
                    title = "SMOKE Test Course",
                    creditHours = 3
                };
                using var req = new HttpRequestMessage(HttpMethod.Post, "/api/courses") { Content = JsonContent(body) };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
                var response = await client.SendAsync(req);
                var is201 = response.StatusCode == HttpStatusCode.Created;
                var hasLocation = response.Headers.Location != null;
                ReportResult(is201 && hasLocation, "POST", "/api/courses (Valid Create)", HttpStatusCode.Created, response.StatusCode);
                if (is201)
                {
                    var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                    smokeCourseId = doc.RootElement.GetProperty("id").GetInt32();
                    createdCourseIds.Add(smokeCourseId);
                }
            }

            // 3.4 POST /api/courses - Duplicate course code (409 Conflict)
            {
                var body = new { code = "CS401", title = "Duplicate CS401", creditHours = 3 };
                using var req = new HttpRequestMessage(HttpMethod.Post, "/api/courses") { Content = JsonContent(body) };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
                var response = await client.SendAsync(req);
                ReportResult(response.StatusCode == HttpStatusCode.Conflict, "POST", "/api/courses (Duplicate Code)", HttpStatusCode.Conflict, response.StatusCode);
            }

            // 3.5 GET /api/courses/{id} - Valid course (200 OK)
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/courses/{smokeCourseId}");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
                var response = await client.SendAsync(req);
                ReportResult(response.StatusCode == HttpStatusCode.OK, "GET", $"/api/courses/{smokeCourseId}", HttpStatusCode.OK, response.StatusCode);
            }

            // 3.6 PUT /api/courses/{id} - Valid update (200 OK)
            {
                var body = new { code = $"SMK{smokeCourseId}", title = "Updated SMOKE Course", creditHours = 4 };
                using var req = new HttpRequestMessage(HttpMethod.Put, $"/api/courses/{smokeCourseId}") { Content = JsonContent(body) };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
                var response = await client.SendAsync(req);
                ReportResult(response.StatusCode == HttpStatusCode.OK, "PUT", $"/api/courses/{smokeCourseId}", HttpStatusCode.OK, response.StatusCode);
            }

            // -------------------------------------------------------------------------
            // 4. ENROLLMENTS & MARKS
            // -------------------------------------------------------------------------
            // 4.1 POST /api/courses/{courseId}/enrollments - Valid enrollment (201 Created)
            int smokeEnrollmentId = 0;
            {
                var body = new { studentId = smokeStudentId };
                using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/courses/{smokeCourseId}/enrollments") { Content = JsonContent(body) };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
                var response = await client.SendAsync(req);
                var is201 = response.StatusCode == HttpStatusCode.Created;
                ReportResult(is201, "POST", $"/api/courses/{smokeCourseId}/enrollments (Valid)", HttpStatusCode.Created, response.StatusCode);
                if (is201)
                {
                    var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                    smokeEnrollmentId = doc.RootElement.GetProperty("id").GetInt32();
                    createdEnrollmentIds.Add(smokeEnrollmentId);
                }
            }

            // 4.2 POST /api/courses/{courseId}/enrollments - Duplicate enrollment (409 Conflict)
            {
                var body = new { studentId = smokeStudentId };
                using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/courses/{smokeCourseId}/enrollments") { Content = JsonContent(body) };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
                var response = await client.SendAsync(req);
                ReportResult(response.StatusCode == HttpStatusCode.Conflict, "POST", $"/api/courses/{smokeCourseId}/enrollments (Duplicate)", HttpStatusCode.Conflict, response.StatusCode);
            }

            // 4.3 GET /api/courses/{courseId}/enrollments - List enrollments (200 OK)
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/courses/{smokeCourseId}/enrollments");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
                var response = await client.SendAsync(req);
                ReportResult(response.StatusCode == HttpStatusCode.OK, "GET", $"/api/courses/{smokeCourseId}/enrollments", HttpStatusCode.OK, response.StatusCode);
            }

            // 4.4 PUT /api/enrollments/{id}/mark - Invalid mark > 100 (400 Bad Request)
            {
                var body = new { mark = 101 };
                using var req = new HttpRequestMessage(HttpMethod.Put, $"/api/enrollments/{smokeEnrollmentId}/mark") { Content = JsonContent(body) };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
                var response = await client.SendAsync(req);
                ReportResult(response.StatusCode == HttpStatusCode.BadRequest, "PUT", $"/api/enrollments/{smokeEnrollmentId}/mark (Mark 101)", HttpStatusCode.BadRequest, response.StatusCode);
            }

            // 4.5 PUT /api/enrollments/{id}/mark - Invalid mark < 0 (400 Bad Request)
            {
                var body = new { mark = -1 };
                using var req = new HttpRequestMessage(HttpMethod.Put, $"/api/enrollments/{smokeEnrollmentId}/mark") { Content = JsonContent(body) };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
                var response = await client.SendAsync(req);
                ReportResult(response.StatusCode == HttpStatusCode.BadRequest, "PUT", $"/api/enrollments/{smokeEnrollmentId}/mark (Mark -1)", HttpStatusCode.BadRequest, response.StatusCode);
            }

            // 4.6 PUT /api/enrollments/{id}/mark - Valid mark update (200 OK)
            {
                var body = new { mark = 85 };
                using var req = new HttpRequestMessage(HttpMethod.Put, $"/api/enrollments/{smokeEnrollmentId}/mark") { Content = JsonContent(body) };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
                var response = await client.SendAsync(req);
                ReportResult(response.StatusCode == HttpStatusCode.OK, "PUT", $"/api/enrollments/{smokeEnrollmentId}/mark (Valid Mark 85)", HttpStatusCode.OK, response.StatusCode);
            }

            // -------------------------------------------------------------------------
            // 5. REPORTS ENDPOINTS
            // -------------------------------------------------------------------------
            // 5.1 GET /api/courses/{courseId}/stats - Valid stats (200 OK)
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/courses/{smokeCourseId}/stats");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
                var response = await client.SendAsync(req);
                ReportResult(response.StatusCode == HttpStatusCode.OK, "GET", $"/api/courses/{smokeCourseId}/stats", HttpStatusCode.OK, response.StatusCode);
            }

            // 5.2 GET /api/students/{id}/transcript - Student own transcript (200 OK)
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, "/api/students/1/transcript");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
                var response = await client.SendAsync(req);
                var is200 = response.StatusCode == HttpStatusCode.OK;
                ReportResult(is200, "GET", "/api/students/1/transcript (Student Own)", HttpStatusCode.OK, response.StatusCode);
            }

            // 5.3 GET /api/students/{id}/transcript - Student viewing another transcript (403 Forbidden)
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, "/api/students/2/transcript");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
                var response = await client.SendAsync(req);
                ReportResult(response.StatusCode == HttpStatusCode.Forbidden, "GET", "/api/students/2/transcript (Student Forbidden)", HttpStatusCode.Forbidden, response.StatusCode);
            }

            // -------------------------------------------------------------------------
            // 6. DELETION AND CONFLICT RULES
            // -------------------------------------------------------------------------
            // 6.1 DELETE /api/students/{id} with existing enrollments (409 Conflict)
            {
                using var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/students/{smokeStudentId}");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
                var response = await client.SendAsync(req);
                ReportResult(response.StatusCode == HttpStatusCode.Conflict, "DELETE", $"/api/students/{smokeStudentId} (Has Enrollments)", HttpStatusCode.Conflict, response.StatusCode);
            }

            // 6.2 DELETE /api/courses/{id} with existing enrollments (409 Conflict)
            {
                using var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/courses/{smokeCourseId}");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
                var response = await client.SendAsync(req);
                ReportResult(response.StatusCode == HttpStatusCode.Conflict, "DELETE", $"/api/courses/{smokeCourseId} (Has Enrollments)", HttpStatusCode.Conflict, response.StatusCode);
            }

            // 6.3 DELETE /api/enrollments/{id} - Valid deletion (204 NoContent)
            {
                using var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/enrollments/{smokeEnrollmentId}");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
                var response = await client.SendAsync(req);
                ReportResult(response.StatusCode == HttpStatusCode.NoContent, "DELETE", $"/api/enrollments/{smokeEnrollmentId} (Valid)", HttpStatusCode.NoContent, response.StatusCode);
                if (response.StatusCode == HttpStatusCode.NoContent)
                {
                    createdEnrollmentIds.Remove(smokeEnrollmentId);
                }
            }

            // 6.4 DELETE /api/students/{id} - After enrollment deleted (204 NoContent)
            {
                using var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/students/{smokeStudentId}");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
                var response = await client.SendAsync(req);
                ReportResult(response.StatusCode == HttpStatusCode.NoContent, "DELETE", $"/api/students/{smokeStudentId} (Valid)", HttpStatusCode.NoContent, response.StatusCode);
                if (response.StatusCode == HttpStatusCode.NoContent)
                {
                    createdStudentIds.Remove(smokeStudentId);
                }
            }

            // 6.5 DELETE /api/courses/{id} - After enrollment deleted (204 NoContent)
            {
                using var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/courses/{smokeCourseId}");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
                var response = await client.SendAsync(req);
                ReportResult(response.StatusCode == HttpStatusCode.NoContent, "DELETE", $"/api/courses/{smokeCourseId} (Valid)", HttpStatusCode.NoContent, response.StatusCode);
                if (response.StatusCode == HttpStatusCode.NoContent)
                {
                    createdCourseIds.Remove(smokeCourseId);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Smoke test exception: {ex.Message}");
            failedCount++;
        }
        finally
        {
            // SAFE CLEANUP: Clean up any remaining test records created by smoke test
            await SafeCleanupAsync(client, teacherToken, createdEnrollmentIds, createdStudentIds, createdCourseIds);
        }

        Console.WriteLine("================================================================================");
        Console.WriteLine($" SMOKE TEST SUMMARY: Total: {passedCount + failedCount} | Passed: {passedCount} | Failed: {failedCount}");
        Console.WriteLine("================================================================================");
        Console.WriteLine();

        return failedCount == 0 ? 0 : 1;
    }

    private static async Task SafeCleanupAsync(
        HttpClient client,
        string teacherToken,
        List<int> enrollmentIds,
        List<int> studentIds,
        List<int> courseIds)
    {
        if (string.IsNullOrEmpty(teacherToken)) return;

        foreach (var eid in enrollmentIds)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/enrollments/{eid}");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
                await client.SendAsync(req);
            }
            catch { }
        }

        foreach (var sid in studentIds)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/students/{sid}");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
                await client.SendAsync(req);
            }
            catch { }
        }

        foreach (var cid in courseIds)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/courses/{cid}");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);
                await client.SendAsync(req);
            }
            catch { }
        }
    }

    private static StringContent JsonContent(object obj)
    {
        return new StringContent(JsonSerializer.Serialize(obj), Encoding.UTF8, "application/json");
    }
}
