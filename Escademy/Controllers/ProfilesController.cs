using Escademy.Dal;
using Escademy.Models;
using Escademy.Services;
using Escademy.ViewModels;
using Escademy.Helpers;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Microsoft.Ajax.Utilities;

namespace Escademy.Controllers
{
    public class ProfilesController : Controller
    {
        // Dal object initialize.
        private EscademyMDB db = new EscademyMDB();
        // GET: Profiles

        public ActionResult Index()
        {
            return RedirectToAction("Profile");
        }

        public ActionResult EditProfile()
        {
            return View();
        }

        public new ActionResult Profile(int id = 0)
        {
            ViewBag.id = id;
            List<ReviewDetailModel> reviewList = new List<ReviewDetailModel>();
            using (var conn = new MySqlConnection(ConnectionString.Get("EscademyDB")))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM esc_accounts WHERE id=@userId";
                    cmd.Parameters.AddWithValue("@userId", (int)ViewBag.id);
                    var reader = cmd.ExecuteReader();

                    if (reader.Read())
                    {
                        var user = new User()
                        {
                            Id = reader.GetInt32("Id"),
                            Email = reader.GetString("Email"),
                            FirstName = reader.GetString("FirstName"),
                            LastName = reader.GetString("LastName"),
                            Level = reader.GetInt32("Level"),
                            Picture = reader.GetString("ProfilePicture"),
                            Description = reader.GetString("Description"),
                            Country = reader.GetString("Country")
                        };

                        if (user.Picture.Length > 0)
                        {
                            user.Picture = $"data:image/png;base64,{user.Picture}";
                        }
                        else
                        {
                            user.Picture = "/Content/Images/portrait_2.png";
                        }

                        ViewBag.user = user;

                        // This method is used for fetching the games which he is providing coaching for.
                        var userGames = (from ep in db.esc_profilegames
                                         join g in db.esc_games on ep.gameId equals g.Id
                                         where ep.accountId.Equals(user.Id)
                                         select new GameVM
                                         {
                                             AccountId = user.Id,
                                             GameId = ep.gameId,
                                             Name = g.Abbreviation
                                         }).DistinctBy(c => c.GameId).ToList();
                        // This method is used for fetching the trophies for user id.
                        var userTrophies = db.esc_profileTrophies.Where(x => x.account_Id.Equals(id)).ToList();
                        var gamesAbbr = db.esc_games.ToList();
                        // This method is used for fetching ratings for the games which he is providing coaching for.
                        //var gameRatings = (from ep in db.esc_profilegamesRating
                        //                   join pg in db.esc_profilegames on ep.profilegamesId equals pg.Id
                        //                   where pg.accountId.Equals(user.Id)
                        //                   select new GameVM
                        //                   {
                        //                       Id = ep.Id,
                        //                       AccountId = user.Id,
                        //                       GameId = pg.gameId,
                        //                       Name = "TEST",
                        //                       Rating = ep.Rating
                        //                   }).ToList();
                        //ViewBag.gameRatings = gameRatings;
                        ViewBag.userGames = userGames;
                        ViewBag.userTrophies = userTrophies;
                        ViewBag.gamesAbbr = gamesAbbr;
                    }
                    else { ViewBag.user = null; }

                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM `esc_orders` as eo inner join esc_accounts as ea on eo.payer_account_id=ea.Id WHERE receiver_id=@userId and review_stars!=0";
                    cmd.Parameters.AddWithValue("@userId", (int)ViewBag.id);
                    var rdr = cmd.ExecuteReader();
                    while (rdr.Read())
                    {
                        reviewList.Add(new ReviewDetailModel()
                        {
                            ReviewComments = rdr.GetString("review_comments"),
                            ReviewStars = rdr.GetInt32("review_stars"),
                            ReviewDate = rdr.GetDateTime("CompletedDate"),
                            Reviewer = rdr.GetString("FirstName") + " " + rdr.GetString("LastName"),
                            ProfilePicture = rdr.GetString("ProfilePicture")
                        });
                    }
                    var reviewListCount = reviewList.Count();
                    float total = reviewList.Sum(x => Convert.ToInt32(x.ReviewStars));
                    var countList = reviewList.Count;

                    float avgStar;
                    avgStar = total / countList;
                    ViewBag.avgStarVal = avgStar.RoundValue();
                    ViewBag.avgCountVal = countList;
                    ViewBag.avgStarStr = avgStar.RoundString();
                }

