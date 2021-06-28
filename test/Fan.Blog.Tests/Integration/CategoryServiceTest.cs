using Fan.Blog.Services;
using Fan.Exceptions;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Fan.Blog.Tests.Integration
{
    public class CategoryServiceTest : BlogServiceIntegrationTestBase
    {
        [Fact]
        public async void Create_category_only_requires_title()
        {
            var cat = await _catSvc.CreateAsync("Technology");
            Assert.Equal(1, cat.Id);
            Assert.Equal("Technology", cat.Title);
            Assert.Equal("technology", cat.Slug);
            Assert.Null(cat.Description);
            Assert.Equal(0, cat.Count);
        }


        [Fact]
        public async void Category_title_will_be_trimmed_beyond_allowed_length()
        {
            var title = string.Join("", Enumerable.Repeat<char>('a', 251));
            var cat = await _catSvc.CreateAsync(title);
            Assert.Equal(CategoryService.TITLE_MAXLEN, cat.Title.Length);
        }

        [Fact]
        public async void Default_category_cannot_be_deleted()
        {
            await Assert.ThrowsAsync<FanException>(() => _catSvc.DeleteAsync(1));
        }

        [Fact]
        public async void Create_category_with_duplicate_title_throws_FanException()
        {
            Seed_1BlogPost_with_1Category_2Tags();

            Task action() => _catSvc.CreateAsync(CAT_TITLE);

            var ex = await Assert.ThrowsAsync<FanException>(action);
            Assert.Equal($"'{CAT_TITLE}' already exists.", ex.Message);
        }

        [Fact]
        public async void Update_category_with_duplicate_title_throws_FanException()
        {
            await _catSvc.CreateAsync("Tech");
            await _catSvc.CreateAsync("Tech!!");

            var cat = await _catSvc.GetAsync(2);
            cat.Title = "Tech";
            Task action() => _catSvc.UpdateAsync(cat);

            var ex = await Assert.ThrowsAsync<FanException>(action);
            Assert.Equal("'Tech' already exists.", ex.Message);
        }

        [Fact]
        public async void Update_category_title_will_generate_new_slug()
        {
            Seed_1BlogPost_with_1Category_2Tags();
            var cat = await _catSvc.GetAsync(CAT_SLUG);
            Assert.Equal(1, cat.Id);
            Assert.Equal("Technology", cat.Title);

            cat.Title = "Music";
            cat.Description = "A music category.";
            cat = await _catSvc.UpdateAsync(cat);

            Assert.Equal(1, cat.Id);
            Assert.Equal("Music", cat.Title);
            Assert.Equal("music", cat.Slug);
            Assert.Equal("A music category.", cat.Description);
        }

        [Fact]
        public async void Get_category_throws_FanException_if_not_found()
        {
            await Assert.ThrowsAsync<FanException>(() => _catSvc.GetAsync(100));
            await Assert.ThrowsAsync<FanException>(() => _catSvc.GetAsync("slug-not-exist"));
        }

        [Fact]
        public async void Category_slug_is_guaranteed_to_be_unique()
        {
            Seed_1BlogPost_with_1Category_2Tags();

            var cat = await _catSvc.CreateAsync("Technology!!!");

            Assert.Equal("technology-2", cat.Slug);
        }

        [Fact]
        public async void Category_with_Chinese_title_results_random_6_char_slug()
        {
            var cat = await _catSvc.CreateAsync("你好");

            Assert.Equal(6, cat.Slug.Length);
        }

        [Fact]
        public async void Category_title_with_pound_sign_will_turn_into_letter_s()
        {
            var category = await _catSvc.CreateAsync("C#");

            Assert.Equal("cs", category.Slug);
        }

        [Fact]
        public async void Category_title_and_description_will_be_cleaned_off_of_any_html_tags()
        {
            var category = await _catSvc.CreateAsync("<h1>Test</h1>", "<p>This is a test category.</p>");

            Assert.Equal("Test", category.Title);
            Assert.Equal("This is a test category.", category.Description);
        }
    }
}
