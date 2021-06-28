using Fan.Blog.Models;
using Fan.Blog.Services.Interfaces;
using Fan.Exceptions;
using Fan.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace Fan.WebApp.Manage.Admin
{
    public class CategoriesModel : PageModel
    {
        private readonly ICategoryService _catSvc;
        private readonly ISettingService _settingSvc;

        public CategoriesModel(ICategoryService catService,
            ISettingService settingService)
        {
            _catSvc = catService;
            _settingSvc = settingService;
        }

        public string CategoryListJsonStr { get; private set; }
        public int DefaultCategoryId { get; private set; }

        public async Task OnGetAsync()
        {
            var blogSettings = await _settingSvc.GetSettingsAsync<BlogSettings>();
            DefaultCategoryId = blogSettings.DefaultCategoryId;

            var cats = await _catSvc.GetAllAsync();
            CategoryListJsonStr = JsonConvert.SerializeObject(cats);
        }

        public async Task OnDeleteAsync(int id)
        {
            await _catSvc.DeleteAsync(id);
        }

        public async Task<IActionResult> OnPostAsync([FromBody]Category category)
        {
            try
            {
                var cat = await _catSvc.CreateAsync(category.Title, category.Description);
                return new JsonResult(cat);
            }
            catch (FanException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        public async Task<IActionResult> OnPostUpdateAsync([FromBody]Category category)
        {
            try
            {
                var cat = await _catSvc.UpdateAsync(category);
                return new JsonResult(cat);
            }
            catch (FanException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        public async Task OnPostDefaultAsync(int id)
        {
            await _catSvc.SetDefaultAsync(id);
        }
    }
}