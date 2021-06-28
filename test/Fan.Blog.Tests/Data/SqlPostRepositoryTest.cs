using Fan.Blog.Data;
using Fan.Blog.Enums;
using Fan.Blog.Models;
using Fan.Blog.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Fan.Blog.Tests.Data
{
    public class SqlPostRepositoryTest : BlogIntegrationTestBase
    {
        SqlPostRepository _postRepo;

        public SqlPostRepositoryTest()
        {
            _postRepo = new SqlPostRepository(_db);
        }

        [Fact]
        public async void GetPost_By_Id_Will_Return_Category_And_Tags_For_BlogPost()
        {
            Seed_1BlogPost_with_1Category_2Tags();

            var post = await _postRepo.GetAsync(1, EPostType.BlogPost);

            Assert.NotNull(post.Category);
            Assert.True(post.PostTags.Count() == 2);
            Assert.True(post.PostTags.ToList()[0].Tag.Id == 1);
        }

        [Fact]
        public async void GetPost_By_Id_Will_Return_Null_If_Not_Found()
        {
            var blogPost = await _postRepo.GetAsync(1, EPostType.BlogPost);

            Assert.Null(blogPost);
        }

        [Fact]
        public async void GetPost_By_Slug_Will_Return_Category_And_Tags_For_BlogPost()
        {
            Seed_1BlogPost_with_1Category_2Tags();

            var post = await _postRepo.GetAsync(1, EPostType.BlogPost);

            Assert.NotNull(post.Category);
            Assert.True(post.PostTags.Count() == 2);
            Assert.True(post.PostTags.ToList()[0].Tag.Id == 1);
        }

        [Fact]
        public async void GetPost_By_Slug_And_Date_Will_Return_Null_If_Not_Found()
        {
            var blogPost = await _postRepo.GetAsync(POST_SLUG, 2016, 12, 31);

            Assert.Null(blogPost);
        }

        [Fact]
        public async void GetPost_By_Slug_And_Date_Will_Return_BlogPost_If_Found()
        {
            Seed_1BlogPost_with_1Category_2Tags();

            var blogPost = await _postRepo.GetAsync(POST_SLUG, 2017, 1, 1);

            Assert.Equal(EPostType.BlogPost, blogPost.Type);
        }

        [Fact]
        public async void GetPostList_By_BlogPosts_Returns_Only_Published_Posts()
        {
            Seed_N_BlogPosts(11);

            var query = new PostListQuery(EPostListQueryType.BlogPosts)
            {
                PageIndex = 1,
                PageSize = 10,
            };

            var list = await _postRepo.GetListAsync(query);

            Assert.Equal(6, list.posts.Count);      
            Assert.Equal(6, list.totalCount);
            var tags = list.posts[0].PostTags;
        }

        [Fact]
        public async void GetPostList_By_Drafts_Returns_All_Drafts()
        {
            Seed_N_BlogPosts(23);

            var query = new PostListQuery(EPostListQueryType.BlogDrafts);            

            var list = await _postRepo.GetListAsync(query);

            Assert.Equal(11, list.posts.Count);      
            Assert.Equal(11, list.totalCount);
        }

        [Fact]
        public async void GetPostList_By_Category_Returns_Posts_For_Category()
        {
            Seed_N_BlogPosts(11);

            var query = new PostListQuery(EPostListQueryType.BlogPostsByCategory)
            {
                CategorySlug = CAT_SLUG,
                PageIndex = 1,
                PageSize = 10,
            };

            var list = await _postRepo.GetListAsync(query);

            Assert.Equal(6, list.posts.Count);
            Assert.Equal(6, list.totalCount);
        }

        [Theory]
        [InlineData(TAG1_SLUG, 6)]
        [InlineData(TAG2_SLUG, 0)]
        public async void GetPostList_By_Tag_Returns_Posts_For_Tag(string slug, int expectedPostCount)
        {
            Seed_N_BlogPosts(11);

            var query = new PostListQuery(EPostListQueryType.BlogPostsByTag)
            {
                TagSlug = slug,
                PageIndex = 1,
                PageSize = 10,
            };

            var list = await _postRepo.GetListAsync(query);

            Assert.Equal(expectedPostCount, list.posts.Count);
            Assert.Equal(expectedPostCount, list.totalCount);
        }

        [Fact]
        public async void GetPostList_By_Number_Returns_All_Posts_Regardless_Status()
        {
            Seed_N_BlogPosts(23);

            var query = new PostListQuery(EPostListQueryType.BlogPostsByNumber) { PageSize = int.MaxValue };
            var list = await _postRepo.GetListAsync(query);

            Assert.Equal(23, list.posts.Count);
            Assert.Equal(23, list.totalCount);
        }

        [Fact]
        public async void CreatePost_Will_Create_Its_Category_And_Tags_Automatically()
        {
            Seed_1User();
            var cat = new Category { Slug = "tech", Title = "Technology" };
            var tag1 = new Tag { Slug = "aspnet", Title = "ASP.NET" };
            var tag2 = new Tag { Slug = "cs", Title = "C#" };
            var post = new Post
            {
                Category = cat,
                Body = "A post body.",
                UserId = Actor.ADMIN_ID,
                UpdatedOn = new DateTimeOffset(new DateTime(2017, 01, 01), new TimeSpan(-7, 0, 0)),
                Title = "Hello World",
                Slug = "hello-world",
                Type = EPostType.BlogPost,
                Status = EPostStatus.Published,
            };
            post.PostTags = new List<PostTag> {
                    new PostTag { Post = post, Tag = tag1 },
                    new PostTag { Post = post, Tag = tag2 },
                };

            await _postRepo.CreateAsync(post);

            var postAgain = _db.Set<Post>().Include(p => p.PostTags).Single(p => p.Id == post.Id);
            Assert.Equal(cat.Id, postAgain.CategoryId);
            Assert.Equal(tag1.Id, postAgain.PostTags.ToList()[0].TagId);
            Assert.Equal(tag2.Id, postAgain.PostTags.ToList()[1].TagId);
        }

        [Fact]
        public async void CreatePost_With_Existing_Tags()
        {
            Seed_1BlogPost_with_1Category_2Tags();
            var post = new Post
            {
                Body = "A post body.",
                UserId = Actor.ADMIN_ID,
                UpdatedOn = new DateTimeOffset(new DateTime(2017, 01, 01), new TimeSpan(-7, 0, 0)),
                Title = "Hello World",
                Slug = "hello-world",
                Type = EPostType.BlogPost,
                Status = EPostStatus.Published,
            };
            var tag1 = _db.Set<Tag>().Single(t => t.Slug == TAG1_SLUG);
            var tag2 = _db.Set<Tag>().Single(t => t.Slug == TAG2_SLUG);
            post.PostTags = new List<PostTag> {
                    new PostTag { Post = post, Tag = tag1 },
                    new PostTag { Post = post, Tag = tag2 },
                };

            await _postRepo.CreateAsync(post);

            var postAgain = _db.Set<Post>().Include(p => p.PostTags).Single(p => p.Id == post.Id);
            Assert.Equal(tag1.Id, postAgain.PostTags.ToList()[0].TagId);
            Assert.Equal(tag2.Id, postAgain.PostTags.ToList()[1].TagId);
        }

        [Fact]
        public async void UpdatePost_With_Tags_Updated()
        {
            Seed_1BlogPost_with_1Category_2Tags();
            var tagRepo = new SqlTagRepository(_db);
            var tagJava = await tagRepo.CreateAsync(new Tag { Title = "Java", Slug = "java" });
            List<string> tagTitles = new List<string> { TAG2_TITLE, "Java" };
            var post = await _postRepo.GetAsync(1, EPostType.BlogPost);

            List<string> tagTitlesCurrent = post.PostTags.Select(pt => pt.Tag.Title).ToList();
            var tagsToRemove = tagTitlesCurrent.Except(tagTitles);
            foreach (var t in tagsToRemove)
            {
                post.PostTags.Remove(post.PostTags.Single(pt => pt.Tag.Title == t));
            }
            post.PostTags.Add(new PostTag { Post = post, Tag = tagJava });

            await _postRepo.UpdateAsync(post);

            var postAgain = await _postRepo.GetAsync(1, EPostType.BlogPost);
            Assert.Equal(2, postAgain.PostTags.Count());
            Assert.True(post.PostTags.ToList()[0].Tag.Slug == "cs");
            Assert.True(post.PostTags.ToList()[1].Tag.Slug == "java");
        }

        [Fact]
        public async void UpdatePost_Can_Add_None_Tracked_Tag()
        {
            Seed_1BlogPost_with_1Category_2Tags();
            var tagRepo = new SqlTagRepository(_db);
            var tag = await tagRepo.CreateAsync(new Tag { Title = "Java", Slug = "java" });

            var post = _db.Set<Post>().Include(p => p.PostTags).Single(p => p.Slug == POST_SLUG);
            post.PostTags.Add(new PostTag { Post = post, Tag = tag });
            await _postRepo.UpdateAsync(post);

            var postAgain = _db.Set<Post>().Include(p => p.PostTags).Single(p => p.Id == post.Id);
            Assert.Equal(3, postAgain.PostTags.Count());
        }

        [Fact]
        public async void UpdatePost_With_A_New_Category()
        {
            Seed_1BlogPost_with_1Category_2Tags();
            var post = await _db.Set<Post>().SingleAsync(p => p.Slug == POST_SLUG);
            Assert.Equal(1, post.Category.Id);
            Assert.Equal(1, post.CategoryId);

            Category newCat = null;

            newCat = new Category { Title = "Fashion", Slug = "fashion" };

            post.Category = newCat;
            _db.SaveChanges();

            var postAgain = _db.Set<Post>().Include(p => p.PostTags).Single(p => p.Id == post.Id);
            Assert.Equal(newCat.Id, postAgain.CategoryId);
            Assert.Equal(2, postAgain.Category.Id);
            Assert.Equal("fashion", postAgain.Category.Slug);
        }

        [Fact]
        public async void DeletePost_Removes_Post_From_Db()
        {
            Seed_1BlogPost_with_1Category_2Tags();

            await _postRepo.DeleteAsync(1);

            Assert.Equal(0, _db.Set<Post>().Count());
        }

        [Fact]
        public async void DeletePost_Throws_Exception_If_Id_Not_Found()
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _postRepo.DeleteAsync(1));
        }
    }

}
