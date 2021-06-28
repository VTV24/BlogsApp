using Fan.Blog.Data;
using Fan.Blog.Events;
using Fan.Blog.Helpers;
using Fan.Blog.Models;
using Fan.Blog.Services.Interfaces;
using Fan.Exceptions;
using Fan.Helpers;
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
    public class TagService : ITagService,
                              INotificationHandler<BlogPostBeforeCreate>,
                              INotificationHandler<BlogPostBeforeUpdate>
    {
        private readonly ITagRepository tagRepository;
        private readonly IDistributedCache cache;
        private readonly ILogger<TagService> logger;

        public TagService(ITagRepository tagRepository,
            IDistributedCache cache,
            ILogger<TagService> logger)
        {
            this.tagRepository = tagRepository;
            this.cache = cache;
            this.logger = logger;
        }

        public const int TITLE_MAXLEN = 24;

        public const int SLUG_MAXLEN = 24;

        public async Task<Tag> GetAsync(int id)
        {
            var tags = await GetAllAsync();
            var tag = tags.SingleOrDefault(c => c.Id == id);
            if (tag == null)
            {
                throw new FanException(EExceptionType.ResourceNotFound,
                    $"Tag with id {id} is not found.");
            }

            return tag;
        }

        public async Task<Tag> GetBySlugAsync(string slug)
        {
            if (slug.IsNullOrEmpty())
                throw new FanException(EExceptionType.ResourceNotFound, "Tag does not exist.");

            var tags = await GetAllAsync();
            var tag = tags.SingleOrDefault(c => c.Slug.Equals(slug, StringComparison.CurrentCultureIgnoreCase));
            if (tag == null)
            {
                throw new FanException(EExceptionType.ResourceNotFound, $"Tag '{slug}' does not exist.");
            }

            return tag;
        }

        public async Task<Tag> GetByTitleAsync(string title)
        {
            if (title.IsNullOrEmpty()) throw new FanException("Tag does not exist.");

            var tags = await GetAllAsync();
            var tag = tags.SingleOrDefault(c => c.Title.Equals(title, StringComparison.CurrentCultureIgnoreCase));
            if (tag == null)
            {
                throw new FanException($"Tag with title '{title}' does not exist.");
            }

            return tag;
        }

        public async Task<List<Tag>> GetAllAsync()
        {
            return await cache.GetAsync(BlogCache.KEY_ALL_TAGS, BlogCache.Time_AllTags, async () =>
            {
                return await tagRepository.GetListAsync();
            });
        }

        public async Task<Tag> CreateAsync(Tag tag)
        {
            if (tag == null || tag.Title.IsNullOrEmpty())
            {
                throw new FanException($"Invalid tag to create.");
            }

            tag.Title = PrepareTitle(tag.Title);

            var allTags = await GetAllAsync();
            if (allTags.Any(t => t.Title.Equals(tag.Title, StringComparison.CurrentCultureIgnoreCase)))
            {
                throw new FanException($"'{tag.Title}' already exists.");
            }

            tag.Slug = BlogUtil.SlugifyTaxonomy(tag.Title, SLUG_MAXLEN, allTags.Select(c => c.Slug));
            tag.Description = Util.CleanHtml(tag.Description);
            tag.Count = tag.Count;

            tag = await tagRepository.CreateAsync(tag);

            await cache.RemoveAsync(BlogCache.KEY_ALL_TAGS);
            await cache.RemoveAsync(BlogCache.KEY_POSTS_INDEX);

            logger.LogDebug("Created {@Tag}", tag);
            return tag;
        }

        public async Task<Tag> UpdateAsync(Tag tag)
        {
            if (tag == null || tag.Id <= 0 || tag.Title.IsNullOrEmpty())
            {
                throw new FanException($"Invalid tag to update.");
            }

            tag.Title = PrepareTitle(tag.Title);

            var allTags = await GetAllAsync();
            allTags.RemoveAll(t => t.Id == tag.Id);
            if (allTags.Any(t => t.Title.Equals(tag.Title, StringComparison.CurrentCultureIgnoreCase)))
            {
                throw new FanException($"'{tag.Title}' already exists.");
            }

            var entity = await tagRepository.GetAsync(tag.Id);
            entity.Title = tag.Title;
            entity.Slug = BlogUtil.SlugifyTaxonomy(tag.Title, SLUG_MAXLEN, allTags.Select(c => c.Slug));
            entity.Description = Util.CleanHtml(tag.Description);
            entity.Count = tag.Count;

            await tagRepository.UpdateAsync(entity);

            await cache.RemoveAsync(BlogCache.KEY_ALL_TAGS);
            await cache.RemoveAsync(BlogCache.KEY_POSTS_INDEX);

            logger.LogDebug("Updated {@Tag}", entity);
            return entity;
        }

        public async Task DeleteAsync(int id)
        {
            await tagRepository.DeleteAsync(id);
            await cache.RemoveAsync(BlogCache.KEY_ALL_TAGS);
            await cache.RemoveAsync(BlogCache.KEY_POSTS_INDEX);
        }

        public async Task Handle(BlogPostBeforeCreate notification, CancellationToken cancellationToken)
        {
            if (notification.TagTitles == null || notification.TagTitles.Count <= 0) return;

            var distinctTitles = notification.TagTitles.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct();
            var allTags = await GetAllAsync();

            foreach (var title in distinctTitles)
            {
                var tag = allTags.FirstOrDefault(t => t.Title.Equals(title, StringComparison.CurrentCultureIgnoreCase));
                if (tag == null)
                {
                    tag = await CreateAsync(new Tag { Title = title });
                }
            }
        }

        public async Task Handle(BlogPostBeforeUpdate notification, CancellationToken cancellationToken)
        {
            if (notification.TagTitles == null || notification.TagTitles.Count <= 0 || notification.PostTags == null) return;

            var currentTitles = notification.PostTags.Select(pt => pt.Tag.Title);
            var distinctTitles = notification.TagTitles.Except(currentTitles);
            var allTags = await GetAllAsync();

            foreach (var title in distinctTitles)
            {
                var tag = allTags.FirstOrDefault(t => t.Title.Equals(title, StringComparison.CurrentCultureIgnoreCase));
                if (tag == null)
                {
                    tag = await CreateAsync(new Tag { Title = title });
                }
            }
        }

        private string PrepareTitle(string title)
        {
            title = Util.CleanHtml(title);
            title = title.Length > TITLE_MAXLEN ? title.Substring(0, TITLE_MAXLEN) : title;
            return title;
        }
    }
}
