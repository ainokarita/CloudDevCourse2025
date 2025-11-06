using Microsoft.AspNetCore.Mvc;
using UmbracoProject.Services;

namespace UmbracoProject.Components
{
    public class GiphyWidgetViewComponent : ViewComponent
    {
        private readonly GiphyService _svc;
        public GiphyWidgetViewComponent(GiphyService svc) => _svc = svc;

        public async Task<IViewComponentResult> InvokeAsync(string? tag = null, string? rating = null)
        {
            var gif = await _svc.GetRandomAsync(tag, rating);
            return View(gif); // view handles null (renders nothing)
        }
    }

}
