using AutoMapper;
using Fan.Blog.Data;
using Fan.Blog.Enums;
using Fan.Blog.Helpers;
using Fan.Blog.Models;
using Fan.Blog.Services.Interfaces;
using Fan.Blog.Validators;
using Fan.Exceptions;
using Fan.Helpers;
using Fan.Navigation;
using Fan.Settings;
using Markdig;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("Fan.Blog.Tests")]

namespace Fan.Blog.Services
{
    public class PageService : IPageService, INavProvider
    {
        private readonly ISettingService settingService;
        private readonly IPostRepository postRepository;
        private readonly IDistributedCache cache;
        private readonly ILogger<PageService> logger;
        private readonly IMapper mapper;
        private readonly IMediator mediator;

        public PageService(
            ISettingService settingService,
            IPostRepository postRepository,
            IDistributedCache cache,
            ILogger<PageService> logger,
            IMapper mapper,
            IMediator mediator)
        {
            this.settingService = settingService;
            this.postRepository = postRepository;
            this.cache = cache;
            this.logger = logger;
            this.mapper = mapper;
            this.mediator = mediator;
        }

        public const string DUPLICATE_TITLE_MSG = "A page with same title exists, please choose a different one.";
        public const string DUPLICATE_SLUG_MSG = "Page slug generated from your title conflicts with another page, please choose a different title.";
        public const string RESERVED_SLUG_MSG = "Page title conflicts with reserved URL '{0}', please choose a different one.";

        public static string[] Reserved_Slugs = new string[]
        {
            "admin", "account", "api", "app", "apps", "assets",
            "blog", "blogs",
            "denied",
            "feed", "feeds", "forum", "forums",
            "image", "images", "img",
            "login", "logout",
            "media",
            "plugin", "plugins", "post", "posts", "preview",
            "register", "rsd",
            "setup", "static",
            "theme", "themes",
            "user", "users",
            "widget", "widgets",
        };

        public const string DOUBLE_BRACKETS = @"\[.*?\]]";

        public async Task<Page> CreateAsync(Page page)
        {
            await EnsurePageTitleAsync(page);

            var post = await ConvertToPostAsync(page, ECreateOrUpdate.Create);

            await postRepository.CreateAsync(post);

            return await GetAsync(post.Id);
        }

        public async Task<Page> UpdateAsync(Page page)
        {
            await EnsurePageTitleAsync(page);

            var origPost = await GetAsync(page.Id);

            var post = await ConvertToPostAsync(page, ECreateOrUpdate.Update);

            await postRepository.UpdateAsync(post);

            var key = await GetCacheKeyAsync(page.Id, origPost);
            await cache.RemoveAsync(key);

            await mediator.Publish(new NavUpdated
            {
                Id = page.Id,
                Type = ENavType.Page,
                IsDraft = post.Status == EPostStatus.Draft
            });

            return await GetAsync(post.Id);
        }

        public async Task DeleteAsync(int id)
        {
            var key = await GetCacheKeyAsync(id);

            await postRepository.DeleteAsync(id);

            await cache.RemoveAsync(key);

            await mediator.Publish(new NavDeleted { Id = id, Type = ENavType.Page });
        }

        public async Task<Page> GetAsync(int id)
        {
            var post = await QueryPostAsync(id);
            var page = mapper.Map<Post, Page>(post);

            if (page.IsParent)
            {
                var childPosts = await postRepository.FindAsync(p => p.Type == EPostType.Page && p.ParentId == page.Id);
                if (childPosts != null)
                {
                    foreach (var childPost in childPosts)
                    {
                        page.Children.Add(mapper.Map<Post, Page>(childPost));
                    }
                }
            }
            else
            {
                var parentPost = await QueryPostAsync(page.ParentId.Value);
                var parent = mapper.Map<Post, Page>(parentPost);
                var childPosts = await postRepository.FindAsync(p => p.Type == EPostType.Page && p.ParentId == parent.Id);
                if (childPosts != null)
                {
                    foreach (var childPost in childPosts)
                    {
                        parent.Children.Add(mapper.Map<Post, Page>(childPost));
                    }
                }

                page.Parent = parent;
            }

            return page;
        }

