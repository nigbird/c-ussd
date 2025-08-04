using System;
using System.Collections.Generic;
using System.Linq;
using ussd.Models;
using Microsoft.AspNetCore.Mvc;
using ussd.Data;

namespace ussd.Controllers
{
    [Route("api/ussd")]
    [ApiController]
    public class UssdController : ControllerBase
    {
        // Simple in-memory session store for demo
        private static Dictionary<string, Dictionary<string, object>> Sessions = new();

        // Example: per-user PINs and transaction history
        private static Dictionary<string, string> UserPins = new() {
            {"+251900000001", "1234"},
            {"+251900000002", "5678"},
            {"+251900000003", "4321"}
        };
        private static Dictionary<string, List<string>> UserTransactions = new();
        private static int MaxPinAttempts = 3;
        private static TimeSpan SessionTimeout = TimeSpan.FromMinutes(5);

        [HttpPost]
        public IActionResult HandleUssd([FromForm] string sessionId, [FromForm] string serviceCode, [FromForm] string phoneNumber, [FromForm] string? text = "")
        {
            Console.WriteLine($"USSD Request: sessionId={sessionId}, serviceCode={serviceCode}, phoneNumber={phoneNumber}, text={text}");
            try
            {
                var inputs = string.IsNullOrEmpty(text) ? new string[0] : text.Split('*');
                var userInput = inputs.Length > 0 ? inputs[^1] : "";
                if (!Sessions.ContainsKey(sessionId))
                    Sessions[sessionId] = new Dictionary<string, object> {
                        { "screen", "PIN" },
                        { "pinAttempts", 0 },
                        { "lastActivity", DateTime.UtcNow }
                    };
                var session = Sessions[sessionId];
                // Session timeout
                if (session.ContainsKey("lastActivity") && DateTime.UtcNow - (DateTime)session["lastActivity"] > SessionTimeout) {
                    Sessions.Remove(sessionId);
                    return Content("END Session timed out. Please try again.", "text/plain");
                }
                session["lastActivity"] = DateTime.UtcNow;

                string response = "";
                var homeMenu = "CON Welcome to Microloan USSD.\n1. Apply for Loan\n2. Check Loan Status\n3. Repay Loan\n4. Check Balance\n5. Transaction History\n6. Change PIN\n0. Exit";
                var correctPin = UserPins.ContainsKey(phoneNumber) ? UserPins[phoneNumber] : "1234";
                switch (session["screen"])
                {
                case "PIN":
                    if (!session.ContainsKey("pinAttempts")) session["pinAttempts"] = 0;
                    int attempts = (int)session["pinAttempts"];
                    if (attempts >= MaxPinAttempts) {
                        Sessions.Remove(sessionId);
                        return Content("END Too many incorrect PIN attempts. Session ended.", "text/plain");
                    }
                    if (string.IsNullOrEmpty(userInput)) {
                        response = "CON Enter your 4-digit PIN:";
                    } else if (userInput.Length == 4 && userInput.All(char.IsDigit)) {
                        if (userInput == correctPin) {
                            session["screen"] = "HOME";
                            session["authenticated"] = true;
                            session["pinAttempts"] = 0;
                            response = homeMenu;
                        } else {
                            session["pinAttempts"] = attempts + 1;
                            response = $"CON Incorrect PIN. Attempt {attempts + 1} of {MaxPinAttempts}. Try again:";
                        }
                    } else {
                        response = "CON Invalid PIN format. Enter your 4-digit PIN:";
                    }
                    break;
                case "HOME":
                    if (!session.ContainsKey("authenticated") || !(bool)session["authenticated"]) {
                        session["screen"] = "PIN";
                        response = "CON Enter your 4-digit PIN:";
                        break;
                    }
                    if (userInput == "")
                        response = homeMenu;
                    else if (userInput == "1")
                    {
                        session["screen"] = "CHOOSE_BANK";
                        response = "CON Select Bank:\n" + string.Join("\n", MockDatabase.Banks.Select((b, i) => $"{i + 1}. {b.Name}"));
                        response += "\n0. Home\n99. Back";
                    }
                    else if (userInput == "2" || (session.ContainsKey("screen") && session["screen"].ToString() == "LOAN_STATUS_PAGING"))
                    {
                        // Loan status: show paginated loans for this user
                        var userLoans = MockLoanDB.GetLoans(phoneNumber);
                        int page = session.ContainsKey("loanStatusPage") ? (int)session["loanStatusPage"] : 0;
                        int pageSize = 2;
                        if (userInput == "9") page++;
                        if (userInput == "99" || userInput == "0") {
                            session["screen"] = "HOME";
                            session.Remove("loanStatusPage");
                            response = homeMenu;
                            break;
                        }
                        if (userLoans.Count == 0)
                        {
                            response = "END You have no loans with NIB, Dashen, CBE, or Awash.";
                            Sessions.Remove(sessionId);
                        }
                        else
                        {
                            session["screen"] = "LOAN_STATUS_PAGING";
                            session["loanStatusPage"] = page;
                            var loansToShow = userLoans.Skip(page * pageSize).Take(pageSize).ToList();
                            response = "CON Your Loan Status:";
                            for (int i = 0; i < loansToShow.Count; i++)
                            {
                                var loan = loansToShow[i];
                                var outstanding = loan.Amount + loan.Interest - loan.Repaid;
                                response += $"\n{(page * pageSize) + i + 1}. {loan.BankName}, {loan.ProductName}, Outstanding: {outstanding}";
                            }
                            if ((page + 1) * pageSize < userLoans.Count)
                            {
                                response += "\n9. More";
                            }
                            response += "\n0. Home\n99. Back";
                        }
                    }
                    else if (userInput == "3")
                    {
                        // Repay Loan: show all active loans for this user and prompt for selection
                        var userLoans = MockLoanDB.GetLoans(phoneNumber).Where(l => l.Status == "Active" && (l.Amount + l.Interest - l.Repaid) > 0).ToList();
                        if (userLoans.Count == 0)
                        {
                            response = "END No active loans to repay.";
                            Sessions.Remove(sessionId);
                        }
                        else
                        {
                            session["screen"] = "REPAY_SELECT_LOAN";
                            session["repayLoans"] = userLoans;
                            response = "CON Select loan to repay:";
                            for (int i = 0; i < userLoans.Count; i++)
                            {
                                var loan = userLoans[i];
                                var outstanding = loan.Amount + loan.Interest - loan.Repaid;
                                response += $"\n{i + 1}. {loan.BankName} - {loan.ProductName} (Outstanding: {outstanding})";
                            }
                            response += "\n0. Home\n99. Back";
                        }
                    }
                    else if (userInput == "4")
                    {
                        var balance = MockBalanceDB.GetBalance(phoneNumber);
                        response = $"END Your account balance is: {balance}";
                        Sessions.Remove(sessionId);
                    }
                    else if (userInput == "5")
                    {
                        // Transaction history
                        if (!UserTransactions.ContainsKey(phoneNumber) || UserTransactions[phoneNumber].Count == 0) {
                            response = "END No transactions found.";
                            Sessions.Remove(sessionId);
                        } else {
                            response = "END Transaction History:\n" + string.Join("\n", UserTransactions[phoneNumber].TakeLast(5));
                            Sessions.Remove(sessionId);
                        }
                    }
                    else if (userInput == "6")
                    {
                        session["screen"] = "CHANGE_PIN";
                        response = "CON Enter new 4-digit PIN:";
                    }
                    else if (userInput == "0")
                    {
                        response = "END Thank you for using Microloan USSD.";
                        Sessions.Remove(sessionId);
                    }
                    else
                        response = $"CON Invalid choice.\n{homeMenu.Substring(4)}";
                    break;
                case "CHANGE_PIN":
                    if (string.IsNullOrEmpty(userInput)) {
                        response = "CON Enter new 4-digit PIN:";
                    } else if (userInput.Length == 4 && userInput.All(char.IsDigit)) {
                        UserPins[phoneNumber] = userInput;
                        response = "END PIN changed successfully.";
                        Sessions.Remove(sessionId);
                    } else {
                        response = "CON Invalid PIN format. Enter new 4-digit PIN:";
                    }
                    break;
                case "REPAY_SELECT_LOAN":
                    var repayLoans = (List<MockLoan>)session["repayLoans"];
                    int repayPage = session.ContainsKey("repayPage") ? (int)session["repayPage"] : 0;
                    int repayPageSize = 2;
                    if (userInput == "9") repayPage++;
                    if (userInput == "99" || userInput == "0") {
                        session["screen"] = "HOME";
                        session.Remove("repayPage");
                        response = homeMenu;
                        break;
                    }
                    var loansToShowRepay = repayLoans.Skip(repayPage * repayPageSize).Take(repayPageSize).ToList();
                    if (int.TryParse(userInput, out int repayChoice) && repayChoice >= 1 && repayChoice <= loansToShowRepay.Count)
                    {
                        session["selectedRepayLoan"] = loansToShowRepay[repayChoice - 1];
                        session["screen"] = "REPAY_ENTER_AMOUNT";
                        var loan = (MockLoan)session["selectedRepayLoan"];
                        var outstanding = loan.Amount + loan.Interest - loan.Repaid;
                        response = $"CON Enter amount to repay (Outstanding: {outstanding}):";
                    }
                    else
                    {
                        response = "CON Select loan to repay:";
                        for (int i = 0; i < loansToShowRepay.Count; i++)
                        {
                            var loan = loansToShowRepay[i];
                            var outstanding = loan.Amount + loan.Interest - loan.Repaid;
                            response += $"\n{(repayPage * repayPageSize) + i + 1}. {loan.BankName} - {loan.ProductName} (Outstanding: {outstanding})";
                        }
                        if ((repayPage + 1) * repayPageSize < repayLoans.Count)
                        {
                            response += "\n9. More";
                        }
                        response += "\n0. Home\n99. Back";
                    }
                    response += "\n0. Home\n99. Back";
                    session["repayPage"] = repayPage;
                    break;
                case "REPAY_ENTER_AMOUNT":
                    var repayLoan = (MockLoan)session["selectedRepayLoan"];
                    var repayOutstanding = repayLoan.Amount + repayLoan.Interest - repayLoan.Repaid;
                    if (decimal.TryParse(userInput, out decimal repayAmount) && repayAmount > 0 && repayAmount <= repayOutstanding)
                    {
                        // Update the mock DB
                        MockLoanDB.RepayLoan(phoneNumber, repayLoan, repayAmount);
                        response = $"END Repayment of {repayAmount} for loan {repayLoan.ProductName} at {repayLoan.BankName} successful. Outstanding: {repayOutstanding - repayAmount}";
                        Sessions.Remove(sessionId);
                    }
                    else
                    {
                        response = $"CON Invalid amount. Enter a number between 1 and {repayOutstanding}:";
                        response += "\n0. Home\n99. Back";
                    }
                    break;
                case "CHOOSE_BANK":
                    if (userInput == "0") {
                        session["screen"] = "HOME";
                        response = homeMenu;
                        Sessions.Remove(sessionId);
                        break;
                    }
                    if (userInput == "99") {
                        session["screen"] = "HOME";
                        response = homeMenu;
                        Sessions.Remove(sessionId);
                        break;
                    }
                    if (int.TryParse(userInput, out int bankChoice) && bankChoice >= 1 && bankChoice <= MockDatabase.Banks.Count)
                    {
                        session["selectedBank"] = MockDatabase.Banks[bankChoice - 1];
                        session["screen"] = "CHOOSE_PRODUCT";
                        var bank = (Bank)session["selectedBank"];
                        var products = bank.LoanProducts;
                        session["eligibleOffers"] = products;
                        response = "CON Choose a loan product:\n" + string.Join("\n", products.Select((p, i) => $"{i + 1}. {p.Name} (Amount: {p.MinAmount}-{p.MaxAmount})"));
                        response += "\n0. Home\n99. Back";
                    }
                    else
                    {
                        response = "CON Invalid bank choice.\n" + string.Join("\n", MockDatabase.Banks.Select((b, i) => $"{i + 1}. {b.Name}"));
                        response += "\n0. Home\n99. Back";
                    }
                    break;
                case "CHOOSE_PRODUCT":
                    var offers = (List<LoanProduct>)session["eligibleOffers"];
                    if (userInput == "0") {
                        session["screen"] = "HOME";
                        response = homeMenu;
                        Sessions.Remove(sessionId);
                        break;
                    }
                    if (userInput == "99") {
                        session["screen"] = "CHOOSE_BANK";
                        response = "CON Select Bank:\n" + string.Join("\n", MockDatabase.Banks.Select((b, i) => $"{i + 1}. {b.Name}"));
                        response += "\n0. Home\n99. Back";
                        break;
                    }
                    if (int.TryParse(userInput, out int prodChoice) && prodChoice >= 1 && prodChoice <= offers.Count)
                    {
                        session["selectedProduct"] = offers[prodChoice - 1];
                        session["screen"] = "APPLY_LOAN_AMOUNT";
                        var prod = (LoanProduct)session["selectedProduct"];
                        response = $"CON Enter amount (range: {prod.MinAmount}-{prod.MaxAmount})";
                        response += "\n0. Home\n99. Back";
                    }
                    else
                    {
                        response = "CON Invalid choice.\n" + string.Join("\n", offers.Select((p, i) => $"{i + 1}. {p.Name} (Amount: {p.MinAmount}-{p.MaxAmount})"));
                        response += "\n0. Home\n99. Back";
                    }
                    break;
                case "APPLY_LOAN_AMOUNT":
                    var product = (LoanProduct)session["selectedProduct"];
                    if (userInput == "0") {
                        session["screen"] = "HOME";
                        response = homeMenu;
                        Sessions.Remove(sessionId);
                        break;
                    }
                    if (userInput == "99") {
                        session["screen"] = "CHOOSE_PRODUCT";
                        response = "CON Choose a loan product:\n" + string.Join("\n", ((List<LoanProduct>)session["eligibleOffers"]).Select((p, i) => $"{i + 1}. {p.Name} (Amount: {p.MinAmount}-{p.MaxAmount})"));
                        response += "\n0. Home\n99. Back";
                        break;
                    }
                    if (decimal.TryParse(userInput, out decimal amount) && amount >= product.MinAmount && amount <= product.MaxAmount)
                    {
                        session["loanAmount"] = amount;
                        session["screen"] = "APPLY_LOAN_CONFIRM";
                        response = $"CON Confirm:\nProduct: {product.Name}\nAmount: {amount}\n1. Confirm\n0. Cancel";
                        response += "\n0. Home\n99. Back";
                    }
                    else
                    {
                        response = $"CON Invalid amount. Enter a number between {product.MinAmount} and {product.MaxAmount}:";
                        response += "\n0. Home\n99. Back";
                    }
                    break;
                case "APPLY_LOAN_CONFIRM":
                    if (userInput == "0") {
                        session["screen"] = "HOME";
                        response = homeMenu;
                        Sessions.Remove(sessionId);
                        break;
                    }
                    if (userInput == "99") {
                        session["screen"] = "APPLY_LOAN_AMOUNT";
                        var selectedProduct = (LoanProduct)session["selectedProduct"];
                        response = $"CON Enter amount (range: {selectedProduct.MinAmount}-{selectedProduct.MaxAmount})";
                        response += "\n0. Home\n99. Back";
                        break;
                    }
                    if (userInput == "1")
                    {
                        var prod = (LoanProduct)session["selectedProduct"];
                        var bank = (Bank)session["selectedBank"];
                        var loanAmount = (decimal)session["loanAmount"];
                        // Actually add the loan to the user's account
                        MockLoanDB.AddLoan(phoneNumber, bank.Name, prod.Name, loanAmount, prod.MinAmount * 0.08m); // Example interest logic
                        response = $"END Application submitted!\nBank: {bank.Name}\nProduct: {prod.Name}\nAmount: {loanAmount}\nStatus: Active. Amount credited to your balance.";
                        Sessions.Remove(sessionId);
                    }
                    else
                    {
                        response = "CON Invalid choice.\n1. Confirm\n0. Cancel";
                        response += "\n0. Home\n99. Back";
                    }
                    break;
                default:
                    response = "END Session error. Please try again.";
                    Sessions.Remove(sessionId);
                    break;
                }
                Console.WriteLine($"USSD Response: {response.Replace("\n", " | ")}");
                return Content(response, "text/plain");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"USSD ERROR: {ex.Message}\n{ex.StackTrace}");
                return Content("END Service error. Please try again later.", "text/plain");
            }
        }
    }
}
