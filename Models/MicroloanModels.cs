namespace ussd.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string NationalId { get; set; }
        public bool IsVerified { get; set; }
        public int CreditScore { get; set; }
    }

    public class Bank
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<LoanProduct> LoanProducts { get; set; }
    }

    public class LoanProduct
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal MinAmount { get; set; }
        public decimal MaxAmount { get; set; }
        public int MinCreditScore { get; set; }
        public string Description { get; set; }
    }
}
