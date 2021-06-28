using Fan.Blog.Data;
using Fan.Blog.Models;
using System.Linq;
using Xunit;

namespace Fan.Blog.Tests.Data
{
    public class SqlTagRepositoryTest : BlogIntegrationTestBase
    {
        SqlTagRepository _tagRepo;
        public SqlTagRepositoryTest()
        {
            _tagRepo = new SqlTagRepository(_db);
        }

        [Fact]
        public async void CreateTag_Creates_A_Tag_In_Db()
        {
            var tag = new Tag { Slug = "tag", Title = "Tag" };

            await _tagRepo.CreateAsync(tag);

            Assert.NotNull(_db.Set<Tag>().SingleOrDefault(c => c.Title == "Tag"));
        }

        [Fact]
        public async void DeleteTag_Will_Delete_PostTag_Association_By_Cascade_Delete()
        {
            Seed_1BlogPost_with_1Category_2Tags();

            await _tagRepo.DeleteAsync(1);

            Assert.True(_db.Set<PostTag>().Count() == 1);
        }

        [Fact]
        public async void GetTagList_Returns_PostCount_With_Tags()
        {
            Seed_N_BlogPosts(11);

            var list = await _tagRepo.GetListAsync();

            Assert.Equal(2, list.Count);
            Assert.Equal(0, list[1].Count);
        }

        [Fact]
        public async void UpdateTag_Updates_It_In_Db()
        {
            var tag = new Tag { Slug = "tag", Title = "Tag" };
            await _tagRepo.CreateAsync(tag);

            var tagAgain = _db.Set<Tag>().Single(t => t.Slug == "tag");
            tagAgain.Title = "Tag2";
            tagAgain.Slug = "tag2";
            await _tagRepo.UpdateAsync(tagAgain);

            var catAgain = _db.Set<Tag>().Single(c => c.Slug == "tag2");
            Assert.Equal("Tag2", catAgain.Title);
            Assert.Equal("tag2", catAgain.Slug);
            Assert.Equal(1, catAgain.Id);
        }
    }
}
