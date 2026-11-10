namespace MarksApi.Entities;

public static class Roles
{
    public const string Teacher = "Teacher";
    public const string Student = "Student";
}

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int? StudentId { get; set; }

    public Student? Student { get; set; }
}
