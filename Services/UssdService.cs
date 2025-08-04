using ussd.Models;
using ussd.Data;

namespace ussd.Services
{
    public class UssdService
    {
        public User? VerifyUser(string nationalId)
        {
            return MockDatabase.Users.FirstOrDefault(u => u.NationalId == nationalId);
        }

        public List<string> GetMainMenu(User user)
        {
            if (!user.IsVerified)
                return new List<string> { "Your Fayida (National ID) is not verified." };
            return new List<string> { "1. Apply for Loan", "2. Repay Loan", "3. Check Balance", "4. Loan Status" };
        }

        public List<Bank> GetBanks()
        {
            return MockDatabase.Banks;
        }

        public List<LoanProduct> GetEligibleProducts(User user, Bank bank)
        {
            return bank.LoanProducts.Where(lp => user.CreditScore >= lp.MinCreditScore).ToList();
        }
    }
}
