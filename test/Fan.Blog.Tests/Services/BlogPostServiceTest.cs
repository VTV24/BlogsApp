using Fan.Blog.Data;
using Fan.Blog.Enums;
using Fan.Blog.Events;
using Fan.Blog.Helpers;
using Fan.Blog.Models;
using Fan.Blog.Services;
using Fan.Blog.Services.Interfaces;
using Fan.Blog.Tests.Helpers;
using Fan.Exceptions;
using Fan.Settings;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Fan.Blog.Tests.Services
{
    public class BlogPostServiceTest
    {
        private readonly BlogPostService blogPostService;
        private readonly Mock<IPostRepository> postRepoMock = new Mock<IPostRepository>();
        private readonly Mock<IMediator> mediatorMock = new Mock<IMediator>();
        private readonly CancellationToken cancellationToken = new CancellationToken();

        public BlogPostServiceTest()
        {
            var serviceProvider = new ServiceCollection().AddMemoryCache().AddLogging().BuildServiceProvider();
            var cache = new MemoryDistributedCache(serviceProvider.GetService<IOptions<MemoryDistributedCacheOptions>>());
            var logger = serviceProvider.GetService<ILoggerFactory>().CreateLogger<BlogPostService>();
            var mapper = BlogUtil.Mapper;

            var settingSvcMock = new Mock<ISettingService>();
            settingSvcMock.Setup(svc => svc.GetSettingsAsync<CoreSettings>()).Returns(Task.FromResult(new CoreSettings()));
            settingSvcMock.Setup(svc => svc.GetSettingsAsync<BlogSettings>()).Returns(Task.FromResult(new BlogSettings()));

            var imgSvcMock = new Mock<IImageService>();

            blogPostService = new BlogPostService(settingSvcMock.Object,
                imgSvcMock.Object,
                postRepoMock.Object,
                cache, logger, mapper, mediatorMock.Object);
        }


        [Fact]
        public async void CreateAsync_BlogPost_from_OLW()
        {
            postRepoMock.Setup(repo => repo.GetAsync(It.IsAny<int>(), EPostType.BlogPost))
                .Returns(Task.FromResult(new Post { CreatedOn = DateTimeOffset.Now }));

            var blogPostCreated = await blogPostService.CreateAsync(new BlogPost
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

            mediatorMock.Verify(m => m.Publish(
                It.Is<BlogPostBeforeCreate>(e => e.CategoryTitle == null
                                              && e.TagTitles == null
                ), cancellationToken), Times.Once);

            postRepoMock.Verify(repo => repo.CreateAsync(
                It.Is<Post>(p => p.Slug == "hello-world"
                              && p.CreatedOn != new DateTimeOffset()
                              && p.Excerpt == null
                           ), null, null), Times.Once);

            var coreSettings = new CoreSettings();
            Assert.Equal("now", blogPostCreated.CreatedOn.ToDisplayString(coreSettings.TimeZoneId));
        }

        [Fact]
        public async void CreateAsync_BlogPost_from_browser()
        {
            postRepoMock.Setup(repo => repo.GetAsync(It.IsAny<int>(), EPostType.BlogPost))
                .Returns(Task.FromResult(new Post()));

            var tagTitles = new List<string> { "test", "c#" };
            await blogPostService.CreateAsync(new BlogPost
            {
                UserId = Actor.ADMIN_ID,
                Title = "Hello World!",
                Slug = "hello-world-from-browser",
                Body = "This is my first post",
                Excerpt = null,
                CategoryId = 1,
                TagTitles = tagTitles,
                CreatedOn = DateTimeOffset.Now,
                Status = EPostStatus.Published,
                CommentStatus = ECommentStatus.AllowComments,
            });

            mediatorMock.Verify(m => m.Publish(
                It.Is<BlogPostBeforeCreate>(e => e.CategoryTitle == null
                                              && e.TagTitles == tagTitles
                ), cancellationToken), Times.Once);

            postRepoMock.Verify(repo => repo.CreateAsync(
                It.Is<Post>(p => p.Slug == "hello-world-from-browser"
                              && p.CreatedOn != new DateTimeOffset()
                              && p.Excerpt == null
                              && p.Category == null),
                null, tagTitles), Times.Once);
        }

        [Fact]
        public async void CreateAsync_draft_BlogPost_with_empty_title_and_slug_is_OK()
        {
            postRepoMock.Setup(repo => repo.GetAsync(It.IsAny<int>(), EPostType.BlogPost))
                .Returns(Task.FromResult(new Post()));

            var tagTitles = new List<string> { "test", "c#" };
            await blogPostService.CreateAsync(new BlogPost
            {
                UserId = Actor.ADMIN_ID,
                Title = null,
                Slug = null,
                Body = "This is my first post",
                Excerpt = null,
                CategoryId = 1,
                TagTitles = tagTitles,
                CreatedOn = DateTimeOffset.Now,
                Status = EPostStatus.Draft,
                CommentStatus = ECommentStatus.AllowComments,
            });

            mediatorMock.Verify(m => m.Publish(
                It.Is<BlogPostBeforeCreate>(e => e.CategoryTitle == null
                                              && e.TagTitles == tagTitles
                ), cancellationToken), Times.Once);

            postRepoMock.Verify(repo => repo.CreateAsync(
                It.Is<Post>(p => p.Slug == null
                              && p.Title == null),
                null, tagTitles), Times.Once);
        }

        [Fact]
        public async void UpdateAsync_BlogPost()
        {
            postRepoMock.Setup(repo => repo.GetAsync(It.IsAny<int>(), EPostType.BlogPost))
                .Returns(Task.FromResult(new Post()));

            var tagTitles = new List<string> { "test", "c#" };
            await blogPostService.UpdateAsync(new BlogPost
            {
                Id = 1,
                UserId = Actor.ADMIN_ID,
                Title = "Hello World!",
                Slug = "hello-world-from-browser",
                Body = "This is my first post",
                Excerpt = null,
                CategoryId = 1,
                TagTitles = tagTitles,
                CreatedOn = DateTimeOffset.Now,
                Status = EPostStatus.Published,
                CommentStatus = ECommentStatus.AllowComments,
            });

            mediatorMock.Verify(m => m.Publish(
                It.Is<BlogPostBeforeUpdate>(e => e.CategoryTitle == null
                                              && e.TagTitles == tagTitles
                                              && e.PostTags.Count() == 0), cancellationToken), Times.Once);

            postRepoMock.Verify(repo => repo.UpdateAsync(
                It.Is<Post>(p => p.Slug == "hello-world-from-browser"
                              && p.Excerpt == null
                              && p.Category == null),
                null, tagTitles), Times.Once);
        }

        [Fact]
        public async void Update_post_title_will_not_alter_slug()
        {
            var title = "A blog post title";
            var dt = DateTimeOffset.Now;
            var postId = 1;
            postRepoMock.Setup(r => r.GetAsync(It.IsAny<string>(), dt.Year, dt.Month, dt.Day))
                .Returns(Task.FromResult((Post)null));

            var slug = await blogPostService.GetBlogPostSlugAsync(title, dt, ECreateOrUpdate.Create, postId);

            var theSlug = await blogPostService.GetBlogPostSlugAsync(slug, dt, ECreateOrUpdate.Update, postId);

            Assert.Equal(theSlug, slug);
        }

        [Fact]
        public async void Update_post_slug_will_alter_slug()
        {
            var title = "A blog post title";
            var dt = DateTimeOffset.Now;
            var postId = 1;
            postRepoMock.Setup(r => r.GetAsync(It.IsAny<string>(), dt.Year, dt.Month, dt.Day))
                .Returns(Task.FromResult((Post)null));

            var slug = await blogPostService.GetBlogPostSlugAsync(title, dt, ECreateOrUpdate.Create, postId);

            slug = "i-want-a-different-slug-for-this-post";

            var theSlug = await blogPostService.GetBlogPostSlugAsync(slug, dt, ECreateOrUpdate.Update, postId);

            Assert.Equal(theSlug, slug);
        }

        [Theory]
        [InlineData("A blog post title", "a-blog-post-title", "a-blog-post-title-2")]
        [InlineData("A blog post title 2", "a-blog-post-title-2", "a-blog-post-title-3")]
        public async void Create_post_will_always_produce_unique_slug(string title, string slug, string expected)
        {
            var dt = DateTimeOffset.Now;
            postRepoMock.Setup(r => r.GetAsync(slug, dt.Year, dt.Month, dt.Day))
                .Returns(Task.FromResult(new Post { Id = 10000, Slug = slug }));

            var postId = 1;
            var slugUnique = await blogPostService.GetBlogPostSlugAsync(title, dt, ECreateOrUpdate.Create, postId);

            Assert.Equal(expected, slugUnique);
        }

        [Fact]
        public async void Update_post_will_produce_unique_slug_if_user_updates_slug_to_run_into_conflict()
        {
            var slug = "i-want-a-different-slug-for-this-post";
            var dt = DateTimeOffset.Now;
            postRepoMock.Setup(r => r.GetAsync(slug, dt.Year, dt.Month, dt.Day))
                .Returns(Task.FromResult(new Post { Id = 10000, Slug = slug }));

            var postId = 1;
            var title = "A blog post title";
            var slugCreated = await blogPostService.GetBlogPostSlugAsync(title, dt, ECreateOrUpdate.Create, postId);
            Assert.Equal("a-blog-post-title", slugCreated);

            slugCreated = "i-want-a-different-slug-for-this-post";

            var slugUpdated = await blogPostService.GetBlogPostSlugAsync(slug, dt, ECreateOrUpdate.Update, postId);

            Assert.Equal("i-want-a-different-slug-for-this-post-2", slugUpdated);
        }

        [Fact]
        public async void BlogPost_draft_can_have_empty_title()
        {
            var blogPost = new BlogPost { Title = "", Status = EPostStatus.Draft };

            await blogPost.ValidateTitleAsync();
        }

        [Theory]
        [InlineData(null, EPostStatus.Published, 1, new string[] { "'Title' must not be empty." })]
        [InlineData("", EPostStatus.Published, 1, new string[] { "'Title' must not be empty." })]
        public async void Publish_BlogPost_does_not_allow_empty_title(string title, EPostStatus status, int numberOfErrors, string[] expectedMessages)
        {
            var blogPost = new BlogPost { Title = title, Status = status };

            var ex = await Assert.ThrowsAsync<FanException>(() => blogPost.ValidateTitleAsync());
            Assert.Equal(numberOfErrors, ex.ValidationErrors.Count);
            Assert.Equal(expectedMessages[0], ex.ValidationErrors[0].ErrorMessage);
        }

        [Theory]
        [InlineData(EPostStatus.Draft, new string[] { "The length of 'Title' must be 250 characters or fewer. You entered 251 characters." })]
        [InlineData(EPostStatus.Published, new string[] { "The length of 'Title' must be 250 characters or fewer. You entered 251 characters." })]
        public async void BlogPost_title_cannot_exceed_250_chars_regardless_status(EPostStatus status, string[] expectedMessages)
        {
            var title = string.Join("", Enumerable.Repeat<char>('a', 251));
            var blogPost = new BlogPost { Title = title, Status = status };

            var ex = await Assert.ThrowsAsync<FanException>(() => blogPost.ValidateTitleAsync());
            Assert.Equal(1, ex.ValidationErrors.Count);
            Assert.Equal(expectedMessages[0], ex.ValidationErrors[0].ErrorMessage);
        }

        [Fact]
        public async void CreateAsync_throws_ArgumentNullException_if_param_passed_in_is_null()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => blogPostService.CreateAsync(null));
        }

        [Fact]
        public async void UpdateAsync_throws_ArgumentException_if_param_passed_in_is_null()
        {
            await Assert.ThrowsAsync<ArgumentException>(() => blogPostService.UpdateAsync(null));
        }
    }
}
