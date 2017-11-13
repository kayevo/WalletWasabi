﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using HiddenWallet.Daemon.Models;
using HiddenWallet.KeyManagement;
using NBitcoin;
using System.Globalization;
using HiddenWallet.FullSpv.Fees;
using HiddenWallet.SharedApi.Models;

namespace HiddenWallet.Daemon.Controllers
{
    [Route("api/v1/[controller]")]
    public class WalletController : Controller
    {
        [HttpGet]
        public string Test()
        {
            return "test";
        }

        [Route("create")]
        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody]PasswordModel request)
        {
            if (request == null || request.Password == null)
            {
                return new ObjectResult(new FailureResponse { Message = "Bad request", Details = "" });
            }

            try
            {
                WalletCreateResponse response = await Global.WalletWrapper.CreateAsync(request.Password);

                return new ObjectResult(response);
            }
            catch (Exception ex)
            {
                return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
            }
        }

        [Route("recover")]
        [HttpPost]
        public async Task<IActionResult> RecoverAsync([FromBody]WalletRecoverRequest request)
        {
            if (request == null || request.Password == null || request.Mnemonic == null)
            {
                return new ObjectResult(new FailureResponse { Message = "Bad request", Details = "" });
            }

            try
            {
                await Global.WalletWrapper.RecoverAsync(request.Password, request.Mnemonic, request.CreationTime);

                return new ObjectResult(new SuccessResponse());
            }
            catch (Exception ex)
            {
                return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
            }
        }

        [Route("load")]
        [HttpPost]
        public async Task<IActionResult> LoadAsync([FromBody]PasswordModel request)
        {
            if (request == null || request.Password == null)
            {
                return new ObjectResult(new FailureResponse { Message = "Bad request", Details = "" });
            }

            try
            {
                await Global.WalletWrapper.LoadAsync(request.Password);

                return new ObjectResult(new SuccessResponse());
            }
            catch (Exception ex)
            {
                return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
            }
        }

        [Route("wallet-exists")]
        [HttpGet]
        public IActionResult WalletExists()
        {
            try
            {
                if (Global.WalletWrapper.WalletExists)
                {
                    return new ObjectResult(new YesNoResponse { Value = true });
                }
                return new ObjectResult(new YesNoResponse { Value = false });
            }
            catch (Exception ex)
            {
                return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
            }
        }

        [Route("status")]
        [HttpGet]
        public async Task<IActionResult> StatusAsync()
        {
            try
            {
                return new ObjectResult(await Global.WalletWrapper.GetStatusResponseAsync());
            }
            catch (Exception ex)
            {
                return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
            }
        }

        [Route("shutdown")]
        [HttpGet]
        public async Task<IActionResult> ShutdownAsync()
        {
            try
            {
                try
                {
                    await Global.WalletWrapper.EndAsync();

                    return new ObjectResult(new SuccessResponse());
                }
                finally
                {
                    // wait until the call returns
                    await Task.Delay(1000).ContinueWith(_ => Environment.Exit(0));
                }
            }
            catch (Exception ex)
            {
                return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
            }
        }

        [Route("balances/{account}")]
        [HttpGet]
        public IActionResult Balances(string account)
        {
            try
            {
                var fail = GetAccount(account, out SafeAccount safeAccount);
                if (fail != null) return new ObjectResult(fail);

                return new ObjectResult(new BalancesResponse
                {
                    Available = Global.WalletWrapper.GetAvailable(safeAccount).ToString(false, true),
                    Incoming = Global.WalletWrapper.GetIncoming(safeAccount).ToString(false, true)
                });
            }
            catch (Exception ex)
            {
                return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
            }
        }

        /// <returns>null if didn't fail</returns>
        private FailureResponse GetAccount(string account, out SafeAccount safeAccount)
        {
            safeAccount = null;
            if (account == null)
                return new FailureResponse { Message = "No request body specified" };

            if (!Global.WalletWrapper.IsDecrypted)
                return new FailureResponse { Message = "Wallet isn't decrypted" };

            var trimmed = account;
            if (string.Equals(trimmed, "alice", StringComparison.OrdinalIgnoreCase))
            {
                safeAccount = Global.WalletWrapper.AliceAccount;
                return null;
            }
            else if (string.Equals(trimmed, "bob", StringComparison.OrdinalIgnoreCase))
            {
                safeAccount = Global.WalletWrapper.BobAccount;
                return null;
            }
            else return new FailureResponse { Message = "Wrong account" };
        }

        [Route("receive/{account}")]
        [HttpGet]
        public IActionResult Receive(string account)
        {
            try
            {
                var fail = GetAccount(account, out SafeAccount safeAccount);
                if (fail != null) return new ObjectResult(fail);

                return new ObjectResult(Global.WalletWrapper.GetReceiveResponse(safeAccount));
            }
            catch (Exception ex)
            {
                return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
            }
        }

        [Route("history/{account}")]
        [HttpGet]
        public IActionResult History(string account)
        {
            try
            {
                var fail = GetAccount(account, out SafeAccount safeAccount);
                if (fail != null) return new ObjectResult(fail);

                return new ObjectResult(Global.WalletWrapper.GetHistoryResponse(safeAccount));
            }
            catch (Exception ex)
            {
                return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
            }
        }

        [Route("build-transaction/{account}")]
        [HttpPost]
        public async Task<IActionResult> BuildTransactionAsync(string account, [FromBody]BuildTransactionRequest request)
        {
            try
            {
                if (request == null || request.Password == null || request.Address == null || request.Amount == null || request.FeeType == null)
                {
                    return new ObjectResult(new FailureResponse { Message = "Bad request", Details = "" });
                }

                var fail = GetAccount(account, out SafeAccount safeAccount);
                if (fail != null) return new ObjectResult(fail);

                BitcoinAddress address;
                try
                {
                    address = BitcoinAddress.Create(request.Address, Global.WalletWrapper.Network);
                }
                catch (Exception)
                {
                    return new ObjectResult(new FailureResponse { Message = "Wrong address", Details = "" });
                }
                Money amount = Money.Zero; // in this case all funds are sent from the wallet
                try
                {
                    if (request.Amount != "all")
                    {
                        var tmpAmount = new Money(decimal.Parse(request.Amount.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture), MoneyUnit.BTC);

                        if (tmpAmount <= Money.Zero) return new ObjectResult(new FailureResponse { Message = "Amount must be > 0 or \"all\"", Details = "" });
                        amount = tmpAmount;
                    }
                }
                catch (Exception)
                {
                    return new ObjectResult(new FailureResponse { Message = "Wrong amount", Details = "" });
                }


                FeeType feeType;
                if (request.FeeType.Equals("high", StringComparison.OrdinalIgnoreCase))
                {
                    feeType = FeeType.High;
                }
                else if (request.FeeType.Equals("medium", StringComparison.OrdinalIgnoreCase))
                {
                    feeType = FeeType.Medium;
                }
                else if (request.FeeType.Equals("low", StringComparison.OrdinalIgnoreCase))
                {
                    feeType = FeeType.Low;
                }
                else
                {
                    return new ObjectResult(new FailureResponse { Message = "Wrong fee type", Details = "" });
                }

                return new ObjectResult(await Global.WalletWrapper.BuildTransactionAsync(request.Password, safeAccount, address, amount, feeType));
            }
            catch (Exception ex)
            {
                return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
            }
        }

        [Route("send-transaction")]
        [HttpPost]
        public async Task<IActionResult> SendTransactionAsync([FromBody]SendTransactionRequest request)
        {
            try
            {
                if (request == null || request.Hex == null)
                {
                    return new ObjectResult(new FailureResponse { Message = "Bad request", Details = "" });
                }

                Transaction tx;
                try
                {
                    tx = new Transaction(request.Hex);
                }
                catch (Exception)
                {
                    return new ObjectResult(new FailureResponse { Message = "Wrong transaction hex", Details = "" });
                }

                return new ObjectResult(await Global.WalletWrapper.SendTransactionAsync(tx));
            }
            catch (Exception ex)
            {
                return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
            }
        }

		[Route("tumbler-server")]
		[HttpGet]
		public async Task<IActionResult> TumblerServerAsync()
		{
			ChaumianCoinJoin.Models.StatusResponse response = await Global.WalletWrapper.GetTumblerStatusAsync();

			if (response.Success)
			{
				response.Address = Global.WalletWrapper.Network == Network.Main ? Global.Config.ChaumianTumblerMainAddress : Global.Config.ChaumianTumblerTestNetAddress;
				return new ObjectResult(response);
			}
			else
			{
				return new ObjectResult(new FailureResponse { Message = "Tumbler Status Error", Details = "" });
			}
		}

		[Route("tumble")]
		[HttpPost]
		public async Task<IActionResult> TumbleAsync([FromBody]TumbleRequest request)
		{
			try
			{
				var getFrom = GetAccount(request.From, out SafeAccount fromAccount);
				if (getFrom != null) return new ObjectResult(getFrom);

				var getTo = GetAccount(request.To, out SafeAccount toAccount);
				if (getTo != null) return new ObjectResult(getTo);

				BaseResponse result = await Global.WalletWrapper.TumbleAsync(fromAccount, toAccount);

				if (result.Success)
				{
					return new ObjectResult(new SuccessResponse());
				}
				else
				{
					return new ObjectResult(new FailureResponse { Message = "Tumbler Error", Details = "Could not submit for mixing [01]" });
				}
			}
			catch (Exception ex)
			{
				return new ObjectResult(new FailureResponse { Message = "Tumbler Error", Details = "Could not submit for mixing [02]. " + ex.ToString() });
			}
		}
	}
}
