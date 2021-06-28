using Fan.Blog.Enums;
using System.Collections.Generic;

namespace Fan.Blog.Models
{
    public class Page : Post, IHierarchical<Page>
    {
        public Page()
        {
            Children = new List<Page>();
        }

        public IList<Page> Children { get; set; }

        public Page Parent { get; set; }

        public new EPostType Type { get; } = EPostType.Page;

        public bool IsParent => ParentId == null || ParentId == 0;

        public bool HasChildren => Children.Count > 0;
    }
}
