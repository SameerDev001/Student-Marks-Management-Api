using MarksApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarksApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Student> Students => Set<Student>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Student configuration
        modelBuilder.Entity<Student>(entity =>
        {
            entity.ToTable("students");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.RegistrationNo).HasMaxLength(20).IsRequired();
            entity.Property(e => e.FullName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(150).IsRequired();
            entity.Property(e => e.CreatedAtUtc).HasColumnType("timestamptz").IsRequired();

            entity.HasIndex(e => e.RegistrationNo).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // Course configuration
        modelBuilder.Entity<Course>(entity =>
        {
            entity.ToTable("courses", t =>
            {
                t.HasCheckConstraint("CK_courses_credit_hours", "credit_hours >= 1 AND credit_hours <= 4");
            });
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Code).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(150).IsRequired();
            entity.Property(e => e.CreditHours).IsRequired();
            entity.Property(e => e.CreatedAtUtc).HasColumnType("timestamptz").IsRequired();

            entity.HasIndex(e => e.Code).IsUnique();
        });

        // Enrollment configuration
        modelBuilder.Entity<Enrollment>(entity =>
        {
            entity.ToTable("enrollments", t =>
            {
                t.HasCheckConstraint("CK_enrollments_mark", "mark IS NULL OR (mark >= 0 AND mark <= 100)");
            });
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.StudentId).IsRequired();
            entity.Property(e => e.CourseId).IsRequired();
            entity.Property(e => e.Mark);
            entity.Property(e => e.EnrolledAtUtc).HasColumnType("timestamptz").IsRequired();

            entity.HasIndex(e => new { e.StudentId, e.CourseId }).IsUnique();

            // Delete restricted
            entity.HasOne(e => e.Student)
                .WithMany(s => s.Enrollments)
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Course)
                .WithMany(c => c.Enrollments)
                .HasForeignKey(e => e.CourseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Username).HasMaxLength(50).IsRequired();
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.Role).HasMaxLength(20).IsRequired();
            entity.Property(e => e.StudentId);

            entity.HasIndex(e => e.Username).IsUnique();

            entity.HasOne(e => e.Student)
                .WithOne(s => s.User)
                .HasForeignKey<User>(e => e.StudentId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
