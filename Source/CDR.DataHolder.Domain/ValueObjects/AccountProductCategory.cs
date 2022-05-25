using System.Collections.Generic;
using System.Linq;

namespace CDR.DataHolder.Domain.ValueObjects
{
	public class AccountProductCategory : ReferenceType<AccountProductCategoryType, string>
	{
		public AccountProductCategoryType Id { get; set; }
		public string Code { get; set; }

		public static IDictionary<AccountProductCategoryType, string> Values
		{
			get
			{
				return new Dictionary<AccountProductCategoryType, string>
				{
					{AccountProductCategoryType.BusinessLoans, "BUSINESS_LOANS" },
					{AccountProductCategoryType.CredAndChrgCards, "CRED_AND_CHRG_CARDS" },
					{AccountProductCategoryType.Leases, "LEASES" },
					{AccountProductCategoryType.MarginLoans, "MARGIN_LOANS" },
					{AccountProductCategoryType.Overdrafts, "OVERDRAFTS" },
					{AccountProductCategoryType.PersLoans, "PERS_LOANS" },
					{AccountProductCategoryType.RegulatedTrustAccounts, "REGULATED_TRUST_ACCOUNTS" },
					{AccountProductCategoryType.ResidentialMortgages, "RESIDENTIAL_MORTGAGES" },
					{AccountProductCategoryType.TermDeposits, "TERM_DEPOSITS" },
					{AccountProductCategoryType.TradeFinance, "TRADE_FINANCE" },
					{AccountProductCategoryType.TransAndSavingsAccounts, "TRANS_AND_SAVINGS_ACCOUNTS" },
					{AccountProductCategoryType.TravelCards, "TRAVEL_CARDS" },
				};
			}
		}
	}

	public enum AccountProductCategoryType
	{
		Unknown = 0,
		BusinessLoans = 1,
		CredAndChrgCards = 2,
		Leases = 3,
		MarginLoans = 4,
		Overdrafts = 5,
		PersLoans = 6,
		RegulatedTrustAccounts = 7,
		ResidentialMortgages = 8,
		TermDeposits = 9,
		TradeFinance = 10,
		TransAndSavingsAccounts = 11,
		TravelCards = 12
	}
}