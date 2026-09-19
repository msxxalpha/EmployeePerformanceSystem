using Microsoft.EntityFrameworkCore;

namespace Indamin.Performance.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<OrgUnit> OrgUnits => Set<OrgUnit>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<EvaluationDomain> EvaluationDomains => Set<EvaluationDomain>();
    public DbSet<PositionQuestion> PositionQuestions => Set<PositionQuestion>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EvaluationPeriod> Periods => Set<EvaluationPeriod>();
    public DbSet<Evaluation> Evaluations => Set<Evaluation>();
    public DbSet<EvaluationScore> Scores => Set<EvaluationScore>();
    public DbSet<EvaluationScoreHistory> ScoreHistory => Set<EvaluationScoreHistory>();
    public DbSet<EvaluatorChangeHistory> EvaluatorHistory => Set<EvaluatorChangeHistory>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>().ToTable("AppUsers");
        b.Entity<OrgUnit>().ToTable("OrgUnits");
        b.Entity<Position>().ToTable("Positions");
        b.Entity<Question>().ToTable("Questions");
        b.Entity<EvaluationDomain>().ToTable("EvaluationDomains");
        b.Entity<EvaluationDomain>().HasIndex(x => x.Code).IsUnique();
        b.Entity<PositionQuestion>().ToTable("PositionQuestions");
        b.Entity<Employee>().ToTable("Employees");
        b.Entity<EvaluationPeriod>().ToTable("EvaluationPeriods");
        b.Entity<Evaluation>().ToTable("Evaluations");
        b.Entity<EvaluationScore>().ToTable("EvaluationScores");
        b.Entity<EvaluationScoreHistory>().ToTable("EvaluationScoreHistory");
        b.Entity<EvaluatorChangeHistory>().ToTable("EvaluatorChangeHistory");
        b.Entity<AuditLog>().ToTable("AuditLogs");

        b.Entity<AppUser>().HasIndex(x => x.UserName).IsUnique();
        b.Entity<Employee>().HasIndex(x => x.PersonnelNo).IsUnique();
        b.Entity<Employee>().HasIndex(x => x.NationalNo).IsUnique();
        b.Entity<Question>().Property(x => x.Code).HasMaxLength(50);
        b.Entity<EvaluationDomain>().Property(x => x.Code).HasMaxLength(50);
        b.Entity<EvaluationDomain>().Property(x => x.Title).HasMaxLength(200);
        b.Entity<EvaluationDomain>().Property(x => x.SortOrder).IsRequired();

        b.Entity<AppUser>()
            .HasOne<Employee>().WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<OrgUnit>()
            .HasOne<OrgUnit>().WithMany()
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<Employee>()
            .HasOne(x => x.Position).WithMany()
            .HasForeignKey(x => x.PositionId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Employee>()
            .HasOne(x => x.Unit).WithMany()
            .HasForeignKey(x => x.UnitId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Employee>()
            .HasOne<Employee>().WithMany()
            .HasForeignKey(x => x.SupervisorId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<PositionQuestion>()
            .HasOne(x => x.Position).WithMany(x => x.QuestionMappings)
            .HasForeignKey(x => x.PositionId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<Question>()
            .HasOne(x => x.EvaluationDomain).WithMany()
            .HasForeignKey(x => x.DomainId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<PositionQuestion>()
            .HasOne(x => x.Question).WithMany(x => x.PositionMappings)
            .HasForeignKey(x => x.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<PositionQuestion>()
            .HasIndex(x => new { x.PositionId, x.QuestionId }).IsUnique();

        b.Entity<Evaluation>()
            .HasOne(x => x.Period).WithMany()
            .HasForeignKey(x => x.PeriodId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Evaluation>()
            .HasOne<Employee>().WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Evaluation>()
            .HasOne<Employee>().WithMany()
            .HasForeignKey(x => x.EvaluatorId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Evaluation>()
            .HasOne<Employee>().WithMany()
            .HasForeignKey(x => x.OriginalEvaluatorId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Evaluation>().HasIndex(x => new { x.PeriodId, x.EmployeeId }).IsUnique();

        b.Entity<EvaluationScore>()
            .HasOne<Evaluation>().WithMany(x => x.Scores)
            .HasForeignKey(x => x.EvaluationId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<EvaluationScore>()
            .HasOne<Question>().WithMany()
            .HasForeignKey(x => x.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<EvaluationScore>().HasIndex(x => new { x.EvaluationId, x.QuestionId }).IsUnique();

        b.Entity<EvaluationScoreHistory>()
            .HasOne<Evaluation>().WithMany()
            .HasForeignKey(x => x.EvaluationId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<EvaluationScoreHistory>()
            .HasOne<Question>().WithMany()
            .HasForeignKey(x => x.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<EvaluationScoreHistory>()
            .HasOne<Employee>().WithMany()
            .HasForeignKey(x => x.ChangedBy)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<EvaluatorChangeHistory>()
            .HasOne<Evaluation>().WithMany()
            .HasForeignKey(x => x.EvaluationId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<EvaluatorChangeHistory>()
            .HasOne<Employee>().WithMany()
            .HasForeignKey(x => x.PreviousEvaluatorId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<EvaluatorChangeHistory>()
            .HasOne<Employee>().WithMany()
            .HasForeignKey(x => x.NewEvaluatorId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<EvaluatorChangeHistory>()
            .HasOne<Employee>().WithMany()
            .HasForeignKey(x => x.ChangedBy)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<AuditLog>()
            .HasOne<AppUser>().WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<Position>().Property(x => x.MaxScore).HasPrecision(10, 2);
        b.Entity<PositionQuestion>().Property(x => x.MaxScore).HasPrecision(10, 2);
        b.Entity<Evaluation>().Property(x => x.FinalScore).HasPrecision(10, 2);
        b.Entity<Evaluation>().Property(x => x.FinalMaxScore).HasPrecision(10, 2);
        b.Entity<EvaluationScore>().Property(x => x.Score).HasPrecision(10, 2);
        b.Entity<EvaluationScore>().Property(x => x.MaxScore).HasPrecision(10, 2);
        b.Entity<EvaluationScoreHistory>().Property(x => x.OldScore).HasPrecision(10, 2);
        b.Entity<EvaluationScoreHistory>().Property(x => x.NewScore).HasPrecision(10, 2);
    }
}