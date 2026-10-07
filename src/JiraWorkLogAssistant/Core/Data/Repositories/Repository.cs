using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace JiraWorkLogAssistant.Core.Data.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext _db;
    protected readonly DbSet<T> _set;

    public Repository(AppDbContext db)
    {
        _db = db;
        _set = db.Set<T>();
    }

    public async Task<T?> GetByIdAsync(int id) => await _set.FindAsync(id);
    public async Task<IEnumerable<T>> GetAllAsync() => await _set.ToListAsync();
    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate) => await _set.Where(predicate).ToListAsync();
    public async Task<T> AddAsync(T entity) { await _set.AddAsync(entity); await _db.SaveChangesAsync(); return entity; }
    public async Task UpdateAsync(T entity) { _set.Update(entity); await _db.SaveChangesAsync(); }
    public async Task DeleteAsync(T entity) { _set.Remove(entity); await _db.SaveChangesAsync(); }
    public async Task SaveChangesAsync() => await _db.SaveChangesAsync();
}
