using Fan.Blog.Enums;
using Fan.Blog.Models;
using Fan.Blog.Tests.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Fan.Blog.Tests.Integration
{
    public class BlogPostServiceTest : BlogServiceIntegrationTestBase
    {
        [Fact]
        public async void Admin_publishes_BlogPost_from_OLW()
        {
            Seed_1BlogPost_with_1Category_2Tags();

            var result = await _blogPostSvc.CreateAsync(new BlogPost
            {
                UserId = Actor.ADMIN_ID,
                Title = "Hello World!",
                Slug = null,
                Body = "This is my first post",
                Excerpt = null,
                CategoryTitle = null,
                TagTitles = null,
                CreatedOn = new DateTimeOffset(),
                Status = EPostStatus.Published,
                CommentStatus = ECommentStatus.AllowComments,
            });

            Assert.Equal(2, result.Id);
            Assert.Equal("hello-world", result.Slug);
            Assert.NotEqual(DateTimeOffset.MinValue, result.CreatedOn);
            Assert.Equal(1, result.Category.Id);
            Assert.Empty(result.Tags);
        }

        [Fact]
        public async void Admin_publishes_BlogPost_from_browser()
        {
            Seed_1BlogPost_with_1Category_2Tags();

            var createdOn = DateTimeOffset.Now;
            var blogPost = await _blogPostSvc.CreateAsync(new BlogPost
            {
                UserId = Actor.ADMIN_ID,
                Title = "Hello World!",
                Slug = null,
                Body = "This is my first post",
                Excerpt = null,
                CategoryId = 1,
                TagTitles = new List<string> { "test", TAG2_TITLE },
                CreatedOn = createdOn,
                Status = EPostStatus.Published,
                CommentStatus = ECommentStatus.AllowComments,
            });
            var tags = await _tagSvc.GetAllAsync();

            Assert.Equal(2, blogPost.Id);
            Assert.Equal("hello-world", blogPost.Slug);
            Assert.Equal(createdOn.ToUniversalTime(), blogPost.CreatedOn);
            Assert.Equal(1, blogPost.Category.Id);
            Assert.Equal(2, blogPost.Tags.Count);
            Assert.Contains(blogPost.Tags, t => t.Title.Equals("test"));
            Assert.Equal(3, tags.Count);
            Assert.Equal(2, tags.Find(t => t.Title == TAG2_TITLE).Count);
            Assert.Equal(1, tags.Find(t => t.Title == "test").Count);
        }

        [Fact]
        public async void Admin_publishes_BlogPost_with_new_Category_and_Tag_from_OLW()
        {
            Seed_1BlogPost_with_1Category_2Tags();

            var result = await _blogPostSvc.CreateAsync(new BlogPost
            {
                UserId = Actor.ADMIN_ID,
                Title = "Hello World!",
                Slug = null,
                Body = "This is my first post",
                Excerpt = null,
                CategoryTitle = "Travel",
                TagTitles = new List<string> { "Windows 10", TAG2_TITLE },
                Tags = await _tagSvc.GetAllAsync(),
                CreatedOn = new DateTimeOffset(),
                Status = EPostStatus.Published,
                CommentStatus = ECommentStatus.AllowComments,
            });

            Assert.Equal(2, result.Id);
            Assert.Equal(2, result.Category.Id);
            Assert.Equal("travel", result.Category.Slug);
            Assert.Equal(2, result.Tags.Count);
            Assert.Equal("cs", result.Tags[1].Slug);

            var cats = await _catSvc.GetAllAsync();
            var tags = await _tagSvc.GetAllAsync();

            Assert.Equal(2, cats.Count);
            Assert.Equal(1, cats[1].Count);
            Assert.Equal(3, tags.Count);
            Assert.Equal(2, tags.Find(t => t.Title == TAG2_TITLE).Count);
        }

        [Fact]
        public async void Admin_updates_BlogPost_from_OLW()
        {
            Seed_1BlogPost_with_1Category_2Tags();
            var blogPost = await _blogPostSvc.GetAsync(1);
            var wasCreatedOn = blogPost.CreatedOn;

            blogPost.CategoryTitle = "Travel";
            blogPost.TagTitles = new List<string> { "Windows 10", TAG2_TITLE };
            blogPost.Tags = await _tagSvc.GetAllAsync();
            blogPost.CreatedOn = DateTimeOffset.Now;

            var result = await _blogPostSvc.UpdateAsync(blogPost);

            Assert.Equal(2, result.Category.Id);
            Assert.Equal("travel", result.Category.Slug);
            Assert.Equal(2, result.Tags.Count);
            Assert.NotNull(result.Tags.SingleOrDefault(t => t.Title == TAG2_TITLE));
            Assert.NotNull(result.Tags.SingleOrDefault(t => t.Slug == "windows-10"));

            var cats = await _catSvc.GetAllAsync();
            var tags = await _tagSvc.GetAllAsync();

            Assert.Equal(2, cats.Count);
            Assert.Equal(0, cats[0].Count);
            Assert.Equal(1, cats[1].Count);

            Assert.Equal(3, tags.Count);
            Assert.Equal(1, tags.Find(t => t.Title == TAG2_TITLE).Count);

            Assert.True(result.CreatedOn > wasCreatedOn);
            Assert.Null(result.UpdatedOn);
        }

        [Fact]
        public async void Admin_updates_BlogPost_to_draft_from_browser()
        {
            Seed_1BlogPost_with_1Category_2Tags();
            var blogPost = await _blogPostSvc.GetAsync(1);
            var wasCreatedOn = blogPost.CreatedOn;

            blogPost.CategoryTitle = "Travel";
            blogPost.TagTitles = new List<string> { "Windows 10", TAG2_TITLE };
            blogPost.Tags = await _tagSvc.GetAllAsync();
            blogPost.CreatedOn = DateTimeOffset.Now;
            blogPost.Status = EPostStatus.Draft;

            var result = await _blogPostSvc.UpdateAsync(blogPost);

            Assert.Equal(2, result.Category.Id);
            Assert.Equal("travel", result.Category.Slug);
            Assert.Equal(2, result.Tags.Count);
            Assert.NotNull(result.Tags.SingleOrDefault(t => t.Title == TAG2_TITLE));
            Assert.NotNull(result.Tags.SingleOrDefault(t => t.Slug == "windows-10"));

            var cats = await _catSvc.GetAllAsync();
            var tags = await _tagSvc.GetAllAsync();

            Assert.Equal(2, cats.Count);
            Assert.Equal(0, cats[0].Count);
            Assert.Equal(0, cats[1].Count);

            Assert.Equal(3, tags.Count);
            Assert.Equal(0, tags.Find(t => t.Title == TAG2_TITLE).Count);

            Assert.True(result.CreatedOn > wasCreatedOn);
            Assert.True(result.UpdatedOn.HasValue);
        }

        [Fact]
        public async void Admin_Can_Save_Draft_With_Empty_Title_And_Slug()
        {
            Seed_1BlogPost_with_1Category_2Tags();

            var createdOn = DateTimeOffset.Now;
            var result = await _blogPostSvc.CreateAsync(new BlogPost
            {
                UserId = Actor.ADMIN_ID,
                Title = null,
                Slug = null,
                Body = "This is my first post",
                Excerpt = null,
                CategoryId = 1,
                TagTitles = null,
                CreatedOn = createdOn,
                Status = EPostStatus.Draft,
                CommentStatus = ECommentStatus.AllowComments,
            });

            Assert.Equal(2, result.Id);
            Assert.Null(result.Slug);
            Assert.Null(result.Title);
            Assert.Equal(createdOn.ToUniversalTime(), result.CreatedOn);
            Assert.Equal(1, result.Category.Id);
            Assert.Empty(result.Tags);
        }
    }
}
