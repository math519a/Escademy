using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Escademy.ViewModels
{
    public class OrderDataVM
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }
        public int OrderId { get; set; }
        public string Date { get; set; }
        public string OrderStatus  { get; set; }
        public string TransactionId { get; set; }
        public int UserId { get; set; }
    }

    public class EarningListModel
    {
        public string Date { get; set; }
        public string For { get; set; }
        public decimal Amount { get; set; }
        public string TransactionId { get; set; }
        public int UserId { get; set; }
    }

    public class EarningDataModel
    {
        public decimal NetIncome { get; set; }
        public decimal AvailableForWithdrawal { get; set; }
        public decimal Withdrawal { get; set; }
        public int UsedForPurchases { get; set; }
        public decimal PendingClearances { get; set; }
        public decimal TotalWithdrawn { get; set; }
    }

    public class WithdrawalDataModel
    {
        public string EmailID { get; set; }
        public decimal Amount { get; set; }
       
    }
}