using Fan.Data;
using Fan.Settings;
using Fan.Themes;
using Fan.Widgets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Fan.Tests.Widgets
{
    public class WidgetServiceTest 
    {
        private readonly IWidgetService widgetService;
        private readonly IThemeService themeService;
        private readonly Mock<IMetaRepository> metaRepoMock = new Mock<IMetaRepository>();

        public WidgetServiceTest()
        {
            // cache and logger
            var serviceProvider = new ServiceCollection().AddMemoryCache().AddLogging().BuildServiceProvider();
            var cache = new MemoryDistributedCache(serviceProvider.GetService<IOptions<MemoryDistributedCacheOptions>>());
            var loggerTheme = serviceProvider.GetService<ILoggerFactory>().CreateLogger<ThemeService>();
            var loggerWidget = serviceProvider.GetService<ILoggerFactory>().CreateLogger<WidgetService>();

            // mock ContentRootPath to return current dir
            var hostingEnvMock = new Mock<IWebHostEnvironment>();
            hostingEnvMock.Setup(env => env.ContentRootPath).Returns(Directory.GetCurrentDirectory());

            // mock CoreSettings
            var settingSvcMock = new Mock<ISettingService>();
            settingSvcMock.Setup(svc => svc.GetSettingsAsync<CoreSettings>())
                .Returns(Task.FromResult(new CoreSettings { Theme = "MyTheme" }));

            // services
            themeService = new ThemeService(hostingEnvMock.Object, cache, metaRepoMock.Object, loggerTheme);
            widgetService = new WidgetService(metaRepoMock.Object, themeService, cache, settingSvcMock.Object, hostingEnvMock.Object, loggerWidget);
        }

        // -------------------------------------------------------------------- setup

        /// <summary>
        /// MyWidget id and meta id.
        /// </summary>
        /// <remarks>
        /// A widget has an id and it's the same as meta id.
        /// </remarks>
        private const int MY_WIDGET_ID = 4;
        private const int MY_WIDGET_ID2 = 5;
        /// <summary>
        /// MyWidget folder.
        /// </summary>
        private const string MY_WIDGET_FOLDER = "MyWidget";

        /// <summary>
        /// Widget area "my-area" id.
        /// </summary>
        private const string MY_AREA_ID = "my-area";
        /// <summary>
        /// Widget area "my-area" meta key. 
        /// </summary>
        /// <remarks>
        /// The meta key of a theme-defined widget area is prefixed by theme key (folder name).
        /// </remarks>
        private const string MY_AREA_META_KEY = "mytheme-my-area";

        private void Setup_Widget_Areas(params int[] widgetIds)
        {
            // system-defined widget area "blog-sidebar1"
            var widgetArea = new WidgetArea { Id = "blog-sidebar1" };
            var meta = new Meta { Id = 1, Key = widgetArea.Id, Value = JsonConvert.SerializeObject(widgetArea), Type = EMetaType.WidgetAreaBySystem };
            metaRepoMock.Setup(repo => repo.GetAsync(meta.Key, EMetaType.WidgetAreaBySystem)).Returns(Task.FromResult(meta));

            // system-defined widget area "blog-sidebar2"
            widgetArea = new WidgetArea { Id = "blog-sidebar2" };
            meta = new Meta { Id = 2, Key = widgetArea.Id, Value = JsonConvert.SerializeObject(widgetArea), Type = EMetaType.WidgetAreaBySystem };
            metaRepoMock.Setup(repo => repo.GetAsync(meta.Key, EMetaType.WidgetAreaBySystem)).Returns(Task.FromResult(meta));

            // theme-defined widget area "my-area"
            widgetArea = widgetIds.Length > 0 ? 
                new WidgetArea { Id = MY_AREA_ID, WidgetIds = widgetIds } :
                new WidgetArea { Id = MY_AREA_ID };
            meta = new Meta { Id = 3, Key = MY_AREA_META_KEY, Value = JsonConvert.SerializeObject(widgetArea), Type = EMetaType.WidgetAreaByTheme };
            metaRepoMock.Setup(repo => repo.GetAsync(meta.Key, EMetaType.WidgetAreaByTheme)).Returns(Task.FromResult(meta));
        }

        private void Setup_Widget_Areas_for_create()
        {
            // system-defined widget area "blog-sidebar1"
            var widgetArea = new WidgetArea { Id = "blog-sidebar1" };
            var meta = new Meta { Id = 1, Key = widgetArea.Id, Value = JsonConvert.SerializeObject(widgetArea), Type = EMetaType.WidgetAreaBySystem };
            metaRepoMock.Setup(repo => repo.GetAsync(meta.Key, EMetaType.WidgetAreaBySystem)).Returns(Task.FromResult(meta));

            // system-defined widget area "blog-sidebar2"
            widgetArea = new WidgetArea { Id = "blog-sidebar2" };
            meta = new Meta { Id = 2, Key = widgetArea.Id, Value = JsonConvert.SerializeObject(widgetArea), Type = EMetaType.WidgetAreaBySystem };
            metaRepoMock.Setup(repo => repo.GetAsync(meta.Key, EMetaType.WidgetAreaBySystem)).Returns(Task.FromResult(meta));

            // suppose now MyTheme just added a theme-defined area, repo CreateAsync will be called
            widgetArea = new WidgetArea { Id = MY_AREA_ID };
            meta = new Meta { Id = 3, Key = MY_AREA_META_KEY, Value = JsonConvert.SerializeObject(widgetArea), Type = EMetaType.WidgetAreaByTheme };
            metaRepoMock.Setup(repo => repo.CreateAsync(It.Is<Meta>(m =>
                m.Key == meta.Key &&
                m.Value == meta.Value &&
                m.Type == meta.Type))).Returns(Task.FromResult(meta));
        }

        private void Setup_MyWidget()
        {
            var myWidget = new MyWidget { Folder = MY_WIDGET_FOLDER };
            var meta = new Meta 
            { 
                Id = MY_WIDGET_ID, 
                Key = "mywidget-2da0l8", 
                Value = JsonConvert.SerializeObject(myWidget),
                Type = EMetaType.Widget,
            };
            metaRepoMock.Setup(repo => repo.GetAsync(MY_WIDGET_ID)).Returns(Task.FromResult(meta));
        }

        // -------------------------------------------------------------------- widget areas

        /// <summary>
        /// RegisterAreaAsync registers a widget area by its id by inserting a meta record to db.
        /// </summary>
        [Fact]
        public async void RegisterAreaAsync_creates_meta_records_for_widget_areas_from_current_theme()
        {
            // Arrange
            Setup_Widget_Areas_for_create();

            // Act registers my area
            var meta = await widgetService.RegisterAreaAsync(MY_AREA_ID, EMetaType.WidgetAreaByTheme);

            // Assert my-area is registered 
            Assert.Equal(3, meta.Id);
            Assert.Equal(MY_AREA_META_KEY, meta.Key);
        }

       
        [Fact]
        public async void OrderWidgetInAreaAsync_orders_widget_index_within_an_area()
        {
            // Arrange: my-area contains two of my widget [4, 5]
            Setup_MyWidget();
            Setup_Widget_Areas(MY_WIDGET_ID, MY_WIDGET_ID2);

            // Act: user drags the second my widget to be first
            await widgetService.OrderWidgetInAreaAsync(MY_WIDGET_ID2, MY_AREA_ID, 0);

            // Assert: the order of the two widgets swapped
            var myArea = new WidgetArea { Id = MY_AREA_ID, WidgetIds = new int[] { 5, 4 } };
            metaRepoMock.Verify(repo => repo.UpdateAsync(
              It.Is<Meta>(m => m.Value == JsonConvert.SerializeObject(myArea))), Times.Once);
        }


        /// <summary>
        /// When user saves widget settings in admin panel, UpdateWidgetAsync updates its meta record.
        /// </summary>
        [Fact]
        public async void UpdateWidgetAsync_updates_meta_for_widget()
        {
            // Arrange:  myWidget meta
            var myWidget = new MyWidget { Folder = MY_WIDGET_FOLDER };
            var myWidgetMetaValueAfterCreate = JsonConvert.SerializeObject(myWidget);
            var meta = new Meta { Id = MY_WIDGET_ID, Key = MY_AREA_META_KEY, Value = myWidgetMetaValueAfterCreate, Type = EMetaType.WidgetAreaByTheme };
            metaRepoMock.Setup(repo => repo.GetAsync(MY_WIDGET_ID)).Returns(Task.FromResult(meta));

            // Act: user saves myWidget settings
            await widgetService.UpdateWidgetAsync(MY_WIDGET_ID, myWidget);

            // Assert: repo UpdateAsync is called once
            metaRepoMock.Verify(repo => repo.UpdateAsync(It.Is<Meta>(m => m.Value == myWidgetMetaValueAfterCreate)), Times.Once);
        }
    }
}
