﻿namespace CoinbasePro.Services.Withdrawals.Models
{
    public class Coinbase
    {
        public decimal Amount { get; set; }

        public string Currency { get; set; }

        public string CoinbaseAccountId { get; set; }
    }
}
