using Escademy.Models;
using MySql.Data.MySqlClient;
using PayPal.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Escademy.Services
{
    public class PayPalPaymentService
    {

        public static Payment CreatePayment(string baseUrl, string intent,string emailId,decimal amount)
        {
            
            // ### Api Context
            // Pass in a `APIContext` object to authenticate 
            // the call and to send a unique request id 
            // (that ensures idempotency). The SDK generates
            // a request id if you do not pass one explicitly. 
            var apiContext = PayPalConfiguration.GetAPIContext();           
            // Payment Resource
            var payment = new Payment()
            {
                intent = intent,    // `sale` or `authorize`
                payer = new Payer() { payment_method = "paypal" },
                transactions = GetTransactionsList(emailId, amount),
                redirect_urls = GetReturnUrls(baseUrl, intent)
            };

            // Create a payment using a valid APIContext
            var createdPayment = payment.Create(apiContext);
            return createdPayment;
        }

        private static List<Transaction> GetTransactionsList(string emailId, decimal amount)
        {
            // A transaction defines the contract of a payment
            // what is the payment for and who is fulfilling it. 
            var transactionList = new List<Transaction>();

            // The Payment creation API requires a list of Transaction; 
            // add the created Transaction to a List
            transactionList.Add(new Transaction()
            {
                description = "Transaction description.",
                invoice_number = GetRandomInvoiceNumber(),
                amount = new Amount()
                {
                    currency = "USD",
                    total = Convert.ToString(amount).Replace(",","."),
                },
                payee = new Payee()
                {
                    email = emailId,
                },
                note_to_payee = emailId

            });
            return transactionList;
        }

        private static RedirectUrls GetReturnUrls(string baseUrl, string intent)
        {
            var returnUrl = intent == "sale" ? "/Profiles/PaymentSuccessful" : "/Home/AuthorizeSuccessful";

            // Redirect URLS
            // These URLs will determine how the user is redirected from PayPal 
            // once they have either approved or canceled the payment.
            return new RedirectUrls()
            {
                cancel_url = baseUrl + "/Home/PaymentCancelled",
                return_url = baseUrl + returnUrl
            };
        }

        public static Payment ExecutePayment(string paymentId, string payerId)
        {
            // ### Api Context
            // Pass in a `APIContext` object to authenticate 
            // the call and to send a unique request id 
            // (that ensures idempotency). The SDK generates
            // a request id if you do not pass one explicitly. 
            var apiContext = PayPalConfiguration.GetAPIContext();

            var paymentExecution = new PaymentExecution() { payer_id = payerId };
            var payment = new Payment() { id = paymentId };

            // Execute the payment.
            var executedPayment = payment.Execute(apiContext, paymentExecution);         

            var paymentDetails = Payment.Get(apiContext, paymentId);
            var emailId = paymentDetails.transactions.Select(x => x.note_to_payee).FirstOrDefault();
            int id = 0;
            decimal totalWithdrawn = 0;
            decimal walletAmount = 0;
            using (var conn = new MySqlConnection(ConnectionString.Get("EscademyDB")))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM esc_accounts WHERE PaypalEmail=@PaypalEmail";
                    cmd.Parameters.AddWithValue("@PaypalEmail", emailId);
                    var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        id = reader.GetInt32("Id");
                    }
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "Update esc_accounts as ea set ea.IfWithdraw=0 where ea.PaypalEmail=@EmailId";
                    cmd.Parameters.AddWithValue("@EmailId",emailId);
                    cmd.ExecuteNonQuery();
                   
                }
                // Gets the TotalWithdrawn from wallet with accountid and the wallet_amount.
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT TotalWithdrawn, wallet_amount FROM esc_wallet WHERE accountId = @accountId";
                    cmd.Parameters.AddWithValue("@accountId", id);
                    var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        totalWithdrawn = reader.GetDecimal("TotalWithdrawn");
                        walletAmount = reader.GetDecimal("wallet_amount");
                    }
                }
                
                // Update the totalwithdrawn when executed
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "UPDATE esc_wallet SET TotalWithdrawn=@TotalWithdrawn WHERE accountId=@accountId";
                    cmd.Parameters.AddWithValue("@accountId", id);
                    cmd.Parameters.AddWithValue("@TotalWithdrawn", totalWithdrawn + walletAmount);
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "Update esc_wallet as ew set ew.wallet_amount=0,ew.updated_date=NOW() where ew.accountId=@Id";
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.ExecuteNonQuery();
                }
                conn.Close();
            }

            return executedPayment;
        }

        public static string GetRandomInvoiceNumber()
        {
            return new Random().Next(999999).ToString();
        }

    }
}