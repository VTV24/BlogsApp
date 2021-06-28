using Fan.Blog.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Fan.Blog.Services.Interfaces
{
    public interface IPageService
    {
        Task<Page> CreateAsync(Page page);

        Task<Page> UpdateAsync(Page page);

        Task DeleteAsync(int id);

        Task<Page> GetAsync(int id);

        Task<Page> GetAsync(params string[] slugs);

        Task<IList<Page>> GetParentsAsync(bool withChildren = false);

        Task SaveNavAsync(int pageId, string navMd);
    }
}
