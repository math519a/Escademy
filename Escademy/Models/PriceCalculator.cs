using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Escademy.Models
{
    public static class PriceCalculator
    {
        private static decimal OUR_CUT = 3; // %
        
        public static decimal CalculateTotalPrice(int id, int quantity, MySqlConnection connection) {
            decimal product_price = CalculateProductPrice(id, quantity, connection);
            decimal cut = CalculateCut(product_price);
            decimal fee = CalculateFee(PayType.PayPal, product_price + cut);

            return product_price + cut + fee;
        }

        public static decimal CalculateProductPrice(int id, int quantity, MySqlConnection connection)
        {
            decimal finalPrice = 0;
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT Price FROM esc_profilegamesPricing WHERE profilegamesId=@id AND Hours=@qty";
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@qty", quantity);

                var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    finalPrice = (decimal)reader.GetFloat("Price");
                }
            }
            return finalPrice * quantity;
        }

        public static decimal CalculateCut(decimal productPrice)
        {
            return (productPrice / 100) * OUR_CUT; // our cut is 10%
        }

        public static decimal CalculateFee(PayType payType, decimal total) {
            if (payType == PayType.PayPal)
            {
                return (total / 100) * 3.4M + 0.4M; // paypal is 3.4% + 0.4$
            }
            return 0;
        }

        public static decimal CalculateCutFromTotalPriceWithFee(PayType payType, decimal price)
        {
            if (payType == PayType.PayPal)
            {
                //                                         1.1
                //                                          |
                //                                          V
                return ((price - 0.4M) / 1.034M) / (1M + OUR_CUT/100M);
            }
            return price / 1.1M;
        }

        // total = (produkt pris + 10% af produkt pris) + 3.4% af (produkt pris + 10% af produkt pris) + 0.4$

        public enum PayType
        {
            PayPal,
            GooglePay
        }
    }
}