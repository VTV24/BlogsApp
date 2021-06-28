using AutoMapper;
using Fan.Blog.Data;
using Fan.Blog.Enums;
using Fan.Blog.Events;
using Fan.Blog.Helpers;
using Fan.Blog.Models;
using Fan.Blog.Services.Interfaces;
using Fan.Blog.Validators;
using Fan.Exceptions;
using Fan.Helpers;
using Fan.Settings;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("Fan.Blog.Tests")]

namespace Fan.Blog.Services
{
    public class BlogPostService : IBlogPostService
    {
        private readonly IPostRepository postRepository;
        private readonly ISettingService settingService;
        private readonly IImageService imageService;
        private readonly IDistributedCache cache;
        private readonly ILogger<BlogPostService> logger;
        private readonly IMapper mapper;
        private readonly IMediator mediator;

        public BlogPostService(
            ISettingService settingService,
            IImageService imageService,
            IPostRepository postRepository,
            IDistributedCache cache,
            ILogger<BlogPostService> logger,
            IMapper mapper,
            IMediator mediator)
        {
            this.settingService = settingService;
            this.imageService = imageService;
            this.postRepository = postRepository;
            this.cache = cache;
            this.mapper = mapper;
            this.logger = logger;
            this.mediator = mediator;
        }

        public const int DEFAULT_PAGE_SIZE = 10;

        public const int DEFAULT_PAGE_INDEX = 1;

        public const int EXCERPT_WORD_LIMIT = 55;

        public async Task<BlogPost> CreateAsync(BlogPost blogPost)
        {
            if (blogPost == null) throw new ArgumentNullException(nameof(blogPost));
            await blogPost.ValidateTitleAsync();

            var post = await ConvertToPostAsync(blogPost, ECreateOrUpdate.Create);

            await mediator.Publish(new BlogPostBeforeCreate
            {
                CategoryTitle = blogPost.CategoryTitle,
                TagTitles = blogPost.TagTitles
            });

            await postRepository.CreateAsync(post, blogPost.CategoryTitle, blogPost.TagTitles);

            if (blogPost.Status == EPostStatus.Published)
            {
                await RemoveBlogCacheAsync();
            }

            await mediator.Publish(new BlogPostCreated { BlogPost = blogPost });

            return await GetAsync(post.Id);
        }

        public async Task<BlogPost> UpdateAsync(BlogPost blogPost)
        {
            if (blogPost == null || blogPost.Id <= 0) throw new ArgumentException(null, nameof(blogPost));
            await blogPost.ValidateTitleAsync();

            var post = await ConvertToPostAsync(blogPost, ECreateOrUpdate.Update);

            await mediator.Publish(new BlogPostBeforeUpdate
            {
                CategoryTitle = blogPost.CategoryTitle,
                TagTitles = blogPost.TagTitles,
                PostTags = post.PostTags,
            });

            await postRepository.UpdateAsync(post, blogPost.CategoryTitle, blogPost.TagTitles);

            await RemoveBlogCacheAsync();
            await RemoveSinglePostCacheAsync(post);

            await mediator.Publish(new BlogPostUpdated { BlogPost = blogPost });

            return await GetAsync(post.Id);
        }

        public async Task DeleteAsync(int id)
        {
            var post = await GetAsync(id);
            await postRepository.DeleteAsync(id);
            await RemoveBlogCacheAsync();
            await RemoveSinglePostCacheAsync(post);
        }

        public async Task<BlogPost> GetAsync(int id)
        {
            var post = await QueryPostAsync(id);
            return ConvertToBlogPost(post);
        }

        public async Task<BlogPost> GetAsync(string slug, int year, int month, int day)
        {
            Post post = null;
            if (new DateTime(year, month, day).IsWithinDays(100))
            {
                var cacheKey = string.Format(BlogCache.KEY_POST, slug, year, month, day);
                post = await cache.GetAsync(cacheKey, BlogCache.Time_SingplePost, async () =>
                {
                    return await postRepository.GetAsync(slug, year, month, day);
                });
            }
            else
            {
                post = await postRepository.GetAsync(slug, year, month, day);
            }

            if (post == null) throw new FanException(EExceptionType.ResourceNotFound);
            var blogPost = ConvertToBlogPost(post);
            blogPost = await PreRenderAsync(blogPost);
            return blogPost;
        }

