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

    private static void AddMasterData(AppDbContext db)
    {
        db.Positions.Add(new Position { Id = 1, Code = "P1", Title = "رده 1" });
        db.OrgUnits.Add(new OrgUnit { Id = 1, Code = "U1", Title = "واحد 1" });
    }

    private static void AddHierarchy(AppDbContext db, bool includeMasterData = true)
    {
        if (includeMasterData) AddMasterData(db);
        db.Employees.AddRange(
            new Employee { Id=1, PersonnelNo="A", FullName="A", PositionId=1, UnitId=1, IsActive=true, IsEvaluator=true },
            new Employee { Id=2, PersonnelNo="B", FullName="B", PositionId=1, UnitId=1, IsActive=true, IsEvaluator=true, SupervisorId=1 },
            new Employee { Id=3, PersonnelNo="C", FullName="C", PositionId=1, UnitId=1, IsActive=true, SupervisorId=2 },
            new Employee { Id=4, PersonnelNo="D", FullName="D", PositionId=1, UnitId=1, IsActive=true, SupervisorId=3 });
    }

    [Fact]
    public async Task UpperEvaluatorSeesCompleteSubtree()
    {
        var (db, service) = CreateDb();
        AddHierarchy(db);
        await db.SaveChangesAsync();
        var ids = (await service.GetSubordinates(1)).OrderBy(x=>x.Id).Select(x=>x.Id).ToArray();
        Assert.Equal(new[]{2,3,4}, ids);
        Assert.Equal(new[]{3,4}, (await service.GetSubordinates(2)).OrderBy(x=>x.Id).Select(x=>x.Id));
        Assert.True((await service.GetSubordinates(1)).All(x=>x.Position != null && x.Unit != null));
        Assert.True(await service.CanEvaluate(1,4));
        Assert.True(await service.CanEvaluate(2,4));
        Assert.False(await service.CanEvaluate(4,1));
    }

    [Fact]
    public async Task HierarchyScopeDoesNotDependOnMasterDataIncludes()
    {
        var (db, service) = CreateDb();
        AddHierarchy(db, includeMasterData:false);
        await db.SaveChangesAsync();
        var ids = (await service.GetSubordinates(1)).OrderBy(x=>x.Id).Select(x=>x.Id).ToArray();
        Assert.Equal(new[]{2,3,4}, ids);
    }

    [Fact]
    public async Task NonEvaluatorCannotBeEvaluatorActor()
    {
        var (db, service) = CreateDb();
        AddMasterData(db);
        db.Employees.Add(new Employee { Id=10, PersonnelNo="10", FullName="X", PositionId=1, UnitId=1, IsActive=true, IsEvaluator=false });
        await db.SaveChangesAsync();
        Assert.False(await service.IsEvaluator(10));
    }

    [Fact]
    public async Task InactiveEvaluatorCannotAct()
    {
        var (db, service) = CreateDb();
        AddMasterData(db);
        db.Employees.Add(new Employee { Id=10, PersonnelNo="10", FullName="X", PositionId=1, UnitId=1, IsActive=false, IsEvaluator=true });
        await db.SaveChangesAsync();
        Assert.False(await service.IsEvaluator(10));
    }

    [Fact]
    public async Task EvaluatorCanReviewEvaluationOfSubordinateEvaluator()
    {
        var (db, service) = CreateDb();
        AddHierarchy(db);
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
    public async Task AncestorCheckFollowsOnlyActiveChain()
    {
        var (db, service) = CreateDb();
        AddHierarchy(db);
        await db.SaveChangesAsync();
        Assert.True(await service.IsAncestor(1,4));
        Assert.True(await service.IsAncestor(2,4));
        Assert.False(await service.IsAncestor(3,2));
        db.Employees.Single(x=>x.Id==2).IsActive=false;
        await db.SaveChangesAsync();
        Assert.False(await service.IsAncestor(1,4));
    }

    [Fact]
    public async Task ScoreCannotExceedQuestionMaxAtDomainValidationBoundary()
    {
        var (db, service) = CreateDb();
        AddMasterData(db);
        db.Questions.Add(new Question { Id=1, PositionId=1, Text="Q", MaxScore=10, IsActive=true });
        await db.SaveChangesAsync();
        Assert.Equal(10, await service.MaxScore(1));
    }

    [Fact]
    public async Task TotalIsRecomputedFromCurrentScores()
    {
        var (db, service) = CreateDb();
        AddMasterData(db);
        db.Evaluations.Add(new Evaluation { Id=7, PeriodId=1, EmployeeId=1, EvaluatorId=1, OriginalEvaluatorId=1 });
        db.Scores.AddRange(
            new EvaluationScore { Id=1, EvaluationId=7, QuestionId=1, Score=7 },
            new EvaluationScore { Id=2, EvaluationId=7, QuestionId=2, Score=3 });
        await db.SaveChangesAsync();
        Assert.Equal(10, await service.Total(7));
        db.Scores.Single(x=>x.Id==2).Score=8;
        await db.SaveChangesAsync();
        Assert.Equal(15, await service.Total(7));
    }

    [Fact]
    public async Task CircularHierarchyDoesNotLoopForever()
    {
        var (db, service) = CreateDb();
        AddMasterData(db);
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
        AddMasterData(db);
        db.Employees.AddRange(
            new Employee { Id=1, PersonnelNo="A", FullName="A", PositionId=1, UnitId=1, IsActive=true, IsEvaluator=true },
            new Employee { Id=2, PersonnelNo="B", FullName="B", PositionId=1, UnitId=1, IsActive=false, IsEvaluator=false, SupervisorId=1 });
        await db.SaveChangesAsync();
        Assert.Empty(await service.GetSubordinates(1));
        Assert.False(await service.CanEvaluate(1,2));
    }

    [Fact]
    public async Task CanEditOnlyInsideConfiguredEvaluationWindow()
    {
        var (db, service) = CreateDb();
        var now = DateTime.Now;
        var inside = new EvaluationPeriod { Id=1, Title="Inside", StartAt=now.AddMinutes(-1), EndAt=now.AddMinutes(1), IsOpen=true };
        var outside = new EvaluationPeriod { Id=2, Title="Outside", StartAt=now.AddMinutes(-2), EndAt=now.AddMinutes(-1), IsOpen=true };
        Assert.True(await service.CanEdit(inside));
        Assert.False(await service.CanEdit(outside));
        Assert.False(await service.CanEdit(new EvaluationPeriod { IsOpen=false, StartAt=now.AddMinutes(-1), EndAt=now.AddMinutes(1) }));
    }
}