        public async Task<Page> GetAsync(params string[] slugs)
        {
            if (slugs == null || slugs.Length <= 0)
            {
                throw new ArgumentNullException(nameof(slugs));
            }

            if (slugs[0] == "preview")
            {
                throw new FanException(EExceptionType.ResourceNotFound);
            }

            var key = GetCacheKey(WebUtility.UrlEncode(slugs[0]));
            var time = BlogCache.Time_ParentPage;

            if (slugs.Length > 1 && !slugs[1].IsNullOrEmpty())
            {
                key = GetCacheKey(slugs[0], slugs[1]);
                time = BlogCache.Time_ChildPage;
            }

            return await cache.GetAsync(key, time, async () =>
            {
                var parents = await GetParentsAsync(withChildren: true);

                var page = parents.SingleOrDefault(p => p.Slug.Equals(WebUtility.UrlEncode(slugs[0]), StringComparison.CurrentCultureIgnoreCase));
                if (page == null || page.Status == EPostStatus.Draft)
                {
                    throw new FanException(EExceptionType.ResourceNotFound);
                }

                if (page.IsParent && slugs.Length > 1 && !slugs[1].IsNullOrEmpty())
                {
                    var child = page.Children.SingleOrDefault(p => p.Slug.Equals(WebUtility.UrlEncode(slugs[1]), StringComparison.CurrentCultureIgnoreCase));
                    if (child == null || child.Status == EPostStatus.Draft)
                    {
                        throw new FanException(EExceptionType.ResourceNotFound);
                    }

                    page = child;
                }

                return page;
            });
        }

        public async Task<IList<Page>> GetParentsAsync(bool withChildren = false)
        {
            var query = new PostListQuery(withChildren ? EPostListQueryType.PagesWithChildren : EPostListQueryType.Pages);
            var (posts, totalCount) = await postRepository.GetListAsync(query);

            var pages = mapper.Map<IList<Post>, IList<Page>>(posts);

            if (!withChildren) return pages;

            var parents = pages.Where(p => p.IsParent);
            foreach (var parent in parents)
            {
                var children = pages.Where(p => p.ParentId == parent.Id);
                foreach (var child in children)
                {
                    child.Parent = parent;
                    parent.Children.Add(child);
                }
            }

            return parents.ToList();
        }

        public async Task SaveNavAsync(int pageId, string navMd)
        {
            var post = await QueryPostAsync(pageId);
            post.Nav = navMd;
            await postRepository.UpdateAsync(post);

            var key = await GetCacheKeyAsync(pageId, post);
            await cache.RemoveAsync(key);
        }

        public bool CanProvideNav(ENavType type) => type == ENavType.Page;

        public async Task<string> GetNavUrlAsync(int id)
        {
            var page = await GetAsync(id);
            return $"/{page.Slug}";
        }

        private async Task<Post> QueryPostAsync(int id)
        {
            var post = await postRepository.GetAsync(id, EPostType.Page);

            if (post == null)
            {
                throw new FanException(EExceptionType.ResourceNotFound,
                    $"Page with id {id} is not found.");
            }

            return post;
        }

        private async Task<Post> ConvertToPostAsync(Page page, ECreateOrUpdate createOrUpdate)
        {
            var post = (createOrUpdate == ECreateOrUpdate.Create) ? new Post() : await QueryPostAsync(page.Id);
            post.Type = EPostType.Page;

            post.ParentId = page.ParentId;

            var parentSlug = "";
            if (page.ParentId.HasValue && page.ParentId > 0)
            {
                var parent = await GetAsync(page.ParentId.Value);
                parentSlug = parent.Slug;
            }

            if (createOrUpdate == ECreateOrUpdate.Create)
            {
                post.CreatedOn = (page.CreatedOn <= DateTimeOffset.MinValue) ? DateTimeOffset.UtcNow : page.CreatedOn.ToUniversalTime();
            }
            else
            {
                var coreSettings = await settingService.GetSettingsAsync<CoreSettings>();
                var postCreatedOnLocal = post.CreatedOn.ToLocalTime(coreSettings.TimeZoneId);

                if (!postCreatedOnLocal.YearMonthDayEquals(page.CreatedOn))
                    post.CreatedOn = (page.CreatedOn <= DateTimeOffset.MinValue) ? post.CreatedOn : page.CreatedOn.ToUniversalTime();
            }

            post.UpdatedOn = DateTimeOffset.UtcNow;

            var slug = SlugifyPageTitle(page.Title);
            await EnsurePageSlugAsync(slug, page);
            post.Slug = slug;

            post.Title = page.Title;

            post.Body = ParseNavLinks(page.Body, parentSlug.IsNullOrEmpty() ? slug : parentSlug);
            post.BodyMark = WebUtility.HtmlEncode(page.BodyMark);

            post.Excerpt = page.Excerpt;

            post.UserId = page.UserId;

            post.Status = page.Status;
            post.CommentStatus = ECommentStatus.NoComments;

            post.PageLayout = page.PageLayout ?? 1;

            logger.LogDebug(createOrUpdate + " Page: {@Post}", post);
            return post;
        }

        private async Task<string> GetCacheKeyAsync(int pageId, Post post = null)
        {
            post = post ?? await QueryPostAsync(pageId);
            var key = string.Format(BlogCache.KEY_PAGE, post.Slug);

            if (post.ParentId.HasValue && post.ParentId.Value > 0)
            {
                var parentPost = await QueryPostAsync(post.ParentId.Value);
                key = string.Format(BlogCache.KEY_PAGE, parentPost.Slug + "_" + post.Slug);
            }

            return key;
        }