        public async Task<BlogPostList> GetListAsync(int pageIndex, int pageSize, bool cacheable = true)
        {
            PostListQuery query = new PostListQuery(EPostListQueryType.BlogPosts)
            {
                PageIndex = (pageIndex <= 0) ? 1 : pageIndex,
                PageSize = pageSize,
            };

            if (query.PageIndex == 1 && cacheable)
            {
                return await cache.GetAsync(BlogCache.KEY_POSTS_INDEX, BlogCache.Time_Posts_Index, async () =>
                {
                    return await QueryPostsAsync(query);
                });
            }

            return await QueryPostsAsync(query);
        }

        public async Task<BlogPostList> GetListForCategoryAsync(string categorySlug, int pageIndex)
        {
            if (categorySlug.IsNullOrEmpty()) throw new FanException("Category does not exist.");

            PostListQuery query = new PostListQuery(EPostListQueryType.BlogPostsByCategory)
            {
                CategorySlug = categorySlug,
                PageIndex = (pageIndex <= 0) ? 1 : pageIndex,
                PageSize = (await settingService.GetSettingsAsync<BlogSettings>()).PostPerPage,
            };

            return await QueryPostsAsync(query);
        }

        public async Task<BlogPostList> GetListForTagAsync(string tagSlug, int pageIndex)
        {
            if (tagSlug.IsNullOrEmpty()) throw new FanException("Tag does not exist.");

            PostListQuery query = new PostListQuery(EPostListQueryType.BlogPostsByTag)
            {
                TagSlug = tagSlug,
                PageIndex = (pageIndex <= 0) ? 1 : pageIndex,
                PageSize = (await settingService.GetSettingsAsync<BlogSettings>()).PostPerPage,
            };

            return await QueryPostsAsync(query);
        }

        public async Task<BlogPostList> GetListForArchive(int? year, int? month, int page = 1)
        {
            if (!year.HasValue) throw new FanException("Year must be provided.");
            var query = new PostListQuery(EPostListQueryType.BlogPostsArchive)
            {
                Year = year.Value,
                Month = month
            };

            return await QueryPostsAsync(query);
        }

        public async Task<BlogPostList> GetListForDraftsAsync()
        {
            PostListQuery query = new PostListQuery(EPostListQueryType.BlogDrafts);

            return await QueryPostsAsync(query);
        }

        public async Task<BlogPostList> GetRecentPostsAsync(int numberOfPosts)
        {
            var query = new PostListQuery(EPostListQueryType.BlogPostsByNumber) { PageSize = numberOfPosts };

            return await QueryPostsAsync(query);
        }

        public async Task<BlogPostList> GetRecentPublishedPostsAsync(int numberOfPosts)
        {
            return await cache.GetAsync(BlogCache.KEY_POSTS_RECENT, BlogCache.Time_Posts_Recent, async () =>
            {
                var query = new PostListQuery(EPostListQueryType.BlogPublishedPostsByNumber)
                {
                    PageSize = numberOfPosts <= 0 ? 1 : numberOfPosts
                };

                return await QueryPostsAsync(query);
            });
        }

        public async Task RemoveBlogCacheAsync()
        {
            await cache.RemoveAsync(BlogCache.KEY_POSTS_INDEX);
            await cache.RemoveAsync(BlogCache.KEY_POSTS_RECENT);
            await cache.RemoveAsync(BlogCache.KEY_ALL_CATS);
            await cache.RemoveAsync(BlogCache.KEY_ALL_TAGS);
            await cache.RemoveAsync(BlogCache.KEY_ALL_ARCHIVES);
            await cache.RemoveAsync(BlogCache.KEY_POST_COUNT);
        }

        private async Task<Post> QueryPostAsync(int id)
        {
            var post = await postRepository.GetAsync(id, EPostType.BlogPost);

            if (post == null)
            {
                throw new FanException($"Blog post with id {id} is not found.");
            }

            return post;
        }

        private async Task<BlogPostList> QueryPostsAsync(PostListQuery query)
        {
            var (posts, totalCount) = await postRepository.GetListAsync(query);

            var blogPostList = new BlogPostList
            {
                TotalPostCount = totalCount
            };
            foreach (var post in posts)
            {
                var blogPost = ConvertToBlogPost(post);
                blogPost = await PreRenderAsync(blogPost);
                blogPostList.Posts.Add(blogPost);
            }

            return blogPostList;
        }

