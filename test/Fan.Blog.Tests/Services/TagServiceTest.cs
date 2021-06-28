using Fan.Blog.Data;
using Fan.Blog.Helpers;
using Fan.Blog.Models;
using Fan.Blog.Services;
using Fan.Blog.Services.Interfaces;
using Fan.Data;
using Fan.Exceptions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Fan.Blog.Tests.Services
{
    public class TagServiceTest
    {
        private readonly Mock<ITagRepository> tagRepoMock = new Mock<ITagRepository>();
        private readonly ITagService tagService;
        private readonly IDistributedCache cache;

        public TagServiceTest()
        {
            var serviceProvider = new ServiceCollection().AddMemoryCache().AddLogging().BuildServiceProvider();
            cache = new MemoryDistributedCache(serviceProvider.GetService<IOptions<MemoryDistributedCacheOptions>>());
            var logger = serviceProvider.GetService<ILoggerFactory>().CreateLogger<TagService>();

            var metaRepoMock = new Mock<IMetaRepository>();
            metaRepoMock.Setup(repo => repo.GetAsync("BlogSettings", EMetaType.Setting))
                .Returns(Task.FromResult(new Meta { Key = "BlogSettings", Value = JsonConvert.SerializeObject(new BlogSettings()) }));

            var tag = new Tag { Id = 1, Title = "technology", Slug = "technology" };
            tagRepoMock.Setup(r => r.GetListAsync()).Returns(Task.FromResult(new List<Tag> { tag }));
            tagRepoMock.Setup(r => r.GetAsync(1)).Returns(Task.FromResult(tag));

            tagService = new TagService(tagRepoMock.Object, cache, logger);
        }

        [Theory]
        [InlineData("Web Development!", "web-development")]
        [InlineData("C#", "cs")]
        public async void User_can_only_input_tag_Title_not_Slug(string title, string expectSlug)
        {
            var tag = new Tag { Title = title };
            tagRepoMock.Setup(repo => repo.CreateAsync(It.IsAny<Tag>())).Returns(Task.FromResult(tag));

            tag = await tagService.CreateAsync(tag);

            Assert.Equal(expectSlug, tag.Slug);
        }

        [Fact]
        public async void CreateTag_Throws_FanException_If_Title_Already_Exist()
        {
            var tag = new Tag { Title = "Technology" };

            var ex = await Assert.ThrowsAsync<FanException>(() => tagService.CreateAsync(tag));
            Assert.Equal("'Technology' already exists.", ex.Message);
        }


        [Fact]
        public async void CreateTag_Calls_TagRepository_CreateAsync_And_Invalidates_Cache_For_AllTags()
        {
            var tag = new Tag { Title = "Tag1" };

            await tagService.CreateAsync(tag);

            tagRepoMock.Verify(repo => repo.CreateAsync(It.IsAny<Tag>()), Times.Exactly(1));
            Assert.Null(await cache.GetAsync(BlogCache.KEY_ALL_TAGS));
        }

        [Fact]
        public async void DeleteTag_Calls_TagRepository_DeleteAsync_And_Invalidates_Cache_For_AllTags()
        {
            await tagService.DeleteAsync(1);

            tagRepoMock.Verify(repo => repo.DeleteAsync(It.IsAny<int>()), Times.Exactly(1));
            Assert.Null(await cache.GetAsync(BlogCache.KEY_ALL_TAGS));
        }

        [Fact]
        public async void GetTagBySlug_Throws_FanException_If_Not_Found()
        {
            await Assert.ThrowsAsync<FanException>(() => tagService.GetAsync(100));
            await Assert.ThrowsAsync<FanException>(() => tagService.GetBySlugAsync("slug-not-exist"));
        }

        [Fact]
        public async void Update_an_existing_tag_with_new_title()
        {
            var techTag = await tagService.GetAsync(1);

            techTag.Title = "Tech";
            techTag = await tagService.UpdateAsync(techTag);

            Assert.Equal("Tech", techTag.Title);
            Assert.Equal("tech", techTag.Slug);
        }

        [Fact]
        public async void Update_tag_with_title_changed_only_in_casing_is_OK()
        {
            var tag = await tagService.GetAsync(1);
            Assert.Equal("technology", tag.Title);

            tag.Title = "Technology";

            var tagAgain = await tagService.UpdateAsync(tag);
            Assert.Equal(1, tagAgain.Id);
            Assert.Equal("Technology", tagAgain.Title);
        }

        [Fact]
        public async void UpdateTag_Calls_TagRepository_And_Invalidates_Cache_For_AllTags()
        {
            var tag = await tagService.GetAsync(1);

            await tagService.UpdateAsync(tag);

            tagRepoMock.Verify(repo => repo.UpdateAsync(It.IsAny<Tag>()), Times.Exactly(1));
            Assert.Null(await cache.GetAsync(BlogCache.KEY_ALL_TAGS));
        }
    }
}
