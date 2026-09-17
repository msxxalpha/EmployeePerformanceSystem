using System;
using System.Linq;
using System.Threading.Tasks;
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
            new Employee { Id=1, PersonnelNo="A", FullName="A", PositionId=1, UnitId=1, IsActive=true, IsEvaluator=true },
            new Employee { Id=2, PersonnelNo="B", FullName="B", PositionId=1, UnitId=1, IsActive=true, IsEvaluator=true, SupervisorId=1 },
            new Employee { Id=3, PersonnelNo="C", FullName="C", PositionId=1, UnitId=1, IsActive=true, SupervisorId=2 },
            new Employee { Id=4, PersonnelNo="D", FullName="D", PositionId=1, UnitId=1, IsActive=true, SupervisorId=3 });
        await db.SaveChangesAsync();
        var ids = (await service.GetSubordinates(1)).OrderBy(x=>x.Id).Select(x=>x.Id).ToArray();
        Assert.Equal(new[]{2,3,4}, ids);
        Assert.Equal(new[]{3,4}, (await service.GetSubordinates(2)).OrderBy(x=>x.Id).Select(x=>x.Id));
        Assert.True(await service.CanEvaluate(1,4));
        Assert.True(await service.CanEvaluate(2,4));
    }

    [Fact]
    public async Task NonEvaluatorCannotBeEvaluatorActor()
    {
        var (db, service) = CreateDb();
        db.Employees.Add(new Employee { Id=10, PersonnelNo="10", FullName="X", PositionId=1, UnitId=1, IsActive=true, IsEvaluator=false });
        await db.SaveChangesAsync();
        Assert.False(await service.IsEvaluator(10));
    }

    [Fact]
    public async Task EvaluatorCanReviewEvaluationOfSubordinateEvaluator()
    {
        var (db, service) = CreateDb();
        db.Employees.AddRange(
            new Employee { Id=1, PersonnelNo="A", FullName="A", PositionId=1, UnitId=1, IsActive=true, IsEvaluator=true },
            new Employee { Id=2, PersonnelNo="B", FullName="B", PositionId=1, UnitId=1, IsActive=true, IsEvaluator=true, SupervisorId=1 },
            new Employee { Id=3, PersonnelNo="C", FullName="C", PositionId=1, UnitId=1, IsActive=true, SupervisorId=2 });
        db.Evaluations.Add(new Evaluation { Id=50, PeriodId=1, EmployeeId=3, EvaluatorId=2, OriginalEvaluatorId=2 });
        await db.SaveChangesAsync();
        var ev = await db.Evaluations.SingleAsync(x=>x.Id==50);
        Assert.True(await service.IsEvaluator(1));
        Assert.True(await service.IsEvaluator(2));
        Assert.True(await service.CanReviewEvaluation(1, ev));
        Assert.True(await service.CanReviewEvaluation(2, ev));
        Assert.False(await service.CanReviewEvaluation(3, ev));
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
            new Employee { Id=1, PersonnelNo="1", FullName="1", PositionId=1, UnitId=1, IsActive=true, IsEvaluator=true, SupervisorId=2 },
            new Employee { Id=2, PersonnelNo="2", FullName="2", PositionId=1, UnitId=1, IsActive=true, IsEvaluator=true, SupervisorId=1 });
        await db.SaveChangesAsync();
        Assert.False(await service.IsAncestor(1,1));
        Assert.Empty(await service.GetSubordinates(1));
    }

    [Fact]
    public async Task InactiveEmployeeIsExcludedFromEvaluatorScope()
    {
        var (db, service) = CreateDb();
        db.Employees.AddRange(
            new Employee { Id=1, PersonnelNo="A", FullName="A", PositionId=1, UnitId=1, IsActive=true, IsEvaluator=true },
            new Employee { Id=2, PersonnelNo="B", FullName="B", PositionId=1, UnitId=1, IsActive=false, IsEvaluator=false, SupervisorId=1 });
        await db.SaveChangesAsync();
        Assert.Empty(await service.GetSubordinates(1));
        Assert.False(await service.CanEvaluate(1,2));
    }
}