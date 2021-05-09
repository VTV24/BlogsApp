using Fan.Exceptions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Fan.Data
{
    public class EntityRepository<T> : IRepository<T> where T : Entity
    {
      
        protected readonly DbSet<T> _entities;
        protected readonly bool isSqlite;

        
        private readonly DbContext _db;

        public EntityRepository(DbContext context) 
        {
            _entities = context.Set<T>();
            _db = context;
            isSqlite = _db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
        }

        /// <summary>
        /// Creates an entity and returns a tracked object with id.
        /// </summary>
        /// <param name="entity"></param>
        /// <returns>The <paramref name="entity"/> with id.</returns>
        /// <exception cref="FanException">
        /// Throws if insert violates unique key constraint. See <see cref="https://stackoverflow.com/a/47465944/32240"/>
        /// </exception>
        public virtual async Task<T> CreateAsync(T entity)
        {
            try
            {
                await _entities.AddAsync(entity);
                await _db.SaveChangesAsync();
                return entity;
            }
            catch (DbUpdateException dbUpdEx) 
            {
                throw GetExceptionForUniqueConstraint(dbUpdEx);
            }
        }

       
        public virtual async Task<IEnumerable<T>> CreateRangeAsync(IEnumerable<T> entities)
        {
            _entities.AddRange(entities);
            await _db.SaveChangesAsync();
            return entities;
        }

       
        public virtual async Task DeleteAsync(int id)
        {
            var entity = await _entities.SingleAsync(e => e.Id == id);
            _entities.Remove(entity);
            await _db.SaveChangesAsync();
        }

        
        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate) =>
            isSqlite ? 
                _entities.ToList().Where(predicate.Compile()).ToList() :
                await _entities.Where(predicate).ToListAsync();

       
        public virtual async Task<T> GetAsync(int id) => await _entities.SingleOrDefaultAsync(e => e.Id == id);

        /// <summary>
        /// Updates an entity.
        /// </summary>
        /// <param name="entity">
        /// The entity to be updated, the EF implementation does not use this parameter.
        /// </param>
        /// <exception cref="FanException">
        /// Throws if update violates unique key constraint.
        /// </exception>
        public virtual async Task UpdateAsync(T entity)
        {
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException dbUpdEx)
            {
                throw GetExceptionForUniqueConstraint(dbUpdEx);
            }
        }

        public virtual async Task UpdateAsync(IEnumerable<T> entities)
        {
            await _db.SaveChangesAsync();
        }

        private Exception GetExceptionForUniqueConstraint(DbUpdateException dbUpdEx)
        {
            if (dbUpdEx.InnerException != null)
            {
                var message = dbUpdEx.InnerException.Message;
                if (message.Contains("UniqueConstraint", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("Unique Constraint", StringComparison.OrdinalIgnoreCase))
                    return new FanException(EExceptionType.DuplicateRecord, dbUpdEx);

                if (dbUpdEx.InnerException.InnerException != null)
                {
                    message = dbUpdEx.InnerException.InnerException.Message;
                    if (message.Contains("UniqueConstraint", StringComparison.OrdinalIgnoreCase)
                        || message.Contains("Unique Constraint", StringComparison.OrdinalIgnoreCase))
                        return new FanException(EExceptionType.DuplicateRecord, dbUpdEx);
                }
            }

            return dbUpdEx;
        }
    }
}
