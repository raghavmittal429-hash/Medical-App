using System.Linq.Expressions;

namespace TABS.Core.Persistence;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id);
    Task<List<T>> GetAllAsync(Func<T, bool>? predicate = null);
    Task AddAsync(T entity);
}

public class InMemoryRepository<T> : IRepository<T> where T : class
{
    private readonly List<T> _items = new();
    private readonly object _lock = new();

    public Task<T?> GetByIdAsync(Guid id)
    {
        lock (_lock)
        {
            var idProp = typeof(T).GetProperty("Id");
            if (idProp == null)
            {
                return Task.FromResult<T?>(null);
            }

            var match = _items.FirstOrDefault(item =>
            {
                var value = idProp.GetValue(item);
                return value is Guid gid && gid == id;
            });

            return Task.FromResult(match);
        }
    }

    public Task<List<T>> GetAllAsync(Func<T, bool>? predicate = null)
    {
        lock (_lock)
        {
            var result = predicate == null
                ? _items.ToList()
                : _items.Where(predicate).ToList();

            return Task.FromResult(result);
        }
    }

    public Task AddAsync(T entity)
    {
        lock (_lock)
        {
            _items.Add(entity);
        }

        return Task.CompletedTask;
    }
}
