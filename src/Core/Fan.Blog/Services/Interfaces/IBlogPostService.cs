using Fan.Blog.Models;
using System.Threading.Tasks;

namespace Fan.Blog.Services.Interfaces
{
    public interface IBlogPostService
    {
        Task<BlogPost> CreateAsync(BlogPost post);
        Task<BlogPost> UpdateAsync(BlogPost post);
        Task DeleteAsync(int id);
        Task<BlogPost> GetAsync(int id);
        Task<BlogPost> GetAsync(string slug, int year, int month, int day);
        Task<BlogPostList> GetListAsync(int pageIndex, int pageSize, bool cacheable = true);
        Task<BlogPostList> GetListForCategoryAsync(string categorySlug, int pageIndex);
        Task<BlogPostList> GetListForTagAsync(string tagSlug, int pageIndex);
        Task<BlogPostList> GetListForArchive(int? year, int? month, int page = 1);
        Task<BlogPostList> GetListForDraftsAsync();
        Task<BlogPostList> GetRecentPostsAsync(int numberOfPosts);
        Task<BlogPostList> GetRecentPublishedPostsAsync(int numberOfPosts);
        Task RemoveBlogCacheAsync();
    }
}