        private string GetCacheKey(string parentSlug, string childSlug = null) =>
            childSlug.IsNullOrEmpty() ?
                string.Format(BlogCache.KEY_PAGE, parentSlug) :
                string.Format(BlogCache.KEY_PAGE, parentSlug + "_" + childSlug);

        internal async Task EnsurePageTitleAsync(Page page)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));

            await page.ValidateTitleAsync();

            if (page.Title.IsNullOrEmpty())
            {
                return;
            }

            if (page.IsParent)
            {
                var parents = await GetParentsAsync();
                if (page.Id > 0)
                {
                    var parent = parents.SingleOrDefault(p => p.Id == page.Id);
                    if (parent != null) parents.Remove(parent);
                }

                if (parents.Any(p => p.Title.Equals(page.Title, StringComparison.InvariantCultureIgnoreCase)))
                {
                    throw new FanException(DUPLICATE_TITLE_MSG);
                }
            }
            else
            {
                var parent = await GetAsync(page.ParentId.Value);
                if (page.Id > 0 && parent.HasChildren)
                {
                    var child = parent.Children.Single(p => p.Id == page.Id);
                    if (child != null) parent.Children.Remove(child);
                }

                if (parent.HasChildren &&
                    parent.Children.Any(p => p.Title.Equals(page.Title, StringComparison.InvariantCultureIgnoreCase)))
                {
                    throw new FanException(DUPLICATE_TITLE_MSG);
                }
            }
        }

        internal async Task EnsurePageSlugAsync(string slug, Page page)
        {
            if (slug.IsNullOrEmpty() || page == null) return;

            if (page.IsParent)
            {
                var parents = await GetParentsAsync();

                if (page.Id > 0)
                {
                    var parent = parents.SingleOrDefault(p => p.Id == page.Id);
                    if (parent != null) parents.Remove(parent);
                }

                if (parents.Any(c => c.Slug == slug))
                {
                    throw new FanException(EExceptionType.DuplicateRecord, DUPLICATE_SLUG_MSG);
                }

                if (Reserved_Slugs.Contains(slug))
                {
                    throw new FanException(EExceptionType.DuplicateRecord, string.Format(RESERVED_SLUG_MSG, slug));
                }
            }
            else
            {
                var parent = await GetAsync(page.ParentId.Value);
                if (page.Id > 0 && parent.HasChildren)
                {
                    var child = parent.Children.Single(p => p.Id == page.Id);
                    if (child != null) parent.Children.Remove(child);
                }

                if (parent.HasChildren &&
                    parent.Children.Any(c => c.Slug == slug))
                {
                    throw new FanException(EExceptionType.DuplicateRecord, DUPLICATE_SLUG_MSG);
                }
            }
        }

        public static string NavMdToHtml(string navMd, string parentSlug)
        {
            if (navMd.IsNullOrEmpty()) return navMd;

            var matches = Regex.Matches(navMd, DOUBLE_BRACKETS);
            foreach (var match in matches)
            {
                var token = match.ToString();
                var text = token.Substring(2, token.Length - 4);

                var slug = Util.Slugify(text);
                if (!parentSlug.IsNullOrEmpty() && parentSlug != slug)
                {
                    slug = $"{parentSlug}/{slug}";
                }

                var link = $"[{text}](/{slug} \"{text}\")";
                navMd = navMd.Replace(match.ToString(), link);
            }

            return Markdown.ToHtml(navMd);
        }

        public static string ParseNavLinks(string body, string parentSlug)
        {
            if (body.IsNullOrEmpty()) return body;

            var matches = Regex.Matches(body, DOUBLE_BRACKETS);
            foreach (var match in matches)
            {
                var token = match.ToString();
                var text = token[2..^2];

                var slug = Util.Slugify(text);
                if (!parentSlug.IsNullOrEmpty() && parentSlug != slug)
                {
                    slug = $"{parentSlug}/{slug}";
                }

                var link = $"[{text}](/{slug} \"{text}\")";
                var linkHtml = Markdown.ToHtml(link);
                if (linkHtml.StartsWith("<p>"))
                {
                    linkHtml = linkHtml.Replace("<p>", "").Replace("</p>", "");
                }
                body = body.Replace(match.ToString(), linkHtml);
            }

            return body;
        }

        public static string SlugifyPageTitle(string title)
        {
            if (title.IsNullOrEmpty()) return title;

            var slug = Util.Slugify(title, maxlen: PostTitleValidator.TITLE_MAXLEN);
            if (slug.IsNullOrEmpty())
            {
                slug = WebUtility.UrlEncode(title);
                if (slug.Length > PostTitleValidator.TITLE_MAXLEN)
                {
                    slug = slug.Substring(0, PostTitleValidator.TITLE_MAXLEN);
                }
            }

            return slug;
        }
    }
}
