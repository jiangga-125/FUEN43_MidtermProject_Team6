// /Services/Pricing/IPricingEngine.cs
using System.Threading.Tasks;

namespace BookLoop.Services.Pricing
{
	public interface IPricingEngine
	{
		Task<PricingResult> PreviewAsync(PricingInputs input);
	}
}
