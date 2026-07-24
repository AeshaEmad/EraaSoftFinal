using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using AeroFly.Web.Repository.IRepository;
using AeroFly.Web.Data;

namespace AeroFly.Web.Repository;

public class Repository<T> : IRepository<T> where T : class
{
    private readonly ApplicationDbContext _context;
    private readonly DbSet<T> _dbSet;

    public Repository(ApplicationDbContext context)
    {
        _context = context;
        _dbSet = _context.Set<T>();
    }

    public async Task CreateAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
    }

    public void Update(T entity)
    {
        _dbSet.Update(entity);
    }

    public void Delete(T entity)
    {
        _dbSet.Remove(entity);
    }

    public async Task<List<T>> GetAsync(
        Expression<Func<T, bool>>? expression = null,
        bool tracked = false,
        params Expression<Func<T, object>>[]? includes)
    {
        IQueryable<T> query = _dbSet;

        if (expression != null)
            query = query.Where(expression);

        if (includes != null)
        {
            foreach (var include in includes)
                query = query.Include(include);
        }

        if (!tracked)
            query = query.AsNoTracking();

        return await query.ToListAsync();
    }

    public async Task<T?> GetOneAsync(
        Expression<Func<T, bool>>? expression = null,
        bool tracked = false,
        params Expression<Func<T, object>>[]? includes)
    {
        IQueryable<T> query = _dbSet;

        if (expression != null)
            query = query.Where(expression);

        if (includes != null)
        {
            foreach (var include in includes)
                query = query.Include(include);
        }

        if (!tracked)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync();
    }

    public async Task<int> CommitAsync()
    {
        return await _context.SaveChangesAsync();
    }
}