using Fan.Blog.Enums;
using Fan.Blog.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Fan.Blog.Services.Interfaces
{
    public interface IStatsService
    {
        Task<Dictionary<int, List<MonthItem>>> GetArchivesAsync();

        Task<PostCount> GetPostCountAsync();

        Task IncViewCountAsync(EPostType postType, int postId);
    }
}
