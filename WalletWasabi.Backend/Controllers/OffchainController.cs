using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;

namespace WalletWasabi.Backend.Controllers;

/// <summary>
/// To acquire offchain data.
/// </summary>
[Produces("application/json")]
[Route("api/v" + Constants.BackendMajorVersion + "/btc/[controller]")]
public class OffchainController : ControllerBase
{
	public OffchainController(IMemoryCache memoryCache, IExchangeRateProvider exchangeRateProvider)
	{
		Cache = memoryCache;
		ExchangeRateProvider = exchangeRateProvider;
	}

	private IMemoryCache Cache { get; }
	private IExchangeRateProvider ExchangeRateProvider { get; }

	internal async Task<IEnumerable<ExchangeRate>> GetExchangeRatesCollectionAsync(CancellationToken cancellationToken)
	{
		var cacheKey = nameof(GetExchangeRatesCollectionAsync);

		if (!Cache.TryGetValue(cacheKey, out IEnumerable<ExchangeRate>? exchangeRates))
		{
			exchangeRates = await ExchangeRateProvider.GetExchangeRateAsync(cancellationToken).ConfigureAwait(false);

			if (exchangeRates.Any())
			{
				var cacheEntryOptions = new MemoryCacheEntryOptions()
					.SetAbsoluteExpiration(TimeSpan.FromSeconds(500));

				Cache.Set(cacheKey, exchangeRates, cacheEntryOptions);
			}
		}

		return exchangeRates!;
	}
}
