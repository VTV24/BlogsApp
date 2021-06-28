using Fan.Blog.Enums;
using Fan.Medias;
using System.IO;
using System.Threading.Tasks;

namespace Fan.Blog.Services.Interfaces
{
    public interface IImageService
    {
        Task DeleteAsync(int mediaId);

        string GetAbsoluteUrl(Media media, EImageSize size);

        Task<Media> UploadAsync(Stream source, int userId, string fileName, string contentType, EUploadedFrom uploadFrom);

        Task<string> ProcessResponsiveImageAsync(string body);
    }
}
