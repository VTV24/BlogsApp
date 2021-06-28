using Fan.Blog.Enums;
using Fan.Settings;

namespace Fan.Blog.Models
{
    public class BlogSettings : ISettings
    {
        public int PostPerPage { get; set; } = 10;
        public int DefaultCategoryId { get; set; } = 1;
        public EPostListDisplay PostListDisplay { get; set; } = EPostListDisplay.FullBody;

        public bool AllowComments { get; set; } = true;
        public ECommentProvider CommentProvider { get; set; } = ECommentProvider.Disqus;
        public string DisqusShortname { get; set; }

        public bool FeedShowExcerpt { get; set; } = false;
    }
}
