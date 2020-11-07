using Escademy.Dal;
using Escademy.Helpers;
using Escademy.Models;
using Escademy.ViewModels;
using Microsoft.AspNet.SignalR;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Mvc;

namespace Escademy.Controllers
{
    public class OrderController : Controller
    {
        private EscademyMDB db = new EscademyMDB();
        public ActionResult New(int id = 0, int gameId = 0, int accountId = 0, int quantity = 1)
        {
            List<ReviewDetailModel> reviewList = new List<ReviewDetailModel>();
            using (var conn = new MySqlConnection(ConnectionString.Get("EscademyDB")))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT a.Created_at, a.FirstName, a.Description, a.ProfilePicture, a.Country FROM esc_accounts a WHERE Id=@accId";
                    cmd.Parameters.AddWithValue("@accId", accountId);

                    var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        ViewBag.user = new User()
                        {
                            Country = reader.GetString("Country"),
                            Description = reader.GetString("Description"),
                            FirstName = reader.GetString("FirstName"),
                            Picture = reader.GetString("ProfilePicture")
                        };


                        var dt = reader.GetDateTime("Created_at");
                        ViewBag.date = $"{dt.ToString("MMM", CultureInfo.InvariantCulture)} {dt.Year}";
                    }
                }
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM `esc_orders` as eo inner join esc_accounts as ea on eo.payer_account_id=ea.Id WHERE receiver_id=@userId and review_stars!=0";
                    cmd.Parameters.AddWithValue("@userId", accountId);
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
            if (EnsureUserRights(UserLevel.Default))
            {
                ViewBag.payerid = GetCurrentUser().Id;
                var priceList = db.esc_profilegamesPricing.Where(x => x.profilegamesId.Equals(id)).ToList();
                ViewBag.pricings = priceList;
                ViewBag.gamePicture = db.esc_profilegamesFiles.Where(x => x.profilegamesId.Equals(id)).FirstOrDefault();
                LoadOrder(id, accountId, gameId, quantity);
                return View();
            }
            return RedirectToAction("Login", "Auth", new { redirectUrl = HttpUtility.HtmlEncode(Request.Url.ToString()) });
        }

        public ActionResult Confirm(int id = 0, int accountId = 0, int gameId = 0, int quantity = 1)
        {
            if (EnsureUserRights(UserLevel.Default))
            {
                ViewBag.serviceId = id;
                ViewBag.payerid = GetCurrentUser().Id;
                var price = db.esc_profilegamesPricing.Where(x => x.profilegamesId.Equals(id) && x.Hours.Equals(quantity)).FirstOrDefault().Price;
                if (price <= 0)
                    return RedirectToAction("Login", "Auth", new { redirectUrl = HttpUtility.HtmlEncode(Request.Url.ToString()) });
                ViewBag.price = price;
                ViewBag.gamePicture = db.esc_profilegamesFiles.Where(x => x.profilegamesId.Equals(id)).FirstOrDefault();
                LoadOrder(id, accountId, gameId, quantity);
                return View();
            }
            return RedirectToAction("Login", "Auth", new { redirectUrl = HttpUtility.HtmlEncode(Request.Url.ToString()) });
        }

        public ActionResult Details(string tx)
        {
            #region Insert Order using IPN CODE
            var order = new CoachOrder();

            // Receive IPN request from PayPal and parse all the variables returned
            var formVals = new Dictionary<string, string>();
            formVals.Add("cmd", "_notify-synch"); //notify-synch_notify-validate
            formVals.Add("at", "wABDeX0w9ivh2l8iWcH1kQOwVmTlOrox0oWCUQeDNqyZHKmBd8GjRy6s6c4");
            formVals.Add("tx", Request["tx"]);

            // if you want to use the PayPal sandbox change this from false to true
            string response = GetPayPalResponse(formVals, useSandbox: false);

            if (response.Contains("SUCCESS"))
            {
                string transactionID = GetPDTValue(response, "txn_id"); // txn_id //d
                string sAmountPaid = GetPDTValue(response, "mc_gross"); // d
                string sCurrency = GetPDTValue(response, "mc_currency");

                var custom_info = GetPDTValue(response, "custom").Split(','); // d
                string payerEmail = GetPDTValue(response, "payer_email"); // d
                string Item = GetPDTValue(response, "item_name");
                string payment_status = GetPDTValue(response, "payment_status");
                string receiver_email = GetPDTValue(response, "receiver_email");

                string sQuantity = custom_info[2];
                int.TryParse(sQuantity, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out int quantity);

                string sPayerId = custom_info[3];
                int.TryParse(sPayerId, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out int payer_account_id);

                //validate the order
                decimal amountPaid = 0;
                decimal.TryParse(sAmountPaid, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out amountPaid);

                order.txn_id = transactionID;
                order.mc_gross = amountPaid;
                order.mc_currency = sCurrency;
                order.payer_mail = payerEmail;
                order.receiver_id = int.Parse(custom_info[0]);
                order.game_id = int.Parse(custom_info[1]);
                order.service_id = int.Parse(custom_info[4]);
                
                order.date = DateTime.UtcNow;
                order.quantity = quantity;
                order.payer_account_id = payer_account_id;

                using (var conn = new MySqlConnection(ConnectionString.Get("EscademyDB")))
                {
                    conn.Open();

                    if (amountPaid >= PriceCalculator.CalculateTotalPrice(order.service_id, quantity, conn) - 0.1M &&
                        sCurrency == "USD" &&
                        payment_status == "Completed" &&
                        receiver_email.Equals("official@escademy.com", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var new_order = !check_if_order_exists(transactionID, conn);

                        if (new_order)
                        {
                            order.success = true;
                            int OrderId = insert_new_order(order, conn);
                            create_transaction(OrderId, order, conn);
                        }
                        else
                        {
                            order.success = false;
                        }

                        ViewBag.order = order;
                    }
                    else
                    {
                        order.success = false;
                        int OrderId = insert_new_order(order, conn);//Incorrect amount.. just log incident.
                    }

                    conn.Close();
                }
            }
            else
            {
                //error
            }
            #endregion

            #region Get Order-Item Detail by Transaction-Id
            var OrderItemDetail = new List<OrderDetailVM>();
            string BuyerName = "";
            decimal TotalAmount = 0;
            string OrderDate = "";
            int OrderNo = 0;
            int i = 0;
            using (var conn = new MySqlConnection(ConnectionString.Get("EscademyDB")))
            {
                conn.Open();
                #region Get Order-Item Detail by Transaction-Id
                using (var cmdActiveOrder = conn.CreateCommand())
                {
                    cmdActiveOrder.CommandText = "select concat(a1.FirstName,' ', a1.LastName) as BuyerName,o.Id as OrderNo,o.mc_gross,o.quantity,o.date as OrderedDate,(select pg.Title from esc_profilegames pg where /*pg.accountId=o.receiver_id and*/ pg.gameId=o.game_id limit 1) as ItemName from esc_orders o inner join esc_accounts a1 on o.payer_account_id=a1.Id where o.txn_id=@transactionId";
                    cmdActiveOrder.Parameters.AddWithValue("@transactionId", tx);
                    var reader = cmdActiveOrder.ExecuteReader();
                    while (reader.Read())
                    {
                        if (i == 0)
                        {
                            BuyerName = reader.GetString("BuyerName");
                            OrderDate = String.Format("{0:ddd, MMM d, yyyy}", @reader.GetDateTime("OrderedDate"));
                            OrderNo = reader.GetInt32("OrderNo");
                            i++;
                        }

                        TotalAmount = TotalAmount + reader.GetDecimal("mc_gross");
                        OrderItemDetail.Add(new OrderDetailVM()
                        {
                            ItemName = reader.GetString("ItemName"),
                            Quantity = reader.GetInt32("quantity"),
                            Price = reader.GetDecimal("mc_gross"),
                        });
                    }
                    ViewBag.OrderItemList = OrderItemDetail;
                    ViewBag.TotalAmount = TotalAmount.ToString().Replace(",",".");
                    ViewBag.OrderNo = OrderNo;
                    ViewBag.BuyerName = BuyerName;
                    ViewBag.OrderDate = OrderDate;
                }
                #endregion
                conn.Close();
            }
            #endregion
            return View();
        }

        // This method can be moved to base controller.
        private bool EnsureUserRights(UserLevel rights)
        {
            if (Session["user"] != null)
            {
                if ((Session["user"] as User).HasRole(rights))
                {
                    return true;
                }
            }
            return false;
        }

        // This method can be moved to base controller.
        private User GetCurrentUser()
        {
            return (User)Session["user"];
        }


        private void LoadOrder(int id, int accountId, int gameId, int quantity)
        {
            ViewBag.coachingId = id;
            ViewBag.accountId = accountId;
            ViewBag.gameId = gameId;
            ViewBag.quantity = quantity;

            using (var conn = new MySqlConnection(ConnectionString.Get("EscademyDB")))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT p.*, g.Picture, a.Created_at, a.FirstName, a.Description userdesc, a.ProfilePicture, a.Country FROM esc_profilegames p JOIN esc_accounts a ON a.Id=p.accountId JOIN esc_games g ON g.Id=p.gameId WHERE p.Id=@Id";
                    cmd.Parameters.AddWithValue("@Id", id);

                    var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        ViewBag.coach = new GameCoaching()
                        {
                            AccountId = accountId,
                            GameId = gameId,
                            Description = reader.GetString("Description"),
                            Title = reader.GetString("Title"),
                            SalaryUSD = reader.GetDecimal("SalaryUSD"),
                            Picture = reader.GetString("Picture")
                        };

                        ViewBag.user = new User()
                        {
                            Country = reader.GetString("Country"),
                            Description = reader.GetString("userdesc"),
                            FirstName = reader.GetString("FirstName"),
                            Picture = reader.GetString("ProfilePicture")
                        };

                        var dt = reader.GetDateTime("Created_at");
                        ViewBag.date = $"{dt.ToString("MMM", CultureInfo.InvariantCulture)} {dt.Year}";
                    }
                }
                conn.Close();
            }
        }

        #region IPN CODE
        string GetPayPalResponse(Dictionary<string, string> formVals, bool useSandbox)
        {

            string paypalUrl = useSandbox ? "https://www.sandbox.paypal.com/cgi-bin/webscr" : "https://www.paypal.com/cgi-bin/webscr";

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(paypalUrl);

            // Set values for the request back
            req.Method = "POST";
            req.ContentType = "application/x-www-form-urlencoded";

            byte[] param = Request.BinaryRead(Request.ContentLength);
            string strRequest = Encoding.ASCII.GetString(param);

            StringBuilder sb = new StringBuilder();
            sb.Append(strRequest);

            foreach (string key in formVals.Keys)
            {
                sb.AppendFormat("&{0}={1}", key, formVals[key]);
            }
            strRequest += sb.ToString();
            req.ContentLength = strRequest.Length;

            //for proxy
            //WebProxy proxy = new WebProxy(new Uri("http://urlort#");
            //req.Proxy = proxy;
            //Send the request to PayPal and get the response
            string response = "";
            using (StreamWriter streamOut = new StreamWriter(req.GetRequestStream(), System.Text.Encoding.ASCII))
            {

                streamOut.Write(strRequest);
                streamOut.Close();
                using (StreamReader streamIn = new StreamReader(req.GetResponse().GetResponseStream()))
                {
                    response = streamIn.ReadToEnd();
                }
            }

            return response;
        }
        string GetPDTValue(string pdt, string key)
        {

            string[] keys = pdt.Split('\n');
            string thisVal = "";
            string thisKey = "";
            foreach (string s in keys)
            {
                string[] bits = s.Split('=');
                if (bits.Length > 1)
                {
                    thisVal = bits[1];
                    thisKey = bits[0];
                    if (thisKey.Equals(key, StringComparison.InvariantCultureIgnoreCase))
                        break;
                }
            }
            return Server.UrlDecode(thisVal);

        }

        int insert_new_order(CoachOrder order, MySqlConnection connection)
        {
            int ordId = 0;
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "INSERT INTO esc_orders (txn_id, mc_gross, mc_currency, quantity, payer_email, payer_account_id, receiver_id, game_id, success, date, order_status) VALUES (@txnId, @mcGross, @mcCurrency, @qty, @payerMail, @payerid, @recieverId, @gameId, @success, @date, @ord_status)";
                cmd.Parameters.AddWithValue("@txnId", order.txn_id);
                cmd.Parameters.AddWithValue("@mcGross", order.mc_gross);
                cmd.Parameters.AddWithValue("@mcCurrency", order.mc_currency);
                cmd.Parameters.AddWithValue("@qty", order.quantity);
                cmd.Parameters.AddWithValue("@payerMail", order.payer_mail);
                cmd.Parameters.AddWithValue("@payerid", order.payer_account_id);

                cmd.Parameters.AddWithValue("@recieverId", order.receiver_id);
                cmd.Parameters.AddWithValue("@gameId", order.game_id);
                cmd.Parameters.AddWithValue("@success", order.success);
                cmd.Parameters.AddWithValue("@date", order.date);
                cmd.Parameters.AddWithValue("@ord_status", "New");

                cmd.ExecuteNonQuery();
                //--Get Recently-Inserted Order-Id--
                
                ordId = (int)cmd.LastInsertedId;
                ChatHub c = new ChatHub();
                c.SendNewOrderNotification(6, 5);
            }
            return ordId;
        }
        void create_transaction(int OrderId, CoachOrder order, MySqlConnection connection)
        {
            using (var cmd = connection.CreateCommand())
            {             
                decimal final_price = PriceCalculator.CalculateCutFromTotalPriceWithFee(PriceCalculator.PayType.PayPal, order.mc_gross);

                cmd.CommandText = "INSERT INTO esc_Transactions (Amount, Status, Date, ReceiverId, senderId, OrderId) VALUES (@amount, @status, @date, @boosterid, @buyerid, @order_id)";
                cmd.Parameters.AddWithValue("@amount", final_price);
                cmd.Parameters.AddWithValue("@status", payment_status.Pending.ToString());
                cmd.Parameters.AddWithValue("@date", DateTime.UtcNow);
                cmd.Parameters.AddWithValue("@boosterid", order.receiver_id);
                cmd.Parameters.AddWithValue("@buyerid", order.payer_account_id);
                cmd.Parameters.AddWithValue("@order_id", OrderId);

                cmd.ExecuteNonQuery();
            }
        }

        enum payment_status
        {
            Pending,
            Withdrawn
        }


        bool check_if_order_exists(string transactionId, MySqlConnection connection)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT count(*) FROM esc_orders WHERE txn_id=@txnId";
                cmd.Parameters.AddWithValue("@txnId", transactionId);

                var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    int count = reader.GetInt32(0);
                    return count > 0;
                }
                else return false;
            }
        }
        #endregion
    }
}