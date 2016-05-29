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
        public long Negatives { get; set; }

        public long Total => Postives + Negatives;
        public double Probabilty => Postives / Total;

        public double GrahamScore(long posdocs, long negdocs)
        {
            var p = Postives * 2;
            var n = Negatives;
            if (p + n < 5) return .5;
            double score = Math.Min(1, (p / posdocs)) / (Math.Min(1, (p / posdocs)) + Math.Min(1, (n / negdocs)));
            return Math.Max(.001, Math.Min(.999, score));
        }

        public TokenEntity Increment(long pos, long neg)
        {
            var merged = new TokenEntity(Classification, Token);
            merged.Postives = Postives + pos;
            merged.Negatives = Negatives + neg;
            return merged;
        }
    }
}
 