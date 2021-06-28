using Fan.Blog.Data;
using Fan.Blog.Events;
using Fan.Blog.Helpers;
using Fan.Blog.Models;
using Fan.Blog.Services.Interfaces;
using Fan.Exceptions;
using Fan.Helpers;
using Fan.Navigation;
using Fan.Settings;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Fan.Blog.Services
{
    public class CategoryService : ICategoryService,
                                   INavProvider,
                                   INotificationHandler<BlogPostBeforeCreate>,
                                   INotificationHandler<BlogPostBeforeUpdate>
    {
        private readonly ICategoryRepository categoryRepository;
        private readonly ISettingService settingService;
        private readonly IMediator mediator;
        private readonly IDistributedCache cache;
        private readonly ILogger<CategoryService> logger;

        public CategoryService(ICategoryRepository categoryRepository,
                               ISettingService settingService,
                               IMediator mediator,
                               IDistributedCache cache,
                               ILogger<CategoryService> logger)
        {
            this.categoryRepository = categoryRepository;
            this.settingService = settingService;
            this.mediator = mediator;
            this.cache = cache;
            this.logger = logger;
        }

        public const int TITLE_MAXLEN = 24;

        public const int SLUG_MAXLEN = 24;

        public async Task<Category> GetAsync(int id)
        {
            var cats = await GetAllAsync();
            var cat = cats.SingleOrDefault(c => c.Id == id);
            if (cat == null)
            {
                throw new FanException(EExceptionType.ResourceNotFound,
                    $"Category with id {id} is not found.");
            }

            return cat;
        }

        public async Task<Category> GetAsync(string slug)
        {
            if (slug.IsNullOrEmpty())
                throw new FanException(EExceptionType.ResourceNotFound, "Category does not exist.");

            var cats = await GetAllAsync();
            var cat = cats.SingleOrDefault(c => c.Slug.Equals(slug, StringComparison.CurrentCultureIgnoreCase));
            if (cat == null)
            {
                throw new FanException(EExceptionType.ResourceNotFound, $"Category '{slug}' does not exist.");
            }

            return cat;
        }

        public async Task<List<Category>> GetAllAsync()
        {
            return await cache.GetAsync(BlogCache.KEY_ALL_CATS, BlogCache.Time_AllCats, async () =>
            {
                return await categoryRepository.GetListAsync();
            });
        }

        public async Task SetDefaultAsync(int id)
        {
            await settingService.UpsertSettingsAsync(new BlogSettings
            {
                DefaultCategoryId = id,
            });
        }

        public async Task<Category> CreateAsync(string title, string description = null)
        {
            if (title.IsNullOrEmpty())
            {
                throw new FanException($"Category title cannot be empty.");
            }

            title = PrepareTitle(title);

            var allCats = await GetAllAsync();
            if (allCats.Any(t => t.Title.Equals(title, StringComparison.CurrentCultureIgnoreCase)))
            {
                throw new FanException($"'{title}' already exists.");
            }

            var category = new Category
            {
                Title = title,
                Slug = BlogUtil.SlugifyTaxonomy(title, SLUG_MAXLEN, allCats.Select(c => c.Slug)),
                Description = Util.CleanHtml(description),
                Count = 0,
            };

            category = await categoryRepository.CreateAsync(category);

            await cache.RemoveAsync(BlogCache.KEY_ALL_CATS);
            await cache.RemoveAsync(BlogCache.KEY_POSTS_INDEX);

            logger.LogDebug("Created {@Category}", category);
            return category;
        }

        public async Task<Category> UpdateAsync(Category category)
        {
            if (category == null || category.Id <= 0 || category.Title.IsNullOrEmpty())
            {
                throw new FanException($"Invalid category to update.");
            }

            category.Title = PrepareTitle(category.Title);

            var allCats = await GetAllAsync();
            allCats.RemoveAll(c => c.Id == category.Id);
            if (allCats.Any(c => c.Title.Equals(category.Title, StringComparison.CurrentCultureIgnoreCase)))
            {
                throw new FanException($"'{category.Title}' already exists.");
            }

            var entity = await categoryRepository.GetAsync(category.Id);
            entity.Title = category.Title;
            entity.Slug = BlogUtil.SlugifyTaxonomy(category.Title, SLUG_MAXLEN, allCats.Select(c => c.Slug));
            entity.Description = Util.CleanHtml(category.Description);
            entity.Count = category.Count;

            await categoryRepository.UpdateAsync(category);

            await cache.RemoveAsync(BlogCache.KEY_ALL_CATS);
            await cache.RemoveAsync(BlogCache.KEY_POSTS_INDEX);

            await mediator.Publish(new NavUpdated { Id = category.Id, Type = ENavType.BlogCategory });

            logger.LogDebug("Updated {@Category}", entity);
            return entity;
        }

        public async Task DeleteAsync(int id)
        {
            var blogSettings = await settingService.GetSettingsAsync<BlogSettings>();

            if (id == blogSettings.DefaultCategoryId)
            {
                throw new FanException("Default category cannot be deleted.");
            }

            await categoryRepository.DeleteAsync(id, blogSettings.DefaultCategoryId);

            await cache.RemoveAsync(BlogCache.KEY_ALL_CATS);
            await cache.RemoveAsync(BlogCache.KEY_POSTS_INDEX);

            await mediator.Publish(new NavDeleted { Id = id, Type = ENavType.BlogCategory });
        }

        public bool CanProvideNav(ENavType type) => type == ENavType.BlogCategory;

        public async Task<string> GetNavUrlAsync(int id)
        {
            var cat = await GetAsync(id);
            return BlogRoutes.GetCategoryRelativeLink(cat.Slug);
        }

        public async Task Handle(BlogPostBeforeCreate notification, CancellationToken cancellationToken)
        {
            await HandleNewCatAsync(notification.CategoryTitle);
        }

        public async Task Handle(BlogPostBeforeUpdate notification, CancellationToken cancellationToken)
        {
            await HandleNewCatAsync(notification.CategoryTitle);
        }

        private async Task HandleNewCatAsync(string categoryTitle)
        {
            if (categoryTitle.IsNullOrEmpty()) return;

            var cat = (await GetAllAsync())
                   .SingleOrDefault(c => c.Title.Equals(categoryTitle, StringComparison.CurrentCultureIgnoreCase));

            if (cat == null)
                await CreateAsync(categoryTitle);
        }

        private string PrepareTitle(string title)
        {
            title = Util.CleanHtml(title);
            title = title.Length > TITLE_MAXLEN ? title.Substring(0, TITLE_MAXLEN) : title;
            return title;
        }
    }
}
