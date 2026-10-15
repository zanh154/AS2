using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;

namespace AssignmentPRN.DataAccess.Data;

public class AivesDbContext(DbContextOptions<AivesDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Course> Courses => Set<Course>();

    public DbSet<AcademicClass> AcademicClasses => Set<AcademicClass>();

    public DbSet<ClassStudent> ClassStudents => Set<ClassStudent>();

    public DbSet<ExamSession> ExamSessions => Set<ExamSession>();

    public DbSet<ExamCandidate> ExamCandidates => Set<ExamCandidate>();

    public DbSet<CourseMaterial> CourseMaterials => Set<CourseMaterial>();

    public DbSet<Question> Questions => Set<Question>();

    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();

    public DbSet<ExamQuestion> ExamQuestions => Set<ExamQuestion>();

    public DbSet<Answer> Answers => Set<Answer>();

    public DbSet<Rubric> Rubrics => Set<Rubric>();

    public DbSet<RubricCriteria> RubricCriteria => Set<RubricCriteria>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<GeneratedQuestion> GeneratedQuestions => Set<GeneratedQuestion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExamLog>(entity => {
            entity.ToTable("exam_logs");
            entity.HasKey(x => x.LogId);
            entity.Property(x => x.LogId).HasColumnName("log_id");
            entity.Property(x => x.ExamId).HasColumnName("exam_id");
            entity.Property(x => x.CandidateId).HasColumnName("candidate_id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.Action).HasColumnName("action").HasMaxLength(100).IsRequired();
            entity.Property(x => x.Description).HasColumnName("description").HasMaxLength(2000);
            entity.Property(x => x.Timestamp).HasColumnName("timestamp");
        });
        ConfigureDocuments(modelBuilder);
        modelBuilder.Entity<ExamVivaSettings>(entity => {
            entity.ToTable("exam_viva_settings");
            entity.HasKey(x => x.ExamId);
            entity.Property(x => x.ExamId).HasColumnName("exam_id").ValueGeneratedNever();
            entity.Property(x => x.SecondsPerQuestion).HasColumnName("seconds_per_question");
            entity.Property(x => x.MaxFollowUpsPerQuestion).HasColumnName("max_follow_ups_per_question");
            entity.HasOne<ExamSession>().WithOne().HasForeignKey<ExamVivaSettings>(x => x.ExamId).OnDelete(DeleteBehavior.Cascade);
        });
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
        ConfigureQuestionBank(modelBuilder);
        ConfigureRubrics(modelBuilder);

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");

            entity.HasKey(e => e.NotificationId);

            entity.Property(e => e.NotificationId)
                .HasColumnName("notification_id")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.UserId)
                .HasColumnName("user_id");

            entity.Property(e => e.CourseId)
                .HasColumnName("course_id");

            entity.Property(e => e.ExamId)
                .HasColumnName("exam_id");

            entity.Property(e => e.Type)
                .HasColumnName("type")
                .HasMaxLength(50);

            entity.Property(e => e.Message)
                .HasColumnName("message")
                .HasMaxLength(1000);

            entity.Property(e => e.IsRead)
                .HasColumnName("is_read");

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .ValueGeneratedNever();
        });
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
            entity.Property(session => session.QuestionScopeJson)
                .HasColumnName("question_scope_json").HasColumnType("text");
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
    }

    private static void ConfigureQuestionBank(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CourseMaterial>(entity =>
        {
            entity.ToTable("course_materials");
            entity.HasKey(material => material.MaterialId);

            entity.Property(material => material.MaterialId)
                .HasColumnName("material_id")
                .ValueGeneratedOnAdd();
            entity.Property(material => material.CourseId)
                .HasColumnName("course_id");
            entity.Property(material => material.FileName)
                .HasColumnName("file_name")
                .HasMaxLength(255)
                .IsRequired();
            entity.Property(material => material.FilePath)
                .HasColumnName("file_path")
                .HasMaxLength(1000)
                .IsRequired();
            // The column is a MySQL ENUM, so the value is stored as its name.
            entity.Property(material => material.FileType)
                .HasColumnName("file_type")
                .HasConversion<string>()
                .IsRequired();
            entity.Property(material => material.FileSize)
                .HasColumnName("file_size");
            entity.Property(material => material.UploadedBy)
                .HasColumnName("uploaded_by");
            // Also a MySQL ENUM. Module 5 never queues anything, so this is always
            // Completed; the column is kept for the future AI-ingestion feature.
            entity.Property(material => material.ProcessingStatus)
                .HasColumnName("processing_status")
                .HasConversion<string>()
                .HasDefaultValue(MaterialProcessingStatus.Completed);
            entity.Property(material => material.UploadedAt)
                .HasColumnName("uploaded_at");

            entity.HasIndex(material => material.CourseId);
            entity.HasOne(material => material.Course)
                .WithMany()
                .HasForeignKey(material => material.CourseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(material => material.Uploader)
                .WithMany()
                .HasForeignKey(material => material.UploadedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Question>(entity =>
        {
            entity.ToTable("questions");
            entity.HasKey(question => question.QuestionId);

            entity.Property(question => question.QuestionId)
                .HasColumnName("question_id")
                .ValueGeneratedOnAdd();
            entity.Property(question => question.CourseId)
                .HasColumnName("course_id");
            entity.Property(question => question.SourceMaterialId)
                .HasColumnName("source_material_id");
            entity.Property(question => question.CreatedBy)
                .HasColumnName("created_by");
            entity.Property(question => question.QuestionText)
                .HasColumnName("question_text")
                .IsRequired();
            entity.Property(question => question.ExpectedAnswer)
                .HasColumnName("expected_answer");
            entity.Property(question => question.BloomLevel)
                .HasColumnName("bloom_level")
                .HasConversion<string>()
                .IsRequired();
            entity.Property(question => question.Difficulty)
                .HasColumnName("difficulty")
                .HasConversion<string>()
                .IsRequired();
            entity.Property(question => question.QuestionType)
                .HasColumnName("question_type")
                .HasConversion<string>()
                .IsRequired();
            entity.Property(question => question.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .IsRequired();
            entity.Property(question => question.Source)
                .HasColumnName("source")
                .HasConversion<string>()
                .IsRequired();
            entity.Property(question => question.ReviewedBy)
                .HasColumnName("reviewed_by");
            entity.Property(question => question.ReviewedAt)
                .HasColumnName("reviewed_at");
            entity.Property(question => question.CreatedAt)
                .HasColumnName("created_at");
            entity.Property(question => question.UpdatedAt)
                .HasColumnName("updated_at");

            // The pick query filters on course + status and must not repeat a question
            // inside one exam, so the two columns are indexed together.
            entity.HasIndex(question => new { question.CourseId, question.Status });
            entity.HasIndex(question => question.SourceMaterialId);
            entity.HasIndex(question => question.Source);
            entity.HasIndex(question => question.ReviewedBy);
            entity.HasOne(question => question.Course)
                .WithMany()
                .HasForeignKey(question => question.CourseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(question => question.CreatedByUser)
                .WithMany()
                .HasForeignKey(question => question.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(question => question.SourceMaterial)
                .WithMany(material => material.Questions)
                .HasForeignKey(question => question.SourceMaterialId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<QuestionOption>(entity =>
        {
            entity.ToTable("question_options");
            entity.HasKey(option => option.OptionId);

            entity.Property(option => option.OptionId)
                .HasColumnName("option_id")
                .ValueGeneratedOnAdd();
            entity.Property(option => option.QuestionId)
                .HasColumnName("question_id");
            entity.Property(option => option.OptionText)
                .HasColumnName("option_text")
                .IsRequired();
            entity.Property(option => option.IsCorrect)
                .HasColumnName("is_correct");
            entity.Property(option => option.DisplayOrder)
                .HasColumnName("display_order");

            entity.HasIndex(option => option.QuestionId);
            entity.HasOne(option => option.Question)
                .WithMany(question => question.Options)
                .HasForeignKey(option => option.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExamQuestion>(entity =>
        {
            entity.ToTable("exam_questions");
            entity.HasKey(item => item.ExamQuestionId);

            entity.Property(item => item.ExamQuestionId)
                .HasColumnName("exam_question_id")
                .ValueGeneratedOnAdd();
            entity.Property(item => item.ExamId)
                .HasColumnName("exam_id");
            entity.Property(item => item.CandidateId)
                .HasColumnName("candidate_id");
            entity.Property(item => item.QuestionId)
                .HasColumnName("question_id");
            entity.Property(item => item.ParentExamQuestionId)
                .HasColumnName("parent_exam_question_id");
            entity.Property(item => item.OrderNo)
                .HasColumnName("order_no");
            entity.Property(item => item.AskedAt)
                .HasColumnName("asked_at");
            entity.Property(item => item.IsCompleted)
                .HasColumnName("is_completed");

            entity.HasIndex(item => new { item.CandidateId, item.OrderNo });
            entity.HasIndex(item => item.QuestionId);
            entity.HasOne(item => item.Session)
                .WithMany()
                .HasForeignKey(item => item.ExamId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Candidate)
                .WithMany()
                .HasForeignKey(item => item.CandidateId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Question)
                .WithMany()
                .HasForeignKey(item => item.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);
            // A follow-up means nothing without its main question, so it goes with it.
            entity.HasOne<ExamQuestion>()
                .WithMany()
                .HasForeignKey(item => item.ParentExamQuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Answer>(entity =>
        {
            entity.ToTable("answers");
            entity.HasKey(answer => answer.AnswerId);

            entity.Property(answer => answer.AnswerId)
                .HasColumnName("answer_id")
                .ValueGeneratedOnAdd();
            entity.Property(answer => answer.ExamQuestionId)
                .HasColumnName("exam_question_id");
            entity.Property(answer => answer.CandidateId)
                .HasColumnName("candidate_id");
            entity.Property(answer => answer.SelectedOptionId)
                .HasColumnName("selected_option_id");
            entity.Property(answer => answer.Transcript)
                .HasColumnName("transcript");
            entity.Property(answer => answer.AudioPath)
                .HasColumnName("audio_path")
                .HasMaxLength(1000);
            entity.Property(answer => answer.StartedAt)
                .HasColumnName("started_at");
            entity.Property(answer => answer.FinishedAt)
                .HasColumnName("finished_at");
            entity.Property(answer => answer.DurationSeconds)
                .HasColumnName("duration_seconds");
            entity.Property(answer => answer.CreatedAt)
                .HasColumnName("created_at");

            // One answer per slot, matching ux_answers_exam_question in the database.
            entity.HasIndex(answer => answer.ExamQuestionId).IsUnique();
            entity.HasOne(answer => answer.ExamQuestion)
                .WithMany()
                .HasForeignKey(answer => answer.ExamQuestionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(answer => answer.SelectedOption)
                .WithMany()
                .HasForeignKey(answer => answer.SelectedOptionId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureRubrics(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Rubric>(entity =>
        {
            entity.ToTable("rubrics");
            entity.HasKey(rubric => rubric.RubricId);

            entity.Property(rubric => rubric.RubricId)
                .HasColumnName("rubric_id")
                .ValueGeneratedOnAdd();

            entity.Property(rubric => rubric.QuestionId)
                .HasColumnName("question_id")
                .IsRequired();

            entity.Property(rubric => rubric.TotalScore)
                .HasColumnName("total_score")
                .HasPrecision(5, 2)
                .HasDefaultValue(0m);

            entity.Property(rubric => rubric.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValue(DateTime.Now);

            entity.Property(rubric => rubric.UpdatedAt)
                .HasColumnName("updated_at");

            // One rubric per question (enforced by unique key in DB).
            entity.HasIndex(rubric => rubric.QuestionId).IsUnique();

            entity.HasOne(rubric => rubric.Question)
                .WithOne(question => question.Rubric)
                .HasForeignKey<Rubric>(rubric => rubric.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RubricCriteria>(entity =>
        {
            entity.ToTable("rubric_criteria");
            entity.HasKey(criteria => criteria.CriteriaId);

            entity.Property(criteria => criteria.CriteriaId)
                .HasColumnName("criteria_id")
                .ValueGeneratedOnAdd();

            entity.Property(criteria => criteria.RubricId)
                .HasColumnName("rubric_id")
                .IsRequired();

            entity.Property(criteria => criteria.CriteriaName)
                .HasColumnName("criteria_name")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(criteria => criteria.Description)
                .HasColumnName("description")
                .HasMaxLength(1000);

            entity.Property(criteria => criteria.MaxScore)
                .HasColumnName("max_score")
                .HasPrecision(5, 2);

            entity.Property(criteria => criteria.DisplayOrder)
                .HasColumnName("display_order")
                .HasDefaultValue(1);

            entity.HasIndex(criteria => criteria.RubricId);

            entity.HasOne(criteria => criteria.Rubric)
                .WithMany(rubric => rubric.Criteria)
                .HasForeignKey(criteria => criteria.RubricId)
                .OnDelete(DeleteBehavior.Cascade);
        });
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
