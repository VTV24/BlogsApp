using Fan.Blog.Enums;
using Fan.Blog.Validators;
using Fan.Data;
using Fan.Exceptions;
using Fan.Membership;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Fan.Blog.Models
{
    public class Post : Entity
    {
        public Post()
        {
            PostTags = new HashSet<PostTag>();
        }

        public async Task ValidateTitleAsync()
        {
            var validator = new PostTitleValidator();
            var result = await validator.ValidateAsync(this);
            if (!result.IsValid)
            {
                throw new FanException($"{Type} title is not valid.", result.Errors);
            }
        }

        public string Body { get; set; }

        public string BodyMark { get; set; }

        public Category Category { get; set; }

        public int? CategoryId { get; set; }

        public int CommentCount { get; set; }

        public ECommentStatus CommentStatus { get; set; }

        public DateTimeOffset CreatedOn { get; set; }

        public string Excerpt { get; set; }

        public int? ParentId { get; set; }

        public int? RootId { get; set; }

        [StringLength(maximumLength: 256)]
        public string Slug { get; set; }

        public EPostStatus Status { get; set; }

        [StringLength(maximumLength: 256)]
        public string Title { get; set; }

        public EPostType Type { get; set; }

        public DateTimeOffset? UpdatedOn { get; set; }

        public User User { get; set; }

        public int UserId { get; set; }

        public int ViewCount { get; set; }

        public virtual ICollection<PostTag> PostTags { get; set; }

        public string Nav { get; set; }

        public byte? PageLayout { get; set; }
    }
}
