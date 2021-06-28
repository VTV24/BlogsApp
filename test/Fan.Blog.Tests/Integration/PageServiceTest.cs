using Fan.Blog.Enums;
using Fan.Blog.Models;
using Fan.Blog.Services;
using Fan.Blog.Tests.Helpers;
using Fan.Exceptions;
using System;
using Xunit;

namespace Fan.Blog.Tests.Integration
{
    public class PageServiceTest : BlogServiceIntegrationTestBase
    {
        [Fact]
        public async void Admin_can_publish_a_page()
        {
            var userId = Seed_1User();

            var page = await _pageService.CreateAsync(new Page
            {
                UserId = userId,
                Title = "Test Page",
                Body = "<h1>Test Page</h1>\n",
                BodyMark = "# Test Page",
                CreatedOn = new DateTimeOffset(new DateTime(2017, 01, 01), new TimeSpan(-7, 0, 0)),
                Status = EPostStatus.Published,
            });

            Assert.Equal(1, page.Id);
            Assert.Equal("test-page", page.Slug);
            Assert.Equal("<h1>Test Page</h1>\n", page.Body);
        }

        [Fact]
        public async void Admin_can_publish_a_child_page()
        {
            var pageId = Seed_1Page();

            var child = await _pageService.CreateAsync(new Page
            {
                ParentId = pageId,
                BodyMark = "# Child Page",
                UserId = Actor.ADMIN_ID,
                CreatedOn = new DateTimeOffset(new DateTime(2019, 07, 30), new TimeSpan(-7, 0, 0)),
                Title = "Test Page",
                Status = EPostStatus.Published,
            });

            var parents = await _pageService.GetParentsAsync(true);
            Assert.Equal(1, parents.Count);
            Assert.Equal(1, parents[0].Children.Count);
            Assert.Equal(child.Id, parents[0].Children[0].Id);
        }

        [Fact]
        public async void Pages_are_hierarchical_parents_contain_children()
        {
            Seed_2_Parents_With_1_Child_Each();

            var parents = await _pageService.GetParentsAsync(true);
            Assert.Equal(2, parents.Count);
            Assert.Equal(1, parents[0].Children.Count);
        }

        [Fact]
        public async void Parent_slug_cannot_conflict_with_Reserved_Slugs()
        {
            var userId = Seed_1User();

            var ex = await Assert.ThrowsAsync<FanException>(() => _pageService.CreateAsync(new Page
            {
                UserId = userId,
                Title = "Login",
                Status = EPostStatus.Published,
            }));

            Assert.Equal(string.Format(PageService.RESERVED_SLUG_MSG, "login"), ex.Message);
        }

        [Fact]
        public async void Child_slug_is_OK_to_use_Reserved_Slugs()
        {
            Seed_2_Parents_With_1_Child_Each();

            await _pageService.CreateAsync(new Page
            {
                UserId = Actor.ADMIN_ID,
                ParentId = 1,
                Title = "Login",
                Status = EPostStatus.Published,
            });
        }

        [Fact]
        public async void Page_title_cannot_have_duplicate_from_its_siblings()
        {
            Seed_2_Parents_With_1_Child_Each();

            var ex = await Assert.ThrowsAsync<FanException>(() => _pageService.CreateAsync(new Page
            {
                UserId = Actor.ADMIN_ID,
                Title = "Page1",
                Status = EPostStatus.Published,
            }));

            Assert.Equal(PageService.DUPLICATE_TITLE_MSG, ex.Message);
        }

        [Fact]
        public async void Get_draft_page_from_public_throws_FanException()
        {
            var pageId = Seed_1Page();

            var page = await _pageService.GetAsync(pageId);
            page.Status = EPostStatus.Draft;
            await _pageService.UpdateAsync(page);

            await Assert.ThrowsAsync<FanException>(() => _pageService.GetAsync(page.Slug));
        }

        [Fact]
        public async void Deleting_a_root_page_also_deletes_its_children()
        {
            Seed_2_Parents_With_1_Child_Each();

            var parents = await _pageService.GetParentsAsync(true);
            var parentId = parents[0].Id;
            var childId = parents[0].Children[0].Id;

            await _pageService.DeleteAsync(parentId);

            await Assert.ThrowsAsync<FanException>(() => _pageService.GetAsync(childId));
        }

        [Fact]
        public async void When_there_are_no_pages_GetParentsAsync_returns_empty_list()
        {
            Assert.Empty(await _pageService.GetParentsAsync());
            Assert.Empty(await _pageService.GetParentsAsync(withChildren: true));
        }
    }
}
