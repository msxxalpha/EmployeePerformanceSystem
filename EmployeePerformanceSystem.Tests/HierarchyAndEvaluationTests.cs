using Indamin.Performance.Data;
using Indamin.Performance.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EmployeePerformanceSystem.Tests;

public class HierarchyAndEvaluationTests
{
    private static (AppDbContext db, PerformanceService service) CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(options);
        return (db, new PerformanceService(db));
    }

    [Fact]
    public async Task UpperEvaluatorSeesCompleteSubtree()
    {
        var (db, service) = CreateDb();
        db.Employees.AddRange(
            new Employee { Id=1, PersonnelNo="A", FullName="A", PositionId=1, UnitId=1, IsEvaluator=true },
            new Employee { Id=2, PersonnelNo="B", FullName="B", PositionId=1, UnitId=1, IsEvaluator=true, SupervisorId=1 },
            new Employee { Id=3, PersonnelNo="C", FullName="C", PositionId=1, UnitId=1, SupervisorId=2 },
            new Employee { Id=4, PersonnelNo="D", FullName="D", PositionId=1, UnitId=1, SupervisorId=3 });
        await db.SaveChangesAsync();
        var ids = await service.GetSubordinates(1);
        Assert.Equal(new[]{2,3,4}, ids.OrderBy(x=>x.Id).Select(x=>x.Id));
    }

    [Fact]
    public async Task NonEvaluatorCannotBeEvaluatorActor()
    {
        var (db, service) = CreateDb();
        db.Employees.Add(new Employee { Id=10, PersonnelNo="10", FullName="X", PositionId=1, UnitId=1, IsEvaluator=false });
        await db.SaveChangesAsync();
        Assert.False(await service.IsEvaluator(10));
    }

    [Fact]
    public async Task EvaluatorCanReviewEvaluationOfSubordinateEvaluator()
    {
        var (db, service) = CreateDb();
        db.Employees.AddRange(
            new Employee { Id=1, PersonnelNo="A", FullName="A", PositionId=1, UnitId=1, IsEvaluator=true },
            new Employee { Id=2, PersonnelNo="B", FullName="B", PositionId=1, UnitId=1, IsEvaluator=true, SupervisorId=1 },
            new Employee { Id=3, PersonnelNo="C", FullName="C", PositionId=1, UnitId=1, SupervisorId=2 });
        db.Evaluations.Add(new Evaluation { Id=50, PeriodId=1, EmployeeId=3, EvaluatorId=2, OriginalEvaluatorId=2 });
        await db.SaveChangesAsync();
        Assert.True(await service.CanReviewEvaluation(1, await db.Evaluations.SingleAsync(x=>x.Id==50)));
        Assert.False(await service.CanReviewEvaluation(3, await db.Evaluations.SingleAsync(x=>x.Id==50)));
    }

    [Fact]
    public async Task ScoreCannotExceedQuestionMaxAtDomainValidationBoundary()
    {
        var (db, service) = CreateDb();
        db.Questions.Add(new Question { Id=1, PositionId=1, Text="Q", MaxScore=10, IsActive=true });
        await db.SaveChangesAsync();
        Assert.Equal(10, await service.MaxScore(1));
    }

    [Fact]
    public async Task CircularHierarchyDoesNotLoopForever()
    {
        var (db, service) = CreateDb();
        db.Employees.AddRange(
            new Employee { Id=1, PersonnelNo="1", FullName="1", PositionId=1, UnitId=1, IsEvaluator=true, SupervisorId=2 },
            new Employee { Id=2, PersonnelNo="2", FullName="2", PositionId=1, UnitId=1, IsEvaluator=true, SupervisorId=1 });
        await db.SaveChangesAsync();
        Assert.False(await service.IsAncestor(1,1));
    }
}
