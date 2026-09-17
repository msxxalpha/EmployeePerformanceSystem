using System.Globalization;
using Indamin.Performance.Data;
using Microsoft.EntityFrameworkCore;
namespace Indamin.Performance.Services;
public class PerformanceService(AppDbContext db)
{
    public static DateTime Jalali(string value)
    {
        var p = value.Replace('-', '/').Split('/');
        if (p.Length != 3) throw new ArgumentException("تاریخ شمسی نامعتبر است.");
        var pc = new PersianCalendar();
        var result = pc.ToDateTime(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]), 0, 0, 0, 0);
        return DateTime.SpecifyKind(result, DateTimeKind.Local);
    }
    public static string ToJalali(DateTime d){var pc=new PersianCalendar();return $"{pc.GetYear(d):0000}/{pc.GetMonth(d):00}/{pc.GetDayOfMonth(d):00}";}
    public async Task<EvaluationPeriod?> CurrentPeriod()=>await db.Periods.Where(x=>x.IsOpen&&x.StartAt<=DateTime.Now).OrderByDescending(x=>x.StartAt).FirstOrDefaultAsync();
    public Task<bool> CanEdit(EvaluationPeriod p)=>Task.FromResult(p.IsOpen&&DateTime.Now>=p.StartAt&&DateTime.Now<=p.EndAt);

    public async Task<List<Employee>> GetSubordinates(int evaluatorId)
    {
        // Authorization scope is derived only from the active Supervisor graph.
        // Position/Unit are optional enrichments and must never determine hierarchy membership.
        var employees = await db.Employees.Where(x=>x.IsActive).AsNoTracking().ToListAsync();
        var byId = employees.ToDictionary(x=>x.Id);
        var bySupervisor=employees.Where(x=>x.SupervisorId.HasValue).GroupBy(x=>x.SupervisorId!.Value).ToDictionary(g=>g.Key,g=>g.ToList());
        var result=new List<Employee>();
        var queue=new Queue<int>();
        queue.Enqueue(evaluatorId);
        var visited=new HashSet<int>{evaluatorId};
        while(queue.Count>0)
        {
            var managerId=queue.Dequeue();
            if(!bySupervisor.TryGetValue(managerId,out var children))continue;
            foreach(var child in children)
            {
                if(!visited.Add(child.Id))continue;
                // A cyclic reporting chain is invalid authorization data; exclude that branch.
                if(IsCyclicChain(child.Id, byId))continue;
                result.Add(child);
                queue.Enqueue(child.Id);
            }
        }
        if(result.Count>0)
        {
            var positionIds=result.Select(x=>x.PositionId).Distinct().ToList();
            var unitIds=result.Select(x=>x.UnitId).Distinct().ToList();
            var positions=await db.Positions.Where(x=>positionIds.Contains(x.Id)).AsNoTracking().ToDictionaryAsync(x=>x.Id);
            var units=await db.OrgUnits.Where(x=>unitIds.Contains(x.Id)).AsNoTracking().ToDictionaryAsync(x=>x.Id);
            foreach(var employee in result)
            {
                employee.Position=positions.GetValueOrDefault(employee.PositionId);
                employee.Unit=units.GetValueOrDefault(employee.UnitId);
            }
        }
        return result.OrderBy(x=>x.FullName).ToList();
    }

    private static bool IsCyclicChain(int employeeId, Dictionary<int,Employee> byId)
    {
        var seen=new HashSet<int>();
        var current=employeeId;
        while(byId.TryGetValue(current,out var employee)&&employee.SupervisorId.HasValue)
        {
            if(!seen.Add(current))return true;
            var supervisorId=employee.SupervisorId.Value;
            if(!byId.ContainsKey(supervisorId))return false;
            current=supervisorId;
        }
        return false;
    }

    public async Task<List<Employee>> GetDirectSubordinates(int evaluatorId)=>(await GetSubordinates(evaluatorId)).Where(x=>x.SupervisorId==evaluatorId).ToList();
    public async Task<bool> CanEvaluate(int evaluatorId,int employeeId)=>evaluatorId!=employeeId&&(await GetSubordinates(evaluatorId)).Any(x=>x.Id==employeeId);
    public async Task<bool> CanReviewEvaluation(int actorId,Evaluation ev){if(!await IsEvaluator(actorId))return false;if(actorId==ev.EvaluatorId)return true;return await IsAncestor(actorId,ev.EvaluatorId);}
    public async Task<bool> IsEvaluator(int employeeId)=>await db.Employees.AnyAsync(x=>x.Id==employeeId&&x.IsActive&&x.IsEvaluator);
    public async Task<bool> IsAncestor(int ancestorId,int employeeId){if(ancestorId==employeeId)return false;var employees=await db.Employees.Where(x=>x.IsActive).Select(x=>new{x.Id,x.SupervisorId}).AsNoTracking().ToListAsync();var map=employees.ToDictionary(x=>x.Id,x=>x.SupervisorId);var current=employeeId;var visited=new HashSet<int>();while(map.TryGetValue(current,out var supervisorId)&&supervisorId.HasValue){if(!visited.Add(current))return false;if(supervisorId.Value==ancestorId)return true;current=supervisorId.Value;}return false;}
    public async Task<decimal> MaxScore(int positionId)=>await db.Questions.Where(x=>x.PositionId==positionId&&x.IsActive).SumAsync(x=>x.MaxScore);
    public async Task<decimal> Total(int evaluationId)=>await db.Scores.Where(x=>x.EvaluationId==evaluationId).SumAsync(x=>x.Score);
    public async Task<decimal> MaxForEvaluation(int employeeId){var e=await db.Employees.FindAsync(employeeId);return e==null?0:await MaxScore(e.PositionId);}
}