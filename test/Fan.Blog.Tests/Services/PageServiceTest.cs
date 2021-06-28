using Fan.Blog.Data;
using Fan.Blog.Enums;
using Fan.Blog.Helpers;
using Fan.Blog.Models;
using Fan.Blog.Services;
using Fan.Blog.Tests.Helpers;
using Fan.Exceptions;
using Fan.Settings;
using Markdig;
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
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace Fan.Blog.Tests.Services
{
    public class PageServiceTest
    {
        private readonly PageService pageService;
        private readonly Mock<IPostRepository> postRepoMock = new Mock<IPostRepository>();

        public PageServiceTest()
        {
            var serviceProvider = new ServiceCollection().AddMemoryCache().AddLogging().BuildServiceProvider();
            var cache = new MemoryDistributedCache(serviceProvider.GetService<IOptions<MemoryDistributedCacheOptions>>());
            var logger = serviceProvider.GetService<ILoggerFactory>().CreateLogger<PageService>();
            var mapper = BlogUtil.Mapper;
            var mediatorMock = new Mock<IMediator>();

            var settingSvcMock = new Mock<ISettingService>();
            settingSvcMock.Setup(svc => svc.GetSettingsAsync<CoreSettings>()).Returns(Task.FromResult(new CoreSettings()));
            settingSvcMock.Setup(svc => svc.GetSettingsAsync<BlogSettings>()).Returns(Task.FromResult(new BlogSettings()));

            pageService = new PageService(settingSvcMock.Object,
                postRepoMock.Object,
                cache, logger, mapper, mediatorMock.Object);
        }

        [Fact]
        public async void CreateAsync_Page()
        {
            postRepoMock.Setup(repo => repo.GetAsync(It.IsAny<int>(), EPostType.Page))
                .Returns(Task.FromResult(new Post { Type = EPostType.Page, CreatedOn = DateTimeOffset.Now }));

            var pageCreated = await pageService.CreateAsync(new Page
            {
                UserId = Actor.ADMIN_ID,
                Title = "Test Page",
                Body = "<h1>Test Page</h1>\n",
                BodyMark = "# Test Page",
                CreatedOn = DateTimeOffset.Now,
                Status = EPostStatus.Published,
            });

            postRepoMock.Verify(repo => repo.CreateAsync(
                It.Is<Post>(p => p.Slug == "test-page"
                              && p.Excerpt == null)), Times.Once);

            var coreSettings = new CoreSettings();
            Assert.Equal("now", pageCreated.CreatedOn.ToDisplayString(coreSettings.TimeZoneId));
        }

        [Fact]
        public async void Page_ValidateTitleAsync_draft_can_have_empty_title()
        {
            var page = new Page { Title = "", Status = EPostStatus.Draft };

            await page.ValidateTitleAsync();
        }

        [Fact]
        public void Page_with_Chinese_title_produces_UrlEncoded_slug_which_may_get_trimmed()
        {
            var pageTitle = string.Join("", Enumerable.Repeat<char>('验', 30));
            var page = new Page
            {
                Title = pageTitle,
            };

            var expectedSlug = WebUtility.UrlEncode(string.Join("", Enumerable.Repeat<char>('验', 27))) + "%E9%AA%";
            Assert.Equal(250, expectedSlug.Length);

            Assert.Equal(expectedSlug, PageService.SlugifyPageTitle(page.Title));
        }

        [Fact]
        public async void Page_title_resulting_duplicate_slug_throws_FanException()
        {
            var slug = WebUtility.UrlEncode(string.Join("", Enumerable.Repeat<char>('验', 27))) + "%E9%AA%";
            IList<Post> list = new List<Post> {
                new Post { Slug = slug, Type = EPostType.Page, Id = 1 },
            };
            postRepoMock.Setup(repo => repo.GetListAsync(It.IsAny<PostListQuery>())).Returns(Task.FromResult((list, 1)));

            var givenTitle = string.Join("", Enumerable.Repeat<char>('验', 30));
            var page = new Page { Title = givenTitle };
            var slug2 = PageService.SlugifyPageTitle(page.Title);
            await Assert.ThrowsAsync<FanException>(() => pageService.EnsurePageSlugAsync(slug2, page));
        }

        [Fact]
        public void Page_navigation_is_tranformed_to_HTML_based_on_child_page_titles()
        {
            var parentSlug = "docs";
            var navMd = "- [[Getting Started]] \n- [[Deploy to Azure]]";

            var actual = PageService.NavMdToHtml(navMd, parentSlug).Replace("\n", "");
            var expected = @"<ul><li><a href=""/docs/getting-started"" title=""Getting Started"">Getting Started</a></li><li><a href=""/docs/deploy-to-azure"" title=""Deploy to Azure"">Deploy to Azure</a></li></ul>";
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void SourceCode_markdown_is_tranformed_to_pre_code_html_by_Markdig()
        {
            var md = @"```cs
var i = 5;
```";
            var actual = Markdown.ToHtml(md).Replace("\n", "");
            var expected = @"<pre><code class=""language-cs"">var i = 5;</code></pre>";
            Assert.Equal(expected, actual);
        }
    }
}
