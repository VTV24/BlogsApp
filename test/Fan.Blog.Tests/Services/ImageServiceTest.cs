using Fan.Blog.Enums;
using Fan.Blog.Models;
using Fan.Blog.Services;
using Fan.Blog.Services.Interfaces;
using Fan.Medias;
using Fan.Settings;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Fan.Blog.Tests.Services
{
    public class ImageServiceTest
    {
        protected Mock<IMediaService> _mediaSvcMock;
        protected Mock<IStorageProvider> _storageProMock;
        protected IImageService _imgSvc;

        readonly string _absPath;
        readonly Media _media;
        const string FILENAME = "pic.jpg";
        const string STORAGE_ENDPOINT = "https://localhost:44381";

        public ImageServiceTest()
        {
            var _settingSvcMock = new Mock<ISettingService>();
            _settingSvcMock.Setup(svc => svc.GetSettingsAsync<CoreSettings>()).Returns(Task.FromResult(new CoreSettings()));
            _settingSvcMock.Setup(svc => svc.GetSettingsAsync<BlogSettings>()).Returns(Task.FromResult(new BlogSettings()));

            var appSettingsMock = new Mock<IOptionsSnapshot<AppSettings>>();
            appSettingsMock.Setup(o => o.Value).Returns(new AppSettings());

            _mediaSvcMock = new Mock<IMediaService>();
            _storageProMock = new Mock<IStorageProvider>();
            _imgSvc = new ImageService(_mediaSvcMock.Object, _storageProMock.Object, appSettingsMock.Object);

            var uploadedOn = DateTimeOffset.UtcNow;
            var year = uploadedOn.Year;
            var month = uploadedOn.Month.ToString("d2");
            _absPath = $"{STORAGE_ENDPOINT}/media/blog/{year}/{month}";

            _storageProMock.Setup(pro => pro.StorageEndpoint).Returns(STORAGE_ENDPOINT);

            _media = new Media
            {
                FileName = FILENAME,
                UploadedOn = uploadedOn,
            };
        }

        [Fact]
        public async void ProcessResponsiveImageAsync_on_large_2200x1650_landscape_picture()
        {
            _mediaSvcMock.Setup(svc => svc.GetMediaAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.FromResult(new Media
                {
                    FileName = "painting-2200x1650.jpg",
                    ResizeCount = 4,
                    Width = 2200,
                    Height = 1650,
                    UploadedOn = new DateTimeOffset(2019, 4, 3, 0, 0, 0, TimeSpan.Zero),
                }));

            var input = "<img src=\"https://localhost:44381/media/blog/2019/04/md/painting-2200x1650.jpg\" alt=\"painting 2200x1650\">";
            var expected = "<img src=\"https://localhost:44381/media/blog/2019/04/md/painting-2200x1650.jpg\" alt=\"painting 2200x1650\" " +
                           $"srcset=\"https://localhost:44381/media/blog/2019/04/sm/painting-2200x1650.jpg {ImageService.SMALL_IMG_SIZE}w, " +
                           $"https://localhost:44381/media/blog/2019/04/md/painting-2200x1650.jpg {ImageService.MEDIUM_IMG_SIZE}w, " + 
                           "https://localhost:44381/media/blog/2019/04/ml/painting-2200x1650.jpg 2x, " +
                           "https://localhost:44381/media/blog/2019/04/lg/painting-2200x1650.jpg 3x\" " +
                           $"sizes=\"(max-width: {ImageService.MEDIUM_LARGE_IMG_SIZE}px) 100vw, {ImageService.MEDIUM_LARGE_IMG_SIZE}px\">";
            var output = await _imgSvc.ProcessResponsiveImageAsync(input);

            Assert.Equal(expected, output);
        }

        [Fact]
        public async void ProcessRepsonsiveImageAsync_on_medium_large_960x1440_portrait_picture()
        {
            _mediaSvcMock.Setup(svc => svc.GetMediaAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.FromResult(new Media
                {
                    FileName = "nightsky-960x1440.jpg",
                    ResizeCount = 3,
                    Width = 960,
                    Height = 1440,
                    UploadedOn = new DateTimeOffset(2019, 4, 3, 0, 0, 0, TimeSpan.Zero),
                }));

            var input = "<img src=\"https://localhost:44381/media/blog/2019/04/md/nightsky-960x1440.jpg\" alt=\"nightsky 960x1440\">";
            var expected = "<img src=\"https://localhost:44381/media/blog/2019/04/md/nightsky-960x1440.jpg\" alt=\"nightsky 960x1440\" "+
                           $"srcset=\"https://localhost:44381/media/blog/2019/04/sm/nightsky-960x1440.jpg {ImageService.SMALL_IMG_SIZE}w, "+
                           $"https://localhost:44381/media/blog/2019/04/md/nightsky-960x1440.jpg {ImageService.MEDIUM_IMG_SIZE}w, "+
                           "https://localhost:44381/media/blog/2019/04/ml/nightsky-960x1440.jpg 2x, "+
                           "https://localhost:44381/media/blog/2019/04/nightsky-960x1440.jpg 3x\" "+
                           "sizes=\"(max-width: 960px) 100vw, 960px\">";
            var output = await _imgSvc.ProcessResponsiveImageAsync(input);

            Assert.Equal(expected, output);
        }

        [Fact]
        public async void ProcessRepsonsiveImageAsync_on_tiny_90x90_square_picture()
        {
            _mediaSvcMock.Setup(svc => svc.GetMediaAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.FromResult(new Media
                {
                    FileName = "sq-90x90.png",
                    ResizeCount = 0,
                    Width = 90,
                    Height = 90,
                    UploadedOn = new DateTimeOffset(2019, 4, 3, 0, 0, 0, TimeSpan.Zero),
                }));

            var input = "<img src=\"https://localhost:44381/media/blog/2019/04/sq-90x90.png\" alt=\"sq 90x90\">";
            var expected = "<img src=\"https://localhost:44381/media/blog/2019/04/sq-90x90.png\" alt=\"sq 90x90\">";
            var output = await _imgSvc.ProcessResponsiveImageAsync(input);

            Assert.Equal(expected, output);
        }

        [Fact]
        public void GetImageUrl_with_0_ResizeCount()
        {
            _media.ResizeCount = 0;

            var origUrl = $"{_absPath}/{FILENAME}";

            var actualUrl = _imgSvc.GetAbsoluteUrl(_media, EImageSize.Original);
            Assert.Equal(origUrl, actualUrl);

            actualUrl = _imgSvc.GetAbsoluteUrl(_media, EImageSize.Large);
            Assert.Equal(origUrl, actualUrl);

            actualUrl = _imgSvc.GetAbsoluteUrl(_media, EImageSize.Medium);
            Assert.Equal(origUrl, actualUrl);

            actualUrl = _imgSvc.GetAbsoluteUrl(_media, EImageSize.Small);
            Assert.Equal(origUrl, actualUrl);
        }

        [Fact]
        public void GetImageUrl_with_1_ResizeCount()
        {
            _media.ResizeCount = 1;

            var origUrl = $"{_absPath}/{FILENAME}";

            var actualUrl = _imgSvc.GetAbsoluteUrl(_media, EImageSize.Original);
            Assert.Equal(origUrl, actualUrl);

            actualUrl = _imgSvc.GetAbsoluteUrl(_media, EImageSize.Large);
            Assert.Equal(origUrl, actualUrl);

            actualUrl = _imgSvc.GetAbsoluteUrl(_media, EImageSize.MediumLarge);
            Assert.Equal(origUrl, actualUrl);

            actualUrl = _imgSvc.GetAbsoluteUrl(_media, EImageSize.Medium);
            Assert.Equal(origUrl, actualUrl);

            var smallUrl = $"{_absPath}/sm/{FILENAME}";
            actualUrl = _imgSvc.GetAbsoluteUrl(_media, EImageSize.Small);
            Assert.Equal(smallUrl, actualUrl);
        }

        [Fact]
        public void GetImageUrl_with_2_ResizeCount()
        {
            _media.ResizeCount = 2;

            var origUrl = $"{_absPath}/{FILENAME}";
            var smallUrl = $"{_absPath}/sm/{FILENAME}";
            var mediumUrl = $"{_absPath}/md/{FILENAME}";

            var actualUrl = _imgSvc.GetAbsoluteUrl(_media, EImageSize.Original);
            Assert.Equal(origUrl, actualUrl);

            actualUrl = _imgSvc.GetAbsoluteUrl(_media, EImageSize.Large);
            Assert.Equal(origUrl, actualUrl);

            actualUrl = _imgSvc.GetAbsoluteUrl(_media, EImageSize.MediumLarge);
            Assert.Equal(origUrl, actualUrl);

            actualUrl = _imgSvc.GetAbsoluteUrl(_media, EImageSize.Medium);
            Assert.Equal(mediumUrl, actualUrl);

            actualUrl = _imgSvc.GetAbsoluteUrl(_media, EImageSize.Small);
            Assert.Equal(smallUrl, actualUrl);
        }

        [Fact]
        public void GetImageUrl_with_3_ResizeCount()
        {
            _media.ResizeCount = 3;

            var origUrl = $"{_absPath}/{FILENAME}";
            var smallUrl = $"{_absPath}/sm/{FILENAME}";
            var mediumUrl = $"{_absPath}/md/{FILENAME}";
            var mediumLargeUrl = $"{_absPath}/ml/{FILENAME}";

            var actualUrl = _imgSvc.GetAbsoluteUrl(_media, EImageSize.Original);
            Assert.Equal(origUrl, actualUrl);

            actualUrl = _imgSvc.GetAbsoluteUrl(_media, EImageSize.Large);
            Assert.Equal(origUrl, actualUrl);

            actualUrl = _imgSvc.GetAbsoluteUrl(_media, EImageSize.MediumLarge);
            Assert.Equal(mediumLargeUrl, actualUrl);

            actualUrl = _imgSvc.GetAbsoluteUrl(_media, EImageSize.Medium);
            Assert.Equal(mediumUrl, actualUrl);

            actualUrl = _imgSvc.GetAbsoluteUrl(_media, EImageSize.Small);
            Assert.Equal(smallUrl, actualUrl);
        }

        [Fact]
        public void GetImageUrl_with_4_ResizeCount()
        {
            _media.ResizeCount = 4;

            var origUrl = $"{_absPath}/{FILENAME}";
            var smallUrl = $"{_absPath}/sm/{FILENAME}";
            var mediumUrl = $"{_absPath}/md/{FILENAME}";
            var mediumLargeUrl = $"{_absPath}/ml/{FILENAME}";
            var largeUrl = $"{_absPath}/lg/{FILENAME}";

            var actualUrl = _imgSvc.GetAbsoluteUrl(_media, EImageSize.Original);
            Assert.Equal(origUrl, actualUrl);

            actualUrl = _imgSvc.GetAbsoluteUrl(_media, EImageSize.Large);
            Assert.Equal(largeUrl, actualUrl);

            actualUrl = _imgSvc.GetAbsoluteUrl(_media, EImageSize.MediumLarge);
            Assert.Equal(mediumLargeUrl, actualUrl);

            actualUrl = _imgSvc.GetAbsoluteUrl(_media, EImageSize.Medium);
            Assert.Equal(mediumUrl, actualUrl);

            actualUrl = _imgSvc.GetAbsoluteUrl(_media, EImageSize.Small);
            Assert.Equal(smallUrl, actualUrl);
        }
    }
}
