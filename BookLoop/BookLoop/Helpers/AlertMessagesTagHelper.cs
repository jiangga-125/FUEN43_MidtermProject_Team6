using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BookLoop.Helpers
{
	[HtmlTargetElement("alert-messages")] // 在 Razor 裡用 <alert-messages />
	public class AlertMessagesTagHelper : TagHelper
	{
		private readonly ITempDataDictionary _tempData;

		public AlertMessagesTagHelper(ITempDataDictionaryFactory factory, IHttpContextAccessor accessor)
		{
			_tempData = factory.GetTempData(accessor.HttpContext!);
		}

		public override void Process(TagHelperContext context, TagHelperOutput output)
		{
			output.TagName = ""; // 不輸出額外標籤
			var html = "";

			if (_tempData["Success"] != null)
			{
				html += $@"
                <div class='alert alert-success alert-dismissible fade show' role='alert'>
                    {_tempData["Success"]}
                    <button type='button' class='btn-close' data-bs-dismiss='alert' aria-label='Close'></button>
                </div>";
			}

			if (_tempData["Error"] != null)
			{
				html += $@"
                <div class='alert alert-danger alert-dismissible fade show' role='alert'>
                    {_tempData["Error"]}
                    <button type='button' class='btn-close' data-bs-dismiss='alert' aria-label='Close'></button>
                </div>";
			}

			if (_tempData["Warning"] != null)
			{
				html += $@"
                <div class='alert alert-warning alert-dismissible fade show' role='alert'>
                    {_tempData["Warning"]}
                    <button type='button' class='btn-close' data-bs-dismiss='alert' aria-label='Close'></button>
                </div>";
			}

			output.Content.SetHtmlContent(html);
		}
	}
}
