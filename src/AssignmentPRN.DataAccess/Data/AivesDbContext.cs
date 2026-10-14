using AssignmentPRN.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace AssignmentPRN.DataAccess;

public class AivesDbContext(DbContextOptions<AivesDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Course> Courses => Set<Course>();

    public DbSet<AcademicClass> AcademicClasses => Set<AcademicClass>();

    public DbSet<ClassStudent> ClassStudents => Set<ClassStudent>();

    public DbSet<ExamSession> ExamSessions => Set<ExamSession>();

    public DbSet<ExamCandidate> ExamCandidates => Set<ExamCandidate>();

    public DbSet<Document> Documents => Set<Document>();

    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();

    public DbSet<GeneratedQuestion> GeneratedQuestions => Set<GeneratedQuestion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(role => role.RoleId);

            entity.Property(role => role.RoleId)
                .HasColumnName("role_id")
                .ValueGeneratedOnAdd();
            entity.Property(role => role.RoleName)
                .HasColumnName("role_name")
                .HasMaxLength(50)
                .IsRequired();
            entity.Property(role => role.Description)
                .HasColumnName("description")
                .HasMaxLength(500);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(user => user.UserId);

            entity.Property(user => user.UserId)
                .HasColumnName("user_id")
                .ValueGeneratedOnAdd();
            entity.Property(user => user.RoleId)
                .HasColumnName("role_id");
            entity.Property(user => user.FullName)
                .HasColumnName("full_name")
                .HasMaxLength(150)
                .IsRequired();
            entity.Property(user => user.Email)
                .HasColumnName("email")
                .HasMaxLength(255)
                .IsRequired();
            entity.Property(user => user.PasswordHash)
                .HasColumnName("password_hash")
                .HasMaxLength(500)
                .IsRequired();
            entity.Property(user => user.Status)
                .HasColumnName("status")
                .IsRequired();
            entity.Property(user => user.CreatedAt)
                .HasColumnName("created_at");
            entity.Property(user => user.UpdatedAt)
                .HasColumnName("updated_at");

            entity.HasOne(user => user.Role)
                .WithMany(role => role.Users)
                .HasForeignKey(user => user.RoleId);
        });

        ConfigureExamSchedule(modelBuilder);
    }

    private static void ConfigureExamSchedule(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Course>(entity =>
        {
            entity.ToTable("courses");
            entity.HasKey(course => course.CourseId);

            entity.Property(course => course.CourseId)
                .HasColumnName("course_id")
                .ValueGeneratedOnAdd();
            entity.Property(course => course.CourseCode)
                .HasColumnName("course_code")
                .HasMaxLength(50)
                .IsRequired();
            entity.Property(course => course.CourseName)
                .HasColumnName("course_name")
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(course => course.Description)
                .HasColumnName("description")
                .HasMaxLength(1000);
            entity.Property(course => course.LecturerId)
                .HasColumnName("lecturer_id");
            entity.Property(course => course.IsActive)
                .HasColumnName("is_active");
            entity.Property(course => course.CreatedAt)
                .HasColumnName("created_at");
            entity.Property(course => course.UpdatedAt)
                .HasColumnName("updated_at");

            entity.HasIndex(course => course.CourseCode).IsUnique();

            entity.HasOne(course => course.Lecturer)
                .WithMany()
                .HasForeignKey(course => course.LecturerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AcademicClass>(entity =>
        {
            entity.ToTable("academic_classes");
            entity.HasKey(item => item.ClassId);

            entity.Property(item => item.ClassId)
                .HasColumnName("class_id")
                .ValueGeneratedOnAdd();
            entity.Property(item => item.ClassCode)
                .HasColumnName("class_code")
                .HasMaxLength(50)
                .IsRequired();
            entity.Property(item => item.ClassName)
                .HasColumnName("class_name")
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(item => item.CourseId)
                .HasColumnName("course_id");
            entity.Property(item => item.LecturerId)
                .HasColumnName("lecturer_id");
            entity.Property(item => item.IsActive)
                .HasColumnName("is_active");
            entity.Property(item => item.CreatedAt)
                .HasColumnName("created_at");
            entity.Property(item => item.UpdatedAt)
                .HasColumnName("updated_at");

            entity.HasIndex(item => item.ClassCode).IsUnique();
            entity.HasOne(item => item.Course)
                .WithMany(course => course.Classes)
                .HasForeignKey(item => item.CourseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Lecturer)
                .WithMany()
                .HasForeignKey(item => item.LecturerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ClassStudent>(entity =>
        {
            entity.ToTable("class_students");
            entity.HasKey(item => new { item.ClassId, item.StudentId });

            entity.Property(item => item.ClassId)
                .HasColumnName("class_id");
            entity.Property(item => item.StudentId)
                .HasColumnName("student_id");
            entity.Property(item => item.JoinedAt)
                .HasColumnName("joined_at");

            entity.HasOne(item => item.Class)
                .WithMany(item => item.Students)
                .HasForeignKey(item => item.ClassId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Student)
                .WithMany()
                .HasForeignKey(item => item.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ExamSession>(entity =>
        {
            entity.ToTable("exam_sessions");
            entity.HasKey(session => session.ExamId);

            entity.Property(session => session.ExamId)
                .HasColumnName("exam_id")
                .ValueGeneratedOnAdd();
            entity.Property(session => session.CourseId)
                .HasColumnName("course_id");
            entity.Property(session => session.LecturerId)
                .HasColumnName("lecturer_id");
            entity.Property(session => session.ExamName)
                .HasColumnName("exam_name")
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(session => session.Description)
                .HasColumnName("description")
                .HasMaxLength(1000);
            entity.Property(session => session.StartTime)
                .HasColumnName("start_time")
                .IsRequired();
            entity.Property(session => session.EndTime)
                .HasColumnName("end_time")
                .IsRequired();
            entity.Property(session => session.TimePerStudent)
                .HasColumnName("time_per_student");
            entity.Property(session => session.MainQuestionCount)
                .HasColumnName("main_question_count");
            entity.Property(session => session.MaxFollowUpCount)
                .HasColumnName("max_follow_up_count");
            // The column is a MySQL ENUM, so the value is stored as its name.
            entity.Property(session => session.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .IsRequired();
            entity.Property(session => session.CreatedAt)
                .HasColumnName("created_at");
            entity.Property(session => session.UpdatedAt)
                .HasColumnName("updated_at");

            entity.HasOne(session => session.Course)
                .WithMany(course => course.ExamSessions)
                .HasForeignKey(session => session.CourseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(session => session.Lecturer)
                .WithMany()
                .HasForeignKey(session => session.LecturerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ExamCandidate>(entity =>
        {
            entity.ToTable("exam_candidates");
            entity.HasKey(candidate => candidate.CandidateId);

            entity.Property(candidate => candidate.CandidateId)
                .HasColumnName("candidate_id")
                .ValueGeneratedOnAdd();
            entity.Property(candidate => candidate.ExamId)
                .HasColumnName("exam_id");
            entity.Property(candidate => candidate.StudentId)
                .HasColumnName("student_id");
            entity.Property(candidate => candidate.ScheduledTime)
                .HasColumnName("scheduled_time");
            entity.Property(candidate => candidate.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .IsRequired();
            entity.Property(candidate => candidate.StartedAt)
                .HasColumnName("started_at");
            entity.Property(candidate => candidate.FinishedAt)
                .HasColumnName("finished_at");

            entity.HasOne(candidate => candidate.Session)
                .WithMany(session => session.Candidates)
                .HasForeignKey(candidate => candidate.ExamId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(candidate => candidate.Student)
                .WithMany()
                .HasForeignKey(candidate => candidate.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        ConfigureDocuments(modelBuilder);
    }

    private static void ConfigureDocuments(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Document>(entity =>
        {
            entity.ToTable("documents");
            entity.HasKey(d => d.DocumentId);

            entity.Property(d => d.DocumentId)
                .HasColumnName("document_id")
                .ValueGeneratedOnAdd();
            entity.Property(d => d.CourseId)
                .HasColumnName("course_id");
            entity.Property(d => d.UploadedById)
                .HasColumnName("uploaded_by_id");
            entity.Property(d => d.FileName)
                .HasColumnName("file_name")
                .HasMaxLength(255)
                .IsRequired();
            entity.Property(d => d.ContentType)
                .HasColumnName("content_type")
                .HasMaxLength(100)
                .IsRequired();
            entity.Property(d => d.FileSize)
                .HasColumnName("file_size");
            entity.Property(d => d.StoragePath)
                .HasColumnName("storage_path")
                .HasMaxLength(500)
                .IsRequired();
            entity.Property(d => d.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .IsRequired();
            entity.Property(d => d.ErrorMessage)
                .HasColumnName("error_message")
                .HasMaxLength(1000);
            entity.Property(d => d.CreatedAt)
                .HasColumnName("created_at");
            entity.Property(d => d.ProcessedAt)
                .HasColumnName("processed_at");

            entity.HasOne(d => d.Course)
                .WithMany()
                .HasForeignKey(d => d.CourseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(d => d.UploadedBy)
                .WithMany()
                .HasForeignKey(d => d.UploadedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(d => d.CourseId);
            entity.HasIndex(d => d.UploadedById);
            entity.HasIndex(d => d.Status);
        });

        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.ToTable("document_chunks");
            entity.HasKey(c => c.ChunkId);

            entity.Property(c => c.ChunkId)
                .HasColumnName("chunk_id")
                .ValueGeneratedOnAdd();
            entity.Property(c => c.DocumentId)
                .HasColumnName("document_id");
            entity.Property(c => c.ChunkIndex)
                .HasColumnName("chunk_index");
            entity.Property(c => c.Content)
                .HasColumnName("content")
                .IsRequired();
            entity.Property(c => c.TokenCount)
                .HasColumnName("token_count");
            entity.Property(c => c.EmbeddingJson)
                .HasColumnName("embedding_json")
                .HasColumnType("json")
                .IsRequired();

            entity.HasOne(c => c.Document)
                .WithMany(d => d.Chunks)
                .HasForeignKey(c => c.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(c => c.DocumentId);
        });

        modelBuilder.Entity<GeneratedQuestion>(entity =>
        {
            entity.ToTable("generated_questions");
            entity.HasKey(q => q.QuestionId);

            entity.Property(q => q.QuestionId)
                .HasColumnName("question_id")
                .ValueGeneratedOnAdd();
            entity.Property(q => q.DocumentId)
                .HasColumnName("document_id");
            entity.Property(q => q.CourseId)
                .HasColumnName("course_id");
            entity.Property(q => q.QuestionText)
                .HasColumnName("question_text")
                .IsRequired();
            entity.Property(q => q.AnswerKey)
                .HasColumnName("answer_key")
                .HasMaxLength(2000);
            entity.Property(q => q.Explanation)
                .HasColumnName("explanation")
                .HasMaxLength(2000);
            entity.Property(q => q.BloomLevel)
                .HasColumnName("bloom_level")
                .HasConversion<int>()
                .IsRequired();
            entity.Property(q => q.Topic)
                .HasColumnName("topic")
                .HasMaxLength(200);
            entity.Property(q => q.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .IsRequired();
            entity.Property(q => q.ReviewedById)
                .HasColumnName("reviewed_by_id");
            entity.Property(q => q.ReviewedAt)
                .HasColumnName("reviewed_at");
            entity.Property(q => q.ReviewNotes)
                .HasColumnName("review_notes")
                .HasMaxLength(1000);
            entity.Property(q => q.CreatedAt)
                .HasColumnName("created_at");

            entity.HasOne(q => q.Document)
                .WithMany(d => d.GeneratedQuestions)
                .HasForeignKey(q => q.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(q => q.Course)
                .WithMany()
                .HasForeignKey(q => q.CourseId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(q => q.ReviewedBy)
                .WithMany()
                .HasForeignKey(q => q.ReviewedById)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(q => q.DocumentId);
            entity.HasIndex(q => q.Status);
            entity.HasIndex(q => q.CourseId);
        });
    }
}
