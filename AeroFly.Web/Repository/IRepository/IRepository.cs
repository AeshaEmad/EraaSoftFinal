using System.Linq.Expressions;

namespace AeroFly.Web.Repository.IRepository;

public interface IRepository<T> where T : class
{
    Task CreateAsync(T entity);
    void Update(T entity);
    void Delete(T entity);

    Task<List<T>> GetAsync(
        Expression<Func<T, bool>>? expression = null,
        bool tracked = false,
        params Expression<Func<T, object>>[]? includes);

    Task<T?> GetOneAsync(
        Expression<Func<T, bool>>? expression = null,
        bool tracked = false,
        params Expression<Func<T, object>>[]? includes);

    Task<int> CommitAsync();
}