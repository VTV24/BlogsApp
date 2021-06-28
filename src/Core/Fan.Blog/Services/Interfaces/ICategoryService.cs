using Fan.Blog.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Fan.Blog.Services.Interfaces
{
    public interface ICategoryService
    {
        Task<Category> GetAsync(int id);
        Task<Category> GetAsync(string slug);
        Task<List<Category>> GetAllAsync();
        Task SetDefaultAsync(int id);
        Task<Category> CreateAsync(string title, string description = null);
        Task<Category> UpdateAsync(Category category);
        Task DeleteAsync(int id);
    }
}
