using Fan.Blog.Helpers;
using Fan.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Fan.Blog.Models
{
    public class Category : Entity
    {
        [Required]
        [StringLength(maximumLength: 256)]
        public string Title { get; set; }

        [Required]
        [StringLength(maximumLength: 256)]
        public string Slug { get; set; }

        public string Description { get; set; }

        [NotMapped]
        public int Count { get; set; }

        [NotMapped]
        public string RelativeLink => BlogRoutes.GetCategoryRelativeLink(Slug);

        [NotMapped]
        public string RssRelativeLink => BlogRoutes.GetCategoryRssRelativeLink(Slug);
    }
}
