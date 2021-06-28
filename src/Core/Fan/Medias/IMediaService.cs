using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Fan.Medias
{
    public interface IMediaService
    {
        Task DeleteMediaAsync(int id);

        Task<bool> ExistsAsync(Expression<Func<Media, bool>> predicate);

        Task<IEnumerable<Media>> FindAsync(Expression<Func<Media, bool>> predicate);

        Task<Media> GetMediaAsync(int id);

        Task<Media> GetMediaAsync(string fileName, int year, int month);

        Task<(List<Media> medias, int count)> GetMediasAsync(EMediaType mediaType, int pageNumber, int pageSize);

        Task<Media> UpdateMediaAsync(int id,
            string title,
            string caption,
            string alt,
            string description);

        Task<Media> UploadImageAsync(Stream source,
            List<ImageResizeInfo> resizes,
            string fileName,
            string contentType,
            string title,
            DateTimeOffset uploadedOn,
            EAppType appType,
            int userId,
            EUploadedFrom uploadFrom = EUploadedFrom.Browser);
    }
}
