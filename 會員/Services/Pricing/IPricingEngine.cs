// /Services/Pricing/IPricingEngine.cs
using System.Threading.Tasks;

namespace 會員.Services.Pricing
{
	public interface IPricingEngine
	{
		Task<PricingResult> PreviewAsync(PricingInputs input);
	}
}
