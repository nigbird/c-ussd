using System.Collections.Generic;
using ussd.Data;
using ussd.Models;

namespace ussd.Data
{
    public class MockLoan
    {
        public string BankName { get; set; } = "";
        public string ProductName { get; set; } = "";
        public decimal Amount { get; set; }
        public decimal Interest { get; set; }
        public decimal Repaid { get; set; }
        public string Status { get; set; } = "";
    }

    public static class MockLoanDB
    {
        // Simulate loan data for demo

        // Repay loan by updating the Repaid amount and user balance
        public static void RepayLoan(string phoneNumber, MockLoan loan, decimal amount)
        {
            var userLoans = GetLoans(phoneNumber);
            var targetLoan = userLoans.FirstOrDefault(l => l.BankName == loan.BankName && l.ProductName == loan.ProductName && l.Amount == loan.Amount && l.Interest == loan.Interest);
            if (targetLoan != null)
            {
                targetLoan.Repaid += amount;
                if (targetLoan.Repaid >= targetLoan.Amount + targetLoan.Interest)
                {
                    targetLoan.Status = "Repaid";
                }
                // Update balance
                MockBalanceDB.AddToBalance(phoneNumber, -amount);
            }
        }

        // Add a new loan for a user and update balance
        public static void AddLoan(string phoneNumber, string bankName, string productName, decimal amount, decimal interest)
        {
            if (!loans.ContainsKey(phoneNumber))
                loans[phoneNumber] = new List<MockLoan>();
            loans[phoneNumber].Add(new MockLoan
            {
                BankName = bankName,
                ProductName = productName,
                Amount = amount,
                Interest = interest,
                Repaid = 0,
                Status = "Active"
            });
            MockBalanceDB.AddToBalance(phoneNumber, amount);
        }

        // All loans are now per-user, created dynamically. No hardcoded demo loans.
        private static Dictionary<string, List<MockLoan>> loans = new();

        public static List<MockLoan> GetLoans(string phoneNumber)
        {
            return loans.ContainsKey(phoneNumber) ? loans[phoneNumber] : new List<MockLoan>();
        }
    }

    public static class MockBalanceDB
    {
        // All balances are now per-user, created dynamically. No hardcoded demo balances.
        private static Dictionary<string, decimal> balances = new();

        public static decimal GetBalance(string phoneNumber)
        {
            return balances.ContainsKey(phoneNumber) ? balances[phoneNumber] : 0;
        }

        // Add or subtract from balance
        public static void AddToBalance(string phoneNumber, decimal amount)
        {
            if (!balances.ContainsKey(phoneNumber))
                balances[phoneNumber] = 0;
            balances[phoneNumber] += amount;
        }
    }
}
