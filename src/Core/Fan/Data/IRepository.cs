using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Fan.Data
{
    /// <summary>
    /// Contract for a base repository that provides commonly used data access methods.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <remarks>
    /// Common implementations of this interface could be sql or no-sql databases.
    /// See <see cref="EntityRepository{T}"/> for an Entity Framework implementation.
    /// </remarks>
    public interface IRepository<T> where T : class 
    {
        Task<T> CreateAsync(T obj);

        Task<IEnumerable<T>> CreateRangeAsync(IEnumerable<T> objs);

        Task DeleteAsync(int id);

        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);

        Task<T> GetAsync(int id);

        Task UpdateAsync(T obj);

        Task UpdateAsync(IEnumerable<T> objs);
    }
}
