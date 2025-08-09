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

        // Example: per-user PINs, balances, and transaction history
        private static Dictionary<string, string> UserPins = new() {
            {"+251900000001", "1234"},
            {"+251900000002", "5678"},
            {"+251900000003", "4321"},
            {"+251949602907", "1235"}
        };
        private static Dictionary<string, decimal> UserBalances = new() {
            {"+251900000001", 5000m},
            {"+251900000002", 12000m},
            {"+251900000003", 300m}
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
                var homeMenu = "CON Welcome to Mobile Banking USSD.\n1. Check Balance\n2. Mini Statement\n3. Money Transfer\n4. Airtime Top-up\n5. Change PIN\n6. Loan Services\n0. Exit";
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
                        // Check Balance
                        var balance = UserBalances.ContainsKey(phoneNumber) ? UserBalances[phoneNumber] : 0m;
                        response = $"END Your account balance is: {balance:C}";
                        Sessions.Remove(sessionId);
                    }
                    else if (userInput == "2")
                    {
                        // Mini Statement
                        if (!UserTransactions.ContainsKey(phoneNumber) || UserTransactions[phoneNumber].Count == 0) {
                            response = "END No transactions found.";
                        } else {
                            response = "END Mini Statement:\n" + string.Join("\n", UserTransactions[phoneNumber].TakeLast(5));
                        }
                        Sessions.Remove(sessionId);
                    }
                    else if (userInput == "3")
                    {
                        // Money Transfer
                        session["screen"] = "TRANSFER_ENTER_NUMBER";
                        response = "CON Enter recipient phone number:";
                    }
                    else if (userInput == "4")
                    {
                        // Airtime Top-up
                        session["screen"] = "AIRTIME_ENTER_AMOUNT";
                        response = "CON Enter amount to top-up:";
                    }
                    else if (userInput == "5")
                    {
                        session["screen"] = "CHANGE_PIN";
                        response = "CON Enter new 4-digit PIN:";
                    }
                    else if (userInput == "6")
                    {
                        // Placeholder for future loan integration
                        response = "END Loan services coming soon.";
                        Sessions.Remove(sessionId);
                    }
                    else if (userInput == "0")
                    {
                        response = "END Thank you for using Mobile Banking USSD.";
                        Sessions.Remove(sessionId);
                    }
                    else
                        response = $"CON Invalid choice.\n{homeMenu.Substring(4)}";
                    break;
                case "TRANSFER_ENTER_NUMBER":
                    if (string.IsNullOrEmpty(userInput)) {
                        response = "CON Enter recipient phone number:";
                    } else {
                        session["recipientNumber"] = userInput;
                        session["screen"] = "TRANSFER_ENTER_AMOUNT";
                        response = "CON Enter amount to transfer:";
                    }
                    break;
                case "TRANSFER_ENTER_AMOUNT":
                    if (decimal.TryParse(userInput, out decimal transferAmount) && transferAmount > 0) {
                        var balance = UserBalances.ContainsKey(phoneNumber) ? UserBalances[phoneNumber] : 0m;
                        if (transferAmount > balance) {
                            response = "END Insufficient funds.";
                            Sessions.Remove(sessionId);
                        } else {
                            UserBalances[phoneNumber] = balance - transferAmount;
                            var recipient = session["recipientNumber"].ToString();
                            if (UserBalances.ContainsKey(recipient))
                                UserBalances[recipient] += transferAmount;
                            else
                                UserBalances[recipient] = transferAmount;
                            if (!UserTransactions.ContainsKey(phoneNumber)) UserTransactions[phoneNumber] = new List<string>();
                            UserTransactions[phoneNumber].Add($"Sent {transferAmount:C} to {recipient} on {DateTime.Now:yyyy-MM-dd HH:mm}");
                            if (!UserTransactions.ContainsKey(recipient)) UserTransactions[recipient] = new List<string>();
                            UserTransactions[recipient].Add($"Received {transferAmount:C} from {phoneNumber} on {DateTime.Now:yyyy-MM-dd HH:mm}");
                            response = $"END Transfer of {transferAmount:C} to {recipient} successful.";
                            Sessions.Remove(sessionId);
                        }
                    } else {
                        response = "CON Invalid amount. Enter amount to transfer:";
                    }
                    break;
                case "AIRTIME_ENTER_AMOUNT":
                    if (decimal.TryParse(userInput, out decimal airtimeAmount) && airtimeAmount > 0) {
                        var balance = UserBalances.ContainsKey(phoneNumber) ? UserBalances[phoneNumber] : 0m;
                        if (airtimeAmount > balance) {
                            response = "END Insufficient funds for airtime top-up.";
                            Sessions.Remove(sessionId);
                        } else {
                            UserBalances[phoneNumber] = balance - airtimeAmount;
                            if (!UserTransactions.ContainsKey(phoneNumber)) UserTransactions[phoneNumber] = new List<string>();
                            UserTransactions[phoneNumber].Add($"Airtime top-up of {airtimeAmount:C} on {DateTime.Now:yyyy-MM-dd HH:mm}");
                            response = $"END Airtime top-up of {airtimeAmount:C} successful.";
                            Sessions.Remove(sessionId);
                        }
                    } else {
                        response = "CON Invalid amount. Enter amount to top-up:";
                    }
                    break;
                case "CHANGE_PIN":
                    if (string.IsNullOrEmpty(userInput)) {
                        response = "CON Enter new 4-digit PIN:";
                    } else if (userInput.Length == 4 && userInput.All(char.IsDigit)) {
                        UserPins[phoneNumber] = userInput;
                        if (!UserTransactions.ContainsKey(phoneNumber)) UserTransactions[phoneNumber] = new List<string>();
                        UserTransactions[phoneNumber].Add($"PIN changed on {DateTime.Now:yyyy-MM-dd HH:mm}");
                        response = "END PIN changed successfully.";
                        Sessions.Remove(sessionId);
                    } else {
                        response = "CON Invalid PIN format. Enter new 4-digit PIN:";
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
