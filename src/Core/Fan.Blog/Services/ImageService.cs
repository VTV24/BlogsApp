﻿using Fan.Blog.Enums;
using Fan.Blog.Services.Interfaces;
using Fan.Exceptions;
using Fan.Helpers;
using Fan.Medias;
using Fan.Settings;
using HtmlAgilityPack;
using Humanizer;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Fan.Blog.Services
{
    public class ImageService : IImageService
    {
        private readonly IMediaService _mediaSvc;
        private readonly IStorageProvider _storageProvider;
        private readonly AppSettings _appSettings;

        public ImageService(IMediaService mediaSvc,
                            IStorageProvider storageProvider,
                            IOptionsSnapshot<AppSettings> appSettings)
        {
            _mediaSvc = mediaSvc;
            _storageProvider = storageProvider;
            _appSettings = appSettings.Value;
        }

        public const string BLOG_APP_NAME = "Blog";

        public static readonly string[] Accepted_Image_Types = { ".jpg", ".jpeg", ".gif", ".png" };

        public static string ValidFileTypesJson = JsonConvert.SerializeObject(Accepted_Image_Types);

        public static readonly long Max_File_Size = (long)(5).Megabytes().Bytes;


        public const string ERR_MSG_FILETYPE = "Only .jpg, .jpeg, .png and .gif are supported.";

        public const string ERR_MSG_FILESIZE = "File cannot be larger than 5MB.";

        public const char IMAGE_PATH_SEPARATOR = '/';

        public const int LARGE_IMG_SIZE = 2400;

        public const int MEDIUM_LARGE_IMG_SIZE = 1800;

        public const int MEDIUM_IMG_SIZE = 1200;

        public const int SMALL_IMG_SIZE = 600;

        public static List<ImageResizeInfo> GetImageResizeList(DateTimeOffset uploadedOn)
        {
            return new List<ImageResizeInfo> {
                new ImageResizeInfo {
                    TargetSize = int.MaxValue,
                    Path = GetImagePath(uploadedOn, EImageSize.Original),
                    PathSeparator = IMAGE_PATH_SEPARATOR,
                },
                new ImageResizeInfo {
                    TargetSize = LARGE_IMG_SIZE,
                    Path = GetImagePath(uploadedOn, EImageSize.Large),
                    PathSeparator = IMAGE_PATH_SEPARATOR,
                },
                new ImageResizeInfo {
                    TargetSize = MEDIUM_LARGE_IMG_SIZE,
                    Path = GetImagePath(uploadedOn, EImageSize.MediumLarge),
                    PathSeparator = IMAGE_PATH_SEPARATOR,
                },
                new ImageResizeInfo {
                    TargetSize = MEDIUM_IMG_SIZE,
                    Path = GetImagePath(uploadedOn, EImageSize.Medium),
                    PathSeparator = IMAGE_PATH_SEPARATOR,
                },
                new ImageResizeInfo {
                    TargetSize = SMALL_IMG_SIZE,
                    Path = GetImagePath(uploadedOn, EImageSize.Small),
                    PathSeparator = IMAGE_PATH_SEPARATOR,
                },
            };
        }

        public static List<ImageResizeInfo> GetImageResizeListForGif(DateTimeOffset uploadedOn)
        {
            return new List<ImageResizeInfo> {
                new ImageResizeInfo {
                    TargetSize = int.MaxValue,
                    Path = GetImagePath(uploadedOn, EImageSize.Original),
                    PathSeparator = IMAGE_PATH_SEPARATOR,
                },
                new ImageResizeInfo {
                    TargetSize = SMALL_IMG_SIZE,
                    Path = GetImagePath(uploadedOn, EImageSize.Small),
                    PathSeparator = IMAGE_PATH_SEPARATOR,
                },
            };
        }

        public static string GetImagePath(DateTimeOffset uploadedOn, EImageSize size)
        {
            var app = BLOG_APP_NAME.ToLowerInvariant();
            var year = uploadedOn.Year.ToString();
            var month = uploadedOn.Month.ToString("d2");
            var sizePath = "";

            switch (size)
            {
                case EImageSize.Large:
                    sizePath = "lg";
                    break;
                case EImageSize.MediumLarge:
                    sizePath = "ml";
                    break;
                case EImageSize.Medium:
                    sizePath = "md";
                    break;
                case EImageSize.Small:
                    sizePath = "sm";
                    break;
                default:
                    sizePath = null;
                    break;
            }

            return size == EImageSize.Original ? $"{app}/{year}/{month}" : $"{app}/{year}/{month}/{sizePath}";
        }

        public async Task DeleteAsync(int mediaId)
        {
            var media = await _mediaSvc.GetMediaAsync(mediaId);
            var resizes = GetImageResizeList(media.UploadedOn);
            var resizeCount = media.ResizeCount;

            await DeleteImageFileAsync(media, EImageSize.Original);
            if (resizeCount == 4)
            {
                await DeleteImageFileAsync(media, EImageSize.Small);
                await DeleteImageFileAsync(media, EImageSize.Medium);
                await DeleteImageFileAsync(media, EImageSize.MediumLarge);
                await DeleteImageFileAsync(media, EImageSize.Large);
            }
            else if (resizeCount == 3)
            {
                await DeleteImageFileAsync(media, EImageSize.Small);
                await DeleteImageFileAsync(media, EImageSize.Medium);
                await DeleteImageFileAsync(media, EImageSize.MediumLarge);
            }
            else if (resizeCount == 2)
            {
                await DeleteImageFileAsync(media, EImageSize.Small);
                await DeleteImageFileAsync(media, EImageSize.Medium);
            }
            else if (resizeCount == 1)
            {
                await DeleteImageFileAsync(media, EImageSize.Small);
            }

            await _mediaSvc.DeleteMediaAsync(mediaId);
        }

        public string GetAbsoluteUrl(Media media, EImageSize size)
        {
            var endpoint = _storageProvider.StorageEndpoint;
            var container = endpoint.EndsWith('/') ? _appSettings.MediaContainerName : $"/{_appSettings.MediaContainerName}";

            if ((size == EImageSize.Original || media.ResizeCount <= 0) ||
                (media.ResizeCount == 1 && size != EImageSize.Small) ||
                (media.ResizeCount == 2 && size == EImageSize.MediumLarge) ||
                (media.ResizeCount == 2 && size == EImageSize.Large) ||
                (media.ResizeCount == 3 && size == EImageSize.Large))
            {
                size = EImageSize.Original;
            }

            var imagePath = GetImagePath(media.UploadedOn, size);
            var fileName = media.FileName;

            return $"{endpoint}{container}/{imagePath}/{fileName}";
        }

        public async Task<Media> UploadAsync(Stream source, int userId, string fileName, string contentType,
            EUploadedFrom uploadFrom)
        {
            var ext = Path.GetExtension(fileName).ToLower();
            var ctype = "." + contentType.Substring(contentType.LastIndexOf("/") + 1).ToLower();
            if (ext.IsNullOrEmpty() || !Accepted_Image_Types.Contains(ext) || !Accepted_Image_Types.Contains(ctype))
            {
                throw new NotSupportedException(ERR_MSG_FILETYPE);
            }

            if (source.Length > Max_File_Size)
            {
                throw new FanException(ERR_MSG_FILESIZE);
            }

            var uploadedOn = DateTimeOffset.UtcNow;

            var (fileNameSlugged, title) = ProcessFileName(fileName, uploadFrom);

            var uniqueFileName = await GetUniqueFileNameAsync(fileNameSlugged, uploadedOn);

            var resizes = contentType.Equals("image/gif") ?
                GetImageResizeListForGif(uploadedOn) : GetImageResizeList(uploadedOn);

            return await _mediaSvc.UploadImageAsync(source, resizes, uniqueFileName, contentType, title,
                uploadedOn, EAppType.Blog, userId, uploadFrom);
        }

        public async Task<string> ProcessResponsiveImageAsync(string body)
        {
            if (body.IsNullOrEmpty()) return body;

            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(body);

                var imgNodes = doc.DocumentNode.SelectNodes("//img");
                if (imgNodes == null || imgNodes.Count <= 0) return body;

                bool changed = false;
                foreach (var imgNode in imgNodes)
                {
                    var imgNodeNew = await GetResponsiveImgNodeAsync(imgNode);
                    if (imgNodeNew != null)
                    {
                        imgNode.ParentNode.ReplaceChild(imgNodeNew, imgNode);
                        changed = true;
                    }
                }

                return changed ? doc.DocumentNode.OuterHtml : body;
            }
            catch (Exception)
            {
                return body;
            }
        }

        private async Task<HtmlNode> GetResponsiveImgNodeAsync(HtmlNode imgNode)
        {
            var src = imgNode.Attributes["src"]?.Value;
            if (src.IsNullOrEmpty()) return null;

            var strAppType = $"{EAppType.Blog.ToString().ToLower()}/";
            var idxLastSlash = src.LastIndexOf('/');
            var fileName = src.Substring(idxLastSlash + 1, src.Length - idxLastSlash - 1);
            if (fileName.IsNullOrEmpty()) return null;

            var idxAppType = src.IndexOf(strAppType) + strAppType.Length;
            var strTimeSize = src.Substring(idxAppType, idxLastSlash - idxAppType);
            var year = Convert.ToInt32(strTimeSize.Substring(0, 4));
            var month = Convert.ToInt32(strTimeSize.Substring(5, 2));

            var media = await _mediaSvc.GetMediaAsync(fileName, year, month);
            var resizeCount = media.ResizeCount;
            if (resizeCount <= 0) return null;

            var srcset = "";
            if (resizeCount == 1)
            {
                srcset = $"{GetAbsoluteUrl(media, EImageSize.Small)} {SMALL_IMG_SIZE}w, " +
                         $"{GetAbsoluteUrl(media, EImageSize.Original)} {media.Width}w";
            }
            else if (resizeCount == 2)
            {
                srcset = $"{GetAbsoluteUrl(media, EImageSize.Small)} {SMALL_IMG_SIZE}w, " +
                         $"{GetAbsoluteUrl(media, EImageSize.Medium)} {MEDIUM_IMG_SIZE}w, " +
                         $"{GetAbsoluteUrl(media, EImageSize.Original)} {media.Width}w";
            }
            else if (resizeCount == 3)
            {
                srcset = $"{GetAbsoluteUrl(media, EImageSize.Small)} {SMALL_IMG_SIZE}w, " +
                         $"{GetAbsoluteUrl(media, EImageSize.Medium)} {MEDIUM_IMG_SIZE}w, " +
                         $"{GetAbsoluteUrl(media, EImageSize.MediumLarge)} 2x, " +
                         $"{GetAbsoluteUrl(media, EImageSize.Original)} 3x";
            }
            else if (resizeCount == 4)
            {
                srcset = $"{GetAbsoluteUrl(media, EImageSize.Small)} {SMALL_IMG_SIZE}w, " +
                         $"{GetAbsoluteUrl(media, EImageSize.Medium)} {MEDIUM_IMG_SIZE}w, " +
                         $"{GetAbsoluteUrl(media, EImageSize.MediumLarge)} 2x, " +
                         $"{GetAbsoluteUrl(media, EImageSize.Large)} 3x";
            }

            var maxWidth = media.Width < MEDIUM_LARGE_IMG_SIZE ? media.Width : MEDIUM_LARGE_IMG_SIZE;
            var defaultWidth = media.Width < MEDIUM_LARGE_IMG_SIZE ? media.Width : MEDIUM_LARGE_IMG_SIZE;
            var sizes = $"(max-width: {maxWidth}px) 100vw, {defaultWidth}px";

            imgNode.Attributes.Add("srcset", srcset);
            imgNode.Attributes.Add("sizes", sizes);

            return imgNode;
        }

        private async Task DeleteImageFileAsync(Media media, EImageSize size)
        {
            var path = GetImagePath(media.UploadedOn, size);
            await _storageProvider.DeleteFileAsync(media.FileName, path, IMAGE_PATH_SEPARATOR);
        }

        private (string fileNameSlugged, string title) ProcessFileName(string fileNameOrig, EUploadedFrom uploadFrom)
        {
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileNameOrig);

            if (fileNameWithoutExt.Length > MediaService.MEDIA_FILENAME_MAXLEN)
            {
                fileNameWithoutExt = fileNameWithoutExt.Substring(0, MediaService.MEDIA_FILENAME_MAXLEN);
            }

            if (uploadFrom == EUploadedFrom.MetaWeblog && fileNameWithoutExt.EndsWith("_2"))
            {
                fileNameWithoutExt = fileNameWithoutExt.Remove(fileNameWithoutExt.Length - 2);
            }

            var slug = Util.Slugify(fileNameWithoutExt);
            if (slug.IsNullOrEmpty())
            {
                slug = Util.RandomString(6);
            }
            else if (uploadFrom == EUploadedFrom.MetaWeblog && slug == "thumb")
            {
                slug = string.Concat(Util.RandomString(6), "_thumb");
            }

            var ext = Path.GetExtension(fileNameOrig).ToLower();
            var fileNameSlugged = $"{slug}{ext}";
            var fileNameEncoded = WebUtility.HtmlEncode(fileNameWithoutExt);

            return (fileNameSlugged: fileNameSlugged, title: fileNameEncoded);
        }

        private async Task<string> GetUniqueFileNameAsync(string fileNameSlugged, DateTimeOffset uploadedOn)
        {
            int i = 2;
            while (await _mediaSvc.ExistsAsync(m => m.AppType == EAppType.Blog &&
                                                    m.UploadedOn.Year == uploadedOn.Year &&
                                                    m.UploadedOn.Month == uploadedOn.Month &&
                                                    m.FileName.Equals(fileNameSlugged)))
            {
                var lookUp = ".";
                var replace = $"-{i}.";
                if (i > 2)
                {
                    int j = i - 1;
                    lookUp = $"-{j}.";
                }

                fileNameSlugged = fileNameSlugged.Replace(lookUp, replace);
                i++;
            }

            return fileNameSlugged;
        }
    }
}
