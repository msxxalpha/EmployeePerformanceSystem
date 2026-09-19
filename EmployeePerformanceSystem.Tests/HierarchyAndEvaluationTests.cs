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
        db.EvaluationDomains.Add(new EvaluationDomain { Id = 1, Code = "D1", Title = "کیفیت" });
        db.Positions.Add(new Position { Id = 2, Code = "P2", Title = "رده 2" });
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
        var (db, service) = CreateDb(); AddHierarchy(db); await db.SaveChangesAsync();
        Assert.Equal(new[]{2,3,4}, (await service.GetSubordinates(1)).OrderBy(x=>x.Id).Select(x=>x.Id).ToArray());
        Assert.Equal(new[]{3,4}, (await service.GetSubordinates(2)).OrderBy(x=>x.Id).Select(x=>x.Id).ToArray());
        Assert.True(await service.CanEvaluate(1,4)); Assert.True(await service.CanEvaluate(2,4)); Assert.False(await service.CanEvaluate(4,1));
    }

    [Fact]
    public async Task HierarchyScopeDoesNotDependOnMasterData()
    {
        var (db, service) = CreateDb(); AddHierarchy(db,false); await db.SaveChangesAsync();
        Assert.Equal(new[]{2,3,4}, (await service.GetSubordinates(1)).OrderBy(x=>x.Id).Select(x=>x.Id).ToArray());
    }

    [Fact]
    public async Task NonEvaluatorAndInactiveEvaluatorCannotAct()
    {
        var (db, service) = CreateDb(); AddMasterData(db);
        db.Employees.AddRange(
            new Employee{Id=10,PersonnelNo="10",FullName="X",PositionId=1,UnitId=1,IsActive=true,IsEvaluator=false},
            new Employee{Id=11,PersonnelNo="11",FullName="Y",PositionId=1,UnitId=1,IsActive=false,IsEvaluator=true});
        await db.SaveChangesAsync();
        Assert.False(await service.IsEvaluator(10)); Assert.False(await service.IsEvaluator(11));
    }

    [Fact]
    public async Task UpperEvaluatorCanReviewAndTakeoverEvaluation()
    {
        var (db, service) = CreateDb(); AddHierarchy(db);
        db.Evaluations.Add(new Evaluation{Id=50,PeriodId=1,EmployeeId=3,EvaluatorId=2,OriginalEvaluatorId=2,FinalScore=17,FinalMaxScore=20});
        await db.SaveChangesAsync();
        var ev=await db.Evaluations.SingleAsync(x=>x.Id==50);
        Assert.True(await service.CanReviewEvaluation(1,ev)); Assert.True(await service.CanReviewEvaluation(2,ev)); Assert.False(await service.CanReviewEvaluation(3,ev));
        ev.EvaluatorId=1; await db.SaveChangesAsync();
        Assert.False(await service.CanReviewEvaluation(2,ev)); Assert.True(await service.CanReviewEvaluation(1,ev));
        Assert.Equal(2,ev.OriginalEvaluatorId);
    }

    [Fact]
    public async Task AncestorCheckFollowsOnlyActiveChain()
    {
        var (db, service) = CreateDb(); AddHierarchy(db); await db.SaveChangesAsync();
        Assert.True(await service.IsAncestor(1,4)); Assert.True(await service.IsAncestor(2,4)); Assert.False(await service.IsAncestor(3,2));
        db.Employees.Single(x=>x.Id==2).IsActive=false; await db.SaveChangesAsync(); Assert.False(await service.IsAncestor(1,4));
    }

    [Fact]
    public async Task CircularHierarchyIsExcluded()
    {
        var (db, service) = CreateDb(); AddMasterData(db);
        db.Employees.AddRange(
            new Employee{Id=1,PersonnelNo="1",FullName="1",PositionId=1,UnitId=1,IsActive=true,IsEvaluator=true,SupervisorId=2},
            new Employee{Id=2,PersonnelNo="2",FullName="2",PositionId=1,UnitId=1,IsActive=true,IsEvaluator=true,SupervisorId=1});
        await db.SaveChangesAsync();
        Assert.Empty(await service.GetSubordinates(1)); Assert.False(await service.IsAncestor(1,1));
    }

    [Fact]
    public async Task InactiveEmployeeIsExcludedFromScope()
    {
        var (db, service) = CreateDb(); AddMasterData(db);
        db.Employees.AddRange(
            new Employee{Id=1,PersonnelNo="1",FullName="1",PositionId=1,UnitId=1,IsActive=true,IsEvaluator=true},
            new Employee{Id=2,PersonnelNo="2",FullName="2",PositionId=1,UnitId=1,IsActive=false,SupervisorId=1});
        await db.SaveChangesAsync(); Assert.Empty(await service.GetSubordinates(1)); Assert.False(await service.CanEvaluate(1,2));
    }

    [Fact]
    public async Task QuestionCanHaveDifferentMaxScoresPerPosition()
    {
        var (db, service) = CreateDb(); AddMasterData(db);
        db.Questions.Add(new Question{Id=1,Code="Q1",Title="Q",DomainId=1,Text="Q",IsActive=true});
        db.PositionQuestions.AddRange(
            new PositionQuestion{Id=1,PositionId=1,QuestionId=1,MaxScore=10,SortOrder=1},
            new PositionQuestion{Id=2,PositionId=2,QuestionId=1,MaxScore=25,SortOrder=1});
        await db.SaveChangesAsync();
        Assert.Equal(10,await service.MaxScore(1)); Assert.Equal(25,await service.MaxScore(2));
    }

    [Fact]
    public async Task EvaluationStoresQuestionMaxSnapshot()
    {
        var (db, service) = CreateDb(); AddMasterData(db);
        db.Questions.Add(new Question{Id=1,Code="Q1",Title="Q",Domain="کیفیت",Text="Q"});
        db.PositionQuestions.Add(new PositionQuestion{Id=1,PositionId=1,QuestionId=1,MaxScore=10});
        db.Evaluations.Add(new Evaluation{Id=7,PeriodId=1,EmployeeId=1,EvaluatorId=1,OriginalEvaluatorId=1});
        db.Scores.Add(new EvaluationScore{Id=1,EvaluationId=7,QuestionId=1,Score=8,MaxScore=10});
        await db.SaveChangesAsync();
        Assert.Equal(8,await service.Total(7)); Assert.Equal(10,db.Scores.Single().MaxScore);
    }

    [Fact]
    public async Task FinalScoreIsTheSumOfQuestionScores()
    {
        var (db, service) = CreateDb(); AddMasterData(db);
        db.Questions.AddRange(new Question{Id=1,Code="Q1",Title="Q1",DomainId=1},new Question{Id=2,Code="Q2",Title="Q2",DomainId=1});
        db.Evaluations.Add(new Evaluation{Id=9,PeriodId=1,EmployeeId=1,EvaluatorId=1,OriginalEvaluatorId=1});
        db.Scores.AddRange(new EvaluationScore{EvaluationId=9,QuestionId=1,Score=7,MaxScore=10},new EvaluationScore{EvaluationId=9,QuestionId=2,Score=3,MaxScore=5});
        await db.SaveChangesAsync();
        var ev=await db.Evaluations.Include(x=>x.Scores).SingleAsync(x=>x.Id==9);
        ev.FinalScore=ev.Scores.Sum(x=>x.Score); ev.FinalMaxScore=ev.Scores.Sum(x=>x.MaxScore); await db.SaveChangesAsync();
        Assert.Equal(10,ev.FinalScore); Assert.Equal(15,ev.FinalMaxScore);
        ev.Scores.Single(x=>x.QuestionId==2).Score=5; ev.FinalScore=ev.Scores.Sum(x=>x.Score); await db.SaveChangesAsync();
        Assert.Equal(12,ev.FinalScore);
    }

    [Fact]
    public async Task CanEditOnlyInsideConfiguredWindow()
    {
        var (db, service) = CreateDb(); var now=DateTime.Now;
        Assert.True(await service.CanEdit(new EvaluationPeriod{IsOpen=true,StartAt=now.AddMinutes(-1),EndAt=now.AddMinutes(1)}));
        Assert.False(await service.CanEdit(new EvaluationPeriod{IsOpen=true,StartAt=now.AddMinutes(-2),EndAt=now.AddMinutes(-1)}));
        Assert.False(await service.CanEdit(new EvaluationPeriod{IsOpen=false,StartAt=now.AddMinutes(-1),EndAt=now.AddMinutes(1)}));
    }

    [Fact]
    public async Task LatestStartedPeriodCanBeViewedAfterClosing()
    {
        var (db,service)=CreateDb();var now=DateTime.Now;
        db.Periods.AddRange(new EvaluationPeriod{Id=1,Title="قدیمی",StartAt=now.AddDays(-3),EndAt=now.AddDays(-2),IsOpen=true},new EvaluationPeriod{Id=2,Title="جدید",StartAt=now.AddHours(-1),EndAt=now.AddHours(1),IsOpen=true});
        await db.SaveChangesAsync();
        var p=await service.LatestStartedPeriod();Assert.NotNull(p);Assert.Equal(2,p!.Id);
    }
}