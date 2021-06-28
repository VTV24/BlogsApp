using System.Collections.Generic;

namespace Fan.Blog.Models
{
    public class BlogPostList
    {
        public BlogPostList()
        {
            Posts = new List<BlogPost>();
        }

        public List<BlogPost> Posts { get; set; }

        public int TotalPostCount { get; set; }
    }
}
