using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Fan.Data
{
    /// <summary>
    /// Sql implementation of the <see cref="IMetaRepository"/> contract.
    /// </summary>
    public class SqlMetaRepository : EntityRepository<Meta>, IMetaRepository
    {
        private readonly FanDbContext _db;

        public SqlMetaRepository(FanDbContext db) : base(db)
        {
            _db = db;
        }

        public async Task<Meta> GetAsync(string key, EMetaType type) =>
            await _entities.SingleOrDefaultAsync(m => m.Key == key && m.Type == type);

        public async Task<List<Meta>> GetListAsync(EMetaType type) =>
            await _entities.Where(m => m.Type == type).ToListAsync();
    }
}