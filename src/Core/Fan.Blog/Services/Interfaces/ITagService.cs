using Fan.Blog.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Fan.Blog.Services.Interfaces
{
    public interface ITagService
    {
        Task<Tag> GetAsync(int id);
        Task<Tag> GetBySlugAsync(string slug);
        Task<Tag> GetByTitleAsync(string title);
        Task<List<Tag>> GetAllAsync();
        Task<Tag> CreateAsync(Tag tag);
        Task<Tag> UpdateAsync(Tag tag);
        Task DeleteAsync(int id);
    }
}
