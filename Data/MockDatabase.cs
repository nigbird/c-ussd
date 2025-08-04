using ussd.Models;

namespace ussd.Data
{
    public static class MockDatabase
    {
        public static List<User> Users = new List<User>
        {
            new User { Id = 1, Name = "Amanuel", NationalId = "FAYIDA123", IsVerified = true, CreditScore = 750 },
            new User { Id = 2, Name = "Sara", NationalId = "FAYIDA456", IsVerified = false, CreditScore = 600 }
        };

        public static List<Bank> Banks = new List<Bank>
        {
            new Bank {
                Id = 1,
                Name = "NIB",
                LoanProducts = new List<LoanProduct>
                {
                    new LoanProduct { Id = 1, Name = "NIB Microloan Basic", MinAmount = 500, MaxAmount = 5000, MinCreditScore = 650, Description = "Basic microloan for verified users." },
                    new LoanProduct { Id = 2, Name = "NIB Microloan Plus", MinAmount = 5000, MaxAmount = 20000, MinCreditScore = 700, Description = "Higher amount for users with good credit." }
                }
            },
            new Bank {
                Id = 2,
                Name = "Dashen",
                LoanProducts = new List<LoanProduct>
                {
                    new LoanProduct { Id = 3, Name = "Dashen Starter Loan", MinAmount = 300, MaxAmount = 3000, MinCreditScore = 600, Description = "Starter loan for new users." },
                    new LoanProduct { Id = 4, Name = "Dashen Premium Loan", MinAmount = 10000, MaxAmount = 50000, MinCreditScore = 750, Description = "Premium loan for high credit users." }
                }
            },
            new Bank {
                Id = 3,
                Name = "CBE",
                LoanProducts = new List<LoanProduct>
                {
                    new LoanProduct { Id = 5, Name = "CBE Small Loan", MinAmount = 200, MaxAmount = 2000, MinCreditScore = 600, Description = "Small loan for new users." },
                    new LoanProduct { Id = 6, Name = "CBE Business Loan", MinAmount = 20000, MaxAmount = 100000, MinCreditScore = 800, Description = "Business loan for entrepreneurs." }
                }
            },
            new Bank {
                Id = 4,
                Name = "Awash",
                LoanProducts = new List<LoanProduct>
                {
                    new LoanProduct { Id = 7, Name = "Awash Quick Loan", MinAmount = 1000, MaxAmount = 10000, MinCreditScore = 650, Description = "Quick loan for verified users." },
                    new LoanProduct { Id = 8, Name = "Awash Gold Loan", MinAmount = 15000, MaxAmount = 75000, MinCreditScore = 780, Description = "Gold loan for top credit users." }
                }
            }
        };
    }
}
