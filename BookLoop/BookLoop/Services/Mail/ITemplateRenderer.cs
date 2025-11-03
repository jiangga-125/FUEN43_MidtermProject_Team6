namespace BookLoop.Services.Mail
{
		public interface ITemplateRenderer
		{
			/// 將模板中的 {{Key}} 用 model 字典取代
			string Render(string template, IDictionary<string, string> model);
		}

}