                conn.Close();
            }


            return View(reviewList);
        }

        public ActionResult Earnings(int id = 0)
        {
            if ((User)Session["user"] != null)
            {
                ViewBag.id = id;
                List<OrderDataVM> orderList = new List<OrderDataVM>();
                List<EarningListModel> earningList = new List<EarningListModel>();
                using (var conn = new MySqlConnection(ConnectionString.Get("EscademyDB")))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT * FROM esc_Transactions as es inner join esc_orders as eo where es.OrderId=eo.Id and eo.receiver_id=@userid";
                        cmd.Parameters.AddWithValue("@userId", (int)ViewBag.id);
                        var reader = cmd.ExecuteReader();

                        while (reader.Read())
                        {
                            orderList.Add(new OrderDataVM()
                            {
                                Id = reader.GetInt32("Id"),
                                Amount = reader.GetInt32("Amount"),
                                Status = reader.GetString("Status"),
                                OrderId = reader.GetInt32("OrderId"),
                                Date = String.Format("{0:MMM d, yyyy}", @reader.GetDateTime("Date")),
                                OrderStatus = reader.GetString("order_status"),
                                TransactionId = reader.GetString("txn_id"),
                                UserId = id
                            });
                        }
                        foreach (var i in orderList)
                        {
                            if (i.Status == "Completed" && i.OrderStatus == "Completed")
                            {
                                earningList.Add(new EarningListModel()
                                {
                                    Date = i.Date,
                                    Amount = i.Amount,
                                    For = "Funds Cleared",
                                    TransactionId = i.TransactionId,
                                    UserId = i.UserId
                                });
                            }
                            else if (i.Status == "Pending" && i.OrderStatus == "Completed")
                            {
                                earningList.Add(new EarningListModel()
                                {
                                    Date = i.Date,
                                    Amount = i.Amount,
                                    For = "Funds Pending Clearance",
                                    TransactionId = i.TransactionId,
                                    UserId = i.UserId
                                });
                            }
                            else if (i.OrderStatus == "Delivered" || i.OrderStatus == "Active")
                            {
                                earningList.Add(new EarningListModel()
                                {
                                    Date = i.Date,
                                    Amount = i.Amount,
                                    For = "Order Revenue",
                                    TransactionId = i.TransactionId,
                                    UserId = i.UserId
                                });
                            }
                        }
                    }
                    conn.Close();
                }
                return View(earningList);
            }
            else
                return RedirectToAction("Index", "Auth");
        }

        public ActionResult FilterEarnings(int id = 0, string statusFilter = "", string monthFilter = "", string yearFilter = "")
        {
            ViewBag.id = id;
            List<OrderDataVM> orderList = new List<OrderDataVM>();
            List<EarningListModel> earningList = new List<EarningListModel>();
            using (var conn = new MySqlConnection(ConnectionString.Get("EscademyDB")))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM esc_Transactions as es inner join esc_orders as eo where es.OrderId=eo.Id and eo.receiver_id=@userid";
                    cmd.Parameters.AddWithValue("@userId", (int)ViewBag.id);
                    var reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        orderList.Add(new OrderDataVM()
                        {
                            Id = reader.GetInt32("Id"),
                            Amount = reader.GetInt32("Amount"),
                            Status = reader.GetString("Status"),
                            OrderId = reader.GetInt32("OrderId"),
                            Date = String.Format("{0:MMMM d, yyyy}", @reader.GetDateTime("Date")),
                            OrderStatus = reader.GetString("order_status"),
                            TransactionId = reader.GetString("txn_id"),
                            UserId = id
                        });
                    }
                    foreach (var i in orderList)
                    {
                        if (i.Status == "Completed" && i.OrderStatus == "Completed")
                        {
                            earningList.Add(new EarningListModel()
                            {
                                Date = i.Date,
                                Amount = i.Amount,
                                For = "Funds Cleared",
                                TransactionId = i.TransactionId,
                                UserId = i.UserId
                            });
                        }
                        else if (i.Status == "Pending" && i.OrderStatus == "Completed")
                        {
                            earningList.Add(new EarningListModel()
                            {
                                Date = i.Date,
                                Amount = i.Amount,
                                For = "Funds Pending Clearance",
                                TransactionId = i.TransactionId,
                                UserId = i.UserId
                            });
                        }
                        else if (i.OrderStatus == "Delivered" || i.OrderStatus == "Active")
                        {
                            earningList.Add(new EarningListModel()
                            {
                                Date = i.Date,
                                Amount = i.Amount,
                                For = "Order Revenue",
                                TransactionId = i.TransactionId,
                                UserId = i.UserId
                            });
                        }
                    }
                    if (monthFilter == "All Months")
                        monthFilter = "";
                    if (statusFilter == "Everything")
                        statusFilter = "";

                    earningList = earningList.Where(x => x.Date.Contains(monthFilter) && x.Date.Contains(yearFilter) && x.For.Contains(statusFilter)).ToList();
                }
                conn.Close();
            }
            return PartialView("_EarningsPartialView", earningList);
        }

        public ActionResult GetEarningsData(int id = 0)
        {
            EarningDataModel earningDataModel = new EarningDataModel();
            earningDataModel.AvailableForWithdrawal = GetAvailableForWithdrawalAmount(id);
            earningDataModel.Withdrawal = GetWithdrawalAmount(id);
            earningDataModel.PendingClearances = GetPendingClearanceAmount(id);
            earningDataModel.TotalWithdrawn = GetTotalWithdrawn(id);
            earningDataModel.NetIncome = earningDataModel.AvailableForWithdrawal + earningDataModel.TotalWithdrawn;

            return PartialView("_EarningsDataPartialView", earningDataModel);
        }

        public decimal GetAvailableForWithdrawalAmount(int id = 0)
        {
            decimal availableForWithdrawalAmount = 0;
            using (var conn = new MySqlConnection(ConnectionString.Get("EscademyDB")))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM esc_wallet as ew where ew.accountId=@userid";
                    cmd.Parameters.AddWithValue("@userId", id);
                    var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        availableForWithdrawalAmount = reader.GetDecimal("wallet_amount");
                    }
                }
                conn.Close();
            }
            return availableForWithdrawalAmount;
        }

        public decimal GetTotalWithdrawn(int id = 0)
        {
            decimal totalWithdrawn = 0;
            using (var conn = new MySqlConnection(ConnectionString.Get("EscademyDB")))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT TotalWithdrawn FROM esc_wallet as ew where ew.accountId=@userid";
                    cmd.Parameters.AddWithValue("@userId", id);
                    var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        totalWithdrawn = reader.GetDecimal("TotalWithdrawn");
                    }
                }
                conn.Close();
            }
            return totalWithdrawn;
        }

        public decimal GetWithdrawalAmount(int id = 0)
        {
            decimal withdrawalAmount = 0;
            using (var conn = new MySqlConnection(ConnectionString.Get("EscademyDB")))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT wallet_amount FROM esc_wallet as es where es.accountId=@userid";
                    cmd.Parameters.AddWithValue("@userId", id);
                    var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        withdrawalAmount = reader.GetDecimal("wallet_amount");
                    }
                }
                conn.Close();
            }
            return withdrawalAmount;
        }


        public decimal GetPendingClearanceAmount(int id = 0)
        {
            decimal pendingClearanceAmount = 0;
            List<OrderDataVM> orderList = new List<OrderDataVM>();
            using (var conn = new MySqlConnection(ConnectionString.Get("EscademyDB")))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT* FROM esc_Transactions as es inner join esc_orders as eo where es.OrderId = eo.Id and eo.receiver_id = @userid";
                    cmd.Parameters.AddWithValue("@userId", id);
                    var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        orderList.Add(new OrderDataVM()
                        {
                            Id = reader.GetInt32("Id"),
                            Amount = reader.GetDecimal("Amount"),
                            Status = reader.GetString("Status"),
                            OrderId = reader.GetInt32("OrderId"),
                            Date = String.Format("{0:MMMM d, yyyy}", @reader.GetDateTime("Date")),
                            OrderStatus = reader.GetString("order_status"),
                            TransactionId = reader.GetString("txn_id"),
                            UserId = id
                        });
                    }
                    foreach (var i in orderList)
                    {
                        if (i.Status == "Pending" && i.OrderStatus == "Completed")
                        {
                            pendingClearanceAmount += i.Amount;
                        }
                    }

                }
                conn.Close();
            }
            return pendingClearanceAmount;
        }


        public ActionResult GetWithdrawDataUsers()
        {
            List<WithdrawalDataModel> userList = new List<WithdrawalDataModel>();
            using (var conn = new MySqlConnection(ConnectionString.Get("EscademyDB")))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM esc_accounts as ea inner join esc_wallet as ew on ea.Id=ew.accountId where ea.IfWithdraw=1";
                    var reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        userList.Add(new WithdrawalDataModel()
                        {
                            EmailID = reader.GetString("Email"),
                            Amount = reader.GetDecimal("wallet_amount"),
                        });
                    }

                }
                conn.Close();
            }
            return PartialView("_WithdrawalListDataPartialView", userList);
        }

        public int SetIsWithdrawTrue(int id = 0, string paypalEmail = "")
        {
            ViewBag.id = id;
            int status = 0;
            int value = 0;
            int walletAmount = 0;
            using (var conn = new MySqlConnection(ConnectionString.Get("EscademyDB")))
            {
                conn.Open();


                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM esc_accounts as ea inner join esc_wallet as ew on ea.Id=ew.accountId WHERE ea.Id=@UserId ";
                    cmd.Parameters.AddWithValue("@UserId", id);

                    var reader = cmd.ExecuteReader();

                    if (reader.Read())
                    {
                        value = reader.GetInt32("IfWithdraw");
                        walletAmount = reader.GetInt32("wallet_amount");
                    }
                }

                if (value == 0 && walletAmount > 0)
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "Update esc_accounts as ea set ea.IfWithdraw=1,ea.PaypalEmail=@PaypalEmail where ea.Id=@UserId ";
                        cmd.Parameters.AddWithValue("@UserId", id);
                        cmd.Parameters.AddWithValue("@PaypalEmail", paypalEmail);
                        cmd.ExecuteNonQuery();
                    }
                    status = 1;
                }
                else
                {
                    status = 0;
                }
                conn.Close();
            }
            return status;
        }

        public ActionResult DeclinePaymentRequest(string emailId)
        {
            using (var conn = new MySqlConnection(ConnectionString.Get("EscademyDB")))
            {
                conn.Open();


                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "Update esc_accounts as ea set ea.IfWithdraw=0 where ea.Email=@EmailId";
                    cmd.Parameters.AddWithValue("@EmailId", emailId);
                    var reader = cmd.ExecuteReader();
                }
                conn.Close();
            }
            var user = (User)Session["user"];
            var id = user.Id;
            return RedirectToAction("Earnings", "Profiles", new { id = id });
        }

        public ActionResult CreatePaymentRequest(string emailId, decimal amount)
        {
            string paypalEmailID = String.Empty;
            using (var conn = new MySqlConnection(ConnectionString.Get("EscademyDB")))
            {
                conn.Open();


                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM esc_accounts WHERE Email=@EmailId";
                    cmd.Parameters.AddWithValue("@EmailId", emailId);
                    var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        paypalEmailID = reader.GetString("PaypalEmail");
                    }
                }
                conn.Close();
            }
            var payment = PayPalPaymentService.CreatePayment(GetBaseUrl(), "sale", paypalEmailID, amount);

            return Redirect(payment.GetApprovalUrl());
        }

        public string GetBaseUrl()
        {
            return Request.Url.Scheme + "://" + Request.Url.Authority;
        }

        public ActionResult PaymentSuccessful(string paymentId, string token, string PayerID)
        {
            // Execute Payment
            var payment = PayPalPaymentService.ExecutePayment(paymentId, PayerID);

            return View();
        }

        public ActionResult PaymentCancelled()
        {
            // TODO: Handle cancelled payment
            return View();
        }
    }
}