using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

public partial class CoinJoinHistoryItemViewModel : HistoryItemViewModelBase
{
	private CoinJoinHistoryItemViewModel(
		int orderIndex,
		TransactionSummary transactionSummary,
		WalletViewModel walletVm,
		Money balance,
		bool isSingleCoinJoinTransaction)
		: base(orderIndex, transactionSummary)
	{
		Date = transactionSummary.FirstSeen.ToLocalTime();
		Balance = balance;
		IsCoinJoin = true;
		CoinJoinTransaction = transactionSummary;
		IsChild = !isSingleCoinJoinTransaction;

		SetAmount(transactionSummary.Amount, transactionSummary.GetFee());

		ShowDetailsCommand = ReactiveCommand.Create(() =>
			UiContext.Navigate(NavigationTarget.DialogScreen).To(
				new CoinJoinDetailsViewModel(this, walletVm.UiTriggers.TransactionsUpdateTrigger)));

		DateString = Date.ToLocalTime().ToUserFacingString();

		ItemType = GetItemType();
		ItemStatus = GetItemStatus();
	}

	public TransactionSummary CoinJoinTransaction { get; private set; }
}
