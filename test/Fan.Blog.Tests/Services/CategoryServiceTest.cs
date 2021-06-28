using Fan.Blog.Data;
using Fan.Blog.Helpers;
using Fan.Blog.Models;
using Fan.Blog.Services;
using Fan.Blog.Services.Interfaces;
using Fan.Exceptions;
using Fan.Settings;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Fan.Blog.Tests.Services
{
    public class CategoryServiceTest
    {
        private readonly ICategoryService categoryService;
        private readonly Mock<ICategoryRepository> catRepoMock = new Mock<ICategoryRepository>();
        private readonly Mock<IMediator> mediatorMock = new Mock<IMediator>();
        private readonly IDistributedCache cache;

        public CategoryServiceTest()
        {
            var serviceProvider = new ServiceCollection().AddMemoryCache().AddLogging().BuildServiceProvider();
            cache = new MemoryDistributedCache(serviceProvider.GetService<IOptions<MemoryDistributedCacheOptions>>());
            var logger = serviceProvider.GetService<ILoggerFactory>().CreateLogger<CategoryService>();

            var settingSvcMock = new Mock<ISettingService>();
            settingSvcMock.Setup(svc => svc.GetSettingsAsync<CoreSettings>()).Returns(Task.FromResult(new CoreSettings()));
            settingSvcMock.Setup(svc => svc.GetSettingsAsync<BlogSettings>()).Returns(Task.FromResult(new BlogSettings()));

            var defaultCat = new Category { Id = 1, Title = "Web Development", Slug = "web-development" };
            catRepoMock.Setup(c => c.GetAsync(1)).Returns(Task.FromResult(defaultCat));
            catRepoMock.Setup(r => r.GetListAsync()).Returns(Task.FromResult(new List<Category> { defaultCat }));

            categoryService = new CategoryService(catRepoMock.Object, settingSvcMock.Object, mediatorMock.Object, cache, logger);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async void Create_category_with_empty_title_throws_FanException(string title)
        {
            await Assert.ThrowsAsync<FanException>(() => categoryService.CreateAsync(title));
        }

        [Fact]
        public async void Create_category_throws_FanException_if_title_already_exists()
        {
            var title = "web development";
            var ex = await Assert.ThrowsAsync<FanException>(() => categoryService.CreateAsync(title));

            Assert.Equal("'web development' already exists.", ex.Message);
        }

        [Fact]
        public async void Create_category_calls_repo_and_invalidates_cache_for_all_categories()
        {
            var cat = new Category { Title = "Cat1" };

            await categoryService.CreateAsync(cat.Title);

            catRepoMock.Verify(repo => repo.CreateAsync(It.IsAny<Category>()), Times.Exactly(1));
            Assert.Null(await cache.GetAsync(BlogCache.KEY_ALL_CATS));
        }

        [Fact]
        public async void Delete_category_calls_repo_and_invalidates_cache_for_all_categories()
        {
            await categoryService.DeleteAsync(2);

            catRepoMock.Verify(repo => repo.DeleteAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Exactly(1));
            Assert.Null(await cache.GetAsync(BlogCache.KEY_ALL_CATS));
        }

        [Fact]
        public async void Update_category_calls_repo_and_invalidates_cache_for_all_categories()
        {
            var cat = await categoryService.GetAsync(1);

            cat.Title = "Cat1";
            await categoryService.UpdateAsync(cat);

            catRepoMock.Verify(repo => repo.UpdateAsync(It.IsAny<Category>()), Times.Exactly(1));
            Assert.Null(await cache.GetAsync(BlogCache.KEY_ALL_CATS));
        }

        [Fact]
        public async void Update_category_with_invalid_category_throws_FanException()
        {
            await Assert.ThrowsAsync<FanException>(() => categoryService.UpdateAsync(null));
            await Assert.ThrowsAsync<FanException>(() => categoryService.UpdateAsync(new Category()));
            await Assert.ThrowsAsync<FanException>(() => categoryService.UpdateAsync(new Category { Id = 1 }));
        }

        [Fact]
        public async void Update_category_with_title_changed_only_in_casing_is_OK()
        {
            var cat = await categoryService.GetAsync(1);
            Assert.Equal("Web Development", cat.Title);

            cat.Title = "web development";

            var catAgain = await categoryService.UpdateAsync(cat);
            Assert.Equal(1, catAgain.Id);
            Assert.Equal("web development", catAgain.Title);
        }
    }
}
