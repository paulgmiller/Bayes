using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.WindowsAzure.Storage.Table;

namespace Bayes
{
    public class TokenEntity : TableEntity
    {
        public TokenEntity() { }

        public TokenEntity(string classification, string token)
        {
            this.PartitionKey = classification;
            this.RowKey = token;
        }

        public string Classification => this.PartitionKey;
        public string Token => this.RowKey;

        public long Postives { get; set; }
        public long Total { get; set; }

        TokenEntity Merge(TokenEntity other)
        {
            //assert class/token are equal
            var merged = new TokenEntity(Classification, Token);
            merged.Postives = Postives + other.Postives;
            merged.Total = Total + other.Total;
            return merged;
        }
    }
}