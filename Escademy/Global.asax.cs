using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Http;
using System.Web.SessionState;
using System.Timers;
using MySql.Data.MySqlClient;
using Escademy.Models;
using System;
using System.Collections.Generic;
using System.Data;

namespace Escademy
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            //-----Timer for clearing seller/coaches money after 1 week of completed orders---  (Time will automatically start after every 1 hour)
            Timer timer = new Timer(3600000);
            timer.Enabled = true;
            // Setup Event Handler for Timer Elapsed Event
            timer.Elapsed += new ElapsedEventHandler(timer_Elapsed);
            timer.Start();
            //-------------------------------------------------------
        }

        static void timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            using (var conn = new MySqlConnection(ConnectionString.Get("EscademyDB")))
            {
                if(conn.State == ConnectionState.Closed)
                {
                    conn.Open();
                }

                #region Get All Orders which are completed,Transaction Not-Completed(Pending) and 1 week completion time
                var OrdersList = GetOrders(conn);
                #endregion

                if (OrdersList.Count > 0)
                {
                    for (int i = 0; i < OrdersList.Count; i++)
                    {
                        SetTransactionCompleted(OrdersList[i].Id, conn);
                        AddAmountInSellerWallet(OrdersList[i].receiver_id, OrdersList[i].mc_gross, conn);
                    }
                }
                if (conn.State == ConnectionState.Open)
                {
                    conn.Close();
                }
            }
        }

        static List<CoachOrder> GetOrders(MySqlConnection connection)
        {
            #region Get All Orders which are completed,Transaction Not-Completed(Pending) and 1 week completion time
            var OrdersList = new List<CoachOrder>();
            using (var cmdOrders = connection.CreateCommand())
            {
                cmdOrders.CommandText = "select o.Id as OrderId,o.receiver_id as SellerId,o.mc_gross from esc_orders o inner join esc_Transactions t on o.Id=t.OrderId where o.order_status='Completed' and t.Status='Pending' and datediff(utc_timestamp(), o.CompletedDate)>7";
                var reader = cmdOrders.ExecuteReader();
                while (reader.Read())
                {
                    OrdersList.Add(new CoachOrder()
                    {
                        Id = reader.GetInt32("OrderId"),
                        receiver_id = reader.GetInt32("SellerId"),
                        mc_gross = reader.GetDecimal("mc_gross")
                    });
                }
            }
            #endregion
            return OrdersList;
        }

        static void SetTransactionCompleted(int OrderId, MySqlConnection connection)
        {
            using (var cmdTransaction = connection.CreateCommand())
            {
                cmdTransaction.CommandText = "update esc_Transactions set Status='Completed' where OrderId=@oid";
                cmdTransaction.Parameters.AddWithValue("@oid", OrderId);
                cmdTransaction.ExecuteNonQuery();
            }
        }

        static void AddAmountInSellerWallet(int sellerId, decimal orderAmount, MySqlConnection connection)
        {
            //--Get the 10% of Order-Amount--
            decimal TenPer_OrderAmount = (orderAmount * 10)/100;
            //--Subtract Ten-Per-Amount from the Order-Amount--
            decimal seller_order_amount = orderAmount - TenPer_OrderAmount;

            //--Get record from 'esc_wallet' table related seller
            DataTable dt_SellerWallet = CheckRecordExistance(sellerId, connection);
            //--Check if Record exists--
            if(dt_SellerWallet.Rows.Count > 0)
            {
                //--Add new-order-amount in the previous wallet amount of seller--
                seller_order_amount = seller_order_amount + Convert.ToDecimal(dt_SellerWallet.Rows[0]["wallet_amount"].ToString());

                #region update Seller-Wallet-Amount by Seller-Id
                using (var cmdWallet = connection.CreateCommand())
                {
                    cmdWallet.CommandText = "update esc_wallet set wallet_amount=@seller_amt,updated_date=@updatedAt where accountId=@sid";
                    cmdWallet.Parameters.AddWithValue("@sid", sellerId);
                    cmdWallet.Parameters.AddWithValue("@seller_amt", seller_order_amount);
                    cmdWallet.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow);
                    cmdWallet.ExecuteNonQuery();
                }
                #endregion
            }
            else
            {
                #region insert Seller-Wallet-Amount in 'esc_wallet' table related seller
                using (var cmdWallet = connection.CreateCommand())
                {
                    cmdWallet.CommandText = "insert into esc_wallet(accountId,wallet_amount,created_date,updated_date)values(@accountId,@seller_amt,@createdAt,@updatedAt)";
                    cmdWallet.Parameters.AddWithValue("@accountId", sellerId);
                    cmdWallet.Parameters.AddWithValue("@seller_amt", seller_order_amount);
                    cmdWallet.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);
                    cmdWallet.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow);
                    cmdWallet.ExecuteNonQuery();
                }
                #endregion
            }
        }

        static DataTable CheckRecordExistance(int sellerId, MySqlConnection connection)
        {
            #region check if record exist in the 'esc_wallet' table by SellerId
            DataTable dt = new DataTable();
            using (var cmdExistance = connection.CreateCommand())
            {
                cmdExistance.CommandText = "SELECT * FROM esc_wallet WHERE accountId=@sid";
                cmdExistance.Parameters.AddWithValue("@sid", sellerId);
                MySqlDataAdapter adp = new MySqlDataAdapter(cmdExistance);
                adp.Fill(dt);
            }
            #endregion
            return dt;
        }

        protected void Application_PostAuthorizeRequest()
        {
            if (IsWebApiRequest())
            {
                HttpContext.Current.SetSessionStateBehavior(SessionStateBehavior.Required);
            }
        }

        protected void Application_BeginRequest()
        {
            if (!Context.Request.IsSecureConnection && !HttpContext.Current.Request.IsLocal)
                Response.Redirect(Context.Request.Url.ToString().Replace("http:", "https:"));
        }

        private bool IsWebApiRequest()
        {
            return  HttpContext.Current.Request.AppRelativeCurrentExecutionFilePath.StartsWith(WebApiConfig.UrlPrefixRelative) ||
                    HttpContext.Current.Request.AppRelativeCurrentExecutionFilePath.StartsWith(WebApiConfig.CRUDPrefixRelative);
        }
    }
}