        private async Task<Post> ConvertToPostAsync(BlogPost blogPost, ECreateOrUpdate createOrUpdate)
        {
            var post = (createOrUpdate == ECreateOrUpdate.Create) ? new Post() : await QueryPostAsync(blogPost.Id);

            if (createOrUpdate == ECreateOrUpdate.Create)
            {
                post.CreatedOn = (blogPost.CreatedOn <= DateTimeOffset.MinValue) ? DateTimeOffset.UtcNow : blogPost.CreatedOn.ToUniversalTime();
            }
            else
            {
                var coreSettings = await settingService.GetSettingsAsync<CoreSettings>();
                var postCreatedOnLocal = post.CreatedOn.ToLocalTime(coreSettings.TimeZoneId);

                if (!postCreatedOnLocal.YearMonthDayEquals(blogPost.CreatedOn))
                    post.CreatedOn = (blogPost.CreatedOn <= DateTimeOffset.MinValue) ? post.CreatedOn : blogPost.CreatedOn.ToUniversalTime();
            }

            if (blogPost.Status == EPostStatus.Draft) post.UpdatedOn = DateTimeOffset.UtcNow;
            else post.UpdatedOn = null;

            if (blogPost.Status == EPostStatus.Draft && blogPost.Title.IsNullOrEmpty())
                post.Slug = null;
            else
                post.Slug = await GetBlogPostSlugAsync(blogPost.Slug.IsNullOrEmpty() ? blogPost.Title : blogPost.Slug,
                                                       post.CreatedOn, createOrUpdate, blogPost.Id);

            post.Title = blogPost.Title;

            post.Body = blogPost.Body.IsNullOrWhiteSpace() ? null : blogPost.Body;
            post.Excerpt = blogPost.Excerpt.IsNullOrWhiteSpace() ? null : blogPost.Excerpt;
            post.UserId = blogPost.UserId;

            post.Status = blogPost.Status;
            post.CommentStatus = blogPost.CommentStatus;

            post.CategoryId = blogPost.CategoryId;

            logger.LogDebug(createOrUpdate + " {@Post}", post);
            return post;
        }

        private BlogPost ConvertToBlogPost(Post post)
        {
            var blogPost = mapper.Map<Post, BlogPost>(post);

            blogPost.Title = WebUtility.HtmlDecode(blogPost.Title);

            blogPost.Excerpt = post.Excerpt.IsNullOrEmpty() ? Util.GetExcerpt(post.Body, EXCERPT_WORD_LIMIT) : post.Excerpt;

            blogPost.CategoryTitle = post.Category?.Title;

            foreach (var postTag in post.PostTags)
            {
                blogPost.Tags.Add(postTag.Tag);
                blogPost.TagTitles.Add(postTag.Tag.Title);
            }

            blogPost.ViewCount = post.ViewCount;

            logger.LogDebug("Show {@BlogPost}", blogPost);
            return blogPost;
        }

        internal async Task<string> GetBlogPostSlugAsync(string input, DateTimeOffset createdOn, ECreateOrUpdate createOrUpdate, int blogPostId)
        {
            var slug = Util.Slugify(input, maxlen: PostTitleValidator.TITLE_MAXLEN, randomCharCountOnEmpty: 8);

            int i = 2;
            if (createOrUpdate == ECreateOrUpdate.Create)
            {
                while (await postRepository.GetAsync(slug, createdOn.Year, createdOn.Month, createdOn.Day) != null)
                {
                    slug = Util.UniquefySlug(slug, ref i);
                }
            }
            else
            {
                var p = await postRepository.GetAsync(slug, createdOn.Year, createdOn.Month, createdOn.Day);
                while (p != null && p.Id != blogPostId)
                {
                    slug = Util.UniquefySlug(slug, ref i);
                    p = await postRepository.GetAsync(slug, createdOn.Year, createdOn.Month, createdOn.Day);
                }
            }

            return slug;
        }

        private async Task RemoveSinglePostCacheAsync(Post post)
        {
            var cacheKey = string.Format(BlogCache.KEY_POST, post.Slug, post.CreatedOn.Year, post.CreatedOn.Month, post.CreatedOn.Day);
            await cache.RemoveAsync(cacheKey);
        }

        private async Task<BlogPost> PreRenderAsync(BlogPost blogPost)
        {
            if (blogPost == null) return blogPost;

            blogPost.Body = OembedParser.Parse(blogPost.Body);
            blogPost.Body = await imageService.ProcessResponsiveImageAsync(blogPost.Body);

            return blogPost;
        }
    }
}
