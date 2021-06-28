using Fan.Blog.Data;
using Fan.Blog.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Xunit;

namespace Fan.Blog.Tests.Data
{
    public class SqlCategoryRepositoryTest : BlogIntegrationTestBase
    {
        SqlCategoryRepository _catRepo;

        public SqlCategoryRepositoryTest()
        {
            _catRepo = new SqlCategoryRepository(_db);
        }

        [Fact]
        public async void CreateCategory_Creates_A_Category_In_Db()
        {
            var cat = new Category { Slug = "cat-create", Title = "CategoryCreate" };

            await _catRepo.CreateAsync(cat);

            Assert.NotNull(_db.Set<Category>().SingleOrDefault(c => c.Title == "CategoryCreate"));
        }

        [Fact]
        public async void DeleteCategory_Will_Recategorize_Its_Posts_To_Default_Category()
        {
            Seed_1BlogPost_with_1Category_2Tags();
            var existCat = _db.Set<Category>().Single(c => c.Slug == CAT_SLUG);

            await _catRepo.DeleteAsync(existCat.Id, 1);

            var post = _db.Set<Post>().Include(p => p.Category).Single(p => p.Slug == POST_SLUG);
            Assert.True(_db.Set<Category>().Count() == 1);
            Assert.True(post.Category.Id == 1);
        }

        [Fact]
        public async void GetCategoryList_Returns_All_Categories()
        {
            var cat1 = new Category { Slug = "cat1", Title = "Category1" };
            var cat2 = new Category { Slug = "cat2", Title = "Category2" };
            await _catRepo.CreateAsync(cat1);
            await _catRepo.CreateAsync(cat2);

            var list = await _catRepo.GetListAsync();

            Assert.Equal(2, list.Count);
        }

        [Fact]
        public async void GetCategoryList_Returns_NonTracked_Categories()
        {
            var cat1 = new Category { Slug = "cat1", Title = "Category1" };
            var cat2 = new Category { Slug = "cat2", Title = "Category2" };
            await _catRepo.CreateAsync(cat1);
            await _catRepo.CreateAsync(cat2);

            var list = await _catRepo.GetListAsync();
            var cat = list.FirstOrDefault(c => c.Slug == "cat1");
            cat.Title = "New Cat";
            await _catRepo.UpdateAsync(cat);

            var catAgain = _db.Set<Category>().FirstOrDefault(c => c.Slug == "cat1");
            Assert.NotEqual("New Cat", catAgain.Title);
        }

        [Fact]
        public async void UpdateCategory_Updates_It_In_Db()
        {
            var cat = new Category { Slug = "cat1", Title = "Category1" };
            await _catRepo.CreateAsync(cat);

            var catAgain = _db.Set<Category>().Single(c => c.Slug == "cat1");
            catAgain.Title = "Dog";
            catAgain.Slug = "dog";
            await _catRepo.UpdateAsync(catAgain);

            catAgain = _db.Set<Category>().Single(c => c.Slug == "dog");
            Assert.Equal("Dog", catAgain.Title);
            Assert.Equal("dog", catAgain.Slug);
            Assert.Equal(1, catAgain.Id);
        }
    }
}
